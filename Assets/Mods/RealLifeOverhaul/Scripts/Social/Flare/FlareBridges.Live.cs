using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What the living network asks of the rest of the mod (Wiring/FlareLinks); without one it still runs on what Flare knows.</summary>
    public static partial class FlareBridges
    {
        /// <summary>The complete population after a seed change. Default: nobody needs a prepared read.</summary>
        internal static Action<int, IReadOnlyList<FlareCitizen>> CitizensChanged;

        /// <summary>The city as the day's posts read it. Default: what Flare knows itself.</summary>
        public static Action<FlareCityFacts> CityFacts;

        /// <summary>The City Office sends a crew to a council works site near the point or in the district: its id, days and address; false if none can go.</summary>
        public static WorksAsk Works;

        public delegate bool WorksAsk(FlareReport kind, string district, float x, float z, int seed, out int site, out int days, out string address);

        /// <summary>True once a site of the council's works has been handed over, or is gone. Default: never.</summary>
        public static Func<int, bool> WorksDone;

        /// <summary>A building site of the council's within so many metres of a point. Default: none.</summary>
        public static Func<float, float, float, bool> SiteNear;

        /// <summary>The inspector looks at the site nearest a point reported as unsafe. True if it was unsafe. Default: nothing.</summary>
        public static Func<float, float, bool> SiteReported;

        /// <summary>Where the player stands now: the police patrolling there, a cleaning round there; once an hour. Default: nothing.</summary>
        public static Action<bool, bool> Watched;

        /// <summary>The district the player stands in (the game's own id), or null. Default: none.</summary>
        public static Func<string> PlayerDistrict;

        /// <summary>The names of the staff of the player's businesses, a few of them, into the list given. Default: none.</summary>
        public static Action<List<string>> Staff;

        /// <summary>The park or sight the player stands at, from Leisure's landmarks found behind the loading screen; false at none. Default: none.</summary>
        public static SightAtAsk SightAt;

        public delegate bool SightAtAsk(float x, float z, out string id, out float sightX, out float sightZ);

        /// <summary>The street event that is on (never a fire): where its middle is and its label. False with none on. Default: none.</summary>
        public static StreetEventAsk StreetEventAt;

        public delegate bool StreetEventAsk(out float x, out float y, out float z, out string label);

        /// <summary>The save's own photos' folder, where the city's photos are kept beside them. Default: none, so no city photos.</summary>
        public static Func<string> PictureFolder;

        /// <summary>The save's id written beside its photos, so a deleted save's go with them; called on a worker thread. Default: nothing.</summary>
        public static Action MarkPictureFolder;

        /// <summary>The rival tycoons of the end game in play, each with their latest move announced. Default: none.</summary>
        public static Action<List<FlareTycoon>> Tycoons;

        /// <summary>The player's own name, as the game has it. Default: none (the app says "you").</summary>
        public static Func<string> PlayerName;

        /// <summary>The way to a place ("street~number") is set on the map, as the game's own "set destination" does. Default: nothing.</summary>
        public static Action<string> Destination;

        /// <summary>The way to the street event on now is set on the map. False if none is on. Default: none.</summary>
        public static Func<bool> EventDestination;

        /// <summary>The phone is put away and photo mode starts as a selfie; the picture saved comes back as a draft. Default: nothing.</summary>
        public static Action TakeSelfie;

        /// <summary>The phone's own camera opens (Photos/CameraMode), its first photo back through SharePhoto; false when the Camera is off. Default: none.</summary>
        public static Func<bool> OpenCamera;

        /// <summary>One of the player's businesses is hiring: somebody may write asking for work. Default: no.</summary>
        public static Func<bool> Hiring;

        /// <summary>
        /// The city's real faults (Scripts/Faults) judge a report before the City Office acts: FaultVerdict's number (0 true,
        /// 1 nothing, 2 nothing and a warning, 3 no crew for this player for now, 4 a clean business), or -1 to answer as
        /// without them; for a real fault, where it is. Default: not judged.
        /// </summary>
        public static FaultAsk FaultCheck;

        public delegate int FaultAsk(FlareReport kind, string district, float x, float z, string reporter, int caseId, string place, out float fx, out float fz,
            out int fault);

        /// <summary>A crew is sent on a case the faults judged true: the case, its fault (-1: another case has it in hand), works or not. Default: nothing.</summary>
        public static Action<int, int, bool> FaultActed;

        /// <summary>A case's works are handed over: its fault is mended. Default: nothing.</summary>
        public static Action<int> FaultDone;

        /// <summary>True while the city's faults are real: the city's people report only those. Default: no.</summary>
        public static Func<bool> FaultsReal;

        private static void ResetLive()
        {
            FaultCheck = null;
            FaultActed = null;
            FaultDone = null;
            FaultsReal = null;
            CityFacts = null;
            Works = null;
            WorksDone = null;
            SiteNear = null;
            SiteReported = null;
            Watched = null;
            PlayerDistrict = null;
            Staff = null;
            PictureFolder = null;
            MarkPictureFolder = null;
            SightAt = null;
            StreetEventAt = null;
            Tycoons = null;
            PlayerName = null;
            Destination = null;
            EventDestination = null;
            TakeSelfie = null;
            OpenCamera = null;
            Hiring = null;
        }
    }
}
