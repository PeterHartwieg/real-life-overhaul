using System;
using BigAmbitions.Characters;
using Extensions;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One person who has been hit by a vehicle, from the impact to the moment they are off our
    /// hands. One of the game's pedestrians (borrowed through PedestrianTakeover), one of the
    /// mod's own people (held through an ActorRole), or one of the game's standing figures (a
    /// group at a hangout spot, a performer: StreetPeople), which stays its spawner's and is only
    /// stood up again or switched off; the rest of the mod does not need to know which. Someone
    /// only knocked down lies a few seconds, gets up where they landed, is angry about it and
    /// walks off (a standing figure goes back to standing where it stood). Someone injured or
    /// killed stays down until they are taken away: by the ambulance, by the scene ending, or by
    /// the three-minute limit. PedestrianIncidents owns them and ticks them four times a second.
    /// </summary>
    public sealed class Casualty : ICasualty, ILyingBody
    {
        public enum Outcome { Knocked, Injured, Dead }

        private const float UpLatest = 9f;        // seconds: on their feet by then even if still rolling
        private const float AngryFor = 3f;
        private const float KerbAllowance = 14f;  // seconds a pedestrian may take to reach the pavement
        private const float LeaveAllowance = 27f;
        private const float RemoveAfter = 175f;   // nobody lies in the road longer than this

        private enum Stage { None, Down, Angry, ToKerb, Leaving }

        private readonly PedestrianIncidents _owner;
        private readonly PedestrianTakeover _pedestrian = new PedestrianTakeover();
        private readonly ActorRole _role = new ActorRole();

        private Ragdoll _ragdoll;
        private int _serial;
        private bool _isActor;
        private BaseHuman _figure;                       // one of the game's standing figures, while it is this casualty
        private Entities.StationaryAiBehavior _figureAi; // what makes it fidget, switched off while it is down
        private Transform _figureSeat;                   // the seat it was thrown from, to sit down on again if it gets up
        private Stage _stage = Stage.None;
        private float _stageEnd;
        private float _upAt;
        private Vector3 _lastPosition;
        private Vector3 _kerb;

        public Casualty(PedestrianIncidents owner)
        {
            _owner = owner;
        }

        public Outcome Result { get; private set; }
        public bool Dead => Result == Outcome.Dead;

        internal Pedestrian HeldPedestrian => _pedestrian.HeldPedestrian;

        /// <summary>The player's car did it, so the player answers for it.</summary>
        public bool ByPlayer { get; private set; }

        /// <summary>The emergency response for this person has been asked for (once).</summary>
        public bool SceneAsked { get; set; }

        public float StruckAt { get; private set; }

        /// <summary>True from the impact until the person is off our hands.</summary>
        public bool InUse => _stage != Stage.None;

        /// <summary>True while lying as a ragdoll and come to rest.</summary>
        public bool Resting => _stage == Stage.Down && Mine && _ragdoll.Resting;

        public bool Present => _stage == Stage.Down && Mine;

        /// <summary>True once the body lies without physics: it made way for someone thrown down later (Ragdoll.Still).</summary>
        public bool Still => Resting && _ragdoll.Still;

        public Vector3 Position
        {
            get
            {
                if (Present)
                {
                    _lastPosition = _ragdoll.Position;
                }
                return _lastPosition;
            }
        }

        // False once the ragdoll was ended by someone else: budget, error, or the game taking the character back.
        private bool Mine => _ragdoll != null && _ragdoll.Active && _ragdoll.Serial == _serial;

        /// <summary>One of the game's pedestrians is hit. False, with the pedestrian left walking, if it cannot be done.</summary>
        public bool BeginPedestrian(Pedestrian pedestrian, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || pedestrian == null || ragdolls == null || !ragdolls.CanBegin(pedestrian.tpc) || !_pedestrian.Take(pedestrian))
            {
                return false;
            }
            _isActor = false;
            Ragdoll ragdoll = ragdolls.Begin(pedestrian.tpc, velocity);
            if (ragdoll == null)
            {
                // Could not be thrown down after all: back to the game where it stands, walking on somewhere.
                Vector3 here = pedestrian.tpc.transform.position;
                if (!StreetSpots.PavementAround(here, 15f, 30f, false, out Vector3 onward))
                {
                    onward = here;
                }
                _pedestrian.HandBack(onward);
                return false;
            }
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        /// <summary>
        /// One of the game's standing figures is hit. It has no walk to interrupt and nothing to be
        /// taken over: its fidgeting is switched off the game's own way and it is thrown down.
        /// False, with the figure as it was, if it cannot be done (or it is sitting: a seat holds it).
        /// </summary>
        public bool BeginFigure(BaseHuman figure, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || figure == null || ragdolls == null || !ragdolls.CanBegin(figure))
            {
                return false;
            }
            // Sitting: let go of the seat the way the game itself ends a sitting state (the seat is told it is free,
            // the sitting animation is switched off), then thrown from where they sat. The seat stays where it is.
            Transform seat = figure.isSittingOn;
            if (seat != null)
            {
                figure.UnFreeze();
                if (figure.isSittingOn != null)
                {
                    return false;   // the game would not let go of them: left alone
                }
            }
            var fidget = figure.GetComponent<Entities.StationaryAiBehavior>();   // looked up on a hit, never per frame
            if (fidget != null)
            {
                fidget.Disable();   // also takes the cup or phone out of its hand, as the pool does
            }
            Ragdoll ragdoll = ragdolls.Begin(figure, velocity);
            if (ragdoll == null)
            {
                if (fidget != null)
                {
                    fidget.Enable();
                }
                if (seat != null)
                {
                    figure.SitOnChair(seat);
                }
                return false;
            }
            _isActor = false;
            _figure = figure;
            _figureSeat = seat;
            _figureAi = fidget;
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        /// <summary>One of the mod's own people is hit, whatever scene they were part of; that scene loses them.</summary>
        public bool BeginActor(StreetActor actor, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || actor == null || !actor.InUse || !actor.IsValid || ragdolls == null)
            {
                return false;
            }
            actor.ClearPose();        // someone kneeling has their animator off, and there is no ragdoll without one
            actor.SetVisible(true);   // nor for a hidden character, for the same reason
            if (!ragdolls.CanBegin(actor.Character) || !_role.Adopt(actor))
            {
                return false;
            }
            return Throw(ragdolls, velocity, outcome, byPlayer, now);
        }

        /// <summary>A new person is put on the road at a point and hit at once: the victim of a staged incident.</summary>
        public bool BeginNewActor(Vector3 near, Vector3 velocity, Outcome outcome, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || ragdolls == null || !_role.Enter(near, ActorLook.Civilian, 3f, onRoad: true))
            {
                return false;
            }
            try
            {
                // Just switched on: one animation step, so it falls from a standing pose and not from whatever it last held.
                ThirdPersonCharacter character = _role.Character;
                if (character != null && character.animator != null && character.animator.enabled)
                {
                    character.animator.Update(0f);
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
            return Throw(ragdolls, velocity, outcome, false, now);
        }

        private bool Throw(RagdollSystem ragdolls, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            _isActor = true;
            _role.GoLimp();
            ThirdPersonCharacter character = _role.Character;
            Ragdoll ragdoll = character != null ? ragdolls.Begin(character, velocity) : null;
            if (ragdoll == null)
            {
                _role.Exit();
                return false;
            }
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        private void Start(Ragdoll ragdoll, Outcome outcome, bool byPlayer, float now)
        {
            _ragdoll = ragdoll;
            _serial = ragdoll.Serial;
            Result = outcome;
            ByPlayer = byPlayer;
            SceneAsked = false;
            StruckAt = now;
            _lastPosition = ragdoll.Position;
            _stage = Stage.Down;
            _upAt = now + Random.Range(4f, 6f);
            _stageEnd = now + (outcome == Outcome.Knocked ? UpLatest : RemoveAfter);
        }

        /// <param name="avoid">What they walk away from once on their feet: the player.</param>
        public void Tick(float now, Vector3 avoid)
        {
            switch (_stage)
            {
                case Stage.Down:
                    if (!Mine || _ragdoll.Astray)
                    {
                        Remove();   // nothing left to lie there, or it fell out of the world
                    }
                    else if (Result == Outcome.Knocked)
                    {
                        if (now >= _stageEnd || (now >= _upAt && _ragdoll.Resting))
                        {
                            GetUp(now, avoid);
                        }
                    }
                    else if (now >= _stageEnd)
                    {
                        Remove();
                    }
                    break;

                case Stage.Angry:
                    if (_isActor)
                    {
                        _role.Tick(now);
                    }
                    if (StillOurs() && now >= _stageEnd)
                    {
                        Leave(now, avoid);
                    }
                    break;

                case Stage.ToKerb:
                    if (StillOurs() && (_pedestrian.Arrived || now >= _stageEnd))
                    {
                        // On the pavement: from here the game has its pedestrian back, walking on up the street.
                        if (!StreetSpots.AwayFrom(_kerb, avoid, 25f, onRoad: false, out Vector3 onward))
                        {
                            onward = _kerb;
                        }
                        _stage = Stage.None;
                        _pedestrian.HandBack(onward);
                    }
                    break;

                case Stage.Leaving:
                    _role.Tick(now);
                    if (!_role.Present || now >= _stageEnd)
                    {
                        Remove();
                    }
                    break;
            }
        }

        // After getting up: false (and cleaned up) if the person was taken from us meanwhile.
        private bool StillOurs()
        {
            bool ours = _isActor ? _role.Present
                : (object)_figure != null ? _figure != null && _figure.gameObject.activeInHierarchy
                : _pedestrian.Held && !_pedestrian.PutAway;
            if (!ours)
            {
                Remove();
            }
            return ours;
        }

        // There is no getting-up animation: the ragdoll ends and they are standing where they landed.
        private void GetUp(float now, Vector3 avoid)
        {
            // A standing figure cannot walk back: it gets up where it stood, a step from where it fell at that speed.
            if ((object)_figure != null || !StreetSpots.OnRoad(_ragdoll.Position, 3f, out Vector3 at))
            {
                at = _ragdoll.Origin;   // landed where nobody can stand: back where they were hit, which is ground
            }
            _ragdoll.End(true, at, false);
            _ragdoll = null;
            _lastPosition = at;
            if ((object)_figure != null)
            {
                // Back where it stood (or sat), and cross about it: the emoji over its head, and the gesture if it has one.
                try
                {
                    if (_figureSeat != null && _figure != null)
                    {
                        _figure.SitOnChair(_figureSeat);
                    }
                    ActorPool people = ActorPool.Instance;
                    if (people != null && _figure != null)
                    {
                        people.ShowEmojiAt(_figure.head, CharacterEmojiName.CommonRedAngry, AngryFor);
                        Animator animator = _figure.animator;
                        if (_figureSeat == null && animator != null && animator.HasParameter(AnimationType.IDontThinkSo.ToString()))
                        {
                            animator.SetTrigger(AnimationType.IDontThinkSo);
                        }
                    }
                }
                catch (Exception e)
                {
                    PedestrianIncidents.Logger?.Error(e);
                }
                _stage = Stage.Angry;
                _stageEnd = now + AngryFor;
                return;
            }

            ActorPool pool = ActorPool.Instance;
            if (_isActor)
            {
                if (!_role.Rise(at))
                {
                    Remove();
                    return;
                }
                _role.Face(avoid);
                _role.Emote(CharacterEmojiName.CommonRedAngry, AngryFor);
                _role.Gesture(AnimationType.IDontThinkSo);
            }
            else
            {
                if (!_pedestrian.StandUp(at))
                {
                    Remove();
                    return;
                }
                if (pool != null)
                {
                    pool.ShowEmoji(_pedestrian.Character, CharacterEmojiName.CommonRedAngry, AngryFor);
                }
            }
            _stage = Stage.Angry;
            _stageEnd = now + AngryFor;
        }

        private void Leave(float now, Vector3 avoid)
        {
            if ((object)_figure != null)
            {
                // Back to standing about, as the game's again.
                _stage = Stage.None;
                LetFigureGo(hide: false);
                return;
            }
            if (_isActor)
            {
                _role.WalkAwayFrom(avoid, Pace.Walk, 30f, 25f);
                _stage = Stage.Leaving;
                _stageEnd = now + LeaveAllowance;
                return;
            }
            // A pedestrian in the road first walks to the nearest pavement, the only ground the game lets it use.
            ThirdPersonCharacter character = _pedestrian.Character;
            if (character != null && StreetSpots.OnPavement(character.transform.position, 14f, out _kerb) && _pedestrian.WalkTo(_kerb))
            {
                _stage = Stage.ToKerb;
                _stageEnd = now + KerbAllowance;
            }
            else
            {
                Remove();
            }
        }

        /// <summary>
        /// Picked up by the ambulance crew. A body at rest is frozen, so it can be moved as one
        /// piece by its root; it stays a ragdoll, lying as it fell, until it is taken away.
        /// </summary>
        public bool Lift(out Transform body, out Transform hips, out Transform head)
        {
            body = null;
            hips = null;
            head = null;
            if (!Present || !_ragdoll.Resting || _ragdoll.Astray || _ragdoll.Character == null)
            {
                return false;
            }
            body = _ragdoll.Character.transform;
            hips = _ragdoll.Hips;
            head = _ragdoll.Head;
            return hips != null && head != null;
        }

        /// <summary>Which way they lie: read from the body, nothing changed.</summary>
        public bool Spine(out Vector3 hips, out Vector3 head)
        {
            hips = default;
            head = default;
            if (!Present || _ragdoll.Hips == null || _ragdoll.Head == null)
            {
                return false;
            }
            hips = _ragdoll.Hips.position;
            head = _ragdoll.Head.position;
            return true;
        }

        // A standing figure leaves our hands. One that was lying there is switched off (it was taken away;
        // its spawner still owns it and puts it back in its pool when the spot is next cleared, which does
        // the same and no more). One that is on its feet goes back to fidgeting. One the game has already
        // put away is only forgotten: it may be somebody else by now.
        private void LetFigureGo(bool hide)
        {
            BaseHuman figure = _figure;
            Entities.StationaryAiBehavior fidget = _figureAi;
            _figure = null;
            _figureAi = null;
            _figureSeat = null;
            try
            {
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    return;
                }
                if (hide)
                {
                    figure.gameObject.SetActive(false);
                }
                else if (fidget != null)
                {
                    fidget.Enable();
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
        }

        /// <summary>The ambulance is leaving with them, or the scene is over.</summary>
        public void TakeAway()
        {
            if (!InUse)
            {
                return;
            }
            bool wasDown = _stage == Stage.Down;
            Remove();
            if (wasDown)
            {
                _owner.OnCollected(this);
            }
        }

        /// <summary>
        /// Off the street at once, whatever state they are in, with the character whole again:
        /// the ragdoll ends first, then a pedestrian goes back through the game's pool and one of
        /// the mod's people back to theirs. Safe to call at any time; no step can stop the next.
        /// </summary>
        public void Remove()
        {
            _stage = Stage.None;
            bool lying = false;
            try
            {
                if (Mine)
                {
                    lying = true;
                    _lastPosition = _ragdoll.Position;
                    _ragdoll.End(false, default, true);
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
            _ragdoll = null;
            LetFigureGo(hide: lying);
            try
            {
                _role.Exit();
                if (_pedestrian.PutAway)
                {
                    _pedestrian.Forget();   // the game already has it back (the player went indoors)
                }
                else
                {
                    _pedestrian.Remove();
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
        }
    }
}
