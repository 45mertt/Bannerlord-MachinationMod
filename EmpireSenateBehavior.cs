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
    public class EmpireSenateBehavior : CampaignBehaviorBase
    {
        private Dictionary<Kingdom, Settlement> _empireCapitals = new Dictionary<Kingdom, Settlement>();
        public Dictionary<Kingdom, Settlement> EmpireCapitals
        {
            get { return _empireCapitals ?? (_empireCapitals = new Dictionary<Kingdom, Settlement>()); }
            set { _empireCapitals = value; }
        }

        private Dictionary<Hero, int> _heroBlackmailMaterial = new Dictionary<Hero, int>();
        public Dictionary<Hero, int> HeroBlackmailMaterial
        {
            get { return _heroBlackmailMaterial ?? (_heroBlackmailMaterial = new Dictionary<Hero, int>()); }
            set { _heroBlackmailMaterial = value; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in EmpireCapitals.ToList())
                {
                    if (pair.Key == null || pair.Key.IsEliminated || string.IsNullOrEmpty(pair.Key.StringId) || !Kingdom.All.Contains(pair.Key) || pair.Value == null)
                    {
                        EmpireCapitals.Remove(pair.Key);
                    }
                }
                foreach (var pair in HeroBlackmailMaterial.ToList())
                {
                    if (pair.Key == null || pair.Key.IsDead)
                    {
                        HeroBlackmailMaterial.Remove(pair.Key);
                    }
                }
            }

            dataStore.SyncData("_empireCapitals", ref _empireCapitals);
            dataStore.SyncData("_heroBlackmailMaterial", ref _heroBlackmailMaterial);

            // Güvenlik: Kayýt yüklenirken (Loading) ilgili veriler null dönerse listeleri baþlat.
            if (dataStore.IsLoading)
            {
                if (_empireCapitals == null) _empireCapitals = new Dictionary<Kingdom, Settlement>();
                if (_heroBlackmailMaterial == null) _heroBlackmailMaterial = new Dictionary<Hero, int>();
            }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            InitializeCapitals();
            AddDialogs(starter);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            InitializeCapitals();
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "town_senate", "Join the Senate",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    var kingdom = Clan.PlayerClan?.Kingdom;
                    if (kingdom == null || !kingdom.Culture.StringId.Contains("empire")) return false;
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    if (!_empireCapitals.ContainsKey(kingdom)) return false;
                    if (Settlement.CurrentSettlement != _empireCapitals[kingdom]) return false;

                    var agendaBehavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
                    if (agendaBehavior == null || !agendaBehavior.HasAgenda(kingdom))
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("There are currently no active senate meetings in this city.");
                    }
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("senate_gathering_wait");
                },
                false, 4);

            starter.AddWaitGameMenu("senate_gathering_wait", "The Senate is meeting...",
                (MenuCallbackArgs args) => { args.MenuContext.GameMenu.StartWait(); },
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenSenateMission(),
                (MenuCallbackArgs args, CampaignTime dt) => { },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            starter.AddGameMenuOption("senate_gathering_wait", "senate_enter", "Enter the Senate",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Mission; return true; },
                (MenuCallbackArgs args) => OpenSenateMission(), true);

            starter.AddGameMenuOption("senate_gathering_wait", "senate_leave", "leave",
                (MenuCallbackArgs args) => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                (MenuCallbackArgs args) => { GameMenu.SwitchToMenu("town"); }, true);

            starter.AddPlayerLine("senate_blackmail_start", "hero_main_options", "senate_blackmail_offer", "I have some secrets... You must support me in this bill.",
                () => Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Culture.StringId.Contains("empire") && _heroBlackmailMaterial.ContainsKey(Hero.OneToOneConversationHero) && _heroBlackmailMaterial[Hero.OneToOneConversationHero] > 0, null);

            starter.AddDialogLine("senate_blackmail_response_success", "senate_blackmail_offer", "lord_pretalk", "So you know that issue... Okay, I'll vote for you in the Senate, as long as we keep it between us.",
                () => true,
                () => {
                    ConsumeBlackmailMaterial(Hero.OneToOneConversationHero, 1);
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -5);
                });
        }

        private void OpenSenateMission()
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
                    "SenateCouncil",
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

        private void InitializeCapitals()
        {
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.Culture.StringId.Contains("empire") && !_empireCapitals.ContainsKey(kingdom))
                {
                    var ruler = kingdom.Leader;
                    if (ruler != null)
                    {
                        var bestTown = ruler.Clan.Settlements.Where(s => s.IsTown).OrderByDescending(s => s.Town.Prosperity).FirstOrDefault();
                        if (bestTown != null)
                        {
                            _empireCapitals[kingdom] = bestTown;
                        }
                    }
                }
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            CheckCapitalLoss(settlement);
        }

        private void CheckCapitalLoss(Settlement settlement)
        {
            foreach (var kvp in _empireCapitals.ToList())
            {
                var kingdom = kvp.Key;
                var capital = kvp.Value;

                if (capital == settlement && capital.OwnerClan.Kingdom != kingdom)
                {
                    // Capital lost!
                    var ruler = kingdom.Leader;
                    var newBestTown = ruler.Clan.Settlements.Where(s => s.IsTown).OrderByDescending(s => s.Town.Prosperity).FirstOrDefault();

                    if (newBestTown == null)
                    {
                        // No other city owned, kingdom takes a severe nerf!
                        InformationManager.DisplayMessage(new InformationMessage($"{kingdom.Name} has lost its capital and the ruler has no other cities! Kingdom stability is collapsing.", Colors.Red));
                        kingdom.RulingClan.Influence -= 500;
                        foreach (var clan in kingdom.Clans)
                        {
                            if (!clan.IsUnderMercenaryService && clan != kingdom.RulingClan)
                            {
                                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(kingdom.Leader, clan.Leader, -20);
                            }
                        }
                        // Removing the capital so it stays null (no capital)
                        _empireCapitals.Remove(kingdom);
                    }
                    else
                    {
                        _empireCapitals[kingdom] = newBestTown;
                        InformationManager.DisplayMessage(new InformationMessage($"{kingdom.Name} lost its capital, new capital {newBestTown.Name} was declared.", Colors.Yellow));
                    }
                }
            }
        }

        private void OnDailyTick()
        {
            // Senate triggers on the 15th of the season (each season is 21 days in native, let's use DayOfSeason == 15)
            int currentDayOfSeason = CampaignTime.Now.GetDayOfSeason;
            if (currentDayOfSeason == 15)
            {
                TriggerSenateIfAgendaExists();
            }
        }

        public void TriggerSenate(Kingdom kingdom)
        {
            if (kingdom.Culture.StringId.Contains("empire") && _empireCapitals.ContainsKey(kingdom))
            {
                var capital = _empireCapitals[kingdom];
                InformationManager.ShowInquiry(new InquiryData("Senate Meets", $"The senate of {kingdom.Name} is meeting in the city of {capital.Name} to discuss the items on the agenda. All Archons have been summoned to the capital.", true, false, "Tamam", "", null, null));

                foreach (var clan in kingdom.Clans)
                {
                    foreach (var lordParty in clan.WarPartyComponents)
                    {
                        if (lordParty.MobileParty != MobileParty.MainParty && lordParty.MobileParty.CurrentSettlement != capital)
                        {
                            SetPartyAiAction.GetActionForVisitingSettlement(lordParty.MobileParty, capital, MobileParty.NavigationType.None, false, false);
                        }
                    }
                }
            }
        }

        private void TriggerSenateIfAgendaExists()
        {
            var agendaBehavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
            if (agendaBehavior == null) return;

            foreach (var kingdom in Kingdom.All)
            {
                if (agendaBehavior.HasAgenda(kingdom))
                {
                    TriggerSenate(kingdom);
                }
            }
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
        }

        public void AddBlackmailMaterial(Hero hero, int amount)
        {
            if (!_heroBlackmailMaterial.ContainsKey(hero))
                _heroBlackmailMaterial[hero] = 0;
            _heroBlackmailMaterial[hero] += amount;
        }

        public bool ConsumeBlackmailMaterial(Hero hero, int amount)
        {
            if (_heroBlackmailMaterial.ContainsKey(hero) && _heroBlackmailMaterial[hero] >= amount)
            {
                _heroBlackmailMaterial[hero] -= amount;
                return true;
            }
            return false;
        }
    }
}