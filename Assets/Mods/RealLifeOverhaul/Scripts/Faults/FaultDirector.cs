using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// Real faults in the city that are worth reporting (Scripts/Faults): street lamps gone dark, potholes, litter, crime
    /// hot spots and unsafe places, rolled at random as the game runs (FaultRules, with a real source of chance:
    /// FaultDice), weighted by the city's state, kept in the save (FaultStore) and shown in the street (FaultWorld).
    ///
    ///   the hour     the city's runner (the player alone, or the host of a Going Public session) turns the faults
    ///                (mended ones go, old hot spots and heaps end), rolls new ones and finds each a place (FaultGame),
    ///                and the city's people report some on Flare (FaultBridges.CityReport). A guest rolls nothing: it takes
    ///                the host's faults whole (Core/SharedCity, part K: FaultShare) and shows them.
    ///   reports      Flare's City Office asks before it acts (FaultDirector.Reports.cs, through Wiring/FaultLinks): a
    ///                report of a real fault sends a crew and the fault is gone when it is done; a report of nothing is
    ///                answered so and, by the option, costs the player.
    ///   the street   a dark lamp near the player after dark scales street crime there (FaultBridges.CrimeScale); litter
    ///                in the player's district keeps the dirty streets' mood, and near a business's door costs it
    ///                customers (<see cref="FootfallOf"/>); a pothole jolts and wears the player's car (<see cref="RoadHit"/>,
    ///                from Transport/CarOwnership's own drive tick); an unsafe place trips a passer-by now and then.
    ///
    /// Off (Settings.CityFaults): nothing breaks, nothing is shown, reports are answered as before; the save's line is kept.
    /// COST: nothing per frame but one time comparison. Once a second a few comparisons; every few seconds a pass over a
    /// dozen faults; the roll once a game hour.
    /// </summary>
    public sealed partial class FaultDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("faults", "FaultDirector", typeof(FaultDirector));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Faults: FaultDirector.Update");
        private static readonly int HotHour = HotSpots.Slot("Faults: FaultDirector.Hour");

        private const float SecondEvery = 1f;      // real seconds between two looks
        private const float SyncEvery = 2f;        // ...between two looks at what is near the player
        private const float TripEvery = 20f;       // ...between two chances of a passer-by tripping at an unsafe place near
        private const float TripNear = 60f;        // metres: the player near enough to an unsafe place to see it
        private const float TripWithin = 6f;       // metres: a passer-by this near it may trip
        private const float CrimeFrom = 150f;      // metres: a crime is in the district of the nearest door this near
        private const int MoodHours = 2;           // the dirty streets' mood kept so many hours at a time

        public static IModLogger Logger;
        public static FaultDirector Instance { get; private set; }

        private FaultFile _file;
        private GameInstance _save;
        private bool _loaded, _failed;
        private float _next, _nextHook, _nextSync, _nextTrip;
        private int _hour = -1;
        private float _crimeScale = 1f;
        private Action _onSave;
        private FaultWorld _world;
        private readonly IFaultDice _dice = new FaultDice();   // a real source of chance, never the save's (FaultDice)
        private readonly FaultFacts _facts = new FaultFacts();
        private readonly List<FaultWant> _wants = new List<FaultWant>(4);
        private readonly List<Fault> _seen = new List<Fault>(4);
        private readonly List<int> _gone = new List<int>(4);
        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(8);
        private readonly List<Vector3> _doors = new List<Vector3>(8);
        private readonly Dictionary<BuildingRegistration, int> _footfall = new Dictionary<BuildingRegistration, int>(8);

        /// <summary>The faults while a save runs them; null otherwise.</summary>
        public FaultFile File => _loaded ? _file : null;

        /// <summary>The lanes the world holds, without running its sync or starting the feature.</summary>
        internal void AppendSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        {
            _world?.AppendSlowedLanes(lanes, originalLimits, currentLimits);
        }

        /// <summary>True on a guest of a Going Public session that follows the host's faults: nothing is rolled here.</summary>
        public static bool Following => SharedCity.Follows(FaultShare.Part);

        /// <summary>True while the faults are real in this game: the City Office checks reports, the city's people report only real ones.</summary>
        public static bool Real => Instance != null && Instance._loaded && !Instance._failed && Settings.CityFaults;

        /// <summary>The dark lamps' lights switched off again now (something else, the performance tools putting back what they held, may have switched one on).</summary>
        public static void KeepDark()
        {
            FaultDirector d = Instance;
            if (d != null && d._loaded)
            {
                d._world.KeepDark();
            }
        }

        private void Awake()
        {
            Instance = this;
            _onSave = OnSaveGame;
            _world = new FaultWorld(transform);
            FaultGame.Logger = Logger;
            ScenePrewarm.AddLate("faults: the city's doors, districts and street lamps read for its faults", FaultGame.Prewarm);
            SharedCity.Register(new SharedCityPart
            {
                Id = FaultShare.Part, Name = "the city's faults",
                Ready = () => _loaded && !_failed && Settings.CityFaults,
                Write = () => _loaded && !_failed && Settings.CityFaults ? FaultShare.Write(_file) : null,
                Apply = (text, whole) => FromHost(text),
                Left = () => Logger?.Info("City faults: the host's faults are no longer followed; this game's own roll from here (" + (_file != null ? _file.Faults.Count : 0) + " kept).")
            });
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                Stop("the mod is unloading");
                _world.Dispose();
                FaultGame.Forget();   // (the city's lists let go with the director; a new save or a switch off and on keeps them: FaultGame.Ready tells another city)
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- running

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + SecondEvery;
            if (_failed)
            {
                return;
            }
            try
            {
                Second(now);
            }
            catch (Exception e)
            {
                Fault("its second", e);
            }
        }

        private void Second(float now)
        {
            if (!Settings.CityFaults)
            {
                if (_loaded)
                {
                    Stop("switched off");
                }
                return;
            }
            GameInstance save = SaveGameManager.Current;
            if (_loaded && !ReferenceEquals(save, _save))
            {
                Stop("another save");
            }
            if (save == null || LoadScene.isLoading || !ModState.Loaded || Helpers.PlayerHelper.PlayerController == null)
            {
                return;
            }
            if (!_loaded)
            {
                Load(save);
            }
            if (now >= _nextHook)
            {
                _nextHook = now + 2f;
                SaveHook.Keep(_onSave);   // the game clears its event hub on every load: the save hook is kept attached
            }
            if (!FaultGame.Complete)
            {
                FaultGame.EnsureHidden();   // (behind a black screen only; meanwhile nothing new breaks)
            }
            if (save.Hour != _hour)
            {
                _hour = save.Hour;
                Hour();
            }
            if (now >= _nextSync)
            {
                _nextSync = now + SyncEvery;
                Near(now);
            }
        }

        private void Load(GameInstance save)
        {
            _save = save;
            string line = null;
            _file = save.modData != null && save.modData.TryGetValue(FaultStore.Key, out line) ? FaultStore.Read(line) : new FaultFile();
            if (_file == null)
            {
                SaveLines.Unreadable(save.modData, FaultStore.Key, "it could not be read");   // (put back as it was read at every save: Core/SaveLines)
                Logger?.Warn("City faults: the save's line of faults could not be read (" + FaultStore.Key + "); the city starts with none and the line is kept in the save as it is.");
                _file = new FaultFile();
            }
            _loaded = true;
            _hour = save.Hour;
            Logger?.Info("City faults: " + (line != null ? "read from the save" : "started in this save") + ": " + _file.Faults.Count + " fault(s) in the city"
                         + (Following ? "; the host's faults are followed (nothing is rolled here)." : "; new ones are rolled each game hour, at random."));
            Changed(false);
        }

        private void Stop(string why)
        {
            if (!_loaded)
            {
                return;
            }
            _world.Clear();
            SetCrime(1f);
            ClearFootfall();
            _loaded = false;
            _save = null;
            _file = null;
            _hour = -1;
            Logger?.Info("City faults: stopped (" + why + "); every lamp, lane and prop put back as the game had it.");
        }

        /// <summary>Everything of the faults off the street (the save is being prepared for the mod's removal): lamps, lanes and props put back.</summary>
        public void StopAll()
        {
            Stop("taken off the street");
        }

        /// <summary>What was remembered of this save is gone (its line was removed): the city starts again with no faults.</summary>
        public void Forget()
        {
            if (_loaded)
            {
                _file = new FaultFile();
                Changed(true);
            }
        }

        private void Fault(string where, Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("City faults: switched off for this session after an error in " + where + ". Nothing more breaks; what was shown is put back.");
            try
            {
                Stop("an error");
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
        }

        private void OnSaveGame()
        {
            try
            {
                if (_loaded && _save != null && _save.modData != null)
                {
                    _save.modData[FaultStore.Key] = FaultStore.Write(_file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the hour

        private void Hour()
        {
            using var hotSpot = HotSpots.Time(HotHour);
            long now = FaultGame.Now();
            bool changed = false;
            if (!Following && !GoingPublic.IsGuest)   // (a guest never rolls, even while the host sends no faults)
            {
                _gone.Clear();
                changed |= FaultRules.Turn(_file, now, Settings.Civic ? CivicPolicy.Position(Lever.Cleaning) : 0, _gone);
                if (_gone.Count > 0)
                {
                    Logger?.Info("City faults: " + _gone.Count + " fault(s) over this hour (mended, swept or the crimes have stopped).");
                }
                changed |= Roll(now);
                if (FaultRules.Seen(_file, now, _dice, _seen) > 0)
                {
                    changed = true;
                    for (int i = 0; i < _seen.Count; i++)
                    {
                        Fault f = _seen[i];
                        bool posted = FaultBridges.CityReport != null && FaultBridges.CityReport(FaultRules.ReportOf(f.Kind), f.District, f.Address, new Vector3(f.X, f.Y, f.Z));
                        Logger?.Info("City faults: one of the city's people reports the " + f.Kind + " at " + Where(f) + (posted ? " on Flare" : " (Flare does not run: not posted)")
                                     + "; reported " + f.Reports + " time(s) so far.");
                    }
                }
            }
            if (changed)
            {
                Changed(true);
            }
            if (FaultBridges.LitterMood != null && FaultRules.LitterIn(_file, FaultGame.PlayerDistrict()))
            {
                FaultBridges.LitterMood(MoodHours);
            }
        }

        // The hour's roll: what breaks, and where. True if anything did.
        private bool Roll(long now)
        {
            if (!FaultGame.Ready)
            {
                return false;
            }
            FaultGame.Read(_facts, now, Settings.CityFaultsCount);
            int wanted = FaultRules.Roll(_file, _facts, _dice, _wants);
            int made = 0;
            for (int i = 0; i < _wants.Count; i++)
            {
                FaultWant w = _wants[i];
                if (!FaultGame.Place(w, _dice, _file, out Vector3 at, out string address))
                {
                    continue;
                }
                Fault f = FaultRules.Add(_file, w.Kind, _facts.Districts[w.District].Id, address, at.x, at.y, at.z, w.Heading, w.Variant, now);
                made++;
                Logger?.Info("City faults: " + Words(f.Kind) + " at " + Where(f) + " (rolled at random: " + FaultRules.Chance(f.Kind, _facts, _file) + " in a thousand this hour).");
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("City faults check (the hour's roll): " + _file.Faults.Count + " fault(s), at most " + FaultRules.MostOf(Settings.CityFaultsCount) + "; " + wanted + " wanted, "
                             + made + " placed: " + (_file.Faults.Count <= FaultRules.MostOf(Settings.CityFaultsCount) && made <= wanted ? "PASS" : "over the limit"));
            }
            return made > 0;
        }

        private static string Words(FaultKind kind)
        {
            switch (kind)
            {
                case FaultKind.Lamp: return "a street lamp has gone dark";
                case FaultKind.Pothole: return "a pothole has opened";
                case FaultKind.Litter: return "litter has piled up";
                case FaultKind.Hotspot: return "a crime hot spot";
                default: return "an unsafe place has appeared (an open manhole or a fallen sign)";
            }
        }

        private static string Where(Fault f)
        {
            return (string.IsNullOrEmpty(f.Address) ? "(" + f.X.ToString("F0") + ", " + f.Z.ToString("F0") + ")" : f.Address) + " in " + Districts.DistrictGame.Name(f.District);
        }

        // The faults changed: the world, the footfall near the player's businesses, and (the host) the guests.
        private void Changed(bool share)
        {
            bool hasPlayer = FaultGame.Player(out Vector3 player);
            _world.Sync(_file, hasPlayer, player);
            Footfall();
            if (share && !Following)
            {
                SharedCity.Changed(FaultShare.Part);
            }
        }

        // A guest: the host's faults, whole.
        private bool FromHost(string text)
        {
            if (!_loaded || !FaultShare.Apply(_file, text))
            {
                return false;
            }
            Changed(false);
            Logger?.Info("City faults: the host's faults are this game's: " + _file.Faults.Count + " in the city.");
            return true;
        }

        // ---------------------------------------------------------------- near the player

        private void Near(float now)
        {
            bool hasPlayer = FaultGame.Player(out Vector3 player);
            _world.Sync(_file, hasPlayer, player);
            if (!hasPlayer)
            {
                return;
            }
            bool dark = FaultGame.Outdoors && FaultRules.DarkAt(_file, player.x, player.z, FaultRules.DarkNear);
            SetCrime(FaultRules.CrimeScale(dark, _save != null ? _save.Hour : 12));
            if (now >= _nextTrip)
            {
                _nextTrip = now + TripEvery;
                if (FaultBridges.Trip != null && _world.UnsafeNear(player, TripNear, out Vector3 spot) && _dice.Next(1000) < FaultRules.TripPerMille)
                {
                    Vector3 away = spot - player;
                    away.y = 0f;
                    if (FaultBridges.Trip(spot, TripWithin, away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward))
                    {
                        Logger?.Info("City faults: a passer-by tripped at the unsafe place by " + spot.ToString("F0") + " and is getting up again.");
                    }
                }
            }
        }

        private void SetCrime(float scale)
        {
            if (scale == _crimeScale)
            {
                return;
            }
            _crimeScale = scale;
            FaultBridges.CrimeScale?.Invoke(scale);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("City faults check (a dark lamp near): street crime here scaled by " + scale.ToString("F2", CultureInfo.InvariantCulture) + (scale > 1f ? " after dark" : " (none)")
                    + ": PASS");
            }
        }

        // ---------------------------------------------------------------- footfall

        /// <summary>Per cent of customers a business of the player's loses to litter near its door now (0 with none, or the faults off).</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            FaultDirector d = Instance;
            return d != null && reg != null && d._footfall.TryGetValue(reg, out int percent) ? percent : 0;
        }

        // The player's businesses with litter near their doors: each whose figure changed is drawn again.
        private void Footfall()
        {
            FaultGame.PlayerBusinesses(_regs, _doors);
            for (int i = 0; i < _regs.Count; i++)
            {
                int percent = FaultRules.LitterFootfall(FaultRules.LitterAt(_file, _doors[i].x, _doors[i].z, FaultRules.LitterNear));
                _footfall.TryGetValue(_regs[i], out int was);
                if (percent == was)
                {
                    continue;
                }
                if (percent == 0)
                {
                    _footfall.Remove(_regs[i]);
                }
                else
                {
                    _footfall[_regs[i]] = percent;
                }
                FaultBridges.Footfall?.Invoke(_regs[i]);
                Logger?.Info("City faults: litter near a business of the player's: " + percent + "% customers there while it lies.");
            }
        }

        private void ClearFootfall()
        {
            if (_footfall.Count == 0)
            {
                return;
            }
            _regs.Clear();
            _regs.AddRange(_footfall.Keys);
            _footfall.Clear();
            for (int i = 0; i < _regs.Count; i++)
            {
                FaultBridges.Footfall?.Invoke(_regs[i]);
            }
        }

        // ---------------------------------------------------------------- what the player meets

        /// <summary>
        /// The player's car went from one point to another (Transport/CarOwnership's drive tick): over a pothole, the share
        /// of tyre life it takes and the jolt (m/s upwards), more at speed. False when it missed them all.
        /// </summary>
        public static bool RoadHit(Vector3 from, Vector3 to, float speed, out float wear, out float jolt)
        {
            wear = jolt = 0f;
            FaultDirector d = Instance;
            if (d == null || !d._loaded || d._failed || !Settings.CityFaults || !d._world.OverHole(from, to))
            {
                return false;
            }
            wear = FaultRules.TyreWear(speed);
            jolt = FaultRules.Jolt(speed);
            Logger?.Info("City faults: the player's car went over a pothole at " + (speed * 3.6f).ToString("F0") + " km/h: a jolt of " + jolt.ToString("F2", CultureInfo.InvariantCulture) + " m/s, "
                         + (wear * 100f).ToString("F1", CultureInfo.InvariantCulture) + "% of the tyres' life.");
            return true;
        }

        /// <summary>A crime reported in the street at a point (Social/News): one more of a hot spot there, or now and then a new one. Never on a guest.</summary>
        public void Crime(Vector3 where)
        {
            if (!_loaded || _failed || Following || GoingPublic.IsGuest || !Settings.CityFaults || where == Vector3.zero || !FaultGame.Ready)
            {
                return;
            }
            string district = FaultGame.DistrictAt(where, CrimeFrom, out string address);
            Fault made = FaultRules.Crime(_file, Settings.CityFaultsCount, district, address, where.x, where.y, where.z, FaultGame.Now(), _dice);
            if (made != null)
            {
                Logger?.Info("City faults: crimes at " + Where(made) + " make it a hot spot.");
            }
            Changed(true);   // (one more crime at a hot spot is the guests' too)
        }
    }
}
