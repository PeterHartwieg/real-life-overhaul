using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.ModApi
{
    /// <summary>The live identity and its avatar, passed by value so that a read needs no temporary objects.</summary>
    internal struct ModApiIdentity
    {
        internal int Index;
        internal string First, Last, District;
        internal int Age, Persona, AppearanceSeed, AppearanceAge;
        internal bool Female, AppearanceFemale;
    }

    /// <summary>Consistency and identity matching for the public contract. Game-free; no work until a caller asks.</summary>
    internal static class ModApiRules
    {
        internal static string Persona(int persona)
        {
            switch (persona)
            {
                case 0: return "Commuter";
                case 1: return "Foodie";
                case 2: return "Parent";
                case 3: return "Student";
                case 4: return "Retiree";
                case 5: return "Sporty";
                case 6: return "Grumpy";
                case 7: return "NightOwl";
                default: return null;
            }
        }

        /// <summary>Checks every live identity before handing out any prepared one. Allocates only in the caller's list.</summary>
        internal static CityApiStatus ReadCitizens(IReadOnlyList<CityApiCitizen> prepared, int preparedSeed,
            int citySeed, int count, Func<int, int, ModApiIdentity> read, List<CityApiCitizen> into)
        {
            into.Clear();
            CityApiStatus status = CheckCitizens(prepared, preparedSeed, citySeed, count, read);
            if (status != CityApiStatus.Ready)
            {
                return status;
            }
            for (int i = 0; i < count; i++)
            {
                into.Add(prepared[i]);
            }
            return CityApiStatus.Ready;
        }

        /// <summary>The same consistency gate for a reservation read, without a temporary citizen list.</summary>
        internal static CityApiStatus CheckCitizens(IReadOnlyList<CityApiCitizen> prepared, int preparedSeed,
            int citySeed, int count, Func<int, int, ModApiIdentity> read)
        {
            if (citySeed == 0 || count == 0)
            {
                return CityApiStatus.NotYet;
            }
            if (prepared == null || preparedSeed != citySeed || prepared.Count != count)
            {
                return CityApiStatus.Refreshing;
            }
            for (int i = 0; i < count; i++)
            {
                ModApiIdentity live = read(citySeed, i);
                CityApiCitizen kept = prepared[i];
                if (kept == null || live.Index != i || live.Age < 1 || live.Age > 130
                    || string.IsNullOrEmpty(live.First) || string.IsNullOrEmpty(live.Last)
                    || live.AppearanceSeed == 0 || live.Age != live.AppearanceAge || live.Female != live.AppearanceFemale
                    || kept.Index != live.Index || kept.First != live.First || kept.Last != live.Last
                    || kept.District != live.District || kept.Age != live.Age || kept.Female != live.Female
                    || kept.Persona == null || kept.Persona != Persona(live.Persona)
                    || kept.AppearanceSeed != live.AppearanceSeed)
                {
                    return CityApiStatus.Refreshing;
                }
            }
            return CityApiStatus.Ready;
        }

        /// <summary>Adds, once each, the indexes of the prepared citizens a social contact stands for.</summary>
        internal static void Reserve(IReadOnlyList<CityApiCitizen> prepared, string name, int appearanceSeed, List<int> indexes)
        {
            for (int i = 0; i < prepared.Count; i++)
            {
                CityApiCitizen citizen = prepared[i];
                if (Matches(citizen, name, appearanceSeed) && !indexes.Contains(citizen.Index))
                {
                    indexes.Add(citizen.Index);
                }
            }
        }

        /// <summary>A social contact keeps a Flare name and face even when their age is clamped for the street.</summary>
        internal static bool Matches(CityApiCitizen citizen, string name, int appearanceSeed)
        {
            if (citizen == null || name == null || appearanceSeed == 0 || citizen.AppearanceSeed != appearanceSeed)
            {
                return false;
            }
            return NameIs(citizen.First, citizen.Last, name);
        }

        /// <summary>True when a social contact's name is this Flare person's first and last name, without building the text.</summary>
        internal static bool NameIs(string first, string last, string name)
        {
            return name != null && !string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last)
                && name.Length == first.Length + 1 + last.Length && name[first.Length] == ' '
                && string.CompareOrdinal(name, 0, first, 0, first.Length) == 0
                && string.CompareOrdinal(name, first.Length + 1, last, 0, last.Length) == 0;
        }
    }
}
