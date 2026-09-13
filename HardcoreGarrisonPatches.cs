using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Core;

namespace RebellionsAndDemographics.Patches
{
    // ----------------------------------------------------------------
    // PATCH 1: Block auto-recruitment ONLY for player-owned settlements
    // AI kingdoms are unaffected.
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(GarrisonRecruitmentCampaignBehavior), "CanSettlementAutoRecruit")]
    public static class GarrisonAutoRecruitPlayerPatch
    {
        private static void Postfix(Settlement settlement, ref bool __result)
        {
            // If native returned false, keep it false
            if (!__result) return;

            // If this settlement belongs to the player's clan, block auto-recruitment
            if (settlement.OwnerClan == Clan.PlayerClan)
            {
                __result = false;
            }
        }
    }

    // ----------------------------------------------------------------
    // PATCH 2: Hero prisoner escape — drastically increase escape chance
    // when prisoner is held in a MOBILE party (not in a dungeon).
    // Original formula: 0.04 * multiplier. We multiply the mobile chance by 5x.
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "DailyHeroTick")]
    public static class PrisonerEscapeMobilePatch
    {
        private static bool Prefix(Hero hero)
        {
            if (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null || hero == Hero.MainHero)
                return true; // run original

            // Only intercept if prisoner is in a mobile (non-settlement) party owned by the player
            bool isInPlayerMobileParty = hero.PartyBelongedToAsPrisoner.IsMobile
                                      && hero.PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement == null
                                      && hero.PartyBelongedToAsPrisoner == PartyBase.MainParty;

            if (!isInPlayerMobileParty)
                return true; // run original for non-player scenarios

            // Check if releasing is allowed
            bool result = true;
            CampaignEventDispatcher.Instance.CanHeroBeReleased(hero, ref result);
            if (!result) return false;

            // Significantly increased escape chance: 0.04 * (5 - party_size_factor) * 5
            float partyFactor = MathF.Pow(MathF.Min(81, hero.PartyBelongedToAsPrisoner.NumberOfHealthyMembers), 0.25f);
            float escapeChance = 0.04f * (5f - partyFactor) * 5f; // 5x the original
            escapeChance = MathF.Clamp(escapeChance, 0.05f, 0.80f); // min 5%, max 80%

            if (MBRandom.RandomFloat < escapeChance)
            {
                EndCaptivityAction.ApplyByEscape(hero);
            }

            return false; // skip original for player mobile party prisoners
        }
    }

    // ----------------------------------------------------------------
    // PATCH 3: Slow down troop upgrades — reduce upgrade XP gain for player party
    // We do this by halving the XP given during garrison daily tick
    // and via troop roster AddXp calls.
    // ----------------------------------------------------------------
    // Note: The main XP nerf is handled through HardcoreTroopXpBehavior (see below)
}
