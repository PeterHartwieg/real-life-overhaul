using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Travel's one component: the save, the ticks, the door panel, and the watchdog that puts a player held by a stay or
    /// trip back in the street as soon as anything is wrong. Idle cost: a few comparisons a frame, and once a second a
    /// distance to each hotel and agency.
    /// </summary>
    public sealed partial class TravelDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("travel", "TravelDirector", typeof(TravelDirector));
        private static readonly int HotUpdate = HotSpots.Slot("Travel: TravelDirector.Update");

        private const NavigationBlocker Block = NavigationBlocker.PaidActivity;   // the game's own; nothing else uses it meanwhile
        private const float SkipPatience = 240f;                                  // real seconds before a time skip is given up on

        public static IModLogger Logger;
        public static TravelDirector Instance { get; private set; }

        private bool _loaded;
        private bool _failed;
        private bool _secluded;
        private int _lastHour = -1;
        private int _lastDay = -1;
        private float _nextSecond;
        private float _nextQuarter;
        private float _nextHook;
        private HotelSite _marked;
        private float _markUntil;
        private bool _skipping;
        private DoorPanel _door;
        private GuideMark _mark;
        private Action _onSave;   // made once: SaveHook compares it

        public Hotels Hotels { get; private set; }
        public HotelStay Stay { get; private set; }
        public HotelVisit Visit { get; private set; }
        public Holidays Holidays { get; private set; }

        /// <summary>True while the door panel is up and in the frame its button ran, so NearbyInteraction stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        /// <summary>Where every list entry's press goes, so that a fault in one never reaches the game's UI code.</summary>
        public Action<int> Pressed { get; private set; }

        /// <summary>The list window every travel screen shares: a copy of the game's list of subway stations.</summary>
        public readonly DeskList Window = new DeskList("RealLifeOverhaul Travel", 1100f, 150f);

        private void Awake()
        {
            Instance = this;
            GameUi.WorldPanel.Logger = Logger;
            GameUi.TravelList.Logger = Logger;
            GameUi.GuideMark.Logger = Logger;
            _door = new DoorPanel(Logger, "Travel check (door panel)");
            _mark = new GuideMark("travel").Keep(this);
            _onReception = _door.Press(ReceptionClicked);
            _onRoom = _door.Press(RoomClicked);
            _onAgent = _door.Press(AgentClicked);
            _doorWhat = DoorWhat;
            TravelState.Reset();
            TravelEffects.Register(Logger);   // before ModState reads the save
            Hotels = new Hotels(transform, Logger);
            Stay = new HotelStay(this);
            Visit = gameObject.AddComponent<HotelVisit>();
            Visit.Initialise(this);
            Holidays = new Holidays(this);
            Pressed = OnPressed;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                if (_skipping)
                {
                    HotelStay.EndSleepEffects();
                }
                if (Visit != null)
                {
                    Destroy(Visit);
                }
                Seclude(false);
                Hotels.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _door?.Hide("the director went with the city");
            _mark?.Hide();
            Window.Destroy(e => Logger?.Error(e));
            TravelBridges.Reset();
            TravelState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
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
            if (_failed)
            {
                return;
            }
            try
            {
                Step();
            }
            catch (Exception e)
            {
                // Once: the player is put back in the street and travel stays off for the session.
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Travel: switched off for this session after the error above. Any room or ticket is kept in the save.");
                try
                {
                    ToStreet();
                    HidePrompt("travel was switched off after an error");
                    _mark.Hide();
                }
                catch (Exception again)
                {
                    Logger?.Error(again);
                }
            }
        }

        private void Step()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (!_loaded)
            {
                if (!Hotels.Resolve())
                {
                    return;   // the city's buildings are not in yet
                }
                Load();
            }
            if (now >= _nextHook)
            {
                _nextHook = now + 2f;   // the game clears its event hub on every load, so the hook is kept attached
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));
            }

            if (TimeHelper.CurrentHour != _lastHour)
            {
                _lastHour = TimeHelper.CurrentHour;
                if (TimeHelper.CurrentDay != _lastDay)
                {
                    _lastDay = TimeHelper.CurrentDay;
                    Holidays.DayTick(_lastDay);
                }
                Stay.HourTick();
                Holidays.HourTick();
                TiredCheck();
            }

            if (now >= _nextQuarter)
            {
                _nextQuarter = now + 0.25f;
                Watch();
                Holidays.TickPending();
            }

            bool street = !BuildingManager.IsInsideBuilding && !_secluded;
            if (now >= _nextSecond)
            {
                _nextSecond = now + 1f;
                Hotels.Keep();
                if (street)
                {
                    bool onFoot = !PlayerHelper.IsUsingVehicle && gm.selectedVehicle == null;
                    Vector3 player = gm.playerController.transform.position;
                    Hotels.Tick(player, onFoot && Settings.Hotels, TimeHelper.CurrentHour);
                    if (onFoot && Settings.Holidays)
                    {
                        Holidays.Tick(player);
                    }
                    else
                    {
                        Holidays.Away();
                    }
                }
                else
                {
                    Hotels.Away();
                    Holidays.Away();
                }
                UpdatePrompt(gm);
                if (_marked != null)
                {
                    if (now > _markUntil || !street || CitySpots.Flat(_marked.Desk - gm.playerController.transform.position) < 12f * 12f)
                    {
                        Unmark();
                    }
                    else
                    {
                        _mark.Show(_marked.Desk, _marked.Name);   // two compares when nothing changed
                    }
                }
            }
        }

        private void Load()
        {
            _loaded = true;
            TravelEffects.Register(Logger);
            Dictionary<string, string> data = SaveGameManager.Current.modData;
            string saved = null;
            if (data != null)
            {
                data.TryGetValue(TravelState.SaveKey, out saved);
            }
            TravelState.Read(saved);
            if (!string.IsNullOrEmpty(saved) && saved.IndexOf('=') < 0)
            {
                SaveLines.Unreadable(data, TravelState.SaveKey, "none of it could be read");
            }
            _lastHour = TimeHelper.CurrentHour;
            _lastDay = TimeHelper.CurrentDay;
            if (TravelState.StreakDay == 0)
            {
                TravelState.StreakDay = _lastDay;   // a save without travel: the count begins today
            }
            Visit.RecoverLoad();
            Holidays.FindAgencies();
            TravelAgentDialog.Host = Holidays;
            TravelPhone.Setup();
            Logger?.Info("Travel: state loaded. room=" + (TravelState.HotelId ?? "none") + " ticket=" + (TravelState.TicketId ?? "none") + " away=" + TravelState.Away
                         + " agencies=" + Holidays.AgencyCount + " hotels=" + Hotels.Sites.Count + ".");
            if (TravelState.Away || TravelState.InRoom)
            {
                StartCoroutine(Guard.Routine(ResumeSoon()));
            }
        }

        // A save made in a room or on a trip: the list comes back once the game has settled, or the player is in the street.
        private IEnumerator ResumeSoon()
        {
            float waited = 0f;
            while (waited < 20f && (waited < 1.5f || !LifeEvents.IsSafeForEvent() || BuildingManager.IsInsideBuilding))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            try
            {
                if (TravelState.Away)
                {
                    if (Settings.Holidays && !GoingPublic.ClockShared)   // in a Going Public session a trip is cut short
                    {
                        Holidays.Resume();
                    }
                    else
                    {
                        Holidays.Abort();
                    }
                }
                else if (TravelState.InRoom)
                {
                    if (Settings.Hotels)
                    {
                        Stay.Resume();
                    }
                    else
                    {
                        TravelState.InRoom = false;
                    }
                }
            }
            catch (Exception e)
            {
                Fault(e);
                ToStreet();
            }
        }

        private void OnSaveGame()
        {
            GameInstance save = SaveGameManager.Current;
            if (!_loaded || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[TravelState.SaveKey] = TravelState.Write();
        }

        public bool CanSeclude(out TestRefusal why, bool pausedIsFine = false)
        {
            why = TestRefusal.None;
            if (!_loaded || _failed || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                why = TestRefusal.NotLoaded;
            }
            else if (BuildingManager.IsInsideBuilding)
            {
                why = TestRefusal.Indoors;
            }
            else if (PlayerHelper.IsUsingVehicle || JobDirector.OnShift || !LifeEvents.IsSafeForEvent(pausedIsFine) || _skipping)
            {
                why = TestRefusal.SceneActive;
            }
            return why == TestRefusal.None;
        }

        /// <summary>Holds the player where they stand with one of the game's navigation blocks, or lets them go.</summary>
        public void Seclude(bool on)
        {
            if (on == _secluded)
            {
                return;
            }
            _secluded = on;
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (on)
            {
                player.ResetNavigation();
                player.SetNavigationBlocker(Block);
                HidePrompt("the player is held in a room or on a trip");
                Unmark();
            }
            else
            {
                player.UnsetNavigationBlocker(Block);
            }
        }

        // The watchdog, four times a second: anything that should not be true while the player is held ends the hold.
        private void Watch()
        {
            if (!_secluded)
            {
                // Reception is used from the street, in no room.
                if (Window.IsOpen && !TravelState.InRoom && !TravelState.Away && !Holidays.ShowingCancel && !Stay.AtReception && !(Visit != null && Visit.Ready))
                {
                    Window.Close();
                }
                return;
            }
            bool open = Window.IsOpen;
            bool wrong = BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || EnergyHelper.goingToHospital || GameMembers.PlayerDead
                         || (TravelState.InRoom && !Settings.Hotels) || (TravelState.Away && !Settings.Holidays)
                         || (!open && !_skipping && !Stay.Busy && !Holidays.Busy && !Stay.PlainSleep && !Holidays.Auto);
            if (wrong)
            {
                Logger?.Info("Travel: the watchdog ended a stay or trip (indoors=" + BuildingManager.IsInsideBuilding + " vehicle=" + PlayerHelper.IsUsingVehicle
                             + " hospital=" + EnergyHelper.goingToHospital + " list=" + open + ").");
                ToStreet();
                return;
            }
            // A game menu over the room's list closes the list and keeps the room, as the transit list does.
            if (open && TravelState.InRoom && !Stay.Busy && (FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen))
            {
                Window.Close();
            }
        }

        /// <summary>The way out of everything: no list or block, energy put back, the player in the street; room and ticket kept.</summary>
        public void ToStreet()
        {
            if (Visit != null && Visit.Active) { Visit.Leave(false); return; }
            bool skipping = _skipping;
            Stay.ForceOut();
            if (TravelState.Away)
            {
                Holidays.Abort();
            }
            if (skipping)
            {
                var uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && !EnergyHelper.goingToHospital)
                {
                    uis.timeMachine.StopTimeMachine(0f);
                }
            }
            HotelStay.EndSleepEffects();
            Seclude(false);
            Holidays.ClearCancel();
            Window.Close();
        }

        private void OnPressed(int id)
        {
            if (_failed || _skipping && id != HotelStay.WakeNow)
            {
                return;
            }
            try
            {
                if (id >= 100)
                {
                    Holidays.Press(id);
                }
                else
                {
                    Stay.Press(id);
                }
            }
            catch (Exception e)
            {
                Fault(e);
                ToStreet();
            }
        }

        public void Run(IEnumerator routine) => StartCoroutine(Guard.Routine(Guarded(routine)));

        // A routine of the stay or the trip: a fault in it ends in the street, never in a frozen list.
        private IEnumerator Guarded(IEnumerator routine)
        {
            while (true)
            {
                object step;
                try
                {
                    if (!routine.MoveNext())
                    {
                        yield break;
                    }
                    step = routine.Current;
                }
                catch (Exception e)
                {
                    Fault(e);
                    ToStreet();
                    yield break;
                }
                yield return step;
            }
        }

        /// <summary>Skips to a time with the game's own time machine, the whole game running meanwhile, and returns when it has stopped.</summary>
        /// <param name="constant">The whole distance in about a second (a day of a trip), not at the machine's own pace (a night's sleep).</param>
        public IEnumerator Skip(Timestamp goal, string infoKey, bool constant = false)
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || goal.GetMinutesFromNow() <= 0f || GoingPublic.ClockShared)
            {
                yield break;   // no time passes in a Going Public session
            }
            _skipping = true;
            uis.timeMachine.StartTimeMachine(goal, false, infoKey, true, constant);
            float waited = 0f;
            yield return null;
            while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < SkipPatience)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                Logger?.Warn("Travel: a time skip did not end within " + SkipPatience + " s and was stopped.");
                uis.timeMachine.StopTimeMachine(0f);
            }
            yield return new WaitForSecondsRealtime(0.3f);   // the machine puts the HUD back a moment after it stops
            _skipping = false;
        }

        public void Mark(HotelSite site, float seconds)
        {
            _marked = site;
            _markUntil = Time.unscaledTime + seconds;
        }

        private void Unmark()
        {
            if (_marked != null)
            {
                _marked = null;
                _mark.Hide();
            }
        }

        /// <summary>Marks the player's own hotel, or the nearest, and returns the sentence that says which (for the Trips page).</summary>
        public string PointToHotel()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !Settings.Hotels)
            {
                return TravelText.T("reallife_travel_trips_no_hotel");
            }
            float distance;
            HotelSite site = TravelState.HasRoom ? Hotels.ById(TravelState.HotelId) : null;
            if (site != null)
            {
                distance = Mathf.Sqrt(CitySpots.Flat(site.Desk - player.transform.position));
            }
            else
            {
                site = Hotels.Nearest(player.transform.position, null, out distance);
            }
            if (site == null)
            {
                return TravelText.T("reallife_travel_trips_no_hotel");
            }
            Mark(site, 600f);
            return HotelLine("reallife_travel_trips_hotel", site, distance);
        }

        // Shows and marks the nearest hotel. False if hotels are off, the player has a room or there is no hotel.
        private static bool SuggestHotel(string key)
        {
            TravelDirector director = Instance;
            PlayerController player = PlayerHelper.PlayerController;
            if (director == null || !director._loaded || director._failed || player == null || !Settings.Hotels || TravelState.HasRoom)
            {
                return false;
            }
            HotelSite site = director.Hotels.Nearest(player.transform.position, null, out float distance);
            if (site == null)
            {
                return false;
            }
            director.Mark(site, 600f);
            TravelText.Toast(NotificationType.Info, HotelLine(key, site, distance), 12f);
            return true;
        }

        // A line whose text takes {hotel}, {metres}, {rate} and {room}: the hotel's cheapest room tonight.
        private static string HotelLine(string key, HotelSite site, float distance)
        {
            RoomType room = TravelCatalogue.Rooms(site.Slot.Tier)[0];
            return TravelText.T(key, "hotel", site.Name, "metres", TravelText.Number(Mathf.RoundToInt(distance)),
                "rate", TravelText.Money(HotelStay.Tonight(site.Slot, room)), "room", TravelText.T(room.NameKey));
        }

        // On the hour: late, worn out and without a room, the player is told where the nearest bed is, once a day each.
        private void TiredCheck()
        {
            if (!Settings.Hotels || TravelState.HasRoom || TravelState.Away || _secluded || BuildingManager.IsInsideBuilding)
            {
                return;
            }
            GameInstance save = SaveGameManager.Current;
            int hour = TimeHelper.CurrentHour;
            int day = TravelEffects.StayDay();
            bool late = hour >= 20 || hour < 5;
            if (late && save.Energy < 25f && TravelState.TiredDay != day)
            {
                if (SuggestHotel(PlayerHelper.IsUsingVehicle ? "reallife_travel_suggest_tired_driving" : "reallife_travel_suggest_tired"))
                {
                    TravelState.TiredDay = day;
                }
            }
            else if (late && save.Energy < 45f && TravelState.HintDay != day && TravelState.TiredDay != day && !HasBed())
            {
                if (SuggestHotel("reallife_travel_suggest_no_home"))
                {
                    TravelState.HintDay = day;
                }
            }
        }

        // The home feature knows best; without it, a rented apartment counts as a bed.
        private static bool HasBed()
        {
            bool? known = TravelBridges.Home.HasBed;
            if (known.HasValue)
            {
                return known.Value;
            }
            List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].GetBuildingType() == "ba:buildingtype_residential")
                {
                    return true;
                }
            }
            return false;
        }

        public void Log(string line) => Logger?.Info(line);

        public void Fault(Exception e) => Logger?.Error(e);
    }
}
