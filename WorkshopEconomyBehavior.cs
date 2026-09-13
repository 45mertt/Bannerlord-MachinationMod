using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    public class WorkshopEconomyBehavior : CampaignBehaviorBase
    {
        public static WorkshopEconomyBehavior Instance;

        private Dictionary<string, int> _npcWorkshopHealth = new Dictionary<string, int>();
        public Dictionary<string, int> NpcWorkshopHealth { get { return _npcWorkshopHealth ?? (_npcWorkshopHealth = new Dictionary<string, int>()); } set { _npcWorkshopHealth = value; } }

        private int _playerSabotageTokens = 0;
        
        private Dictionary<string, int> _sabotageDebuffDays = new Dictionary<string, int>();
        public Dictionary<string, int> SabotageDebuffDays { get { return _sabotageDebuffDays ?? (_sabotageDebuffDays = new Dictionary<string, int>()); } set { _sabotageDebuffDays = value; } }

        private List<string> _processedArmies = new List<string>();
        public List<string> ProcessedArmies { get { return _processedArmies ?? (_processedArmies = new List<string>()); } set { _processedArmies = value; } }

        private Dictionary<string, float> _supplyChainBonus = new Dictionary<string, float>();
        public Dictionary<string, float> SupplyChainBonus { get { return _supplyChainBonus ?? (_supplyChainBonus = new Dictionary<string, float>()); } set { _supplyChainBonus = value; } }

        private Dictionary<string, int> _monopolyWarningDays = new Dictionary<string, int>();
        public Dictionary<string, int> MonopolyWarningDays { get { return _monopolyWarningDays ?? (_monopolyWarningDays = new Dictionary<string, int>()); } set { _monopolyWarningDays = value; } }

        private int _dailyTickCounter = 0;

        public WorkshopEconomyBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in NpcWorkshopHealth.ToList()) { if (!(pair.Key != null)) NpcWorkshopHealth.Remove(pair.Key); }
                foreach (var pair in SabotageDebuffDays.ToList()) { if (!(pair.Key != null)) SabotageDebuffDays.Remove(pair.Key); }
                foreach (var pair in SupplyChainBonus.ToList()) { if (!(pair.Key != null)) SupplyChainBonus.Remove(pair.Key); }
                foreach (var pair in MonopolyWarningDays.ToList()) { if (!(pair.Key != null)) MonopolyWarningDays.Remove(pair.Key); }
                
                var armies = ProcessedArmies;
                // armies.RemoveAll(x => x == null);
            }

            dataStore.SyncData("_npcWorkshopHealth", ref _npcWorkshopHealth);
            dataStore.SyncData("_playerSabotageTokens", ref _playerSabotageTokens);
            dataStore.SyncData("_sabotageDebuffDays", ref _sabotageDebuffDays);
            dataStore.SyncData("_processedArmies", ref _processedArmies);
            dataStore.SyncData("_supplyChainBonus", ref _supplyChainBonus);
            dataStore.SyncData("_monopolyWarningDays", ref _monopolyWarningDays);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_workshop_menu", "workshop_sabotage", "{=rad_auto_131}Sabotage Opponent (1,000 Denars)",
                (args) =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    bool hasWorkshop = Settlement.CurrentSettlement.Town.Workshops.Any(w => w.Owner == Hero.MainHero);
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    return hasWorkshop && Hero.MainHero.Gold >= 1000;
                },
                (args) =>
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, true);
                    _playerSabotageTokens++;
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_132}Sabotage line has been established!").ToString(), Colors.Green));
                });
        }

        private void OnDailyTick()
        {
            _dailyTickCounter++;
            bool isWeekly = (_dailyTickCounter % 7 == 0);

            // Decrement sabotage debuff days
            var debuffKeys = _sabotageDebuffDays.Keys.ToList();
            foreach (var k in debuffKeys)
            {
                if (_sabotageDebuffDays[k] > 0)
                {
                    _sabotageDebuffDays[k]--;
                }
            }

            // Mechanic 1: Price Dumping
            foreach (var town in Town.AllTowns)
            {
                var playerShares = WorkshopKingdomBehavior.Instance?.GetSharesInSettlement(town.Settlement.StringId) ?? new List<VirtualWorkshopShare>();
                int totalWorkshops = town.Workshops.Length;
                
                if (playerShares.Count > 0)
                {
                    foreach (var ps in playerShares)
                    {
                        int level = ps.Level;

                        var npcWorkshops = town.Workshops.Where(w => w.Owner != Hero.MainHero && w.WorkshopType != null && w.WorkshopType.StringId == ps.WorkshopTypeId).ToList();
                        foreach (var nw in npcWorkshops)
                        {
                            string key = town.Settlement.StringId + "_" + nw.WorkshopType.StringId;
                            if (!_npcWorkshopHealth.ContainsKey(key))
                            {
                                _npcWorkshopHealth[key] = 100;
                            }
                            
                            if (MBRandom.RandomFloat < 0.1f) // 10% chance per day to lose health
                            {
                                _npcWorkshopHealth[key] -= 10;
                            }

                            if (_npcWorkshopHealth[key] <= 0)
                            {
                                _npcWorkshopHealth[key] = 100; // Reset just in case
                                
                                int maxShares = town.Workshops != null ? town.Workshops.Length : 4;
                                if (maxShares > 4) maxShares = 4;
                                var localShares = WorkshopKingdomBehavior.Instance?.GetSharesInSettlement(town.Settlement.StringId);
                                
                                int globalShares = WorkshopKingdomBehavior.Instance?.GetAllShares().Count ?? 0;
                                int globalLimit = Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(Hero.MainHero.Clan.Tier);

                                if ((localShares == null || localShares.Count < maxShares) && globalShares < globalLimit)
                                {
                                    ShowBuyoutInquiry(nw, town);
                                }
                            }
                        }
                    }
                }
            }

            // Mechanic 2: Guild Sabotage
            if (isWeekly)
            {
                if (_playerSabotageTokens > 0 && MBRandom.RandomFloat < 0.6f)
                {
                    // Find random NPC workshop in a town where player has workshop
                    var eligibleTowns = Town.AllTowns.Where(t => t.Workshops.Any(w => w.Owner == Hero.MainHero)).ToList();
                    if (eligibleTowns.Count > 0)
                    {
                        var t = eligibleTowns.GetRandomElementInefficiently();
                        var npcWs = t.Workshops.Where(w => w.Owner != Hero.MainHero).ToList();
                        if (npcWs.Count > 0)
                        {
                            var target = npcWs.GetRandomElementInefficiently();
                            string k = t.Settlement.StringId + "_" + target.WorkshopType.StringId;
                            _npcWorkshopHealth[k] = 30;
                            _playerSabotageTokens--;
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_134}Sabotage successful!").ToString() + target.WorkshopType.Name.ToString() + "workshop (" + t.Name.ToString() + ") was severely damaged.", Colors.Green));
                        }
                    }
                }

                foreach (var town in Town.AllTowns)
                {
                    if (town.Workshops.Any(w => w.Owner == Hero.MainHero))
                    {
                        if (MBRandom.RandomFloat < 0.3f)
                        {
                            _sabotageDebuffDays[town.Settlement.StringId] = 7;
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_135}Rival guilds").ToString() + town.Name.ToString() + "He is sabotaging your business in his city!", Colors.Red));
                        }
                    }
                }
            }

            // Mechanic 3: Shadow Tenders
            foreach (var party in MobileParty.All)
            {
                if (party.Army != null && party.Army.LeaderParty == party && party.CurrentSettlement != null && party.CurrentSettlement.IsTown)
                {
                    if (!_processedArmies.Contains(party.StringId))
                    {
                        int playerWsCount = party.CurrentSettlement.Town.Workshops.Count(w => w.Owner == Hero.MainHero);
                        if (playerWsCount > 0)
                        {
                            int playerScore = playerWsCount * 3 + MBRandom.RandomInt(1, 6);
                            int npcScore = MBRandom.RandomInt(1, 11);

                            if (playerScore > npcScore)
                            {
                                int amount = MBRandom.RandomInt(8000, 20000);
                                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, true);
                                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_136}Army supply tender was won! +").ToString() + amount.ToString() + "Dinar", Colors.Green));
                            }
                        }
                        _processedArmies.Add(party.StringId);
                    }
                }
            }
        }

        private void ShowBuyoutInquiry(Workshop nw, Town town)
        {
            int cost = 15000;
            InformationManager.ShowInquiry(new InquiryData(new TaleWorlds.Localization.TextObject("{=rad_auto_141}Workshop Collapse").ToString(), 
                town.Name.ToString() + "rival in city" + nw.WorkshopType.Name.ToString() + "on the verge of bankruptcy." + cost + "Would you like to buy shares of the workshop in exchange for Denars?",
                true, true, "Evet", "No", 
                () => {
                    if (Hero.MainHero.Gold >= cost)
                    {
                        var maxShares = town.Workshops != null ? town.Workshops.Length : 4;
                        if (maxShares > 4) maxShares = 4;
                        var localShares = WorkshopKingdomBehavior.Instance?.GetSharesInSettlement(town.Settlement.StringId);
                        
                        if (localShares != null && localShares.Count >= maxShares)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_137}You have reached the maximum share limit in this city!").ToString(), Colors.Red));
                            return;
                        }

                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);
                        
                        string newTag = System.Guid.NewGuid().ToString("N");
                        var newShare = new VirtualWorkshopShare()
                        {
                            SettlementId = town.Settlement.StringId,
                            WorkshopTypeId = nw.WorkshopType?.StringId ?? "unknown",
                            Name = nw.WorkshopType?.Name.ToString() ?? "Workshop Share",
                            Trait = "LocalMonopoly",
                            Tag = newTag,
                            Level = 1
                        };
                        WorkshopKingdomBehavior.Instance?.AddPlayerShare(newShare);

                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_138}Workshop shares have been purchased successfully!").ToString(), Colors.Green));
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_139}There is not enough gold.").ToString(), Colors.Red));
                    }
                }, null));
        }

        private void OnVillageRaided(Village village)
        {
            if (village.VillageType == null || village.VillageType.PrimaryProduction == null) return;
            string prodName = village.VillageType.PrimaryProduction.StringId;

            foreach (var town in Town.AllTowns)
            {
                foreach (var ws in town.Workshops)
                {
                    if (ws.WorkshopType != null && ws.WorkshopType.StringId.Contains(prodName))
                    {
                        string key = town.Settlement.StringId + "_" + ws.WorkshopType.StringId;
                        if (ws.Owner == Hero.MainHero)
                        {
                            _supplyChainBonus[key] = 1.5f;
                            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_140}Village plunder created a raw material shortage! 14 days +50% profit bonus in your workshops.").ToString(), Colors.Green));
                        }
                        else
                        {
                            if (!_npcWorkshopHealth.ContainsKey(key)) _npcWorkshopHealth[key] = 100;
                            _npcWorkshopHealth[key] -= 30;
                        }
                    }
                }
            }
        }

        public float GetEconomyMultiplier(Workshop w)
        {
            string key = w.Settlement.StringId + "_" + (w.WorkshopType?.StringId ?? "");
            float mult = 1.0f;
            if (_sabotageDebuffDays.TryGetValue(w.Settlement.StringId, out int debuffDays) && debuffDays > 0)
                mult *= 0.6f;
            if (_supplyChainBonus.TryGetValue(key, out float bonus))
                mult *= bonus;
            return mult;
        }
    }
}
