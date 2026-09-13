using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using SandBox.View;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class NordSturgiaFestivalBehavior : CampaignBehaviorBase
    {
        private Dictionary<Kingdom, Settlement> _festivalLocations = new Dictionary<Kingdom, Settlement>();
        public Dictionary<Kingdom, Settlement> FestivalLocations 
        { 
            get { return _festivalLocations ?? (_festivalLocations = new Dictionary<Kingdom, Settlement>()); }
            set { _festivalLocations = value; }
        }

        private Dictionary<Kingdom, bool> _isFestivalActive = new Dictionary<Kingdom, bool>();
        public Dictionary<Kingdom, bool> IsFestivalActive 
        { 
            get { return _isFestivalActive ?? (_isFestivalActive = new Dictionary<Kingdom, bool>()); }
            set { _isFestivalActive = value; }
        }

        public override void RegisterEvents()
        {
            // FESTİVAL İPTAL: Şimdilik kapatıldı.
            // CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            // CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in FestivalLocations.ToList()) 
                { 
                    if (pair.Key == null || pair.Key.IsEliminated || string.IsNullOrEmpty(pair.Key.StringId) || !Kingdom.All.Contains(pair.Key) || pair.Value == null) 
                    {
                        FestivalLocations.Remove(pair.Key); 
                    }
                }
                foreach (var pair in IsFestivalActive.ToList()) 
                { 
                    if (pair.Key == null || pair.Key.IsEliminated || string.IsNullOrEmpty(pair.Key.StringId) || !Kingdom.All.Contains(pair.Key)) 
                    {
                        IsFestivalActive.Remove(pair.Key); 
                    }
                }
            }
            dataStore.SyncData("_festivalLocations", ref _festivalLocations);
            dataStore.SyncData("_isFestivalActive", ref _isFestivalActive);
            
            if (_festivalLocations == null) _festivalLocations = new Dictionary<Kingdom, Settlement>();
            if (_isFestivalActive == null) _isFestivalActive = new Dictionary<Kingdom, bool>();
        }

        private void OnDailyTick()
        {
            // First day of each season triggers a festival
            if (CampaignTime.Now.GetDayOfSeason == 1)
            {
                TriggerFestival();
            }
        }

        public void TriggerFestival(Kingdom kingdom)
        {
            if (_isFestivalActive.ContainsKey(kingdom) && _isFestivalActive[kingdom])
                return; // Already active, prevent double trigger

            if (IsNordOrSturgia(kingdom))
            {
                var strongestTown = kingdom.Settlements.Where(s => s.IsTown).OrderByDescending(s => s.Town.Prosperity + (s.Town.GarrisonParty?.Party.NumberOfAllMembers ?? 0)).FirstOrDefault();
                
                if (strongestTown != null)
                {
                    _festivalLocations[kingdom] = strongestTown;
                    _isFestivalActive[kingdom] = true;
                    
                    InformationManager.ShowInquiry(new InquiryData("Big Holiday Announcement!", $"A Great Holiday has been declared in the kingdom of {kingdom.Name}. All the lords are heading to the city of {strongestTown.Name}.", true, false, "Tamam", "", null, null));
                    
                    // Force AI lords to travel to the strongest town
                    foreach (var clan in kingdom.Clans)
                    {
                        foreach (var lordParty in clan.WarPartyComponents)
                        {
                            if (lordParty.MobileParty != MobileParty.MainParty && lordParty.MobileParty.CurrentSettlement != strongestTown)
                            {
                                SetPartyAiAction.GetActionForVisitingSettlement(lordParty.MobileParty, strongestTown, MobileParty.NavigationType.None, false, false);
                            }
                        }
                    }
                }
            }
        }

        private void TriggerFestival()
        {
            foreach (var kingdom in Kingdom.All)
            {
                TriggerFestival(kingdom);
            }
        }

        private bool IsNordOrSturgia(Kingdom kingdom)
        {
            var cid = kingdom.Culture.StringId.ToLower();
            return cid.Contains("sturgia") || cid.Contains("nord");
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Festival için Savaş Konseyi paterni
            starter.AddGameMenuOption("town", "town_festival", "Go to the Festival Area",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    var kingdom = Clan.PlayerClan?.Kingdom;
                    if (kingdom == null || !IsNordOrSturgia(kingdom)) return false;
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    if (!_festivalLocations.ContainsKey(kingdom)) return false;
                    if (Settlement.CurrentSettlement != _festivalLocations[kingdom]) return false;

                    if (!IsFestivalActiveForMainHero())
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TaleWorlds.Localization.TextObject("There are currently no active festivals in this city.");
                    }
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("festival_gathering_wait");
                },
                false, 4);

            starter.AddWaitGameMenu("festival_gathering_wait", "The lords gathered in the festival area...",
                (MenuCallbackArgs args) => { args.MenuContext.GameMenu.StartWait(); },
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenFestivalMission(),
                (MenuCallbackArgs args, CampaignTime dt) => { },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            starter.AddGameMenuOption("festival_gathering_wait", "festival_enter", "Join the Assembly",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenFestivalMission(), true);

            starter.AddGameMenuOption("festival_gathering_wait", "festival_leave", "leave",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                (MenuCallbackArgs args) => { GameMenu.SwitchToMenu("town"); }, true);

            // Bribe with Gold
            starter.AddPlayerLine("nord_festival_bribe_gold", "hero_main_options", "nord_festival_bribe_gold_response", "My lord, enjoy the feast. I also offer you 5000 gold as a small gift. I need your support on that bill on the agenda.", 
                () => IsFestivalActiveForMainHero() && Hero.MainHero.Gold >= 5000 && HasAgenda(), 
                () => { GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, 5000); });
                
            starter.AddDialogLine("nord_festival_bribe_gold_response", "nord_festival_bribe_gold", "lord_pretalk", "Your generosity is legendary! You can be sure that I will be with you in the vote.", 
                () => true, 
                () => { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 5); });

            // Bribe with Battlefield Loot
            starter.AddPlayerLine("nord_festival_bribe_loot", "hero_main_options", "nord_festival_bribe_loot_response", "I am leading a great caravan of war spoils to you. If you support me in the legislative vote, these spoils are yours.", 
                () => IsFestivalActiveForMainHero() && HasBattlefieldLoot() && HasAgenda(), 
                () => { ConsumeBattlefieldLoot(); });
                
            starter.AddDialogLine("nord_festival_bribe_loot_response", "nord_festival_bribe_loot", "lord_pretalk", "{=rad_nord_01}Spoils of war? Sturgian / Nord blood loves this! I am behind you on the law proposal.", 
                () => true, 
                () => { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 10); });
        }

        private void OpenFestivalMission()
        {
            try
            {
                var settlement = Settlement.CurrentSettlement;
                string sceneName = "empire_town_a";
                if (settlement != null && settlement.LocationComplex != null)
                {
                    var loc = settlement.LocationComplex.GetLocationWithId("center");
                    if (loc != null) sceneName = loc.GetSceneName(1);
                }

                MissionLogic[] logicList = new MissionLogic[]
                {
                    new MissionOptionsComponent(),
                    new CampaignMissionComponent(),
                    new MissionBasicTeamLogic(),
                    new FestivalTownMissionLogic(settlement),
                    
                    new MissionConversationLogic(),
                    new MissionAgentLookHandler(),
                    new MissionSettlementPrepareLogic(),
                    new MissionAgentHandler(),
                    new AgentHumanAILogic()
                };

                Mission mission = MissionState.OpenNew(
                    "FestivalCouncil",
                    SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.Town),
                    (m) => logicList,
                    true, true
                );

                if (mission != null)
                {
                    mission.AddMissionBehavior(SandBoxViewCreator.CreateMissionConversationView(mission));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionMainAgentEquipmentController(mission));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionSingleplayerEscapeMenu(false));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionLeaveView());
                }
            }
            catch (System.Exception)
            {
                GameMenu.SwitchToMenu("town");
            }
        }

        private bool IsFestivalActiveForMainHero()
        {
            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom != null && IsNordOrSturgia(kingdom) && _isFestivalActive.ContainsKey(kingdom) && _isFestivalActive[kingdom])
            {
                if (MobileParty.MainParty.CurrentSettlement == _festivalLocations[kingdom])
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasAgenda()
        {
            var behavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
            if (behavior != null && Clan.PlayerClan?.Kingdom != null)
            {
                return behavior.HasAgenda(Clan.PlayerClan.Kingdom);
            }
            return false;
        }

        private bool HasBattlefieldLoot()
        {
            // We interact with ScavengerCampaignBehavior assuming it holds _battlefields
            // We don't have direct access to its private field, but we'll assume there is a public method
            // or we just simulate it for now. Since the user said "you can find it from the codes", and it's a private list `_battlefields`
            // we will need to use reflection if no public method exists. Let's assume reflection or a public method.
            // For the sake of safety, let's use reflection.
            var scavenger = Campaign.Current.GetCampaignBehavior<ScavengerCampaignBehavior>();
            if (scavenger != null)
            {
                var field = scavenger.GetType().GetField("_battlefields", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var list = field.GetValue(scavenger) as System.Collections.IList;
                    return list != null && list.Count > 0;
                }
            }
            return false;
        }

        private void ConsumeBattlefieldLoot()
        {
            var scavenger = Campaign.Current.GetCampaignBehavior<ScavengerCampaignBehavior>();
            if (scavenger != null)
            {
                var field = scavenger.GetType().GetField("_battlefields", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var list = field.GetValue(scavenger) as System.Collections.IList;
                    if (list != null && list.Count > 0)
                    {
                        list.RemoveAt(0); // Consume the first unlooted battlefield
                    }
                }
            }
        }
    }
}
