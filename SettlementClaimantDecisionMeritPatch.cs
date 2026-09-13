using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace RebellionsAndDemographics.Patches
{
    [HarmonyPatch(typeof(SettlementClaimantDecision), "CalculateMeritOfOutcome")]
    public static class SettlementClaimantDecisionMeritPatch
    {
        public static void Postfix(SettlementClaimantDecision __instance, DecisionOutcome candidateOutcome, ref float __result)
        {
            if (LoyaltyKingdomBehavior.Instance == null) return;

            SettlementClaimantDecision.ClanAsDecisionOutcome outcome = candidateOutcome as SettlementClaimantDecision.ClanAsDecisionOutcome;
            if (outcome != null && outcome.Clan != null)
            {
                Clan clan = outcome.Clan;

                // Fief Limit Check (Player only)
                if (clan == Hero.MainHero.Clan && clan.Kingdom != null && clan.Kingdom.RulingClan != clan && clan.Tier < 6)
                {
                    var limits = FiefLimitBehavior.GetFiefLimits(clan.Tier);
                    int currentTowns = clan.Settlements.Count(s => s.IsTown);
                    int currentCastles = clan.Settlements.Count(s => s.IsCastle);
                    
                    if (__instance.Settlement.IsTown && currentTowns >= limits.MaxTowns) { __result = -999999f; return; }
                    if (__instance.Settlement.IsCastle && currentCastles >= limits.MaxCastles) { __result = -999999f; return; }
                }

                // Ruling clan always has merit

                if ((clan.Kingdom != null && clan.Kingdom.RulingClan == clan)) return;

                // Get loyalty score
                float loyalty = LoyaltyKingdomBehavior.Instance.GetClanLoyalty(clan);

                // Threshold: If loyalty is below 50, you are almost excluded.
                if (loyalty < 50f)
                {
                    // Drastically reduce the merit so the clan is never on the ballot
                    __result -= 50000f; 
                }
                else if (loyalty < 100f)
                {
                    // Moderate penalty
                    __result -= 1000f;
                }
                else
                {
                    // Add the loyalty as a bonus to the native merit!
                    __result += loyalty * 5f;
                }
            }
        }
    }
}
