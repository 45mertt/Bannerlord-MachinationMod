using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Library;

namespace RebellionsAndDemographics
{
    [HarmonyPatch(typeof(KingdomPoliciesVM), "ExecuteProposeOrDisavow")]
    public static class AgendaPolicyPatch
    {
        public static bool Prefix(KingdomPoliciesVM __instance)
        {
            var selectedItem = __instance.CurrentSelectedPolicy;
            if (selectedItem != null && selectedItem.Policy != null)
            {
                var policy = selectedItem.Policy;
                var kingdom = Clan.PlayerClan?.Kingdom;
                if (kingdom != null)
                {
                    var behavior = Campaign.Current.GetCampaignBehavior<AgendaPoolBehavior>();
                    if (behavior != null)
                    {
                        behavior.AddPolicyToAgenda(kingdom, policy);
                        
                        var nord = Campaign.Current.GetCampaignBehavior<NordSturgiaFestivalBehavior>();
                        if (nord != null) nord.TriggerFestival(kingdom);
                        
                        var empire = Campaign.Current.GetCampaignBehavior<EmpireSenateBehavior>();
                        if (empire != null) empire.TriggerSenate(kingdom);

                        var aserai = Campaign.Current.GetCampaignBehavior<AseraiDivanBehavior>();
                        if (aserai != null) aserai.TriggerDivan(kingdom);
                        
                        var vlandia = Campaign.Current.GetCampaignBehavior<VlandiaCouncilBehavior>();
                        if (vlandia != null) vlandia.TriggerVlandiaCouncil(kingdom);

                        return true; // Let native behavior run
                    }
                }
            }
            return true;
        }
    }
}
