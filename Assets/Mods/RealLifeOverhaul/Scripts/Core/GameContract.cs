namespace RealLifeOverhaul
{
    /// <summary>
    /// THE GAME CONTRACT: every piece of the game the mod reaches by name, the parts of the mod that stand on it, and the
    /// type it is expected to have. Resolved once at start (<see cref="ContractBook.Resolve"/>); after a game update a
    /// missing piece switches off only its parts (<see cref="GamePart.Off"/>), each named once. ContractCheck checks
    /// every line against the game's assemblies on disk.
    /// TO ADD A PIECE: a part if it serves something new, then one line (Field, StaticField, Prop, Method, StaticMethod or
    /// TypeOf) with the assembly's simple name, the type's full name (nested after '+'), the member's name and its type as
    /// ContractBook.TypeText writes it. Optional: the code has another way without it. Read it as GameContract.X.Field
    /// (or .Property, .Setter, .Method, .Type), null when missing. No Unity or game types in this file.
    /// </summary>
    public static class GameContract
    {
        private const string Game = "BigAmbitions";
        private const string Plugins = "ExternalPlugins";
        private const string Sky = "DayNightCycle";
        private const string UnityUi = "UnityEngine.UI";
        private const string Steam = "Facepunch.Steamworks.Win64";

        static GameContract()
        {
        }

        /// <summary>Makes sure every line below is registered: the initialiser runs on first use.</summary>
        public static void Touch()
        {
        }

        // The parts: id, Meta/FeatureList feature, what it is.

        public static readonly GamePart MarkersBehind = ContractBook.Part("markers_behind", "camera", "the game's place markers put right when what they mark is behind the camera");
        public static readonly GamePart LookBehind = ContractBook.Part("look_behind", "camera", "looking behind with the right mouse button held");
        public static readonly GamePart CharacterDetail = ContractBook.Part("character_detail", "character", "the detailed character copied for the player's looks");
        public static readonly GamePart ShopPreview = ContractBook.Part("shop_preview", "character", "the lighting of the clothes shop's preview");
        public static readonly GamePart LooksGrid = ContractBook.Part("looks_grid", "character", "the mod's garments in the game's character screens");
        public static readonly GamePart WornPieces = ContractBook.Part("worn_pieces", "character", "the mod's own hair and clothes on the player");
        public static readonly GamePart ClothesShop = ContractBook.Part("clothes_shop", "leisure", "the clothes shops' screen");
        public static readonly GamePart AppTables = ContractBook.Part("app_tables", "phone", "the tables of the mod's phone apps");
        public static readonly GamePart AppButtons = ContractBook.Part("app_buttons", "phone", "the buttons of the mod's phone apps");
        public static readonly GamePart ContactPictures = ContractBook.Part("contact_pictures", "phone", "the pictures of the mod's contacts");
        public static readonly GamePart ContactSearch = ContractBook.Part("contact_search", "phone", "the search box of the Contacts app");
        public static readonly GamePart PhoneSkin = ContractBook.Part("phone_skin", "phone", "the phone's skin");
        public static readonly GamePart MapPicker = ContractBook.Part("map_picker", "navigation", "clicking the markers when choosing a place on the city map");
        public static readonly GamePart MapPlaces = ContractBook.Part("map_places", "navigation", "the mod's places in the city map's filters");
        public static readonly GamePart NavMarks = ContractBook.Part("nav_marks", "navigation", "the game's look for the markers on the minimap");
        public static readonly GamePart PanelBeside = ContractBook.Part("panel_beside", null, "the mod's cards beside the game's building card");
        public static readonly GamePart CardFlag = ContractBook.Part("card_flag", null, "the mod's cards over things in the street");
        public static readonly GamePart CardGuard = ContractBook.Part("card_guard", null, "putting away the game's card over a thing that was removed");
        public static readonly GamePart KeyClash = ContractBook.Part("key_clash", "keys", "the warning when a key of the mod is one of the game's");
        public static readonly GamePart Moods = ContractBook.Part("moods", "life", "the mod's moods");
        public static readonly GamePart Thunder = ContractBook.Part("thunder", "events", "thunderstorms in the heaviest showers");
        public static readonly GamePart ClickHome = ContractBook.Part("click_home", "home", "clicking a cooker or a coffee machine at home");
        public static readonly GamePart ClickLedger = ContractBook.Part("click_ledger", "ledger", "clicking the City Ledger at home");
        public static readonly GamePart ClickBusStop = ContractBook.Part("click_bus_stop", "transport", "clicking a bus stop");
        public static readonly GamePart ClickDesk = ContractBook.Part("click_desk", "careers_study", "clicking your desk at work");
        public static readonly GamePart Mop = ContractBook.Part("mop", "jobs", "the cleaner's mop");
        public static readonly GamePart SchoolCar = ContractBook.Part("school_car", "licences", "the driving school's car");
        public static readonly GamePart SaveCheck = ContractBook.Part("save_check", "savetools", "the save tools' test of whether the game may save here");
        public static readonly GamePart BenchSitters = ContractBook.Part("bench_sitters", "citylife", "people sitting on the game's benches");
        public static readonly GamePart StreetLamps = ContractBook.Part("street_lamps", "performance", "the savings on street lamps");
        public static readonly GamePart LampsOut = ContractBook.Part("street_lamps_out", "faults", "street lights gone dark among the city's faults");
        public static readonly GamePart FarDistricts = ContractBook.Part("far_districts", "performance", "the far districts trial of the performance benchmark");
        public static readonly GamePart UiProbe = ContractBook.Part("ui_probe", "diagnostics", "the interface part of the performance probe");
        public static readonly GamePart BodyFade = ContractBook.Part("body_fade", "ragdoll", "showing a body the game faded beside a car");
        public static readonly GamePart BusStops = ContractBook.Part("bus_stops", "transport", "bus stops kept clear of parked cars");
        public static readonly GamePart SchoolDeskPart = ContractBook.Part("school_desk", "careers_study", "attending a class at a desk of the school");
        public static readonly GamePart ComputerClassPart = ContractBook.Part("computer_class", "careers_study",
            "an online class, homework or a working day, from the buttons of the computer at home");
        public static readonly GamePart FurnitureSeats = ContractBook.Part("furniture_seats", "law", "people seated on the furniture of the mod's rooms");
        public static readonly GamePart SpecialHeadline = ContractBook.Part("special_headline", null, "a special building's own name over the game's building panel");
        public static readonly GamePart SpecialSearch = ContractBook.Part("special_search", null, "the special buildings' own names in the city map's search");
        public static readonly GamePart RideToggle = ContractBook.Part("ride_toggle", "transport", "the fast forward switch of the bus ride");
        public static readonly GamePart CustomersKept = ContractBook.Part("customers_kept", null, "the customers of the hour under way kept when the mod draws a day's customers again");
        public static readonly GamePart InteriorCustomers = ContractBook.Part("interior_customers", "interiors", "the basket and the day's customers on the Interior tab");
        public static readonly GamePart InteriorTabPart = ContractBook.Part("interior_tab", "interiors", "the Interior tab in BizMan");
        public static readonly GamePart BusYield = ContractBook.Part("bus_yield", "transport", "a bus held behind a traffic car that waits for it");
        public static readonly GamePart HairLampTest = ContractBook.Part("hair_lamp_test", "character", "the hair test's street lamp as bright as the game's");
        public static readonly GamePart PlayerDeath = ContractBook.Part("player_death", null, "waiting while the player is dead or at a funeral");
        public static readonly GamePart PrivateFences = ContractBook.Part("private_fences", null, "leaving out the buildings behind the game's private fences");
        public static readonly GamePart CardTarget = ContractBook.Part("card_target", null, "the mod's lines on the game's building card");
        public static readonly GamePart StationNames = ContractBook.Part("station_names", "transport", "the subway stations' names in the mod's journeys");
        public static readonly GamePart QuestionBox = ContractBook.Part("question_box", null, "the mod's questions in the game's question box");
        public static readonly GamePart BuildRead = ContractBook.Part("build_read", null, "reading which build of the game this is");
        public static readonly GamePart AppOpening = ContractBook.Part("app_opening", "phone", "the game's own first steps when an app of the mod's opens");
        public static readonly GamePart ClassroomMarks = ContractBook.Part("classroom_marks", "careers_study", "the marks over the university's classroom doors");
        public static readonly GamePart PersonaCourses = ContractBook.Part("persona_courses", "careers_study", "the mod's courses in the Persona app's education box");
        public static readonly GamePart FrontSignsPart = ContractBook.Part("front_signs", null, "the signs on the mod's buildings");
        public static readonly GamePart ClaimedRows = ContractBook.Part("claimed_rows", null, "the mod's buildings left out of the city map's rows of units to let");
        public static readonly GamePart StartCompetitors = ContractBook.Part("start_competitors", "tech_sector", "the competitors in the city on BizMan's start screen, for the mod's own kinds of business");
        public static readonly GamePart ChatFilter = ContractBook.Part("chat_filter", "flare", "Steam's own filter of what other players of a Going Public session type on Flare (the mod's own word list without it)");
        public static readonly GamePart PhotoHolds = ContractBook.Part("photo_holds", "photos", "the Camera telling its own hold on the player from the game's, and putting the world's clicks back as they were (the mop's floor)");
        public static readonly GamePart RivalOffers = ContractBook.Part("rival_offers", null, "taking the buttons off a rival's offer that is over");
        public static readonly GamePart StaffLeaving = ContractBook.Part("staff_leaving", null, "paying an employee the mod lets go at the turn of a day only once");
        public static readonly GamePart KeyHidden = ContractBook.Part("key_hidden", "textsai", "the key typed for Your own AI shown as dots in the game's text box");
        public static readonly GamePart CarWash = ContractBook.Part("car_wash", "garage",
            "stand-ins for the spray and the sound of a car that has none, so the game's car wash in a repair bay ends on it");
        public static readonly GamePart TrafficMend = ContractBook.Part("traffic_mend", "traffic", "putting the city's traffic right when it stops adding cars for good");

        public static readonly GamePart HotelRoom = ContractBook.Part("hotel_room", "travel", "walking in a hotel guest room");
        public static readonly GameMember HotelBedEnvironment = Field(HotelRoom, Game, "BedController", "sleepEnvironment", "SleepEnvironment");
        public static readonly GameMember HotelLampAlwaysOn = Field(HotelRoom, Game, "IndoorLightController", "ignoreTimeOfDay", "Boolean");

        // The pieces, each group under the code that reads it.

        // Camera/PoiDirection: without them every marker is looked at, and a direction marker uses the whole screen
        public static readonly GameMember PermanentPois = Optional(StaticField(MarkersBehind, Game, "UI.PermanentPointsOfInterest", "PermanentPois", "List<PointOfInterest>"));
        public static readonly GameMember PoiDeadZone = Optional(Field(MarkersBehind, Game, "PointOfInterest", "deadZone", "Vector4"));
        public static readonly GameMember PoiPointer = Optional(Field(MarkersBehind, Game, "PointOfInterest", "pointerRectTransform", "RectTransform"));

        // Camera/ThirdPersonCamera: without it the game's own right click also fires when a look behind ends
        public static readonly GameMember RightButtonHeld = Optional(StaticField(LookBehind, Game, "MouseController", "RightButtonHeld", "Boolean"));

        // Character/CharacterTools, Phone/Contacts/PortraitTurn (else the game's public call)
        public static readonly GameMember PortraitInstance = Optional(StaticField(On(CharacterDetail, ContactPictures), Game, "Character.Customization.PortraitGenerator", "_instance", "PortraitGenerator"));
        public static readonly GameMember PortraitCharacter = Optional(Field(On(CharacterDetail, ContactPictures), Game, "Character.Customization.PortraitGenerator", "portraitAppearanceSetter", "AppearanceSetter"));

        // Character/CharacterQuality (through CharacterTools.ReadField)
        public static readonly GameMember PreviewSetUp = Field(ShopPreview, Game, "EmployeeUniformPreview", "setUp", "GameObject");
        public static readonly GameMember PreviewCamera = Field(ShopPreview, Game, "EmployeeUniformPreview", "previewCamera", "Camera");

        // Character/GridLooks
        public static readonly GameMember CustomizerPicker = Field(LooksGrid, Game, "Character.Customization.CharacterCustomizer", "elementPicker", "ElementPicker");
        public static readonly GameMember CustomizerShown = Field(LooksGrid, Game, "Character.Customization.CharacterCustomizer", "onSubCategoryChanged", "UnityEvent<AppearanceElementType>");
        public static readonly GameMember PickerTemplate = Field(LooksGrid, Game, "Character.Customization.ElementPicker", "entryTemplate", "Transform");
        public static readonly GameMember PickerOutline = Field(LooksGrid, Game, "Character.Customization.ElementPicker", "_currentSelectedOutline", "GameObject");

        // Character/ModLooks
        public static readonly GameMember WornRenderers = Field(WornPieces, Game, "AppearanceSetter", "_selectedMeshRenderers", "Dictionary<AppearanceElementType,SkinnedMeshRenderer>");

        // Leisure/Shops/ClothesScreen
        public static readonly GameMember CustomizerTags = Field(ClothesShop, Game, "Character.Customization.CharacterCustomizer", "tags", "AppearanceTag[]");
        public static readonly GameMember ClothesSaveButton = Field(ClothesShop, Game, "ChangeCharacterClothesUI", "saveClothesChangesButton", "Button");

        // Core/GameUi/AppShell: the row of the game's employee table, and the colour of a chosen row
        public static readonly GameMember EmployeeTable = TypeOf(AppTables, Game, "UI.Smartphone.Apps.MyEmployees.EmployeeScrollerController");
        public static readonly GameMember EmployeeTableRow = Field(AppTables, Game, "UI.Smartphone.Apps.MyEmployees.EmployeeScrollerController", "prefab", "EmployeeCellView");
        public static readonly GameMember RowChosenColour = Optional(Field(AppTables, Game, "BaTable.BaTableCellView`1", "selectedBackgroundColor", "Color"));

        // Core/GameUi/PhoneApps, PhoneGridPager: each has another way when the field is gone
        public static readonly GameMember HudAppTemplate = Optional(Field(AppButtons, Game, "SmartphoneUI", "appButtonTemplate", "Transform"));
        public static readonly GameMember MenuAppTemplate = Optional(Field(AppButtons, Game, "UI.Smartphone.FullMenu", "appButtonTemplate", "Transform"));
        public static readonly GameMember AppButtonIcon = Optional(Field(AppButtons, Game, "AppButton", "icon", "Image"));

        // Core/GameUi/WorldPanel: each has another way
        public static readonly GameMember ResumePanel = Optional(Field(PanelBeside, Game, "UI.InGameUI.BuildingResume", "panelRectTransform", "RectTransform"));
        public static readonly GameMember OverlayVisible = Optional(NeedsSetter(Prop(CardFlag, Game, "UI.Overlays.OverlayUI", "IsVisible", "Boolean", true)));
        public static readonly GameMember OverlayHeader = Optional(Method(CardFlag, Game, "UI.Overlays.OverlayUI", "SetUpHeader", "Void"));

        // Core/GameUi/MapPicker, Core/MapPlaces, Navigation/NavMarks: the game's marker
        public static readonly GameMember PoiBlob = Field(On(MapPicker, MapPlaces, NavMarks), Game, "PointOfInterest", "blobImage", "Image");
        public static readonly GameMember PoiButton = Field(On(MapPicker, MapPlaces), Game, "PointOfInterest", "button", "Button");
        public static readonly GameMember PoiIcon = Field(NavMarks, Game, "PointOfInterest", "buildingIcon", "BuildingIcon");
        public static readonly GameMember PoiText = Field(NavMarks, Game, "PointOfInterest", "_text", "String");
        public static readonly GameMember BuildingIconImage = Field(NavMarks, Game, "BuildingIcon", "image", "Image");

        // Core/MapPlaces: the city map's filters
        public static readonly GameMember FilterToggles = Field(MapPlaces, Game, "CityMapFilters", "_filterToggles", "Dictionary<String,Toggle>");
        public static readonly GameMember FilterEntries = Field(MapPlaces, Game, "CityMapFilters", "_filterEntries", "Dictionary<String,CityMapFilter>");
        public static readonly GameMember FilterToggleAll = Field(MapPlaces, Game, "CityMapFilters", "_toggleAll", "Toggle");
        public static readonly GameMember FilterCreate = Method(MapPlaces, Game, "CityMapFilters", "CreateFilter", "CityMapFilter",
            "String", "Sprite", "CityMapFilterData", "CityMapFilterCategory", "LanguageChangeEventDataHolder", "Nullable<Vector3>", "Func<Nullable<Vector3>>");
        public static readonly GameMember FilterCreateLine = Method(MapPlaces, Game, "CityMapFilters", "CreateLine", "CityMapFilterCategory", "String");

        // Core/OverlayGuard
        public static readonly GameMember OverlaySimple = Field(CardGuard, Game, "Player.HUD.ItemInfoOverlays.OverlayManager", "simpleOverlay", "SimpleOverlay");
        public static readonly GameMember OverlayDetailed = Field(CardGuard, Game, "Player.HUD.ItemInfoOverlays.OverlayManager", "detailedOverlay", "DetailedOverlay");

        // Core/RivalOfferGuard: a rival's open offer told from one that is over; without them only the employee is judged
        public static readonly GameMember OfferDeadline = Optional(Field(RivalOffers, Game, "Entities.EmployeeInstance", "poachDeadlineDays", "Int32"));
        public static readonly GameMember OfferDay = Optional(Field(RivalOffers, Game, "Entities.EmployeeInstance", "lastPoachDay", "Int32"));

        // Core/StaffLeave: which day an hours count is of; without it a count at a day's first hour is taken for the day before
        public static readonly GameMember LastWorkedDay = Optional(Field(StaffLeaving, Game, "Entities.EmployeeInstance", "lastWorkedDay", "Int32"));

        // Core/Keys: the player's bindings now; without them the game's defaults are warned about
        public static readonly GameMember PlayerInput = Optional(StaticField(KeyClash, Game, "Helpers.InputHelper", "playerInput", "PlayerInput"));
        public static readonly GameMember VehicleInput = Optional(StaticField(KeyClash, Plugins, "NWH.VehiclePhysics2.Input.InputSystemVehicleInputProvider", "vehicleInputActions", "VehicleInputActions"));

        // Core/ModState, Family, Travel, Meta/SaveTools: the game's register of mood types, the mod's own added to it
        public static readonly GameMember HappinessRegistry = StaticField(Moods, Game, "Helpers.HappinessHelper", "Modifiers", "Dictionary<String,HappinessModifier>");

        // Events/Weather
        public static readonly GameMember RainIntensity = StaticField(Thunder, Sky, "BigAmbitions.DayNightCycle.RainHelper", "NextRainMaxIntensity", "Single");

        // Home/HomeOffers, Ledger/LedgerPointer, Transport/TransitPointer: the game's list of what a click does
        public static readonly GameMember CtaBehaviours = StaticField(On(ClickHome, ClickLedger, ClickBusStop, ClickDesk, HotelRoom), Game, "Player.HUD.ItemInfoOverlays.CtaManager", "CtaBehaviors", "List<ICtaBehavior>");

        // Jobs/JobMop
        public static readonly GameMember MopClick = Optional(Method(Mop, Game, "MopController", "OnFloorCellClick", "Void", "DirtSpotObject"));   // else the cell under the player

        // Licences/Driving/TestCar; Photos/PhotosGame (without it, any hold while the camera is open is taken for its own)
        public static readonly GameMember NavTargets = Field(SchoolCar, Game, "EntityController", "navMeshTargets", "Transform[]");
        public static readonly GameMember NavBlockers = Field(On(SchoolCar, PhotoHolds), Game, "PlayerController", "_activeNavigationBlockers", "HashSet<NavigationBlocker>");

        // Photos/PhotosGame: the layers the game's mouse points at, held and put back by the Camera (else the game's defaults)
        public static readonly GameMember WorldMask = Optional(StaticField(PhotoHolds, Game, "MouseController", "RaycastLayerMark", "LayerMask"));

        // Meta/SaveTools ("yes" when it cannot be read)
        public static readonly GameMember CanSave = Optional(StaticMethod(SaveCheck, Game, "SaveGameManager", "CanSave", "Boolean"));

        // Social/Flare/FlareLiveGame: Steam's own text filter (else the mod's own word list, FlareFilter)
        public static readonly GameMember SteamFilterInit = Optional(StaticMethod(ChatFilter, Steam, "Steamworks.SteamUtils", "InitFilterText", "Boolean"));
        public static readonly GameMember SteamFilterText = Optional(StaticMethod(ChatFilter, Steam, "Steamworks.SteamUtils", "FilterText", "String", "TextFilteringContext", "SteamId", "String"));

        // People/Seating
        public static readonly GameMember BenchSitter = Field(BenchSitters, Game, "OutsideBenchController", "_npc", "BaseHuman");

        // Performance/PerfGame
        public static readonly GameMember LampLights = Field(StreetLamps, Game, "StreetLamp", "lights", "Light[]");
        public static readonly GameMember LampData = Field(StreetLamps, Game, "StreetLamp", "additionalLightDatas", "HDAdditionalLightData[]");
        public static readonly GameMember LampGlow = Field(StreetLamps, Game, "StreetLamp", "volumetricLightObject", "GameObject");
        public static readonly GameMember LampIsOn = Field(StreetLamps, Game, "StreetLamp", "_isOn", "Boolean");
        public static readonly GameMember CullList = Field(StreetLamps, Game, "Culling.CullingGroupController", "_cullables", "List<ICullable>");   // the lamps, from the game's culling list

        // Faults/FaultWorld: a lamp gone dark is given empty lists of lights to switch, and put back after
        public static readonly GameMember LampOutLights = Field(LampsOut, Game, "StreetLamp", "lights", "Light[]");
        public static readonly GameMember LampOutData = Field(LampsOut, Game, "StreetLamp", "additionalLightDatas", "HDAdditionalLightData[]");
        public static readonly GameMember LampOutGlow = Field(LampsOut, Game, "StreetLamp", "volumetricLightObject", "GameObject");
        public static readonly GameMember LampOutIsOn = Field(LampsOut, Game, "StreetLamp", "_isOn", "Boolean");
        public static readonly GameMember LampOutHead = Field(LampsOut, Game, "StreetLamp", "parentMeshRenderer", "MeshRenderer");
        public static readonly GameMember LampOutHeads = Field(LampsOut, Game, "StreetLamp", "childMeshRenderers", "MeshRenderer[]");
        public static readonly GameMember LampOutEmission = Field(LampsOut, Game, "StreetLamp", "emissionMaterialIndex", "Int32");

        // Performance/PerfProbe (Unity's interface)
        public static readonly GameMember LayoutQueue = Field(UiProbe, UnityUi, "UnityEngine.UI.CanvasUpdateRegistry", "m_LayoutRebuildQueue", null);
        public static readonly GameMember GraphicQueue = Field(UiProbe, UnityUi, "UnityEngine.UI.CanvasUpdateRegistry", "m_GraphicRebuildQueue", null);

        // Phone/Contacts
        public static readonly GameMember ContactIcons = Optional(StaticField(ContactPictures, Game, "UI.Smartphone.Apps.Contacts.ContactsApp", "PredefinedContactIconSprites", "Dictionary<String,Sprite>"));   // else from the next load
        public static readonly GameMember ContactsMarkAll = Field(ContactSearch, Game, "UI.Smartphone.Apps.Contacts.ContactsApp", "markAllAsReadButton", "Button");
        public static readonly GameMember TextInputField = Field(On(ContactSearch, KeyHidden), Game, "TextInputUI", "inputField", "TMP_InputField");   // also Texts/TextsGame.AskSecret

        // Phone/PhoneSkin
        public static readonly GameMember FrameData = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "frameData", "List<SmartPhoneFrame.FrameData>");
        public static readonly GameMember FrameImage = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "frameImage", "Image");
        public static readonly GameMember FrameMask = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "maskImage", "Image");

        // Ragdoll/Ragdoll
        public static readonly GameMember NearItemToHide = Method(BodyFade, Game, "HumanHider", "SetIsNearItemToHide", "Void", "Boolean");

        // Transport/BusZones: a parking lane's private settings, and the scenery it may switch off with a bus stop's kerb
        public static readonly GameMember LaneSpotCount = Field(BusStops, Game, "ParkingLaneGenerator", "spotCountOverride", "Int32");
        public static readonly GameMember LaneSideBySide = Field(BusStops, Game, "ParkingLaneGenerator", "sideBySideParkingSpots", "Boolean");
        public static readonly GameMember LaneCurved = Field(BusStops, Game, "ParkingLaneGenerator", "curvedLane", "Boolean");
        public static readonly GameMember LaneVehicles = Field(BusStops, Game, "ParkingLaneGenerator", "specificVehicles", "String[]");
        public static readonly GameMember LaneSpotDistance = Field(BusStops, Game, "ParkingLaneGenerator", "parkingSpotDistance", "Single");
        // Found by name on the scenery: a renamed one is no longer switched off with its kerb.
        public static readonly GameMember SceneryTree = Optional(TypeOf(BusStops, Game, "TreeController"));
        public static readonly GameMember SceneryMapHider = Optional(TypeOf(BusStops, Game, "CityMapHider"));
        public static readonly GameMember SceneryDirt = Optional(TypeOf(BusStops, Game, "DirtSpotObject"));
        public static readonly GameMember SceneryNavModifier = Optional(TypeOf(BusStops, "Unity.AI.Navigation", "Unity.AI.Navigation.NavMeshModifier"));
        public static readonly GameMember SceneryNavObstacle = Optional(TypeOf(BusStops, "UnityEngine.AIModule", "UnityEngine.AI.NavMeshObstacle"));
        public static readonly GameMember SceneryLight = Optional(TypeOf(BusStops, "Unity.RenderPipelines.HighDefinition.Runtime", "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData"));

        // Performance/Tweaks/FarDistricts: the game's components whose code switches renderers (where, in the game's
        // code); without every one the benchmark trial is not safe and stays off.
        public static readonly GameMember[] Switchers =
        {
            TypeOf(FarDistricts, Game, "CityBuildingController"),        // CityBuildingController.cs 241, 718
            TypeOf(FarDistricts, Game, "CityHamptonsHouseController"),   // CityHamptonsHouseController.cs 86-116
            TypeOf(FarDistricts, Game, "HamptonsHouse"),                 // HamptonsHouse.cs 121-252
            TypeOf(FarDistricts, Game, "BuildingSignController"),        // through CityBuildingController.cs 718
            TypeOf(FarDistricts, Game, "BuildingLogoSignController"),    // through CityBuildingController.cs 718
            TypeOf(FarDistricts, Game, "BillboardAd"),                   // BillboardAd.cs 101, 106
            TypeOf(FarDistricts, Game, "DirtSpotObject"),                // DirtSpotObject.cs 63, 79
            TypeOf(FarDistricts, Game, "GasStationTrigger"),             // GasStationTrigger.cs 39-49
            TypeOf(FarDistricts, Game, "Buildings.BuildingTypes.Special.PrivateDriverService.PrivateDriverGarageTrigger"),   // PrivateDriverGarageTrigger.cs 68-82
            TypeOf(FarDistricts, Game, "AutoParkSpot"),                  // AutoParkSpot.cs 14, 20; VehicleParkingHelper.cs 65-190
            TypeOf(FarDistricts, Game, "FenceDoor"),                     // FenceDoor.cs 352, 357
            TypeOf(FarDistricts, Game, "EntityController"),              // EntityController.cs 145, 160 (ViewBlockingEntity and its kin derive from it: ViewBlockingEntity.cs 248)
            TypeOf(FarDistricts, Game, "ItemController"),                // ItemController.cs 461, 475
            TypeOf(FarDistricts, Game, "BaseHuman"),                     // BaseHuman.cs 297; HumanHider.cs 117, 126
            TypeOf(FarDistricts, Game, "CarFeatures"),                   // CarFeatures.cs 114, 130
            TypeOf(FarDistricts, Game, "CarController"),                 // CarFeatures.cs 114, 130
            TypeOf(FarDistricts, Game, "Buildings.TaxiDestinationData"), // Buildings/TaxiDestinationData.cs 49
            TypeOf(FarDistricts, Game, "Controllers.CoatCheckController"),   // Controllers/CoatCheckController.cs 143
            TypeOf(FarDistricts, Game, "CarnivalPedestrian"),            // CarnivalPedestrian.cs 48; CarnivalPedestrianPool.cs 44
            TypeOf(FarDistricts, Game, "Controllers.AttractionViewBlockingEntity"),    // Controllers/AttractionViewBlockingEntity.cs 49
            TypeOf(FarDistricts, Game, "Controllers.FerrisWheelViewBlockingEntity"),   // Controllers/FerrisWheelViewBlockingEntity.cs 38-58
            TypeOf(FarDistricts, Game, "ParkingBuildingWithDifferentHeightsManager"),  // (22 writes)
            TypeOf(FarDistricts, Game, "PlayerActivity.GolfCourse"),     // PlayerActivity/GolfCourse.cs 232-498
            TypeOf(FarDistricts, Game, "SewerSteam"),                    // SewerSteam.cs 54, 59
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisPlayer"),
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisBall"),
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisScoreLine"),
            TypeOf(FarDistricts, Game, "PlayerActivity.WorkoutActivity"),
        };

        // Core/GameUi/InfoBox: the hover effect taken off a copied drop-down; Core/GameFurniture: a light kept on copied furniture
        public static readonly GameMember HoverEffect = Optional(TypeOf(null, Game, "UI.HoverEffect"));
        public static readonly GameMember HdLight = Optional(TypeOf(null, "Unity.RenderPipelines.HighDefinition.Runtime", "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData"));

        // Careers/SchoolDesk: the game's study activity, and the panel's current activity set to the mod's class
        public static readonly GameMember StudyDoor = Field(SchoolDeskPart, Game, "PlayerActivity.StudyActivity", "_educationDoor", "EntityController");
        public static readonly GameMember StudyPlace = Field(SchoolDeskPart, Game, "PlayerActivity.StudyActivity", "_studyDiploma", "StudyDiploma");
        public static readonly GameMember CurrentActivity = NeedsSetter(Prop(SchoolDeskPart, Game, "PlayerActivity.PlayerActivityUI", "GetCurrentActivity", "IPlayerActivity", false));

        // Careers/ComputerButtons: the button the game copies for its computer's Play a game and Pass the time
        public static readonly GameMember ComputerButtonTemplate = Field(ComputerClassPart, Game, "Player.HUD.ItemInfoOverlays.CustomizableButtonsOverlay", "buttonTemplate", "Button");

        // Core/GameFurniture: where the game seats people on an item
        public static readonly GameMember SittingPositions = Field(FurnitureSeats, Game, "ItemController", "sittingPositions", "Transform[]");

        // Core/SpecialBuildings: the building panel's headline, and the city map's search
        public static readonly GameMember BuildingHeadline = Field(SpecialHeadline, Game, "UI.CurrentBuilding.CurrentBuildingUI", "headlineLabel", "TextMeshProUGUI");
        public static readonly GameMember SearchBuildings = Field(SpecialSearch, Game, "CityMapFilters", "_searchableBuildings", "List<CityBuildingController>");
        public static readonly GameMember SearchDropdown = Field(SpecialSearch, Game, "CityMapFilters", "searchDropdown", "Dropdown");
        public static readonly GameMember DropdownOptions = Field(SpecialSearch, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember SearchPopulate = Method(SpecialSearch, Game, "CityMapFilters", "PopulateSearchDropdown", "Void", "Address");

        // Transport/BusRidePanel: the game's fast forward switch on its activity panel
        public static readonly GameMember FastForward = Field(RideToggle, Game, "PlayerActivity.PlayerActivityUI", "fastForwardToggle", "Toggle");

        // Core/CustomerDraw: the game's list of a day's customers at each business
        public static readonly GameMember CustomerTable = StaticField(On(CustomersKept, InteriorCustomers), Game, "AI.Customers.CustomerEntries.CustomerEntriesHelper", "BusinessCustomerEntries", "Dictionary<Address,List<CustomerEntry>>");

        // Interiors/InteriorTab: BizMan's business page, its tabs and their containers (without the details, no scroll of them)
        public static readonly GameMember BizManMenu = Field(InteriorTabPart, Game, "BizManBusiness", "menu", "Transform");
        public static readonly GameMember BizManContainers = Field(InteriorTabPart, Game, "BizManBusiness", "containers", "Transform");
        public static readonly GameMember BizManTabs = Field(InteriorTabPart, Game, "BizManBusiness", "_tabs", "List<String>");
        public static readonly GameMember BizManSetTab = Method(InteriorTabPart, Game, "BizManBusiness", "SetTab", "Void", "String");
        public static readonly GameMember BizManDetails = Optional(Field(InteriorTabPart, Game, "BizManBusiness", "businessInfo", "Transform"));

        // Transport/TransitBus: what a traffic car of the game's stands for (its sensor's list)
        public static readonly GameMember CarObstacles = Field(BusYield, Plugins, "GleyTrafficSystem.VehicleComponent", "obstacleList", "List<Collider>");

        // Traffic/TrafficMendGame: the traffic's parts a new density manager is made from, its link to them, and the game's
        // last traffic density (without that, the cars on the road are kept as the number wanted until the game sets it)
        public static readonly GameMember TrafficDensity = Field(TrafficMend, Plugins, "GleyTrafficSystem.TrafficManager", "densityManager", "DensityManager");
        public static readonly GameMember TrafficVehicles = Field(TrafficMend, Plugins, "GleyTrafficSystem.TrafficManager", "trafficVehicles", "TrafficVehicles");
        public static readonly GameMember TrafficPoints = Field(TrafficMend, Plugins, "GleyTrafficSystem.TrafficManager", "waypointManager", "WaypointManager");
        public static readonly GameMember TrafficCameras = Field(TrafficMend, Plugins, "GleyTrafficSystem.TrafficManager", "activeCameraPositions", "NativeArray<float3>");
        public static readonly GameMember TrafficSquares = Field(TrafficMend, Plugins, "GleyTrafficSystem.TrafficManager", "activeSquaresLevel", "Int32");
        public static readonly GameMember TrafficGrid = Field(TrafficMend, Plugins, "GleyUrbanAssets.UrbanManager", "gridManager", "GridManager");
        public static readonly GameMember DensityStart = Method(TrafficMend, Plugins, "GleyTrafficSystem.DensityManager", "Initialize", "DensityManager",
            "TrafficVehicles", "WaypointManager", "GridManager", "PositionValidator", "NativeArray<float3>", "Int32", "Vector3", "Vector3", "Int32");
        public static readonly GameMember DensityCount = Field(TrafficMend, Plugins, "GleyTrafficSystem.DensityManager", "currentnrOfVehicles", "Int32");
        public static readonly GameMember DensityGrid = Field(TrafficMend, Plugins, "GleyTrafficSystem.DensityManager", "gridManager", "GridManager");
        public static readonly GameMember LastTrafficDensity = Optional(Field(TrafficMend, Game, "TimeOfDayController", "_lastTrafficDensity", "Int32"));

        // Careers/CampusMarks: the game's icons over items, lent to the university's classroom doors
        public static readonly GameMember IconPool = Field(ClassroomMarks, Game, "Player.HUD.ItemWarningIcons.ItemWarningIconManager", "_iconPool", "ObjectPool<ItemWarningIcon>");
        public static readonly GameMember IconsActive = Field(ClassroomMarks, Game, "Player.HUD.ItemWarningIcons.ItemWarningIconManager", "_activeIcons", "Dictionary<Int32,ItemWarningIcon>");
        public static readonly GameMember IconVisuals = Method(ClassroomMarks, Game, "Player.HUD.ItemWarningIcons.ItemWarningIconManager", "GetIconVisuals", "ValueTuple<Sprite,Sprite,Boolean>", "WarningIconType");

        // Careers/PersonaEducation: the Persona app's education box and its rows
        public static readonly GameMember EducationTemplate = Field(PersonaCourses, Game, "UI.Smartphone.Apps.Persona.EducationInfo", "educationEntryTemplate", "EducationInfoEntry");
        public static readonly GameMember EducationHeader = Field(PersonaCourses, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "headerLabel", "TextLocalizationComponent");
        public static readonly GameMember EducationStatus = Field(PersonaCourses, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "statusLabel", "TextLocalizationComponent");
        public static readonly GameMember EducationCheck = Field(PersonaCourses, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "checkmarkObj", "GameObject");

        // Tech/Firms/StartCards: BizMan's start screen, the cards it makes for the kinds of business, and each card's competitors
        public static readonly GameMember StartCardsHolder = Field(StartCompetitors, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessUI", "businessesContainer", "Transform");
        public static readonly GameMember StartCardType = Field(StartCompetitors, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "_businessType", "BusinessType");
        public static readonly GameMember StartCardLabel = Field(StartCompetitors, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "competitorsLabel", "TextLocalizationComponent");
        public static readonly GameMember StartCardAmount = Field(StartCompetitors, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "competitorsAmountLabel", "TMP_Text");

        // Core/FrontSigns: the game's wide sign types and its logo sign's face, for the signs on the mod's buildings
        public static readonly GameMember SignTypes = Field(FrontSignsPart, Game, "BuildingSignController", "wideSignTypes", "WideSignTypes");
        public static readonly GameMember LogoFace = Field(FrontSignsPart, Game, "BuildingLogoSignController", "logoSignRenderer", "Renderer");

        // Core/ClaimedMap: the step of the city map's look at its buildings, as the game declares it (read on the closed type)
        public static readonly GameMember MapLookStep = Field(ClaimedRows, Game, "Helpers.DistributedWork`1", "_updateAction", "Action<T>");

        // Character/HairTests: a lit street lamp's brightness (else the test lamp keeps its own)
        public static readonly GameMember LampMaxValue = Optional(Field(HairLampTest, Game, "StreetLamp", "maxValue", "Single"));

        // Members an older game lacks, which the code names directly but only in small methods never inlined, called in a
        // try (Core/GameMembers, GameConfirm, GameBuild, GameUi/PhoneApps; ContractCheck holds them to it). Resolved here
        // so that on such a game the way without is taken before anything asks.
        public static readonly GameMember PlayerDead = Optional(NeedsSetter(Prop(PlayerDeath, Game, "Helpers.FuneralHelper", "PlayerDead", "Boolean", true)));
        public static readonly GameMember FuneralActive = Optional(Prop(PlayerDeath, Game, "Helpers.FuneralHelper", "IsFuneralActive", "Boolean", true));
        public static readonly GameMember FenceIndex = Optional(Field(PrivateFences, Game, "Buildings.Building", "locatedInsidePrivateFenceIndex", "Int32"));
        public static readonly GameMember ResumeTarget = Prop(CardTarget, Game, "UI.InGameUI.BuildingResume", "TargetController", "EntityController", false);
        public static readonly GameMember StationNameKey = Optional(Method(StationNames, Game, "SubwayStation", "GetNameKey", "String"));
        public static readonly GameMember StationName = Optional(Field(StationNames, Game, "SubwayStation", "stationName", "SubwayStationName"));
        public static readonly GameMember ConfirmByKey = Optional(StaticMethod(QuestionBox, Game, "HudConfirm", "Show", "Void",
            "String", "String", "Action", "Action", "String", "String", "Boolean", "Boolean"));
        public static readonly GameMember ConfirmByData = Optional(StaticMethod(QuestionBox, Game, "HudConfirm", "Show", "Void",
            "LanguageChangeEventDataHolder", "LanguageChangeEventDataHolder", "Action", "Action", "String", "String", "Boolean", "Boolean"));
        public static readonly GameMember ConfirmOpen = Optional(StaticField(QuestionBox, Game, "HudConfirm", "isOpen", "Boolean"));
        public static readonly GameMember ConfirmListening = Optional(StaticField(QuestionBox, Game, "HudConfirm", "onShow",
            "Action<LanguageChangeEventDataHolder,LanguageChangeEventDataHolder,String,String,Action,Action,Boolean>"));
        public static readonly GameMember VersionCurrent = Optional(StaticMethod(BuildRead, Game, "GameVersion", "GetCurrent", "GameVersion"));
        public static readonly GameMember VersionBuild = Optional(Field(BuildRead, Game, "GameVersion", "buildNumber", "Int32"));
        public static readonly GameMember MenuStartView = Optional(Field(BuildRead, Game, "MainMenuController", "startView", "GameObject"));
        public static readonly GameMember PhoneOutlineOff = Optional(Method(AppOpening, Game, "SmartphoneUI", "StopOutliningCurrentApp", "Void"));
        public static readonly GameMember UisResume = Optional(Field(AppOpening, Game, "UI.UIs", "buildingResume", "BuildingResume"));
        public static readonly GameMember ResumeClose = Optional(Method(AppOpening, Game, "UI.InGameUI.BuildingResume", "Close", "Void"));
        public static readonly GameMember MenuBizMan = Optional(Field(AppOpening, Game, "UI.Smartphone.FullMenu", "bizMan", "BizMan"));
        public static readonly GameMember BizManBusinessPage = Optional(Field(AppOpening, Game, "UI.Smartphone.Apps.BizMan.BizMan", "business", "BizManBusiness"));
        public static readonly GameMember BizManSettingsPage = Optional(Field(AppOpening, Game, "BizManBusiness", "bizManSettings", "BizManSettings"));
        public static readonly GameMember BizManRegistration = Optional(Field(AppOpening, Game, "BizManBusiness", "buildingRegistration", "BuildingRegistration"));
        public static readonly GameMember SettingsUnsaved = Optional(Prop(AppOpening, Game, "BizManSettings", "HasUnsavedChanges", "Boolean", false));
        public static readonly GameMember RegistrationName = Optional(Field(AppOpening, Game, "BuildingRegistration", "BusinessName", "String"));
        public static readonly GameMember NoticeError = Optional(StaticMethod(AppOpening, Game, "UI.Notification.Notifications", "ShowError", "Void", "String", "String", "Boolean"));

        // Garage/GarageWash: what the game's wash plays and shows, given a stand-in on a car that has none
        public static readonly GameMember WashSpray = Optional(Field(CarWash, Game, "CarController", "washVfx", "GameObject"));
        public static readonly GameMember WashSound = Optional(Field(CarWash, Game, "CarController", "washAudioSource", "AudioSource"));

        /// <summary>The five pieces Build 3675 lacks (the funeral helper's two properties are one, its type).</summary>
        public static readonly GameMember[] LackedBy3675 = { PlayerDead, FuneralActive, FenceIndex, ResumeTarget, StationNameKey, ConfirmByKey };

        /// <summary>
        /// The game's layers the mod names. They are project settings, not in the assemblies, so ContractCheck cannot read
        /// them: Core/Guard looks each up at start and names any that is gone.
        /// </summary>
        public static readonly string[] Layers =
        {
            "AiVehicles", "AutoParkSpots", "BuildingWalls", "Buildings", "BuildingsOutlined", "Crosswalk", "Default", "Ground", "GroundUnplacable", "Human",
            "Ignore Raycast", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles", "ParkingArea", "Player", "PlayerVehicles", "Roads",
            "StreetProps", "TransparentFX", "UI", "Vehicles", "Water",
        };

        private static GamePart[] On(params GamePart[] parts)
        {
            return parts;
        }

        private static GamePart[] One(GamePart part)
        {
            return part != null ? new[] { part } : new GamePart[0];
        }

        private static GameMember Field(GamePart part, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, false, valueType, null, false, One(part));
        }

        private static GameMember Field(GamePart[] parts, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, false, valueType, null, false, parts);
        }

        private static GameMember StaticField(GamePart part, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, true, valueType, null, false, One(part));
        }

        private static GameMember StaticField(GamePart[] parts, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, true, valueType, null, false, parts);
        }

        private static GameMember Prop(GamePart part, string assembly, string type, string name, string valueType, bool isStatic)
        {
            return new GameMember(GameMemberKind.Property, assembly, type, name, isStatic, valueType, null, false, One(part));
        }

        private static GameMember Method(GamePart part, string assembly, string type, string name, string returns, params string[] parameters)
        {
            return new GameMember(GameMemberKind.Method, assembly, type, name, false, returns, parameters, false, One(part));
        }

        private static GameMember StaticMethod(GamePart part, string assembly, string type, string name, string returns, params string[] parameters)
        {
            return new GameMember(GameMemberKind.Method, assembly, type, name, true, returns, parameters, false, One(part));
        }

        private static GameMember TypeOf(GamePart part, string assembly, string type)
        {
            return new GameMember(GameMemberKind.Type, assembly, type, null, false, null, null, false, One(part));
        }

        private static GameMember Optional(GameMember member)
        {
            member.MakeOptional();
            return member;
        }

        private static GameMember NeedsSetter(GameMember member)
        {
            member.NeedsSetter = true;
            return member;
        }
    }
}
