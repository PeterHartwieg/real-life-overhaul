using System;
using System.Collections.Generic;
using RealLifeOverhaul.Faults;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.ModApi;
using UI.Load;

namespace RealLifeOverhaul
{
    /// <summary>Public reads of what Flare, the social cast, casualties and faults already keep. No frame work.</summary>
    internal static class ModApiLinks
    {
        private static readonly List<CityApiCitizen> Citizens = new List<CityApiCitizen>(FlareLiveRules.Citizens);
        private static readonly Func<int, int, ModApiIdentity> ReadIdentity = Identity;
        private static readonly Action<int, IReadOnlyList<FlareCitizen>> PopulationChanged = Prepare;
        private static readonly Func<CityApiStatus> ReadStatus = Status;
        private static readonly Func<bool> ReadShared = () => GoingPublic.InSession;
        private static readonly ModApiBridges.CitizensRead ReadPopulation = Population;
        private static readonly Func<List<int>, CityApiStatus> ReadReservations = Reservations;
        private static readonly Func<List<object>, List<object>, CityApiStatus> ReadBodies = Bodies;
        private static readonly Func<List<object>, List<int>, List<int>, CityApiStatus> ReadLanes = Lanes;
        private static int _citySeed;
        private static bool _failed;

        internal static void Connect()
        {
            FlareBridges.CitizensChanged = PopulationChanged;
            ModApiBridges.Status = ReadStatus;
            ModApiBridges.Shared = ReadShared;
            ModApiBridges.Citizens = ReadPopulation;
            ModApiBridges.ReservedCitizens = ReadReservations;
            ModApiBridges.ReservedBodies = ReadBodies;
            ModApiBridges.SlowedLanes = ReadLanes;
            FlareDirector flare = FlareDirector.Instance;
            if (flare != null && flare.File != null)
            {
                Prepare(FlareLiveRules.CitySeed(flare.File), flare.Citizens);
            }
        }

        // The reads stay answered after the features are disconnected: their directors give back bodies and lanes when
        // they are destroyed, later in the unload, and until then another mod must still see them as held. Each read
        // finds its feature through its Instance, so a destroyed one answers that nothing is held.
        internal static void Disconnect()
        {
            FlareBridges.CitizensChanged = null;
            _failed = false;
        }

        private static CityApiStatus Status()
        {
            if (GoingPublic.InSession)
            {
                return CityApiStatus.Shared;
            }
            if (!ModState.Loaded || SaveGameManager.Current == null)
            {
                return CityApiStatus.NotYet;
            }
            return LoadScene.isLoading ? CityApiStatus.Refreshing : CityApiStatus.Ready;
        }

        // Immutable public objects are prepared on the population's event, never during a public read.
        private static void Prepare(int seed, IReadOnlyList<FlareCitizen> citizens)
        {
            if (_failed || ModApiRules.CheckCitizens(Citizens, _citySeed, seed, citizens.Count, ReadIdentity) != CityApiStatus.Refreshing)
            {
                return;
            }
            Citizens.Clear();
            _citySeed = 0;
            try
            {
                for (int i = 0; i < citizens.Count; i++)
                {
                    Citizens.Add(new CityApiCitizen(Identity(seed, citizens[i])));
                }
                _citySeed = seed;
            }
            catch (Exception e)
            {
                Citizens.Clear();
                _failed = true;
                Wiring.Fault("ModApiLinks.Prepare", e);
            }
        }

        private static ModApiIdentity Identity(int seed, int index)
        {
            return Identity(seed, FlareDirector.Instance.Citizens[index]);
        }

        private static ModApiIdentity Identity(int seed, FlareCitizen citizen)
        {
            FlareAvatarPlan avatar = FlareAvatarRules.Citizen(seed, citizen.Index);
            return new ModApiIdentity
            {
                Index = citizen.Index, First = citizen.First, Last = citizen.Last, District = citizen.District,
                Age = citizen.Age, Female = citizen.Female, Persona = (int)citizen.Persona,
                AppearanceSeed = avatar.Seed, AppearanceAge = avatar.Age, AppearanceFemale = avatar.Female
            };
        }

        private static CityApiStatus Population(List<CityApiCitizen> into, out int citySeed)
        {
            citySeed = 0;
            if (!Settings.SocialMedia)
            {
                return CityApiStatus.Off;
            }
            FlareDirector flare = FlareDirector.Instance;
            if (flare == null)
            {
                return CityApiStatus.NotYet;
            }
            if (_failed || flare.Faulted || FeatureGuards.FeatureOff("flare"))
            {
                return CityApiStatus.Failed;
            }
            if (!flare.Running)
            {
                return CityApiStatus.Off;
            }
            if (flare.File == null)
            {
                return CityApiStatus.NotYet;
            }
            int seed = FlareLiveRules.CitySeed(flare.File);
            CityApiStatus status = ModApiRules.ReadCitizens(Citizens, _citySeed, seed,
                flare.Citizens.Count, ReadIdentity, into);
            citySeed = status == CityApiStatus.Ready ? seed : 0;
            return status;
        }

        // A Flare person said yes online and is out with the player: their contact carries the Flare name and face
        // (FlareInviteLinks.MeetOnline; the age is clamped there, so it is not compared). The prepared citizens are kept
        // when Flare stops, so somebody still out is still found. Nobody out costs nothing.
        private static CityApiStatus Reservations(List<int> indexes)
        {
            SocialCast cast = SocialDirector.Instance?.Cast;
            if (cast == null)
            {
                return CityApiStatus.Ready;
            }
            for (int slot = 0; slot < SocialCast.Size; slot++)
            {
                int person = cast.OutPerson(slot);
                if (person < 0)
                {
                    continue;
                }
                SocialPerson who = person < SocialState.People.Length ? SocialState.People[person] : null;
                FlareDirector flare = FlareDirector.Instance;
                if (who == null || Citizens.Count == 0
                    || (flare != null && flare.File != null && FlareLiveRules.CitySeed(flare.File) != _citySeed))
                {
                    return CityApiStatus.Refreshing;
                }
                ModApiRules.Reserve(Citizens, who.Name, who.Seed, indexes);
            }
            return CityApiStatus.Ready;
        }

        // Whatever RLO holds is listed, whether or not its option is still on: a body or a lane is given back by its own
        // feature, not by this read.
        private static CityApiStatus Bodies(List<object> pedestrians, List<object> characters)
        {
            SocialDirector.Instance?.Cast?.AppendBodies(characters);
            PedestrianIncidents.Instance?.AppendBodies(pedestrians);
            RagdollSystem.Instance?.AppendBodies(characters);
            return CityApiStatus.Ready;
        }

        private static CityApiStatus Lanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        {
            FaultDirector.Instance?.AppendSlowedLanes(lanes, originalLimits, currentLimits);
            return CityApiStatus.Ready;
        }
    }
}
