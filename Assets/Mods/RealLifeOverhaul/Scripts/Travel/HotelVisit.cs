using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Buildings;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One physical hotel visit, independent of the menu room. Owns only temporary furniture and its activities;
    /// never substitutes a building registration. Idle cost: one phase comparison per frame.
    /// </summary>
    public sealed partial class HotelVisit : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("travel", "HotelVisit", typeof(HotelVisit));
        private static readonly int HotUpdate = HotSpots.Slot("Travel: HotelVisit.Update");
        private Exception _pendingFault;
        private bool _pendingGuarded;
        private enum Phase
        {
            Outside,
            Entering,
            Furnishing,
            Ready,
            Leaving
        }

        private Phase _phase;
        private TravelDirector _director;
        private HotelSite _site;
        private Building _host;
        private BuildingRegistration _real;
        private ulong _revision;
        private int _exit;
        private float _deadline, _settle, _exitAfter;
        private bool _checkout, _faulted, _blocked, _restore, _exitRequested;
        private bool _recoverPending;
        private float _recoverAfter, _recoverUntil;
        private Action<Address> _changed, _changeSeen;
        private IPlayerActivity _activity;
        private HotelSleep _sleep;
        private bool _sleeping;
        private readonly List<ItemController> _fixtures = new List<ItemController>(20);
        private static BuildingManager Manager => InstanceBehavior<BuildingManager>.Instance;
        private static IPlayerActivity CurrentActivity => InstanceBehavior<UIs>.Instance?.playerActivityUI?.GetCurrentActivity;
        public bool Active => _phase != Phase.Outside;
        public string HotelId => Active ? _site?.Slot.Id : null;
        public bool Ready => _phase == Phase.Ready && SameBooking && Inside && Eligible(_real);
        public bool PrivateRoom => Active && Inside && SameBooking && Eligible(_real);
        private bool SameBooking => TravelState.HasRoom && TravelState.BookingRevision == _revision && TravelState.HotelId == _site?.Slot.Id;
        private bool Inside => BuildingManager.IsInsideBuilding && SameAddress(Manager?.building, _host);

        private static bool SameAddress(Building a, Building b) => a != null && b != null && a.StreetName == b.StreetName && a.StreetNumber == b.StreetNumber;
        internal void Initialise(TravelDirector director)
        {
            _director = director;
            _changed = Guard.Wrap<Address>(FrameGuard, Changed);
        }

        internal static bool Eligible(BuildingRegistration r) => r != null
            && HotelVisitRules.Empty(r.RentedByPlayer, r.BuildingOwnedByPlayer, r.HasEstablishedBusiness, r.Layout,
                r.businessTypeName, r.itemInstances?.Count ?? -1);

        /// <summary>Unsupported hosts keep the menu experience. A started transition is owned until exit settles.</summary>
        internal bool TryEnter(HotelSite site)
        {
            if (Active || _faulted || !Settings.Hotels || site?.Building?.building == null
                || TravelState.HotelId != site.Slot.Id || HotelStay.PastCheckout() || GameContract.HotelRoom.Off || FrameGuard.Off
                || TravelState.InRoom || PlayerHelper.IsHoldingItem || PlayerHelper.IsUsingVehicle
                || !_director.CanSeclude(out _, pausedIsFine: true))
            {
                return false;
            }
            Building host = site.Building.building;
            var real = BuildingHelper.GetBuildingRegistration(host.Address);
            string claim = SpecialBuildings.FiledAt(site.Building);
            string reservation = SpecialBuildings.ReservedFor(host.StreetName, host.StreetNumber);
            if (!HotelVisitRules.Supported(TravelState.RoomId, host.BuildingSize, host.BuildingVersion, GoingPublic.InSession)
                || !Eligible(real)
                || (claim != null
                && claim != "hotel_" + site.Slot.Id)
                || (reservation != null
                && reservation != "hotel_" + site.Slot.Id)
                || site.Building.entranceDoors == null
                || site.Building.entranceDoors.Length == 0
                || CitySpots.Flat(site.Desk - PlayerHelper.GetPosition()) > 6f * 6f)
            {
                return false;
            }

            _site = site;
            _host = host;
            _real = real;
            _revision = TravelState.BookingRevision;
            _exit = site.Building.entranceDoors[0].doorId;
            _phase = Phase.Entering;
            _deadline = Time.unscaledTime + 90f;
            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _changed, ref _changeSeen);
            try
            {
                if (!Manager.EnterBuilding(host, false, false, 0, _exit))
                {
                    throw new InvalidOperationException("Native hotel entry was refused.");
                }

                return true;
            }
            catch (Exception error)
            {
                Fail(error);
                return Active;
            }
        }

        /// <summary>Rebuilds a saved room at its safe entrance; an unfinished activity is never resumed or credited.</summary>
        internal void RecoverLoad()
        {
            _recoverPending = true;
            _recoverAfter = Time.unscaledTime + 1.5f;
            _recoverUntil = Time.unscaledTime + 90f;
        }

        private void RecoverSettledLoad()
        {
            if (Time.unscaledTime >= _recoverUntil)
            {
                _recoverPending = false;
                throw new TimeoutException("The native load transition did not settle for hotel recovery.");
            }

            if (Time.unscaledTime < _recoverAfter || LoadScene.isLoading || Manager == null || Manager.enteringBuilding || Manager.exitingBuilding)
            {
                return;
            }

            _recoverPending = false;
            if (Active || !BuildingManager.IsInsideBuilding || TravelState.InRoom || TravelState.Away || Manager?.building == null)
            {
                return;
            }

            foreach (HotelSite site in _director.Hotels.Sites)
            {
                if (!SameAddress(Manager.building, site.Building?.building) || !Eligible(Manager.buildingRegistration))
                {
                    continue;
                }

                _site = site;
                _host = site.Building.building;
                _real = Manager.buildingRegistration;
                _revision = TravelState.BookingRevision;
                _exit = site.Building.entranceDoors != null && site.Building.entranceDoors.Length > 0 ? site.Building.entranceDoors[0].doorId : 0;
                _phase = Phase.Entering;
                _restore = true;
                _deadline = Time.unscaledTime + 90f;
                if (!SameBooking || GameContract.HotelRoom.Off
                    || !HotelVisitRules.Supported(TravelState.RoomId, _host.BuildingSize, _host.BuildingVersion, GoingPublic.InSession)
                    || site.Building.entranceDoors == null || site.Building.entranceDoors.Length == 0)
                {
                    Leave(false);
                }

                return;
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (!Active && !_recoverPending)
            {
                return;
            }
            try
            {
                if (_recoverPending)
                {
                    RecoverSettledLoad();
                    if (!Active)
                    {
                        return;
                    }
                }

                if (_pendingFault != null)
                {
                    Exception error = _pendingFault;
                    _pendingFault = null;
                    Fail(error, _pendingGuarded);
                    _pendingGuarded = false;
                }

                if (FrameGuard.Off && _phase != Phase.Leaving)
                {
                    Leave(false);
                }

                Tick();
            }
            catch (Exception error)
            {
                Guard.Fail(FrameGuard, error);
                Fail(error, true);
            }
        }

        private void Tick()
        {
            if (LoadScene.isLoading || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                Clear();
                return;
            }

            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _changed, ref _changeSeen);
            if (Manager == null)
            {
                Clear();
                return;
            }

            if (GameMembers.PlayerDead || EnergyHelper.goingToHospital)
            {
                SettleCheckout();
                Clear();
                return;
            }

            if (_phase == Phase.Leaving)
            {
                if (!Inside && !Manager.enteringBuilding && !Manager.exitingBuilding)
                {
                    SettleCheckout();
                    Clear();
                    return;
                }

                if (Inside && !Manager.enteringBuilding && !Manager.exitingBuilding && !_exitRequested && Time.unscaledTime >= _exitAfter)
                {
                    SettleCheckout();
                    _exitRequested = true;
                    Manager.ExitFromBuilding(_exit);
                }

                if (Time.unscaledTime >= _deadline)
                {
                    _faulted = true;
                    _director.Fault(new TimeoutException("Native hotel recovery did not finish; temporary fixtures released and booking retained."));
                    SettleCheckout();
                    Clear();
                }

                return;
            }

            if (!SameBooking || !Settings.Hotels || GoingPublic.InSession || !Eligible(_real) || !ReferenceEquals(BuildingHelper.GetBuildingRegistration(_host.Address), _real))
            {
                Leave(false);
                return;
            }

            if (HotelStay.PastCheckout())
            {
                Leave(true);
                return;
            }

            AdoptActivity();
            ObserveSleep();
            if (_phase == Phase.Entering)
            {
                if (Manager.enteringBuilding)
                {
                    Timeout();
                    return;
                }

                if (!Inside)
                {
                    throw new InvalidOperationException("Native hotel entry ended outside the selected host.");
                }

                PlayerHelper.PlayerController.SetNavigationBlocker(NavigationBlocker.PaidActivity);
                _blocked = true;
                if (_restore)
                {
                    _restore = false;
                    if (Manager.exitZones == null
                        || Manager.exitZones.Count == 0
                        || !PlayerHelper.PlayerController.Character.navmeshAgent.Warp(Manager.exitZones[0].playerSpawnPoint.position))
                    {
                        throw new InvalidOperationException("The saved hotel visit has no safe entrance to rebuild from.");
                    }
                }

                Furnish();
                _settle = Time.unscaledTime + 1f;
                _phase = Phase.Furnishing;
            }
            else if (_phase == Phase.Furnishing && Time.unscaledTime >= _settle)
            {
                VerifyRoutes();
                InstallOffers();
                _phase = Phase.Ready;
                Unblock();
            }
            else if (_phase == Phase.Ready && (!Inside || Manager.exitingBuilding))
            {
                StopActivity(true);
                _phase = Phase.Leaving;
                _exitAfter = Time.unscaledTime;
                RemoveOffers();
            }
        }

        private void Timeout()
        {
            if (Time.unscaledTime >= _deadline)
            {
                throw new TimeoutException("Hotel entry did not complete within 90 seconds.");
            }
        }

        private void Changed(Address address)
        {
            if (!Active || _host == null || address?.streetName != _host.StreetName || address.streetNumber != _host.StreetNumber)
            {
                return;
            }

            try
            {
                if (!Eligible(_real))
                {
                    Leave(false);
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        /// <summary>Stops only this visit's activity. Booking settlement waits until Home has observed the native stop.</summary>
        public void Leave(bool checkout)
        {
            if (!Active)
            {
                return;
            }

            _checkout |= checkout;
            if (_phase == Phase.Leaving)
            {
                return;
            }

            _phase = Phase.Leaving;
            RemoveOffers();
            AdoptActivity();
            StopActivity(true);
            foreach (ItemController item in _fixtures)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }

            _director.Window.Close();
            Unblock();
            _exitAfter = Time.unscaledTime + 1.1f;
            _deadline = Time.unscaledTime + 90f;
            _exitRequested = false;
        }

        private void SettleCheckout()
        {
            if (!_checkout || !SameBooking)
            {
                return;
            }

            _checkout = false;
            TravelText.Toast(NotificationType.Info, _director.Stay.CheckOutNow(false), 10f);
        }

        private void AdoptActivity()
        {
            IPlayerActivity current = CurrentActivity;
            var entity = PlayerHelper.PlayerController?.Character?.CurrentEntityController as ItemController;
            if (current != null && !ReferenceEquals(current, _activity) && entity != null && _fixtures.Contains(entity))
            {
                CreditSleep();
                _activity = current;
            }
        }

        private void ObserveSleep()
        {
            if (_activity == null)
            {
                return;
            }

            var state = _activity.GetState();
            if (!_sleeping
                && _activity is SleepActivity
                && ReferenceEquals(CurrentActivity, _activity)
                && (state == PlayerActivityState.Started
                || state == PlayerActivityState.Running))
            {
                _sleep = new HotelSleep(TimeHelper.NowInMinutes(), TravelEffects.StayDay());
                _sleeping = true;
            }

            if (state == PlayerActivityState.Finished || !ReferenceEquals(CurrentActivity, _activity))
            {
                CreditSleep();
                _activity = null;
            }
        }

        private void CreditSleep()
        {
            if (!_sleeping)
            {
                return;
            }

            _sleeping = false;
            double checkout = new Timestamp(TravelState.CheckoutDay, HotelStay.CheckoutHour, 0f).GetTotalMinutes();
            _director.Stay.CompleteNight(TravelCatalogue.Room(_sleep.Room), _sleep, Math.Min(TimeHelper.NowInMinutes(), checkout));
        }

        private void StopActivity(bool credit)
        {
            if (_activity != null && ReferenceEquals(CurrentActivity, _activity) && _activity.GetState() != PlayerActivityState.Finished)
            {
                IPlayerActivity owned = _activity;
                var uis = InstanceBehavior<UIs>.Instance;
                if (!EnergyHelper.goingToHospital && !GameMembers.PlayerDead && uis?.timeMachine != null && uis.timeMachine.isRunning)
                {
                    uis.timeMachine.CancelTimeMachine(); // the native panel accounts elapsed activity before finishing
                }

                if (owned.GetState() != PlayerActivityState.Finished)
                {
                    owned.Finish();
                }
            }

            if (credit)
            {
                CreditSleep();
            }
            else
            {
                _sleeping = false;
            }

            _activity = null;
        }

        public int BedQuality
        {
            get
            {
                if (!Ready
                    || _bed == null
                    || !ReferenceEquals(PlayerHelper.PlayerController?.Character?.CurrentEntityController, _bed)
                    || !(_activity is SleepActivity)
                    || !ReferenceEquals(CurrentActivity, _activity))
                {
                    return -1;
                }

                var state = _activity.GetState();
                return state == PlayerActivityState.Started || state == PlayerActivityState.Running ? HotelStay.BedQuality(TravelCatalogue.Room(TravelState.RoomId)) : -1;
            }
        }

        private void Fail(Exception error, bool guarded = false)
        {
            _recoverPending = false;
            if (!_faulted)
            {
                _faulted = true;
                if (!guarded)
                {
                    Guard.Fail(FrameGuard, error);
                }
                _director.Log("Travel: physical hotel visits are off for this session after a fault; bookings and the menu room remain available.");
            }

            try
            {
                Leave(false);
            }
            catch (Exception again)
            {
                _director.Fault(again);
                Clear();
            }
        }

        private void Unblock()
        {
            if (!_blocked)
            {
                return;
            }

            _blocked = false;
            PlayerHelper.PlayerController?.UnsetNavigationBlocker(NavigationBlocker.PaidActivity);
        }

        private void Clear()
        {
            RemoveOffers();
            foreach (ItemController item in _fixtures)
            {
                if (item == null)
                {
                    continue;
                }

                if (Inside)
                {
                    item.gameObject.SetActive(false); // the native indoor container owns destruction until exit
                }
                else
                {
                    Destroy(item.gameObject);
                }
            }

            _fixtures.Clear();
            Unblock();
            GlobalEvents.onBuildingRegistrationChange -= _changed;
            _changeSeen = null;
            _phase = Phase.Outside;
            _site = null;
            _host = null;
            _real = null;
            _bed = null;
            _desk = null;
            _activity = null;
            _sleeping = _checkout = _restore = _exitRequested = false;
        }

        private void OnDestroy()
        {
            // Scene teardown owns the player and native UI. Only detach our resources here.
            GlobalEvents.onBuildingRegistrationChange -= _changed;
            RemoveOffers();
            try
            {
                Clear();
            }
            catch (Exception error)
            {
                _director?.Fault(error);
            }
        }
    }
}
