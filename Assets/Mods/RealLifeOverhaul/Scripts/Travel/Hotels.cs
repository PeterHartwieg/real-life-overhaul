using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One hotel as it stands in the city this session.</summary>
    public sealed class HotelSite
    {
        public HotelSlot Slot;
        public CityBuildingController Building;
        public Vector3 Desk;            // where the player stands to be served: the door, or the cottages' board
        public Vector3 Front;
        public bool Dressed;            // the frontage was looked at, and whatever could be stood there stands
        public Transform Dressing;      // sign, awning, luggage cart
        public bool HasDoormanSpot;
        public Vector3 DoormanSpot;
        public Vector3 DoormanFace;
        public string Name;
    }

    /// <summary>The city's hotels, which the game lacks, picked from static data so every session and machine agrees.</summary>
    public sealed partial class Hotels
    {
        private const string Residential = "ba:buildingtype_residential";
        private const float WaitForUtilities = 90f;   // real seconds at most

        // Distances in metres.
        private const float DressWithin = 150f;
        private const float DeskReach = 3.6f;
        private const float BoardReach = 1.9f;

        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly List<HotelSite> _sites = new List<HotelSite>(8);
        private readonly HashSet<Building> _taken = new HashSet<Building>();
        private bool _resolved;
        private float _waitUntil;   // the real time until which the hotels wait for City Utilities to choose first
        private int _look;

        public Hotels(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
        }

        public IReadOnlyList<HotelSite> Sites => _sites;

        public HotelSite InReach { get; private set; }

        /// <summary>Changes each time a hotel moves to another building (for the map).</summary>
        public int Moves { get; private set; }

        public HotelSite ById(string id)
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_sites[i].Slot.Id == id)
                {
                    return _sites[i];
                }
            }
            return null;
        }

        /// <summary>The nearest hotel of a tier, or of any with null, to a point as the crow flies.</summary>
        public HotelSite Nearest(Vector3 from, HotelTier? tier, out float distance)
        {
            HotelSite best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _sites.Count; i++)
            {
                if (tier.HasValue && _sites[i].Slot.Tier != tier.Value)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(_sites[i].Desk - from);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = _sites[i];
                }
            }
            distance = best != null ? Mathf.Sqrt(bestSqr) : 0f;
            return best;
        }

        /// <summary>Picks the buildings, once per city load when the save is ready. False if the city is not there yet.</summary>
        public bool Resolve()
        {
            if (_resolved)
            {
                return true;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null || BuildingHelper.allBuildings == null || BuildingHelper.allBuildings.Count == 0)
            {
                return false;
            }
            Func<bool> first = TravelBridges.UtilitiesPicked;
            if (first != null && !first())
            {
                if (_waitUntil <= 0f)
                {
                    _waitUntil = Time.unscaledTime + WaitForUtilities;
                }
                if (Time.unscaledTime < _waitUntil)
                {
                    return false;   // City Utilities chooses its residential building first, in the same order on every machine
                }
            }
            _resolved = true;
            _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
            var said = new System.Text.StringBuilder("Travel: hotels:");
            foreach (HotelSlot slot in TravelCatalogue.Hotels)
            {
                CityBuildingController building = slot.Anchors != null ? Anchor(city, slot) : Pick(city, slot, slot.Sizes) ?? Pick(city, slot, null);
                if (building == null || !Door(building, out Vector3 door))
                {
                    said.Append(' ').Append(slot.Id).Append("=none");
                    continue;
                }
                _taken.Add(building.building);
                var site = new HotelSite { Slot = slot, Building = building, Desk = door, Front = door, Name = TravelText.T(slot.NameKey) };
                _sites.Add(site);
                if (slot.Anchors == null)
                {
                    // The game names the building after the hotel; the cottages' board stands by somebody else's building.
                    SpecialBuildings.Set("hotel_" + slot.Id, building, SpecialKind.Hotel, slot.NameKey, "reallife_map_card_hotel", () => Settings.Hotels);
                }
                said.Append(' ').Append(slot.Id).Append('=').Append(building.building.StreetName).Append(' ').Append(building.building.StreetNumber);
            }
            _logger?.Info(said.ToString());
            return true;
        }

        /// <summary>Once a second; every fifth, a hotel in a building now a player's moves on, unless in use.</summary>
        public void Keep()
        {
            if (!_resolved || ++_look < 5)
            {
                return;
            }
            _look = 0;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            for (int i = 0; city != null && i < _sites.Count; i++)
            {
                HotelSite site = _sites[i];
                if (site.Slot.Anchors != null || site.Building == null || !Mine(site.Building.building)
                    || (TravelState.InRoom && TravelState.HotelId == site.Slot.Id)
                    || TravelDirector.Instance?.Visit?.HotelId == site.Slot.Id)
                {
                    continue;
                }
                CityBuildingController next = Pick(city, site.Slot, site.Slot.Sizes) ?? Pick(city, site.Slot, null);
                if (next == null || !Door(next, out Vector3 door))
                {
                    continue;   // nowhere else in its district: it stays, under the game's own name while it is the player's
                }
                string was = site.Building.building.StreetName + " " + site.Building.building.StreetNumber;
                if (_doormanAt == site)
                {
                    ShelveDoorman();
                }
                if (site.Dressing != null)
                {
                    UnityEngine.Object.Destroy(site.Dressing.gameObject);
                    site.Dressing = null;
                }
                _taken.Remove(site.Building.building);
                _taken.Add(next.building);
                site.Building = next;
                site.Desk = door;
                site.Front = door;
                site.Dressed = false;
                site.HasDoormanSpot = false;
                SpecialBuildings.Set("hotel_" + site.Slot.Id, next, SpecialKind.Hotel, site.Slot.NameKey, "reallife_map_card_hotel", () => Settings.Hotels);
                Moves++;
                _logger?.Info("Travel: " + site.Slot.Id + " was at " + was + ", which is the player's now; it has moved to "
                              + next.building.StreetName + " " + next.building.StreetNumber + ".");
            }
        }

        public void Tick(Vector3 player, bool onFoot, int hour)
        {
            HotelSite reach = null;
            HotelSite near = null;
            float nearSqr = DoormanWithin * DoormanWithin;
            for (int i = 0; i < _sites.Count; i++)
            {
                HotelSite site = _sites[i];
                Vector3 offset = site.Desk - player;
                float sqr = CitySpots.Flat(offset);
                if (!site.Dressed && Settings.Hotels && sqr < DressWithin * DressWithin)
                {
                    Dress(site);   // hotels are far apart: at most one is this near
                }
                float reachMetres = site.Slot.Anchors != null ? BoardReach : DeskReach;
                if (onFoot && Mathf.Abs(offset.y) < 2.5f && sqr < reachMetres * reachMetres)
                {
                    reach = site;
                }
                if (site.Slot.Doorman && site.HasDoormanSpot && sqr < nearSqr)
                {
                    nearSqr = sqr;
                    near = site;
                }
            }
            InReach = reach;
            TickDoorman(player, near, hour);
        }

        public void Away()
        {
            InReach = null;
        }

        public void Dispose()
        {
            ReleaseDoorman(true);
            for (int i = 0; i < _sites.Count; i++)
            {
                HotelSite site = _sites[i];
                if (site.Dressing != null)
                {
                    UnityEngine.Object.Destroy(site.Dressing.gameObject);
                }
            }
            _sites.Clear();
            _taken.Clear();
            InReach = null;
        }

        // The residential building of the district with the most passing trade; ties by street name, then number.
        private CityBuildingController Pick(CityManager city, HotelSlot slot, string sizes)
        {
            Building best = null;
            CityBuildingController bestController = null;
            List<Building> all = BuildingHelper.allBuildings;
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                if (b == null || b.BuildingType != Residential || b.Neighbourhood != slot.Neighbourhood || GameMembers.Fenced(b)
                    || (int)b.requiredDLC != 0 || b.IsHamptonsHouse() || _taken.Contains(b) || string.IsNullOrEmpty(b.BuildingSize)
                    || SpecialBuildings.Holds(b.StreetName, b.StreetNumber))   // another feature's building
                {
                    continue;
                }
                if (sizes != null && sizes.IndexOf(b.BuildingSize[b.BuildingSize.Length - 1]) < 0)
                {
                    continue;
                }
                if (best != null && !Better(b, best))
                {
                    continue;
                }
                if (Mine(b))
                {
                    continue;
                }
                CityBuildingController controller = city.FindCityBuildingController(b.Address);
                if (controller == null || !Door(controller, out _))
                {
                    continue;
                }
                best = b;
                bestController = controller;
            }
            return bestController;
        }

        private static bool Better(Building a, Building b)
        {
            if (a.trafficIndex != b.trafficIndex)
            {
                return a.trafficIndex > b.trafficIndex;
            }
            int street = string.CompareOrdinal(a.StreetName, b.StreetName);
            return street != 0 ? street < 0 : a.StreetNumber < b.StreetNumber;
        }

        private static bool Mine(Building building)
        {
            try
            {
                BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(building.Address);
                return registration == null || registration.RentedByPlayer || registration.BuildingOwnedByPlayer;
            }
            catch (Exception)
            {
                return true;   // a building the game cannot answer for is no hotel
            }
        }

        // The cottages have no building of their own: their board stands by the first of these that is there.
        private static CityBuildingController Anchor(CityManager city, HotelSlot slot)
        {
            foreach (string anchor in slot.Anchors)
            {
                int bar = anchor.IndexOf('|');
                if (bar > 0 && int.TryParse(anchor.Substring(bar + 1), out int number))
                {
                    CityBuildingController controller = city.FindCityBuildingController(new Address(anchor.Substring(0, bar), number));
                    if (controller != null && controller.building != null && Door(controller, out _))
                    {
                        return controller;
                    }
                }
            }
            return null;
        }

        /// <summary>Where a building's first entrance door is; false if it has none.</summary>
        internal static bool Door(CityBuildingController building, out Vector3 door)
        {
            door = default;
            if (building == null || building.entranceDoors == null)
            {
                return false;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    door = building.entranceDoors[i].doorTransform.position;
                    return true;
                }
            }
            return false;
        }
    }
}
