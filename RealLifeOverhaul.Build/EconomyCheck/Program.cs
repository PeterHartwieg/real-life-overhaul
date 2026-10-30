using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// Checks for the economy's game-free parts. A check that fails prints one line; the last line is
// ALL CHECKS PASSED or the count that failed.
internal static partial class Program
{
    private static int _failed, _run;
    private static int _day;
    private static float _income;
    private static LegalStanding _legal;

    private static void Check(bool ok, string what)
    {
        _run++;
        if (!ok)
        {
            _failed++;
            Console.WriteLine("FAIL  " + what);
        }
    }

    /// <summary>One account with settable figures, standing in for the game's loans.</summary>
    private sealed class Account : ICreditAccount
    {
        public float Owed, Cap, Due;
        public string Key => "loans";
        public string NameKey => "name";
        public float Balance => Owed;
        public float Limit => Cap;
        public float PaymentDue => Due;
    }

    private static Account Begin(int day, CreditFile file = null)
    {
        CreditLedger.Close();
        _day = day;
        _income = 0f;
        _legal = default(LegalStanding);
        CreditLedger.Clock = () => _day;
        CreditLedger.Income = () => _income;
        CreditLedger.Legal = () => _legal;
        var account = new Account { Cap = 100000f };
        if (file == null)
        {
            file = new CreditFile();
            CreditScoring.Open(file, new List<LoanView>(), day);
        }
        CreditLedger.Open(file);
        CreditLedger.Register(account);
        CreditLedger.Refresh();
        return account;
    }

    private static int Score()
    {
        CreditLedger.Touch();
        return CreditLedger.Score.Value;
    }

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--hotel-nights")
        {
            HotelNights();
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length >= 1 && args[0] == "--companion")
        {
            CompanionChecks(args.Length == 2 ? args[1] : null);
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--stocker")
        {
            StockerFixes();
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--client-hours")
        {
            ClientHours();
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--own-ai")
        {
            TextsService();   // (Your own AI's checks alone, while working on them: TextsServiceChecks.cs)
            TextsSpoken();    // (and a spoken line's wait for it: TextsSpokenChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--derivatives")
        {
            Derivatives();   // (the raw materials exchange, futures and options alone, while working on them: DerivChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--branch")
        {
            Branches();   // (the bank branches' checks alone, while working on them: BranchChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--bank-levels")
        {
            BankLevels();   // (the bank's levels and the owner's pay alone, while working on them: BankTierChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--managers")
        {
            Managers();   // (the managers' checks alone, the directory's among them, while working on them: ManagerChecks.cs, ManagerDirectoryChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--meals")
        {
            Meals();   // (meals and music alone, while working on them: MealsChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--texts-history")
        {
            CallBack();       // (a call back's and RLO Texts' shared history's checks alone, while working on them: CallBackChecks.cs, TextsHistoryChecks.cs)
            TextsHistory();
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--more-people")
        {
            MorePeople();   // (more people to meet alone, while working on it: MorePeopleChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--designs")
        {
            Designs();   // (the ready-made designs' checks alone, while working on them: DesignChecks.cs)
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--clean-fixes")
        {
            RagdollLifeFixes();   // (the clean-up's fixes alone, while working on them: RagdollFixChecks.cs and the rest named in Main)
            BusinessInteriorFixes();
            PhoneWorldFixes();
            DiagnosticsFixes();
            CalendarFixes();
            TextsStreetFixes();
            WiringFixes();
            CoreFixes();
            Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--probe-start")
        {
            return 0;   // (TextsStartChecks: can this checker be started as a program on this PC)
        }
        if (args.Length > 1 && args[0] == "-m")
        {
            return PretendLlama(args);   // (started by TextServer as a pretend llama-server: TextsStartChecks.cs)
        }
        if (args.Length == 2 && args[0] == "--texts-spec")
        {
            return WriteTextsSpec(args[1]);   // (RLO Texts' contract of data v6 and its fixtures, written from the mod: TextsSpecWriter.cs)
        }
        CompanionChecks(null);
        Catalogue();
        Start();
        CleanHistory();
        Misses();
        Applications();
        OwedAndLegal();
        Bounds();
        Gate();
        Terms();
        Fee();
        Folding();
        Tests();
        SaveAndLoad();
        Identity();
        DailyRun();
        Products();
        CreditIncomeLimits();
        Interest();
        Instalments();
        OverdraftCover();
        CardCycle();
        LadderSteps();
        PaymentPlan();
        Repossession();
        Consolidation();
        Shark();
        WaysOut();
        SaveVersionTwo();
        LoanHoldsOnTheGamesFormula();
        City();
        Ventures();
        Company();
        Rota();
        Managers();   // the managers of the player's places: their decisions inside the limits, mistakes, undo and the save (ManagerChecks.cs)
        Estate();
        Online();
        Services();
        Brands();
        Tables();
        Roommates();
        Banks();
        Calendar();
        Utilities();
        SpecialNames();
        StudyPrompts();
        OutageExcuses();   // what the home's internet down for an outage excuses: an online class, a day from home, a video interview (OutageExcuseChecks.cs)
        NextClasses();
        Helpers();
        CampusRulesChecks();
        ClassDoors();
        ClassPlay();    // classes as the game's own studying: the computer's buttons at home, the minutes counted once (ClassPlayChecks.cs)
        CampusHome();
        CityReserveChecks();   // the two largest offices kept for the player, the courthouse at the moving company, the moves told once (CityReserveChecks.cs)   // the university's permanent building, its reservation, the older game's rule and the fallback (CampusHomeChecks.cs)
        Internet();
        Broadband();
        Empire();
        Dealer();
        ShowroomFloor();
        Markets();
        MarketPace();   // the open and the close in pieces, the catch-up's budget, the worth taken apart once (MarketPaceChecks.cs)
        Invest();       // a large investor's tools: holdings by kind, worked orders, dividends put back, rollovers and ladders (InvestChecks.cs)
        Listing();
        Ledger();
        Newsstand();
        Flare();
        FlareLive();   // Flare as a living network: the feed, the City Office, the reactions, the filter, pictures, Going Public, the save, the texts (FlareLiveChecks.cs)
        FlareReview();   // the review round of Flare as a living network (FlareReviewChecks.cs)
        FlareClosedReviews();   // the city's reviews come of visits: none of a business the game has closed then, asked again at the hour, a review that went up kept (FlareClosedReviewChecks.cs)
        Faults();   // the city's real faults: weighting, limits, reports, escalation, the city's people, the save, Going Public, the source of chance, texts (FaultChecks.cs)
        Texts();    // RLO Texts: the card byte for byte with the training's, the map, the checker, the queue, the budget, speed, the hub, the save, Going Public, packs, the real runner (TextsChecks.cs)
        TextsService();   // Your own AI: the services, each request and answer, faults and rests, the runner, the hub's fallback, Going Public, Sign in with ChatGPT end to end, the web client, the sealed keys, no secret in a log or a save (TextsServiceChecks.cs)
        TextsSpoken();    // a spoken line with your own AI: the wait for it, the pack in the end of the wait, a guess waited for, the first words, the stream read to its last word, the log naming the source (TextsSpokenChecks.cs)
        TextsSix();   // RLO Texts' contract of data v6: the fixtures are the mod's cards now, built by the mod and rendered from the spec alone, its shape, seven languages, older packs untouched (TextsSixChecks.cs)
        TextsLanguagesSix();   // RLO Texts in every language of the mod: the seven, a pack's languages, the length and tokens by script, the checker in each, the hub, Going Public's shared text (TextsLanguageChecks.cs)
        TextsDecodingSix();    // RLO Texts' requests of data v6: the grammars read and tried, the tokens, the name kept out, the bodies, the hub's job (TextsDecodingChecks.cs)
        TextsShownSix();       // what RLO Texts showed: the model's words against the written lines and why, by pack and intent, the hourly line, the hub counting every way (TextsShownChecks.cs)
        TextsMemorySix();      // what people remember: the lines, a card's choice the same in any order, whose keys, the save, caps and pruning, the topics (TextsMemoryChecks.cs)
        TextsCitySix();        // the city's new talk: the staff, life events, who texts, gossip, reviews, every written line, the save, RLO Texts Large, the sources (TextsCityChecks.cs)
        TextsServiceSix();     // your own AI beside data v6: the card finished as a pack's in the game's language, the pack's own card after a miss; each of the seven languages asked, checked and fallen back in; Dutch and Turkish keep their written lines (TextsServiceSixChecks.cs, TextsServiceLanguageChecks.cs)
        FlareDmTexts();   // a message's answer worded by RLO Texts: its speaker by the account's key, asked again after the model restarts (FlareDmTextsChecks.cs)
        ComposerChecks();   // a call's composer: the two answers without a pack, typed words worth the warm answer a day as before, the model's words read by no rule, cleaned (ComposerChecks.cs)
        TypedReadingChecks();   // what an answer typed on a call says, in every language, and what it gets back: never a sorry to good news (TypedReadingChecks.cs)
        CallBack();     // a call back opens on what the person just texted about: every feature's texts, the day and the next, once, the save, the flow, the table, the texts (CallBackChecks.cs)
        TextsHistory(); // RLO Texts' shared history: memories and the last text on the card in the packs' own shape, the budget, the day after an outing, the player's news without a pack (TextsHistoryChecks.cs)
        TextsStart();   // RLO Texts' start ends by its limits for certain: each stage timed, a watchdog, against a pretend server that never answers (TextsStartChecks.cs)
        TextsHang();   // RLO Texts' server that stops answering: a probe tells a hang from a slow machine, a hung or stopped server started again three times a session at most (TextsHangChecks.cs)
        TextsSpeedFix();   // RLO Texts on a GPU at normal priority with two threads, on the processor below normal; the start's lines say what each reply took (TextsSpeedChecks.cs)
        TextsCacheFix();   // RLO Texts' server without Steam's shader cache variables, so the GPU driver keeps its shaders; the first reply's limit and the cache's state (TextsCacheChecks.cs)
        FlareNoLimit();  // posting on Flare without a limit: all made and shown, what moves bounded, Going Public queued and none dropped, the save, the texts (FlareNoLimitChecks.cs)
        FlareSawPost();   // a city person's "saw your post" only after a real post of the player's, within three days; a hello that claims none for the tests (FlareSawPostChecks.cs)
        FlareInvites();   // invitations in Flare messages: reading, the decision, timing, booked once, the answer's words and card, three languages, sources (FlareInviteChecks.cs)
        StreetTalk();   // talking to anybody in the street: who, whether they stop, the choices, numbers and invitations, word of mouth, the words read, the lines, timing, sources (StreetTalkChecks.cs)
        StreetOffers();   // who in the street is offered a talk: reach, angle, the nearest in front, the time in front without reset, the grace, the kinds of people, give-back, the check lines (StreetOfferChecks.cs)
        StreetTextsV5();   // RLO Texts data v5 in street talk: v5's cards for a data 5 pack, v4's exact cards for a data 4 one, the moment, the name only when told, thanks, the packs' licences (StreetTextsV5Checks.cs)
        StreetConversation();   // street talk that feels alive: the order by the moment and the person, no repeats, follow-ups, patience, warmth, the panel and Esc, the turn's log line, the card's three lines, written lines once a day (StreetChatChecks.cs)
        FlareComments();   // the comments under posts that are not the player's: never more than the count, in time, the same after a save and on a guest, the cards, the texts, the cost (FlareCommentChecks.cs)
        FlareCommentsAsked();   // every comment the count promises written and shown: each asked again after a miss, the open thread first, the budget not locked by what the queue held back, the times, the post's own words (FlareCommentAskChecks.cs)
        FlareCrowd();    // Flare's city of four hundred and its influencers with no fixed most, Mom's and Alex's one look (FlareCrowdChecks.cs)
        StreetLook();   // somebody met in the street keeps the look the game gave them, through a save; a city person met on Flare their face; a card's age the face's (StreetLookChecks.cs)
        FlareFaceLoad();   // Flare's faces drawn ahead: the likely ones behind the loading screen within its budget, once per look, the textures and the folder bounded, each face the person's own (FlareFaceLoadChecks.cs)
        FlareThemeSwitch();   // Flare's light and dark switch: the look by setting and hour, a press always the other look, its words, written and saved as the Settings app does, every open part painted again (FlareThemeChecks.cs)
        Shared();
        Fame();
        CrimeFixes();   // a hold-up empties the till, rewards at today's prices, a release in a session names no hours, no Crime.Pay dropped (CrimeFixChecks.cs)
        CityEventFixes();   // the spare hose by the engine, a guest's traffic left alone by the weather, arrivals past one who never came (CityEventFixChecks.cs)
        RagdollLifeFixes();   // a kneeling medic struck as one standing and kneeling again if not, the memorial's ids with commas, the building manager tested (RagdollFixChecks.cs)
        BusinessInteriorFixes();   // a smear's room tested with its own dip, an office's stations with no item list, a refresh that says why not, the wear test as play (BusinessFixChecks.cs)
        PhoneWorldFixes();   // the level of detail put back only if it was set, the doctor's refused payment ends the call with a word (PhoneFixChecks.cs)
        DiagnosticsFixes();   // the logs page on the graphics card's figures, no count of civic props nobody reads (DiagnosticsFixChecks.cs)
        CalendarFixes();   // a failed calendar source leaves nothing behind, every app made only once the phone can take it (CalendarFixChecks.cs)
        WiringFixes();   // every Wiring adapter behind a try with a Wiring fault, the pairing before asked inside one, the financial analyst named, lists let go after a fault (WiringFixChecks.cs)
        CoreFixes();   // the phone dump gone, the shown buildings compared as a set, the tab copies taken back, the pictures found behind the loading screen, the bar marker by share, the font not looked for again (CoreFixChecks.cs)
        TextsStreetFixes();   // a raise thanked though first seen on another text's look, never "X and X", web requests let go, the packs said found by a barrier, a Done that asks again safe in the hub's look (TextsFixChecks.cs)
        LogNumbers();   // no number in a log line written in the machine's culture: decimals and groupings invariant (LogNumberChecks.cs)
        JusticeSaveRoundTrip();   // Justice's save line: names with %XXXX escapes come back as written, one at the very end too (JusticeSaveChecks.cs)
        TravelPrices();   // travel's extras, dinners, splurges, events and loyalty night at today's prices, and what the room shows is what it charges (TravelPriceChecks.cs)
        Leftovers();   // every object a feature lists for clean-up is made, every text of leisure and photo mode is read (LeftoverChecks.cs)
        Endings();
        Infra2();
        Ventures2();
        Firms();
        Competitors();
        LetGo();        // a building let go: the utilities' accounts and bills, business pressure's closures, an underground car park never supplied (LetGoChecks.cs)
        SoldBack();     // a building sold and put back as the player's for nothing goes back to its buyer, a purchase since stays the player's (SoldBackChecks.cs)
        Hiring();
        Tech();
        Careers();
        WorkPace();     // place keys, the professions a type employs and the save's writer, as they were (WorkPaceChecks.cs)
        LifePace();     // utility accounts and Flare's shops seen from their indexes, as the passes found them (LifePaceChecks.cs)
        Apps();         // the apps' clicks: a table written as it scrolls, the Ledger's pages kept, a photo's small copy made a row at a time (AppsChecks.cs)
        CartQueue();
        VendorCartFixes();   // the drinks cart off crossings, its pitch checked after the cut, a customer who cannot reach it replaced (VendorCartFixChecks.cs)
        StockerFixes();      // the shelf to stock shown as soon as a box is carried in, its marker cleared on the way out (StockerFixChecks.cs)
        Work();
        Desks();
        SetOff();       // a class's desk, the work desk and the computer at home: set off as the game's activities, stood up whatever fails, the keys (SetOffChecks.cs)
        SessionDesks(); // the school's and the university's desks in a Going Public session: never the game's study panel, which it takes (SessionDeskChecks.cs)
        Life();
        CareerPay();    // what the professions pay against the gigs, how long courses take, the reviews' pace, the climb, a timetable against the working days (CareerPayChecks.cs)
        Own();
        FamilyStudy();
        Districts();
        Interiors();
        Designs();      // the ready-made designs: the firms' folders, the names, the showroom's lot, and the plan from the game's own design files (DesignChecks.cs)
        BusRide();
        PlayerBank();
        Branches();     // the bank branches: an hour's customers, visits in the bank's books, the reach over many branches, a branch that pays, the save, the option off (BranchChecks.cs)
        BankLevels();   // the bank's levels and the owner's pay: each level's limits binding, moving up, an older save's bank fitted, the pay's split, the balance figures (BankTierChecks.cs)
        Office();
        CivicFixes();   // the player's own canvasser's leaflet, the bridges to Standing and the court, the donation cap (CivicFixChecks.cs)
        SaveLinesKept();
        Morning();
        EndScale();     // the end game at scale: each venture's costs, upkeep, staff and income by size, the payback, old saves phased in, the option off (EndScaleChecks.cs)
        BugReports();   // "Report a bug": report.txt, the user folder taken out, a log's tail, the mod's lines, nothing sent (BugReportChecks.cs)
        Rivals();       // the rival tycoons: growth, pace, bounds, counters, a takeover, a seed, the save and the texts (RivalChecks.cs)
        Wages();        // wages the mod raises: the report's 30 days reproduced, the cap, one rise a period, the repair on load, every write bounded (WageChecks.cs)
        BrandRec();     // three gyms' recognition over 20 days, tenths shown, mornings missed, a quit influencer's paid posts, paid posts counted and saved (BrandRecChecks.cs)
        DayRoll();      // the game's night kept whole: the report's growing payroll and stuck brand reproduced, unknown goods taken out of the market, hours a stopped night left put right (DayRollChecks.cs)
        Photos();       // the Camera and the Gallery: names, the cap, deleted saves, the order from a folder, the photo and its small copy to Flare, the camera's rules, the restore, the sources, the texts (PhotoChecks.cs)
        Gallery();      // the Gallery: the photo that would not open, one tap, albums, days, rows in view, before and after, favourites, texts (GalleryChecks.cs)
        GuideMarks();   // the mod's marks on the game's destination guider: whose a marker is, Hide whatever was believed, the watch by time, owner and scene, unloading, the accident's own end (GuideMarkChecks.cs)
        TextBoxes();    // issue 22: a call's or a talk's text box closed however it ends (the phone shut, Esc, hung up, left, a door, a load, its own time), the panel's handle back after a call, the holds' own values (TextBoxChecks.cs)
        BugBooks();     // "Report a bug" with the staff and the money: each wage against the game's own with its hours, a day of each rota, yesterday's and today's money by kind and place (BugBooksChecks.cs)
        CardFix();      // the credit card: paid off by hand inside a debt arrangement and the arrangement ended once, one miss a statement, the overdue minimum carried (CardFixChecks.cs)
        EconomyFixes(); // brands, services, the dealership and property: money paid short or twice, figures stale or a day out, state never put back (EconomyFixChecks.cs)
        CompanyFixes(); // running a business: a campaign's report, the day's books, cover, licences, a strike's overtime, a rival's offer, the strike's day (CompanyFixChecks.cs)
        RotaFixes();    // the staff rota planner: a choice kept, a job never stuck, raises in the stamp, a confirmed plan as confirmed, the goods, the ladder, the page (RotaFixChecks.cs)
        EconomyFixes2(); // the own bank, the city, the web shops, investors and the Finance app: days run while off, money paid short or twice, sums at the start's prices, state lost (EconomyFix2Checks.cs)
        BankingFixes(); // the player's banking: the student loan kept, an approval's interest once and on what is financed, a card paid early, a quest loan, loans priced again, the lender's fee (BankingFixChecks.cs)
        SocialFixes();  // the social life: a loan's few more days, a report placed indoors, a missed call's favour, one trainer a cast, an outing put forward kept (SocialFixChecks.cs)
        PeopleAge();    // the People page: each person's age, a year more at each birthday, in every language; the Guide names People as a contact (PeopleAgeChecks.cs)
        MorePeople();   // more people to meet: the first eight kept, every place bringing somebody new up to 400, the later people's own record, the circle (MorePeopleChecks.cs)
        CinemaFixes();  // a film with a friend: rested as in the game's own cinema seat, given back on every way out (CinemaFixChecks.cs)
        PartnerTalk();    // the partner: Social's calls, texts and outings count as talking for family life (PartnerTalkChecks.cs)
        MealFixes();    // a completed meal fills once by the game's food and records bought food through the bridge
        ClientHours();  // cleaning and stocking: a client open for the walk and the work, one closing calls the job off, the clock paid up to closing (ClientHoursChecks.cs)
        TechFixes();    // the tech sector: a board that cannot hire, a pitch whose meeting never opened, the utilities asked safely, an interview cut short (TechFixChecks.cs)
        TechMoves();    // a tech firm moved by the moving company: its data follows whole, a save left behind is put back, nothing followed with a doubt (TechMoveChecks.cs)
        BizManLink();   // the Company app's Open in BizMan: on every business's page, the game's BizMan.Open for the player's own business, the notice, the texts (BizManLinkChecks.cs)
        MarketFixes();  // the stock market: a guest's trade lines culture-free, the index steady as companies join over the cap, the tape swapped safely, the chart's guard (MarketFixChecks.cs)
        CareersFixes(); // education and careers: stood down once the guard has switched it off, no sitting on a disabled host, a lost course never read back (CareersFixChecks.cs)
        InterviewPlaces(); // an interview where it is held: in at the door behind the black screen and out again, at the home's computer, every end (InterviewPlaceChecks.cs)
        ClassCounts();  // a class, homework or exam being sat is never lost: settled once by its own record, the hourly pass asks the book, the clock's minutes, the school desk's keys (ClassCountChecks.cs)
        DeskStarts();   // a classroom door, a school desk and the Campus app begin a class or exam by one rule; no button the game's panel cannot fill or dim (DeskStartChecks.cs)
        HomeFixes();    // home life: the roommate's talk test with no character, the door to social life nobody read, rain and sleep in a hotel room (HomeFixChecks.cs)
        LifeFixes();    // life events: news of the player's home, building, neighbours and landlord only to a player with a home (LifeFixChecks.cs)
        TravelFixes();  // travel: the hotel room refused while the game was paused (TravelFixChecks.cs)
        HotelNights();  // one receipt per booking and stay-day, including day zero, across faults and loads
        ChessFixes();   // street chess: the whole setup clear of crossings in both placement paths (ChessFixChecks.cs)
        LedgerFixes();  // the City Ledger: a kiosk copy never charged when the page cannot open, the subscription's lines (LedgerFixChecks.cs)
        FamilyWealth(); // the partner takes the player's money by how they lean (FamilyWealthChecks.cs)
        FamilyWish();   // the partner stops working or retrains once the player is rich (FamilyWishChecks.cs)
        FamilyExpect(); // the partner expects a bigger home and more evenings out as the player gets richer (FamilyExpectChecks.cs)
        FamilyWork();   // the partner cuts back to half days once the player is well off (FamilyWorkChecks.cs)
        FamilyFixes();  // family life: a refused passage done at once, the player's own missed appointment booked again, counselling's threshold (FamilyFixChecks.cs)
        FamilyHomeFixes();  // the partner at home: night texts kept for the morning, cooking and watching together, moving about, the chat face to face (FamilyHomeChecks.cs)
        FamilyChores();  // household chores asked when really so and counted when really done (FamilyChoreChecks.cs)
        FamilyHeldTexts();  // the family texts kept for the morning, kept in the save too (FamilyHeldTextChecks.cs)
        VentureFollowOns();  // a startup's follow-on once a round, back to the share before it and no more, kept in the save, older saves as they were (VentureFollowOnChecks.cs)
        MetaFixes();    // the options page and Report a bug: Import without its backup, a report never left busy, the pages' search and dispatcher lines (MetaFixChecks.cs)
        MoodBuffs();    // the mod's moods: a cap from each catalogue entry, a load puts saved moods back as saved (never through Going Public's scaled add), a save past the cap trimmed once (MoodBuffChecks.cs)
        LaterLanguages(); // Dutch and Turkish: listed wherever the other languages are, as complete as the other translations, What's new numbered and marked (LaterLanguageChecks.cs)
        RivalOffers();  // a rival's offer whose employee has left loses its buttons, every way of leaving, an older save (RivalOfferChecks.cs)
        StaffLeaving(); // an employee the mod lets go leaves once, with one wage and one notice; one rival's offer to an employee at a time (StaffLeaveChecks.cs)
        HardTimesTriggers(); // a difficult moment at home comes of what is going on: each one's causes, nothing when all is well, a cause gone, the options, the save, the same state (HardTimesChecks.cs)
        MorningNotices(); // the morning line only from a sleep the game ended, with its hours; the city's news of the night packed into it; four notices' switches (MorningNewsChecks.cs)
        CalendarApp();  // the Calendar: the months of every year length, the grid, each schedule's days against its feature's own rule, an empty month, the sources, the texts (CalendarAppChecks.cs)
        Meals();        // meals and music: the seat a meal is taken to, how long it is eaten, the dance's lift and its cap, every way out, the texts (MealsChecks.cs)
        Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
        return _failed == 0 ? 0 : 1;
    }

    private static void Catalogue()
    {
        int sum = 0;
        foreach (int w in CreditCatalogue.Weights)
        {
            sum += w;
        }
        Check(sum == 100, "the factors' weights add up to 100 (they add up to " + sum + ")");
        Check(CreditCatalogue.Weights.Length == (int)CreditFactor.Count && CreditCatalogue.FactorKeys.Length == (int)CreditFactor.Count, "a weight and a name for every factor");
        Check(CreditCatalogue.GradeFrom.Length == 6 && CreditCatalogue.GradeKeys.Length == 6, "six grades, each with a name");
        Check(CreditCatalogue.GradeOf(300) == CreditGrade.VeryPoor && CreditCatalogue.GradeOf(499) == CreditGrade.VeryPoor, "300 to 499 is very poor");
        Check(CreditCatalogue.GradeOf(500) == CreditGrade.Poor && CreditCatalogue.GradeOf(579) == CreditGrade.Poor, "500 to 579 is poor");
        Check(CreditCatalogue.GradeOf(580) == CreditGrade.Fair && CreditCatalogue.GradeOf(669) == CreditGrade.Fair, "580 to 669 is fair");
        Check(CreditCatalogue.GradeOf(670) == CreditGrade.Good && CreditCatalogue.GradeOf(739) == CreditGrade.Good, "670 to 739 is good");
        Check(CreditCatalogue.GradeOf(740) == CreditGrade.VeryGood && CreditCatalogue.GradeOf(799) == CreditGrade.VeryGood, "740 to 799 is very good");
        Check(CreditCatalogue.GradeOf(800) == CreditGrade.Excellent && CreditCatalogue.GradeOf(850) == CreditGrade.Excellent, "800 to 850 is excellent");
        Check(CreditCatalogue.GradeFrom[(int)CreditGrade.Fair] == CreditCatalogue.LendingThreshold, "the lending threshold is where 'fair' begins");
        Check(CreditCatalogue.GradeTo(CreditGrade.Poor) == 579 && CreditCatalogue.GradeTo(CreditGrade.Excellent) == 850, "the top of a grade");
        Check(CreditCatalogue.ScoreFor(0f) == 300 && CreditCatalogue.ScoreFor(100f) == 850 && CreditCatalogue.ScoreFor(-5f) == 300 && CreditCatalogue.ScoreFor(140f) == 850, "the scale runs from 300 to 850");
        Check((int)CreditKind.Count == 25 && (int)CreditKind.TestAdjust == 15 && (int)CreditKind.Application == 8 && (int)CreditKind.Consolidated == 16 && (int)CreditKind.Bankruptcy == 24,
            "the kinds of event keep their numbers (they are in saves); the new ones follow the old");
    }

    private static void Start()
    {
        Begin(1);
        Check(Score() == CreditCatalogue.CleanStart && CreditCatalogue.CleanStart == 640, "no debts and no record starts at 640 (got " + Score() + ")");
        Check(CreditLedger.Grade == CreditGrade.Fair, "640 is fair");
        Begin(200);
        Check(Score() == 640, "a late start with nothing on file is the same 640");

        // A save with a loan half paid: taken as paid on time for the days that took.
        var loans = new List<LoanView> { new LoanView { Street = "5th Ave", Number = 12, Total = 10000f, Remaining = 6000f, Payment = 100, Interest = 10 } };
        var file = new CreditFile();
        CreditScoring.Open(file, loans, 90);
        Check(file.FirstCreditDay == 50 && file.OldOnTime == 40 && file.StartDay == 90, "a half-paid loan gives 40 days of history");
        Account a = Begin(90, file);
        a.Owed = 6000f;
        a.Due = 110f;
        _income = 2000f;
        int withLoan = Score();
        Check(withLoan > 640 && withLoan < 760, "a loan in good order starts above a blank file, not at the top (got " + withLoan + ")");

        // The same loan with a conviction and a court debt running late starts lower, but from the record, not from a penalty.
        _legal = new LegalStanding { Convictions = 1, LateDays = 3, Outstanding = 900 };
        int withRecord = Score();
        Check(withRecord < withLoan && withRecord >= 300, "a record lowers the starting estimate (got " + withRecord + ")");
        var early = new CreditFile();
        CreditScoring.Open(early, loans, 10);
        Check(early.FirstCreditDay == 1, "the history cannot begin before day 1");
        var fresh = new CreditFile();
        CreditScoring.Open(fresh, new List<LoanView> { new LoanView { Total = 5000f, Remaining = 5000f, Payment = 50 } }, 30);
        Check(fresh.FirstCreditDay == 30 && fresh.OldOnTime == 0, "a loan with nothing paid yet starts its history today");
    }

    private static void CleanHistory()
    {
        Account a = Begin(1);
        CreditLedger.Post(CreditKind.NewCredit, 10000, 1, "loans");
        a.Owed = 10000f;
        a.Due = 120f;
        _income = 1500f;
        int previous = Score(), first = previous;
        bool rising = true;
        for (int day = 2; day <= 200; day++)
        {
            _day = day;
            a.Owed = Math.Max(0f, a.Owed - 100f);
            CreditLedger.File.Roll(day);
            CreditLedger.Post(CreditKind.PaidOnTime, 120, day - 1, "loans");
            int now = Score();
            rising &= now >= previous - 1;   // (a point of rounding either way is not a fall)
            previous = now;
        }
        Check(rising, "a clean history never falls");
        Check(previous >= 800 && previous > first + 100, "a clean history rises towards the top (from " + first + " to " + previous + ")");
        Check(CreditLedger.Grade == CreditGrade.Excellent, "two hundred days on time is excellent");
        Check(CreditLedger.Score.Missed == 0 && CreditLedger.Score.OnTime == CreditCatalogue.PaymentFadeDays - 1, "the app counts the instalments of the last sixty days, today's not yet taken (" + CreditLedger.Score.OnTime + ")");
    }

    private static void Misses()
    {
        Account a = Begin(1);
        CreditLedger.Post(CreditKind.NewCredit, 10000, 1, "loans");
        a.Owed = 10000f;
        a.Due = 120f;
        _income = 1500f;
        for (int day = 2; day <= 40; day++)
        {
            _day = day;
            CreditLedger.Post(CreditKind.PaidOnTime, 120, day - 1, "loans");
        }
        int before = Score();
        _day = 41;
        int effect = CreditLedger.Post(CreditKind.Missed, 100, 40, "loans");
        int after = Score();
        Check(effect < -15 && after == before + effect, "a missed instalment drops the score at once (by " + effect + ")");
        Check(CreditLedger.File.UnseenMissed == 1 && CreditLedger.File.LastMissedDay == 40 && CreditLedger.File.LastMissedAmount == 100, "a miss is kept for the badge and the Planner");
        int second = CreditLedger.Post(CreditKind.Missed, 100, 41, "loans");
        Check(second < 0, "a second miss drops it further");
        int low = Score();

        // Paying on time again: it climbs back, and the misses are gone from the factor after the fade.
        int previous = low;
        bool recovering = true;
        for (int day = 42; day <= 41 + CreditCatalogue.PaymentFadeDays + 1; day++)
        {
            _day = day;
            CreditLedger.File.Roll(day);
            CreditLedger.Post(CreditKind.PaidOnTime, 120, day - 1, "loans");
            int now = Score();
            recovering &= now >= previous - 1;
            previous = now;
        }
        Check(recovering && previous > before, "it recovers over the stated sixty days (" + low + " to " + previous + ", was " + before + ")");
        Check(CreditLedger.Score.Missed == 0, "after the fade no miss weighs on the payment factor");
        Check(CreditLedger.File.OldMissed == 2, "the misses stay on the file's totals");

        // A miss weighs more on a thin file than on a long one.
        Account thin = Begin(1);
        thin.Owed = 5000f;
        CreditLedger.Post(CreditKind.PaidOnTime, 60, 1, "loans");
        int thinEffect = CreditLedger.Post(CreditKind.Missed, 50, 1, "loans");
        Check(thinEffect < effect, "a miss costs a thin file more (" + thinEffect + " against " + effect + ")");
        int part = CreditLedger.Post(CreditKind.PartPaid, 25, 1, "loans");
        Check(part < 0 && part > thinEffect, "an instalment paid in part costs less than one missed");
    }

    private static void Applications()
    {
        Begin(10);
        int before = Score();
        int effect = CreditLedger.Post(CreditKind.Application, 5000, 10, "phone");
        Check(effect < 0 && effect >= -12, "an application dents the score a little (" + effect + ")");
        int second = CreditLedger.Post(CreditKind.Application, 5000, 10, "phone");
        int third = CreditLedger.Post(CreditKind.Application, 5000, 10, "phone");
        Check(second < 0 && third < 0 && Score() == before + effect + second + third, "applying over and over costs each time");
        Check(CreditLedger.Score.Applications == 3, "three applications are counting");
        int previous = Score();
        bool fading = true;
        for (int day = 11; day <= 10 + CreditCatalogue.ApplicationFadeDays; day++)
        {
            _day = day;
            int now = Score();
            fading &= now >= previous;
            previous = now;
        }
        Check(fading && previous == before, "the dent fades over ten days and is gone (" + previous + ")");
        Check(CreditLedger.Score.Applications == 0, "after ten days no application counts");
        for (int i = 0; i < 20; i++)
        {
            CreditLedger.Post(CreditKind.Application, 500, 20, "phone");
        }
        Check(CreditLedger.Score.Factor[(int)CreditFactor.Applications] == 0f && Score() >= 300, "the dent stops at the whole factor");
    }

    private static void OwedAndLegal()
    {
        Account a = Begin(5);
        int clear = Score();
        a.Owed = 50000f;
        int half = Score();
        a.Owed = 100000f;
        int full = Score();
        a.Owed = 250000f;
        int over = Score();
        Check(clear > half && half > full && full == over, "owing more of what could be borrowed lowers the score, to a floor (" + clear + ", " + half + ", " + full + ")");
        a.Owed = 20000f;
        a.Due = 300f;
        _income = 0f;
        int noIncome = Score();
        _income = 3000f;
        int income = Score();
        _income = 600f;
        int tight = Score();
        Check(income > tight && tight == noIncome, "payments against income: comfortable beats tight, and no income is the worst (" + income + ", " + tight + ")");
        Check(CreditLedger.Score.ServiceShare == 0.5f, "the share of income the payments take is reported");
        a.Cap = 0f;
        Check(CreditLedger.Gather().Limit == 0f && Score() <= tight, "owing with nothing to measure against counts as fully used");

        Begin(5);
        int clean = Score();
        _legal = new LegalStanding { Outstanding = 400 };
        int owing = Score();
        _legal = new LegalStanding { Outstanding = 400, LateDays = 2 };
        int late = Score();
        _legal = new LegalStanding { Convictions = 1 };
        int convicted = Score();
        _legal = new LegalStanding { Convictions = 2, Judgments = 1, LateDays = 9, Outstanding = 1 };
        int worst = Score();
        Check(clean > owing && owing > late && clean > convicted && convicted > worst, "the record: a debt, a late debt, a conviction, all of it");
        Check(CreditScoring.Legal(_legal) == 0f && clean - worst == (int)Math.Round(CreditCatalogue.PointsOf(CreditFactor.Legal)), "the record can cost its whole 15% and no more (" + (clean - worst) + " points)");
        Check(worst < CreditCatalogue.LendingThreshold, "a bad enough record alone puts a blank file under the threshold (" + worst + ")");
        var tips = new CreditFactor[2];
        int count = CreditScoring.Tips(CreditLedger.Score, tips);
        Check(count == 2 && (tips[0] == CreditFactor.Legal || tips[1] == CreditFactor.Legal), "with that record, the record is one of the two things to mend");
    }

    private static void Bounds()
    {
        var random = new Random(12345);
        bool inside = true;
        for (int run = 0; run < 400; run++)
        {
            Account a = Begin(1 + random.Next(300));
            a.Owed = random.Next(3) == 0 ? 0f : random.Next(400000);
            a.Cap = random.Next(4) == 0 ? 0f : random.Next(300000);
            a.Due = random.Next(5000);
            _income = random.Next(8000) - 2000;
            _legal = new LegalStanding { Convictions = random.Next(4), Judgments = random.Next(3), LateDays = random.Next(12), Outstanding = random.Next(2) * 500, RecentOffences = random.Next(8) };
            for (int i = 0; i < 40; i++)
            {
                var kind = (CreditKind)random.Next((int)CreditKind.TestAdjust);
                CreditLedger.Post(kind, random.Next(20000), _day - random.Next(80), "x", 1 + random.Next(3));
            }
            if (random.Next(4) == 0)
            {
                CreditLedger.TestSet(300 + random.Next(551), _day);
            }
            int score = Score();
            inside &= score >= CreditCatalogue.ScoreMin && score <= CreditCatalogue.ScoreMax;
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                inside &= CreditLedger.Score.Factor[f] >= 0f && CreditLedger.Score.Factor[f] <= 1f;
            }
        }
        Check(inside, "the score is always within 300 to 850 and every factor within 0 to 1");
    }

    private static void Gate()
    {
        CreditLedger.Close();
        CreditRefusal reason;
        Check(!CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.NotReady, "no file, no loan");
        Check(CreditLedger.Post(CreditKind.Missed, 1, 1, "x") == 0 && CreditLedger.Value == 640, "nothing is recorded while there is no file");
        Begin(1);
        Check(CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.None, "at 640 lenders lend");
        CreditLedger.TestSet(580, 1);
        Check(CreditLedger.CanBorrow(out reason), "at exactly 580 lenders lend");
        CreditLedger.TestSet(579, 1);
        Check(!CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.BelowThreshold, "at 579 nobody lends");
        Check(LendingTerms.Decide(579, 1000f, 50000f) == LoanAnswer.BelowThreshold && LendingTerms.Decide(300, 500f, 1e9f) == LoanAnswer.BelowThreshold, "under the threshold every amount is refused");
        Check(LendingTerms.Decide(580, 1000f, 50000f) == LoanAnswer.Approved, "at the threshold an amount within the limit is approved");
        Check(LendingTerms.Decide(700, 499f, 50000f) == LoanAnswer.TooLittle && LendingTerms.Decide(700, 500f, 50000f) == LoanAnswer.Approved, "the smallest loan is 500");
        Check(LendingTerms.Decide(700, 50001f, 50000f) == LoanAnswer.TooMuch && LendingTerms.Decide(700, 50000f, 50000f) == LoanAnswer.Approved, "no more than the lender will lend now");

        int changes = 0;
        CreditGrade was = CreditGrade.Fair, now = CreditGrade.Fair;
        Action<CreditGrade, CreditGrade> heard = (a, b) =>
        {
            changes++;
            was = a;
            now = b;
        };
        CreditLedger.GradeChanged += heard;
        CreditLedger.TestSet(578, 1);
        Check(changes == 0, "a move inside a grade raises nothing");
        CreditLedger.TestSet(700, 1);
        Check(changes == 1 && was == CreditGrade.Poor && now == CreditGrade.Good && CreditLedger.File.UnseenGrade && CreditLedger.File.LastGradeWas == (int)CreditGrade.Poor, "a change of grade is raised once, with both grades");
        CreditLedger.GradeChanged -= heard;
    }

    private static void Terms()
    {
        const float own = 100000f;
        const int rate = 8;
        bool capRises = true, rateFalls = true, floor = true, shut = true;
        LenderTerms previous = LendingTerms.For(CreditCatalogue.LendingThreshold, own, rate);
        for (int score = CreditCatalogue.ScoreMin; score <= CreditCatalogue.ScoreMax; score++)
        {
            LenderTerms t = LendingTerms.For(score, own, rate);
            if (score < CreditCatalogue.LendingThreshold)
            {
                shut &= !t.Lends && t.Cap == 0f;
                continue;
            }
            capRises &= t.Cap >= previous.Cap && t.Lends;
            rateFalls &= t.AnnualRate <= previous.AnnualRate;
            previous = t;
            for (int lowRate = 0; lowRate <= 3; lowRate++)
            {
                floor &= LendingTerms.For(score, own, lowRate).AnnualRate >= CreditCatalogue.RateFloor;
            }
        }
        Check(shut, "under the threshold the cap is 0 at every score");
        Check(capRises, "the cap never falls as the score rises");
        Check(rateFalls, "the rate never rises as the score rises");
        Check(floor, "a rate is never under 1");
        LenderTerms at = LendingTerms.For(580, own, rate), best = LendingTerms.For(800, own, rate), top = LendingTerms.For(850, own, rate), mid = LendingTerms.For(690, own, rate);
        Check(at.Cap == 40000f && at.AnnualRate == 14, "at 580: 40% of the bank's cap, its rate plus 6");
        Check(best.Cap == 130000f && best.AnnualRate == 6 && top.Cap == best.Cap && top.AnnualRate == best.AnnualRate, "from 800: 130% of the cap, the rate less 2");
        Check(mid.Cap == 85000f && mid.AnnualRate == 10, "half way: 85% of the cap, the rate plus 2");
        Check(LendingTerms.For(579, own, rate).AnnualRate == 14, "under the threshold the rate is the threshold's (it is not offered)");
        Check(LendingTerms.For(800, own, 2).AnnualRate == 1 && LendingTerms.For(800, own, 1).AnnualRate == 1, "a low rate stops at 1");
        Check(LendingTerms.For(700, 0f, rate).Cap == 0f && LendingTerms.For(700, -5f, rate).Cap == 0f, "a bank with no cap lends nothing");
        Check(LendingTerms.MostNow(40000f, 15000f, 1e9f) == 25000f && LendingTerms.MostNow(40000f, 50000f, 1e9f) == 0f, "what is owed to a lender comes off its cap");
        Check(LendingTerms.MostNow(40000f, 0f, 12000.9f) == 12000f && LendingTerms.MostNow(40000f, 0f, -3f) == 0f, "never more than the borrower's means allow");
    }

    private static void Fee()
    {
        Check(LendingTerms.PhoneFee(500f) == 25 && LendingTerms.PhoneFee(1000f) == 25 && LendingTerms.PhoneFee(1666f) == 25, "the fee is never under 25");
        Check(LendingTerms.PhoneFee(2000f) == 30 && LendingTerms.PhoneFee(10000f) == 150 && LendingTerms.PhoneFee(100000f) == 1500, "the fee is 1.5% of the amount");
        Check(LendingTerms.PhoneFee(2010f) == 31, "a fee with a fraction is rounded up");
        Check(LendingTerms.PhoneFee(0f) == 0 && LendingTerms.PhoneFee(-10f) == 0, "no amount, no fee");
        bool rises = true;
        int previous = 0;
        for (int amount = 500; amount <= 300000; amount += 137)
        {
            int fee = LendingTerms.PhoneFee(amount);
            rises &= fee >= previous && fee >= amount * 0.015f - 0.01f && fee <= amount * 0.015f + 25f;
            previous = fee;
        }
        Check(rises, "the fee never falls as the amount rises");
    }

    private static void Folding()
    {
        Begin(1);
        CreditFile file = CreditLedger.File;
        long paid = 0;
        int count = 0;
        for (int i = 0; i < 500; i++)
        {
            _day = 1 + i / 3;
            int amount = 100 + i;
            CreditLedger.Post(CreditKind.PaidOnTime, amount, _day, "loans", 1 + i % 2);
            paid += amount;
            count += 1 + i % 2;
            if (i % 50 == 0)
            {
                CreditLedger.Post(CreditKind.Missed, 77, _day, "loans");
            }
        }
        Check(file.Events.Count == CreditCatalogue.LedgerMost, "the file keeps the last 120 events (" + file.Events.Count + ")");
        long amountOnFile;
        int total = file.Total(CreditKind.PaidOnTime, out amountOnFile);
        Check(total == count && amountOnFile == paid, "folding keeps the totals (" + total + " of " + count + ", " + amountOnFile + " of " + paid + ")");
        long missedAmount;
        Check(file.Total(CreditKind.Missed, out missedAmount) == 10 && missedAmount == 770, "every kind keeps its own total");
        Check(file.Events[file.Events.Count - 1].Day == _day && file.Events[0].Day <= file.Events[file.Events.Count - 1].Day, "the newest events are the ones kept, in order");

        file.Roll(_day);
        int inWindow = 0, missedInWindow = 0;
        foreach (PayDay d in file.Days)
        {
            inWindow += d.OnTime;
            missedInWindow += d.Missed;
        }
        Check(file.Days.Count <= CreditCatalogue.PaymentFadeDays && file.Days[0].Day > _day - CreditCatalogue.PaymentFadeDays, "the days kept are the last sixty");
        Check(inWindow + file.OldOnTime == count && missedInWindow + file.OldMissed == 10, "rolling the days keeps every instalment counted");

        // A day posted out of order lands in its place.
        file.CountPayments(_day - 5, 1, 0, 0);
        bool ordered = true;
        for (int i = 1; i < file.Days.Count; i++)
        {
            ordered &= file.Days[i].Day > file.Days[i - 1].Day;
        }
        Check(ordered, "the days stay in order, one entry a day");
    }

    // The file's line without its header: the days, the events, the totals and the loans.
    private static string Body(CreditFile file)
    {
        string line = CreditStore.Write(file);
        return line.Substring(line.IndexOf("~d:", StringComparison.Ordinal));
    }

    private static void Tests()
    {
        Account a = Begin(30);
        a.Owed = 3000f;
        CreditLedger.Post(CreditKind.PaidOnTime, 50, 29, "loans");
        string before = Body(CreditLedger.File);
        int real = Score();
        Check(CreditLedger.TestSet(560, 30) == 560 && !CreditLedger.Score.Lends, "the test tool sets 560");
        Check(CreditLedger.TestSet(600, 30) == 600 && CreditLedger.TestSet(760, 30) == 760, "and 600, and 760");
        CreditLedger.Post(CreditKind.Missed, 100, 30, "test", 1, true);
        for (int day = 1; day <= 30; day++)
        {
            CreditLedger.Post(CreditKind.PaidOnTime, 100, day, "test", 1, true);
        }
        Check(CreditLedger.File.UnseenMissed == 0, "a test miss sets no badge");
        Check(CreditLedger.TestClear(), "there was something to clear");
        Check(Score() == real && Body(CreditLedger.File) == before && CreditLedger.File.TestOffset == 0, "clearing the test entries leaves the score, the days, the events and the totals exactly as they were");
        Check(!CreditLedger.TestClear(), "nothing to clear the second time");

        // Test entries never reach the totals, even when they are folded away.
        Begin(1);
        for (int i = 0; i < 200; i++)
        {
            CreditLedger.Post(CreditKind.Missed, 10, 1, "test", 1, true);
        }
        long amount;
        Check(CreditLedger.File.Total(CreditKind.Missed, out amount) == 0 && amount == 0, "test entries are never folded into the totals");
    }

    private static void SaveAndLoad()
    {
        Account a = Begin(12);
        a.Owed = 8000f;
        CreditFile file = CreditLedger.File;
        CreditLedger.Post(CreditKind.NewCredit, 8000, 12, "phone");
        CreditLedger.Post(CreditKind.Application, 8000, 12, "phone");
        for (int day = 13; day <= 150; day++)
        {
            _day = day;
            file.Roll(day);
            CreditLedger.Post(day % 17 == 0 ? CreditKind.Missed : CreditKind.PaidOnTime, 95, day - 1, "loans");
        }
        CreditLedger.TestSet(601, _day);
        file.LegalSeen = new LegalStanding { Convictions = 1, Judgments = 2, LateDays = 3, Outstanding = 450, RecentOffences = 1 };
        file.Loans.Add(new LoanMark { Street = "Odd|Name; St~", Number = 7, Total = 8000f, FirstDay = 12, Remaining = 1234.5f, Payment = 66, Interest = 9 });
        file.Loans.Add(new LoanMark { Street = "W 34th St", Number = 120, Total = 25000f, FirstDay = 90, Remaining = 25000f, Payment = 208, Interest = 34 });
        int score = Score();

        var data = new Dictionary<string, string>();
        CreditStore.Save(data, file);
        string line = data[CreditStore.Key];
        Check(data.Count == 1 && line.StartsWith("v=2~h:"), "one line in the save, under reallife:economy, with its version first");
        Check(line.IndexOf('\n') < 0 && line.Length < 8000, "the line is small (" + line.Length + " characters with a full file)");
        bool fresh;
        int version;
        CreditFile back = CreditStore.Load(data, out fresh, out version);
        Check(back != null && !fresh && version == 2, "it loads");
        Check(back != null && CreditStore.Write(back) == line, "a round trip writes the same line");
        if (back != null)
        {
            Check(back.Loans.Count == 2 && back.Loans[0].Street == "Odd Name  St " && back.Loans[0].Remaining == 1234.5f && back.Loans[1].Interest == 34, "the loans come back, a street name's dividers taken out");
            Check(back.Loans[0].Ref == null, "no object is carried across a save");
            Check(back.LegalSeen.Judgments == 2 && back.LegalSeen.Outstanding == 450 && back.TestOffset == file.TestOffset && back.FirstCreditDay == 12, "the header comes back");
            Begin(_day, back).Owed = 8000f;
            Check(Score() == score, "the loaded file scores the same (" + Score() + " and " + score + ")");
        }

        // A later version: a higher number, more fields at the end of records, and a part this version does not know.
        string later = line.Replace("v=2~", "v=3~").Replace("~d:", "|77|new~x:something|new;more~d:") + "~z:1|2|3";
        int laterVersion;
        CreditFile fromLater = CreditStore.Read(later, out laterVersion);
        Check(fromLater != null && laterVersion == 3, "a save from a later version is read");
        Check(fromLater != null && CreditStore.Write(fromLater) == line, "what this version knows of it is all there, written back as this version");

        // Damage: nothing throws, and what cannot be read opens a fresh file.
        var damaged = new[] { "", "v=", "v=0~h:1", "nonsense", "v=1", "v=1~h:~d:~e:~f:~l:", "v=1~h:a|b|c~d:x|y;1|2~e:99|1|1;|||~f:x~l:a|b", "v=1~~~q~h:5", "v=1~e:" + new string(';', 400) };
        bool calm = true;
        int opened = 0;
        foreach (string text in damaged)
        {
            try
            {
                var d = new Dictionary<string, string> { { CreditStore.Key, text } };
                CreditFile f = CreditStore.Load(d, out fresh, out version);
                opened += f != null ? 1 : 0;
                calm &= f == null == fresh;
                if (f != null)
                {
                    CreditStore.Write(f);
                    var s = new CreditScore();
                    CreditScoring.Compute(f, default(CreditInputs), 10, s);
                    calm &= s.Value >= 300 && s.Value <= 850;
                }
            }
            catch (Exception)
            {
                calm = false;
            }
        }
        Check(calm, "a damaged line never throws");
        Check(opened == 5, "a line with a version is read as far as it goes, one without is not (" + opened + ")");
        Check(CreditStore.Load(null, out fresh, out version) == null && fresh && CreditStore.Load(new Dictionary<string, string>(), out fresh, out version) == null && fresh, "a save without the line is a fresh file");
        var many = new CreditFile();
        var text2 = new System.Text.StringBuilder("v=1~e:");
        for (int i = 0; i < 300; i++)
        {
            text2.Append(i > 0 ? ";" : "").Append("0|1|5|1|0|0|loans");
        }
        Check(CreditStore.Read(text2.ToString(), out version).Events.Count == CreditCatalogue.LedgerMost && many.Events.Count == 0, "a line with too many events is cut to the cap");
    }

    private static LoanView View(object reference, string street, int number, float total, float remaining, int payment, int interest = 10)
    {
        return new LoanView { Ref = reference, Street = street, Number = number, Total = total, Remaining = remaining, Payment = payment, Interest = interest };
    }

    private static int Count(List<LoanFinding> findings, LoanNote note)
    {
        int count = 0;
        foreach (LoanFinding f in findings)
        {
            count += f.Note == note ? 1 : 0;
        }
        return count;
    }

    private static void Identity()
    {
        object a = new object(), b = new object(), c = new object(), d = new object();
        var marks = new List<LoanMark>();
        var findings = new List<LoanFinding>();
        var live = new List<LoanView> { View(a, "5th Ave", 12, 10000f, 10000f, 100), View(b, "5th Ave", 12, 10000f, 10000f, 100), View(c, "Wall St", 3, 4000f, 4000f, 40) };
        LoanBook.Sync(marks, live, 5, findings, true);
        Check(marks.Count == 3 && findings.Count == 0, "the first look is quiet");
        Check(ReferenceEquals(marks[0].Ref, a) && ReferenceEquals(marks[1].Ref, b) && marks[2].FirstDay == 5, "each loan has its mark");

        // The list changes: the first is paid off and removed, one is added at the end.
        live = new List<LoanView> { View(b, "5th Ave", 12, 10000f, 10000f, 100), View(c, "Wall St", 3, 4000f, 4000f, 40), View(d, "5th Ave", 12, 10000f, 10000f, 100) };
        LoanBook.Sync(marks, live, 6, findings);
        Check(Count(findings, LoanNote.Gone) == 1 && Count(findings, LoanNote.New) == 1 && findings.Count == 2, "one gone, one new, though all three share a bank and two an amount");
        Check(marks.Count == 3 && ReferenceEquals(marks[0].Ref, b) && ReferenceEquals(marks[2].Ref, d) && marks[2].FirstDay == 6, "the marks follow the objects, not the places in the list");

        findings.Clear();
        live[0] = View(b, "5th Ave", 12, 10000f, 7000f, 100);
        live[1] = View(c, "Wall St", 3, 4000f, 4000f, 60);
        LoanBook.Sync(marks, live, 6, findings);
        Check(findings.Count == 2 && findings[0].Note == LoanNote.ExtraPaid && findings[0].Amount == 3000 && findings[1].Note == LoanNote.PaymentChanged && findings[1].Amount == 60, "paying part off and changing the daily payment are seen");
        findings.Clear();
        LoanBook.Sync(marks, live, 6, findings);
        Check(findings.Count == 0, "nothing changed, nothing found");

        // Across a save: the objects are new, the fingerprints and balances tell the loans apart.
        var file = new CreditFile();
        file.Loans.AddRange(marks);
        int version;
        CreditFile loaded = CreditStore.Read(CreditStore.Write(file), out version);
        object b2 = new object(), c2 = new object(), d2 = new object();
        live = new List<LoanView> { View(b2, "5th Ave", 12, 10000f, 7000f, 100), View(c2, "Wall St", 3, 4000f, 4000f, 60), View(d2, "5th Ave", 12, 10000f, 10000f, 100) };
        findings.Clear();
        LoanBook.Sync(loaded.Loans, live, 9, findings);
        Check(findings.Count == 0 && loaded.Loans.Count == 3, "after a load every loan is known again: nothing new, nothing gone");
        Check(ReferenceEquals(loaded.Loans[0].Ref, b2) && loaded.Loans[0].Remaining == 7000f && ReferenceEquals(loaded.Loans[2].Ref, d2) && loaded.Loans[2].FirstDay == 6, "twins of one bank and one amount are told apart by their balance");

        // The list in another order after the load, and one twin paid down while the mod was away.
        loaded = CreditStore.Read(CreditStore.Write(file), out version);
        live = new List<LoanView> { View(d2, "5th Ave", 12, 10000f, 9000f, 100), View(c2, "Wall St", 3, 4000f, 4000f, 60), View(b2, "5th Ave", 12, 10000f, 7000f, 100) };
        findings.Clear();
        LoanBook.Sync(loaded.Loans, live, 9, findings);
        Check(Count(findings, LoanNote.New) == 0 && Count(findings, LoanNote.Gone) == 0 && Count(findings, LoanNote.ExtraPaid) == 1, "a twin paid down in the meantime is still the same loan");
        Check(ReferenceEquals(loaded.Loans[0].Ref, b2) && ReferenceEquals(loaded.Loans[2].Ref, d2), "the exact balance is matched first, whatever the order");

        // A loan that left the list is not taken for a new one of the same print made the same hour.
        marks.Clear();
        findings.Clear();
        object old = new object(), fresh = new object();
        LoanBook.Sync(marks, new List<LoanView> { View(old, "Wall St", 3, 4000f, 1000f, 40) }, 3, findings, true);
        LoanBook.Sync(marks, new List<LoanView> { View(fresh, "Wall St", 3, 4000f, 4000f, 40) }, 4, findings);
        Check(Count(findings, LoanNote.Gone) == 1 && Count(findings, LoanNote.New) == 1 && marks.Count == 1 && marks[0].FirstDay == 4, "paid off and borrowed again is one gone and one new");
    }

    private static void DailyRun()
    {
        object a = new object(), b = new object(), c = new object(), d = new object();
        var marks = new List<LoanMark>();
        var findings = new List<LoanFinding>();
        // Before the run (the hour event): four loans. a is paid, b is missed (only its interest is taken),
        // c has less left than its daily payment and is paid off, d was changed by the player an hour ago.
        var before = new List<LoanView> { View(a, "5th Ave", 12, 10000f, 5000f, 100, 12), View(b, "5th Ave", 12, 10000f, 5000f, 100, 12), View(c, "Wall St", 3, 4000f, 30f, 40, 5), View(d, "Wall St", 3, 9000f, 9000f, 75, 11) };
        LoanBook.Sync(marks, before, 20, findings, true);
        var after = new List<LoanView> { View(a, "5th Ave", 12, 10000f, 4900f, 100, 12), View(b, "5th Ave", 12, 10000f, 5000f, 100, 12), View(d, "Wall St", 3, 9000f, 8925f, 75, 11) };
        LoanBook.Settle(marks, after, findings);
        Check(findings.Count == 4 && Count(findings, LoanNote.Paid) == 2 && Count(findings, LoanNote.Missed) == 1 && Count(findings, LoanNote.PaidOff) == 1, "paid, missed and paid off are told apart");
        foreach (LoanFinding f in findings)
        {
            if (f.Note == LoanNote.Missed)
            {
                Check(f.Mark.Total == 10000f && f.Amount == 100, "the missed one is the loan whose balance did not move, for its daily payment");
            }
            if (f.Note == LoanNote.PaidOff)
            {
                Check(f.Mark.Street == "Wall St" && f.Amount == 35, "the last instalment is what was left, and the interest");
            }
        }
        Check(marks.Count == 3 && marks[0].Remaining == 4900f, "the marks are brought up to date, the paid-off loan gone from them");

        // The player pays part off during the day; the hour event before the run sees it, so the run's own part is read right.
        findings.Clear();
        var midday = new List<LoanView> { View(a, "5th Ave", 12, 10000f, 2900f, 100, 12), View(b, "5th Ave", 12, 10000f, 5000f, 100, 12), View(d, "Wall St", 3, 9000f, 8925f, 75, 11) };
        LoanBook.Sync(marks, midday, 21, findings);
        Check(findings.Count == 1 && findings[0].Note == LoanNote.ExtraPaid && findings[0].Amount == 2000, "paying part off by hand is its own event");
        findings.Clear();
        LoanBook.Settle(marks, midday, findings);
        Check(Count(findings, LoanNote.Missed) == 3 && findings.Count == 3, "and if the run then takes nothing, all three are missed, the one paid down by hand included");

        // A loan with no daily payment (a bank without settings lets it be set to nothing) is neither paid nor missed.
        marks.Clear();
        findings.Clear();
        var zero = new List<LoanView> { View(a, "5th Ave", 12, 10000f, 5000f, 0, 12) };
        LoanBook.Sync(marks, zero, 5, findings, true);
        LoanBook.Settle(marks, zero, findings);
        Check(findings.Count == 0, "no payment due, nothing to report");
    }
}
