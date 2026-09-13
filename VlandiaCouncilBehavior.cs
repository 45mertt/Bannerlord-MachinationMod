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

namespace RebellionsAndDemographics
{
    public class VlandiaCouncilBehavior : CampaignBehaviorBase
    {
        private float _lastCouncilTime = 0f;

        public override void RegisterEvents()
        {
            // KONSEY İPTAL: Şimdilik kapatıldı.
            // CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            // CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_lastCouncilTime", ref _lastCouncilTime);
        }

        private void OnDailyTick()
        {
            var currentTime = (float)CampaignTime.Now.ToDays;
            if (currentTime - _lastCouncilTime > 60f) // Trigger every ~60 days (Long period)
            {
                TriggerVlandiaCouncil();
                _lastCouncilTime = currentTime;
            }
        }

        public void TriggerVlandiaCouncil(Kingdom kingdom)
        {
            if (kingdom.Culture.StringId.ToLower().Contains("vlandia"))
            {
                var strongestTown = kingdom.Settlements.Where(s => s.IsTown).OrderByDescending(s => s.Town.Prosperity).FirstOrDefault();
                if (strongestTown != null)
                {
                    InformationManager.ShowInquiry(new InquiryData("Magnum Concilium Collects", $"The Feudal Grand Council of {kingdom.Name} meets in the city of {strongestTown.Name}. You must use your power of persuasion to pass laws.", true, false, "Tamam", "", null, null));
                    
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

        private void TriggerVlandiaCouncil()
        {
            foreach (var kingdom in Kingdom.All)
            {
                var agendaBehavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
                if (agendaBehavior != null && agendaBehavior.HasAgenda(kingdom))
                {
                    TriggerVlandiaCouncil(kingdom);
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "town_council", "Join the Council",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    var kingdom = Clan.PlayerClan?.Kingdom;
                    if (kingdom == null || !kingdom.Culture.StringId.ToLower().Contains("vlandia")) return false;
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    var strongestTown = kingdom.Settlements.Where(s => s.IsTown).OrderByDescending(s => s.Town.Prosperity).FirstOrDefault();
                    if (strongestTown == null || Settlement.CurrentSettlement != strongestTown) return false;

                    var agendaBehavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
                    if (agendaBehavior == null || !agendaBehavior.HasAgenda(kingdom))
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("There are currently no active council meetings in this city.");
                    }
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("council_gathering_wait");
                },
                false, 4);

            starter.AddWaitGameMenu("council_gathering_wait", "The council is meeting...",
                (MenuCallbackArgs args) => { args.MenuContext.GameMenu.StartWait(); },
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenCouncilMission(),
                (MenuCallbackArgs args, CampaignTime dt) => { },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            starter.AddGameMenuOption("council_gathering_wait", "council_enter", "Enter the Council",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenCouncilMission(), true);

            starter.AddGameMenuOption("council_gathering_wait", "council_leave", "leave",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                (MenuCallbackArgs args) => { GameMenu.SwitchToMenu("town"); }, true);

            // Dialogs for Vlandia Persuasion
            starter.AddPlayerLine("vlandia_council_persuade", "hero_main_options", "vlandia_council_persuade_response", "My lord, the bill on the table is essential to the future of our kingdom. I ask for your support in this matter.", 
                () => IsVlandiaCouncilActiveForMainHero() && HasAgenda() && 
                      Hero.OneToOneConversationHero != null && 
                      Hero.OneToOneConversationHero.MapFaction == Clan.PlayerClan.Kingdom && 
                      Hero.OneToOneConversationHero.Clan != Clan.PlayerClan && 
                      Hero.MainHero != Clan.PlayerClan.Kingdom.Leader && 
                      Hero.OneToOneConversationHero != Clan.PlayerClan.Kingdom.Leader, null);
                
            starter.AddDialogLine("vlandia_council_persuade_response", "vlandia_council_persuade", "lord_pretalk", "I'm not sure... You may be right, but you must also look after the lords' interests.", 
                () => true, 
                () => {
                    // Logic to start native PersuasionTask would go here.
                    // For simplicity, let's do a basic charm check simulation.
                    int charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
                    bool success = MBRandom.RandomInt(0, 100) < (charm / 2 + 20);
                    
                    if (success)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"{Hero.OneToOneConversationHero.Name} is convinced!", Colors.Green));
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 2);
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"{Hero.OneToOneConversationHero.Name} wasn't convinced.", Colors.Red));
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -2);
                    }
                });

            // Dialog for forcing the policy
            starter.AddPlayerLine("vlandia_council_force", "hero_main_options", "vlandia_council_force_response", "(Force to Pass) Despite all objections, this law will pass! I am your king!", 
                () => IsVlandiaCouncilActiveForMainHero() && HasAgenda() && 
                      Hero.MainHero == Clan.PlayerClan?.Kingdom?.Leader && 
                      Hero.OneToOneConversationHero != null && 
                      Hero.OneToOneConversationHero.MapFaction == Clan.PlayerClan.Kingdom && 
                      Hero.OneToOneConversationHero.Clan != Clan.PlayerClan, null);
                
            starter.AddDialogLine("vlandia_council_force_response", "vlandia_council_force", "lord_pretalk", "This is tyranny! We're leaving the council!", 
                () => true, 
                () => {
                    IncreaseClanStress();
                });
        }
        
        private void IncreaseClanStress()
        {
            // The user requested to spike "Clan Stress" leading to civil war
            InformationManager.DisplayMessage(new InformationMessage("The lords abandoned the council! The clans' stress has increased drastically!", Colors.Red));
            
            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom != null)
            {
                foreach (var clan in kingdom.Clans)
                {
                    if (clan != kingdom.RulingClan && !clan.IsUnderMercenaryService)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, clan.Leader, -30);
                    }
                }
            }
            
            // Try to access RebellionCoreBehavior to spike _clanUnrest
            var rebellionCore = Campaign.Current.GetCampaignBehaviors<CampaignBehaviorBase>().FirstOrDefault(b => b.GetType().Name == "RebellionCoreBehavior");
            if (rebellionCore != null)
            {
                var field = rebellionCore.GetType().GetField("_clanUnrest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var dict = field.GetValue(rebellionCore) as Dictionary<Clan, float>;
                    if (dict != null)
                    {
                        foreach (var clan in dict.Keys.ToList())
                        {
                            if (clan.Kingdom == kingdom && clan != kingdom.RulingClan)
                            {
                                dict[clan] += 50f; // Spike stress
                            }
                        }
                    }
                }
            }
        }

        private void OpenCouncilMission()
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
                    "VlandiaCouncil",
                    SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.Town),
                    (m) => logicList, true, true
                );

                if (mission != null)
                {
                    mission.AddMissionBehavior(SandBoxViewCreator.CreateMissionConversationView(mission));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionMainAgentEquipmentController(mission));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionSingleplayerEscapeMenu(false));
                    mission.AddMissionBehavior(ViewCreator.CreateMissionLeaveView());
                }
            }
            catch (Exception) { GameMenu.SwitchToMenu("town"); }
        }

        private bool IsVlandiaCouncilActiveForMainHero()
        {
            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom != null && kingdom.Culture.StringId.ToLower().Contains("vlandia"))
            {
                return true; // Simplified for now. Should check location and time.
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
    }
}
