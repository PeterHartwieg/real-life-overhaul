using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.ModApi;

/// <summary>Pins the public read contract, identity consistency, reservation matching and the cost of repeated reads.</summary>
internal static partial class Program
{
    private static readonly string[] ApiPersonas =
    {
        "Commuter", "Foodie", "Parent", "Student", "Retiree", "Sporty", "Grumpy", "NightOwl"
    };
    private static readonly string[] ApiDistricts =
    {
        "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_hellskitchen"
    };
    private static readonly Func<int, int, ModApiIdentity> ApiReadIdentity = ApiIdentity;
    private static readonly object ApiPedestrian = new object(), ApiCharacter = new object(), ApiLane = new object();
    private static readonly Exception ApiFault = new InvalidOperationException("A read fault");
    private static List<CityApiCitizen> _apiPrepared;
    private static ModApiIdentity[] _apiLive;
    private static int _apiSeed;
    private static CityApiStatus _apiStatus;
    private static bool _apiThrow;

    private static void ModApi()
    {
        int before = _run, failedBefore = _failed;
        ApiShape();
        ApiPopulation();
        ApiReservations();
        ApiBoundary();
        ApiCost();
        ApiUnwire();
        Console.WriteLine("ModApi: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failures");
    }

    private static void ApiShape()
    {
        Check(CityApi.ContractVersion == 1, "CityApi contract version is 1");
        string[] statuses = { "Ready", "Off", "NotYet", "Refreshing", "Shared", "Failed" };
        Check(Enum.GetUnderlyingType(typeof(CityApiStatus)) == typeof(int), "CityApiStatus has int storage");
        Check(Enum.GetNames(typeof(CityApiStatus)).Length == statuses.Length, "CityApiStatus has exactly six outcomes");
        for (int i = 0; i < statuses.Length; i++)
        {
            Check(Enum.GetName(typeof(CityApiStatus), i) == statuses[i], "CityApiStatus " + statuses[i] + " = " + i);
        }
        Check(Enum.GetNames(typeof(FlarePersona)).Length == 8, "Flare has eight contract personas");
        for (int i = 0; i < ApiPersonas.Length; i++)
        {
            Check(Enum.GetName(typeof(FlarePersona), i) == ApiPersonas[i] && ModApiRules.Persona(i) == ApiPersonas[i],
                "CityApi persona " + i + " is " + ApiPersonas[i]);
        }
        Check(ModApiRules.Persona(-1) == null && ModApiRules.Persona(8) == null, "CityApi rejects unknown personas");
        Type citizen = typeof(CityApiCitizen);
        Check(citizen.FullName == "RealLifeOverhaul.ModApi.CityApiCitizen" && citizen.IsPublic && citizen.IsSealed,
            "CityApiCitizen is public and sealed in the contract namespace");
        string[] fields = { "Index", "First", "Last", "District", "Age", "Female", "Persona", "AppearanceSeed" };
        Type[] types = { typeof(int), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(string), typeof(int) };
        FieldInfo[] publicFields = citizen.GetFields(BindingFlags.Public | BindingFlags.Instance);
        Check(publicFields.Length == fields.Length, "CityApiCitizen has eight public instance fields");
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = citizen.GetField(fields[i]);
            Check(field != null && field.FieldType == types[i] && field.IsInitOnly && !field.IsStatic,
                "CityApiCitizen." + fields[i] + " is readonly " + types[i].Name);
        }
        PropertyInfo version = typeof(CityApi).GetProperty("ContractVersion");
        Check(version != null && version.PropertyType == typeof(int) && version.GetMethod.IsStatic && !version.CanWrite,
            "CityApi.ContractVersion is a public static int getter");
        ApiMethod("ReadCitizens", new[] { typeof(List<CityApiCitizen>), typeof(int).MakeByRefType() }, new[] { "into", "citySeed" });
        ApiMethod("ReadReservedCitizens", new[] { typeof(List<int>) }, new[] { "indexes" });
        ApiMethod("ReadReservedBodies", new[] { typeof(List<object>), typeof(List<object>) }, new[] { "pedestrians", "characters" });
        ApiMethod("ReadSlowedLanes", new[] { typeof(List<object>), typeof(List<int>), typeof(List<int>) },
            new[] { "lanes", "originalLimits", "currentLimits" });
    }

    private static void ApiMethod(string name, Type[] parameters, string[] names)
    {
        MethodInfo method = typeof(CityApi).GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
        Check(method != null && method.ReturnType == typeof(CityApiStatus), "CityApi." + name + " parameter and return types");
        if (method == null) return;
        ParameterInfo[] found = method.GetParameters();
        for (int i = 0; i < found.Length; i++)
        {
            Check(found[i].Name == names[i] && found[i].IsOut == (name == "ReadCitizens" && i == 1),
                "CityApi." + name + " parameter " + names[i] + " and out flag");
        }
    }

    private static ModApiIdentity ApiIdentity(int seed, int index) => _apiLive[index];

    private static void ApiCity(int seed, IReadOnlyList<string> districts)
    {
        var file = new FlareFile { Seed = seed };
        _apiSeed = FlareLiveRules.CitySeed(file);
        var citizens = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(_apiSeed, districts, citizens);
        _apiLive = new ModApiIdentity[citizens.Count];
        _apiPrepared = new List<CityApiCitizen>(citizens.Count);
        for (int i = 0; i < citizens.Count; i++)
        {
            FlareCitizen c = citizens[i];
            FlareAvatarPlan avatar = FlareAvatarRules.Citizen(_apiSeed, c.Index);
            var identity = new ModApiIdentity
            {
                Index = c.Index, First = c.First, Last = c.Last, District = c.District, Age = c.Age, Female = c.Female,
                Persona = (int)c.Persona, AppearanceSeed = avatar.Seed, AppearanceAge = avatar.Age, AppearanceFemale = avatar.Female
            };
            _apiLive[i] = identity;
            _apiPrepared.Add(new CityApiCitizen(identity));
        }
    }

    private static CityApiStatus ApiReadPopulation(List<CityApiCitizen> into, out int citySeed)
    {
        citySeed = _apiSeed;
        if (_apiThrow)
        {
            into.Add(_apiPrepared[0]);
            throw ApiFault;
        }
        return ModApiRules.ReadCitizens(_apiPrepared, _apiSeed, _apiSeed, _apiLive.Length, ApiReadIdentity, into);
    }

    private static void ApiPopulation()
    {
        ApiWire();
        var into = new List<CityApiCitizen>(400);
        foreach (int seed in new[] { 73129, 1, -9543, int.MaxValue, int.MinValue })
        {
            ApiCity(seed, ApiDistricts);
            into.Add(_apiPrepared[0]);
            Check(CityApi.ReadCitizens(into, out int foundSeed) == CityApiStatus.Ready && foundSeed == seed && into.Count == 400,
                "CityApi clears the list and reads all 400 people for seed " + seed);
            for (int i = 0; i < 400; i++)
            {
                ModApiIdentity c = _apiLive[i];
                FlareAvatarPlan avatar = FlareAvatarRules.Citizen(seed, i);
                CityApiCitizen read = into[i];
                Check(read.Index == i && read.First == c.First && read.Last == c.Last && read.District == c.District
                    && read.Age == c.Age && read.Female == c.Female && read.Persona == ApiPersonas[c.Persona]
                    && read.AppearanceSeed == avatar.Seed && read.Age == avatar.Age && read.Female == avatar.Female,
                    "CityApi seed " + seed + " person " + i + " agrees with every Flare identity and avatar field");
            }
            string digest = ApiDigest(into);
            ApiCity(seed, ApiDistricts);
            CityApi.ReadCitizens(into, out _);
            Check(ApiDigest(into) == digest, "CityApi determinism for every identity field, seed " + seed);
            if (seed == 73129)
            {
                Check(digest == "2DF27EEE8664159B3310FFD1AA2D65A71A619D54D89F7C7BD09D975F68B642FD", "CityApi contract 1 population fingerprint: " + digest);
            }
        }
        ApiCity(73129, ApiDistricts);
        ModApiIdentity last = _apiLive[399];
        _apiLive[399].Index = 398;
        ApiRefreshing(into, "duplicate index");
        _apiLive[399] = last;
        _apiLive[399].First = "";
        ApiRefreshing(into, "empty first name");
        _apiLive[399] = last;
        _apiLive[399].Last = null;
        ApiRefreshing(into, "missing surname");
        _apiLive[399] = last;
        _apiLive[399].AppearanceSeed = 0;
        ApiRefreshing(into, "missing face seed");
        _apiLive[399] = last;
        _apiLive[399].Persona = 8;
        ApiRefreshing(into, "unknown persona");
        _apiLive[399] = last;
        _apiLive[399].District = "another district";
        ApiRefreshing(into, "changed district");
        _apiLive[399] = last;
        _apiLive[399].AppearanceAge++;
        ApiRefreshing(into, "changed avatar age");
        _apiLive[399] = last;
        _apiLive[399].AppearanceFemale = !last.AppearanceFemale;
        ApiRefreshing(into, "changed avatar sex");
        _apiLive[399] = last;
        var one = new List<CityApiCitizen> { _apiPrepared[0] };
        Check(ModApiRules.ReadCitizens(one, _apiSeed, _apiSeed, 1, ApiReadIdentity, into) == CityApiStatus.Ready
            && into.Count == 1 && ReferenceEquals(one[0], into[0]), "CityApi one person reuses the immutable object");
        Check(ModApiRules.ReadCitizens(null, _apiSeed, _apiSeed, 400, ApiReadIdentity, into) == CityApiStatus.Refreshing
            && into.Count == 0, "CityApi an unprepared population is Refreshing and clears the list");
        for (int i = 0; i < 400; i++)
        {
            ModApiIdentity original = _apiLive[i];
            _apiLive[i].Age++;
            into.Add(_apiPrepared[0]);
            Check(CityApi.ReadCitizens(into, out int seed) == CityApiStatus.Refreshing && into.Count == 0 && seed == 0,
                "CityApi age disagreement at " + i + " is Refreshing with no partial population");
            _apiLive[i] = original;
            _apiLive[i].Female = !original.Female;
            Check(CityApi.ReadCitizens(into, out seed) == CityApiStatus.Refreshing && into.Count == 0 && seed == 0,
                "CityApi sex disagreement at " + i + " is Refreshing with no partial population");
            _apiLive[i] = original;
        }
        Check(ModApiRules.ReadCitizens(_apiPrepared, 2, _apiSeed, 400, ApiReadIdentity, into) == CityApiStatus.Refreshing
            && into.Count == 0, "CityApi a new seed cannot read the previous population");
        Check(ModApiRules.ReadCitizens(_apiPrepared, _apiSeed, _apiSeed, 399, ApiReadIdentity, into) == CityApiStatus.Refreshing,
            "CityApi a partial population is Refreshing");
        into.Add(_apiPrepared[0]);
        Check(ModApiRules.ReadCitizens(_apiPrepared, _apiSeed, 0, 400, ApiReadIdentity, into) == CityApiStatus.NotYet
            && into.Count == 0, "CityApi no seed is NotYet and clears the list");
        Check(ModApiRules.ReadCitizens(_apiPrepared, _apiSeed, _apiSeed, 0, ApiReadIdentity, into) == CityApiStatus.NotYet,
            "CityApi no people is NotYet");
        ApiCity(73129, null);
        Check(CityApi.ReadCitizens(into, out _) == CityApiStatus.Ready && into[0].District == null,
            "CityApi unknown districts stay null");
        ApiCity(73129, ApiDistricts);
    }

    private static void ApiRefreshing(List<CityApiCitizen> into, string reason)
    {
        into.Add(_apiPrepared[0]);
        Check(CityApi.ReadCitizens(into, out int seed) == CityApiStatus.Refreshing && into.Count == 0 && seed == 0,
            "CityApi " + reason + " is Refreshing and leaves no partial output");
    }

    private static string ApiDigest(IReadOnlyList<CityApiCitizen> citizens)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            for (int i = 0; i < citizens.Count; i++)
            {
                CityApiCitizen c = citizens[i];
                writer.Write(c.Index);
                writer.Write(c.First);
                writer.Write(c.Last);
                writer.Write(c.District ?? "");
                writer.Write(c.Age);
                writer.Write(c.Female);
                writer.Write(c.Persona);
                writer.Write(c.AppearanceSeed);
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    private static void ApiReservations()
    {
        CityApiCitizen citizen = _apiPrepared[0];
        string name = citizen.First + " " + citizen.Last;
        Check(ModApiRules.Matches(citizen, name, citizen.AppearanceSeed), "CityApi reserves a matching full name and face");
        Check(!ModApiRules.Matches(citizen, name, citizen.AppearanceSeed + 1), "CityApi never reserves on name alone");
        Check(!ModApiRules.Matches(citizen, "Another Person", citizen.AppearanceSeed), "CityApi never reserves on face alone");
        Check(!ModApiRules.Matches(citizen, name.ToUpperInvariant(), citizen.AppearanceSeed), "CityApi name matching is ordinal");
        Check(!ModApiRules.Matches(citizen, name + " ", citizen.AppearanceSeed), "CityApi name matching is exact");
        Check(!ModApiRules.Matches(citizen, null, citizen.AppearanceSeed) && !ModApiRules.Matches(null, name, 1)
            && !ModApiRules.Matches(citizen, name, 0), "CityApi missing reservation identity never matches");
        Check(ModApiRules.NameIs(citizen.First, citizen.Last, name) && !ModApiRules.NameIs(citizen.First, citizen.Last, citizen.First + citizen.Last)
            && !ModApiRules.NameIs(citizen.First, citizen.Last, citizen.Last + " " + citizen.First) && !ModApiRules.NameIs(citizen.First, citizen.Last, null)
            && !ModApiRules.NameIs("", citizen.Last, " " + citizen.Last), "CityApi compares a contact's name with a Flare first and last name exactly");
        var reserved = new List<int> { 7 };
        CityApiCitizen other = _apiPrepared[5];
        ModApiRules.Reserve(_apiPrepared, name, citizen.AppearanceSeed, reserved);
        ModApiRules.Reserve(_apiPrepared, name, citizen.AppearanceSeed, reserved);
        ModApiRules.Reserve(_apiPrepared, other.First + " " + other.Last, other.AppearanceSeed, reserved);
        Check(reserved.Count == 3 && reserved[0] == 7 && reserved[1] == citizen.Index && reserved[2] == other.Index,
            "CityApi reserves each matching citizen once and keeps what the list held");
        ModApiRules.Reserve(_apiPrepared, name, other.AppearanceSeed, reserved);
        ModApiRules.Reserve(_apiPrepared, other.First + " " + other.Last, citizen.AppearanceSeed, reserved);
        ModApiRules.Reserve(_apiPrepared, "Nobody Here", 0, reserved);
        ModApiRules.Reserve(new List<CityApiCitizen>(), name, citizen.AppearanceSeed, reserved);
        Check(reserved.Count == 3, "CityApi reserves nobody for a name with another's face, an unknown contact or no citizens");
        foreach (int age in new[] { 1, 17, 18, 85, 86, 130 })
        {
            ModApiIdentity identity = _apiLive[0];
            identity.Age = age;
            var aged = new CityApiCitizen(identity);
            var person = new RealLifeOverhaul.SocialPerson { Name = name, Seed = identity.AppearanceSeed, Age = Math.Clamp(age, 18, 85) };
            Check(ModApiRules.Matches(aged, person.Name, person.Seed), "CityApi reserved matching ignores clamped age " + age);
        }
    }

    private static CityApiStatus ApiReadReservations(List<int> indexes)
    {
        indexes.Add(3);
        if (_apiThrow) throw ApiFault;
        return _apiStatus;
    }

    private static CityApiStatus ApiReadBodies(List<object> pedestrians, List<object> characters)
    {
        pedestrians.Add(ApiPedestrian);
        characters.Add(ApiCharacter);
        if (_apiThrow) throw ApiFault;
        return _apiStatus;
    }

    private static CityApiStatus ApiReadLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
    {
        lanes.Add(ApiLane);
        originalLimits.Add(50);
        currentLimits.Add(35);
        if (_apiThrow) throw ApiFault;
        return _apiStatus;
    }

    private static void ApiWire()
    {
        _apiStatus = CityApiStatus.Ready;
        _apiThrow = false;
        ModApiBridges.Status = () => CityApiStatus.Ready;
        ModApiBridges.Citizens = ApiReadPopulation;
        ModApiBridges.ReservedCitizens = ApiReadReservations;
        ModApiBridges.ReservedBodies = ApiReadBodies;
        ModApiBridges.SlowedLanes = ApiReadLanes;
    }

    private static void ApiUnwire()
    {
        ModApiBridges.Status = null;
        ModApiBridges.Shared = null;
        ModApiBridges.Citizens = null;
        ModApiBridges.ReservedCitizens = null;
        ModApiBridges.ReservedBodies = null;
        ModApiBridges.SlowedLanes = null;
    }

    private static void ApiBoundary()
    {
        var citizens = new List<CityApiCitizen>(400);
        var indexes = new List<int>(2);
        var pedestrians = new List<object>(1);
        var characters = new List<object>(1);
        var lanes = new List<object>(1);
        var original = new List<int>(1);
        var current = new List<int>(1);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Ready && indexes.Count == 1 && indexes[0] == 3,
                "CityApi clears reserved citizens before filling");
            Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Ready
                && pedestrians.Count == 1 && ReferenceEquals(pedestrians[0], ApiPedestrian)
                && characters.Count == 1 && ReferenceEquals(characters[0], ApiCharacter), "CityApi clears both body lists before filling");
            Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Ready && lanes.Count == 1
                && ReferenceEquals(lanes[0], ApiLane) && original.Count == 1 && original[0] == 50
                && current.Count == 1 && current[0] == 35, "CityApi clears all lane lists and keeps limits aligned");
        }
        foreach (CityApiStatus status in Enum.GetValues<CityApiStatus>())
        {
            if (status == CityApiStatus.Ready) continue;
            ModApiBridges.Status = () => status;
            Check(CityApi.ReadCitizens(citizens, out int seed) == status && seed == 0 && citizens.Count == 0,
                "CityApi population boundary clears on " + status);
            // What RLO holds does not wait for the city: only a shared session withholds it.
            Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Ready && indexes.Count == 1,
                "CityApi reservations are read while the citizens' read is " + status);
            Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Ready && pedestrians.Count == 1 && characters.Count == 1,
                "CityApi bodies are read while the citizens' read is " + status);
            Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Ready && lanes.Count == 1,
                "CityApi lanes are read while the citizens' read is " + status);
        }
        ModApiBridges.Status = () => CityApiStatus.Ready;
        ModApiBridges.Shared = () => true;
        Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Shared && indexes.Count == 0, "CityApi reservation boundary clears on Shared");
        Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Shared && pedestrians.Count == 0 && characters.Count == 0,
            "CityApi body boundary clears on Shared");
        Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Shared && lanes.Count == 0 && original.Count == 0 && current.Count == 0,
            "CityApi lane boundary clears on Shared");
        ModApiBridges.Shared = null;
        ApiWire();
        _apiStatus = CityApiStatus.Refreshing;
        Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Refreshing && indexes.Count == 0,
            "CityApi discards partial reservations on Refreshing");
        Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Refreshing && pedestrians.Count == 0 && characters.Count == 0,
            "CityApi discards partial body reads on Refreshing");
        Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Refreshing
            && lanes.Count == 0 && original.Count == 0 && current.Count == 0, "CityApi discards partial lane reads on Refreshing");
        _apiThrow = true;
        Check(CityApi.ReadCitizens(citizens, out int failedSeed) == CityApiStatus.Failed && failedSeed == 0 && citizens.Count == 0,
            "CityApi a population fault never escapes or returns a partial list");
        Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Failed && indexes.Count == 0, "CityApi a reservation fault clears the list");
        Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Failed && pedestrians.Count == 0 && characters.Count == 0,
            "CityApi a body fault clears both lists");
        Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Failed
            && lanes.Count == 0 && original.Count == 0 && current.Count == 0, "CityApi a lane fault clears all lists");
        ApiWire();
        Check(CityApi.ReadCitizens(null, out failedSeed) == CityApiStatus.Failed && failedSeed == 0
            && CityApi.ReadReservedCitizens(null) == CityApiStatus.Failed, "CityApi null single outputs fail safely");
        characters.Add(ApiCharacter);
        Check(CityApi.ReadReservedBodies(null, characters) == CityApiStatus.Failed && characters.Count == 0,
            "CityApi clears the other body list when one is null");
        Check(CityApi.ReadReservedBodies(characters, characters) == CityApiStatus.Failed && characters.Count == 0,
            "CityApi aliased body lists fail safely");
        original.Add(50);
        Check(CityApi.ReadSlowedLanes(null, original, current) == CityApiStatus.Failed && original.Count == 0,
            "CityApi clears the other lane lists when one is null");
        Check(CityApi.ReadSlowedLanes(lanes, original, original) == CityApiStatus.Failed && original.Count == 0,
            "CityApi aliased limit lists fail safely");
        ApiUnwire();
        Check(CityApi.ReadCitizens(citizens, out failedSeed) == CityApiStatus.NotYet && failedSeed == 0,
            "CityApi unwired city is NotYet");
        ModApiBridges.Status = () => CityApiStatus.Ready;
        Check(CityApi.ReadReservedCitizens(indexes) == CityApiStatus.Ready && indexes.Count == 0,
            "CityApi absent reservation feature is Ready and empty");
        Check(CityApi.ReadReservedBodies(pedestrians, characters) == CityApiStatus.Ready && pedestrians.Count == 0 && characters.Count == 0,
            "CityApi absent body features are Ready and empty");
        Check(CityApi.ReadSlowedLanes(lanes, original, current) == CityApiStatus.Ready && lanes.Count == 0 && original.Count == 0 && current.Count == 0,
            "CityApi absent lane feature is Ready and empty");
        ApiWire();
    }

    private static void ApiCost()
    {
        var citizens = new List<CityApiCitizen>(400);
        var indexes = new List<int>(2);
        var pedestrians = new List<object>(1);
        var characters = new List<object>(1);
        var lanes = new List<object>(1);
        var original = new List<int>(1);
        var current = new List<int>(1);
        for (int i = 0; i < 100; i++)
        {
            CityApi.ReadCitizens(citizens, out _);
            CityApi.ReadReservedCitizens(indexes);
            CityApi.ReadReservedBodies(pedestrians, characters);
            CityApi.ReadSlowedLanes(lanes, original, current);
        }
        var watch = new Stopwatch();
        string name = _apiPrepared[0].First + " " + _apiPrepared[0].Last;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < 2000; i++)
        {
            CityApi.ReadCitizens(citizens, out _);
            CityApi.ReadReservedCitizens(indexes);
            CityApi.ReadReservedBodies(pedestrians, characters);
            CityApi.ReadSlowedLanes(lanes, original, current);
            ModApiRules.Matches(citizens[0], name, citizens[0].AppearanceSeed);
        }
        watch.Stop();
        long cost = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(cost == 0, "CityApi repeated reads allocate 0 bytes with caller capacity: " + cost);
        Check(watch.Elapsed.TotalMilliseconds / 2000 < 1, "CityApi four reads, 400 people, budget of 1 ms per set");
    }
}
