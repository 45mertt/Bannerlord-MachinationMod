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
    /* public class CouncilSaveDefiner : SaveableTypeDefiner
    {
        public CouncilSaveDefiner() : base(198_456_888) { }
        
    } */

    public class CouncilCampaignBehavior : CampaignBehaviorBase
    {
        private bool _isCouncilGathering = false;
        private CampaignTime _gatheringStartTime;
        private Settlement _councilCity = null;
        
        private List<Hero> _invitedLords = new List<Hero>();
        [SaveableProperty(1)]
        public List<Hero> InvitedLords { get { return _invitedLords ?? (_invitedLords = new List<Hero>()); } set { _invitedLords = value; } }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            
        }
        


        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var invited = InvitedLords;
                invited.RemoveAll(x => x == null);
            }
            dataStore.SyncData("_isCouncilGathering", ref _isCouncilGathering);
            dataStore.SyncData("_gatheringStartTime", ref _gatheringStartTime);
            dataStore.SyncData("_councilCity", ref _councilCity);
            dataStore.SyncData("_invitedLords", ref _invitedLords);
        }
        

        private void OnHourlyTick()
        {
            if (_isCouncilGathering && _councilCity != null)
            {
                int arrived = 0;
                foreach (var h in _invitedLords) if (h.CurrentSettlement == _councilCity) arrived++;
                bool timeIsUp = _gatheringStartTime.ElapsedDaysUntilNow > 3f;
                bool quorum = _invitedLords.Count > 0 && ((float)arrived / _invitedLords.Count >= 0.5f || arrived >= 2);

                if ((quorum || timeIsUp) && Settlement.CurrentSettlement == _councilCity) StartCouncilSession();

                foreach (var hero in _invitedLords)
                {
                    if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero && hero.PartyBelongedTo.CurrentSettlement != _councilCity)
                        hero.PartyBelongedTo.SetMoveGoToSettlement(_councilCity, 0, false);
                }
            }
        }

        private void StartCouncilSession()
        {
            _isCouncilGathering = false;
            _invitedLords.Clear();
            _councilCity = null;

            CouncilRequestGenerator.InitializeCouncilRequests(Clan.PlayerClan.Kingdom);

            GameMenu.ExitToLast();
            OpenCouncilMission(Settlement.CurrentSettlement);
        }

        private void OpenCouncilMission(Settlement settlement)
        {
            string sceneName = "khuzait_castle_keep_a_l1_interior"; // Veya settlement.LocationComplex.GetScene("lordshall", 1);
            BasicLeaveMissionLogic leaveLogic = new BasicLeaveMissionLogic(false);

            MissionLogic[] logicList = new MissionLogic[]
            {
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new MissionBasicTeamLogic(),
                new CouncilMissionLogic(settlement),
                leaveLogic,
                new MissionConversationLogic()
            };

            Mission mission = MissionState.OpenNew(
                "GrandCouncil",
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.All),
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

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // MENU OPTION
            starter.AddGameMenuOption("town_keep", "town_grand_council_summon", "{=str_council_summon}Summon Council ({COST} Gold, {INF} Influence)",
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    if (_isCouncilGathering) return false;
                    var k = Clan.PlayerClan.Kingdom;
                    if (k == null || k.Leader != Hero.MainHero || k.Clans.Count < 2) return false;

                    if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement.OwnerClan != Clan.PlayerClan) return false;

                    MBTextManager.SetTextVariable("COST", (k.Clans.Count - 1) * 5000);
                    MBTextManager.SetTextVariable("INF", 50);
                    return Clan.PlayerClan.Influence >= 50;
                },
                (args) => {
                    var k = Clan.PlayerClan.Kingdom;
                    int cost = (k.Clans.Count - 1) * 5000;
                    if (Hero.MainHero.Gold < cost) { InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=str_turmoil_insufficient_gold}Insufficient Gold").ToString(), Colors.Red)); return; }
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);
                    ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -50f);
                    _isCouncilGathering = true;
                    _gatheringStartTime = CampaignTime.Now;
                    _councilCity = Settlement.CurrentSettlement;
                    _invitedLords.Clear();
                    foreach (var c in k.Clans) if (c != Clan.PlayerClan && !c.IsEliminated && c.Leader != null) { _invitedLords.Add(c.Leader); c.Leader.PartyBelongedTo?.SetMoveGoToSettlement(_councilCity, 0, false); }
                    GameMenu.SwitchToMenu("town_wait_menus");
                });

            // GATHERING STATUS
            starter.AddGameMenuOption("town_wait_menus", "town_grand_council_status", "{=str_council_status}Lords Gathering... ({ARRIVED}/{TOTAL})",
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    if (!_isCouncilGathering) return false;
                    int arr = 0; foreach (var h in _invitedLords) if (h.CurrentSettlement == _councilCity) arr++;
                    MBTextManager.SetTextVariable("ARRIVED", arr);
                    MBTextManager.SetTextVariable("TOTAL", _invitedLords.Count);
                    return true;
                }, null);

            // --- DIALOGS ---

            // 1. Lord starts proposal
            starter.AddDialogLine("council_start", "start", "council_lord_proposal",
                "{=str_council_dynamic}{COUNCIL_DESC}",
                () => {
                    if (Mission.Current == null || !Mission.Current.HasMissionBehavior<CouncilMissionLogic>()) return false;
                    Hero partner = Hero.OneToOneConversationHero;
                    if (partner == null) return false;

                    var request = CouncilManager.GetRequestForHero(partner);
                    if (request != null && !CouncilManager.IsRequestResolved(request.RequestID))
                    {
                        CouncilManager.SetCurrentActiveRequest(request);
                        // Set text variables for the dialog
                        MBTextManager.SetTextVariable("COUNCIL_DESC", request.Description);
                        return true;
                    }
                    return false;
                }, null, 10000);

            // 2. Fallback if no proposal
            starter.AddDialogLine("council_start_fallback", "start", "close_window",
                "{=str_council_fallback}My Liege, everything is well. Our loyalty is absolute.",
                () => Mission.Current != null && Mission.Current.HasMissionBehavior<CouncilMissionLogic>() && Hero.OneToOneConversationHero != null,
                null, 9000);

            // 3. Player Options
            starter.AddPlayerLine("council_opt_1", "council_lord_proposal", "council_finish_1",
                "{=str_opt_1}{OPT_1_TEXT}",
                () => {
                    if (CouncilManager.CurrentRequest == null) return false;
                    MBTextManager.SetTextVariable("OPT_1_TEXT", CouncilManager.CurrentRequest.Option1Text);
                    return true;
                },
                () => {
                    CouncilManager.CurrentRequest.Option1Action?.Invoke();
                    CouncilManager.MarkRequestAsResolved(CouncilManager.CurrentRequest.RequestID);
                });

            starter.AddPlayerLine("council_opt_2", "council_lord_proposal", "council_finish_2",
                "{=str_opt_2}{OPT_2_TEXT}",
                () => {
                    if (CouncilManager.CurrentRequest == null) return false;
                    MBTextManager.SetTextVariable("OPT_2_TEXT", CouncilManager.CurrentRequest.Option2Text);
                    return true;
                },
                () => {
                    CouncilManager.CurrentRequest.Option2Action?.Invoke();
                    CouncilManager.MarkRequestAsResolved(CouncilManager.CurrentRequest.RequestID);
                });

            // 4. Lord Response
            starter.AddDialogLine("council_res_1", "council_finish_1", "close_window",
                "{=str_rsp_1}{RSP_1_TEXT}",
                () => {
                    if (CouncilManager.CurrentRequest == null) return false;
                    MBTextManager.SetTextVariable("RSP_1_TEXT", CouncilManager.CurrentRequest.Option1Response);
                    return true;
                }, null);

            starter.AddDialogLine("council_res_2", "council_finish_2", "close_window",
                "{=str_rsp_2}{RSP_2_TEXT}",
                () => {
                    if (CouncilManager.CurrentRequest == null) return false;
                    MBTextManager.SetTextVariable("RSP_2_TEXT", CouncilManager.CurrentRequest.Option2Response);
                    return true;
                }, null);
        }
    }
}

