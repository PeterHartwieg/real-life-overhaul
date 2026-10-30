using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What travel keeps in a save, as one line of modData that the base game carries unread. Game-free.</summary>
    public static class TravelState
    {
        public const string SaveKey = "reallife:travel";
        // Left off the saved event keys; written in two parts so the locale check does not take it for a key.
        private const string EventPrefix = "reallife" + "_travel_ev_";
        private const int MostPastTrips = 12;

        // The room.
        public static string HotelId;          // null: no room
        public static string RoomId;
        public static int CheckInDay;
        public static int Nights;
        public static int Paid;                // dollars handed over so far
        public static int Owed;                // still to pay at checkout (a stay on deposit)
        public static int NightRate;           // the average night of this booking, for refunds and extras
        public static bool LateCheckout;
        public static int NightsSlept;
        public static int NightCredited = int.MinValue; // the last stay-day counted, including day zero; never a pending sleep

        /// <summary>Changes when a booking is cleared, replaced or loaded; a coroutine from the old booking cannot settle the new one.</summary>
        public static ulong BookingRevision { get; private set; }
        public static bool InRoom;             // up in the room, its list open; a save made there reopens it
        public static bool Package;            // the room came with the Hamptons weekend

        // Habits and limits.
        public static int WakeHour = 7;
        public static int Stamps;
        public static int FreeNights;
        public static int NightsEver;
        public static int TiredDay;            // the day the "too tired" notice last showed
        public static int HintDay;             // the day the "nowhere to sleep" notice last showed
        public static int MinibarDay;
        public static int MinibarCount;
        public static int FreshDay;
        public static int LaundryDay;
        public static int RestDay;
        public static int MorningDay;          // the day a morning in the room last brought a treat
        public static int DinnerDay;
        public static int WalkDay;

        // The ticket: a trip that is paid for and not begun.
        public static string TicketId;
        public static bool TicketFirst;
        public static bool TicketInsured;
        public static bool TicketCoach;
        public static string CompanionId;
        public static string CompanionName;
        public static int TicketPaid;
        public static int TicketDay;

        // The trip under way.
        public static bool Away;
        public static int AwayStep;            // 0 departure; two cards a day (the choice, its outcome); then the return
        public static int AwayStartDay;
        public static int AwaySpent;
        public static int AwayRelaxed;
        public static int AwaySouvenirs;
        public static string AwayEvent;        // the day's event being shown, so a save made then shows the same
        public static int AwayChoice;
        public static int AwayMoneyAtStart;

        // The rest.
        public static int Souvenirs;           // brought home and not given away yet
        public static bool Suitcase;
        public static bool AgencyVisited;
        public static int Streak;              // days without a break
        public static int StreakDay;           // the last day counted
        public static bool Warned;
        public static bool BurntOut;
        public static int FairDay;             // the day a trade fair was last offered
        public static bool FairOpen;           // the invitation stands
        public static int TripsTaken;

        /// <summary>Past trips, newest last: "id:day it began:days".</summary>
        public static readonly List<string> Past = new List<string>(MostPastTrips);
        public static readonly HashSet<string> Seen = new HashSet<string>();
        public static string LastEvent;        // not saved: the event dealt last, never dealt twice running

        public static bool HasRoom => HotelId != null;
        public static bool HasTicket => TicketId != null;

        public static void Reset()
        {
            ClearRoom();
            ClearTicket();
            ClearAway();
            WakeHour = 7;
            Stamps = FreeNights = NightsEver = TiredDay = HintDay = MinibarDay = MinibarCount = 0;
            FreshDay = LaundryDay = RestDay = MorningDay = DinnerDay = WalkDay = 0;
            Souvenirs = 0;
            Suitcase = AgencyVisited = Warned = BurntOut = FairOpen = false;
            Streak = StreakDay = FairDay = TripsTaken = 0;
            Past.Clear();
            Seen.Clear();
            LastEvent = null;
        }

        public static void ClearRoom()
        {
            unchecked { BookingRevision++; }
            HotelId = RoomId = null;
            CheckInDay = Nights = Paid = Owed = NightRate = NightsSlept = 0;
            NightCredited = int.MinValue;
            LateCheckout = InRoom = Package = false;
        }

        public static void ClearTicket()
        {
            TicketId = CompanionId = CompanionName = null;
            TicketFirst = TicketInsured = TicketCoach = false;
            TicketPaid = TicketDay = 0;
        }

        public static void ClearAway()
        {
            Away = false;
            AwayStep = AwayStartDay = AwaySpent = AwayRelaxed = AwaySouvenirs = AwayChoice = AwayMoneyAtStart = 0;
            AwayEvent = null;
        }

        public static int CheckoutDay => CheckInDay + Nights;

        public static void AddPast(string tripId, int day, int days)
        {
            Past.Add(tripId + ":" + day.ToString(CultureInfo.InvariantCulture) + ":" + days.ToString(CultureInfo.InvariantCulture));
            while (Past.Count > MostPastTrips)
            {
                Past.RemoveAt(0);
            }
            TripsTaken++;
        }

        public static string Write()
        {
            var text = new StringBuilder(512);
            Put(text, "v", "1");
            Put(text, "hotel", HotelId);
            Put(text, "room", RoomId);
            Put(text, "in", CheckInDay);
            Put(text, "nights", Nights);
            Put(text, "paid", Paid);
            Put(text, "owed", Owed);
            Put(text, "rate", NightRate);
            Put(text, "late", LateCheckout);
            Put(text, "slept", NightsSlept);
            Put(text, "inroom", InRoom);
            Put(text, "package", Package);
            Put(text, "wake", WakeHour);
            Put(text, "stamps", Stamps);
            Put(text, "free", FreeNights);
            Put(text, "ever", NightsEver);
            Put(text, "tired", TiredDay);
            Put(text, "hint", HintDay);
            Put(text, "barday", MinibarDay);
            Put(text, "bar", MinibarCount);
            Put(text, "fresh", FreshDay);
            Put(text, "laundry", LaundryDay);
            Put(text, "rest", RestDay);
            Put(text, "morning", MorningDay);
            Put(text, "dinner", DinnerDay);
            Put(text, "walk", WalkDay);
            Put(text, "ticket", TicketId);
            Put(text, "tfirst", TicketFirst);
            Put(text, "tins", TicketInsured);
            Put(text, "tcoach", TicketCoach);
            Put(text, "comp", CompanionId);
            Put(text, "compname", Clean(CompanionName));
            Put(text, "tpaid", TicketPaid);
            Put(text, "tday", TicketDay);
            Put(text, "away", Away);
            Put(text, "astep", AwayStep);
            Put(text, "astart", AwayStartDay);
            Put(text, "aspent", AwaySpent);
            Put(text, "arelax", AwayRelaxed);
            Put(text, "agifts", AwaySouvenirs);
            Put(text, "aevent", AwayEvent);
            Put(text, "achoice", AwayChoice);
            Put(text, "amoney", AwayMoneyAtStart);
            Put(text, "gifts", Souvenirs);
            Put(text, "case", Suitcase);
            Put(text, "agency", AgencyVisited);
            Put(text, "streak", Streak);
            Put(text, "streakday", StreakDay);
            Put(text, "warned", Warned);
            Put(text, "burnt", BurntOut);
            Put(text, "fair", FairDay);
            Put(text, "fairopen", FairOpen);
            Put(text, "taken", TripsTaken);
            Put(text, "past", string.Join(",", Past));
            var seen = new List<string>(Seen.Count);
            foreach (string key in Seen)
            {
                seen.Add(key.StartsWith(EventPrefix, StringComparison.Ordinal) ? key.Substring(EventPrefix.Length) : key);
            }
            Put(text, "seen", string.Join(",", seen));
            if (NightCredited != int.MinValue)
            {
                Put(text, "ncred", NightCredited.ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>Reads what <see cref="Write"/> wrote. Anything missing or unreadable keeps its fresh value; never throws.</summary>
        public static void Read(string saved)
        {
            Reset();
            if (string.IsNullOrEmpty(saved))
            {
                return;
            }
            var data = new Dictionary<string, string>();
            foreach (string pair in saved.Split(';'))
            {
                int equals = pair.IndexOf('=');
                if (equals > 0)
                {
                    data[pair.Substring(0, equals)] = pair.Substring(equals + 1);
                }
            }
            HotelId = Text(data, "hotel");
            RoomId = Text(data, "room");
            CheckInDay = Int(data, "in");
            Nights = Int(data, "nights");
            Paid = Int(data, "paid");
            Owed = Int(data, "owed");
            NightRate = Int(data, "rate");
            LateCheckout = Bool(data, "late");
            NightsSlept = Int(data, "slept");
            InRoom = Bool(data, "inroom");
            Package = Bool(data, "package");
            NightCredited = Int(data, "ncred", int.MinValue);
            WakeHour = Math.Min(Math.Max(Int(data, "wake", 7), 4), 12);
            Stamps = Int(data, "stamps");
            FreeNights = Int(data, "free");
            NightsEver = Int(data, "ever");
            TiredDay = Int(data, "tired");
            HintDay = Int(data, "hint");
            MinibarDay = Int(data, "barday");
            MinibarCount = Int(data, "bar");
            FreshDay = Int(data, "fresh");
            LaundryDay = Int(data, "laundry");
            RestDay = Int(data, "rest");
            MorningDay = Int(data, "morning");
            DinnerDay = Int(data, "dinner");
            WalkDay = Int(data, "walk");
            TicketId = Text(data, "ticket");
            TicketFirst = Bool(data, "tfirst");
            TicketInsured = Bool(data, "tins");
            TicketCoach = Bool(data, "tcoach");
            CompanionId = Text(data, "comp");
            CompanionName = Text(data, "compname");
            TicketPaid = Int(data, "tpaid");
            TicketDay = Int(data, "tday");
            Away = Bool(data, "away");
            AwayStep = Int(data, "astep");
            AwayStartDay = Int(data, "astart");
            AwaySpent = Int(data, "aspent");
            AwayRelaxed = Int(data, "arelax");
            AwaySouvenirs = Int(data, "agifts");
            AwayEvent = Text(data, "aevent");
            AwayChoice = Int(data, "achoice");
            AwayMoneyAtStart = Int(data, "amoney");
            Souvenirs = Int(data, "gifts");
            Suitcase = Bool(data, "case");
            AgencyVisited = Bool(data, "agency");
            Streak = Int(data, "streak");
            StreakDay = Int(data, "streakday");
            Warned = Bool(data, "warned");
            BurntOut = Bool(data, "burnt");
            FairDay = Int(data, "fair");
            FairOpen = Bool(data, "fairopen");
            TripsTaken = Int(data, "taken");
            string past = Text(data, "past");
            if (past != null)
            {
                foreach (string trip in past.Split(','))
                {
                    if (trip.Length > 0 && Past.Count < MostPastTrips)
                    {
                        Past.Add(trip);
                    }
                }
            }
            string seen = Text(data, "seen");
            if (seen != null)
            {
                foreach (string key in seen.Split(','))
                {
                    if (key.Length > 0)
                    {
                        Seen.Add(EventPrefix + key);
                    }
                }
            }

            // A booking or ticket naming something the catalogue does not know is dropped whole.
            if (HotelId != null && (TravelCatalogue.Hotel(HotelId) == null || TravelCatalogue.Room(RoomId) == null || Nights <= 0))
            {
                ClearRoom();
            }
            if (TicketId != null && TravelCatalogue.TripById(TicketId) == null)
            {
                ClearTicket();
                ClearAway();
            }
            if (Away && TicketId == null)
            {
                ClearAway();
            }
        }

        private static void Put(StringBuilder text, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                text.Append(key).Append('=').Append(value).Append(';');
            }
        }

        private static void Put(StringBuilder text, string key, int value)
        {
            if (value != 0)
            {
                text.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
        }

        private static void Put(StringBuilder text, string key, bool value)
        {
            if (value)
            {
                text.Append(key).Append("=1;");
            }
        }

        // A name from another feature: the line's two separators are taken out of it.
        private static string Clean(string value) => value?.Replace(';', ' ').Replace('=', ' ');

        private static string Text(Dictionary<string, string> data, string key)
        {
            return data.TryGetValue(key, out string value) && value.Length > 0 ? value : null;
        }

        private static int Int(Dictionary<string, string> data, string key, int fallback = 0)
        {
            return data.TryGetValue(key, out string value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                ? number
                : fallback;
        }

        private static bool Bool(Dictionary<string, string> data, string key) => data.TryGetValue(key, out string value) && value == "1";

        /// <summary>Deals an unseen event, never the last one dealt; a pool seen through starts again. Null if empty.</summary>
        public static TravelEvent Deal(TravelEvent[] pool, bool companion, Func<float> random01)
        {
            TravelEvent picked = null;
            for (int round = 0; round < 2 && picked == null; round++)
            {
                int fresh = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (Fits(pool[i], companion) && !Seen.Contains(pool[i].Key) && pool[i].Key != LastEvent)
                    {
                        fresh++;
                    }
                }
                if (fresh == 0)
                {
                    // All seen: only this pool is shuffled back in.
                    for (int i = 0; i < pool.Length; i++)
                    {
                        Seen.Remove(pool[i].Key);
                    }
                    continue;
                }
                int index = Math.Min((int)(random01() * fresh), fresh - 1);
                for (int i = 0; i < pool.Length; i++)
                {
                    if (Fits(pool[i], companion) && !Seen.Contains(pool[i].Key) && pool[i].Key != LastEvent && index-- == 0)
                    {
                        picked = pool[i];
                        break;
                    }
                }
            }
            if (picked == null)
            {
                for (int i = 0; i < pool.Length && picked == null; i++)
                {
                    picked = Fits(pool[i], companion) ? pool[i] : null;   // a pool of one, dealt last time too
                }
            }
            if (picked != null)
            {
                Seen.Add(picked.Key);
                LastEvent = picked.Key;
            }
            return picked;
        }

        private static bool Fits(TravelEvent e, bool companion) => companion || !e.Companion;
    }
}
