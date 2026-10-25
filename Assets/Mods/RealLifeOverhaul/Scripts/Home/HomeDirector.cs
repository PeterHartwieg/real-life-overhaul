using System;
using System.Globalization;
using BAModAPI;
using BigAmbitions.Items;
using Extensions;
using Helpers;
using Parking.UndergroundParking;
using PlayerActivity;
using PlayerActivity.Activities.Rest;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A routine the player asked for: from the phone, the furniture, the game's panel or a test button.</summary>
    public enum HomeWish
    {
        None,
        Cook,          // opens the cooking list
        CookFirst,     // a test: the first dish the fridges allow, without the list
        Coffee,
        Tidy,
        Stretch,
        PushUps,
        SitUps,
        Run,
        StopRun,
        Doctor
    }

    /// <summary>
    /// Home life and health: once a second it reads what the player is doing off the game's activity panel and moves the
    /// needs; on the hour and at midnight it settles mood, illness and diet; it offers the routines; on waking from a sleep
    /// it sends the morning line, with the city's news held while the player slept (Home/WakeRules, Core/MorningNews).
    /// Cost: idle, one time compare a frame; a run is ticked four times a second. A fault switches the feature off for
    /// the session.
    /// </summary>
    public sealed partial class HomeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("home", "HomeDirector", typeof(HomeDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const string ReadingParameter = "ReadingABook";

        public static IModLogger Logger;
        public static HomeDirector Instance { get; private set; }

        /// <summary>The player's body for the mod's routines (the jobs' class: held on the spot, a clip or the game's own animation).</summary>
        public JobBody Body { get; private set; }

        /// <summary>Set by a routine just before it feeds the player: what the meal the game is about to report was. Skipped means "not ours".</summary>
        public Meal OwnMeal = Meal.Skipped;

        public bool Jogging => _run != null && _run.Active;

        private JobProps _props;
        private JogRun _run;
        private HomeActivity _activity;
        private HomeActivity _starting;      // chosen on the cooking list: started for the player once the game's panel has shown it
        private CookPlan _plan;              // chosen on the cooking list and waiting for the player to be free
        private float _planUntil;
        private Action _later;
        private int _laterFrame;
        private HomeWish _wish;
        private float _wishUntil;
        private ItemController _clicked;     // the coffee machine a click was on: the cup is made at that one
        private readonly GameUi.WorldPanel _doctor = new GameUi.WorldPanel { Name = "doctor's desk" };
        private Action _onDoctor;
        private int _keyFrame = -1;
        private float _nextSlow;
        private float _nextRunTick;
        private bool _broken;

        private Doing _doing;
        private float _lastMinutes = -1f;
        private int _lastHour = -1;
        private float _lastHunger;
        private float _laidAt = -1f;     // game minutes: the clock at the last look before the player was seen asleep (Home/WakeRules); -1 while awake
        private float _endedAt = -1f;    // game minutes: when the game itself ended that sleep (its energy event from SleepActivity.Finish); -1 not yet
        private bool _awayNight;         // that sleep is a hotel room's, which the room ends rather than the game
        private float _wokeReal = -1f;   // real seconds: the wake whose morning line waits for the city's news (Core/MorningNewsRules.CaughtUp); -1 none
        private float _wokeGame;         // game seconds (Time.time) at that wake
        private float _wokeHours;        // that sleep's hours, for its line
        private int _wokeHour;           // the hour it ended at, for its greeting
        private int _bedHour;
        private int _bedQuality;
        private bool _bedCar;
        private bool _bedHome;
        private int _readingKnown;       // 0 not asked, 1 the player's animator has the parameter, 2 it has not
        private bool _wasOn;

        private void Awake()
        {
            Instance = this;
            HomeState.Reset();
            HomeTidy.Reset();
            HomePlace.Reset();
            JogRun.ForgetCity();
            ScenePrewarm.Add("home: the city's parks being listed for the run", JogRun.Prewarm);
            _props = new JobProps(transform);
            Body = new JobBody(_props, Logger);
            _run = new JogRun(Logger);
            _run.KeptBy(this);
            HomeHealth.Logger = Logger;
            HomeOffers.Logger = Logger;
            HomeFridgeButton.Logger = Logger;
            HomeCookList.Logger = Logger;
            HomeNeedsBar.Logger = Logger;
            GameUi.WorldPanel.Logger = Logger;
            GameUi.TravelList.Logger = Logger;
            GameUi.GuideMark.Logger = Logger;
            _onDoctor = DoctorClicked;
        }

        /// <summary>True while the doctor's panel holds the game's interact key, and in the frame its button ran: the mod's own key then stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._doctor.Showing || Instance._keyFrame == Time.frameCount);

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GameEvent.onGameEventTriggered -= _onGameEvent;   // the wrapped handler that was put on
            MorningNews.Drop();   // the city is going: news held for a morning line goes with it, unshown (before TakeBack would show it)
            TakeBack();
            HomeCookList.Remove();
            HomeNeedsBar.Remove();
            _run.Dispose();
            _props.Dispose();
            HomeHealth.Reset();
            HomeState.Reset();
            HomeTidy.Reset();
            HomePlace.Reset();
            JogRun.ForgetCity();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Runs something a few frames from now (one thing at a time: the routines' own next step).</summary>
        public void Later(Action what, int frames)
        {
            _later = what;
            _laterFrame = Time.frameCount + frames;
        }

        /// <summary>The game ran a click of ours: on a cooker, a coffee machine, or "Cook a meal" in a fridge's panel.</summary>
        public void OfferClicked(HomeWish wish, ItemController item)
        {
            if (_broken)
            {
                return;
            }
            try
            {
                bool busy = ThirdPersonCamera.IsUiOpen();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (choice): the player clicks "
                                 + (wish == HomeWish.Coffee ? "a coffee machine" : item is FridgeController ? "'Cook a meal' in a fridge's panel" : "a cooker")
                                 + (item != null ? " (" + item.itemName + ")" : string.Empty) + ": " + (busy ? "nothing is started, another screen is open" : "it is started") + ".");
                }
                if (busy)
                {
                    return;
                }
                _clicked = wish == HomeWish.Coffee ? item : null;
                Act(wish, false);
                _clicked = null;
            }
            catch (Exception e)
            {
                _clicked = null;
                Logger?.Error(e);
            }
        }

        /// <summary>Asks for a routine as soon as nothing is in the way (the phone put down, the conversation over).</summary>
        public void Wish(HomeWish wish)
        {
            _wish = wish;
            _wishUntil = Time.unscaledTime + 20f;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Home: HomeDirector.Update");
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
            if (_broken)
            {
                return;
            }
            try
            {
                float now = Time.unscaledTime;
                if (_later != null && Time.frameCount >= _laterFrame)
                {
                    Action what = _later;
                    _later = null;
                    what();
                }
                CookingTogether.Tick(this);
                if (_plan != null)
                {
                    TryCook(now);
                }
                if (_run.Active)
                {
                    PlayerController player = PlayerHelper.PlayerController;
                    if (player == null)
                    {
                        _run.End("reallife_home_run_ended_left");
                    }
                    else
                    {
                        if (Time.time >= _nextRunTick)
                        {
                            _nextRunTick = Time.time + 0.25f;
                            _run.Tick(player);
                            _run.Mark();
                        }
                    }
                }
                if (_activity != null && _activity.SkipAt > 0f && now >= _activity.SkipAt)
                {
                    _activity.SkipAt = -1f;
                    var uis = InstanceBehavior<UIs>.Instance;
                    if (_activity.Running && uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, _activity) && !uis.timeMachine.isRunning)
                    {
                        uis.playerActivityUI.ForceTimeMachine();   // the game's own time skip, as its bed starts one
                    }
                }
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(now);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Home: switched off for this session after the error above; nothing of its own is left on the player.");
                _broken = true;
                TakeBack();
                HomeHealth.Reset();
            }
        }

        // Everything of ours off the player and the screen. Never throws.
        private void TakeBack()
        {
            try
            {
                HomeOffers.Remove();
                HomeFridgeButton.Remove();
                Body?.Release();
                _run?.End("reallife_home_run_ended_left");
                HideDoctor();
                HomeNeedsBar.Hide();
                _activity = null;
                _later = null;
                _wish = HomeWish.None;
                _starting = null;
                _plan = null;
                _offersTold = false;
                _laidAt = -1f;
                _wokeReal = -1f;
                MorningNews.Release();   // news held while asleep is shown as it came: no morning line will take it
                HomeCookList.Close("home life was switched off or the city is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Slow(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (gm == null || gm.playerController == null || save == null || LoadScene.isLoading)
            {
                return;
            }
            PlayerController player = gm.playerController;
            if (!HomeState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                HomeState.Load(save.modData);
                HomeState.NewDay(TimeHelper.CurrentDay);
                HomePhone.Setup();
                _lastMinutes = TimeHelper.NowInMinutes();
                _lastHour = TimeHelper.CurrentHour;
                _lastHunger = save.Hunger;
                _doing = Doing.Nothing;
                Logger?.Info("Home: ready. Hygiene " + (int)HomeState.Hygiene + ", stress " + (int)HomeState.Stress + ", fitness " + (int)HomeState.Fitness
                             + ", illness " + HomeState.Ill + ", home " + (HomeTidy.HasHome ? "yes" : "none") + ".");
            }

            // The game clears its event hubs on every load, so the hooks are kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // Core/SaveHook: nothing is allocated while the game's event is unchanged
            _onGameEvent = _onGameEvent ?? Guard.Wrap<string>(OnGameEvent);   // made once
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);

            float minutesNow = TimeHelper.NowInMinutes();
            float minutesBefore = _lastMinutes;   // the clock at the last look: a sleep first seen now began after it
            float minutes = minutesNow - _lastMinutes;
            _lastMinutes = minutesNow;
            if (minutes < 0f || minutes > 3f * 24f * 60f)
            {
                minutes = 0f;
            }
            if (!Settings.HomeLife)
            {
                if (_wasOn)
                {
                    _wasOn = false;   // switched off on the options page: nothing of ours stays on the player, the screen or their energy
                    TakeBack();
                    HomeHealth.Reset();
                }
                _doing = Doing.Nothing;
                _lastHunger = save.Hunger;
                return;
            }

            _wasOn = true;
            Body.Bind(player);
            if (_activity != null)
            {
                var ui = InstanceBehavior<UIs>.Instance;
                if (_activity.Over || ui == null || !ReferenceEquals(ui.playerActivityUI.GetCurrentActivity, _activity))
                {
                    _activity = null;   // finished, or called off on the way there
                }
            }

            bool atHome = HomeTidy.AtHome();
            bool inside = BuildingManager.IsInsideBuilding;
            bool sheltered = HomeBridges.Sheltered != null && HomeBridges.Sheltered();   // up in a hotel room, stood at its door
            bool onFootOutside = !inside && !sheltered && !PlayerHelper.IsUsingVehicle && !UndergroundParkingManager.IsInsideParking;
            Doing doing = ReadDoing(player);
            int awayBed = HomeBridges.SleepingAway != null ? HomeBridges.SleepingAway() : -1;
            if (awayBed >= 0 && doing == Doing.Nothing)
            {
                doing = Doing.Sleeping;   // a hotel night: the time skip is sleep, though no bed of the game's is in use
            }
            Sleep(doing, minutesNow, minutesBefore, atHome, awayBed);
            _doing = doing;
            HomeHealth.Advance(minutes, doing, atHome, onFootOutside, _run.Active ? _run.Boost : 1f);
            _lastHunger = save.Hunger;
            MorningLine(now);

            int hour = TimeHelper.CurrentHour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                int passed = HomeState.NewDay(TimeHelper.CurrentDay);
                if (passed > 0)
                {
                    HomeHealth.NewDay(passed);
                }
                HomeTidy.Read();
                HomeHealth.Hourly(atHome, inside && !atHome && !(HomeBridges.PrivateGuestRoom?.Invoke() ?? false)
                                             && !GoingPublic.Mine(InstanceBehavior<BuildingManager>.Instance.buildingRegistration));
            }

            if (HomeCookList.IsOpen && (!atHome || !Settings.HomeRoutines || PlayerHelper.IsUsingVehicle || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen
                                        || PlayerActivityUI.IsPanelOpen))
            {
                HomeCookList.Close(!atHome ? "the player left home" : "another screen came up");
            }
            bool free = Free(player);
            if (_wish != HomeWish.None)
            {
                if (free || _wish == HomeWish.StopRun)
                {
                    HomeWish wanted = _wish;
                    _wish = HomeWish.None;
                    Act(wanted, false);
                    free = false;
                }
                else if (now > _wishUntil)
                {
                    Refuse("reallife_home_refused_busy", _wish.ToString(), "waited 20 s");
                    _wish = HomeWish.None;
                }
            }
            Offer(player, free && !_run.Active, atHome);
            Strip(atHome);
        }

        private Doing ReadDoing(PlayerController player)
        {
            if (_run.Active)
            {
                return _run.Moving ? Doing.Jogging : Doing.Nothing;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            IPlayerActivity activity = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity : null;
            if (activity == null)
            {
                return Doing.Nothing;
            }
            PlayerActivityState state = activity.GetState();
            if (state != PlayerActivityState.Running && state != PlayerActivityState.Started)
            {
                return Doing.Nothing;
            }
            switch (activity)
            {
                case HomeActivity own: return own.Kind;
                case SleepActivity _: return Doing.Sleeping;
                case HygieneActivity _:
                {
                    EntityController used = player.Character != null ? player.Character.CurrentEntityController : null;
                    return used is ToiletController ? Doing.Toilet : used is SinkController ? Doing.Washing : Doing.Showering;
                }
                case EntertainActivity _:
                {
                    Animator animator = player.Character != null ? player.Character.animator : null;
                    if (_readingKnown == 0 && animator != null)
                    {
                        _readingKnown = animator.HasParameter(ReadingParameter) ? 1 : 2;
                    }
                    return _readingKnown == 1 && animator != null && animator.GetBool(ReadingParameter) ? Doing.Reading : Doing.Screen;
                }
                case RestActivity _: return Doing.Resting;
                case WorkoutActivity _: return Doing.Workout;
                case SwimmingActivity _:
                case TennisActivity _:
                case GolfActivity _: return Doing.Sport;
                case WorkActivity _: return Doing.Working;
                case StudyActivity _: return Doing.Studying;
                default: return Doing.Nothing;
            }
        }

        // A sleep is counted from the last look before it was seen (the player lay down after it) to the minute the game ended
        // it, or for a hotel night the look that finds it over (Home/WakeRules). Only that wake can bring the morning line.
        private void Sleep(Doing doing, float minutesNow, float minutesBefore, bool atHome, int awayBed)
        {
            if (doing == Doing.Sleeping)
            {
                if (_doing != Doing.Sleeping)
                {
                    _laidAt = minutesBefore;
                    _endedAt = -1f;
                    _awayNight = awayBed >= 0;
                    MorningNews.Hold();   // the city's news waits for the morning line from now (Core/MorningNews)
                    _bedHour = TimeHelper.CurrentHour;
                    _bedCar = PlayerHelper.IsUsingVehicle;
                    _bedHome = atHome;
                    _bedQuality = 50;
                    if (awayBed >= 0)
                    {
                        _bedQuality = awayBed;   // no bed of the game's to read: the room says what it is
                    }
                    else
                    {
                        try
                        {
                            PlayerController player = PlayerHelper.PlayerController;
                            var bed = (MouseController.currentTargetEntity as BedController)
                                      ?? (player != null && player.Character != null ? player.Character.CurrentEntityController as BedController : null);
                            if (bed != null && !string.IsNullOrEmpty(bed.itemName))
                            {
                                int guestQuality = HomeBridges.PhysicalBedQuality?.Invoke() ?? -1;
                                _bedQuality = guestQuality >= 0 ? guestQuality : ItemsGetter.GetByName(bed.itemName).quality;
                            }
                        }
                        catch (Exception)
                        {
                            _bedQuality = 50;   // a bed the game has no data for is an ordinary bed
                        }
                    }
                }
                return;
            }
            if (_doing != Doing.Sleeping)
            {
                return;
            }
            float laidAt = _laidAt;
            bool ended = WakeRules.Ended(_awayNight, _endedAt, laidAt);
            float roseAt = WakeRules.RoseAt(_endedAt, laidAt, minutesNow);
            float hours = WakeRules.Hours(laidAt, roseAt);
            _laidAt = -1f;
            _endedAt = -1f;
            if (!Settings.HomeSleep)
            {
                MorningNews.Release();
                return;
            }
            HomeHealth.Slept(hours, _bedQuality, _bedCar, _bedHour, _bedHome);
            int day = TimeHelper.CurrentDay;
            bool due = WakeRules.Due(Settings.HomeMorning, laidAt, ended, hours, day, HomeState.MorningDay);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Home check (morning line): slept " + hours.ToString("F1", CultureInfo.InvariantCulture) + " h, from minute "
                             + laidAt.ToString("F0", CultureInfo.InvariantCulture) + " to " + roseAt.ToString("F0", CultureInfo.InvariantCulture)
                             + (_awayNight ? " (a hotel night)" : ended ? " (ended by the game's own sleep)" : " (never ended by the game's sleep)") + "; "
                             + (due ? "the line goes out" + (MorningNews.Holding ? " with the city's news held meanwhile" : string.Empty)
                                 : "no line: " + (!Settings.HomeMorning ? "the morning summary is off" : !ended ? "not a sleep the game ended"
                                     : hours < WakeRules.NightAtLeast ? "a nap" : "told already today"))
                             + ": PASS");
            }
            if (!due)
            {
                MorningNews.Release();   // no line to carry it: each held notice is shown as it came
                return;
            }
            HomeState.MorningDay = day;
            if (!MorningNews.Holding)
            {
                HomeReport.Morning(hours, TimeHelper.CurrentHour);
                return;
            }
            _wokeHours = hours;
            _wokeHour = TimeHelper.CurrentHour;
            _wokeGame = Time.time;
            _wokeReal = Time.unscaledTime;
        }

        // The morning line of a wake once the city's news it waits for has come in (Core/MorningNewsRules.CaughtUp), with that
        // news in it; and news held for a summary whose option has since been switched off, shown as it came.
        private void MorningLine(float now)
        {
            if (_wokeReal >= 0f)
            {
                if (MorningNewsRules.CaughtUp(Time.time, _wokeGame, now, _wokeReal))
                {
                    _wokeReal = -1f;
                    HomeReport.Morning(_wokeHours, _wokeHour);
                }
                return;
            }
            if (MorningNews.Holding && !MorningNews.Wanted)
            {
                MorningNews.Release();
            }
        }

        private static bool Free(PlayerController player)
        {
            return LifeEvents.IsSafeForEvent() && !ThirdPersonCamera.IsUiOpen() && !PlayerHelper.IsUsingVehicle && !JobDirector.OnShift
                   && !player.NavigationDisabled && !GameManager.IsAnyMiniGameActive();
        }

        private Action _onSave;   // made once: Core/SaveHook compares it

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || !HomeState.Loaded)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new System.Collections.Generic.Dictionary<string, string>();
                }
                HomeState.Save(save.modData);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game says "food eaten" for anything that fills: what it was is told from where it was eaten. It says "energy
        // increased" from SleepActivity.Finish for a sleep under way (and from RestActivity's): the minute a sleep ended.
        private const string FoodEaten = "ba:gameevent_foodeaten";
        private const string EnergyIncreased = "ba:gameevent_playerenergyincreased";
        private Action<string> _onGameEvent;
        private Action<string> _gameEventSeen;

        private static readonly int HotOnGameEvent = HotSpots.Slot("Home: HomeDirector.OnGameEvent");
        private void OnGameEvent(string gameEvent)
        {
            using var hotSpot = HotSpots.Time(HotOnGameEvent);
            if (!HomeState.Loaded || _broken)
            {
                return;
            }
            if (gameEvent == EnergyIncreased)
            {
                SleepEnded();
                return;
            }
            if (gameEvent != FoodEaten)
            {
                return;
            }
            try
            {
                GameInstance save = SaveGameManager.Current;
                float rise = save.Hunger - _lastHunger;
                _lastHunger = save.Hunger;
                Meal own = OwnMeal;
                OwnMeal = Meal.Skipped;
                if (own != Meal.Skipped)
                {
                    HomeState.Ate(own);
                }
                else if (rise >= HomeBalance.MealAtLeast)
                {
                    HomeState.Ate(HomeTidy.AtHome() ? Meal.Plain : Meal.Junk);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A sleep being counted has ended in the game's own Finish (the sleep is still the panel's activity as it raises the
        // event, so a rest on a bench, which raises the same, is told apart): its minute is the sleep's end.
        private void SleepEnded()
        {
            if (_laidAt < 0f)
            {
                return;
            }
            try
            {
                var uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.playerActivityUI != null && uis.playerActivityUI.GetCurrentActivity is SleepActivity)
                {
                    _endedAt = TimeHelper.NowInMinutes();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
