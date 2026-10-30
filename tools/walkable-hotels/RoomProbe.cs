using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BigAmbitions.Items;
using BusinessLayoutSets;
using Controllers;
using Helpers;
using PlayerActivity;
using RealLifeOverhaul;
using Player.HUD.ItemInfoOverlays;
using UI;
using UnityEngine;
using UnityEngine.AI;

namespace HotelEntryProbe
{
    /// <summary>Disposable native fixture and route experiment. Never included in the shipped mod.</summary>
    public sealed partial class Probe
    {
        private readonly List<ItemController> _fixtures = new List<ItemController>();
        private IPlayerActivity _activity;
        private bool _roomRejected;
        private bool _correctEnergy, _sleepObserved;
        private readonly List<object> _sleepEvents = new List<object>();

        private static float[] At(Vector3 p) => new[] { p.x, p.y, p.z };

        private object RoomInfo()
        {
            var manager = Manager;
            var activity = InstanceBehavior<UIs>.Instance?.playerActivityUI?.GetCurrentActivity;
            return new
            {
                inside = InsideHost(), entering = manager?.enteringBuilding, exiting = manager?.exitingBuilding,
                address = manager?.building?.Address?.ToString(), size = manager?.building?.BuildingSize,
                version = manager?.building?.BuildingVersion, player = At(PlayerHelper.GetPosition()),
                appearance = Hash(PlayerHelper.PlayerController.Character.appearanceSetter.GetCopyOfCurrentElements()),
                energy = SaveGameManager.Current.Energy, day = SaveGameManager.Current.Day,
                hour = SaveGameManager.Current.Hour, minute = SaveGameManager.Current.Minute,
                activity = activity?.GetType().Name, activityState = activity?.GetState().ToString(),
                rejected = _roomRejected, snapshot = Snapshot(),
                sleepEvents = _sleepEvents.ToArray(), multiplier = EnergyHelper.energyRegenMultiplier,
                exits = manager?.exitZones?.Select(e => new {
                    position = At(e.transform.position), spawn = At(e.playerSpawnPoint.position),
                    active = e.gameObject.activeInHierarchy, enabled = e.enabled,
                    despawner = e.despawner == null ? null : new { name = e.despawner.name,
                        position = At(e.despawner.transform.position), active = e.despawner.gameObject.activeInHierarchy,
                        enabled = e.despawner.enabled, e.despawner.isParkingExit, e.despawner.exitToZoneId,
                        collider = e.despawner.GetComponent<Collider>() == null ? null : new {
                            enabled = e.despawner.GetComponent<Collider>().enabled,
                            center = At(e.despawner.GetComponent<Collider>().bounds.center),
                            size = At(e.despawner.GetComponent<Collider>().bounds.size) } },
                    colliders = e.GetComponentsInChildren<Collider>(true).Select(c => new { name = c.name,
                        active = c.gameObject.activeInHierarchy, enabled = c.enabled, trigger = c.isTrigger,
                        components = c.GetComponents<Component>().Select(b => b == null ? "missing" : b.GetType().AssemblyQualifiedName).ToArray(),
                        center = At(c.bounds.center), size = At(c.bounds.size) }).ToArray()
                }).ToArray(),
                fixtures = (_fixtures.Count > 0 ? _fixtures : manager?.IndoorItemContainer?.GetComponentsInChildren<ItemController>(true).ToList() ?? new List<ItemController>()).Where(f => f != null).Select(f => new
                {
                    item = f.itemName, type = f.GetType().Name, position = At(f.transform.position),
                    environment = f is BedController ? (typeof(BedController).GetField("sleepEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(f) as SleepEnvironment)?.EnergyRegen.ToString() : null,
                    lightOn = f is IndoorLightController light ? (bool?)light.IsLightOn : null,
                    quality = f.Item.quality, occupied = f.Occupied, active = f.gameObject.activeInHierarchy,
                    target = At(f.GetClosestNavMeshTargetPosition(PlayerHelper.GetPosition())),
                    reachable = PlayerHelper.PlayerController.ExistsRoute(f, false),
                    playerOwned = f.BuildingContext.IsPlayerOwnedBusiness
                }).ToArray(),
                shellItems = manager?.currentBuildingVersion?.GetComponentsInChildren<ItemController>(true)
                    .Select(f => new { item = f.itemName, type = f.GetType().Name }).ToArray(),
                beds = ItemsGetter.AllItems?.Where(i => i != null && i.itemName.Contains("bed") && !i.itemName.Contains("bedside") && !i.itemName.Contains("flatbed"))
                    .Select(i => new { item = i.itemName, quality = i.quality }).ToArray()
            };
        }

        private void Furnish(string bedName)
        {
            if (!InsideHost() || Manager.enteringBuilding || Manager.exitingBuilding || !Eligible(_real)
                || _fixtures.Any(f => f != null) || Manager.building.BuildingSize != "ba:buildingsize_l" || Manager.building.BuildingVersion != 1)
                throw new InvalidOperationException("Fixture experiment needs a settled empty L1 host without previous fixtures.");
            string path = Path.Combine(Application.streamingAssetsPath, "InteriorDesigns", "Residential", "L1", "Modern.json");
            var layout = BusinessLayoutSetHelper.Deserialize(path);
            if (layout == null || layout.BuildingSize != Manager.building.BuildingSize || layout.BuildingVersion != Manager.building.BuildingVersion)
                throw new InvalidOperationException("Native L1 residential layout unavailable.");
            bedName ??= "ba:itemname_beddoublewooden";
            if (!bedName.StartsWith("ba:itemname_bed", StringComparison.Ordinal) || bedName.Contains("bedside") || ItemsGetter.GetByName(bedName, true)?.quality != 0)
                throw new InvalidOperationException("Select an actual native bed of quality 0 from room inspection.");
            CaptureRegistration();
            _roomRejected = false;
            foreach (var item in layout.Items)
            {
                if (item.itemName == "ba:itemname_kingsizebed") Place(bedName, item.position, item.rotation);
                else if (item.itemName == "ba:itemname_armchair1" || item.itemName == "ba:itemname_officedesk2left")
                    Place(item.itemName, item.position, item.rotation);
            }
            Place("ba:itemname_shower", new Vector3(1101.5f, 0f, -173f), Quaternion.identity);
            var empty = BusinessLayoutSetHelper.Deserialize(Path.Combine(Application.streamingAssetsPath, "BusinessLayouts", "Empty", "L1", "default.json"));
            foreach (var lamp in empty.Items.Where(i => i.itemName.StartsWith("ba:itemname_ceilinglamp", StringComparison.Ordinal)))
                Place(lamp.itemName, lamp.position, lamp.rotation);
            Manager.ForceUpdateAvailableProducers();
            foreach (var fixture in _fixtures) fixture.ForceUpdateNavMesh();
            Respond(new { furnished = _fixtures.Count, snapshot = Snapshot() });
        }

        private void Place(string name, Vector3 position, Quaternion rotation)
        {
            var item = PrefabHelper.CreatePrefabItem(name, Manager.IndoorItemContainer);
            if (item == null) throw new InvalidOperationException("Missing native prefab: " + name);
            _fixtures.Add(item);
            item.itemName = name;
            item.ItemInstance = ItemHelper.InitializeNewInstance(name);
            item.playerItemPurchaserSettings = new PlayerItemPurchaserSettings { enabled = false };
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
        }

        private void UseFixture(string kind, bool correctEnergy, float? energy)
        {
            if (!InsideHost() || _roomRejected || !Eligible(_real) || PlayerHelper.IsHoldingItem || PlayerHelper.IsUsingVehicle
                || InstanceBehavior<UIs>.Instance.playerActivityUI.GetCurrentActivity != null)
                throw new InvalidOperationException("An idle, empty-handed guest in the eligible room is required.");
            if (kind == "bed")
            {
                if (energy.HasValue)
                {
                    if (energy < 20f || energy > 90f) throw new InvalidOperationException("Probe starting energy must be 20 to 90.");
                    EnergyHelper.GenerateEnergy(energy.Value - SaveGameManager.Current.Energy);
                }
                _correctEnergy = correctEnergy;
                _sleepObserved = false;
                _sleepEvents.Clear();
                var bed = _fixtures.OfType<BedController>().Single();
                var environment = typeof(BedController).GetField("sleepEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(bed) as SleepEnvironment;
                if (environment == null) throw new InvalidOperationException("Native bed sleep environment missing.");
                PlayerActivityUI.Show(environment, bed);
            }
            else if (kind == "bath") _fixtures.OfType<HygieneItemController>().Single().PerformActivity();
            else _fixtures.OfType<SeatController>().Single().GetSeatControllerCta().Item2?.Invoke();
            _activity = InstanceBehavior<UIs>.Instance.playerActivityUI.GetCurrentActivity;
            Respond(RoomInfo());
        }

        private void Production(string kind)
        {
            var director = TravelDirector.Instance;
            if (director == null) throw new InvalidOperationException("RLO is not loaded.");
            if (_op == "hotelclaim")
            {
                SelectHost();
                if (!director.Visit.Ready || !Eligible(_real)) throw new InvalidOperationException("Needs an eligible production room.");
                CaptureRegistration();
                BuildingHelper.RentBuilding(_site.Building.building, 100f, 0f);
            }
            else if (_op == "hoteloff") RealLifeOverhaul.Settings.Hotels = false;
            else if (_op == "hotelon") RealLifeOverhaul.Settings.Hotels = true;
            else if (_op == "hotelbook")
            {
                SelectHost();
                CaptureRegistration();
                if (!director.Stay.Book(_site, TravelCatalogue.Room("standard"), 2, false, out string why))
                    throw new InvalidOperationException(why);
            }
            else if (_op == "hotelenter")
            {
                SelectHost();
                CaptureRegistration();
                if (!director.Stay.EnterRoom(out TestRefusal why, _site)) throw new InvalidOperationException(why.ToString());
            }
            else if (_op == "hoteluse")
            {
                var items = Manager.IndoorItemContainer.GetComponentsInChildren<ItemController>();
                ItemController item = items.Single(f => kind == "bed" ? f is BedController : kind == "bath" ? f is HygieneItemController
                    : kind == "seat" ? f is SeatController : f.itemName == "ba:itemname_officedesk2left");
                var offers = GameContract.CtaBehaviours.Field.GetValue(null) as List<ICtaBehavior>;
                var offer = offers.FirstOrDefault(o => o.GetType().FullName.Contains("HotelVisit") && o.ShouldShow(item));
                if (offer == null) throw new InvalidOperationException("The production hotel click action is not offered.");
                offer.GetCta(item).Item2?.Invoke();
            }
            var offersNow = GameContract.CtaBehaviours.Field.GetValue(null) as List<ICtaBehavior>;
            var goals = SaveGameManager.Current.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.Name.ToLowerInvariant().Contains("goal") || f.Name.ToLowerInvariant().Contains("objective") || f.Name.ToLowerInvariant().Contains("quest"))
                .ToDictionary(f => f.Name, f => f.GetValue(SaveGameManager.Current));
            Respond(new { goals, hotelCtaCount = offersNow.Count(o => o.GetType().FullName.Contains("HotelVisit")),
                hotelEventCount = GlobalEvents.onBuildingRegistrationChange?.GetInvocationList().Count(IsHotelHandler),
                active = director.Visit.Active, ready = director.Visit.Ready, hotel = director.Visit.HotelId,
                physicalBedQuality = director.Visit.BedQuality, privateRoom = director.Visit.PrivateRoom,
                inRoom = TravelState.InRoom, hasRoom = TravelState.HasRoom, nights = TravelState.NightsSlept,
                credited = TravelState.NightCredited, hygiene = HomeState.Hygiene, room = RoomInfo() });
        }

        private static bool IsHotelHandler(Delegate d)
        {
            if (d.Target is HotelVisit) return true;
            return d.Target != null && d.Target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(f => f.GetValue(d.Target) is Delegate inner && inner.Target is HotelVisit);
        }

        private void Claim(bool eventless)
        {
            if (!InsideHost() || !Eligible(_real) || _fixtures.Count == 0)
                throw new InvalidOperationException("An eligible furnished probe room is required.");
            if (eventless) _real.RentedByPlayer = true;
            else BuildingHelper.RentBuilding(_site.Building.building, 100f, 0f);
            Respond(new { eventless, immediate = RoomInfo() });
        }

        private void RegistrationChanged(Address address)
        {
            if (_real != null && address?.streetName == _real.StreetName && address.streetNumber == _real.StreetNumber)
                CheckRoomOwnership();
        }

        private void CheckRoomOwnership()
        {
            if (_activity is SleepActivity && ReferenceEquals(InstanceBehavior<UIs>.Instance?.playerActivityUI?.GetCurrentActivity, _activity))
            {
                if (!_sleepObserved && _activity.GetState() == PlayerActivityState.Started)
                {
                    _sleepObserved = true;
                    _sleepEvents.Add(new { phase = "observed start", frame = Time.frameCount, appearance = Hash(PlayerHelper.PlayerController.Character.appearanceSetter.GetCopyOfCurrentElements()),
                energy = SaveGameManager.Current.Energy,
                        day = SaveGameManager.Current.Day, hour = SaveGameManager.Current.Hour, minute = SaveGameManager.Current.Minute,
                        multiplier = EnergyHelper.energyRegenMultiplier });
                    if (_correctEnergy) EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
                }
            }
            if (_roomRejected || _fixtures.Count == 0 || _real == null || !InsideHost() || Eligible(_real)) return;
            _roomRejected = true;
            // Release only the activity this probe opened, and prevent pickup before the next input handler.
            foreach (var fixture in _fixtures) if (fixture != null) fixture.gameObject.SetActive(false);
            if (_activity != null && ReferenceEquals(InstanceBehavior<UIs>.Instance?.playerActivityUI?.GetCurrentActivity, _activity))
                _activity.Finish();
            Observe("ownership changed; fixtures disabled");
            if (!Manager.enteringBuilding && !Manager.exitingBuilding) Manager.ExitFromBuilding(_exitId);
        }
    }
}
