using System;
using System.IO;
using BigAmbitions.Items;
using BusinessLayoutSets;
using Controllers;
using Helpers;
using PlayerActivity;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Native L1 furniture loaded from the installed game. Nothing is added to the saved item dictionary.</summary>
    public sealed partial class HotelVisit
    {
        private BedController _bed;
        private ItemController _desk;
        private SleepEnvironment _environment;
        private void Furnish()
        {
            if (!Inside || !Eligible(_real) || Manager.building.BuildingSize != "ba:buildingsize_l" || Manager.building.BuildingVersion != 1)
            {
                throw new InvalidOperationException("The hotel shell changed during entry.");
            }

            var layout = BusinessLayoutSetHelper.Deserialize(Path.Combine(Application.streamingAssetsPath, "InteriorDesigns", "Residential", "L1", "Modern.json"));
            var empty = BusinessLayoutSetHelper.Deserialize(Path.Combine(Application.streamingAssetsPath, "BusinessLayouts", "Empty", "L1", "default.json"));
            if (layout == null || empty == null || layout.BuildingSize != _host.BuildingSize || layout.BuildingVersion != _host.BuildingVersion)
            {
                throw new InvalidOperationException("The installed game's hotel furnishing source is unavailable.");
            }

            foreach (var item in layout.Items)
            {
                if (item.itemName == "ba:itemname_kingsizebed")
                {
                    _bed = Place("ba:itemname_beddoublewooden", item.position, item.rotation) as BedController;
                }
                else if (item.itemName == "ba:itemname_officedesk2left")
                {
                    _desk = Place(item.itemName, item.position, item.rotation);
                }
                else if (item.itemName == "ba:itemname_armchair1" || Decoration(item.itemName))
                {
                    Place(item.itemName, item.position, item.rotation);
                }
            }

            Place("ba:itemname_shower", new Vector3(1101.5f, 0f, -173f), Quaternion.identity);
            foreach (var item in empty.Items)
            {
                if (item.itemName.StartsWith("ba:itemname_ceilinglamp", StringComparison.Ordinal))
                {
                    Place(item.itemName, item.position, item.rotation);
                }
            }

            _environment = _bed == null ? null : GameContract.HotelBedEnvironment.Field?.GetValue(_bed) as SleepEnvironment;
            if (_bed == null || _desk == null || _bed.Item.quality != 0 || _environment == null || _environment.EnergyRegen != EnergyRegen.Bed)
            {
                throw new InvalidOperationException("The native bed does not match the Standard room's sleep rate.");
            }

            Manager.ForceUpdateAvailableProducers();
            foreach (ItemController item in _fixtures)
            {
                item.ForceUpdateNavMesh();
            }
        }

        private static bool Decoration(string name)
        {
            return name == "ba:itemname_wardrobe" || name == "ba:itemname_smallcabinet"
                   || name == "ba:itemname_colorfulcarpet" || name == "ba:itemname_patterncarpet"
                   || name == "ba:itemname_coffeetable2" || name == "ba:itemname_coffeetable3"
                   || name == "ba:itemname_deskplant01" || name == "ba:itemname_groundplant02"
                   || name == "ba:itemname_groundplant03" || name == "ba:itemname_groundplant04"
                   || name == "ba:itemname_pictureframe2" || name == "ba:itemname_pictureframe3"
                   || name == "ba:itemname_pictureframe4" || name == "ba:itemname_pictureframe5";
        }

        private ItemController Place(string name, Vector3 at, Quaternion rotation)
        {
            ItemController item = PrefabHelper.CreatePrefabItem(name, Manager.IndoorItemContainer);
            if (item == null)
            {
                throw new InvalidOperationException("A hotel fixture is unavailable: " + name);
            }

            _fixtures.Add(item);
            item.itemName = name;
            item.ItemInstance = ItemHelper.InitializeNewInstance(name);
            item.playerItemPurchaserSettings = new PlayerItemPurchaserSettings
            {
                enabled = false
            };
            item.transform.SetPositionAndRotation(at, rotation);
            if (item is IndoorLightController)
            {
                GameContract.HotelLampAlwaysOn.Field.SetValue(item, true);
            }

            item.gameObject.SetActive(true);
            return item;
        }

        private void VerifyRoutes()
        {
            if (!Inside || !Eligible(_real) || Manager.exitZones == null || Manager.exitZones.Count == 0)
            {
                throw new InvalidOperationException("Hotel navigation lost its native exit.");
            }

            Vector3 entrance = Manager.exitZones[0].playerSpawnPoint.position;
            var path = new NavMeshPath();
            var agent = PlayerHelper.PlayerController.Character.navmeshAgent;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            foreach (ItemController item in _fixtures)
            {
                if (!(item is BedController) && !(item is HygieneItemController) && !(item is SeatController) && item != _desk)
                {
                    continue;
                }

                Vector3 target = item.GetClosestNavMeshTargetPosition(entrance);
                if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, filter)
                    || !NavMesh.CalculatePath(entrance, hit.position, filter, path)
                    || path.status != NavMeshPathStatus.PathComplete
                    || !NavMesh.CalculatePath(hit.position, entrance, filter, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    throw new InvalidOperationException("A hotel fixture cannot be reached from the entrance: " + item.itemName);
                }
            }
        }
    }
}
