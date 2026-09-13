using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.View;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using SandBox.View;
using TaleWorlds.Engine;
using ClassLibrary22;

namespace RebellionsAndDemographics
{
    public class MonopolySlot
    {
        [TaleWorlds.SaveSystem.SaveableProperty(1)]
        public int SlotIndex { get; set; }

        [TaleWorlds.SaveSystem.SaveableProperty(2)]
        public string OwnerHeroId { get; set; }

        [TaleWorlds.SaveSystem.SaveableProperty(3)]
        public bool IsEmpty { get; set; }
        
        [TaleWorlds.SaveSystem.SaveableProperty(4)]
        public string WorkshopTypeId { get; set; }
    }
    public class VirtualWorkshopShare
    {
        [SaveableProperty(1)]
        public string SettlementId { get; set; } = "";

        [SaveableProperty(2)]
        public string WorkshopTypeId { get; set; } = "";

        [SaveableProperty(3)]
        public string Name { get; set; } = "";

        [SaveableProperty(4)]
        public int Level { get; set; } = 1;

        [SaveableProperty(5)]
        public int ConfiscationWarningDays { get; set; } = 0;

        [SaveableProperty(6)]
        public bool IsWarTaxed { get; set; } = false;

        [SaveableProperty(7)]
        public string Trait { get; set; } = ""; // Random trait like "PrimeLocation", "ShoddyTools"

        [SaveableProperty(8)]
        public string Tag { get; set; } = "";

        [SaveableProperty(9)]
        public int StrategyType { get; set; } = 0; // 0=Guild, 1=War, 2=Smuggling, 3=Sweatshop, 4=Hoarding

        [SaveableProperty(10)]
        public int RawMaterials { get; set; } = 100;

        [SaveableProperty(11)]
        public string OverseerStringId { get; set; } = "";

        [SaveableProperty(12)]
        public int PendingCrisisType { get; set; } = -1; // -1 means no crisis, otherwise WorkshopMissionType

        [SaveableProperty(13)]
        public int PendingCrisisDaysLeft { get; set; } = 0;

        [SaveableProperty(14)]
        public bool IsConfiscatedAndWaitingForSale { get; set; } = false;

        [SaveableProperty(15)]
        public CampaignTime ConfiscationEndTime { get; set; }
    }

    public class WorkshopKingdomBehavior : CampaignBehaviorBase
    {
        private List<VirtualWorkshopShare> _playerShares = new List<VirtualWorkshopShare>();
        private List<string> _confiscatedTownIds = new List<string>();
        private List<string> PendingDuelComplaints = new List<string>();
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MonopolySlot>> _townMonopolySlots = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MonopolySlot>>();
        private System.Collections.Generic.Dictionary<string, bool> _tradePermits = new System.Collections.Generic.Dictionary<string, bool>();

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MonopolySlot>> TownMonopolySlots => _townMonopolySlots;
        public System.Collections.Generic.Dictionary<string, bool> TradePermits => _tradePermits;

        // Anti-spam warning logic for empty raw materials
        private Dictionary<string, bool> _outOfMaterialsWarningGiven = new Dictionary<string, bool>();

        public Kingdom ActiveSabotageKingdom = null;
        public int ActiveSabotageDaysLeft = 0;
        public Kingdom ActiveSpyKingdom = null;
        public int ActiveSpyDaysLeft = 0;
        public float LastTreasonOfferTime = -999f;
        private int _lastTickDay = -1;

        public static WorkshopKingdomBehavior Instance { get; private set; }
        public static bool IndustryLeaderPersuasionBonus = false;

        public List<VirtualWorkshopShare> PlayerShares => _playerShares;

        public WorkshopKingdomBehavior()
        {
            Instance = this;
        }

        public List<VirtualWorkshopShare> GetSharesInSettlement(string settlementId)
        {
            return _playerShares.Where(s => s.SettlementId == settlementId).ToList();
        }

        public void AddPlayerShare(VirtualWorkshopShare share)
        {
            _playerShares.Add(share);
        }


        public bool HasTradePermit(Settlement town)
        {
            if (town.MapFaction == Hero.MainHero.MapFaction) return true;
            if (_tradePermits.ContainsKey(town.StringId) && _tradePermits[town.StringId]) return true;
            return false;
        }

        public void GrantTradePermit(Settlement town)
        {
            _tradePermits[town.StringId] = true;
        }

        public System.Collections.Generic.List<MonopolySlot> GetOrInitializeSlots(Settlement town)
        {
            if (!_townMonopolySlots.ContainsKey(town.StringId))
            {
                var list = new System.Collections.Generic.List<MonopolySlot>();
                int totalSlots = 12; // Sabit 12 slot
                var lords = town.MapFaction.Heroes.Where(l => l.IsAlive && l.Age >= 18).ToList();
                if (lords.Count == 0) lords.Add(town.OwnerClan.Leader);

                for (int i = 0; i < totalSlots; i++)
                {
                    bool isEmpty = (TaleWorlds.Core.MBRandom.RandomFloat < 0.2f); // 20% bos
                    string owner = "";
                    if (!isEmpty)
                    {
                        var lord = lords.GetRandomElement();
                        if (lord != null) owner = lord.StringId;
                    }
                    
                    list.Add(new MonopolySlot()
                    {
                        SlotIndex = i,
                        IsEmpty = isEmpty,
                        OwnerHeroId = owner,
                        WorkshopTypeId = town.Town.Workshops.GetRandomElement()?.WorkshopType?.StringId ?? "unknown"
                    });
                }
                _townMonopolySlots[town.StringId] = list;
            }
            
            return _townMonopolySlots[town.StringId];
        }

        public List<VirtualWorkshopShare> GetAllShares()
        {
            return _playerShares;
        }

        public void HandleMissionSuccess(WorkshopMissionType type, VirtualWorkshopShare share)
        {
            if (share == null) return;
            switch (type)
            {
                case WorkshopMissionType.Smuggling:
                    InformationManager.DisplayMessage(new InformationMessage($"{share.Name} property was rescued from government seizure! (Smuggling Successful)", Colors.Green));
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 5000, true);
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(share.SettlementId, CouncilJudgmentCrisisType.Corruption);
                    break;
                case WorkshopMissionType.WarehouseDefense:
                    InformationManager.DisplayMessage(new InformationMessage($"Your stocks are safe from rebels!", Colors.Green));
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(share.SettlementId, CouncilJudgmentCrisisType.WorkerRevolt);
                    break;
                case WorkshopMissionType.Brawl:
                    InformationManager.DisplayMessage(new InformationMessage($"You repelled the saboteurs! Your workshop was not damaged.", Colors.Green));
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(share.SettlementId, CouncilJudgmentCrisisType.Corruption);
                    break;
                case WorkshopMissionType.Duel:
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(share.SettlementId, CouncilJudgmentCrisisType.Garrison);
                    Hero.MainHero.Clan.Renown += 10f;
                    break;
                case WorkshopMissionType.Inspector:
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(share.SettlementId, CouncilJudgmentCrisisType.Inspector);
                    break;
                case WorkshopMissionType.Evacuation:
                    var allConfiscated = _playerShares.Where(s => s.SettlementId == share.SettlementId && s.IsConfiscatedAndWaitingForSale).ToList();
                    int totalMaterials = allConfiscated.Sum(s => s.RawMaterials);
                    InformationManager.DisplayMessage(new InformationMessage($"Evacuation Successful! {totalMaterials} raw materials have been transferred to your batch and your shares have been recovered and deleted from the system.", Colors.Green));
                    var item = TaleWorlds.CampaignSystem.Campaign.Current.ObjectManager.GetObject<ItemObject>("grain");
                    if (item != null && totalMaterials > 0)
                    {
                        MobileParty.MainParty.ItemRoster.AddToCounts(item, totalMaterials);
                    }
                    foreach (var s in allConfiscated)
                    {
                        RemoveShareCompletely(s);
                    }
                    break;
            }
        }

        public string GetRandomTrait()
        {
            string[] traits = { "PrimeLocation", "ShoddyTools", "SkilledArtisans", "BadReputation", "LocalMonopoly" };
            return traits[MBRandom.RandomInt(traits.Length)];
        }

        public float GetTotalIncomeMultiplier(VirtualWorkshopShare share)
        {
            float mult = 1.0f;

            switch (share.Trait)
            {
                case "PrimeLocation": mult += 0.20f; break;
                case "ShoddyTools": mult -= 0.15f; break;
                case "SkilledArtisans": mult += 0.30f; break;
                case "BadReputation": mult -= 0.20f; break;
                case "LocalMonopoly": mult += 0.40f; break;
            }

            Settlement settlement = Settlement.Find(share.SettlementId);
            if (settlement != null)
            {
                if (Hero.MainHero.Culture == settlement.Culture)
                {
                    mult += 0.30f;
                }

                if (settlement.MapFaction != null && Hero.MainHero.MapFaction != settlement.MapFaction)
                {
                    if (Hero.MainHero.MapFaction.Leader != null && settlement.MapFaction.Leader != null)
                    {
                        float relation = Hero.MainHero.MapFaction.Leader.GetRelation(settlement.MapFaction.Leader);
                        float dipBonus = (relation / 100f) * 0.20f;
                        mult += dipBonus;
                    }
                }
            }

            return Math.Max(0f, mult);
        }


        private int _dailyAggregatedIncome = 0;
        private List<string> _dailyShortageSettlements = new List<string>();

        private void DailyTick()
        {
            if (_dailyAggregatedIncome != 0 || _dailyShortageSettlements.Count > 0)
            {
                string shortageText = "";
                if (_dailyShortageSettlements.Count > 0)
                {
                    TextObject shortageObj = new TextObject("{=rad_agg_income_shortage} (Stock Shortage: {TOWNS})");
                    shortageObj.SetTextVariable("TOWNS", string.Join(", ", _dailyShortageSettlements.Distinct()));
                    shortageText = shortageObj.ToString();
                }

                if (_dailyAggregatedIncome >= 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, _dailyAggregatedIncome, true);

                    TextObject posMsg = new TextObject("{=rad_agg_income_pos}Total Workshop Income: +{INCOME} Denars{SHORTAGE}");
                    posMsg.SetTextVariable("INCOME", _dailyAggregatedIncome);
                    posMsg.SetTextVariable("SHORTAGE", shortageText);
                    InformationManager.DisplayMessage(new InformationMessage(posMsg.ToString(), Colors.Green));
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, Math.Abs(_dailyAggregatedIncome), true);

                    TextObject negMsg = new TextObject("{=rad_agg_income_neg}Total Workshop Income: {INCOME} Denars (Loss!){SHORTAGE}");
                    negMsg.SetTextVariable("INCOME", _dailyAggregatedIncome);
                    negMsg.SetTextVariable("SHORTAGE", shortageText);
                    InformationManager.DisplayMessage(new InformationMessage(negMsg.ToString(), Colors.Red));
                }

                _dailyAggregatedIncome = 0;
                _dailyShortageSettlements.Clear();
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HourlyTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var pShares = _playerShares;
                if (pShares != null) pShares.RemoveAll(x => x == null);

                var cTowns = _confiscatedTownIds;
                if (cTowns != null) cTowns.RemoveAll(x => x == null);

                var pDuels = PendingDuelComplaints;
                if (pDuels != null) pDuels.RemoveAll(x => x == null);
            }

            dataStore.SyncData("_playerShares", ref _playerShares);
            dataStore.SyncData("_confiscatedTownIds", ref _confiscatedTownIds);
            dataStore.SyncData("PendingDuelComplaints", ref PendingDuelComplaints);
            dataStore.SyncData("LastTreasonOfferTime", ref LastTreasonOfferTime);
            dataStore.SyncData("ActiveSabotageKingdom", ref ActiveSabotageKingdom);
            dataStore.SyncData("ActiveSabotageDaysLeft", ref ActiveSabotageDaysLeft);
            dataStore.SyncData("ActiveSpyKingdom", ref ActiveSpyKingdom);
            dataStore.SyncData("ActiveSpyDaysLeft", ref ActiveSpyDaysLeft);

            if (dataStore.IsLoading)
            {
                if (_playerShares == null) _playerShares = new List<VirtualWorkshopShare>();
                if (_confiscatedTownIds == null) _confiscatedTownIds = new List<string>();
                if (PendingDuelComplaints == null) PendingDuelComplaints = new List<string>();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddMenus(starter);
            FixBrokenPlayerWorkshops();
        }

        private void FixBrokenPlayerWorkshops()
        {
            try
            {
                if (Hero.MainHero == null || Clan.PlayerClan == null) return;
                
                // 1. Clean up corrupted workshops
                foreach (var town in Town.AllTowns)
                {
                    if (town.Workshops != null)
                    {
                        foreach (var w in town.Workshops)
                        {
                            if (w != null && w.Owner != null && w.Owner.Clan == Clan.PlayerClan && w.WorkshopType == null)
                            {
                                var notable = town.Settlement.Notables?.FirstOrDefault();
                                if (notable != null)
                                {
                                    ChangeOwnerOfWorkshopAction.ApplyByDeath(w, notable);
                                }
                                else if (town.Settlement.OwnerClan?.Leader != null)
                                {
                                    ChangeOwnerOfWorkshopAction.ApplyByDeath(w, town.Settlement.OwnerClan.Leader);
                                }
                                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 15000, true);
                                InformationManager.DisplayMessage(new InformationMessage("RebellionsAndDemographics: Fixed a broken workshop in " + town.Name + ". You were refunded 15000 Denars.", Colors.Green));
                            }
                        }
                    }
                }

                // 2. Clean up shadow parties that shouldn't be in the clan
                var partiesToDestroy = new System.Collections.Generic.List<MobileParty>();
                foreach (var party in MobileParty.All)
                {
                    if (party != null && party.ActualClan == Clan.PlayerClan)
                    {
                        if (party.StringId.Contains("shadow_") || party.StringId.Contains("pendraic_") || party.PartyComponent is TaleWorlds.CampaignSystem.Party.PartyComponents.CustomPartyComponent)
                        {
                            partiesToDestroy.Add(party);
                        }
                    }
                }
                foreach (var p in partiesToDestroy)
                {
                    DestroyPartyAction.Apply(null, p);
                    InformationManager.DisplayMessage(new InformationMessage("RebellionsAndDemographics: Removed corrupted event party from clan: " + p.Name, Colors.Green));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("RebellionsAndDemographics Cleanup Error: " + ex.Message, Colors.Red));
            }
        }

        public void SetWorkshopConfiscated(VirtualWorkshopShare share)
        {
            if (share == null) return;

            var allLocalShares = _playerShares.Where(s => s.SettlementId == share.SettlementId).ToList();
            foreach (var localShare in allLocalShares)
            {
                localShare.IsConfiscatedAndWaitingForSale = true;
                localShare.ConfiscationEndTime = CampaignTime.Now + CampaignTime.Hours(24f);
            }

            InformationManager.DisplayMessage(new InformationMessage($"{share.Name} and all your other shares in this city are sealed! You have 24 hours to sell the shares or poach the goods.", Colors.Red));
        }

        public void RemoveShareCompletely(VirtualWorkshopShare share)
        {
            if (share != null && _playerShares.Contains(share))
            {
                _playerShares.Remove(share);
            }
        }

        private void HourlyTick()
        {
            var expiredShares = _playerShares.Where(s => s.IsConfiscatedAndWaitingForSale && s.ConfiscationEndTime.IsPast).ToList();
            foreach (var share in expiredShares)
            {
                RemoveShareCompletely(share);
                InformationManager.DisplayMessage(new InformationMessage($"{share.Name} shares were permanently seized by the state. Time's up!", Colors.Red));

                if (PendingDuelComplaints.Contains(share.SettlementId) && !_playerShares.Any(s => s.SettlementId == share.SettlementId && s.IsConfiscatedAndWaitingForSale))
                {
                    PendingDuelComplaints.Remove(share.SettlementId);
                }
            }
        }

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_workshop_menu", "workshop_kingdom_manage", "{=rad_wk_09}Workshop Share & Trade Management",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown;
                },
                args =>
                {
                    OpenTownWorkshopMenu();
                });

            starter.AddGameMenuOption("town", "town_workshop_evacuation", "{=rad_wk_01}Smuggle the Sealed Goods (Evacuation Operation)",
                args =>
                {
                    if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown)
                    {
                        var hasConfiscatedShares = _playerShares.Any(s => s.SettlementId == Settlement.CurrentSettlement.StringId && s.IsConfiscatedAndWaitingForSale);
                        if (hasConfiscatedShares)
                        {
                            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                            return true;
                        }
                    }
                    return false;
                },
                args =>
                {
                    var initialSelections = TaleWorlds.CampaignSystem.Roster.TroopRoster.CreateDummyTroopRoster();
                    initialSelections.AddToCounts(TaleWorlds.CampaignSystem.CharacterObject.PlayerCharacter, 1);

                    args.MenuContext.OpenTroopSelection(
                        TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.MemberRoster,
                        initialSelections,
                        (character) =>
                        {
                            if (!character.IsPlayerCharacter)
                            {
                                return !character.IsNotTransferableInHideouts;
                            }
                            return false;
                        },
                        delegate (TaleWorlds.CampaignSystem.Roster.TroopRoster selectedTroops)
                        {
                            WorkshopActionMissionsLogic.SelectedPlayerTroops = selectedTroops;
                            // Target share can be any confiscated share in this settlement
                            var targetShare = _playerShares.FirstOrDefault(s => s.SettlementId == Settlement.CurrentSettlement.StringId && s.IsConfiscatedAndWaitingForSale);
                            StartActionMission(WorkshopMissionType.Evacuation, targetShare, Settlement.CurrentSettlement);
                        },
                        10,
                        1
                    );
                });

            starter.AddGameMenuOption("town", "town_workshop_complain", "{=rad_wk_02}Complain about the Commander to the Lord",
                args =>
                {
                    if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown)
                    {
                        if (PendingDuelComplaints.Contains(Settlement.CurrentSettlement.StringId))
                        {
                            args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                            return true;
                        }
                    }
                    return false;
                },
                args =>
                {
                    PendingDuelComplaints.Remove(Settlement.CurrentSettlement.StringId);
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_119}The lord listened to his complaint and punished the commander. The seal on the goods has been removed!").ToString(), Colors.Green));

                    var allConfiscated = _playerShares.Where(s => s.SettlementId == Settlement.CurrentSettlement.StringId && s.IsConfiscatedAndWaitingForSale).ToList();
                    foreach (var s in allConfiscated)
                    {
                        s.IsConfiscatedAndWaitingForSale = false;
                    }
                });

            starter.AddGameMenuOption("town", "town_workshop_crisis", "{=rad_wk_03}Intervene in the Workshop Crisis",
                args =>
                {
                    if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown)
                    {
                        var crisisShare = _playerShares.FirstOrDefault(s => s.SettlementId == Settlement.CurrentSettlement.StringId && s.PendingCrisisType != -1);
                        if (crisisShare != null)
                        {
                            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                            return true;
                        }
                    }
                    return false;
                },
                args =>
                {
                    var town = Settlement.CurrentSettlement;
                    var crisisShare = _playerShares.FirstOrDefault(s => s.SettlementId == town.StringId && s.PendingCrisisType != -1);
                    if (crisisShare != null)
                    {
                        int typeInt = crisisShare.PendingCrisisType;
                        var missionType = (WorkshopMissionType)typeInt;

                        int maxTroopCount = 10;

                        var initialSelections = TaleWorlds.CampaignSystem.Roster.TroopRoster.CreateDummyTroopRoster();
                        initialSelections.AddToCounts(TaleWorlds.CampaignSystem.CharacterObject.PlayerCharacter, 1);

                        args.MenuContext.OpenTroopSelection(
                            TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.MemberRoster,
                            initialSelections,
                            (character) =>
                            {
                                if (!character.IsPlayerCharacter)
                                {
                                    return !character.IsNotTransferableInHideouts;
                                }
                                return false;
                            },
                            delegate (TaleWorlds.CampaignSystem.Roster.TroopRoster selectedTroops)
                            {
                                crisisShare.PendingCrisisType = -1; // Consume crisis
                                crisisShare.PendingCrisisDaysLeft = 0;
                                WorkshopActionMissionsLogic.SelectedPlayerTroops = selectedTroops;
                                StartActionMission(missionType, crisisShare, town);
                            },
                            maxTroopCount,
                            1
                        );
                    }
                });

            starter.AddGameMenuOption("town", "reclaim_confiscated_workshop", "{=rad_auto_118}Recover Confiscated Property",
                args =>
                {
                    if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown)
                    {
                        bool hasClaim = _confiscatedTownIds.Contains(Settlement.CurrentSettlement.StringId);
                        bool ownsTown = Settlement.CurrentSettlement.MapFaction == Hero.MainHero.MapFaction;
                        if (hasClaim && ownsTown)
                        {
                            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                            return true;
                        }
                    }
                    return false;
                },
                args =>
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 10000, true);
                    _confiscatedTownIds.Remove(Settlement.CurrentSettlement.StringId);
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_120}You got your properties back and won compensation of 10,000 Denars.").ToString(), Colors.Green));
                });
        }

        public void OpenTownWorkshopMenu()
        {
            Settlement town = Settlement.CurrentSettlement;
            if (town == null || !town.IsTown) return;

            var localShares = _playerShares.Where(s => s.SettlementId == town.StringId).ToList();
            int sharePrice = 10000 + (town.Town != null ? (int)town.Town.Prosperity : 0);

            WorkshopSharesUIManager.Open(town);
        }

        public int GetUpgradeCost(int currentLevel)
        {
            switch (currentLevel)
            {
                case 1: return 5000;
                case 2: return 12000;
                case 3: return 25000;
                case 4: return 50000;
                default: return 999999;
            }
        }

        public void ShowManagementMenu(VirtualWorkshopShare share)
        {
            var vm = new CitySelectionVM();

            string desc = new TaleWorlds.Localization.TextObject("{=rad_mng_desc_1}Current Strategy: {STRATEGY}\nRaw Material Stock: {RAW_MAT}\nSelect a strategy or buy raw materials.")
                .SetTextVariable("STRATEGY", share.StrategyType)
                .SetTextVariable("RAW_MAT", share.RawMaterials).ToString();

            vm.Description = new TaleWorlds.Localization.TextObject("{=rad_mng_title}{SHARE_NAME} - Management\n\n{DESC}")
                .SetTextVariable("SHARE_NAME", share.Name)
                .SetTextVariable("DESC", desc).ToString();

            vm.ShowList = true;
            vm.ShowInput = false;
            vm.HasAcceptButton = true;
            vm.HasCancelButton = false;
            vm.AcceptButtonText = new TaleWorlds.Localization.TextObject("{=rad_btn_accept}Accept").ToString();

            string selectedId = null;

            Action<CitySelectionItemVM, string> onSelect = (item, id) => {
                foreach (var x in vm.Items) x.IsSelected = false;
                item.IsSelected = true;
                selectedId = id;
            };

            string activeText = new TaleWorlds.Localization.TextObject("{=rad_active_txt}(Active)").ToString();
            string noWarText = new TaleWorlds.Localization.TextObject("{=rad_nowar_txt}(No War)").ToString();

            vm.Items.Add(new CitySelectionItemVM(0, new TaleWorlds.Localization.TextObject("{=rad_strat_0}Strategy: Normal (Default) {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 0 ? activeText : "").ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_0}Profit: Low").ToString(), share.StrategyType == 0, (item) => onSelect(item, "strat_0")));
            vm.Items.Add(new CitySelectionItemVM(1, new TaleWorlds.Localization.TextObject("{=rad_strat_1}Strategy: Guild Standards {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 1 ? activeText : "").ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_1}Profit: Standard").ToString(), share.StrategyType == 1, (item) => onSelect(item, "strat_1")));

            bool isAtWarNow = TaleWorlds.CampaignSystem.Kingdom.All.Any(k => k == Hero.MainHero.MapFaction && k.IsAtWarWith(Kingdom.All.FirstOrDefault(x => x != Hero.MainHero.MapFaction)));
            bool canSelectWar = share.StrategyType != 2 && isAtWarNow;
            vm.Items.Add(new CitySelectionItemVM(2, new TaleWorlds.Localization.TextObject("{=rad_strat_2}Strategy: War Profiteering {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 2 ? activeText : (isAtWarNow ? "" : noWarText)).ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_2}Profit: High (At War)").ToString(), !canSelectWar, (item) => onSelect(item, "strat_2")));

            vm.Items.Add(new CitySelectionItemVM(3, new TaleWorlds.Localization.TextObject("{=rad_strat_3}Strategy: Black Market / Smuggling {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 3 ? activeText : "").ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_3}Profit: Very High").ToString(), share.StrategyType == 3, (item) => onSelect(item, "strat_3")));
            vm.Items.Add(new CitySelectionItemVM(4, new TaleWorlds.Localization.TextObject("{=rad_strat_4}Strategy: Cheap Labor (Mass Production) {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 4 ? activeText : "").ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_4}Profit: High").ToString(), share.StrategyType == 4, (item) => onSelect(item, "strat_4")));
            vm.Items.Add(new CitySelectionItemVM(5, new TaleWorlds.Localization.TextObject("{=rad_strat_5}Strategy: Hoarding {ACTIVE}").SetTextVariable("ACTIVE", share.StrategyType == 5 ? activeText : "").ToString(), new TaleWorlds.Localization.TextObject("{=rad_prof_5}Profit: None").ToString(), share.StrategyType == 5, (item) => onSelect(item, "strat_5")));

            vm.Items.Add(new CitySelectionItemVM(6, new TaleWorlds.Localization.TextObject("{=rad_strat_6}Buy Raw Materials (50 Units = 1000 Denars)").ToString(), "", Hero.MainHero.Gold < 1000, (item) => onSelect(item, "buy_materials")));

            vm.OnAccept = () => {
                if (string.IsNullOrEmpty(selectedId)) return;

                CitySelectionUIManager.Close();

                if (selectedId.StartsWith("strat_"))
                {
                    share.StrategyType = int.Parse(selectedId.Replace("strat_", ""));
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_strat_changed}Strategy successfully changed!").ToString(), Colors.Green));
                }
                else if (selectedId == "buy_materials")
                {
                    if (Hero.MainHero.Gold >= 1000)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, true);
                        share.RawMaterials += 50;
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_mat_bought}Bought 50 Raw Materials for 1000 Denars.").ToString(), Colors.Green));
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_no_gold_1}Not enough gold.").ToString(), Colors.Red));
                    }
                }
            };

            vm.OnCancel = () => {
                CitySelectionUIManager.Close();
            };

            CitySelectionUIManager.Open(vm);
        }

        private void DailyTickSettlement(Settlement settlement)
        {
            if (_lastTickDay != (int)CampaignTime.Now.ToDays)
            {
                _lastTickDay = (int)CampaignTime.Now.ToDays;

                if (ActiveSabotageDaysLeft > 0)
                {
                    ActiveSabotageDaysLeft--;
                    if (ActiveSabotageDaysLeft <= 0 && ActiveSabotageKingdom != null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"Your economic sabotage agreement on the {ActiveSabotageKingdom.Name} kingdom has ended. The enemy kingdom has sent your reward.", Colors.Red));
                        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 50000, true);
                        foreach (var s in _playerShares.Where(share => share.StrategyType == -1))
                        {
                            s.StrategyType = 0; // reset
                        }
                        ActiveSabotageKingdom = null;
                    }
                }
                if (ActiveSpyDaysLeft > 0)
                {
                    ActiveSpyDaysLeft--;
                    if (ActiveSpyKingdom != null)
                    {
                        // Add prosperity/loyalty to enemy kingdom towns
                        foreach (var t in ActiveSpyKingdom.Settlements.Where(s => s.IsTown))
                        {
                            t.Town.Prosperity += 5f;
                            t.Town.Loyalty += 0.5f;
                        }
                    }
                    if (ActiveSpyDaysLeft <= 0 && ActiveSpyKingdom != null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"Your spy network in the kingdom of {ActiveSpyKingdom.Name} has expired and has been shut down without being discovered.", Colors.Yellow));
                        ActiveSpyKingdom = null;
                    }
                }
            }

            if (!settlement.IsTown) return;

            var localShares = _playerShares.Where(s => s.SettlementId == settlement.StringId).ToList();

            if (localShares.Count > 0)
            {
                List<VirtualWorkshopShare> sharesToRemove = new List<VirtualWorkshopShare>();
                int totalSettlementIncome = 0;

                foreach (var share in localShares)
                {
                    if (share.Level == 5)
                    {
                        settlement.Town.Prosperity += 2f;
                    }

                    if (settlement.OwnerClan != null && settlement.OwnerClan.Leader != null)
                    {
                        if (settlement.OwnerClan.Leader.GetRelation(Hero.MainHero) < -40)
                        {
                            share.ConfiscationWarningDays++;
                            if (share.ConfiscationWarningDays % 7 == 0)
                            {
                                MBInformationManager.AddQuickInformation(new TextObject($"Lord {settlement.OwnerClan.Leader.Name} is threatening your workshop shares in {settlement.Name}!"));
                            }
                            else if (share.ConfiscationWarningDays >= 21)
                            {
                                share.ConfiscationWarningDays = 0;
                                sharesToRemove.Add(share);
                                InformationManager.DisplayMessage(new InformationMessage($"Your workshop share in {settlement.Name} was confiscated due to poor relations!", Colors.Red));
                                continue;
                            }
                        }
                        else
                        {
                            share.ConfiscationWarningDays = 0;
                        }
                    }

                    if (settlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction) && !share.IsWarTaxed)
                    {
                        ShowWarConfiscationMenu(share, settlement);
                    }

                    totalSettlementIncome += CalculateIncome(share, settlement);

                    if (share.PendingCrisisType != -1)
                    {
                        share.PendingCrisisDaysLeft--;
                        if (share.PendingCrisisDaysLeft <= 0)
                        {
                            HandleMissionFailureTimeout((WorkshopMissionType)share.PendingCrisisType, share);
                            share.PendingCrisisType = -1;
                        }
                    }
                }

                if (totalSettlementIncome != 0)
                {
                    _dailyAggregatedIncome += totalSettlementIncome;
                }

                foreach (var share in sharesToRemove)
                {
                    _playerShares.Remove(share);
                }

                if (localShares.Count >= 3)
                {
                    GainKingdomInfluenceAction.ApplyForDefault(Hero.MainHero, 2f);
                    if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.CurrentSettlement == settlement)
                    {
                        IndustryLeaderPersuasionBonus = true;
                    }
                    else
                    {
                        IndustryLeaderPersuasionBonus = false;
                    }
                }

                // --- Town-level Crisis Check ---
                var validShares = localShares.Where(s => s.PendingCrisisType == -1).ToList();
                if (validShares.Count > 0)
                {
                    // Random Duel Event
                    if (MBRandom.RandomFloat < 0.005f)
                    {
                        var randomShare = validShares.GetRandomElementInefficiently();
                        TriggerCrisisMission(WorkshopMissionType.Duel, randomShare, settlement);
                    }
                    else if (MBRandom.RandomFloat < 0.01f)
                    {
                        var randomShare = validShares.GetRandomElementInefficiently();
                        WorkshopMissionType crisisType = WorkshopMissionType.Inspector;
                        switch (randomShare.StrategyType)
                        {
                            case 1: crisisType = WorkshopMissionType.Brawl; break;
                            case 2: crisisType = WorkshopMissionType.WarehouseDefense; break;
                            case 3: crisisType = WorkshopMissionType.Smuggling; break;
                            case 4: crisisType = WorkshopMissionType.Brawl; break;
                        }
                        TriggerCrisisMission(crisisType, randomShare, settlement);
                    }
                }
            }
        }

        private int CalculateIncome(VirtualWorkshopShare share, Settlement settlement)
        {
            if (share.IsConfiscatedAndWaitingForSale) return 0;

            // Treason economic sabotage
            if (share.StrategyType == -1) return 0;

            float baseIncome = 250f;
            float additiveMultiplier = 1.0f; // Base multiplier
            int rawMaterialConsumption = 2;

            switch (share.StrategyType)
            {
                case 0: additiveMultiplier += 0.0f; rawMaterialConsumption = 2; break; // Normal
                case 1: additiveMultiplier += 0.2f; rawMaterialConsumption = 4; break; // Guild
                case 2: // WarProduction
                    bool isAtWar = TaleWorlds.CampaignSystem.Kingdom.All.Any(k => k.IsAtWarWith(settlement.MapFaction));
                    additiveMultiplier += isAtWar ? 1.0f : -0.5f;
                    rawMaterialConsumption = isAtWar ? 8 : 2;
                    break;
                case 3: additiveMultiplier += 2.0f; rawMaterialConsumption = 4; break; // Smuggling
                case 4: additiveMultiplier += 1.5f; rawMaterialConsumption = 10; break; // Sweatshop
                case 5: // Hoarding
                    rawMaterialConsumption = 0;
                    if (settlement.Town.FoodStocks < 100f || settlement.Town.Prosperity < 2000f)
                        additiveMultiplier += 4.0f;
                    else
                        additiveMultiplier -= 1.0f; // Yields 0
                    break;
            }

            if (!string.IsNullOrEmpty(share.OverseerStringId))
            {
                rawMaterialConsumption = (int)(rawMaterialConsumption * 0.7f);
                additiveMultiplier += 0.2f;
            }

            share.RawMaterials -= rawMaterialConsumption;
            if (share.RawMaterials < 0) share.RawMaterials = 0;

            if (share.RawMaterials <= 0)
            {
                _dailyShortageSettlements.Add(settlement.Name.ToString());
                if (!_outOfMaterialsWarningGiven.ContainsKey(share.Tag) || !_outOfMaterialsWarningGiven[share.Tag])
                {
                    _outOfMaterialsWarningGiven[share.Tag] = true;
                }
                additiveMultiplier = 0f; // No materials, no income
            }
            else
            {
                _outOfMaterialsWarningGiven[share.Tag] = false;
            }

            switch (share.Level)
            {
                case 2: additiveMultiplier += 0.25f; break;
                case 3: additiveMultiplier += 1.00f; break;
                case 4: additiveMultiplier += 2.75f; break;
                case 5: additiveMultiplier += 5.75f; break;
            }

            int limit = Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(Hero.MainHero.Clan.Tier);
            int overLimit = _playerShares.Count - limit;
            if (overLimit > 0)
            {
                additiveMultiplier -= (0.15f * overLimit);
            }

            // Prevent negative multiplier BEFORE applying war tax
            if (additiveMultiplier < 0f) additiveMultiplier = 0f;

            if (share.IsWarTaxed)
            {
                // Force it into negative by subtracting base so it becomes a penalty
                additiveMultiplier -= 0.60f;
            }

            float mcmMult = RebellionSettings.Instance != null ? RebellionSettings.Instance.WorkshopIncomeMultiplier : 1.0f;

            // Apply MCM multiplier only to positive side to avoid multiplying taxes
            if (additiveMultiplier > 0)
            {
                additiveMultiplier *= mcmMult;
            }

            int finalIncome = (int)(baseIncome * additiveMultiplier);
            return finalIncome;
        }

        private void TriggerCrisisMission(WorkshopMissionType type, VirtualWorkshopShare share, Settlement settlement)
        {
            if (settlement == null) return;
            if (share.PendingCrisisType != -1) return; // Wait until current is resolved

            share.PendingCrisisType = (int)type;
            share.PendingCrisisDaysLeft = 3;

            string title = "";
            string desc = "";

            switch (type)
            {
                case WorkshopMissionType.Duel:
                    title = "Garrison Requisition";
                    desc = $"The local commander in {settlement.Name} is trying to confiscate your goods! If you don't go there and intervene within 3 days, you will lose the goods!";
                    break;
                case WorkshopMissionType.Inspector:
                    title = "Corrupt Inspector";
                    desc = $"The guild inspector in {settlement.Name} comes to deduct heavy taxes, using the standards as an excuse. If you don't go to the city center and convince him within 3 days, you will pay a heavy fine!";
                    break;
                case WorkshopMissionType.Smuggling:
                    title = "Raid";
                    desc = $"{settlement.Name} guards have detected your contraband. If you do not go and miss the goods within 3 days, they will be confiscated!";
                    break;
                case WorkshopMissionType.Brawl:
                    title = new TextObject("{=rad_wk_06}Worker Rebellion / Sabotage").ToString();
                    desc = $"Goons hired by the rival guild in the city of {settlement.Name} are attacking your workshop! If you do not intervene within 3 days, the workshop will suffer great damage!";
                    break;
                case WorkshopMissionType.WarehouseDefense:
                    title = "Hunger Rebellion";
                    desc = $"The people of {settlement.Name} learned that there was food in your warehouse and started a rebellion! If you don't intervene within 3 days, they will loot everything!";
                    break;
            }

            MBInformationManager.AddQuickInformation(new TextObject($"{title}: {settlement.Name} your workshop is in danger! Go there now!"));
            InformationManager.ShowInquiry(new InquiryData(title, desc, true, false, new TaleWorlds.Localization.TextObject("{=rad_btn_ok}OK").ToString(), "", null, null));
        }

        public void HandleMissionFailureTimeout(WorkshopMissionType type, VirtualWorkshopShare share)
        {
            if (share == null) return;
            switch (type)
            {
                case WorkshopMissionType.Smuggling:
                    InformationManager.DisplayMessage(new InformationMessage($"The state confiscated {share.Name}'s property! (No intervention)", Colors.Red));
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 5000, true);
                    break;
                case WorkshopMissionType.WarehouseDefense:
                    InformationManager.DisplayMessage(new InformationMessage($"Your stocks have been looted by the rebels! All your raw material stocks have been reset! (No intervention)", Colors.Red));
                    share.RawMaterials = 0;
                    foreach (var otherShare in _playerShares.Where(s => s.SettlementId == share.SettlementId && s != share))
                    {
                        otherShare.RawMaterials = 0;
                    }
                    break;
                case WorkshopMissionType.Brawl:
                    InformationManager.DisplayMessage(new InformationMessage($"Your workshop was destroyed and confiscated by saboteurs! (No intervention)", Colors.Red));
                    RemoveShareCompletely(share);
                    break;
                case WorkshopMissionType.Duel:
                    InformationManager.DisplayMessage(new InformationMessage($"Garrison Commander confiscated their property! You can complain to the Lord within 24 hours.", Colors.Red));
                    share.IsConfiscatedAndWaitingForSale = true;
                    share.ConfiscationEndTime = CampaignTime.Now + CampaignTime.Hours(24f);
                    if (!PendingDuelComplaints.Contains(share.SettlementId))
                    {
                        PendingDuelComplaints.Add(share.SettlementId);
                    }
                    break;
                case WorkshopMissionType.Inspector:
                    int inspectorFine = 3000 + MBRandom.RandomInt(0, 10) * 100 + MBRandom.RandomInt(0, 9) * 10 + MBRandom.RandomInt(0, 9);
                    InformationManager.DisplayMessage(new InformationMessage($"The inspector imposed heavy fines and confiscated your {inspectorFine} Denar! (No intervention)", Colors.Red));
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, inspectorFine, true);
                    break;
            }
        }

        private void StartActionMission(WorkshopMissionType type, VirtualWorkshopShare share, Settlement town)
        {
            var sceneName = town.LocationComplex.GetScene("center", 1);
            if (type == WorkshopMissionType.Duel) sceneName = town.LocationComplex.GetScene("arena", 1);

            MissionInitializerRecord rec = new MissionInitializerRecord(sceneName);
            rec.DoNotUseLoadingScreen = false;

            Mission mission = TaleWorlds.MountAndBlade.MissionState.OpenNew("WorkshopActionMission",
                  rec,
                  (m) => new MissionBehavior[]
                  {
                      new MissionOptionsComponent(),
                      new CampaignMissionComponent(),
                      new MissionAgentLookHandler(),
                      new MissionConversationLogic(),
                      new MissionFacialAnimationHandler(),
                      new MissionAgentPanicHandler(),
                      new AgentHumanAILogic(),
                      new SandBox.Missions.MissionLogics.MissionAgentHandler(),
                      new WorkshopActionMissionsLogic(type, share),
                      SandBoxViewCreator.CreateMissionConversationView(m),
                      ViewCreator.CreateMissionMainAgentEquipmentController(m),
                      ViewCreator.CreateMissionAgentStatusUIHandler(m),
                      SandBoxViewCreator.CreateMissionNameMarkerUIHandler(m),
                      ViewCreator.CreateMissionSingleplayerEscapeMenu(false),
                      ViewCreator.CreateMissionLeaveView()
                  }, true, true);
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (newOwner != null && newOwner.MapFaction == Hero.MainHero.MapFaction)
            {
                if (_confiscatedTownIds.Contains(settlement.StringId))
                {
                    InformationManager.DisplayMessage(new InformationMessage($"You can get back your confiscated property in your city {settlement.Name}!", Colors.Green));
                }
            }
        }

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            foreach (var share in _playerShares)
            {
                share.IsWarTaxed = false;
            }
        }

        private void ShowWarConfiscationMenu(VirtualWorkshopShare share, Settlement settlement)
        {
            List<InquiryElement> elements = new List<InquiryElement>();
            elements.Add(new InquiryElement("tax", new TaleWorlds.Localization.TextObject("{=rad_auto_126}Pay War Tax (-60% Income)").ToString(), null));
            elements.Add(new InquiryElement("sale", new TaleWorlds.Localization.TextObject("{=rad_auto_127}Urgent Sale (+35% Value, 5250 Denars)").ToString(), null));
            elements.Add(new InquiryElement("confiscate", new TaleWorlds.Localization.TextObject("{=rad_auto_128}Accept Confiscation (Gain Cause for War)").ToString(), null));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TaleWorlds.Localization.TextObject("{=rad_auto_130}War Confiscation (Seizure)").ToString(),
                $"Your workshop stake in {settlement.Name} is at risk due to war!",
                elements,
                false,
                1,
                1,
                new TaleWorlds.Localization.TextObject("{=rad_btn_accept}Confirm").ToString(),
                "",
                args =>
                {
                    string choice = args[0].Identifier as string;
                    if (choice == "tax")
                    {
                        share.IsWarTaxed = true;
                        InformationManager.DisplayMessage(new InformationMessage($"You pay war tax in your city {settlement.Name}.", Colors.Yellow));
                    }
                    else if (choice == "sale")
                    {
                        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 5250, true);
                        _playerShares.Remove(share);
                    }
                    else if (choice == "confiscate")
                    {
                        _playerShares.Remove(share);
                        if (!_confiscatedTownIds.Contains(settlement.StringId))
                        {
                            _confiscatedTownIds.Add(settlement.StringId);
                        }
                        InformationManager.DisplayMessage(new InformationMessage($"You have gained a Cause of War (Casus Belli) for unjust confiscation in your city {settlement.Name}.", Colors.Red));
                    }
                },
                null));
        }
    }

    [HarmonyPatch(typeof(DefaultWorkshopModel), "GetMaxWorkshopCountForClanTier")]
    public class PatchMaxWorkshopCount
    {
        static bool Prefix(int tier, ref int __result)
        {
            try
            {
                if (Hero.MainHero == null || DefaultSkills.Trade == null) return true;
                int tradeSkill = Hero.MainHero.GetSkillValue(DefaultSkills.Trade);
                __result = 3 + (tradeSkill / 25);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.WorkshopsCampaignBehavior), "RunTownWorkshop")]
    public class PatchRunTownWorkshop
    {
        private static System.Reflection.MethodInfo _getDataMethod;

        static bool Prefix(Town townComponent, Workshop workshop, object __instance)
        {
            if (workshop == null || workshop.WorkshopType == null) return false;

            try
            {
                if (workshop.Settlement == null || workshop.Settlement.Town == null) return false;
            }
            catch
            {
                return false;
            }

            if (workshop.Owner == Hero.MainHero)
            {
                try
                {
                    if (_getDataMethod == null)
                    {
                        _getDataMethod = __instance.GetType().GetMethod("GetDataOfWorkshop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                    if (_getDataMethod != null)
                    {
                        var data = _getDataMethod.Invoke(__instance, new object[] { workshop });
                        if (data == null)
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                return null;
            }
            return null;
        }
    }

    // ==========================================
    // Workshop Shares UI
    // ==========================================
    public class ShareItemVM : TaleWorlds.Library.ViewModel
    {
        private string _name;
        private string _shareId;
        private System.Action<string> _onSelect;

        [TaleWorlds.Library.DataSourceProperty]
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChangedWithValue(value, "Name"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public string ShareId { get => _shareId; set { if (_shareId != value) { _shareId = value; OnPropertyChangedWithValue(value, "ShareId"); } } }

        public ShareItemVM(string name, string id, System.Action<string> onSelect)
        {
            Name = name;
            ShareId = id;
            _onSelect = onSelect;
        }

        public void ExecuteSelectShare()
        {
            _onSelect?.Invoke(ShareId);
        }
    }

    
}
