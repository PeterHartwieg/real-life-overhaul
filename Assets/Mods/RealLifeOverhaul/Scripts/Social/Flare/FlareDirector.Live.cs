using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare as a living network (FlareLiveRules): the city's posts, reactions, the City Office's cases, invitations,
    /// messages, stories and the badge. Cost: nothing per frame; the city's people once a seed (0.2 ms), the day a few
    /// dozen posts and one pass over the buildings, the hour the cases due and the badge.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly List<FlareCitizen> _citizens = new List<FlareCitizen>(FlareLiveRules.Citizens);
        private readonly FlareCityFacts _facts = new FlareCityFacts();
        private readonly List<FlarePost> _made = new List<FlarePost>(32);
        private readonly List<FlareCase> _due = new List<FlareCase>(8);
        private readonly List<string> _staff = new List<string>(8);
        private readonly List<string> _tycoons = new List<string>(4);              // names, for the accounts called on
        private readonly List<FlareTycoon> _tycoonsIn = new List<FlareTycoon>(4);
        private readonly List<FlareReaction> _reactions = new List<FlareReaction>(16);
        private readonly List<FlareNotice> _notices = new List<FlareNotice>(64);
        private readonly List<string> _myPlaces = new List<string>(8);
        private readonly StringBuilder _line = new StringBuilder(256);
        private int _citizensSeed;
        private int _unread;
        private int _caseNoteDay = -1, _caseNotes;
        private int _hiringDay = -100;

        public const int NoticesMost = 60;
        private const int CaseNotesPerDay = 3;     // notifications of an answer to the player's report, a day at most

        public IReadOnlyList<FlareCitizen> Citizens => _citizens;

        public long Now => Save != null ? FlareLiveRules.Now(Save.Day, Save.Hour, FlareGame.MinuteOfDay % 60) : 0;

        /// <summary>The player as a post's mention names them: "me" alone, "p:" and their id in a session.</summary>
        public string MeKey => GoingPublic.InSession && GoingPublicLink.MyId.Length > 0 ? "p:" + GoingPublicLink.MyId : "me";

        public string MyName
        {
            get
            {
                string name = FlareBridges.PlayerName != null ? FlareBridges.Ask(FlareBridges.PlayerName, null) : null;
                return string.IsNullOrEmpty(name) ? FlareText.T("reallife_flare_you") : name;
            }
        }

        public string MyHandle => FlareText.HandleOf(MyName);

        /// <summary>Some of the city's businesses for a review's choice: the day's sample, made now if the day has not made it.</summary>
        public IReadOnlyList<FlareBiz> CityShops
        {
            get
            {
                if (_facts.Shops.Count == 0 && Running)
                {
                    FlareLiveGame.CityShops(_facts.Shops);
                }
                return _facts.Shops;
            }
        }

        public int CurrentDay => Save != null ? Save.Day : 0;

        /// <summary>Notifications and messages not yet seen: the badge.</summary>
        public int Unread => _unread;

        private void LiveStart(FlareFile file)
        {
            if (file.Net.CitySeed == 0)
            {
                file.Net.CitySeed = file.CastSeed != 0 ? file.CastSeed : file.Seed;
            }
            EnsureCitizens(file);
        }

        // The city's people for the seed in use: the host's while a guest follows it.
        private void EnsureCitizens(FlareFile file)
        {
            int seed = FlareLiveRules.CitySeed(file);
            if (seed == _citizensSeed && _citizens.Count > 0)
            {
                FlareBridges.CitizensChanged?.Invoke(seed, _citizens);
                return;
            }
            FlareGame.Districts(_districts);
            FlareLiveRules.CitizensOf(seed, _districts, _citizens);
            _citizensSeed = seed;
            FlareBridges.CitizensChanged?.Invoke(seed, _citizens);
        }

        /// <summary>True while this machine is a guest that follows the host's feed: its city posts and City Office are the host's.</summary>
        public bool Following => SharedCity.Follows(FlareNetShare.Part);

        // After the account's own daily round: the watches, the city's posts of the day, the city's people's message.
        private void LiveDay(int day)
        {
            EnsureCitizens(_file);
            if (FlareLiveRules.WatchDay(_file.Net, day))
            {
                ShareWatchesChanged();
            }
            for (int i = _file.Net.Stories.Count - 1; i >= 0; i--)
            {
                if (day - _file.Net.Stories[i].Day > 3)
                {
                    _file.Net.Stories.RemoveAt(i);   // a piece for a paper that has come and gone
                }
            }
            for (int i = _file.Net.Going.Count - 1; i >= 0; i--)
            {
                FlarePost invite = _file.Post(_file.Net.Going[i]);
                if (invite == null || invite.When < day * 24 - 24)
                {
                    _file.Net.Going.RemoveAt(i);
                }
            }
            ReadFacts(day);
            bool newDay = _file.Net.CityDay != day;
            int made = FlareLiveRules.CityDay(_file, _facts, _citizens, _made);
            for (int i = 0; i < _made.Count; i++)
            {
                CityMade(_made[i]);
            }
            if (newDay)
            {
                Tycoons(day);
            }
            CityDm(day);
            if (made > 0)
            {
                Logger?.Info("Flare: the city's posts of day " + day + ": " + made + " (" + _made.Count + " reports and reviews among them), at its own minute each; the feed's busyness "
                             + _facts.Busy + ".");
                ShareChanged(null);
            }
            Recount();
            TextsAhead();   // the day's posts: their words asked now, each by its own minute
        }

        private void ReadFacts(int day)
        {
            FlareCityFacts f = _facts;
            f.Day = day;
            f.Rain = f.Rained = false;
            f.News = _file.NewsDay >= day - 1 ? _file.NewsToday : 0;
            f.NewsDistrict = f.EventToday = f.EventTomorrow = f.SiteDistrict = f.Policy = f.Mover = null;
            f.EventTodayFrom = f.EventTomorrowFrom = -1;
            f.PriceTrend = f.LateSites = f.MoverBp = f.RateMove = 0;
            f.Election = f.LitterPolicy = false;
            f.Candidates.Clear();
            f.Busy = FlareRules.Clamp(Settings.FlareBusy, 0, 2);
            f.Following = Following;
            f.RealFaults = FaultsReal;
            FlareGame.Districts(f.Districts);
            f.Mine.Clear();
            _myPlaces.Clear();
            List<BuildingRegistration> mine = FlareGame.Businesses();
            for (int i = 0; i < mine.Count; i++)
            {
                f.Mine.Add(FlareLiveGame.Biz(mine[i], true));
                _myPlaces.Add(FlareGame.Place(mine[i]));
            }
            if (!f.Following)
            {
                FlareLiveGame.CityShops(f.Shops);
            }
            else
            {
                f.Shops.Clear();
            }
            if (_moverName != null && _moverDay >= day - 1)
            {
                f.Mover = _moverName;
                f.MoverBp = _moverBp;
            }
            try
            {
                FlareBridges.CityFacts?.Invoke(f);
            }
            catch (Exception e)
            {
                Logger?.Warn("Flare: the city's facts for the day could not all be read (" + e.Message + "); the posts go by what Flare knows.");
            }
        }

        // A city person's report or review as it goes up: a report is a case, a review moves its business's customers.
        private void CityMade(FlarePost p)
        {
            if (p.Source == FlareSource.Report)
            {
                FileCase(p, "c" + p.Who, 0f, 0f);
            }
            else if (p.Source == FlareSource.Review)
            {
                BusinessEffect(p, false);
            }
        }

        // The rival tycoons of the end game: one posts in their own voice now and then, about their latest move if any.
        private void Tycoons(int day)
        {
            ReadTycoons();
            if (Following)
            {
                return;
            }
            FlarePost p = FlareLiveRules.TycoonPost(_file, _tycoonsIn, day);
            if (p != null)
            {
                _file.Posts.Add(p);
                Logger?.Info("Flare: " + p.Name + " posts on day " + day + (p.Arg >= FlareLiveRules.TycoonMoveArg ? " about their move (" + (p.Arg - FlareLiveRules.TycoonMoveArg) + ")." : "."));
            }
        }

        private void ReadTycoons()
        {
            _tycoonsIn.Clear();
            _tycoons.Clear();
            try
            {
                FlareBridges.Tycoons?.Invoke(_tycoonsIn);
            }
            catch (Exception e)
            {
                Logger?.Warn("Flare: the rival tycoons could not be read (" + e.Message + ").");
                _tycoonsIn.Clear();
            }
            for (int i = 0; i < _tycoonsIn.Count; i++)
            {
                _tycoons.Add(_tycoonsIn[i].Name);
            }
        }

        private int TycoonVoice(string name)
        {
            for (int i = 0; i < _tycoonsIn.Count; i++)
            {
                if (_tycoonsIn[i].Name == name)
                {
                    return _tycoonsIn[i].Index;
                }
            }
            return -1;
        }

        private void CityDm(int day)
        {
            bool hiring = day - _hiringDay <= 7 || FlareBridges.Ask(FlareBridges.Hiring, false);
            string with, what;
            if (!FlareLiveRules.CityDm(_file, day, hiring, out with, out what))
            {
                return;
            }
            long at = FlareLiveRules.Now(day, 10 + FlareRules.Roll(_file.Seed, day, 842) % 10, FlareRules.Roll(_file.Seed, day, 843) % 60);
            string name = AccountName(with);
            FlareLiveRules.Dm(_file.Net, with, name, at, false, null, FlareText.DmKey(what), null);
            Logger?.Info("Flare: " + name + " writes to the player (" + what + ") on day " + day + ".");
        }

        private void LiveHour(int day, int hour)
        {
            long now = FlareLiveRules.Now(day, hour, 0);
            if (!Following)
            {
                FlareLiveRules.Due(_file.Net, now + 59, _due);   // the cases of this hour: the answer is dated at its own minute
                for (int i = 0; i < _due.Count; i++)
                {
                    Answer(_due[i], day);
                }
                Works(day);
            }
            Invitations(day, hour);
            Watched(day);
            Recount();
            TextsAhead();   // the hour's answers and replies: their words asked now
        }

        private void Watched(int day)
        {
            if (FlareBridges.Watched == null)
            {
                return;
            }
            string here = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            bool patrol = here != null && _file.Net.WatchIn(here, day, FlareOrder.Patrol) != FlareOrder.None;
            bool clean = here != null && _file.Net.WatchIn(here, day, FlareOrder.Cleanup) != FlareOrder.None;
            FlareBridges.Watched(patrol, clean);
        }

        // Works the City Office sent: done once the council's site is handed over.
        private void Works(int day)
        {
            for (int i = 0; i < _file.Net.Cases.Count; i++)
            {
                FlareCase c = _file.Net.Cases[i];
                if (c.State == FlareCaseState.Acting && c.Order == FlareOrder.Works && c.Ref >= 0 && FlareBridges.WorksDone != null && FlareBridges.WorksDone(c.Ref))
                {
                    c.State = FlareCaseState.Done;
                    Logger?.Info("Flare: the City Office's works for case " + c.Id + " (" + c.Kind + " in " + FlareGame.DistrictName(c.District) + ") are done.");
                    FlareBridges.FaultDone?.Invoke(c.Id);   // a real fault the works were for is mended
                    ShareCaseChanged(c);
                }
            }
        }

        // An invitation the player goes to: a notification as it starts, with the way there.
        private void Invitations(int day, int hour)
        {
            int at = day * 24 + hour;
            for (int i = 0; i < _file.Net.Going.Count; i++)
            {
                FlarePost p = _file.Post(_file.Net.Going[i]);
                if (p != null && p.When == at && Settings.SocialMediaNotices)
                {
                    string place = p.Place;
                    Tell(NotificationType.Info, "reallife_flare_note_invite_now", () => GoTo(place), "event", FlareText.InviteName(p));
                }
            }
        }

        private static void GoTo(string place)
        {
            if (place != null)
            {
                FlareBridges.Destination?.Invoke(place);
            }
            else
            {
                FlareBridges.Ask(FlareBridges.EventDestination, false);
            }
        }

        /// <summary>An account's name as shown, by its key ("c12", "o:0", "i:3", "b:name", "k:name", "p:player", "f3", "me").</summary>
        public string AccountName(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "?";
            }
            if (key == "me")
            {
                return MyName;
            }
            int n;
            switch (key[0])
            {
                case 'c':
                    return int.TryParse(key.Substring(1), out n) && n >= 0 && n < _citizens.Count ? _citizens[n].First + " " + _citizens[n].Last : "?";
                case 'f':
                case 'p' when key.Length > 1 && key[1] != ':':
                    return int.TryParse(key.Substring(1), out n) ? PersonName(n) ?? "?" : "?";
                case 'o':
                    return int.TryParse(key.Substring(2), out n) ? FlareText.OfficialName(n) : "?";
                case 'i':
                    FlareInfluencer inf = _file != null && int.TryParse(key.Substring(2), out n) ? _file.Influencer(n) : null;
                    return inf != null ? inf.Handle : "?";
                case 's':
                    return key.Length > 2 ? key.Substring(2) : "?";
                case 'b':
                    return key.Length == 1 ? FlareText.T("reallife_flare_your_boss") : key.Substring(2);
                case 'l':
                    return FlareText.T("reallife_flare_ledger_name");
                default:
                    return key.Length > 2 ? key.Substring(2) : key;
            }
        }

        public void OpenPost(int id)
        {
            if (_app != null && _view != null && Running)
            {
                _view.WantPost(id);
                _app.Open();
            }
        }

        /// <summary>The camera button: the phone's own camera, or with the Camera off a shot of the screen (FlareDirector.Camera.cs).</summary>
        public void CameraPressed()
        {
            if (FlareBridges.OpenCamera == null || !FlareBridges.OpenCamera())
            {
                TakeShot();
            }
        }

        /// <summary>The selfie button: the phone is put away for a selfie in photo mode, which comes back to the compose sheet.</summary>
        public void SelfiePressed()
        {
            if (FlareBridges.TakeSelfie == null)
            {
                return;
            }
            _awaitSelfie = UnityEngine.Time.unscaledTime + 600f;
            FlareLiveGame.CloseMenu();
            FlareBridges.TakeSelfie();
        }

        private float _awaitSelfie = -1f;
    }
}
