using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.ModApi
{
    /// <summary>The live reads supplied by Wiring/ModApiLinks. Unwired: no city, no reservations and no slowed lanes.</summary>
    internal static class ModApiBridges
    {
        /// <summary>The city and session's readiness for the citizens' read. Default: NotYet.</summary>
        internal static Func<CityApiStatus> Status;

        /// <summary>True in a Going Public session. Default: not shared.</summary>
        internal static Func<bool> Shared;

        /// <summary>The prepared identities, checked against Flare's live people. Default: NotYet.</summary>
        internal static CitizensRead Citizens;

        /// <summary>The social cast's live Flare identities. Default: Ready, empty.</summary>
        internal static Func<List<int>, CityApiStatus> ReservedCitizens;

        /// <summary>The social cast and casualty systems' bodies. Default: Ready, empty.</summary>
        internal static Func<List<object>, List<object>, CityApiStatus> ReservedBodies;

        /// <summary>The lanes held by the city's faults. Default: Ready, empty.</summary>
        internal static Func<List<object>, List<int>, List<int>, CityApiStatus> SlowedLanes;

        /// <summary>A population read with its seed, supplied once per city load.</summary>
        internal delegate CityApiStatus CitizensRead(List<CityApiCitizen> into, out int citySeed);
    }
}
