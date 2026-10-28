using System;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The room as a list of things to do, each with what it costs or does.</summary>
    public sealed partial class HotelStay
    {
        private const int Sleep = 1, Nap = 2, Wake = 3, Freshen = 4, RoomService = 5, Minibar = 6, Laundry = 7, Rest = 8, Messages = 9,
            GoOut = 10, Extend = 11, Late = 12, CheckOut = 13, Table = 14, Car = 15, Walk = 16, Dinner = 17, Golf = 18,
            ConfirmOut = 20, Back = 21;   // list entries
        internal const int WakeNow = 19;   // the one entry listened to while time is skipped

        private const int MinibarPerDay = 4;

        private const string ButtonDo = "reallife_travel_list_do";
        private const string ButtonChange = "reallife_travel_list_change";
        private const string ButtonChoose = "reallife_travel_list_choose";

        private Action<int> _onPick;
        private Action _onClosed;

        /// <summary>True while the night is being slept with no list, the game's window not being copyable.</summary>
        public bool PlainSleep => _plain;

        internal void OpenServices()
        {
            if (!_director.Visit.Ready)
            {
                return;
            }
            _confirming = false;
            Show();
        }

        public void Press(int id)
        {
            if (!TravelState.HasRoom || (!TravelState.InRoom && !_director.Visit.Ready))
            {
                return;
            }
            if (_director.Visit.Ready && (id == Sleep || id == Nap || id == Wake || id == Freshen || id == Rest))
            {
                return;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            if (_sleeping)
            {
                if (id == WakeNow)
                {
                    StopSleep();
                }
                return;
            }
            GameInstance save = SaveGameManager.Current;
            int today = TimeHelper.CurrentDay;
            switch (id)
            {
                case Sleep:
                    _director.Run(SleepRoutine(room, SleepGoal(), true));
                    return;
                case Nap:
                    _director.Run(SleepRoutine(room, Capped(TimeHelper.GetTimeInHours(2)), false));
                    return;
                case Wake:
                    TravelState.WakeHour = TravelState.WakeHour >= 10 ? 5 : TravelState.WakeHour + 1;
                    break;
                case Freshen:
                    if (TravelBridges.Home.Freshen(room.Quality))
                    {
                        Say(TravelText.T("reallife_travel_room_fresh"));
                    }
                    else if (TravelState.FreshDay != today)
                    {
                        TravelState.FreshDay = today;
                        TravelEffects.Mood(1);
                        Say(TravelText.T(room.Quality == 0 ? "reallife_travel_room_fresh_shared" : "reallife_travel_room_fresh"));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_fresh_again"));
                    }
                    break;
                case RoomService:
                    int meal = TravelCatalogue.MealPrice(hotel.Tier);
                    if (save.Hunger > 92f)
                    {
                        Say(TravelText.T("reallife_travel_room_not_hungry"));
                    }
                    else if (PaidExtra(meal))
                    {
                        TravelEffects.Feed(hotel.Tier == HotelTier.Budget ? 25f : hotel.Tier == HotelTier.Luxury ? 100f : 60f);
                        if (hotel.Tier == HotelTier.Luxury)
                        {
                            TravelEffects.Mood(1);
                        }
                        string served = hotel.Tier == HotelTier.Budget ? "reallife_travel_room_vending"
                            : hotel.Tier == HotelTier.Luxury ? "reallife_travel_room_service_lux" : "reallife_travel_room_service";
                        Say(TravelText.T(served, "amount", TravelText.Money(meal)));
                    }
                    break;
                case Minibar:
                    if (TravelState.MinibarDay != today)
                    {
                        TravelState.MinibarDay = today;
                        TravelState.MinibarCount = 0;
                    }
                    int snack = TravelCatalogue.MinibarPrice(hotel.Tier);
                    if (TravelState.MinibarCount >= MinibarPerDay)
                    {
                        Say(TravelText.T("reallife_travel_room_minibar_empty"));
                    }
                    else if (PaidExtra(snack))
                    {
                        TravelState.MinibarCount++;
                        TravelEffects.Feed(12f);
                        TravelEffects.Energy(3f);
                        Say(TravelText.T("reallife_travel_room_minibar", "amount", TravelText.Money(snack),
                            "left", TravelText.Number(MinibarPerDay - TravelState.MinibarCount)));
                    }
                    break;
                case Laundry:
                    int wash = TravelCatalogue.LaundryPrice(hotel.Tier);
                    if (TravelState.LaundryDay == today)
                    {
                        Say(TravelText.T("reallife_travel_room_laundry_again"));
                    }
                    else if (PaidExtra(wash))
                    {
                        TravelState.LaundryDay = today;
                        if (!TravelBridges.Home.Laundry(room.Quality))
                        {
                            TravelEffects.Mood(1);
                        }
                        Say(wash > 0
                            ? TravelText.T("reallife_travel_room_laundry", "amount", TravelText.Money(wash))
                            : TravelText.T("reallife_travel_room_laundry_free"));
                    }
                    break;
                case Rest:
                    _director.Run(RestRoutine(room));
                    return;
                case Messages:
                    Say(MessagesLine());
                    break;
                case GoOut:
                    LeaveRoom();
                    return;
                case Extend:
                    ExtendStay(hotel, room);
                    break;
                case Late:
                    int fee = LateFee(hotel.Tier);
                    if (TravelState.LateCheckout)
                    {
                        Say(TravelText.T("reallife_travel_room_late_already"));
                    }
                    else if (PaidExtra(fee))
                    {
                        TravelState.LateCheckout = true;
                        Say(fee > 0
                            ? TravelText.T("reallife_travel_room_late", "amount", TravelText.Money(fee), "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour))
                            : TravelText.T("reallife_travel_room_late_free", "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour)));
                    }
                    break;
                case CheckOut:
                    _confirming = true;
                    break;
                case Back:
                    _confirming = false;
                    break;
                case ConfirmOut:
                    if (_director.Visit.Active) { _director.Visit.Leave(true); return; }
                    LeaveRoom();
                    TravelText.Toast(NotificationType.Success, CheckOutNow(true), 10f);
                    return;
                case Table:
                    int table = TravelCatalogue.TablePrice;
                    if (TravelState.DinnerDay == today)
                    {
                        Say(TravelText.T("reallife_travel_room_table_again"));
                    }
                    else if (TravelBridges.Social.TableBooked(TravelText.T(hotel.NameKey)))
                    {
                        TravelState.DinnerDay = today;
                        Say(TravelText.T("reallife_travel_room_table_booked"));
                    }
                    else if (PaidExtra(table))
                    {
                        TravelState.DinnerDay = today;
                        TravelEffects.Feed(100f);
                        TravelEffects.Mood(2);
                        Say(TravelText.T("reallife_travel_room_table", "amount", TravelText.Money(table)));
                    }
                    break;
                case Car:
                    HotelSite site = _director.Hotels.ById(hotel.Id);
                    Say(TravelText.T(site != null && TravelBridges.Transport.CallCar(site.Front) ? "reallife_travel_room_car" : "reallife_travel_room_car_none"));
                    break;
                case Walk:
                    _director.Run(WalkRoutine());
                    return;
                case Dinner:
                    CottageDinner(today);
                    break;
                case Golf:
                    if (TravelBridges.Leisure.PlayGolf())
                    {
                        LeaveRoom();
                        return;
                    }
                    Say(TravelText.T("reallife_travel_room_golf_none"));
                    break;
            }
            Show();
        }

        private void CottageDinner(int today)
        {
            HotelSite site = _director.Hotels.ById(TravelState.HotelId);
            int dinner = TravelCatalogue.DinnerPrice;
            if (TravelState.DinnerDay == today)
            {
                Say(TravelText.T("reallife_travel_room_table_again"));
            }
            else if (site != null && TravelBridges.Leisure.DinnerOut(site.Front))
            {
                TravelState.DinnerDay = today;
                LeaveRoom();
            }
            else if (PaidExtra(dinner))
            {
                TravelState.DinnerDay = today;
                TravelEffects.Feed(100f);
                TravelEffects.Mood(2);
                Say(TravelText.T("reallife_travel_room_dinner", "amount", TravelText.Money(dinner)));
            }
        }

        private void ExtendStay(HotelSlot hotel, RoomType room)
        {
            int day = TravelState.CheckoutDay;
            if (TravelState.Package)
            {
                Say(TravelText.T("reallife_travel_room_extend_package"));
                return;
            }
            if (TravelCatalogue.RoomsFree(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday) <= 0)
            {
                Say(TravelText.T("reallife_travel_room_extend_full"));
                return;
            }
            int rate = ExtensionRate(hotel, room);
            if (TravelState.Owed > 0)
            {
                TravelState.Owed += Mathf.RoundToInt(rate * (1f + TravelCatalogue.DepositSurcharge));
            }
            else if (!TravelEffects.Pay(rate, "reallife_transaction_hotel"))
            {
                Say(TravelText.T("reallife_travel_room_no_money"));
                return;
            }
            else
            {
                TravelState.Paid += rate;
            }
            TravelState.Nights++;
            TravelState.LateCheckout = false;   // a late checkout bought for the old last day does not move with it
            Say(TravelText.T("reallife_travel_room_extended", "amount", TravelText.Money(rate), "day", TravelEffects.DayName(TravelState.CheckoutDay)));
        }

        private static int ExtensionRate(HotelSlot hotel, RoomType room)
        {
            int day = TravelState.CheckoutDay;
            return Mathf.RoundToInt(TravelCatalogue.NightRate(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday)
                                    * TravelCatalogue.LongStayFactor(TravelState.Nights + 1));
        }

        private static string CheckoutTerms(RoomType room)
        {
            int begun = NightsBegun();
            int unused = Mathf.Max(TravelState.Nights - begun, 0);
            if (TravelState.Package)
            {
                return TravelText.T("reallife_travel_room_out_package");
            }
            if (TravelState.Owed > 0)
            {
                int owed = unused > 0 ? OwedLeavingEarly(begun) : TravelState.Owed;
                return TravelText.T("reallife_travel_room_out_owed", "amount", TravelText.Money(owed));
            }
            if (unused == 0)
            {
                return TravelText.T("reallife_travel_room_out_plain");
            }
            int refund = RefundFor(unused, room);
            return refund > 0
                ? TravelText.T("reallife_travel_room_out_refund", "nights", TravelText.Number(unused), "amount", TravelText.Money(refund))
                : TravelText.T("reallife_travel_room_out_norefund", "nights", TravelText.Number(unused));
        }

        private static string MessagesLine()
        {
            int unread = 0;
            var contacts = SaveGameManager.Current.Contacts;
            if (contacts != null)
            {
                for (int i = 0; i < contacts.Count; i++)
                {
                    if (contacts[i] != null && contacts[i].messagesQueue != null)
                    {
                        unread += contacts[i].NumberOfUnreadMessages;
                    }
                }
            }
            string stamps = TravelText.T("reallife_travel_room_messages_stamps", "stamps", TravelText.Number(TravelState.Stamps),
                "need", TravelText.Number(TravelCatalogue.StampsForFreeNight), "free", TravelText.Number(TravelState.FreeNights));
            string messages = unread > 0
                ? TravelText.T("reallife_travel_room_messages", "count", TravelText.Number(unread))
                : TravelText.T("reallife_travel_room_messages_none");
            return messages + " " + stamps;
        }

        // Pays for an extra, or tells the player they cannot afford it.
        private static bool PaidExtra(int amount)
        {
            if (TravelEffects.Pay(amount, "reallife_transaction_hotel_extras"))
            {
                return true;
            }
            Say(TravelText.T("reallife_travel_room_no_money"));
            return false;
        }

        private static int LateFee(HotelTier tier) => tier == HotelTier.Luxury ? 0 : Mathf.RoundToInt(TravelState.NightRate * TravelCatalogue.LateCheckoutShare);

        // Before noon, the first visit of the day to a luxury room after a night slept there brings a small treat.
        private void Morning()
        {
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            int today = TimeHelper.CurrentDay;
            if (hotel.Tier != HotelTier.Luxury || TravelState.MorningDay == today || TravelState.NightsSlept == 0 || TimeHelper.CurrentHour >= 12)
            {
                return;
            }
            TravelState.MorningDay = today;
            TravelEvent treat = TravelState.Deal(TravelCatalogue.FineMornings, false, () => Random.value);
            TravelEffects.Apply(treat);
            if (treat != null)
            {
                Say(TravelText.T(treat.Key));
            }
        }

        // The room's entries in the game's list window, filled again whenever what they say may have changed.
        private void Show()
        {
            if (!TravelState.HasRoom || (!TravelState.InRoom && !_director.Visit.Ready))
            {
                return;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            TravelList list = _director.Window.Take(this);
            if (list == null)
            {
                if (!_director.Visit.Active)
                {
                    Plain(room);
                }
                return;
            }
            if (_sleeping)
            {
                // The time skip hides the HUD and its own Cancel is the way to get up: the list is put away until it ends.
                _director.Window.Close();
                return;
            }
            if (_onPick == null)
            {
                _onPick = _director.Pressed;
                _onClosed = RoomClosed;
            }
            float began = Time.realtimeSinceStartup;
            bool opening = !list.IsOpen;
            string subtitle = TravelText.T("reallife_travel_room_subtitle", "room", TravelText.T(room.NameKey), "nights", TravelText.Number(NightsLeft()),
                "day", TravelEffects.DayName(TravelState.CheckoutDay), "time", TravelText.Hour(CheckoutHour));
            string info = subtitle + "\n<size=80%>" + (_confirming ? CheckoutTerms(room) : TravelText.T(BodyKey(room))) + "</size>";
            list.Begin(TravelText.T(hotel.NameKey), info);
            int entries;
            if (_confirming)
            {
                list.Add(Back, TravelText.T("reallife_travel_btn_back"), TravelText.T("reallife_travel_list_stay"), ButtonChoose, true);
                list.Add(ConfirmOut, TravelText.T("reallife_travel_btn_checkout_confirm"), TravelText.T("reallife_travel_list_checkout_detail"), ButtonDo, true);
                entries = 2;
            }
            else
            {
                entries = Choices(list, hotel, room);
            }
            list.Open(_onPick, _onClosed);
            if (opening && Meta.Dev.Verbose)
            {
                _director.Log("Travel check (room list): the game's list is open: " + entries + " entries for the " + room.Id + " at " + hotel.Id + ", "
                              + list.Made + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        // The main page. Each entry says what it costs or does; one that cannot be used now is off, and its line says why.
        private int Choices(TravelList list, HotelSlot hotel, RoomType room)
        {
            GameInstance save = SaveGameManager.Current;
            int today = TimeHelper.CurrentDay;
            bool budget = hotel.Tier == HotelTier.Budget;
            bool luxury = hotel.Tier == HotelTier.Luxury;
            int count = 0;
            void Add(int id, string name, string detail, string button, bool enabled)
            {
                list.Add(id, name, detail, button, enabled);
                count++;
            }

            if (!_director.Visit.Ready)
            {
                string wake = TravelText.Hour(TravelState.WakeHour);
                Add(Sleep, TravelText.T("reallife_travel_btn_sleep", "time", wake), TravelText.T("reallife_travel_list_sleep"), ButtonDo, true);
                Add(Nap, TravelText.T("reallife_travel_btn_nap"), TravelText.T("reallife_travel_list_nap"), ButtonDo, true);
                Add(Wake, TravelText.T("reallife_travel_btn_wake", "time", wake), TravelText.T("reallife_travel_list_wake"), ButtonChange, true);
                Add(Freshen, TravelText.T(room.Quality == 0 ? "reallife_travel_btn_freshen_shared" : "reallife_travel_btn_freshen"),
                    TravelText.T("reallife_travel_list_freshen"), ButtonDo, true);
            }
            bool hungry = save.Hunger <= 92f;
            Add(RoomService, TravelText.T(budget ? "reallife_travel_btn_vending" : "reallife_travel_btn_room_service", "amount", TravelText.Money(TravelCatalogue.MealPrice(hotel.Tier))),
                TravelText.T(hungry ? "reallife_travel_list_meal" : "reallife_travel_list_not_hungry"), ButtonDo, hungry);
            if (!budget)
            {
                bool left = TravelState.MinibarDay != today || TravelState.MinibarCount < MinibarPerDay;
                Add(Minibar, TravelText.T("reallife_travel_btn_minibar", "amount", TravelText.Money(TravelCatalogue.MinibarPrice(hotel.Tier))),
                    TravelText.T(left ? "reallife_travel_list_minibar" : "reallife_travel_list_minibar_empty"), ButtonDo, left);
            }
            bool washed = TravelState.LaundryDay == today;
            string laundry = washed ? "reallife_travel_list_laundry_done" : luxury ? "reallife_travel_list_laundry_free" : "reallife_travel_list_laundry";
            Add(Laundry, TravelText.T(budget ? "reallife_travel_btn_laundry_coin" : "reallife_travel_btn_laundry"),
                TravelText.T(laundry, "amount", TravelText.Money(TravelCatalogue.LaundryPrice(hotel.Tier))), ButtonDo, !washed);
            if (!_director.Visit.Ready)
            {
                Add(Rest, TravelText.T("reallife_travel_btn_rest"), TravelText.T("reallife_travel_list_rest"), ButtonDo, true);
            }
            Add(Messages, TravelText.T("reallife_travel_btn_messages"), TravelText.T("reallife_travel_list_messages"), ButtonDo, true);
            bool dined = TravelState.DinnerDay == today;
            if (luxury)
            {
                Add(Table, TravelText.T("reallife_travel_btn_table"),
                    TravelText.T(dined ? "reallife_travel_list_dined" : "reallife_travel_list_table", "amount", TravelText.Money(TravelCatalogue.TablePrice)), ButtonDo, !dined);
                if (TravelBridges.HasTransport)
                {
                    Add(Car, TravelText.T("reallife_travel_btn_car"), TravelText.T("reallife_travel_list_car"), ButtonDo, true);
                }
            }
            if (hotel.Tier == HotelTier.Cottage)
            {
                bool walked = TravelState.WalkDay == today;
                Add(Walk, TravelText.T("reallife_travel_btn_walk"), TravelText.T(walked ? "reallife_travel_list_walked" : "reallife_travel_list_walk"), ButtonDo, !walked);
                Add(Dinner, TravelText.T("reallife_travel_btn_dinner"),
                    TravelText.T(dined ? "reallife_travel_list_dined" : "reallife_travel_list_dinner", "amount", TravelText.Money(TravelCatalogue.DinnerPrice)), ButtonDo, !dined);
                if (TravelBridges.HasLeisure)
                {
                    Add(Golf, TravelText.T("reallife_travel_btn_golf"), TravelText.T("reallife_travel_list_golf"), ButtonDo, true);
                }
            }
            bool extend = !TravelState.Package;
            string extension = extend
                ? TravelText.T("reallife_travel_list_extend", "amount", TravelText.Money(ExtensionRate(hotel, room)))
                : TravelText.T("reallife_travel_list_extend_package");
            Add(Extend, TravelText.T("reallife_travel_btn_extend"), extension, ButtonDo, extend);
            Add(Late, TravelText.T("reallife_travel_btn_late", "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour)),
                TravelText.T(TravelState.LateCheckout ? "reallife_travel_list_late_done" : "reallife_travel_list_late", "amount", TravelText.Money(LateFee(hotel.Tier))),
                ButtonDo, !TravelState.LateCheckout);
            Add(CheckOut, TravelText.T("reallife_travel_btn_checkout"), TravelText.T("reallife_travel_list_checkout"), ButtonChoose, true);
            Add(GoOut, TravelText.T("reallife_travel_btn_go_out"), TravelText.T("reallife_travel_list_go_out"), ButtonDo, true);
            return count;
        }

        // The player closed the list (Cancel, or a menu opened over it): the room is kept, the player outside.
        private void RoomClosed()
        {
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (room list): closed" + (TravelState.InRoom ? " by the player." : "."));
            }
            if (TravelState.InRoom && !_sleeping)
            {
                LeaveRoom();
            }
        }

        // No window to show: the night is slept, then the player is outside again, so that a stay can never trap anyone.
        private void Plain(RoomType room)
        {
            if (_sleeping)
            {
                return;
            }
            if (!_plain)
            {
                _plain = true;
                _director.Log("Travel check (room list): the game's list could not be made; the night is slept at once and the other choices are not offered: FAIL.");
                _director.Run(SleepRoutine(room, SleepGoal(), true));
                return;
            }
            LeaveRoom();
        }

        private static string BodyKey(RoomType room)
        {
            switch (room.Id)
            {
                case "dorm": return "reallife_travel_room_body_dorm";
                case "single": return "reallife_travel_room_body_single";
                case "standard": return "reallife_travel_room_body_standard";
                case "deluxe": return "reallife_travel_room_body_deluxe";
                case "king": return "reallife_travel_room_body_king";
                case "suite": return "reallife_travel_room_body_suite";
                case "cottage_whole": return "reallife_travel_room_body_cottage_whole";
                default: return "reallife_travel_room_body_cottage_room";
            }
        }

        private static void Say(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                TravelText.Toast(NotificationType.Info, text, 8f);
            }
        }
    }
}
