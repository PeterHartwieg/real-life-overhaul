using System;
using System.Collections.Generic;
using Controllers;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using PlayerActivity;
using UI;

namespace RealLifeOverhaul
{
    /// <summary>The game's item click actions, restricted to this visit's bed and service desk.</summary>
    public sealed partial class HotelVisit
    {
        /// <summary>Answers only for this visit's fixtures; faults are handled after the game's list iteration.</summary>
        private sealed class RoomCta : ICtaBehavior
        {
            private readonly HotelVisit _visit;
            internal RoomCta(HotelVisit visit)
            {
                _visit = visit;
            }

            public override bool ShouldShow(EntityController item)
            {
                try
                {
                    return _visit.Ready && item is ItemController fixture && _visit._fixtures.Contains(fixture)
                           && (item == _visit._bed || item == _visit._desk || item is HygieneItemController || item is SeatController);
                }
                catch (Exception error)
                {
                    _visit._pendingFault = error;
                    _visit._pendingGuarded = true;
                    return Guard.Failed(FrameGuard, error, false);
                }
            }

            public override (string, Action) GetCta(EntityController item)
            {
                try
                {
                    if (!ShouldShow(item))
                    {
                        return (string.Empty, null);
                    }

                    if (item == _visit._bed)
                    {
                        return ("reallife_travel_click_bed", _visit._useBed);
                    }

                    if (item == _visit._desk)
                    {
                        return ("reallife_travel_click_services", _visit._useDesk);
                    }

                    return item is HygieneItemController ? ("reallife_travel_click_shower", _visit._useBath) : ("reallife_travel_click_seat", _visit._useSeat);
                }
                catch (Exception error)
                {
                    _visit._pendingFault = error;
                    _visit._pendingGuarded = true;
                    return Guard.Failed<(string, Action)>(FrameGuard, error, (string.Empty, null));
                }
            }
        }

        private RoomCta _offer;
        private List<ICtaBehavior> _offers;
        private Action _useBed, _useDesk, _useBath, _useSeat;
        private void InstallOffers()
        {
            _offers = GameContract.CtaBehaviours.Field?.GetValue(null) as List<ICtaBehavior>;
            if (_offers == null)
            {
                throw new InvalidOperationException("The game's hotel click actions are unavailable.");
            }

            _useBed = () => Use(0);
            _useDesk = () => Use(1);
            _useBath = () => Use(2);
            _useSeat = () => Use(3);
            _offer = new RoomCta(this);
            _offers.Insert(0, _offer);
            foreach (ItemController item in _fixtures)
            {
                if (item == _bed || item == _desk || item is HygieneItemController || item is SeatController)
                {
                    item.simpleOverlayType |= SimpleOverlayType.ClickToAction;
                }
            }
        }

        private void RemoveOffers()
        {
            if (_offers != null && _offer != null)
            {
                _offers.Remove(_offer);
            }

            _offers = null;
            _offer = null;
        }

        private void Use(int kind)
        {
            try
            {
                if (!Ready
                    || HotelStay.PastCheckout()
                    || GoingPublic.InSession
                    || PlayerHelper.IsHoldingItem
                    || PlayerHelper.IsUsingVehicle
                    || CurrentActivity != null
                    || Manager.enteringBuilding
                    || Manager.exitingBuilding)
                {
                    return;
                }

                if (kind == 1)
                {
                    _director.Stay.OpenServices();
                    return;
                }

                if (kind == 0)
                {
                    PlayerActivityUI.Show(_environment, _bed);
                }
                else
                {
                    foreach (ItemController item in _fixtures)
                    {
                        if (kind == 2 && item is HygieneItemController bath)
                        {
                            bath.PerformActivity();
                            break;
                        }

                        if (kind == 3 && item is SeatController seat)
                        {
                            seat.GetSeatControllerCta().Item2?.Invoke();
                            break;
                        }
                    }
                }

                _activity = CurrentActivity;
            }
            catch (Exception error)
            {
                _pendingFault = error;
            }
        }
    }
}
