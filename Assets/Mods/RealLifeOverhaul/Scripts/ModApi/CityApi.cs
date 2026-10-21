using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.ModApi
{
    /// <summary>The fixed outcomes of contract 1. Only Ready supplies an answer.</summary>
    public enum CityApiStatus
    {
        Ready = 0,
        Off = 1,
        NotYet = 2,
        Refreshing = 3,
        Shared = 4,
        Failed = 5
    }

    /// <summary>
    /// Read-only city facts for other mods, on the game's main thread. No frame work, state changes or allocations
    /// beyond growing the caller's lists. A failed read clears every output and never escapes into the caller.
    /// </summary>
    public static class CityApi
    {
        public static int ContractVersion => 1;

        /// <summary>Clears and fills the current account's Flare people. The seed is zero unless Ready.</summary>
        public static CityApiStatus ReadCitizens(List<CityApiCitizen> into, out int citySeed)
        {
            citySeed = 0;
            try
            {
                into?.Clear();
                if (into == null)
                {
                    return CityApiStatus.Failed;
                }
                CityApiStatus status = Status();
                if (status == CityApiStatus.Ready)
                {
                    status = ModApiBridges.Citizens != null
                        ? ModApiBridges.Citizens(into, out citySeed) : CityApiStatus.NotYet;
                }
                if (status != CityApiStatus.Ready)
                {
                    into.Clear();
                    citySeed = 0;
                }
                return status;
            }
            catch (Exception)
            {
                into?.Clear();
                citySeed = 0;
                return CityApiStatus.Failed;
            }
        }

        /// <summary>Clears and fills the Flare indexes represented by the live social cast. Ready, Refreshing, Shared or Failed.</summary>
        public static CityApiStatus ReadReservedCitizens(List<int> indexes)
        {
            try
            {
                indexes?.Clear();
                if (indexes == null)
                {
                    return CityApiStatus.Failed;
                }
                CityApiStatus status = Session();
                if (status == CityApiStatus.Ready)
                {
                    status = ModApiBridges.ReservedCitizens != null
                        ? ModApiBridges.ReservedCitizens(indexes) : CityApiStatus.Ready;
                }
                if (status != CityApiStatus.Ready)
                {
                    indexes.Clear();
                }
                return status;
            }
            catch (Exception)
            {
                indexes?.Clear();
                return CityApiStatus.Failed;
            }
        }

        /// <summary>Clears and fills the held game pedestrians and character components, including shelved cast bodies.
        /// Ready, Shared or Failed.</summary>
        public static CityApiStatus ReadReservedBodies(List<object> pedestrians, List<object> characters)
        {
            try
            {
                pedestrians?.Clear();
                characters?.Clear();
                if (pedestrians == null || characters == null || ReferenceEquals(pedestrians, characters))
                {
                    return CityApiStatus.Failed;
                }
                CityApiStatus status = Session();
                if (status == CityApiStatus.Ready)
                {
                    status = ModApiBridges.ReservedBodies != null
                        ? ModApiBridges.ReservedBodies(pedestrians, characters) : CityApiStatus.Ready;
                }
                if (status != CityApiStatus.Ready)
                {
                    pedestrians.Clear();
                    characters.Clear();
                }
                return status;
            }
            catch (Exception)
            {
                pedestrians?.Clear();
                characters?.Clear();
                return CityApiStatus.Failed;
            }
        }

        /// <summary>Clears and fills parallel lists of waypoints, their original limits and their current maxSpeed.
        /// Ready, Shared or Failed.</summary>
        public static CityApiStatus ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        {
            try
            {
                lanes?.Clear();
                originalLimits?.Clear();
                currentLimits?.Clear();
                if (lanes == null || originalLimits == null || currentLimits == null
                    || ReferenceEquals(originalLimits, currentLimits))
                {
                    return CityApiStatus.Failed;
                }
                CityApiStatus status = Session();
                if (status == CityApiStatus.Ready)
                {
                    status = ModApiBridges.SlowedLanes != null
                        ? ModApiBridges.SlowedLanes(lanes, originalLimits, currentLimits) : CityApiStatus.Ready;
                }
                if (status != CityApiStatus.Ready)
                {
                    lanes.Clear();
                    originalLimits.Clear();
                    currentLimits.Clear();
                }
                return status;
            }
            catch (Exception)
            {
                lanes?.Clear();
                originalLimits?.Clear();
                currentLimits?.Clear();
                return CityApiStatus.Failed;
            }
        }

        private static CityApiStatus Status()
        {
            return ModApiBridges.Status != null ? ModApiBridges.Status() : CityApiStatus.NotYet;
        }

        // What RLO holds is known while a city loads too: only a shared session keeps it from other mods.
        private static CityApiStatus Session()
        {
            return ModApiBridges.Shared != null && ModApiBridges.Shared() ? CityApiStatus.Shared : CityApiStatus.Ready;
        }
    }
}
