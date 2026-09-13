using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.GameComponents;

namespace RebellionsAndDemographics
{
    /// <summary>
    /// Overrides the inventory capacity model to:
    /// - Drastically reduce spare mounts (horses) contribution to carry weight (70% reduction)
    /// - Reduce pack animals (mules) contribution significantly (50% reduction)
    /// - Only applies to the player's party
    /// This forces the player to store valuables in their castle stash
    /// </summary>
    public class HardcoreInventoryCapacityModel : DefaultInventoryCapacityModel
    {
        // Player-only mount carry capacity reduction
        private const float MountCapacityMultiplier   = 0.30f; // 70% less capacity from horses
        private const float PackAnimalMultiplier      = 0.50f; // 50% less capacity from mules

        public override ExplainedNumber CalculateInventoryCapacity(
            MobileParty mobileParty,
            bool isCurrentlyAtSea,
            bool includeDescriptions = false,
            int additionalTroops = 0,
            int additionalSpareMounts = 0,
            int additionalPackAnimals = 0,
            bool includeFollowers = false)
        {
            // Non-player parties: use default untouched
            if (mobileParty != MobileParty.MainParty)
                return base.CalculateInventoryCapacity(mobileParty, isCurrentlyAtSea, includeDescriptions, additionalTroops, additionalSpareMounts, additionalPackAnimals, includeFollowers);

            // --- Recalculate manually with reduced mount/pack animal contribution ---
            ExplainedNumber stat = new ExplainedNumber(0f, includeDescriptions);

            PartyBase party = mobileParty.Party;
            int numMounts      = party.NumberOfMounts;
            int numMembers     = party.NumberOfHealthyMembers;
            int numPackAnimals = party.NumberOfPackAnimals;

            if (includeFollowers)
            {
                foreach (MobileParty attachedParty in mobileParty.AttachedParties)
                {
                    numMounts      += attachedParty.Party.NumberOfMounts;
                    numMembers     += attachedParty.Party.NumberOfHealthyMembers;
                    numPackAnimals += attachedParty.Party.NumberOfPackAnimals;
                }
            }

            // Steward: ArenicosHorses perk still works
            Hero perkOwner = null;
            if (mobileParty.HasPerk(DefaultPerks.Steward.ArenicosHorses, out perkOwner))
            {
                int bonus = MathF.Round((float)numMembers * DefaultPerks.Steward.ArenicosHorses.PrimaryBonus);
                numMembers += bonus;
            }
            Hero perkOwner2 = null;
            if (mobileParty.HasPerk(DefaultPerks.Steward.ForcedLabor, out perkOwner2))
            {
                numMembers += party.PrisonRoster.TotalHealthyCount;
            }

            stat.Add(10f, new TextObject("{=basevalue}Base"));
            stat.Add((float)numMembers * 2f * 10f, new TextObject("{=5k4dxUEJ}Troops"));

            if (!isCurrentlyAtSea)
            {
                // Spare mounts: original is numMounts * 2 * 10, we apply MountCapacityMultiplier
                float mountContrib = (float)numMounts * 2f * 10f * MountCapacityMultiplier;
                stat.Add(mountContrib, new TextObject("{=rad_inventory_horses}Spare Mounts (Restricted)"));

                // Pack animals: original is numPackAnimals * 10 * 10, we apply PackAnimalMultiplier
                ExplainedNumber packStat = new ExplainedNumber((float)numPackAnimals * 10f * 10f * PackAnimalMultiplier);
                PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.BeastWhisperer, mobileParty, isPrimaryBonus: false, ref packStat);
                PerkHelper.AddPerkBonusForParty(DefaultPerks.Riding.DeeperSacks, mobileParty, isPrimaryBonus: true, ref packStat);
                PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.ArenicosMules, mobileParty, isPrimaryBonus: true, ref packStat);
                stat.Add(packStat.ResultNumber, new TextObject("{=rad_inventory_mules}Pack Animals (Restricted)"));

                PerkHelper.AddPerkBonusForParty(DefaultPerks.Trade.CaravanMaster, mobileParty, isPrimaryBonus: true, ref stat);
            }

            stat.LimitMin(10f);
            return stat;
        }
    }
}
