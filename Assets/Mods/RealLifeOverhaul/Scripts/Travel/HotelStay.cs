using System.Collections;
using BAModAPI;
using Dialogs;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A hotel stay from desk to checkout; with no hotel interiors in the game, the room is the travel list.</summary>
    public sealed partial class HotelStay
    {
        private readonly TravelDirector _director;
        private readonly HotelReception _reception;
        private bool _plain;               // no list could be made: the night is slept at once, then the player is outside
        private bool _sleeping;
        private bool _confirming;

        public HotelStay(TravelDirector director)
        {
            _director = director;
            _reception = new HotelReception(this, director);
        }

        public bool AtReception => _reception.IsOpen;

        public bool Busy => _sleeping;

        /// <summary>What a stay beginning tonight costs in all, and its average night.</summary>
        public static int Quote(HotelSlot hotel, RoomType room, int nights, out int perNight)
        {
            int first = TravelEffects.StayDay();
            float demand = TravelBridges.Leisure.DemandToday;
            float total = 0f;
            for (int night = 0; night < nights; night++)
            {
                total += TravelCatalogue.NightRate(hotel, room, first + night, TravelEffects.Weekday(first + night), demand);
            }
            total *= TravelCatalogue.LongStayFactor(nights);
            perNight = Mathf.Max(1, Mathf.RoundToInt(total / nights));
            return perNight * nights;
        }

        public static int Tonight(HotelSlot hotel, RoomType room)
        {
            int day = TravelEffects.StayDay();
            return TravelCatalogue.NightRate(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday);
        }

        public static int FreeTonight(HotelSlot hotel, RoomType room)
        {
            int day = TravelEffects.StayDay();
            return TravelCatalogue.RoomsFree(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday);
        }

        /// <summary>Nights of the booking that have begun, the one in progress included.</summary>
        public static int NightsBegun() => Mathf.Clamp(TravelEffects.StayDay() - TravelState.CheckInDay + 1, 1, Mathf.Max(TravelState.Nights, 1));

        public static int NightsLeft() => Mathf.Max(TravelState.Nights - NightsBegun(), 0);

        public static int CheckoutHour => TravelState.LateCheckout ? TravelCatalogue.LateCheckoutHour : TravelCatalogue.CheckoutHour;

        public static bool PastCheckout() => TravelState.HasRoom && TimeHelper.IsInThePast(TravelState.CheckoutDay, CheckoutHour);

        // Leaving a stay on deposit early: the nights begun are owed, the first having been paid at the desk.
        private static int OwedLeavingEarly(int begun)
        {
            return Mathf.RoundToInt(Mathf.Max(begun - 1, 0) * TravelState.NightRate * (1f + TravelCatalogue.DepositSurcharge));
        }

        private static int RefundFor(int unused, RoomType room) => Mathf.RoundToInt(unused * TravelState.NightRate * room.Refund);

        /// <summary>Takes a room from tonight, paid now or on deposit with the rest 5% dearer at checkout.</summary>
        public bool Book(HotelSite site, RoomType room, int nights, bool deposit, out string failKey)
        {
            failKey = null;
            if (TravelState.HasRoom)
            {
                failKey = "reallife_travel_desk_has_room";
                return false;
            }
            if (FreeTonight(site.Slot, room) <= 0)
            {
                failKey = "reallife_travel_desk_full";
                return false;
            }
            int total = Quote(site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightMost) : 0;
            int now = deposit ? Mathf.Max(perNight - voucher, 0) : Mathf.Max(total - voucher, 0);
            int later = deposit ? Mathf.RoundToInt((total - perNight) * (1f + TravelCatalogue.DepositSurcharge)) : 0;
            if (HotelReception.Owned(site.Slot.Id))
            {
                now = later = perNight = voucher = 0;   // the hotel's owner stays for nothing
            }
            if (!TravelEffects.Pay(now, "reallife_transaction_hotel"))
            {
                failKey = "reallife_travel_desk_no_money";
                return false;
            }
            if (voucher > 0)
            {
                TravelState.FreeNights--;
            }
            TravelState.ClearRoom();
            TravelState.HotelId = site.Slot.Id;
            TravelState.RoomId = room.Id;
            TravelState.CheckInDay = TravelEffects.StayDay();
            TravelState.Nights = nights;
            TravelState.Paid = now;
            TravelState.Owed = later;
            TravelState.NightRate = perNight;
            _director.Log("Travel: booked " + room.Id + " at " + site.Slot.Id + " for " + nights + " night(s): " + now + " paid, " + later + " owed at checkout.");
            return true;
        }

        public void BookPackage(HotelSite site, RoomType room, int nights)
        {
            TravelState.ClearRoom();
            TravelState.HotelId = site.Slot.Id;
            TravelState.RoomId = room.Id;
            TravelState.CheckInDay = TravelEffects.StayDay();
            TravelState.Nights = nights;
            TravelState.NightRate = Tonight(site.Slot, room);
            TravelState.Package = true;
        }

        /// <summary>Gives the room up, settling what is owed or refunded and a stamp a night. Returns the line to show.</summary>
        public string CheckOutNow(bool atDesk)
        {
            if (!TravelState.HasRoom)
            {
                return null;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            int begun = NightsBegun();
            int unused = PastCheckout() ? 0 : Mathf.Max(TravelState.Nights - begun, 0);
            int refund = 0;
            int owed = TravelState.Owed;
            if (unused > 0 && !TravelState.Package)
            {
                if (owed > 0)
                {
                    owed = OwedLeavingEarly(begun);
                }
                else
                {
                    refund = RefundFor(unused, room);
                }
            }
            if (owed > 0)
            {
                TravelEffects.Settle(-owed, "reallife_transaction_hotel");
            }
            if (refund > 0)
            {
                TravelEffects.Settle(refund, "reallife_transaction_hotel_refund");
            }
            bool package = TravelState.Package;
            int slept = TravelState.NightsSlept;
            if (!package)
            {
                TravelState.Stamps += begun;
                while (TravelState.Stamps >= TravelCatalogue.StampsForFreeNight)
                {
                    TravelState.Stamps -= TravelCatalogue.StampsForFreeNight;
                    TravelState.FreeNights++;
                }
            }
            TravelState.NightsEver += begun;
            string name = TravelText.T(hotel.NameKey);
            _director.Log("Travel: checked out of " + hotel.Id + " after " + begun + " night(s): " + owed + " settled, " + refund + " refunded, "
                          + TravelState.Stamps + " stamp(s), " + TravelState.FreeNights + " free night(s).");
            TravelState.ClearRoom();
            _confirming = false;
            if (package)
            {
                _director.Holidays.WeekendOver(slept);
            }
            if (refund > 0)
            {
                return TravelText.T("reallife_travel_checkout_refund", "hotel", name, "amount", TravelText.Money(refund));
            }
            return owed > 0
                ? TravelText.T("reallife_travel_checkout_settled", "hotel", name, "amount", TravelText.Money(owed))
                : TravelText.T(atDesk ? "reallife_travel_checkout_done" : "reallife_travel_checkout_auto", "hotel", name);
        }

        /// <summary>Once an hour: a room past its checkout time is given up, wherever the player is.</summary>
        public void HourTick()
        {
            if (!TravelState.HasRoom || _sleeping || !PastCheckout())
            {
                return;
            }
            if (_director.Visit.Active) { _director.Visit.Leave(true); return; }
            if (TravelState.InRoom)
            {
                LeaveRoom();
            }
            TravelText.Toast(NotificationType.Info, CheckOutNow(false), 10f);
        }

        public bool EnterRoom(out TestRefusal why, HotelSite site = null, bool physical = true)
        {
            why = TestRefusal.None;
            if (!TravelState.HasRoom)
            {
                why = TestRefusal.NothingFits;
                return false;
            }
            if (PastCheckout())
            {
                HourTick();
                why = TestRefusal.NothingFits;
                return false;
            }
            site ??= _director.Hotels.ById(TravelState.HotelId);
            if (site == null || site.Slot.Id != TravelState.HotelId || _director.Visit.Active)
            {
                why = TestRefusal.NothingFits;
                return false;
            }
            if (physical && !TravelState.InRoom && _director.Visit.TryEnter(site))
            {
                return true;
            }
            // Going up to the room opens a list, not a scene, so a paused game may.
            if (!_director.CanSeclude(out why, pausedIsFine: true))
            {
                return false;
            }
            TravelState.InRoom = true;
            _confirming = false;
            _plain = false;
            _director.Seclude(true);
            Morning();
            Show();
            return true;
        }

        /// <summary>Back to the pavement; the room is kept.</summary>
        public void LeaveRoom()
        {
            if (_director.Visit.Active) { _director.Visit.Leave(false); return; }
            if (_sleeping)
            {
                StopSleep();
            }
            TravelState.InRoom = false;
            _confirming = false;
            _plain = false;
            _director.Seclude(false);
            _director.Window.Close();
        }

        public void Resume()
        {
            if (TravelState.InRoom && !EnterRoom(out _, physical: false))
            {
                TravelState.InRoom = false;
            }
        }

        /// <summary>The watchdog found the stay in a state it cannot be in: the player is simply outside again.</summary>
        public void ForceOut()
        {
            _sleeping = false;
            _confirming = false;
            _plain = false;
            TravelState.InRoom = false;
            EndSleepEffects();
        }

        /// <summary>Reception: the game's list of rooms, or if that cannot be made, the same questions in the game's dialogue panel.</summary>
        public void OpenDesk(HotelSite site)
        {
            if (_reception.Open(site))
            {
                return;
            }
            HotelDeskDialog.Site = site;
            HotelDeskDialog.Stay = this;
            HotelDeskDialog.Booked = false;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_hoteldesk_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new HotelDeskDialog()));
            InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(AfterDesk));
        }

        // The conversation is over: with a room just taken, straight up to it.
        private void AfterDesk()
        {
            if (HotelDeskDialog.Booked && TravelState.HasRoom && TravelDirector.Instance != null)
            {
                HotelDeskDialog.Booked = false;
                _director.Run(EnterSoon(HotelDeskDialog.Site));
            }
        }

        public void GoUpSoon(HotelSite site)
        {
            _director.Run(EnterSoon(site));
        }

        private IEnumerator EnterSoon(HotelSite site)
        {
            yield return new WaitForSecondsRealtime(0.4f);   // the dialogue panel or the list closes first
            EnterRoom(out _, site);
        }
    }
}
