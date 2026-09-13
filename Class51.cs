using HarmonyLib;
using SandBox;
using SandBox.View;
using SandBox.Missions.MissionLogics;
using SandBox.Conversation.MissionLogics;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using System.Linq;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;

namespace RebellionsAndDemographics
{
    [HarmonyPatch]
    public static class WarCabinetPatches
    {
        [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetScoreOfDeclaringWar")]
        [HarmonyPostfix]
        public static void Postfix_GetScoreOfDeclaringWar(IFaction factionDeclaresWar, IFaction factionDeclaredWar, Clan evaluatingClan, ref float __result)
        {
            if (factionDeclaresWar != null && factionDeclaredWar != null)
            {
                var stance = factionDeclaresWar.GetStanceWith(factionDeclaredWar);
                if (stance != null && !factionDeclaresWar.IsAtWarWith(factionDeclaredWar) && stance.PeaceDeclarationDate != TaleWorlds.CampaignSystem.CampaignTime.Never)
                {
                    if (stance.PeaceDeclarationDate.ElapsedDaysUntilNow < 20f)
                    {
                        __result = -999999f;
                    }
                }
            }

            if (WarCabinetData.TargetFaction == factionDeclaredWar && factionDeclaresWar == Clan.PlayerClan.Kingdom)
            {
                if (WarCabinetData.CurrentType == WarCabinetData.CouncilType.ProvokeWar)
                {
                    if (WarCabinetData.AccumulatedScore > 0)
                    {
                        __result += 100000f;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(KingdomDiplomacyVM), "OnSetPeaceItem")]
        [HarmonyPostfix]
        public static void Postfix_OnSetPeaceItem(KingdomDiplomacyVM __instance, KingdomTruceItemVM item)
        {
            try
            {
                if (Clan.PlayerClan.Kingdom == null || __instance.PlayerTruces == null) return;
                if (Clan.PlayerClan.Kingdom.Leader != Hero.MainHero) return;

                Kingdom targetKingdom = item.Faction2 as Kingdom;
                if (targetKingdom == null) return;

                int influenceCost = 50;
                bool canAfford = Clan.PlayerClan.Influence >= influenceCost;

                // {=wc_btn_war}War Cabinet: Persuade for War
                // {=wc_desc_war}Gather your vassals and demand their support for a war campaign.
                KingdomDiplomacyProposalActionItemVM btn = new KingdomDiplomacyProposalActionItemVM(
                    new TextObject("{=wc_btn_war}War Cabinet: Persuade for War"),
                    new TextObject("{=wc_desc_war}Gather your vassals and demand their support for a war campaign."),
                    influenceCost,
                    canAfford,
                    TextObject.GetEmpty(),
                    () => {
                        ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -influenceCost);
                        OpenIndependentCouncil(targetKingdom, WarCabinetData.CouncilType.ProvokeWar);
                    });

                __instance.Actions.Add(btn);
            }
            catch { }
        }

        public static void OpenIndependentCouncil(Kingdom target, WarCabinetData.CouncilType type)
        {
            WarCabinetData.ResetForNewCouncil(target, type);

            if (Campaign.Current.CurrentMenuContext != null) GameMenu.ExitToLast();

            string sceneName = "khuzait_castle_keep_a_l1_interior";

            MissionState.OpenNew("WarCabinetMission",
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.All),
                (mission) => new MissionBehavior[]
                {
                    new MissionOptionsComponent(),
                    new CampaignMissionComponent(),
                    new MissionBasicTeamLogic(),
                    new WarCabinetMissionLogic(),
                    new BasicLeaveMissionLogic(true),
                    new MissionConversationLogic(),
                    new MissionAgentLookHandler(),
                    SandBoxViewCreator.CreateMissionConversationView(mission),
                    ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                    ViewCreator.CreateMissionSingleplayerEscapeMenu(false),
                    ViewCreator.CreateMissionLeaveView()
                }, true, true);
        }
    }
}
