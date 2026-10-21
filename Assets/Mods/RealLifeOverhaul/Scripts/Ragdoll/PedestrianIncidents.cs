using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// People hit by cars. A hit is reported here (by CarStrikes for the player's car, or staged
    /// here with a traffic car); this rolls what it did to the person from the speed, throws
    /// them down as a ragdoll and looks after them until they are off the street:
    ///   under 15 km/h   knocked down, back up after a few seconds, furious;
    ///   15 to 35 km/h   injured, stays down, survives;
    ///   35 to 60 km/h   badly injured, sometimes killed, the odds worsening with speed;
    ///   over 60 km/h    usually killed.
    /// Someone who stays down becomes a scene: TrafficAccidents sends the ambulance and patrol
    /// car and their crews, exactly as to a car accident, and the body goes with the ambulance.
    /// What it costs the player is PolicePursuit's business (stop, or be chased).
    /// Six people at once can be down with physics on them (RagdollSystem.MaxActive); a seventh makes the one who has lain longest a still body, which stays as it
    /// lies. Fourteen places in all; the oldest still bodies are taken away out of view. With nobody down this is one time comparison per
    /// frame; otherwise they are moved along four times a second.
    /// A traffic car can hit somebody for real as well (StruckByTraffic): the one car CarStrikes watches for
    /// Traffic/Reckless. The same roll from its real speed decides what it did to them, and once they lie
    /// still the same scene is asked for, with the car that hit them standing in it if its driver stopped.
    /// While that feature is the source of accidents, the staged incidents (a roll every 20 seconds) are not made.
    /// </summary>
    public class PedestrianIncidents : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "PedestrianIncidents", typeof(PedestrianIncidents));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float CheckInterval = 20f;
        private const float TickInterval = 0.25f;
        private const float KnockSpeed = 15f / 3.6f;     // m/s: below this nobody is hurt
        private const float InjurySpeed = 35f / 3.6f;    // ...below this nobody dies
        private const float DeadlySpeed = 60f / 3.6f;    // ...above this most do
        internal const float KnockedBelow = KnockSpeed * 0.9f;   // m/s: the most a two-wheeler's strike counts for (CarStrikes): knocked down, never hurt
        private const float StagedShare = 0.4f;          // staged incidents per staged car accident
        private const float StagedDeathChance = 0.35f;
        private const float SameSceneDistance = 60f;     // metres: casualties this close together share an ambulance

        private static readonly string[] StagedStarts =
        {
            "reallife_pedstruck_start_1", "reallife_pedstruck_start_2", "reallife_pedstruck_start_3"
        };

        public static IModLogger Logger;
        public static PedestrianIncidents Instance { get; private set; }

        private readonly Casualty[] _casualties = new Casualty[RagdollSystem.MaxBodies];
        private float _nextCheck;
        private float _nextTick;
        private bool _anyone;
        private int _figureFailures;   // standing figures in a row that could not be thrown down
        private bool _figuresOff;

        /// <summary>The game's own pedestrians still held by casualties, without changing their takeover.</summary>
        internal void AppendBodies(List<object> pedestrians)
        {
            for (int i = 0; i < _casualties.Length; i++)
            {
                Pedestrian pedestrian = _casualties[i]?.HeldPedestrian;
                if (pedestrian != null && !pedestrians.Contains(pedestrian))
                {
                    pedestrians.Add(pedestrian);
                }
            }
        }

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _casualties.Length; i++)
            {
                _casualties[i] = new Casualty(this);
            }
            _nextCheck = Time.time + CheckInterval;
            RagdollSystem.MakeRoom = GiveUpOne;
        }

        private void OnDestroy()
        {
            ClearAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ragdoll: PedestrianIncidents.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (!_anyone)
            {
                if (now < _nextCheck)
                {
                    return;
                }
                _nextCheck = now + CheckInterval;
                try
                {
                    if (Settings.PedestrianIncidents && !RecklessDrivers.IsSource && CanStage() && LifeEvents.IsSafeForEvent()
                        && !Wiring.Scenes.AnyLargeSceneActive   // (an accident, a crime, a fire or street event, a rally: Wiring/Scenes)
                        && Random.value < StreetRules.Due(Settings.AccidentFrequency, Events.Weather.AccidentFactor, TimeHelper.CurrentHour) * StagedShare)
                    {
                        TryStage(now, TrafficAccidents.Instance.FindMovingCar(PlayerHelper.GetPosition()));
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    ClearAll();
                    if (Guard.StandsDown(e, "people hit by traffic"))
                    {
                        enabled = false;   // (a game that lacks what a staging names would fail at every look)
                    }
                }
                return;
            }

            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                // ClearAll leaves nobody down, so a fault cannot repeat.
                Logger?.Error(e);
                ClearAll();
                if (Guard.StandsDown(e, "people hit by traffic"))
                {
                    enabled = false;
                }
            }
        }

        private void Tick(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            // Indoors or fast-forwarding the street is gone, and the game has taken its pedestrians back.
            if (!Settings.Ragdolls || !TrafficTools.Street || player == null)
            {
                ClearAll();
                return;
            }
            Vector3 avoid = player.transform.position;
            bool anyone = false;
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty casualty = _casualties[i];
                if (!casualty.InUse)
                {
                    continue;
                }
                casualty.Tick(now, avoid);
                if (!casualty.InUse)
                {
                    continue;
                }
                anyone = true;
                // Help is called once it is clear where they have ended up. (Somebody a staged incident put down has
                // had it asked for already; anybody else who was not the player's doing was hit by a car on the road.)
                if (!casualty.SceneAsked && casualty.Result != Casualty.Outcome.Knocked && casualty.Resting)
                {
                    casualty.SceneAsked = true;
                    if (casualty.ByPlayer)
                    {
                        CallHelp(casualty);
                    }
                    else
                    {
                        CallHelpStruck(casualty);
                    }
                }
            }
            if (!anyone)
            {
                _anyone = false;
                _nextCheck = now + CheckInterval * 3f;
            }
        }

        // ---------------------------------------------------------------- hits

        private const float TripForward = 1.4f, TripUp = 0.8f;   // m/s: a trip throws a walker forward and a little up, as a stumble does
        private static readonly List<Transform> TripRemotes = new List<Transform>(4);

        // True if a walker is (or holds, or is held by) another player of a Going Public session.
        private static bool IsRemote(Transform walker)
        {
            for (int i = 0; i < TripRemotes.Count; i++)
            {
                Transform r = TripRemotes[i];
                if (r != null && (walker == r || walker.IsChildOf(r) || r.IsChildOf(walker)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// One of the game's pedestrians within <paramref name="within"/> metres of a point trips (an unsafe place of the
        /// city's faults: Scripts/Faults): knocked down, back on their feet a few seconds later, never hurt worse and nobody
        /// to blame. False if nobody walking is that near or no ragdoll can be had. A pass over the walkers on the street,
        /// on the faults' own timer.
        /// </summary>
        public bool Trip(Vector3 at, float within, Vector3 forward)
        {
            try
            {
                List<Pedestrian> walkers = PerfGame.Pedestrians;
                Pedestrian nearest = null;
                float best = within * within;
                TripRemotes.Clear();
                GoingPublic.RemotePlayers(TripRemotes);   // (Going Public's players are not the game's walkers, but never throw one down even so)
                for (int i = 0; walkers != null && i < walkers.Count; i++)
                {
                    Pedestrian p = walkers[i];
                    if (p == null || p.tpc == null || IsRemote(p.tpc.transform))
                    {
                        continue;
                    }
                    Vector3 offset = p.tpc.transform.position - at;
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr < best)
                    {
                        best = sqr;
                        nearest = p;
                    }
                }
                if (nearest == null || CanStrike(nearest.tpc) != Strike.Down)
                {
                    return false;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return false;
                }
                HornReactions.LetGo(nearest);
                if (!casualty.BeginPedestrian(nearest, forward * TripForward + Vector3.up * TripUp, Casualty.Outcome.Knocked, false, Time.time))
                {
                    return false;
                }
                _anyone = true;
                _nextTick = Time.time + TickInterval;
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return false;
            }
        }

        /// <summary>The player's car has run into one of the game's pedestrians.</summary>
        /// <param name="speed">Impact speed in m/s.</param>
        public Strike StrikePedestrian(Pedestrian pedestrian, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                // Asked first, and cheaply: this is called again and again while the car is over someone who cannot be thrown.
                Strike why = CanStrike(pedestrian != null ? pedestrian.tpc : null);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(pedestrian);   // a pace raised or a stop made for a horn is undone before they are taken over
                if (casualty.BeginPedestrian(pedestrian, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>The player's car has run into one of the game's standing figures (StreetPeople).</summary>
        public Strike StrikeFigure(BaseHuman figure, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                Strike why = _figuresOff ? Strike.Failed : CanStrike(figure, seatedToo: true);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(figure);   // turned to look at a horn: turned back first
                if (casualty.BeginFigure(figure, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    _figureFailures = 0;
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                // Not built like the characters the ragdoll was written for: given up after a few in a row, so
                // that a car standing over one does not try (and reset its fidgeting) every other physics step.
                if (++_figureFailures >= 3)
                {
                    _figuresOff = true;
                    Logger?.Warn("Pedestrian collisions: the game's standing figures cannot be thrown down (three in a row refused); they are left alone for this session.");
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>The player's car has run into one of the mod's own people.</summary>
        public Strike StrikeActor(StreetActor actor, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                if (actor == null || !actor.IsValid)
                {
                    return Strike.NotCounted;
                }
                Strike why = CanStrike(actor.Character);   // includes people the pool is not drawing: nobody out of sight is run over
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                if (casualty.BeginActor(actor, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        // ---------------------------------------------------------------- hit by traffic, on the road

        /// <summary>
        /// A traffic car has run into one of the game's pedestrians (CarStrikes saw them inside its box). What
        /// it did to them is rolled from its real speed exactly as for the player's car; nobody is charged.
        /// </summary>
        /// <param name="speed">Impact speed in m/s.</param>
        public Strike StruckByTraffic(Pedestrian pedestrian, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                Strike why = CanStrike(pedestrian != null ? pedestrian.tpc : null);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(pedestrian);
                // Somebody the mod sent across the road is the game's again from this line on (its ground and its
                // scripts as they were), so that the casualty takes over an ordinary pedestrian.
                bool stepped = Jaywalker.Yield(pedestrian);
                if (casualty.BeginPedestrian(pedestrian, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                if (stepped && pedestrian != null && pedestrian.isActiveAndEnabled && InstanceBehavior<CityManager>.IsInitialized)
                {
                    pedestrian.Release();   // neither thrown down nor walking anywhere: back to the pool, whose next use of it is an ordinary one
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>A traffic car has run into one of the game's standing figures (StreetPeople).</summary>
        public Strike StruckByTraffic(BaseHuman figure, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                Strike why = _figuresOff ? Strike.Failed : CanStrike(figure, seatedToo: true);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(figure);
                if (casualty.BeginFigure(figure, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    _figureFailures = 0;
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                if (++_figureFailures >= 3)
                {
                    _figuresOff = true;
                    Logger?.Warn("Pedestrian collisions: the game's standing figures cannot be thrown down (three in a row refused); they are left alone for this session.");
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>A traffic car has run into one of the mod's own people.</summary>
        public Strike StruckByTraffic(StreetActor actor, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                if (actor == null || !actor.IsValid)
                {
                    return Strike.NotCounted;
                }
                Strike why = CanStrike(actor.Character);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                if (casualty.BeginActor(actor, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>How many people traffic has put on the ground who did not get up again, since the session began: a reckless driver's tick reads it to know it has hurt somebody.</summary>
        public int TrafficHurt { get; private set; }

        /// <summary>Whether the last of those died.</summary>
        public bool TrafficKilled { get; private set; }

        private void OnTrafficHit(Casualty casualty, float speed, VehicleComponent car)
        {
            _anyone = true;
            _nextTick = Time.time + TickInterval;
            Logger?.Info($"Pedestrian hit by traffic ('{TrafficTools.Name(car)}') at {speed * 3.6f:F0} km/h: {casualty.Result}.");
            if (casualty.Result == Casualty.Outcome.Knocked)
            {
                TrafficAccidents.Notice(NotificationType.Warning, "reallife_streets_knocked", 6f, null);
                return;
            }
            TrafficHurt++;
            TrafficKilled = casualty.Dead;
        }

        // An ambulance and a patrol car for someone a traffic car has put on the ground, with that car standing in the
        // scene if its driver stopped (the reckless drivers' director says, and lets go of it). Nobody is left without
        // help because another scene is on: as for the player's own victims, any other incident gives way.
        private void CallHelpStruck(Casualty casualty)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            if (accidents == null || accidents.HasCasualty)
            {
                return;   // crews are already attending someone here; this one goes with the same ambulance
            }
            StreetCrime.EndActive();
            TrafficAccidents.EndActive();
            RecklessDrivers drivers = RecklessDrivers.Instance;
            VehicleComponent car = drivers != null ? drivers.GiveUpStriker() : null;
            if (accidents.StageCasualty(casualty, casualty.Position, car, casualty.Dead, byPlayer: false, asItStands: true))
            {
                TrafficAccidents.Notice(casualty.Dead ? NotificationType.Error : NotificationType.Warning, StagedStarts[Random.Range(0, StagedStarts.Length)], 8f);
            }
            else if (car != null)
            {
                TrafficTools.ReturnToTraffic(car, "a driver who stopped for somebody they hit: no scene could start");
            }
        }

        /// <summary>What became of someone inside the car's sweep.</summary>
        public enum Strike
        {
            Down,         // thrown down
            NotCounted,   // nobody there to hit: gone, already lying there, or the save is not loaded
            Seated,       // sitting: wired to the seat, left alone
            NoRagdoll,    // every ragdoll is still falling, ragdolls are off or failed, or the person is not being drawn
            Failed        // could not be taken over or thrown after all
        }

        // Down if this person can be thrown right now; otherwise why not.
        private static Strike CanStrike(BaseHuman person, bool seatedToo = false)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (!ModState.Loaded || ragdolls == null || person == null || RagdollSystem.Holds(person))
            {
                return Strike.NotCounted;
            }
            if (person.isSittingOn != null && !seatedToo)
            {
                // One of the game's plain figures is let go of its seat and thrown (Casualty.BeginFigure). A full street
                // character that sits has more tied to the seat than that (its own UnFreeze): left alone.
                return Strike.Seated;
            }
            return ragdolls.CanBegin(person) ? Strike.Down : Strike.NoRagdoll;
        }

        private void OnPlayerHit(Casualty casualty, float speed, Rigidbody playerCar)
        {
            _anyone = true;
            _nextTick = Time.time + TickInterval;
            Logger?.Info($"Pedestrian hit by the player at {speed * 3.6f:F0} km/h: {casualty.Result}.");
            if (casualty.Result == Casualty.Outcome.Knocked)
            {
                Notifications.Show(NotificationType.Warning, "reallife_pedhit_knocked", null, 6f);
                return;
            }

            // The player gets the chance to stop; if police pursuits are off it is settled as an accident on the spot.
            PolicePursuit pursuit = PolicePursuit.Instance;
            bool deferred = pursuit != null && playerCar != null && pursuit.ReportPedestrian(playerCar, casualty.Dead, casualty.Position);
            if (!deferred)
            {
                PolicePursuit.ChargeForPeople(casualty.Dead ? 0 : 1, casualty.Dead ? 1 : 0, stopped: true);
            }
        }

        // What the impact did to them. Between the injury and deadly speeds the odds of dying climb from 10% to 45%.
        private static Casualty.Outcome Roll(float speed)
        {
            if (speed < KnockSpeed)
            {
                return Casualty.Outcome.Knocked;
            }
            if (speed < InjurySpeed)
            {
                return Casualty.Outcome.Injured;
            }
            float death = speed < DeadlySpeed
                ? Mathf.Lerp(0.10f, 0.45f, Mathf.InverseLerp(InjurySpeed, DeadlySpeed, speed))
                : Mathf.Lerp(0.75f, 0.95f, Mathf.InverseLerp(DeadlySpeed, 90f / 3.6f, speed));
            return Random.value < death ? Casualty.Outcome.Dead : Casualty.Outcome.Injured;
        }

        // The body leaves at about the car's speed and a little upwards: a metre at walking pace, a long way at 60.
        private static Vector3 Launch(Vector3 carVelocity, float speed)
        {
            Vector3 along = carVelocity;
            along.y = 0f;
            along = along.sqrMagnitude > 0.01f ? along.normalized : Vector3.forward;
            return along * Mathf.Clamp(speed, 2.5f, 26f) + Vector3.up * Mathf.Clamp(1.5f + speed * 0.18f, 1.5f, 5.5f);
        }

        // An ambulance and a patrol car for someone the player has put on the ground. Any other incident gives way.
        private void CallHelp(Casualty casualty)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            if (accidents == null || accidents.HasCasualty)
            {
                return;   // crews are already attending someone here; this one goes with the same ambulance
            }
            StreetCrime.EndActive();
            TrafficAccidents.EndActive();
            if (accidents.StageCasualty(casualty, casualty.Position, null, casualty.Dead, byPlayer: true))
            {
                Notifications.Show(NotificationType.Warning, "reallife_pedhit_scene", null, 8f, "reallife_accident");
            }
        }

        /// <summary>
        /// A casualty has gone with the ambulance (or the scene ended). Anyone else lying
        /// injured nearby is taken along: there is one ambulance, and nobody is left behind.
        /// </summary>
        internal void OnCollected(Casualty collected)
        {
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty other = _casualties[i];
                if (other != collected && other.Present && other.Result != Casualty.Outcome.Knocked && other.Resting
                    && (other.Position - collected.Position).sqrMagnitude < SameSceneDistance * SameSceneDistance)
                {
                    other.Remove();
                }
            }
        }

        // ---------------------------------------------------------------- staged incidents

        private static bool CanStage()
        {
            return Settings.Ragdolls && ModState.Loaded && TrafficTools.Ready && ActorPool.Instance != null
                   && TrafficAccidents.Instance != null && RagdollSystem.Instance != null && RagdollSystem.Instance.Working
                   && PlayerHelper.PlayerController != null;
        }

        /// <summary>Options-menu test button: a traffic car hits someone nearby, right away.</summary>
        public void StageNow()
        {
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.Ragdolls)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && !CanStage())
                {
                    why = TestRefusal.PoolBusy;   // the ragdolls or the character pool are not to be had this session
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", why);
                    return;
                }
                StreetCrime.EndActive();    // one incident at a time, and the tester asked for this one
                TrafficAccidents.EndActive();
                VehicleComponent car = TrafficAccidents.Instance.FindMovingCarWide(PlayerHelper.GetPosition(), out int within, out string census);
                if (car == null)
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", TestRefusal.NoMovingCar, within, census);
                }
                else if (!TryStage(Time.time, car))
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", TestRefusal.PoolBusy, 0, "no ragdoll or character free, or the accident scene could not start");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
            }
        }

        // A traffic car some way off knocks down someone who stepped out in front of it.
        private bool TryStage(float now, VehicleComponent car)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            // The car first: nobody lying in the road is made to give up their place for a scene that cannot happen.
            Casualty casualty = car != null ? FreeCasualty() : null;
            if (casualty == null)
            {
                return false;
            }
            Transform body = car.transform;
            Vector3 forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            forward.Normalize();

            bool dead = Random.value < StagedDeathChance;
            float speed = dead ? Random.Range(11f, 16f) : Random.Range(5.5f, 9f);   // m/s: the severity is rolled, not the car's real speed
            Vector3 ahead = body.position + forward * (Mathf.Max(1.5f, car.length * 0.5f) + 1.2f);
            if (!casualty.BeginNewActor(ahead, Launch(forward, speed), dead ? Casualty.Outcome.Dead : Casualty.Outcome.Injured, now))
            {
                return false;
            }
            if (!accidents.StageCasualty(casualty, body.position, car, dead, byPlayer: false))
            {
                casualty.Remove();
                return false;
            }
            casualty.SceneAsked = true;
            _anyone = true;
            _nextTick = now + TickInterval;
            Logger?.Info($"Pedestrian incident staged: {(dead ? "fatal" : "injury")}, {Vector3.Distance(PlayerHelper.GetPosition(), body.position):F0} m away.");
            TrafficAccidents.Notice(dead ? NotificationType.Error : NotificationType.Warning, StagedStarts[Random.Range(0, StagedStarts.Length)], 8f);
            return true;
        }

        // ---------------------------------------------------------------- testing and clearing up

        /// <summary>
        /// Options-menu test button: the nearest pedestrian is knocked down with a medium push,
        /// away from the player, and gets up again. Nothing else comes of it; it is for looking
        /// at the physics.
        /// </summary>
        public void TestNearest()
        {
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.Ragdolls)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Ragdoll", why);
                    return;
                }
                PlayerController player = PlayerHelper.PlayerController;
                var city = InstanceBehavior<CityManager>.Instance;
                Pedestrian nearest = null;
                if (city != null && city.pedestrianSpawner != null)
                {
                    List<Pedestrian> walkers = city.pedestrianSpawner.activePedestrians;
                    float best = 60f * 60f;
                    for (int i = 0; i < walkers.Count; i++)
                    {
                        Pedestrian walker = walkers[i];
                        if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || walker.tpc.isSittingOn != null)
                        {
                            continue;
                        }
                        float sqr = (walker.transform.position - player.transform.position).sqrMagnitude;
                        if (sqr < best)
                        {
                            best = sqr;
                            nearest = walker;
                        }
                    }
                }
                Vector3 push = nearest != null ? nearest.transform.position - player.transform.position : Vector3.forward;
                Casualty casualty = nearest != null ? FreeCasualty() : null;
                if (casualty != null && casualty.BeginPedestrian(nearest, Launch(push, 6f), Casualty.Outcome.Knocked, false, Time.time))
                {
                    _anyone = true;
                    _nextTick = Time.time + TickInterval;
                    Logger?.Info($"Ragdoll test: {nearest.name} knocked down, {Mathf.Sqrt((nearest.transform.position - player.transform.position).sqrMagnitude):F0} m away.");
                    return;
                }
                TestRefusals.Tell(Logger, "Ragdoll", nearest == null ? TestRefusal.NobodyNear : TestRefusal.PoolBusy, nearest == null ? 60 : 0,
                    nearest == null ? null : "everyone who could make way is still falling, or the ragdoll could not be built");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
            }
        }

        private Casualty FreeCasualty()
        {
            if (!Settings.Ragdolls)
            {
                return null;
            }
            for (int i = 0; i < _casualties.Length; i++)
            {
                if (!_casualties[i].InUse)
                {
                    // A new object each time (on a hit, never per frame): a scene still holding the
                    // last person who used this place must not find a different one behind it.
                    _casualties[i] = new Casualty(this);
                    return _casualties[i];
                }
            }
            // All are down: the one who has lain longest makes way.
            return GiveUpOne(false) ? FreeCasualty() : null;
        }

        // Also what RagdollSystem asks when it has too many bodies lying about, or no place left. Only someone
        // already at rest is removed: the one who has lain longest of those the camera is not on. If everyone
        // is in view, the one who has lain longest of all; or, when only an unseen one will do (too many still
        // bodies: nobody need vanish for that), nobody.
        private bool GiveUpOne(bool unseenOnly)
        {
            Casualty oldest = null;
            bool oldestSeen = true;
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty casualty = _casualties[i];
                if (!casualty.Resting || (unseenOnly && !casualty.Still))
                {
                    continue;
                }
                bool seen = RagdollSystem.InView(casualty.Position);
                if (seen && unseenOnly)
                {
                    continue;
                }
                if (oldest == null || (oldestSeen && !seen) || (seen == oldestSeen && casualty.StruckAt < oldest.StruckAt))
                {
                    oldest = casualty;
                    oldestSeen = seen;
                }
            }
            if (oldest == null)
            {
                return false;
            }
            oldest.Remove();
            return true;
        }

        // Everybody off the street and whole again. Marked over first, so no failure here can make it run again.
        private void ClearAll()
        {
            _anyone = false;
            _nextCheck = Time.time + CheckInterval * 3f;
            for (int i = 0; i < _casualties.Length; i++)
            {
                if (_casualties[i] != null)
                {
                    _casualties[i].Remove();   // catches its own faults
                }
            }
        }
    }
}
