using System;
using RealLifeOverhaul;

// Exercises the production store and completion path; no Unity, time skip or simulated bed effects.
internal static partial class Program
{
    private static void HotelBooking()
    {
        TravelState.ClearRoom();
        TravelState.HotelId = "kitchen_hotel";
        TravelState.RoomId = "standard";
        TravelState.CheckInDay = 1;
        TravelState.Nights = 3;
        TravelState.NightRate = 150;
        TravelState.Paid = 450;
    }

    private static void HotelNights()
    {
        Check(HotelVisitRules.Supported("standard", "ba:buildingsize_l", 1, false), "hotel access: proved Standard L1 room");
        foreach (string room in new[] { null, "", "deluxe", "dorm", "suite", "cottage_whole" })
            Check(!HotelVisitRules.Supported(room, "ba:buildingsize_l", 1, false), "hotel access: unsupported category keeps menu " + room);
        Check(!HotelVisitRules.Supported("standard", "ba:buildingsize_l", 1, true), "hotel access: multiplayer keeps menu");
        Check(!HotelVisitRules.Supported("standard", "ba:buildingsize_l", 2, false), "hotel access: changed shell version keeps menu");
        Check(!HotelVisitRules.Supported("standard", "ba:buildingsize_m", 1, false), "hotel access: unsupported shell size keeps menu");
        Check(HotelVisitRules.Empty(false, false, false, null, "ba:businesstype_empty", 0), "hotel access: empty unlisted host is eligible");
        for (int occupancy = 1; occupancy < 8; occupancy++)
            Check(!HotelVisitRules.Empty((occupancy & 1) != 0, (occupancy & 2) != 0, (occupancy & 4) != 0, null, "ba:businesstype_empty", 0),
                "hotel access: every combination of rent, ownership and business is refused " + occupancy);
        Check(!HotelVisitRules.Empty(false, false, false, "default", "ba:businesstype_empty", 0), "hotel access: existing layout refused");
        Check(!HotelVisitRules.Empty(false, false, false, null, "ba:businesstype_office", 0), "hotel access: foreign business type refused");
        foreach (int items in new[] { -1, 1, 20 })
            Check(!HotelVisitRules.Empty(false, false, false, null, "ba:businesstype_empty", items), "hotel access: missing or occupied item dictionary refused " + items);
        TravelState.Reset();
        Check(TravelState.NightCredited == int.MinValue && !TravelState.Write().Contains("ncred="), "hotel nights: no-credit sentinel omitted");
        HotelBooking();
        TravelState.NightsSlept = 2;
        TravelState.Read(TravelState.Write());
        Check(TravelState.HasRoom && TravelState.NightsSlept == 2 && TravelState.NightCredited == int.MinValue,
            "hotel nights: older saves keep their count without inventing a date");
        var dayZero = new HotelSleep(1440, 0);
        int prepared = 0, applied = 0;
        Action prepare = () => prepared++;
        Action apply = () =>
        {
            Check(TravelState.NightCredited == 0 && TravelState.NightsSlept == 3, "hotel nights: receipt is visible before effects");
            applied++;
        };
        foreach (double minutes in new[] { -1d, 0, 1, 239, 239.999, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Check(!HotelNightRules.Complete(dayZero, 1440 + minutes, prepare, apply), "hotel nights: reject invalid or short elapsed " + minutes);
        }
        Check(prepared == 0 && applied == 0, "hotel nights: rejected sleep does not compute or apply outcomes");
        Check(HotelNightRules.Complete(dayZero, 1680, prepare, apply), "hotel nights: exactly four hours credits day zero");
        Check(!HotelNightRules.Complete(dayZero, 1800, prepare, apply) && prepared == 1 && applied == 1,
            "hotel nights: repeated completion cannot replay an outcome");
        string saved = TravelState.Write();
        Check(saved.EndsWith("ncred=0;", StringComparison.Ordinal), "hotel nights: day zero appended, never omitted as zero");
        ulong revision = TravelState.BookingRevision;
        TravelState.Read(saved);
        Check(TravelState.NightCredited == 0 && TravelState.NightsSlept == 3 && TravelState.Paid == 450 && TravelState.Owed == 0,
            "hotel nights: real store round trip preserves day zero, count and accounting");
        Check(TravelState.BookingRevision != revision && !HotelNightRules.Matches(dayZero), "hotel nights: loading invalidates an unfinished activity");
        Check(!HotelNightRules.Complete(new HotelSleep(1800, 0), 2040, prepare, apply), "hotel nights: saved receipt blocks the same day on either room path");
        Check(HotelNightRules.Complete(new HotelSleep(1800, 1), 2040, null, null), "hotel nights: six o'clock's new stay-day can earn another night");

        HotelBooking();
        var daylight = new HotelSleep(1440 + 13 * 60, 1);
        Check(HotelNightRules.Complete(daylight, 1440 + 17 * 60, null, null), "hotel nights: qualifying daylight native sleep counts");
        Check(!HotelNightRules.Complete(new HotelSleep(1440 + 23 * 60, 1), 2880 + 3 * 60, null, null),
            "hotel nights: daylight and evening starts share their stay-day");
        Check(!HotelNightRules.Complete(new HotelSleep(1440, 0), 1680, null, null), "hotel nights: older days cannot replace a receipt");

        HotelBooking();
        var old = new HotelSleep(1440, 1);
        HotelBooking();
        Check(!HotelNightRules.Complete(old, 1680, prepare, apply) && TravelState.NightCredited == int.MinValue,
            "hotel nights: cancel and rebook the identical hotel, category and day invalidates old sleep");
        var changed = new HotelSleep(1440, 1);
        TravelState.RoomId = "deluxe";
        Check(!HotelNightRules.Matches(changed), "hotel nights: category mismatch rejected");
        TravelState.RoomId = "standard";
        TravelState.HotelId = "downtown_hotel";
        Check(!HotelNightRules.Matches(changed), "hotel nights: hotel mismatch rejected");
        TravelState.HotelId = "kitchen_hotel";
        TravelState.CheckInDay++;
        Check(!HotelNightRules.Matches(changed), "hotel nights: check-in mismatch rejected");

        HotelBooking();
        var fault = new HotelSleep(1440, 1);
        bool threw = false;
        try { HotelNightRules.Complete(fault, 1680, () => throw new InvalidOperationException("prepare"), () => applied++); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && TravelState.NightCredited == int.MinValue && TravelState.NightsSlept == 0,
            "hotel nights: a fault before receipt leaves no credited night");
        threw = false;
        int effects = 0;
        try { HotelNightRules.Complete(fault, 1680, null, () => { effects++; throw new InvalidOperationException("effect"); }); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && TravelState.NightCredited == 1 && TravelState.NightsSlept == 1, "hotel nights: a fault after receipt keeps the committed night");
        Check(!HotelNightRules.Complete(fault, 1680, null, () => effects++) && effects == 1, "hotel nights: failed effects are never retried");
        TravelState.Read(TravelState.Write());
        Check(!HotelNightRules.Complete(new HotelSleep(1440, 1), 1680, null, () => effects++) && effects == 1,
            "hotel nights: reload after an effect fault does not replay it");

        HotelBooking();
        var replaced = new HotelSleep(1440, 1);
        Check(!HotelNightRules.Complete(replaced, 1680, HotelBooking, () => effects++) && TravelState.NightsSlept == 0,
            "hotel nights: replacement during outcome preparation cannot credit a different stay");
        var pending = new HotelSleep(1440, 1);
        TravelState.Read(TravelState.Write());
        Check(!HotelNightRules.Complete(pending, 2040, null, null) && TravelState.NightsSlept == 0,
            "hotel nights: loading a five-hour unfinished sleep grants no pending outcome");
        Check(!TravelState.Write().Contains("revision") && !TravelState.Write().Contains("pending"), "hotel nights: no runtime identity or pending animation saved");
        Check(!HotelNightRules.Complete(default, 1680, null, null), "hotel nights: default activity cannot credit");
        Check(!HotelNightRules.Complete(new HotelSleep(double.NaN, 1), 1680, null, null), "hotel nights: invalid start cannot credit");
        Check(!HotelNightRules.Complete(new HotelSleep(1440, int.MinValue), 1680, null, null), "hotel nights: sentinel is never a real stay-day");
        TravelState.NightsSlept = int.MaxValue;
        Check(!HotelNightRules.Complete(new HotelSleep(1440, 1), 1680, null, null), "hotel nights: count cannot overflow");
        HotelBooking();
        TravelState.Read(TravelState.Write() + "ncred=bad;");
        Check(TravelState.NightCredited == int.MinValue, "hotel nights: malformed receipt keeps the old-save default");
        TravelState.Read(TravelState.Write() + "ncred=-2147483648;");
        Check(TravelState.NightCredited == int.MinValue && !TravelState.Write().Contains("ncred="), "hotel nights: explicit sentinel is read as none and omitted");
        TravelState.Read(TravelState.Write() + "hotel=unknown_hotel;ncred=3;");
        Check(!TravelState.HasRoom && TravelState.NightCredited == int.MinValue, "hotel nights: invalid booking clears its receipt too");
        HotelBooking();
        TravelState.NightCredited = 3;
        TravelState.Seen.Add("example");
        Check(TravelState.Write().EndsWith("seen=example;ncred=3;", StringComparison.Ordinal), "hotel nights: receipt follows the old record's last field");
        TravelState.Reset();
        Check(!HotelNightRules.Complete(new HotelSleep(1440, 1), 1680, null, null), "hotel nights: no booking cannot credit");
    }
}
