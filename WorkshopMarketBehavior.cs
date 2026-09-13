using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class TradeWar
    {
        public TradeWar() {}
        [SaveableField(1)] public Hero Aggressor;
        [SaveableField(2)] public Kingdom TargetFaction;
        [SaveableField(3)] public CampaignTime StartTime;
        [SaveableField(4)] public int AggressorScore;
        [SaveableField(5)] public int TargetScore;
        
        private List<Hero> _coalitionMembers = new List<Hero>();
        [SaveableProperty(6)]
        public List<Hero> CoalitionMembers { get { return _coalitionMembers ?? (_coalitionMembers = new List<Hero>()); } set { _coalitionMembers = value; } }

        public TradeWar(Hero aggressor, Kingdom targetFaction)
        {
            Aggressor = aggressor;
            TargetFaction = targetFaction;
            StartTime = CampaignTime.Now;
            AggressorScore = 0;
            TargetScore = 0;
        }
    }


    /// <summary>
    /// Workshop pazar dinamikleri, ihale sistemi, oyuncu arasý rekabet vb.
    /// </summary>
    public class WorkshopMarketBehavior : CampaignBehaviorBase
    {
        // Workshop baþýna piyasa çarpaný (1.0 = normal, 1.5 = boom, 0.7 = bust)
        private Dictionary<string, float> _workshopModifiers = new Dictionary<string, float>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, float> WorkshopModifiers { get { return _workshopModifiers ?? (_workshopModifiers = new Dictionary<string, float>()); } set { _workshopModifiers = value; } }

        // Çarpan bitiþ zamanlarý (workshop StringId -> expiry)
        private Dictionary<string, CampaignTime> _modifierExpiry = new Dictionary<string, CampaignTime>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, CampaignTime> ModifierExpiry { get { return _modifierExpiry ?? (_modifierExpiry = new Dictionary<string, CampaignTime>()); } set { _modifierExpiry = value; } }

        // Aktif ihale teklifleri (settlement StringId -> ihale oluþturma zamaný)
        private Dictionary<string, CampaignTime> _activeTenders = new Dictionary<string, CampaignTime>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, CampaignTime> ActiveTenders { get { return _activeTenders ?? (_activeTenders = new Dictionary<string, CampaignTime>()); } set { _activeTenders = value; } }

        // Ýhale fiyatlarý (settlement StringId -> fiyat)
        private Dictionary<string, int> _tenderPrices = new Dictionary<string, int>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, int> TenderPrices { get { return _tenderPrices ?? (_tenderPrices = new Dictionary<string, int>()); } set { _tenderPrices = value; } }

        // Ýhale hangi workshop'u satýyor (settlement StringId -> workshop tag)
        private Dictionary<string, int> _tenderWorkshopIndex = new Dictionary<string, int>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, int> TenderWorkshopIndex { get { return _tenderWorkshopIndex ?? (_tenderWorkshopIndex = new Dictionary<string, int>()); } set { _tenderWorkshopIndex = value; } }

        // Son ihale üretimi
        // (SaveableField/Property removed - handled via SyncData)
        private CampaignTime _lastTenderTime = CampaignTime.Never;

        // Son NPC teklifi
        // (SaveableField/Property removed - handled via SyncData)
        private CampaignTime _lastNpcOfferTime = CampaignTime.Never;

        // Ambargo + fahiþ fiyat eþ zamanlý baþlangýcý
        // (SaveableField/Property removed - handled via SyncData)
        private CampaignTime _extremePolicyStart = CampaignTime.Never;

        // Aktif politikalar (settlement StringId -> policy adý)
        private Dictionary<string, string> _activePolicies = new Dictionary<string, string>();
        // (SaveableField/Property removed - handled via SyncData)
        public Dictionary<string, string> ActivePolicies { get { return _activePolicies ?? (_activePolicies = new Dictionary<string, string>()); } set { _activePolicies = value; } }

        // Koalisyon uyarýsý gönderildi mi?
        // (SaveableField/Property removed - handled via SyncData)
        private bool _coalitionWarningSent = false;

        // (SaveableField/Property removed - handled via SyncData)
        private bool _hasDistributedInitialWorkshops = false;

        // (SaveableField/Property removed - handled via SyncData)
        private int _townDistributionIndex = 0;


        // YENÝ: Aktif Ticaret Savaþlarý (Ambargolar)
        private List<TradeWar> _activeTradeWars = new List<TradeWar>();
        // (SaveableField/Property removed - handled via SyncData)
        public List<TradeWar> ActiveTradeWars { get { return _activeTradeWars ?? (_activeTradeWars = new List<TradeWar>()); } set { _activeTradeWars = value; } }

        public void AddTradeWar(Hero aggressor, Kingdom targetFaction)
        {
            if (!_activeTradeWars.Any(w => w.Aggressor == aggressor && w.TargetFaction == targetFaction))
            {
                _activeTradeWars.Add(new TradeWar(aggressor, targetFaction));
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.BarterablesRequested.AddNonSerializedListener(this, OnBarterablesRequested);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }



        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in WorkshopModifiers.ToList()) { if (!(pair.Key != null)) WorkshopModifiers.Remove(pair.Key); }
                foreach (var pair in ModifierExpiry.ToList()) { if (!(pair.Key != null)) ModifierExpiry.Remove(pair.Key); }
                foreach (var pair in ActiveTenders.ToList()) { if (!(pair.Key != null)) ActiveTenders.Remove(pair.Key); }
                foreach (var pair in TenderPrices.ToList()) { if (!(pair.Key != null)) TenderPrices.Remove(pair.Key); }
                foreach (var pair in TenderWorkshopIndex.ToList()) { if (!(pair.Key != null)) TenderWorkshopIndex.Remove(pair.Key); }
                foreach (var pair in ActivePolicies.ToList()) { if (!(pair.Key != null && pair.Value != null)) ActivePolicies.Remove(pair.Key); }
                var tradeWars = ActiveTradeWars;
                // tradeWars.RemoveAll(x => x == null);
            }

            dataStore.SyncData("_workshopModifiers",      ref _workshopModifiers);
            dataStore.SyncData("_modifierExpiry",         ref _modifierExpiry);
            dataStore.SyncData("_activeTenders",          ref _activeTenders);
            dataStore.SyncData("_tenderPrices",           ref _tenderPrices);
            dataStore.SyncData("_tenderWorkshopIndex",    ref _tenderWorkshopIndex);
            dataStore.SyncData("_lastTenderTime",         ref _lastTenderTime);
            dataStore.SyncData("_lastNpcOfferTime",       ref _lastNpcOfferTime);
            dataStore.SyncData("_extremePolicyStart",     ref _extremePolicyStart);
            dataStore.SyncData("_activePolicies",         ref _activePolicies);
            dataStore.SyncData("_coalitionWarningSent",   ref _coalitionWarningSent);
            dataStore.SyncData("_hasDistributedInitialWorkshops", ref _hasDistributedInitialWorkshops);
            dataStore.SyncData("_townDistributionIndex",  ref _townDistributionIndex);

            dataStore.SyncData("_activeTradeWars",        ref _activeTradeWars);

            if (_workshopModifiers   == null) _workshopModifiers   = new Dictionary<string, float>();
            if (_modifierExpiry      == null) _modifierExpiry      = new Dictionary<string, CampaignTime>();
            if (_activeTenders       == null) _activeTenders       = new Dictionary<string, CampaignTime>();
            if (_tenderPrices        == null) _tenderPrices        = new Dictionary<string, int>();
            if (_tenderWorkshopIndex == null) _tenderWorkshopIndex = new Dictionary<string, int>();
            if (_activePolicies      == null) _activePolicies      = new Dictionary<string, string>();

            if (_activeTradeWars     == null) _activeTradeWars     = new List<TradeWar>();
        }


        // ------------------------------------------------------------------ //
        // Yardýmcý: workshop benzersiz anahtarý
        // ------------------------------------------------------------------ //
        private static string Key(Workshop w) => w.Settlement.StringId + "_" + w.Tag;


        // ------------------------------------------------------------------ //
        // Public API: piyasa çarpaný sorgulama
        // ------------------------------------------------------------------ //
        public float GetModifier(Workshop w)
        {
            string key = Key(w);
            if (_modifierExpiry.TryGetValue(key, out var expiry) && expiry.IsPast)
            {
                _workshopModifiers.Remove(key);
                _modifierExpiry.Remove(key);
            }
            return _workshopModifiers.TryGetValue(key, out float val) ? val : 1f;
        }

        // ------------------------------------------------------------------ //
        // Session baþlangýcý — menüler
        // ------------------------------------------------------------------ //
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 1. Ana Menüye "{=rad_wk_07}Workshop Operations" butonu ekle
            starter.AddGameMenuOption("town", "town_workshop_actions", "{=rad_wk_07}Workshop Operations",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown;
                },
                args =>
                {
                    GameMenu.SwitchToMenu("town_workshop_menu"); }, false, 7);

            // 2. Alt Menüyü Oluþtur
            starter.AddGameMenu("town_workshop_menu", "{=rad_wk_08}You can manage industrial and commercial activities in the city from here.", args => { });


            // Ýhale menüsü -> Alt menüye
            starter.AddGameMenuOption("town_workshop_menu", "workshop_tender", "{=rad_wm_05}See Workshop Tenders",
                args =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    return _activeTenders.ContainsKey(Settlement.CurrentSettlement.StringId);
                },
                args =>
                {
                    var settlement = Settlement.CurrentSettlement;
                    if (!_tenderPrices.TryGetValue(settlement.StringId, out int price)) return;
                    if (!_tenderWorkshopIndex.TryGetValue(settlement.StringId, out int idx)) return;

                    var workshops = settlement.Town.Workshops;
                    if (idx >= workshops.Length) return;

                    var targetWs = workshops[idx];
                    
                    // Ýhale her zaman fiyatýn %50'sinden baþlar
                    int startingBid = (int)(price * 0.5f);
                    
                    StartInteractiveAuction(settlement, targetWs, startingBid);
                });

            // Fiyat Politikasý / Ambargo menüsü
            starter.AddGameMenuOption("town_workshop_menu", "price_policy", "{=rad_auto_338}Set Price Policy",
                args =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    var town = Settlement.CurrentSettlement;
                    int total = town.Town.Workshops.Length;
                    int ours  = town.Town.Workshops.Count(w => w.Owner == Hero.MainHero);
                    return total > 0 && ours * 2 > total; // >50%
                },
                args =>
                {
                    var settlement = Settlement.CurrentSettlement;
                    var elements = new List<InquiryElement>
                    {
                        new InquiryElement("damping",   new TaleWorlds.Localization.TextObject("{=rad_auto_343}Price Dropping (Dumping) — 500 Influence").ToString(),          null, Hero.MainHero.Clan.Influence >= 500, "Competing workshops incur losses"),
                        new InquiryElement("gouging",   new TaleWorlds.Localization.TextObject("{=rad_auto_344}Exorbitant Pricing (+50% Revenue)").ToString(),            null, true,                                 "City loyalty decreases by -2 every day"),
                        new InquiryElement("embargo",   new TaleWorlds.Localization.TextObject("{=rad_auto_345}Request Embargo Permission — 300 Influence").ToString(),               null, Hero.MainHero.Clan.Influence >= 300, "For an embargo, you must get permission from the king or the parliament before the war."),
                        new InquiryElement("lift",      new TaleWorlds.Localization.TextObject("{=rad_auto_346}Remove Active Policy").ToString(),                     null, _activePolicies.ContainsKey(settlement.StringId), "")
                    };

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_auto_354}Set Price Policy").ToString(),
                        new TextObject("{=rad_wm_01}Start an aggressive pricing policy (Dumping or Exorbitant Prices) in this city.\n\nCoalition Warning: Frequently pursuing aggressive policies may anger trade guilds.").ToString(),
                        elements, false, 1, 1, "start", "Cancel",
                        chosen =>
                        {
                            string choice = chosen[0].Identifier as string;
                            if (choice == "embargo")
                            {
                                ShowEmbargoTargetSelection(settlement);
                            }
                            else
                            {
                                ApplyPricePolicy(settlement, choice);
                            }
                        }, 
                        null));
                });

            // Alt menüden çýkýþ (Geri Dön)
            starter.AddGameMenuOption("town_workshop_menu", "town_workshop_back", "{=rad_wmb_01}Go back",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args =>
                {
                    GameMenu.SwitchToMenu("town");
                }, true);
        }


        private void ShowEmbargoTargetSelection(Settlement settlement)
        {
            var elements = new List<InquiryElement>();
            foreach(var f in Campaign.Current.Factions.Where(x => x.IsKingdomFaction && x != Hero.MainHero.MapFaction))
            {
                elements.Add(new InquiryElement(f, f.Name.ToString(), null));
            }
            
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TaleWorlds.Localization.TextObject("{=rad_auto_355}Target Kingdom Selection").ToString(), new TextObject("{=rad_wm_02}Which kingdom will you ask permission from the king/council to impose an embargo on?").ToString(),
                elements, true, 1, 1, "Select", "Cancel", 
                chosen => {
                    var target = chosen[0].Identifier as Kingdom;
                    GainKingdomInfluenceAction.ApplyForDefault(Hero.MainHero, -300f);
                    EmbargoPersuasionBehavior.Instance.RequestEmbargo(settlement, target);
                }, null));
        }


        private void OnBarterablesRequested(BarterData args)
        {
            if (args.OffererHero == Hero.MainHero && args.OtherHero != null && args.OtherHero.IsLord && args.OtherHero != Hero.MainHero)
            {
                var war = _activeTradeWars.FirstOrDefault(w => w.Aggressor == Hero.MainHero);
                if (war != null)
                {
                    // Target lord should not be from the target faction!
                    if (args.OtherHero.MapFaction != war.TargetFaction && !war.CoalitionMembers.Contains(args.OtherHero))
                    {
                        args.AddBarterGroup(new JoinEmbargoBarterGroup());
                        args.AddBarterable<JoinEmbargoBarterGroup>(new JoinEmbargoBarterable(args.OtherHero, war), true);
                    }
                }
            }
        }

        private void DistributeWorkshopsGradually()
        {
            if (_hasDistributedInitialWorkshops) return;

            var lords = Hero.AllAliveHeroes.Where(h => h.IsLord && h.Clan != null && h.Clan.Leader == h && h.Clan != Hero.MainHero.Clan && h.Gold > 10000).ToList();
            if (lords.Count == 0) return;

            foreach (var lord in lords)
            {
                var factionTowns = Town.AllTowns.Where(t => t.MapFaction == lord.MapFaction).ToList();
                if (factionTowns.Count == 0) factionTowns = Town.AllTowns.ToList();

                var availableWorkshops = factionTowns
                    .Where(t => t.Workshops != null)
                    .SelectMany(t => t.Workshops)
                    .Where(w => w != null && w.Owner != null && !w.Owner.IsLord)
                    .ToList();

                if (availableWorkshops.Count > 0)
                {
                    var w = availableWorkshops.GetRandomElement();
                    ChangeOwnerOfWorkshopAction.ApplyByDeath(w, lord);
                    lord.Gold -= 5000;
                }
            }

            _hasDistributedInitialWorkshops = true;
        }

        private class AuctionState
        {
            public Settlement Settlement;
            public Workshop Workshop;
            public int CurrentBid;
            public Hero HighestBidder;
            public List<Hero> ActiveAIs = new List<Hero>();
            public Dictionary<Hero, int> AIMaxBids = new Dictionary<Hero, int>();
            public bool PlayerWithdrawn = false;
        }

        private void StartInteractiveAuction(Settlement settlement, Workshop workshop, int startPrice)
        {
            var state = new AuctionState
            {
                Settlement = settlement,
                Workshop = workshop,
                CurrentBid = startPrice,
                HighestBidder = null 
            };

            var notables = settlement.Notables.ToList();
            notables.Shuffle();
            for (int i = 0; i < Math.Min(4, notables.Count); i++)
            {
                Hero notable = notables[i];
                state.ActiveAIs.Add(notable);
                float randomM = 0.5f + (MBRandom.RandomFloat * 0.8f);
                state.AIMaxBids[notable] = (int)(startPrice * 2 * randomM); 
            }

            ShowAuctionUI(state);
        }

        private void ShowAuctionUI(AuctionState state)
        {
            if (state.PlayerWithdrawn && state.ActiveAIs.Count == 0) { EndAuction(state); return; }
            if (!state.PlayerWithdrawn && state.ActiveAIs.Count == 0 && state.HighestBidder == Hero.MainHero) { EndAuction(state); return; }

            string desc = new TextObject("{=rad_wm_03}{SETTLEMENT} - {WORKSHOP} Tender\nCurrent Highest Bid: {BID} Denars").SetTextVariable("SETTLEMENT", state.Settlement.Name).SetTextVariable("WORKSHOP", state.Workshop.WorkshopType?.Name).SetTextVariable("BID", state.CurrentBid).ToString();
            
            var elements = new List<InquiryElement>();
            if (!state.PlayerWithdrawn)
            {
                elements.Add(new InquiryElement("bid_10", new TaleWorlds.Localization.TextObject("{=rad_auto_347}+10 Denars").ToString(), null));
                elements.Add(new InquiryElement("bid_100", new TaleWorlds.Localization.TextObject("{=rad_auto_348}+100 Denars").ToString(), null));
                elements.Add(new InquiryElement("bid_1000", new TaleWorlds.Localization.TextObject("{=rad_auto_349}+1000 Denars").ToString(), null));
                elements.Add(new InquiryElement("withdraw", "{=rad_wm_06}Leave", null));
            }
            else elements.Add(new InquiryElement("wait", new TaleWorlds.Localization.TextObject("{=rad_auto_350}keep watching").ToString(), null));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "{=rad_wm_04}Tender", desc, elements, true, 1, 1, "Select", "",
                chosen =>
                {
                    string choice = chosen[0].Identifier as string;
                    if (choice == "bid_10") { state.CurrentBid += 10; state.HighestBidder = Hero.MainHero; }
                    else if (choice == "bid_100") { state.CurrentBid += 100; state.HighestBidder = Hero.MainHero; }
                    else if (choice == "bid_1000") { state.CurrentBid += 1000; state.HighestBidder = Hero.MainHero; }
                    else if (choice == "withdraw") { state.PlayerWithdrawn = true; }

                    ProcessAIBids(state);
                }, null));
        }

        private void ProcessAIBids(AuctionState state)
        {
            for (int i = state.ActiveAIs.Count - 1; i >= 0; i--)
            {
                Hero ai = state.ActiveAIs[i];
                if (state.HighestBidder == ai) continue;

                if (state.CurrentBid >= state.AIMaxBids[ai]) state.ActiveAIs.RemoveAt(i);
                else
                {
                    state.CurrentBid += MBRandom.RandomInt(10, 500);
                    state.HighestBidder = ai;
                    break;
                }
            }
            ShowAuctionUI(state);
        }

        private void EndAuction(AuctionState state)
        {
            if (state.HighestBidder == Hero.MainHero)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, state.CurrentBid, true);
                WorkshopHelper.TransferWorkshopToPlayer(state.Workshop);
            }
            _activeTenders.Remove(state.Settlement.StringId);
        }

        private void ApplyPricePolicy(Settlement settlement, string policy)
        {
            string sid = settlement.StringId;
            switch (policy)
            {
                case "damping":
                    GainKingdomInfluenceAction.ApplyForDefault(Hero.MainHero, -500f);
                    _activePolicies[sid] = "damping";
                    break;
                case "gouging":
                    _activePolicies[sid] = "gouging";
                    break;
                case "lift":
                    _activePolicies.Remove(sid);
                    break;
            }
        }

        private void OnWeeklyTick()
        {
            foreach (var town in Town.AllTowns)
            {
                foreach (var workshop in town.Workshops)
                {
                    if (workshop.Owner == Hero.MainHero) 
                    {
                        string key = Key(workshop);
                        if (MBRandom.RandomFloat < 0.15f) { _workshopModifiers[key] = 1.5f; _modifierExpiry[key] = CampaignTime.DaysFromNow(7f); }
                    }
                }
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement.IsTown && capturerHero == Hero.MainHero && newOwner == Hero.MainHero)
            {
                var elements = new List<InquiryElement>
                {
                    new InquiryElement("confiscate", new TaleWorlds.Localization.TextObject("{=rad_auto_351}Seize the Workshops (Confiscation)").ToString(), null, true, "All the workshops in the city are yours, but loyalty is low and the merchants are hated."),
                    new InquiryElement("free", new TaleWorlds.Localization.TextObject("{=rad_auto_352}Release").ToString(), null, true, "Let the old owners continue their business. City loyalty and merchant relations increase.")
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    new TaleWorlds.Localization.TextObject("{=rad_auto_356}City Conquered: Economic Decision").ToString(),
                    new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_07}You have conquered {SETTLEMENT}! What do you want to do about the city's workshops (industry)?").SetTextVariable("SETTLEMENT", settlement.Name).ToString(),
                    elements, true, 1, 1, "Decide", "",
                    chosen =>
                    {
                        string choice = chosen[0].Identifier as string;
                        if (choice == "confiscate")
                        {
                            foreach(var w in settlement.Town.Workshops)
                            {
                                if (w.Owner != Hero.MainHero)
                                {
                                    ChangeOwnerOfWorkshopAction.ApplyByDeath(w, Hero.MainHero);
                                }
                            }
                            settlement.Town.Loyalty -= 30f;
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_339}You've taken over all the workshops! The people and merchants are angry.").ToString(), Colors.Red));
                        }
                        else
                        {
                            settlement.Town.Loyalty += 20f;
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_340}You didn't touch the free market. City loyalty increased rapidly!").ToString(), Colors.Green));
                        }
                    }, null));
            }
        }

        private void OnDailyTick()
        {
            if (!_hasDistributedInitialWorkshops) DistributeWorkshopsGradually();

            var expired = _modifierExpiry.Where(kvp => kvp.Value.IsPast).Select(kvp => kvp.Key).ToList();
            foreach (var k in expired)
            {
                _workshopModifiers.Remove(k);
                _modifierExpiry.Remove(k);
            }

            // Ýhale üretimi (30 günde bir)
            if (_lastTenderTime == CampaignTime.Never || _lastTenderTime.ElapsedDaysUntilNow >= 30f)
            {
                GenerateTenders();
                _lastTenderTime = CampaignTime.Now;
            }

            // Süresi dolan ihaleler: NPC rakip devreye giriyor
            var expiredTenders = _activeTenders.Where(kvp => kvp.Value.ElapsedDaysUntilNow >= 3f).Select(kvp => kvp.Key).ToList();
            foreach (var sid in expiredTenders)
            {
                if (MBRandom.RandomFloat < 0.40f)
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_08}Bulundu?unuz {SID} ?ehrindeki ihaleyi bir NPC sat?n ald?!").SetTextVariable("SID", TaleWorlds.CampaignSystem.Settlements.Settlement.Find(sid)?.Name.ToString() ?? sid).ToString(), Colors.White));
                }
                _activeTenders.Remove(sid);
                _tenderPrices.Remove(sid);
                _tenderWorkshopIndex.Remove(sid);
            }

            // NPC'nin oyuncuya satýn alma teklifi (20 günde bir, %25 þans)
            if (_lastNpcOfferTime == CampaignTime.Never || _lastNpcOfferTime.ElapsedDaysUntilNow >= 20f)
            {
                _lastNpcOfferTime = CampaignTime.Now;
                if (MBRandom.RandomFloat < 0.25f)
                    TriggerNpcBuyOffer();
            }

            // Politika etkileri
            ApplyDailyPolicyEffects();

            // Koalisyon uyarýsý/savaþ
            CheckTradeCoalition();
        }

        private void GenerateTenders()
        {
            var visited = Town.AllTowns
                .Where(t => Hero.MainHero.MapFaction != null && t.Settlement.MapFaction != Hero.MainHero.MapFaction)
                .OrderBy(_ => MBRandom.RandomFloat)
                .Take(2)
                .ToList();

            foreach (var town in visited)
            {
                string sid = town.Settlement.StringId;
                if (_activeTenders.ContainsKey(sid)) continue;

                // NPC'ye ait bir workshop bul
                var npcWorkshop = town.Workshops?.FirstOrDefault(w => w.Owner != null && w.Owner != Hero.MainHero && w.WorkshopType != null);
                if (npcWorkshop == null) continue;

                int price = MBRandom.RandomInt(8000, 25001);
                int wsIdx  = Array.IndexOf(town.Workshops, npcWorkshop);

                _activeTenders[sid] = CampaignTime.Now;
                _tenderPrices[sid]  = price;
                _tenderWorkshopIndex[sid] = wsIdx;

                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_04}Bidding has started in {TOWN}! Go to the city.").SetTextVariable("TOWN", town.Name).ToString(), Colors.Yellow));
            }
        }

        private void TriggerNpcBuyOffer()
        {
            var playerWorkshops = Town.AllTowns
                .Where(t => t.Workshops != null)
                .SelectMany(t => t.Workshops)
                .Where(w => w.Owner == Hero.MainHero)
                .ToList();

            if (!playerWorkshops.Any()) return;

            var target = playerWorkshops[MBRandom.RandomInt(playerWorkshops.Count)];
            int offerMultiplier = MBRandom.RandomInt(15, 26); // 1.5x – 2.5x
            int baseValue = 15000;
            int offerPrice = (baseValue * offerMultiplier) / 10;

            var npcHeroes = Hero.AllAliveHeroes.Where(h => h.IsLord && !h.IsDead && h != Hero.MainHero).OrderBy(_ => MBRandom.RandomFloat).Take(1).FirstOrDefault();
            if (npcHeroes == null) return;

            var elements = new List<InquiryElement>
            {
                new InquiryElement("accept",  new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_01}Accept ({OFFER} Denars)").SetTextVariable("OFFER", offerPrice).ToString(),  null),
                new InquiryElement("decline", new TaleWorlds.Localization.TextObject("{=rad_auto_353}Reddet").ToString(),                           null)
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TaleWorlds.Localization.TextObject("{=rad_auto_357}Purchase Offer").ToString(),
                new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_06}{NAME} is offering {OFFER} Denars for your workshop in {SETTLEMENT}!").SetTextVariable("NAME", npcHeroes.Name).SetTextVariable("OFFER", offerPrice).SetTextVariable("SETTLEMENT", target.Settlement?.Name).ToString(),
                elements, true, 1, 1, "Cevapla", "",
                chosen =>
                {
                    if ((chosen[0].Identifier as string) == "accept")
                    {
                        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, offerPrice, true);
                        WorkshopHelper.TransferWorkshopToNpc(target, npcHeroes);
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, npcHeroes, 5);
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_05}The workshop was sold to {NAME} at {OFFER} Denars.").SetTextVariable("NAME", npcHeroes.Name).SetTextVariable("OFFER", offerPrice).ToString(), Colors.Green));
                    }
                }, null));
        }

        private void ApplyDailyPolicyEffects()
        {
            foreach (var kvp in _activePolicies.ToList())
            {
                var settlement = Settlement.Find(kvp.Key);
                if (settlement == null || !settlement.IsTown) continue;

                switch (kvp.Value)
                {
                    case "gouging":
                        // Fahiþ fiyat: þehir sadakatini düþür
                        settlement.Town.Loyalty -= 2f;
                        break;
                    case "damping":
                        // Damping: rakip NPC'leri zarara uðrat › 5% þans ile ihaleye düþür
                        foreach (var ws in settlement.Town.Workshops)
                        {
                            if (ws.Owner != null && ws.Owner != Hero.MainHero && MBRandom.RandomFloat < 0.05f)
                            {
                                string sid = settlement.StringId;
                                if (!_activeTenders.ContainsKey(sid))
                                {
                                    _activeTenders[sid] = CampaignTime.Now;
                                    _tenderPrices[sid]  = MBRandom.RandomInt(4000, 8001);
                                    _tenderWorkshopIndex[sid] = Array.IndexOf(settlement.Town.Workshops, ws);
                                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_03}Don't break the price! The rival workshop in {SETTLEMENT} was driven into bankruptcy.").SetTextVariable("SETTLEMENT", settlement.Name).ToString(), Colors.Yellow));
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void CheckTradeCoalition()
        {
            if (_extremePolicyStart == CampaignTime.Never) return;

            float days = _extremePolicyStart.ElapsedDaysUntilNow;

            if (days >= 30f && !_coalitionWarningSent)
            {
                _coalitionWarningSent = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    new TaleWorlds.Localization.TextObject("{=rad_auto_341}Your business policies have disturbed all of Calradia! Kingdoms are discussing forming a Trade Coalition...").ToString(),
                    Colors.Red));
            }
            else if (days >= 45f)
            {
                // Krallýklar savaþ ilan ediyor
                foreach (var kingdom in Kingdom.All.Where(k => k != Hero.MainHero.MapFaction && !k.IsAtWarWith(Hero.MainHero.MapFaction) && !k.IsEliminated))
                {
                    if (!FactionManager.IsAtWarAgainstFaction(kingdom, Hero.MainHero.MapFaction))
                    {
                        DeclareWarAction.ApplyByDefault(kingdom, Hero.MainHero.MapFaction);
                    }
                }
                _extremePolicyStart = CampaignTime.Never;
                _coalitionWarningSent = false;
                InformationManager.DisplayMessage(new InformationMessage(
                    new TaleWorlds.Localization.TextObject("{=rad_auto_342}TRADE COALITION! The kingdoms of Calradia have declared war against your commercial tyranny!").ToString(),
                    Colors.Red));
            }
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (party != MobileParty.MainParty) return;
            if (!settlement.IsTown) return;

            if (_activeTenders.ContainsKey(settlement.StringId))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TaleWorlds.Localization.TextObject("{=rad_loc_wmb_02}A workshop in the city of {SETTLEMENT} has been put out to tender! You can look at the city menu.").SetTextVariable("SETTLEMENT", settlement.Name).ToString(),
                    Colors.Yellow));
            }
        }
    }

    /// <summary>
    /// Workshop transfer yardýmcý sýnýfý.
    /// </summary>
    public static class WorkshopHelper
    {
        public static void TransferWorkshopToPlayer(Workshop w)
        {
            if (w == null || Hero.MainHero == null) return;
            ChangeOwnerOfWorkshopAction.ApplyByDeath(w, Hero.MainHero);
        }

        public static void TransferWorkshopToNpc(Workshop w, Hero newOwner)
        {
            if (w == null || newOwner == null) return;
            ChangeOwnerOfWorkshopAction.ApplyByDeath(w, newOwner);
        }
    }
}
