using System;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    /// <summary>
    /// Village Barracks Investment System:
    /// - Player can visit a village notable and fund a local barracks
    /// - Costs: significant gold + weapons
    /// - Reward: 30-40 pre-trained Tier 3-4 troops delivered immediately
    ///
    /// Also handles:
    /// - Troop XP penalty: player's party gains XP at 40% of normal rate (slows leveling)
    /// - Daily warning message if player is in the field with noble prisoners
    /// </summary>
    public class VillageBarracksBehavior : CampaignBehaviorBase
    {
        public static VillageBarracksBehavior Instance { get; private set; }

        // Tracks which villages the player has already invested in (no double-dipping)
        private List<string> _investedVillages = new List<string>();

        // XP reduction rate for player party (0.4 = 40% of original XP, i.e. 60% slower)
        private const float PlayerXpMultiplier = 0.4f;

        // Barracks investment cost
        private const int GoldCost     = 15000;
        private const int WeaponCount  = 15;   // swords/spears needed

        public VillageBarracksBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
        }

        // -------------------------------------------------------
        // Daily troop XP reduction for player party
        // -------------------------------------------------------
        private void OnDailyTickParty(MobileParty party)
        {
            if (party != MobileParty.MainParty) return;

            // Reduce XP accumulation: strip some XP from each troop per day
            // This simulates slower leveling without patching the core XP model
            foreach (var elem in party.MemberRoster.GetTroopRoster())
            {
                if (elem.Character.IsHero) continue;
                int xpToStrip = MathF.Round((float)elem.Xp * (1f - PlayerXpMultiplier));
                if (xpToStrip > 0)
                {
                    party.MemberRoster.AddXpToTroop(elem.Character, -xpToStrip);
                }
            }
        }

        // -------------------------------------------------------
        // Hourly warning: noble prisoners in mobile party
        // -------------------------------------------------------
        private void OnHourlyTickParty(MobileParty party)
        {
            if (party != MobileParty.MainParty) return;
            if (party.CurrentSettlement != null) return;

            // Check if any noble prisoners are at risk
            bool hasNoblePrisoner = false;
            foreach (var elem in party.PrisonRoster.GetTroopRoster())
            {
                if (elem.Character.IsHero && elem.Character.HeroObject != Hero.MainHero
                    && elem.Character.HeroObject.IsLord)
                {
                    hasNoblePrisoner = true;
                    break;
                }
            }

            // Show warning once per day (roughly every 24 calls)
            if (hasNoblePrisoner && CampaignTime.Now.ToHours % 12 < 1f)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_prisoner_warning}WARNING: Noble prisoners in your camp are at high risk of escaping! Reach your castle dungeon immediately.")
                        .ToString(),
                    new Color(1f, 0.3f, 0.1f)));
            }
        }

        // -------------------------------------------------------
        // Dialogs
        // -------------------------------------------------------
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // ---- Entry: Ask notable about funding a barracks ----
            starter.AddPlayerLine("rad_barracks_ask", "hero_main_options", "rad_barracks_menu",
                "{=rad_vil_fund_ask}I want to fund a local barracks in this village.",
                () => {
                    if (Hero.OneToOneConversationHero == null) return false;
                    if (!Hero.OneToOneConversationHero.IsNotable) return false;
                    var settlement = Hero.OneToOneConversationHero.CurrentSettlement;
                    if (settlement == null || !settlement.IsVillage) return false;
                    if (_investedVillages.Contains(settlement.StringId)) return false;
                    return true;
                }, null);

            starter.AddDialogLine("rad_barracks_menu", "rad_barracks_menu", "rad_barracks_options",
                "{=rad_vil_fund_01}A barracks? Aye, we could use trained fighters around here. But I won't do this for free — you'll need to provide the gold and the weapons, {PLAYER.NAME}.",
                () => {
                    StringHelpers.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
                    return true;
                }, null);

            // ---- Show cost info ----
            starter.AddPlayerLine("rad_barracks_inquire", "rad_barracks_options", "rad_barracks_cost",
                "{=rad_vil_fund_02}How much will this cost me?",
                null, null);

            starter.AddDialogLine("rad_barracks_cost", "rad_barracks_cost", "rad_barracks_options",
                "{=rad_vil_fund_03}I need {GOLD} gold and {WEAPONS} swords or spears. In return, I can rally 30 to 40 of our best trained men — not green recruits, real fighters.",
                () => {
                    MBTextManager.SetTextVariable("GOLD", GoldCost);
                    MBTextManager.SetTextVariable("WEAPONS", WeaponCount);
                    return true;
                }, null);

            // ---- Accept & invest ----
            starter.AddPlayerLine("rad_barracks_accept", "rad_barracks_options", "rad_barracks_result",
                "{=rad_vil_fund}Here is the gold and the weapons. Build this barracks.",
                () => {
                    if (Hero.MainHero.Gold < GoldCost) return false;
                    return CountWeapons() >= WeaponCount;
                },
                null);

            starter.AddDialogLine("rad_barracks_result", "rad_barracks_result", "close_window",
                "{=rad_vil_fund_04}You have my gratitude, {PLAYER.NAME}. I will have our men ready within the hour. They will serve under your banner.",
                () => {
                    StringHelpers.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
                    return true;
                },
                () => {
                    var settlement = Hero.OneToOneConversationHero.CurrentSettlement;
                    ExecuteBarracksInvestment(settlement);
                });

            // ---- Not enough resources ----
            starter.AddPlayerLine("rad_barracks_cant_afford", "rad_barracks_options", "rad_barracks_cant_afford_response",
                "{=rad_vil_fund_05}I don't have what you need right now.",
                () => {
                    return Hero.MainHero.Gold < GoldCost || CountWeapons() < WeaponCount;
                }, null);

            starter.AddDialogLine("rad_barracks_cant_afford_response", "rad_barracks_cant_afford_response", "close_window",
                "{=rad_vil_fund_06}Come back when you have {GOLD} gold and {WEAPONS} swords or spears.",
                () => {
                    MBTextManager.SetTextVariable("GOLD", GoldCost);
                    MBTextManager.SetTextVariable("WEAPONS", WeaponCount);
                    return true;
                }, null);

            // ---- Cancel ----
            starter.AddPlayerLine("rad_barracks_cancel", "rad_barracks_options", "lord_pretalk",
                "{=rad_vil_fund_07}Maybe another time.",
                null, null);

            // ---- Already invested ----
            starter.AddPlayerLine("rad_barracks_done", "hero_main_options", "rad_barracks_done_response",
                "{=rad_vil_fund_08}How are the men from the barracks I funded doing?",
                () => {
                    if (Hero.OneToOneConversationHero == null || !Hero.OneToOneConversationHero.IsNotable) return false;
                    var settlement = Hero.OneToOneConversationHero.CurrentSettlement;
                    return settlement != null && settlement.IsVillage && _investedVillages.Contains(settlement.StringId);
                }, null);

            starter.AddDialogLine("rad_barracks_done_response", "rad_barracks_done_response", "hero_main_options",
                "{=rad_vil_fund_09}They are trained and ready. The barracks still stands, but we cannot produce another batch of fighters without a fresh investment.",
                null, null);
        }

        // -------------------------------------------------------
        // Core Logic: Invest and spawn troops
        // -------------------------------------------------------
        private void ExecuteBarracksInvestment(Settlement settlement)
        {
            if (settlement == null) return;

            // Take gold
            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, GoldCost, true);

            // Take weapons
            TakeWeapons(WeaponCount);

            // Mark village as invested
            if (!_investedVillages.Contains(settlement.StringId)) _investedVillages.Add(settlement.StringId);

            // Determine troop culture and Tier 3-4 troops
            var culture = settlement.Culture;
            var troops = GetTier3To4Troops(culture);

            if (troops.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_barracks_no_troops}No suitable troops found for this culture. Contact your modder.").ToString(),
                    Colors.Red));
                return;
            }

            // Give 30-40 troops
            int totalToGive = MBRandom.RandomInt(30, 41);
            int given = 0;

            while (given < totalToGive)
            {
                var troop = troops[MBRandom.RandomInt(0, troops.Count)];
                int batch = Math.Min(MBRandom.RandomInt(3, 8), totalToGive - given);
                MobileParty.MainParty.MemberRoster.AddToCounts(troop, batch);
                given += batch;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=rad_barracks_done_msg}The village barracks has delivered {COUNT} trained fighters to your party!")
                    .SetTextVariable("COUNT", given)
                    .ToString(),
                Colors.Green));
        }

        private static List<CharacterObject> GetTier3To4Troops(CultureObject culture)
        {
            var result = new List<CharacterObject>();
            foreach (var troop in CharacterObject.All)
            {
                if (troop.IsHero || troop.IsPlayerCharacter) continue;
                if (troop.Culture != culture) continue;
                if (troop.Tier < 3 || troop.Tier > 4) continue;
                if (troop.Occupation != Occupation.Soldier) continue;
                result.Add(troop);
            }
            return result;
        }

        // -------------------------------------------------------
        // Weapon counting helpers
        // -------------------------------------------------------
        private static int CountWeapons()
        {
            int count = 0;
            foreach (var elem in PartyBase.MainParty.ItemRoster)
            {
                var item = elem.EquipmentElement.Item;
                if (item == null) continue;
                if (item.Type == ItemObject.ItemTypeEnum.OneHandedWeapon ||
                    item.Type == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                    item.Type == ItemObject.ItemTypeEnum.Polearm)
                {
                    count += elem.Amount;
                }
            }
            return count;
        }

        private static void TakeWeapons(int needed)
        {
            int remaining = needed;
            for (int i = PartyBase.MainParty.ItemRoster.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var elem = PartyBase.MainParty.ItemRoster.GetElementCopyAtIndex(i);
                var item = elem.EquipmentElement.Item;
                if (item == null) continue;
                if (item.Type == ItemObject.ItemTypeEnum.OneHandedWeapon ||
                    item.Type == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                    item.Type == ItemObject.ItemTypeEnum.Polearm)
                {
                    int toTake = Math.Min(remaining, elem.Amount);
                    PartyBase.MainParty.ItemRoster.AddToCounts(elem.EquipmentElement, -toTake);
                    remaining -= toTake;
                }
            }
        }

        // -------------------------------------------------------
        // Save
        // -------------------------------------------------------
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_rad_invested_villages", ref _investedVillages);
        }
    }
}
