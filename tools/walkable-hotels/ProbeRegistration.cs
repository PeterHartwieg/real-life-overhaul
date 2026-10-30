using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Helpers;
using Newtonsoft.Json;
using RealLifeOverhaul;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotelEntryProbe
{
    /// <summary>Test-only field comparisons and actual binary-save readback.</summary>
    public sealed partial class Probe
    {
        private Dictionary<string, string> _fieldsBefore;

        private void SelectHost()
        {
            _site = Object.FindObjectOfType<TravelDirector>()?.Hotels?.ById(_hotel);
            if (_site?.Building?.building == null || _site.Slot.Tier != HotelTier.MidRange)
                throw new InvalidOperationException("Needs a resolved midrange hotel host.");
            _hostAddress = _site.Building.building.Address;
            _exitId = _site.Building.entranceDoors[0].doorId;
            _real = BuildingHelper.GetBuildingRegistration(_hostAddress);
        }

        private void CaptureRegistration()
        {
            _before = Fingerprint();
            _fieldsBefore = Fields(_real);
            _dirtBefore = _real.dirtSpots;
            _designBefore = _real.interiorDesigns;
            _itemsBefore = _real.itemInstances;
        }

        private BuildingRegistration SavedRegistration(GameInstance game) => game?.BuildingRegistrations?.SingleOrDefault(
            r => r.StreetName == _real.StreetName && r.StreetNumber == _real.StreetNumber);

        private static string Hash(object value)
        {
            using var hash = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, new ProbeVectors()));
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static Dictionary<string, string> Fields(BuildingRegistration r) => new Dictionary<string, string>
        {
            ["ownership"] = Hash(new { r.StreetName, r.StreetNumber, r.RentedByPlayer, r.AvailableForRent, r.RentPerDay,
                r.BusinessName, r.businessTypeName, r.buildingOwnerRivalId }),
            ["layout"] = Hash(r.Layout), ["items"] = Hash(r.itemInstances),
            ["dirt"] = Hash(r.dirtSpots), ["designs"] = Hash(r.interiorDesigns)
        };

        private object CompareFields(BuildingRegistration r)
        {
            var hashes = Fields(r);
            return new { hashes, unchanged = hashes.ToDictionary(p => p.Key, p => _fieldsBefore[p.Key] == p.Value),
                dirtCount = r.dirtSpots?.Count, items = r.itemInstances?.Count, designs = r.interiorDesigns?.Count };
        }

        private void ReadSaved(string name)
        {
            if (_real == null || _fieldsBefore == null || string.IsNullOrEmpty(name) || name.Length > 160
                || !name.StartsWith("FT RLO Hotel ", StringComparison.Ordinal) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new InvalidOperationException("Capture a host first and supply a disposable checkpoint name.");
            string path = Path.Combine(SaveGamePathHelper.GetCharacterFolderPath(SaveGameManager.Current.characterId), name + ".hsg");
            var game = SaveGameSerializationHelper.DeserializeBinaryData(path);
            var saved = SavedRegistration(game);
            Respond(new { saved = name, registrationPresent = saved != null,
                fields = saved == null ? null : CompareFields(saved), data = saved == null ? null : RegistrationData(saved),
                game.CurrentStreetName, game.CurrentStreetNumber,
                playerPosition = new[] { game.LastPlayerPosition.x, game.LastPlayerPosition.y, game.LastPlayerPosition.z } });
        }
    }
}
