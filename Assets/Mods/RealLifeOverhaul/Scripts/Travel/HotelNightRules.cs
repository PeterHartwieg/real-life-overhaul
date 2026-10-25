using System;

namespace RealLifeOverhaul
{
    /// <summary>A sleep's booking and start, kept only in memory. A load or replacement booking invalidates it.</summary>
    public readonly struct HotelSleep
    {
        internal readonly ulong Revision;
        internal readonly string Hotel, Room;
        internal readonly int CheckIn, Day;
        internal readonly double Started;

        public HotelSleep(double started, int stayDay)
        {
            Revision = TravelState.BookingRevision;
            Hotel = TravelState.HotelId;
            Room = TravelState.RoomId;
            CheckIn = TravelState.CheckInDay;
            Day = stayDay;
            Started = started;
        }
    }

    /// <summary>
    /// One completed night per booking and stay-day, shared by simulated and physical sleep. Game-free; called only
    /// when a sleep ends. Records the receipt before external effects, so a failing notice cannot grant another night.
    /// </summary>
    public static class HotelNightRules
    {
        public const int MinimumMinutes = 240;

        public static bool Matches(HotelSleep sleep)
        {
            return TravelState.HasRoom && sleep.Revision == TravelState.BookingRevision
                   && sleep.Hotel == TravelState.HotelId && sleep.Room == TravelState.RoomId && sleep.CheckIn == TravelState.CheckInDay;
        }

        public static bool CanCredit(HotelSleep sleep, double ended)
        {
            return Matches(sleep)
                   && sleep.Day != int.MinValue && sleep.Day > TravelState.NightCredited
                   && TravelState.NightsSlept < int.MaxValue
                   && !double.IsNaN(sleep.Started) && !double.IsInfinity(sleep.Started) && sleep.Started >= 0
                   && !double.IsNaN(ended) && !double.IsInfinity(ended) && ended - sleep.Started >= MinimumMinutes;
        }

        /// <summary>
        /// Prepare an outcome without applying it, recheck the booking, record, then apply once. Preparation can fail
        /// without a receipt; an effect can fail after it without replay. Callers guard exceptions at their entry point.
        /// </summary>
        public static bool Complete(HotelSleep sleep, double ended, Action prepare, Action effects)
        {
            if (!CanCredit(sleep, ended))
            {
                return false;
            }
            prepare?.Invoke();
            if (!CanCredit(sleep, ended))
            {
                return false;
            }
            TravelState.NightCredited = sleep.Day;
            TravelState.NightsSlept++;
            effects?.Invoke();
            return true;
        }
    }
}
