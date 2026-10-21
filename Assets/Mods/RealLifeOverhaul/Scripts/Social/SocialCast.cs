using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.SoundSystem;
using Character;
using Characters;
using Characters.EmojiSystem;
using Extensions;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people of the player's life on the street: two characters of the social life's own, made once and reused, each
    /// dressed as whoever it stands for. One costs what a pedestrian of the game's costs while drawn; nothing runs by itself.
    /// </summary>
    public sealed class SocialCast
    {
        public const int Size = 2;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;   // metres
        private const float HideBehind = 6f;       // metres behind the camera
        private const float MaxBuild = 0.55f;      // the game's fatness: the clips are made for slim to average (see CityPeople)

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };
        private static NavMeshPath _path;

        private readonly CityPerson[] _people = new CityPerson[Size];
        private readonly int[] _wears = { -1, -1 };   // whose look each body has
        private readonly bool[] _out = new bool[Size];
        private readonly bool[] _seated = new bool[Size];
        private readonly MonoBehaviour _host;
        private readonly CityProps _props;
        private readonly IModLogger _logger;
        private bool _failed;

        public SocialCast(MonoBehaviour host, CityProps props, IModLogger logger)
        {
            _host = host;
            _props = props;
            _logger = logger;
        }

        /// <summary>Somebody on the street, not in the car.</summary>
        public bool AnyOut => (_out[0] && !_seated[0]) || (_out[1] && !_seated[1]);

        public bool HasRoom => !_failed && (!_out[0] || !_out[1]);

        /// <summary>The social person represented by a live slot, including somebody seated in a car; -1 for none.</summary>
        internal int OutPerson(int slot) => _out[slot] ? _wears[slot] : -1;

        /// <summary>Every body belongs to the cast while it is pooled, even when it is not out.</summary>
        internal void AppendBodies(List<object> characters)
        {
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                if (person != null && person.Character != null && !characters.Contains(person.Character))
                {
                    characters.Add(person.Character);
                }
            }
        }

        /// <summary>At a tested spot, facing a point. Null if both bodies are out, the spot will not take them, or none can be made.</summary>
        public CityPerson Put(SocialPerson who, Vector3 spot, Vector3 face)
        {
            if (_failed || who == null)
            {
                return null;
            }
            int slot = -1;
            for (int i = 0; i < Size; i++)
            {
                if (!_out[i] && (slot < 0 || _wears[i] == who.Index))
                {
                    slot = i;   // the body that already looks like them, if it is free
                }
            }
            if (slot < 0)
            {
                return null;
            }
            try
            {
                CityPerson person = _people[slot];
                if (person == null || !person.Valid)
                {
                    person = Create(spot);
                    _people[slot] = person;
                    _wears[slot] = -1;
                }
                person.Character.gameObject.SetActive(true);
                person.Reset(_props);
                if (_wears[slot] != who.Index)
                {
                    Dress(person, who);
                    _wears[slot] = who.Index;
                }
                person.InUse = true;
                person.Actor.InUse = true;
                if (!person.Place(spot, face))
                {
                    Shelve(slot);
                    return null;
                }
                _out[slot] = true;
                _seated[slot] = false;
                return person;
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Social: characters could not be made (see the error above); nobody is put on the street for this session.");
                return null;
            }
        }

        /// <summary>At once to another tested spot, for somebody nobody is looking at. False, and unmoved, if it will not take them.</summary>
        public bool Move(CityPerson person, Vector3 spot, Vector3 face)
        {
            int slot = SlotOf(person);
            if (slot < 0 || _seated[slot] || person.Lost)
            {
                return false;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            Vector3 before = person.Position;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                agent.Warp(before);
                return false;
            }
            person.Idle(0.1f);
            person.Turn(face);
            person.Stand();
            return true;
        }

        /// <summary>The walk their own navigation agent would make to a place; false if there is no route at all.</summary>
        public static bool Walk(CityPerson person, Vector3 to, out float length, out bool whole)
        {
            length = 0f;
            whole = false;
            if (person == null || person.Lost)
            {
                return false;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!agent.CalculatePath(to, _path) || _path.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }
            whole = _path.status == NavMeshPathStatus.PathComplete;
            length = SocialPlaces.Length(_path);
            return true;
        }

        /// <summary>The carriageway as well as the pavement, to reach a car door.</summary>
        public static void OnRoad(CityPerson person)
        {
            if (person == null || person.Lost)
            {
                return;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            if (agent.enabled && agent.agentTypeID != NavMeshHelper.PlayerAgentTypeId)
            {
                Vector3 here = person.Position;
                agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
                agent.Warp(here);
            }
        }

        /// <summary>Into the car: off the street, still held.</summary>
        public void Seat(CityPerson person)
        {
            int slot = SlotOf(person);
            if (slot < 0 || _seated[slot])
            {
                return;
            }
            _seated[slot] = true;
            try
            {
                person.Reset(_props);
                person.Character.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>False, and still seated, if they cannot stand there.</summary>
        public bool Unseat(CityPerson person, Vector3 spot, Vector3 face)
        {
            int slot = SlotOf(person);
            if (slot < 0 || !_seated[slot] || !person.Valid)
            {
                return false;
            }
            person.Character.gameObject.SetActive(true);
            NavMeshAgent agent = person.Character.navmeshAgent;
            agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
            if (!agent.enabled)
            {
                person.Character.transform.position = spot;
            }
            agent.enabled = true;
            person.Character.capsuleCollider.enabled = true;
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                person.Character.gameObject.SetActive(false);
                return false;
            }
            person.Character.LookTarget = Vector3.zero;
            person.Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            person.Turn(face);
            person.Stand();
            _seated[slot] = false;
            return true;
        }

        public bool Seated(CityPerson person)
        {
            int slot = SlotOf(person);
            return slot >= 0 && _seated[slot];
        }

        /// <summary>Safe with null and with somebody already taken in.</summary>
        public void Release(CityPerson person)
        {
            int slot = SlotOf(person);
            if (slot >= 0)
            {
                Shelve(slot);
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Size; i++)
            {
                if (_out[i])
                {
                    Shelve(i);
                }
            }
        }

        /// <summary>Drawing and animation off for whoever is far away or behind the camera. A few comparisons.</summary>
        public void Cull()
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return;
            }
            Transform eye = camera.transform;
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                if (!_out[i] || _seated[i] || person == null || person.Lost)
                {
                    continue;
                }
                Vector3 offset = person.Position - eye.position;
                person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, eye.forward) > -HideBehind);
            }
        }

        /// <summary>Nothing for somebody not drawn.</summary>
        public void Emoji(CityPerson person, CharacterEmojiName emoji, float seconds)
        {
            try
            {
                if (person == null || person.Lost || !person.Character.visible || person.Character.head == null || _host == null || !_host.isActiveAndEnabled
                    || GameManager.GetMainCamera() == null || CharacterEmojiSystem.GetCharacterEmojiByName(emoji) == null)
                {
                    return;
                }
                _host.StartCoroutine(CharacterEmojiSystem.ShowEmoji(person.Character.head, emoji, false, seconds));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        public static void DoorSound(Vector3 position)
        {
            SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
            if (sounds != null)
            {
                sounds.PlayAudio(SoundType.CarDoorOpenClose, position);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                _people[i] = null;
                _out[i] = false;
                _seated[i] = false;
                _wears[i] = -1;
                try
                {
                    if (person != null && person.Valid)
                    {
                        person.InUse = false;
                        person.Actor.InUse = false;
                        ActivityAnimators.Forget(person.Character.animator);
                        if (!RagdollSystem.Holds(person.Character))
                        {
                            UnityEngine.Object.Destroy(person.Character.gameObject);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        private int SlotOf(CityPerson person)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person != null && _people[i] == person && _out[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void Shelve(int slot)
        {
            CityPerson person = _people[slot];
            _out[slot] = false;
            _seated[slot] = false;
            if (person == null)
            {
                return;
            }
            bool taken = person.Valid && RagdollSystem.Holds(person.Character);
            person.InUse = false;
            if (taken)
            {
                // Knocked down: the casualty system has the body and puts it away itself.
                _people[slot] = null;
                _wears[slot] = -1;
                return;
            }
            person.Actor.InUse = false;
            try
            {
                if (person.Valid)
                {
                    person.Reset(_props);
                    person.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private CityPerson Create(Vector3 position)
        {
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, position, Quaternion.identity, _host.transform);
            character.name = "RealLifeOverhaul Friend";
            // Idle fidgets would play over a scene's wave or cup.
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            // Left to physics, a body is shoved about by whatever it brushes.
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            _logger?.Info("Social: a character was created for the people in the player's life (two at most).");
            return new CityPerson(character);
        }

        // From the person's seed, the same every time, or the look kept from the street. One mesh merge.
        private static void Dress(CityPerson person, SocialPerson who)
        {
            person.Look = CityLook.Civilian;
            AppearanceSetter appearance = person.Character.appearanceSetter;
            CityOutfits.Restore(person.Dyes);
            appearance.data.ageInDays = Mathf.Clamp(who.Age, 18, 85) * SocialPlaces.DaysPerYear;
            if (!PersonLookGame.Dress(appearance, who.Seed))
            {
                // The game colours a beard by the engine's dice: seeded as for their contact's picture, then put back.
                UnityEngine.Random.State dice = UnityEngine.Random.state;
                UnityEngine.Random.InitState(who.Seed);
                try
                {
                    appearance.RandomizeElements(who.Female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(who.Seed), CasualWear, who.Seed);
                }
                finally
                {
                    UnityEngine.Random.state = dice;
                }
            }
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
            Looks.Count("social: a friend's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
        }
    }
}
