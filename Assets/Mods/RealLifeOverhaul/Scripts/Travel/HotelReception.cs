using System;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A hotel's reception in a copy of the game's subway list; <see cref="HotelDeskDialog"/> is the fallback.</summary>
    public sealed class HotelReception
    {
        private const int RowBack = -1, RowNights = -2, RowPayNow = -3, RowDeposit = -4, RowBooks = -5;   // rooms are 0 and up
        private const string ButtonChoose = "reallife_travel_list_choose";
        private const string ButtonChange = "reallife_travel_list_change";
        private const string ButtonBook = "reallife_travel_list_book";

        /// <summary>Whether the player owns this hotel as a business (Economy/Estate): the desk offers its books, the room is free.</summary>
        public static Func<string, bool> Owner;

        /// <summary>Opens the owner's books of a hotel.</summary>
        public static Action<string> Books;

        private readonly HotelStay _stay;
        private readonly TravelDirector _director;
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly StringBuilder _text = new StringBuilder(260);
        private HotelSite _site;
        private RoomType _room;       // null on page one
        private int _nights;          // index into the night choices
        private string _closing;
        private string _greeting;

        public HotelReception(HotelStay stay, TravelDirector director)
        {
            _stay = stay;
            _director = director;
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool IsOpen => _director.Window.OpenFor(this);

        public static bool Owned(string hotelId) => Owner != null && Owner(hotelId);

        /// <summary>Opens a hotel's desk. False if the game's window could not be made, and the caller uses the dialogue.</summary>
        public bool Open(HotelSite site)
        {
            TravelList list = _director.Window.Take(this);
            if (list == null)
            {
                return false;
            }
            try
            {
                float began = Time.realtimeSinceStartup;
                _site = site;
                _room = null;
                _nights = 0;
                _closing = null;
                _greeting = TravelText.T(TravelText.Pick(site.Slot.Tier == HotelTier.Budget ? TravelCatalogue.HostelGreetings : TravelCatalogue.DeskGreetings), "hotel", site.Name);
                int rooms = FillRooms(list);
                list.Open(_onPick, _onClosed);
                if (Meta.Dev.Verbose)
                {
                    TravelDirector.Instance.Log("Travel check (reception list): the game's list is open: " + rooms + " rooms at " + site.Slot.Id + ", "
                                                + list.Made + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _director.Fault(e);
                _director.Window.Close();
                return false;
            }
        }

        // Page one: the rooms. Returns how many.
        private int FillRooms(TravelList list)
        {
            _text.Length = 0;
            _text.Append(_greeting);
            if (TravelCatalogue.WeekendNight(TravelEffects.Weekday(TravelEffects.StayDay())))
            {
                _text.Append(' ').Append(TravelText.T("reallife_travel_desk_weekend"));
            }
            if (TravelState.FreeNights > 0)
            {
                _text.Append(' ').Append(TravelText.T("reallife_travel_desk_voucher"));
            }
            list.Begin(_site.Name, _text.ToString());
            RoomType[] rooms = TravelCatalogue.Rooms(_site.Slot.Tier);
            for (int i = 0; i < rooms.Length; i++)
            {
                RoomType room = rooms[i];
                int free = HotelStay.FreeTonight(_site.Slot, room);
                string detail = free > 0
                    ? TravelText.T("reallife_travel_desk_room", "room", TravelText.T(room.NameKey), "rate", TravelText.Money(HotelStay.Tonight(_site.Slot, room)),
                        "week", TravelText.Money(HotelStay.Quote(_site.Slot, room, 7, out _)), "free", TravelText.Number(free))
                    : TravelText.T("reallife_travel_desk_room_full", "room", TravelText.T(room.NameKey));
                list.Add(i, TravelText.T(room.NameKey), detail, ButtonChoose, free > 0);
            }
            if (Owned(_site.Slot.Id))
            {
                list.Add(RowBooks, TravelText.T("reallife_estate_desk_books"), TravelText.T("reallife_estate_desk_books_detail"), ButtonChoose, true);
            }
            return rooms.Length;
        }

        // Page two: one room, how many nights, how to pay.
        private void FillBooking(TravelList list)
        {
            int nights = TravelCatalogue.NightChoices[_nights % TravelCatalogue.NightChoices.Length];
            int total = HotelStay.Quote(_site.Slot, _room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightMost) : 0;
            int later = Mathf.RoundToInt((total - perNight) * (1f + TravelCatalogue.DepositSurcharge));
            string howLong = TravelText.T(nights >= 7 ? "reallife_travel_desk_nights_long" : "reallife_travel_desk_nights",
                "nights", TravelText.Number(nights), "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                "day", TravelEffects.DayName(TravelEffects.StayDay() + nights), "night", TravelText.Money(perNight));
            string voucherLine = TravelState.FreeNights > 0 ? "\n<size=80%>" + TravelText.T("reallife_travel_desk_voucher") + "</size>" : string.Empty;
            list.Begin(_site.Name, TravelText.T(_room.NameKey) + voucherLine);
            list.Add(RowBack, TravelText.T("reallife_travel_list_back"), TravelText.T("reallife_travel_list_back_detail"), ButtonChoose, true);
            list.Add(RowNights, TravelText.T("reallife_travel_list_nights", "nights", TravelText.Number(nights)), howLong, ButtonChange, true, list.HeightFor(howLong.Length, true));
            list.Add(RowPayNow, TravelText.T("reallife_travel_desk_pay_now"),
                TravelText.T("reallife_travel_list_pay_now", "total", TravelText.Money(Mathf.Max(total - voucher, 0))), ButtonBook, true);
            if (nights == 1)
            {
                list.Add(RowDeposit, TravelText.T("reallife_travel_list_deposit"), TravelText.T("reallife_travel_list_deposit_one"), ButtonBook, false);
            }
            else
            {
                string pay = TravelText.T("reallife_travel_desk_pay", "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                    "deposit", TravelText.Money(Mathf.Max(perNight - voucher, 0)), "later", TravelText.Money(later));
                list.Add(RowDeposit, TravelText.T("reallife_travel_list_deposit"), pay, ButtonBook, true, list.HeightFor(pay.Length, true));
            }
        }

        private void Picked(int id)
        {
            try
            {
                TravelList list = _director.Window.Take(this);
                if (list == null || !list.IsOpen || _site == null)
                {
                    return;
                }
                RoomType[] rooms = TravelCatalogue.Rooms(_site.Slot.Tier);
                if (id >= 0 && id < rooms.Length)
                {
                    _room = rooms[id];
                    _nights = 0;
                    FillBooking(list);
                    list.Open(_onPick, _onClosed);
                    Step("room " + _room.Id);
                }
                else if (id == RowBack)
                {
                    _room = null;
                    int count = FillRooms(list);
                    list.Open(_onPick, _onClosed);
                    Step("back to the " + count + " rooms");
                }
                else if (id == RowNights && _room != null)
                {
                    _nights = (_nights + 1) % TravelCatalogue.NightChoices.Length;
                    FillBooking(list);
                    list.Open(_onPick, _onClosed);
                    Step("nights " + TravelCatalogue.NightChoices[_nights]);
                }
                else if ((id == RowPayNow || id == RowDeposit) && _room != null)
                {
                    Book(list, id == RowDeposit);
                }
                else if (id == RowBooks)
                {
                    string hotel = _site.Slot.Id;
                    Close("the owner opened the books");
                    Books?.Invoke(hotel);
                }
            }
            catch (Exception e)
            {
                _director.Fault(e);
                Close("an error");
            }
        }

        private void Book(TravelList list, bool deposit)
        {
            int nights = TravelCatalogue.NightChoices[_nights % TravelCatalogue.NightChoices.Length];
            RoomType room = _room;
            HotelSite site = _site;
            if (!_stay.Book(site, room, nights, deposit, out string failKey))
            {
                string why = failKey ?? "reallife_travel_desk_closed";
                TravelText.Toast(NotificationType.Warning, TravelText.T(why, "hotel", site.Name), 8f);
                Step("booking " + room.Id + " refused: " + why);
                if (why == "reallife_travel_desk_full")
                {
                    _room = null;
                    FillRooms(list);
                    list.Open(_onPick, _onClosed);
                }
                else if (why == "reallife_travel_desk_has_room")
                {
                    Close("a room is already booked");
                }
                return;
            }
            Step("booked " + room.Id + " for " + nights + " night(s), " + (deposit ? "on deposit" : "paid now"));
            TravelText.Toast(NotificationType.Success, TravelText.T("reallife_travel_desk_booked", "room", TravelText.T(room.NameKey),
                "day", TravelEffects.DayName(TravelState.CheckoutDay), "time", TravelText.Hour(TravelCatalogue.CheckoutHour)), 10f);
            Close("a room was booked");
            _stay.GoUpSoon(_site);
        }

        /// <summary>Closes the desk if it is open, saying why in the log.</summary>
        public void Close(string why)
        {
            if (IsOpen)
            {
                _closing = why;
                _director.Window.Close();
            }
        }

        private void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                TravelDirector.Instance?.Log("Travel check (reception list): closed (" + (_closing ?? "cancelled by the player, or another screen took the window") + ").");
            }
            _closing = null;
            _site = null;
            _room = null;
        }

        private static void Step(string what)
        {
            if (Meta.Dev.Verbose)
            {
                TravelDirector.Instance?.Log("Travel check (choice): reception: " + what + ".");
            }
        }
    }
}
