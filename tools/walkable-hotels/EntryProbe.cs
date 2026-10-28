using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BAModAPI;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: RegisterModClass(typeof(HotelEntryProbe.Entry))]

namespace HotelEntryProbe
{
    /// <summary>Test-only observer, installed separately from RLO in an owned disposable agent profile.</summary>
    [ModEntryOnCityLoad]
    public sealed class Entry : IModBigAmbitions
    {
        private GameObject _host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            if (Probe.Allowed())
            {
                _host = new GameObject("Hotel native entry probe");
                _host.AddComponent<Probe>().Root = context.ModRootPath;
            }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync()
        {
            if (_host != null) Object.Destroy(_host);
            _host = null;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the real registration before/after native entry, saves and exit. No furniture or shadow is installed:
    /// this isolates the first native gate, including mutations that precede the entry event. Polls only in FT RLO Hotel saves.
    /// </summary>
    public sealed partial class Probe : MonoBehaviour
    {
        public string Root;
        private string _last, _id, _op, _before, _hotel = "kitchen_hotel";
        private float _poll, _deadline;
        private bool _allowed;
        private float _settleUntil;
        private bool _entered, _exited;
        private Action<Address> _enterHandler, _exitHandler, _enterSeen, _exitSeen;
        private Action<Address> _changeHandler, _changeSeen;
        private Action _saveHandler, _saveSeen;
        private HotelSite _site;
        private BuildingRegistration _real;
        private Address _hostAddress;
        private int _exitId;
        private object _dirtBefore, _designBefore, _itemsBefore;
        private readonly List<object> _events = new List<object>();
        private static BuildingManager Manager => InstanceBehavior<BuildingManager>.Instance;

        public static bool Allowed()
        {
            string path = Application.persistentDataPath.Replace('\\', '/');
            return Environment.GetCommandLineArgs().Contains("-agent-tests")
                   && (path.EndsWith("/com.hovgaardgames.bigambitions.agenttests.mac", StringComparison.Ordinal)
                       || path.EndsWith("/Hovgaard Games/Big Ambitions Agent Tests", StringComparison.Ordinal));
        }

        private bool Disposable => _allowed && SaveGameManager.Current?.SaveGameName?.StartsWith("FT RLO Hotel ", StringComparison.Ordinal) == true;

        private void Awake()
        {
            _allowed = Allowed();
            _enterHandler = Entered;
            _exitHandler = Exited;
            _saveHandler = Saving;
            _changeHandler = RegistrationChanged;
            KeepEvents();
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterBuilding -= _enterHandler;
            GlobalEvents.onExitBuilding -= _exitHandler;
            GlobalEvents.onSaveGame -= _saveHandler;
            GlobalEvents.onBuildingRegistrationChange -= _changeHandler;
        }

        private void KeepEvents()
        {
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _enterHandler, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _exitHandler, ref _exitSeen);
            EventHold.Keep(ref GlobalEvents.onSaveGame, _saveHandler, ref _saveSeen);
            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _changeHandler, ref _changeSeen);
        }

        private void Entered(Address address) { _entered = true; Observe("entered"); }
        private void Exited(Address address) { _exited = true; Observe("exited"); }
        private void Saving() => Observe("save event");

        private void Observe(string phase)
        {
            try
            {
                if (_real != null && _before != null && Disposable && _events.Count < 32)
                    _events.Add(new { phase, frame = Time.frameCount, inside = BuildingManager.IsInsideBuilding,
                        sameManagerRegistration = ReferenceEquals(Manager?.buildingRegistration, _real), snapshot = Snapshot() });
            }
            catch (Exception e) { Debug.LogError("Hotel entry probe: " + e); }
        }

        private string Fingerprint() => JsonConvert.SerializeObject(RegistrationData(_real), new ProbeVectors());

        private static object RegistrationData(BuildingRegistration r) => new
        {
            r.StreetName, r.StreetNumber, r.AvailableForRent, r.RentedByPlayer, r.BuildingOwnedByPlayer,
            r.BusinessName, r.businessTypeName, r.Layout, r.itemInstances, r.dirtSpots, r.interiorDesigns
        };

        private object Snapshot() => _real == null || _before == null ? null : new
        {
            unchanged = Fingerprint() == _before,
            fields = CompareFields(_real),
            persistedReal = ReferenceEquals(SavedRegistration(SaveGameManager.Current), _real),
            canonicalDirt = InsideHost() ? Hash(_real.dirtSpots) == Hash(BuildingCleanlinessHelper.GetDirtSpotsForBuilding(_site.Building.building)) : (bool?)null,
            sameDirtList = ReferenceEquals(_real.dirtSpots, _dirtBefore),
            sameDesignList = ReferenceEquals(_real.interiorDesigns, _designBefore),
            sameItemDictionary = ReferenceEquals(_real.itemInstances, _itemsBefore),
            registeredObjectUnchanged = ReferenceEquals(BuildingHelper.GetBuildingRegistration(_site.Building.building.Address), _real)
        };

        private void Update()
        {
            if (!Disposable || string.IsNullOrEmpty(Root)) return;
            try
            {
                KeepEvents();
                CheckRoomOwnership();
                if (_id != null)
                {
                    if (Manager == null) throw new InvalidOperationException("Building manager was unloaded.");
                    bool finished = !Manager.enteringBuilding && !Manager.exitingBuilding
                                    && (_op == "enter" ? BuildingManager.IsInsideBuilding : !BuildingManager.IsInsideBuilding);
                    if (finished)
                    {
                        if (_settleUntil == 0) _settleUntil = Time.unscaledTime + 2f;
                        if (Time.unscaledTime < _settleUntil) return;
                        if (_op == "enter" && !InsideHost()) throw new InvalidOperationException("Entered a different building.");
                        if (_op == "enter" ? !_entered : !_exited) throw new InvalidOperationException("Native transition event was missed; result is inconclusive.");
                        Observe("settled " + _op);
                        Respond(new { operation = _op, snapshot = Snapshot(), events = _events.ToArray(), inside = BuildingManager.IsInsideBuilding });
                    }
                    else if (Time.unscaledTime >= _deadline) throw new TimeoutException("Native transition did not complete in 90 seconds. Inspect before sending exit.");
                    return;
                }
                if (Time.unscaledTime < _poll) return;
                _poll = Time.unscaledTime + .25f;
                string path = Path.Combine(Root, "request.json");
                if (!File.Exists(path) || new FileInfo(path).Length > 2048) return;
                string text = File.ReadAllText(path);
                if (text == _last) return;
                _last = text;
                JObject request = JObject.Parse(text);
                string id = (string)request["id"];
                if (string.IsNullOrEmpty(id) || id.Length > 60 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_'))) return;
                _id = id;
                _op = (string)request["op"];
                _hotel = (string)request["hotel"] ?? "kitchen_hotel";
                if (_op == "status")
                {
                    var director = Object.FindObjectOfType<TravelDirector>();
                    var site = director?.Hotels?.ById(_hotel);
                    Respond(new { available = site?.Building != null, inside = BuildingManager.IsInsideBuilding,
                        insideHost = InsideHost(), entering = Manager?.enteringBuilding, exiting = Manager?.exitingBuilding,
                        nativeAddress = Manager?.building?.Address?.ToString(),
                        vanillaCanEnter = site?.Building != null && BuildingHelper.CanEnterBuilding(site.Building.building.Address),
                        hosts = director?.Hotels?.Sites.Where(x => x.Slot.Tier == HotelTier.MidRange).Select(HostInfo).ToArray(),
                        hotel = site?.Slot.Id, address = site?.Building?.building?.Address?.ToString(), snapshot = Snapshot(), events = _events.ToArray() });
                }
                else if (_op == "capture")
                {
                    SelectHost();
                    CaptureRegistration();
                    Respond(new { snapshot = Snapshot(), data = RegistrationData(_real) });
                }
                else if (_op == "dismiss_test_overlay")
                {
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Hart.Campaign.CampaignHud")).FirstOrDefault(t => t != null);
                    var panel = type == null ? null : Object.FindObjectOfType(type);
                    var close = type?.GetMethod("ShowPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (panel == null || close == null) throw new InvalidOperationException("The isolated rig's campaign overlay is not available.");
                    close.Invoke(panel, new object[] { false });
                    Respond(new { closed = true });
                }
                else if (_op == "saved") ReadSaved((string)request["saveName"]);
                else if (_op == "room") Respond(RoomInfo());
                else if (_op == "claim") Claim((bool?)request["eventless"] == true);
                else if (_op == "hotelbook" || _op == "hotelenter" || _op == "hoteluse" || _op == "hotelroom" || _op == "hoteloff" || _op == "hotelon" || _op == "hotelclaim") Production((string)request["kind"]);
                else if (_op == "furnish") Furnish((string)request["bed"]);
                else if (_op == "bed" || _op == "bath" || _op == "seat") UseFixture(_op, (bool?)request["correctEnergy"] == true, (float?)request["energy"]);
                else if (_op == "enter") Begin();
                else if (_op == "exit")
                {
                    if (!InsideHost() || Manager.enteringBuilding || Manager.exitingBuilding) throw new InvalidOperationException("Not in the probe's settled host.");
                    _deadline = Time.unscaledTime + 90f;
                    _settleUntil = 0;
                    _exited = false;
                    Manager.ExitFromBuilding(_exitId);
                }
                else throw new InvalidOperationException("Use status, enter or exit.");
            }
            catch (Exception e)
            {
                Debug.LogError("Hotel entry probe: " + e);
                if (_id != null) Respond(new { error = e.ToString(), operation = _op });
            }
        }

        private bool InsideHost() => BuildingManager.IsInsideBuilding && _hostAddress != null
            && Manager?.building?.StreetName == _hostAddress.streetName && Manager.building.StreetNumber == _hostAddress.streetNumber;

        private void Begin()
        {
            if (GoingPublic.InSession || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || PlayerHelper.IsHoldingItem
                || Manager == null || Manager.enteringBuilding || Manager.exitingBuilding || TravelState.HasRoom || TravelState.Away)
                throw new InvalidOperationException("Needs an idle single-player disposable city, outside, empty-handed, without a booking or vehicle.");
            var director = Object.FindObjectOfType<TravelDirector>();
            if (director == null || !director.CanSeclude(out _, pausedIsFine: true))
                throw new InvalidOperationException("The player is busy or unavailable for native entry.");
            SelectHost();
            if (!Eligible(_real))
                throw new InvalidOperationException("Current host is ineligible: " + (_real == null ? "no registration" : JsonConvert.SerializeObject(new {
                    _real.AvailableForRent, _real.RentedByPlayer, _real.BuildingOwnedByPlayer, _real.HasEstablishedBusiness,
                    _real.businessTypeName, _real.Layout, itemCount = _real.itemInstances?.Count })));
            if (_site.Building.entranceDoors == null || _site.Building.entranceDoors.Length == 0)
                throw new InvalidOperationException("No native door.");
            CaptureRegistration();
            _events.Clear();
            _entered = false;
            _exited = false;
            _settleUntil = 0;
            Observe("before entry");
            _deadline = Time.unscaledTime + 90f;
            Manager.EnterBuilding(_site.Building.building, false, false, 0, _site.Building.entranceDoors[0].doorId);
        }

        // An empty host need not be listed for rent; occupancy is checked separately from that market flag.
        private static bool Eligible(BuildingRegistration real) => real != null && !real.RentedByPlayer
            && !real.BuildingOwnedByPlayer && !real.HasEstablishedBusiness && string.IsNullOrEmpty(real.Layout)
            && (real.itemInstances == null || real.itemInstances.Count == 0) && real.businessTypeName == "ba:businesstype_empty";

        private static object HostInfo(HotelSite site)
        {
            var r = site.Building != null ? BuildingHelper.GetBuildingRegistration(site.Building.building.Address) : null;
            return new { hotel = site.Slot.Id, address = site.Building?.building?.Address?.ToString(), eligible = Eligible(r),
                availableForRent = r?.AvailableForRent, rented = r?.RentedByPlayer, owned = r?.BuildingOwnedByPlayer,
                business = r?.HasEstablishedBusiness, type = r?.businessTypeName, layout = r?.Layout, items = r?.itemInstances?.Count };
        }

        private void Respond(object result)
        {
            string id = _id;
            _id = null;
            string path = Path.Combine(Root, "response-" + id + ".json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(result, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }

    /// <summary>Writes Unity values without following their computed normalised properties back into themselves.</summary>
    internal sealed class ProbeVectors : JsonConverter
    {
        public override bool CanRead => false;
        public override bool CanConvert(Type type) => type == typeof(Vector3) || type == typeof(Vector2) || type == typeof(Vector4)
                                                     || type == typeof(Quaternion) || type == typeof(Color);
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer) => throw new NotSupportedException();
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value is Vector3 v) { writer.WriteValue(v.x); writer.WriteValue(v.y); writer.WriteValue(v.z); }
            else if (value is Vector2 v2) { writer.WriteValue(v2.x); writer.WriteValue(v2.y); }
            else if (value is Vector4 v4) { writer.WriteValue(v4.x); writer.WriteValue(v4.y); writer.WriteValue(v4.z); writer.WriteValue(v4.w); }
            else if (value is Quaternion q) { writer.WriteValue(q.x); writer.WriteValue(q.y); writer.WriteValue(q.z); writer.WriteValue(q.w); }
            else if (value is Color c) { writer.WriteValue(c.r); writer.WriteValue(c.g); writer.WriteValue(c.b); writer.WriteValue(c.a); }
            writer.WriteEndArray();
        }
    }
}
