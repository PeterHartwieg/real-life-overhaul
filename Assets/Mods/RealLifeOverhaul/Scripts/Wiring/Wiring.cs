using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the features meet. Every feature of the mod was built to work alone and talks to
    /// the others only through optional bridges with a default; this folder is the one place
    /// that fills those bridges in, one small file per pairing:
    ///
    ///     Standing        the one answer to "has the player a record, and what does the city think of them"
    ///     LawJustice      an arrest from the street to the court: who holds, who charges, who is told
    ///     LicenceLinks    driving licences with the law and the courts
    ///     CarSoundLinks   the recorded car sounds with the Garage's builds and its turntable
    ///     Scenes          the one question every street scene asks before it starts
    ///     CivicLinks      city hall with the record, the weather, the paper and the scenes; Paper, a story for the City Ledger
    ///     LifeLinks       careers, social life, travel, the garage and the weather with each other
    ///     DrivingLinks    the driven car's handling with the custom shop's parts on top of it
    ///     FamilyLinks     family life with social life, home life and the law
    ///     HardTimesLinks  family life's difficult moments with what home life, careers, the social life, travel and the
    ///                     economy know of the player's days
    ///     NavLinks        the minimap and the GPS with the scenes that take the screen, and the police stations
    ///     TaxiLinks       the taxi called from the phone with the holds, the weather, the GPS's roads and the minimap
    ///     EconomyLinks    the credit rating with the legal record and the fines; the Finance rows in the Planner
    ///     DebtLinks       lenders with the courts (a civil claim for a debt), the private lender with the holds and the
    ///                     injury of a mugging, a repossession with what the camera sees
    ///     CityLinks       the city's economy with the calendar and the weather, business pressure (footfall, rivals) and the paper
    ///     VenturesLinks   investors with the city's economy, a pitch with the founder's CV and what has hold of the player;
    ///                     the Ventures rows in the Planner
    ///     MarketLinks     the stock market with the city's economy, the council, the weather, the street, crime, fires and
    ///                     accidents, the player's own takings, the game's rivals and the old Ventures market
    ///     LedgerLinks     the City Ledger with the city's paper, the market's close, the economy, the street's calendar,
    ///                     the council, the other fixtures of the street and the phone's City Ledger contact; Newsstand
    ///                     with the internet at the player's homes, the Utilities app and the Planner
    ///     CompanyLinks    running a business with business pressure (footfall, closures, inspections, audits), fires, break-ins, the courts, the paper and the Planner
    ///     EstateLinks     property with the council's rent and permits policies, the event calendar, a hotel's reception and the Planner
    ///     OnlineLinks     the online shops with the company's campaigns, the calendar's seasons, the GPS's roads and the Planner
    ///     TechLinks       the professions and the tech sector with Standing (the employer's esteem), the game's rivals and the paper
    ///     CareersLinks    education and careers with the internet at home, the council, the licences, Standing, the paper and the Planner
    ///     ServicesLinks   selling a business with Ventures (investors bought out), the credit side (business loans paid off) and running a business; the Planner
    ///     BrandLinks      brands and franchises with business pressure (footfall), the city's economy (prices), fires, inspections, strikes, the courts and the Planner
    ///     RoommateLinks   the roommate with family life (a partner who moves in), social life (friends, introductions) and the Planner
    ///     FlareLinks      the social media app with the social life (friends who follow and are tagged), the law (its
    ///                     reputation, scrutiny), the council (the election), careers, photo mode, business pressure
    ///                     (footfall), the company (a sponsored post), brands (scandals) and the City Ledger
    ///     FlareLiveLinks  Flare's living network with the council's works (the City Office's crews), the police and the
    ///                     cleaners (Crime/CityWatch, the clean streets' mood), the weather, the news, the street's calendar,
    ///                     the staff, photo mode's selfie and the City Ledger (its stories)
    ///     StandingLinks   the player's standing (wealth, recognition, reputation) with Flare, the company, property,
    ///                     brands, business pressure, good deeds and the Finance app's Standing page
    ///     PermadeathLinks an ending the player chooses with the records a life is told from (social life, family
    ///                     life, the record, Flare, the council, the businesses) and the City Ledger's page it is printed on
    ///     DistrictLinks   the district view on the city map with what the features keep by district: the city's economy,
    ///                     Standing, power, water and the internet, the council, and the crimes the paper hears of
    ///     InteriorLinks   interiors and decor with business pressure (footfall, complaints, inspections), the city's
    ///                     prices, the cleaning service, brands, Standing, the staff, the tech sector, Flare and the paper
    ///     OwnBankLinks    the player's own bank with Standing (its name), the council (defaults), the courts (what comes
    ///                     back), the Stocks app (its fund) and the City Ledger (its news)
    ///     RivalLinks      the rival tycoons with the stock market (their holdings, a stake, a bid, a takeover), the company
    ///                     (an offer to the staff), business pressure (footfall, a smear, inspections), property (prices),
    ///                     the courts (a claim), Standing, the City Ledger and the Finance app's page
    ///
    /// <see cref="Connect"/> is called once per city load, after every director has been added;
    /// <see cref="Disconnect"/> on unload puts every bridge back to its default. Nothing here
    /// has an Update: an adapter is called when the feature that owns the bridge asks, and
    /// answers from what the other feature already keeps. An adapter never throws into the
    /// feature that called it: a fault is logged once (<see cref="Fault"/>) and the default
    /// answer is given.
    ///
    /// ADDING A PAIRING: a file here with a nested static class and an internal Connect() /
    /// Disconnect(), one line for each in the two lists below.
    /// </summary>
    public static partial class Wiring
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static IModLogger Logger;

        /// <summary>True between Connect and Disconnect.</summary>
        public static bool Connected { get; private set; }

        public static void Connect(IModLogger logger, GameObject root)
        {
            Logger = logger;
            SaveHook.Logger = logger;
            if (Connected)
            {
                Disconnect();
            }
            Connected = true;
            Reported.Clear();
            Step("law and justice", Arrests.Connect);
            Step("licences", LicenceLinks.Connect);
            Step("the recorded car sounds", CarSoundLinks.Connect);
            Step("civic life", CivicLinks.Connect);
            Step("everyday life", LifeLinks.Connect);
            Step("the driven car's handling and the custom shop's parts", DrivingLinks.Connect);
            Step("family life", FamilyLinks.Connect);
            Step("family life's difficult moments", HardTimesLinks.Connect);
            Step("the minimap and the GPS", NavLinks.Connect);
            Step("the taxi by phone", TaxiLinks.Connect);
            Step("the economy", EconomyLinks.Connect);
            Step("credit and debt", DebtLinks.Connect);
            Step("the city's economy", CityLinks.Connect);
            Step("investors", VenturesLinks.Connect);
            Step("the stock market", MarketLinks.Connect);
            Step("the player's company on the market", ListingLinks.Connect);
            Step("the City Ledger", LedgerLinks.Connect);
            Step("running a business", CompanyLinks.Connect);
            Step("property", EstateLinks.Connect);
            Step("online shops", OnlineLinks.Connect);
            Step("the tech sector", TechLinks.Connect);
            Step("education and careers", CareersLinks.Connect);
            Step("the cleaning service and selling a business", ServicesLinks.Connect);
            Step("brands and franchises", BrandLinks.Connect);
            Step("the roommate", RoommateLinks.Connect);
            Step("power and water", UtilityLinks.Connect);
            Step("the social media app", FlareLinks.Connect);
            Step("the social media app's living network", FlareLiveLinks.Connect);   // after its own: its part of the paper follows the others'
            Step("invitations in Flare messages", FlareInviteLinks.Connect);   // a yes books a plan of the social life
            Step("street talk", StreetLinks.Connect);   // after the invitations' and the faults' own: Flare's look for an invitation, footfall on top of the others' 
            Step("the camera and the gallery", PhotosLinks.Connect);   // after Flare's: Flare's selfie button it wraps is theirs already
            Step("interiors and decor", InteriorLinks.Connect);   // after Flare's: its part of footfall goes on top of the others'
            Step("the city's faults", FaultLinks.Connect);   // after Flare's and the interiors': its part of footfall goes on top of theirs
            Step("RLO Texts", TextsLinks.Connect);   // the model's words for Flare and the phone's texts
            Step("permadeath", PermadeathLinks.Connect);
            Step("the district view", DistrictLinks.Connect);
            Step("empty shops as offices", OfficeLinks.Connect);
            Step("the player's bank", OwnBankLinks.Connect);   // after running a business: its pieces of the paper follow the company's
            Step("the end game at scale", EndScaleLinks.Connect);
            Step("rival tycoons", RivalLinks.Connect);   // after the city's economy, the company, Flare and the interiors: what it wraps of theirs (footfall, the deal, the paper) is theirs already
            Step("the public city read API", ModApiLinks.Connect);
            Step("standing", StandingLinks.Connect);   // last: what it wraps of the others' is theirs already
            Logger?.Info("Wiring: the features are connected.");
        }

        public static void Disconnect()
        {
            if (!Connected)
            {
                return;
            }
            Connected = false;
            Step("standing", StandingLinks.Disconnect);   // first: what it wrapped is put back before the others take theirs off
            Step("the public city read API", ModApiLinks.Disconnect);
            Step("rival tycoons", RivalLinks.Disconnect);   // next: what it wrapped is put back before the others take theirs off
            Step("the player's bank", OwnBankLinks.Disconnect);   // before running a business: what it wrapped of the paper is put back first
            Step("the end game at scale", EndScaleLinks.Disconnect);
            Step("the district view", DistrictLinks.Disconnect);
            Step("empty shops as offices", OfficeLinks.Disconnect);
            Step("permadeath", PermadeathLinks.Disconnect);
            Step("RLO Texts", TextsLinks.Disconnect);
            Step("the city's faults", FaultLinks.Disconnect);   // before Flare's: what it wrapped of footfall is put back first
            Step("interiors and decor", InteriorLinks.Disconnect);   // before the social media app's: what it wrapped is put back first
            Step("the camera and the gallery", PhotosLinks.Disconnect);   // before Flare's: the selfie button it wrapped is put back first
            Step("street talk", StreetLinks.Disconnect);   // before the invitations': what it wrapped of footfall is put back first
            Step("invitations in Flare messages", FlareInviteLinks.Disconnect);
            Step("the social media app's living network", FlareLiveLinks.Disconnect);   // what it wrapped of the paper is put back first
            Step("the social media app", FlareLinks.Disconnect);   // ...and then the social media app's
            Step("the taxi by phone", TaxiLinks.Disconnect);
            Step("property", EstateLinks.Disconnect);
            Step("the cleaning service and selling a business", ServicesLinks.Disconnect);
            Step("brands and franchises", BrandLinks.Disconnect);
            Step("power and water", UtilityLinks.Disconnect);
            Step("the roommate", RoommateLinks.Disconnect);
            Step("running a business", CompanyLinks.Disconnect);
            Step("education and careers", CareersLinks.Disconnect);
            Step("the tech sector", TechLinks.Disconnect);
            Step("online shops", OnlineLinks.Disconnect);
            Step("the city's economy", CityLinks.Disconnect);
            Step("credit and debt", DebtLinks.Disconnect);
            Step("the economy", EconomyLinks.Disconnect);
            Step("the City Ledger", LedgerLinks.Disconnect);
            Step("the player's company on the market", ListingLinks.Disconnect);
            Step("the stock market", MarketLinks.Disconnect);
            Step("investors", VenturesLinks.Disconnect);
            Step("the minimap and the GPS", NavLinks.Disconnect);
            Step("family life's difficult moments", HardTimesLinks.Disconnect);
            Step("family life", FamilyLinks.Disconnect);
            Step("the driven car's handling and the custom shop's parts", DrivingLinks.Disconnect);
            Step("everyday life", LifeLinks.Disconnect);
            Step("civic life", CivicLinks.Disconnect);
            Step("licences", LicenceLinks.Disconnect);
            Step("the recorded car sounds", CarSoundLinks.Disconnect);
            Step("law and justice", Arrests.Disconnect);
        }

        // One pairing failing to connect must not cost the others theirs.
        private static void Step(string name, Action what)
        {
            try
            {
                what();
            }
            catch (Exception e)
            {
                Fault(name, e);
            }
        }

        /// <summary>A bridge threw: said once per place and session, never per call.</summary>
        internal static void Fault(string where, Exception e)
        {
            if (Reported.Add(where))
            {
                Logger?.Warn("Wiring: " + where + " failed; the default answer is used from here on. " + e);
            }
        }
    }
}
