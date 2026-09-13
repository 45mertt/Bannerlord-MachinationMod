using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Helpers;

namespace RebellionsAndDemographics
{
    public class SiegeCompanionBehavior : CampaignBehaviorBase
    {
        public static SiegeCompanionBehavior Instance { get; private set; }
        public SiegeCompanionData Data { get; private set; }

        public SiegeCompanionBehavior()
        {
            Instance = this;
            Data = new SiegeCompanionData();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            var specs = Data.CompanionSpecs;
            var levels = Data.CompanionLevels;
            var trainTimes = Data.CompanionTrainingEndTime;
            var trainSettlements = Data.CompanionTrainingSettlement;

            dataStore.SyncData("rad_siege_comp_specs", ref specs);
            dataStore.SyncData("rad_siege_comp_levels", ref levels);
            dataStore.SyncData("rad_siege_comp_train_time", ref trainTimes);
            dataStore.SyncData("rad_siege_comp_train_stl", ref trainSettlements);

            if (specs != null) Data.CompanionSpecs = specs;
            if (levels != null) Data.CompanionLevels = levels;
            if (trainTimes != null) Data.CompanionTrainingEndTime = trainTimes;
            if (trainSettlements != null) Data.CompanionTrainingSettlement = trainSettlements;
        }

        private void OnHourlyTick()
        {
            List<string> completed = new List<string>();
            
            foreach (var kvp in Data.CompanionTrainingEndTime)
            {
                if (kvp.Value.IsPast)
                {
                    completed.Add(kvp.Key);
                }
            }

            foreach (var heroId in completed)
            {
                Data.CompanionTrainingEndTime.Remove(heroId);
                
                // Level up
                int currentLvl = Data.GetLevel(heroId);
                if (currentLvl < 5)
                {
                    Data.CompanionLevels[heroId] = currentLvl + 1;
                }

                Hero hero = Hero.AllAliveHeroes.Find(h => h.StringId == heroId);
                string settlementName = "a nearby town";
                if (Data.CompanionTrainingSettlement.TryGetValue(heroId, out string stlId))
                {
                    Settlement stl = Settlement.Find(stlId);
                    if (stl != null) settlementName = stl.Name.ToString();
                    Data.CompanionTrainingSettlement.Remove(heroId);
                }

                if (hero != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_train_done}" + hero.Name.ToString() + " has completed their siege training and is waiting for you at " + settlementName + ".")
                        .ToString(),
                        Colors.Green));
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Talk to companion to start training
            starter.AddPlayerLine("rad_siege_spec_ask", "hero_main_options", "rad_siege_spec_menu",
                "{=rad_siege_ask}I want to send you to siege engineering training.",
                () => {
                    var companion = Hero.OneToOneConversationHero;
                    if (companion == null || !companion.IsPlayerCompanion) return false;
                    
                    // Don't show if already max level or currently training
                    if (Data.CompanionTrainingEndTime.ContainsKey(companion.StringId)) return false;
                    if (Data.GetLevel(companion.StringId) >= 5) return false;
                    
                    return true;
                }, null);

            starter.AddDialogLine("rad_siege_spec_menu", "rad_siege_spec_menu", "rad_siege_spec_options",
                "{=rad_siege_menu}Alright. It will take 7 days and cost gold. What should I specialize in?",
                () => {
                    var heroId = Hero.OneToOneConversationHero.StringId;
                    int currentLvl = Data.GetLevel(heroId);
                    int cost = GetTrainingCost(currentLvl);
                    MBTextManager.SetTextVariable("TRAINING_COST", cost);
                    MBTextManager.SetTextVariable("CURRENT_LEVEL", currentLvl);
                    MBTextManager.SetTextVariable("NEXT_LEVEL", currentLvl + 1);
                    MBTextManager.SetTextVariable("CURRENT_LEVEL", currentLvl);
                    MBTextManager.SetTextVariable("NEXT_LEVEL", currentLvl + 1);
                    return true;
                }, null);

            // Options
            AddTrainingOption(starter, "rad_siege_spec_ram", "Battering Ram", SiegeSpecialization.Ram);
            AddTrainingOption(starter, "rad_siege_spec_tower", "Siege Tower", SiegeSpecialization.Tower);
            AddTrainingOption(starter, "rad_siege_spec_ballista", "Ballista", SiegeSpecialization.Ballista);
            AddTrainingOption(starter, "rad_siege_spec_onager", "Onager", SiegeSpecialization.Onager);
            AddTrainingOption(starter, "rad_siege_spec_trebuchet", "Trebuchet", SiegeSpecialization.Trebuchet);

            // Level Up Option (if already has a spec)
            starter.AddPlayerLine("rad_siege_spec_upgrade", "rad_siege_spec_options", "rad_siege_spec_confirm",
                "{=rad_siege_upg}Advance your current specialization to Level {NEXT_LEVEL} ({TRAINING_COST} Denars).",
                () => {
                    var heroId = Hero.OneToOneConversationHero.StringId;
                    var spec = Data.CompanionSpecs.ContainsKey(heroId) ? Data.CompanionSpecs[heroId] : SiegeSpecialization.None;
                    if (spec == SiegeSpecialization.None) return false;
                    
                    int cost = GetTrainingCost(Data.GetLevel(heroId));
                    return Hero.MainHero.Gold >= cost;
                }, 
                () => {
                    var hero = Hero.OneToOneConversationHero;
                    StartTraining(hero, Data.CompanionSpecs[hero.StringId]);
                });

            // Insufficient Gold
            starter.AddPlayerLine("rad_siege_spec_no_gold", "rad_siege_spec_options", "lord_pretalk",
                "{=rad_siege_no_gold}I don't have {TRAINING_COST} Denars right now.",
                () => {
                    var heroId = Hero.OneToOneConversationHero.StringId;
                    return Hero.MainHero.Gold < GetTrainingCost(Data.GetLevel(heroId));
                }, null);

            // Cancel
            starter.AddPlayerLine("rad_siege_spec_cancel", "rad_siege_spec_options", "lord_pretalk",
                "{=rad_siege_cancel}Nevermind, we need you here.",
                null, null);

            // Confirm Dialog
            starter.AddDialogLine("rad_siege_spec_confirm", "rad_siege_spec_confirm", "close_window",
                "{=rad_siege_conf}I'll pack my things. See you in 7 days.",
                null, null);
                
            // Status Query
            starter.AddPlayerLine("rad_siege_status_ask", "hero_main_options", "rad_siege_status_response",
                "{=rad_siege_stat_ask}What siege engines can we build right now?",
                () => {
                    return MobileParty.MainParty != null && MobileParty.MainParty.SiegeEvent != null && Hero.OneToOneConversationHero.IsPlayerCompanion;
                }, null);

            starter.AddDialogLine("rad_siege_status_response", "rad_siege_status_response", "hero_main_options",
                "{=rad_siege_stat_res}Check the siege camp menu, commander. (Engines without specialists in our party are locked. Higher level specialists build faster and stronger engines.)",
                null, null);
        }

        private void AddTrainingOption(CampaignGameStarter starter, string id, string name, SiegeSpecialization spec)
        {
            starter.AddPlayerLine(id, "rad_siege_spec_options", "rad_siege_spec_confirm",
                "{=rad_siege_opt}Train as a " + name + " specialist [Level 1] ({TRAINING_COST} Denars).",
                () => {
                    var heroId = Hero.OneToOneConversationHero.StringId;
                    // Only show if they don't have a spec yet
                    if (Data.CompanionSpecs.ContainsKey(heroId) && Data.CompanionSpecs[heroId] != SiegeSpecialization.None) return false;
                    
                    int cost = GetTrainingCost(Data.GetLevel(heroId));
                    return Hero.MainHero.Gold >= cost;
                },
                () => {
                    StartTraining(Hero.OneToOneConversationHero, spec);
                });
        }

        private int GetTrainingCost(int currentLevel)
        {
            switch (currentLevel)
            {
                case 0: return 2000;
                case 1: return 5000;
                case 2: return 10000;
                case 3: return 20000;
                case 4: return 40000;
                default: return 999999;
            }
        }

        private void StartTraining(Hero companion, SiegeSpecialization spec)
        {
            int currentLvl = Data.GetLevel(companion.StringId);
            int cost = GetTrainingCost(currentLvl);
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);

            Data.AssignSpecialization(companion.StringId, spec);
            Data.CompanionTrainingEndTime[companion.StringId] = CampaignTime.DaysFromNow(7f);
            
            // Find nearest town
            Settlement nearestTown = null;
            float closestDist = float.MaxValue;
            foreach (var town in Town.AllTowns)
            {
                float dist = town.Settlement.GatePosition.DistanceSquared(MobileParty.MainParty.GetPosition2D);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearestTown = town.Settlement;
                }
            }
            if (nearestTown == null) nearestTown = Settlement.All[0]; // fallback
            
            Data.CompanionTrainingSettlement[companion.StringId] = nearestTown.StringId;

            // Remove from party and send to town
            if (companion.PartyBelongedTo != null)
            {
                companion.PartyBelongedTo.MemberRoster.RemoveTroop(companion.CharacterObject);
            }
            
            companion.ChangeState(Hero.CharacterStates.Active);
            EnterSettlementAction.ApplyForCharacterOnly(companion, nearestTown);
        }
    }
}
