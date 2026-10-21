using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's ragdolls: six with physics on their bones at most, reused. Anyone who wants a
    /// character thrown down asks here. When all six are taken, the one that has lain at rest
    /// longest is made a still body: its bodies, joints and colliders come off and it stays
    /// exactly as it lies, so nobody vanishes in front of the player to make room. Still bodies
    /// are meant to number eight at most: beyond that, twice a second, whoever owns the lying
    /// bodies (PedestrianIncidents) is asked to take away its oldest one that is out of view.
    /// Only with every place taken (fourteen people down) is one removed in view.
    /// While a ragdoll is active this checks it once a frame (has it come to rest, has
    /// the game taken the character back); a body at rest is frozen and no longer simulated.
    /// With no ragdoll active the component is switched off and nothing runs.
    /// Ending a ragdoll always goes through here too, so a character can never be left with
    /// bodies on its bones: scene unloaded, character recycled, error, all end the same way.
    /// </summary>
    public class RagdollSystem : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "RagdollSystem", typeof(RagdollSystem));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const int MaxActive = 6;   // a ragdoll is 11 bodies and 10 joints while it falls (a second or two) and nothing simulated once at rest: six falling at once is 66 bodies for that moment
        public const int MaxStill = 8;    // bodies lying with nothing on their bones: each is one drawn character with its animator off
        public const int MaxBodies = MaxActive + MaxStill;

        private const float SeenWithin = 150f;   // metres: a body further off than this, or behind the camera, is not being looked at

        public static IModLogger Logger;
        public static RagdollSystem Instance { get; private set; }

        /// <summary>
        /// Asked to take one lying body away. Whoever keeps bodies lying about (PedestrianIncidents)
        /// removes its oldest one at rest and returns true: out of view if it has one there, and only
        /// such a one if the argument is true.
        /// </summary>
        public static Func<bool, bool> MakeRoom;

        private bool _stillLogged;

        private readonly Ragdoll[] _ragdolls = new Ragdoll[MaxBodies];
        private float _nextYield;   // when bodies at rest are next asked whether a vehicle has ended up on top of them
        private bool _failed;   // something went wrong once: no more ragdolls this session
        private int _refusals;  // characters in a row that could not be made a ragdoll

        /// <summary>False once ragdolls have been given up for this session.</summary>
        public bool Working => !_failed;

        /// <summary>Active ragdolls keep their character even when their physics has come to rest.</summary>
        internal void AppendBodies(List<object> characters)
        {
            for (int i = 0; i < _ragdolls.Length; i++)
            {
                Ragdoll ragdoll = _ragdolls[i];
                BaseHuman character = ragdoll != null && ragdoll.Active ? ragdoll.Character : null;
                if (character != null && !characters.Contains(character))
                {
                    characters.Add(character);
                }
            }
        }

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < MaxBodies; i++)
            {
                _ragdolls[i] = new Ragdoll();
            }
            enabled = false;   // nobody is down
        }

        private void OnDestroy()
        {
            EndAll();
            MakeRoom = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>True if a ragdoll could be started on this character right now. Changes nothing.</summary>
        public bool CanBegin(BaseHuman character)
        {
            if (_failed || !Settings.Ragdolls || !Ragdoll.CanTake(character) || Find(character) != null)
            {
                return false;
            }
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Resting && !_ragdolls[i].Still)
                {
                    return true;   // one at rest can be made still, which gives up its physics
                }
            }
            return Count(still: false) < MaxActive;
        }

        private int Count(bool still)
        {
            int count = 0;
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Still == still)
                {
                    count++;
                }
            }
            return count;
        }

        // The body that has lain at rest longest keeps its pose and gives up its physics. False if all are still falling.
        private bool SettleOldest()
        {
            Ragdoll oldest = null;
            for (int i = 0; i < MaxBodies; i++)
            {
                Ragdoll ragdoll = _ragdolls[i];
                if (ragdoll.Active && ragdoll.Resting && !ragdoll.Still && (oldest == null || ragdoll.StartedAt < oldest.StartedAt))
                {
                    oldest = ragdoll;
                }
            }
            if (oldest == null)
            {
                return false;
            }
            oldest.Settle();
            if (!_stillLogged)
            {
                _stillLogged = true;
                Logger?.Info($"Ragdoll: all {MaxActive} are in use; the body that has lain longest keeps its pose without physics (up to {MaxStill} such, the oldest taken away out of view). (Logged once.)");
            }
            return true;
        }

        /// <summary>True if the camera could be looking at this place: in front of it and not far off. For deciding who may be taken away.</summary>
        public static bool InView(Vector3 position)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Transform eye = camera.transform;
            Vector3 offset = position - eye.position;
            float distance = offset.magnitude;
            // In front, within the picture's widest half angle and a little over (bodies are two metres long).
            return distance < SeenWithin && (distance < 6f || Vector3.Dot(offset, eye.forward) > distance * 0.45f);
        }

        /// <summary>
        /// Throws a character down with the given velocity. Returns null, with the character as
        /// it was, if it cannot be done: budget full, not a skeleton we know, no ground to land on.
        /// </summary>
        public Ragdoll Begin(BaseHuman character, Vector3 velocity)
        {
            if (_failed || !Settings.Ragdolls || !Ragdoll.CanTake(character) || Find(character) != null)
            {
                return null;
            }
            try
            {
                // Physics for six: a seventh takes it from the one that has lain longest, who stays as they lie.
                if (Count(still: false) >= MaxActive && !SettleOldest())
                {
                    return null;   // all six are still falling
                }
                Ragdoll free = FindFree();
                if (free == null && MakeRoom != null && MakeRoom(false))
                {
                    free = FindFree();   // every place was taken: someone had to go, out of view if anyone was
                }
                if (free == null)
                {
                    return null;
                }
                int layer = RagdollLayers.Choose(character.transform.position, out bool blocksTraffic);
                if (layer < 0)
                {
                    return null;   // nothing to land on just here; elsewhere may be fine
                }
                if (!free.Begin(character, velocity, layer, blocksTraffic, Time.time))
                {
                    // Not the skeleton this was written for. Three in a row and it is given up,
                    // rather than have everyone who is hit fail the same way.
                    // (Only full characters count: a plain standing figure that will not do is its caller's to give up on.)
                    if (character is ThirdPersonCharacter && ++_refusals >= 3)
                    {
                        _failed = true;
                        Logger?.Warn("Ragdoll: characters here cannot be made ragdolls (bones not as expected); switched off for this session.");
                    }
                    return null;
                }
                _refusals = 0;
                enabled = true;
                return free;
            }
            catch (Exception e)
            {
                Fail(e);
                return null;
            }
        }

        /// <summary>Ends a ragdoll; see <see cref="Ragdoll.End"/>. Does nothing if it has already ended.</summary>
        public void End(Ragdoll ragdoll, bool stand, Vector3 place, bool restoreMovement)
        {
            if (ragdoll != null)
            {
                ragdoll.End(stand, place, restoreMovement);
            }
        }

        /// <summary>True while this character is a ragdoll: physics has the body, and nothing else may move or animate it.</summary>
        public static bool Holds(BaseHuman character)
        {
            return Instance != null && Instance.Find(character) != null;
        }

        /// <summary>Puts a character back on its feet where it was hit, if it is a ragdoll. For whoever is about to reuse it.</summary>
        public static void Release(BaseHuman character)
        {
            Ragdoll ragdoll = Instance != null ? Instance.Find(character) : null;
            if (ragdoll != null)
            {
                ragdoll.End(false, default, true);
            }
        }

        private Ragdoll Find(BaseHuman character)
        {
            if (character == null)
            {
                return null;   // (a destroyed character would otherwise "equal" another destroyed one)
            }
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Character == character)
                {
                    return _ragdolls[i];
                }
            }
            return null;
        }

        /// <summary>Where one of the bodies lies, for whoever must drive round it. False if that place is empty.</summary>
        public bool TryGetBody(int index, out Vector3 position)
        {
            Ragdoll ragdoll = _ragdolls[index];
            bool down = ragdoll != null && ragdoll.Active && !ragdoll.Astray;
            position = down ? ragdoll.Position : default;
            return down;
        }

        private Ragdoll FindFree()
        {
            for (int i = 0; i < MaxBodies; i++)
            {
                if (!_ragdolls[i].Active)
                {
                    return _ragdolls[i];
                }
            }
            return null;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ragdoll: RagdollSystem.Update");
        // Runs only while somebody is down. Per ragdoll: a handful of flags, and until it is frozen the rest check.
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
            try
            {
                bool any = false;
                float now = Time.time;
                bool yield = now >= _nextYield;
                if (yield)
                {
                    _nextYield = now + 0.5f;
                }
                for (int i = 0; i < MaxBodies; i++)
                {
                    Ragdoll ragdoll = _ragdolls[i];
                    if (!ragdoll.Active)
                    {
                        continue;
                    }
                    if (ragdoll.Lost)
                    {
                        // Destroyed, or back in the game's hands: strip it and touch nothing else.
                        ragdoll.End(false, default, false);
                        continue;
                    }
                    any = true;
                    ragdoll.KeepLimp();
                    ragdoll.Tick(now);
                    if (yield)
                    {
                        ragdoll.YieldToVehicles();
                    }
                }
                if (!any)
                {
                    enabled = false;
                }
                else if (yield && MakeRoom != null && Count(still: true) > MaxStill)
                {
                    MakeRoom(true);   // more lying about than there should be: the oldest nobody is looking at goes
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Ragdolls are an extra. If one faults, everybody gets up and there are no more until the next load.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Ragdoll: switched off for this session after the error above.");
            EndAll();
        }

        private void EndAll()
        {
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i] != null)
                {
                    _ragdolls[i].End(false, default, true);
                }
            }
            enabled = false;
        }
    }

    /// <summary>
    /// Which physics layer a ragdoll's parts go on. They must land on the ground, and ideally
    /// traffic should brake for a body in the road as it does for anyone on foot, which means
    /// the game's Human layer. Whether that layer collides with the ground is a project setting
    /// this mod cannot read from code alone, so it is asked of the engine: the ground under the
    /// character is found with a ray, and the first of Human, Default or any other layer that
    /// collides with it (and with the game's road and ground layers) is used. Worked out once
    /// per kind of ground and remembered.
    /// </summary>
    internal static class RagdollLayers
    {
        private const int Unknown = 0;
        private const int None = -1;

        private static readonly int[] Chosen = new int[32];   // per ground layer: 0 not asked yet, -1 none, else layer + 1
        private static readonly int[] Surfaces = new int[3];  // the game's road and ground layers, -1 for any it lacks
        private static int _human = -2;
        private static int _surfaceMask;
        private static int _anythingMask;
        private static bool _warned;

        /// <summary>The layer for a ragdoll at this place, or -1 if nothing would stop it falling for ever.</summary>
        /// <param name="blocksTraffic">True if it is the layer traffic brakes for.</param>
        public static int Choose(Vector3 feet, out bool blocksTraffic)
        {
            if (_human == -2)
            {
                _human = LayerMask.NameToLayer("Human");
                Surfaces[0] = LayerMask.NameToLayer("Roads");
                Surfaces[1] = LayerMask.NameToLayer("Ground");
                Surfaces[2] = LayerMask.NameToLayer("GroundUnplacable");
                _surfaceMask = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
                // Failing those: anything solid that is not a vehicle, a person, interface or one of the game's invisible helpers.
                _anythingMask = ~(TrafficTools.VehicleMask | LayerMask.GetMask("Human", "Player", "UI", "Ignore Raycast", "Trigger",
                    "Guiders", "WallPlacementLayer", "AutoParkSpots", "ParkingArea"));
            }
            blocksTraffic = false;
            Vector3 from = feet + Vector3.up * 0.6f;
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f, _surfaceMask, QueryTriggerInteraction.Ignore)
                && !Physics.Raycast(from, Vector3.down, out hit, 6f, _anythingMask, QueryTriggerInteraction.Ignore))
            {
                return None;
            }
            int ground = hit.collider.gameObject.layer;
            if (Chosen[ground] == Unknown)
            {
                Chosen[ground] = Pick(ground);
                RagdollSystem.Logger?.Info(Chosen[ground] == None
                    ? $"Ragdoll: no layer collides with the ground layer '{LayerMask.LayerToName(ground)}'; no ragdolls there."
                    : $"Ragdoll: parts go on layer '{LayerMask.LayerToName(Chosen[ground] - 1)}' over ground layer '{LayerMask.LayerToName(ground)}'.");
            }
            if (Chosen[ground] == None)
            {
                return None;
            }
            int layer = Chosen[ground] - 1;
            blocksTraffic = layer == _human;
            if (!blocksTraffic && !_warned)
            {
                _warned = true;
                RagdollSystem.Logger?.Warn("Ragdoll: the Human layer does not collide with the ground here, so traffic will not stop for a body. (Logged once.)");
            }
            return layer;
        }

        // Human, then Default, then anything else. First a layer that lands on road and pavement alike (a body
        // thrown off the kerb crosses from one to the other); failing that, one that lands on what is underfoot.
        private static int Pick(int ground)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool everywhere = pass == 0;
                if (_human >= 0 && Lands(_human, ground, everywhere))
                {
                    return _human + 1;
                }
                if (Lands(0, ground, everywhere))
                {
                    return 1;   // Default
                }
                for (int layer = 1; layer < 32; layer++)
                {
                    if (layer != ground && !string.IsNullOrEmpty(LayerMask.LayerToName(layer)) && Lands(layer, ground, everywhere))
                    {
                        return layer + 1;
                    }
                }
            }
            return None;
        }

        private static bool Lands(int layer, int ground, bool everywhere)
        {
            if (Physics.GetIgnoreLayerCollision(layer, ground))
            {
                return false;
            }
            for (int i = 0; everywhere && i < Surfaces.Length; i++)
            {
                if (Surfaces[i] >= 0 && Physics.GetIgnoreLayerCollision(layer, Surfaces[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
