using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    // =============================================
    // 1. SAVAï¿½ ALANI VERï¿½Sï¿½ (Save Destekli)
    // =============================================
    public class BattlefieldData
    {
        [SaveableField(1)]
        public float PositionX;

        [SaveableField(2)]
        public float PositionY;

        [SaveableField(3)]
        public CampaignTime CreationTime;

        [SaveableField(4)]
        public int TotalDead;

        [SaveableField(5)]
        public string AttackerCultureId;

        [SaveableField(6)]
        public string DefenderCultureId;

        [SaveableField(7)]
        public string AttackerFactionId;

        [SaveableField(8)]
        public string DefenderFactionId;

        [SaveableField(9)]
        public bool AlreadyLooted;

        [SaveableField(10)]
        public float LastNotifiedHour;

        public BattlefieldData() { }

        public BattlefieldData(float posX, float posY, int totalDead,
            string attackerCulture, string defenderCulture,
            string attackerFaction, string defenderFaction)
        {
            PositionX = posX;
            PositionY = posY;
            CreationTime = CampaignTime.Now;
            TotalDead = totalDead;
            AttackerCultureId = attackerCulture ?? "empire";
            DefenderCultureId = defenderCulture ?? "empire";
            AttackerFactionId = attackerFaction ?? "";
            DefenderFactionId = defenderFaction ?? "";
            AlreadyLooted = false;
            LastNotifiedHour = -100f;
        }

        // --- Sï¿½RE DOLDU MU? ---
        public bool IsExpired()
        {
            return CreationTime.ElapsedHoursUntilNow > 48;
        }

        // --- KLAN SEVï¿½YESï¿½NE Gï¿½RE Dï¿½ï¿½MAN SAYISI ---
        public int GetEnemyCount()
        {
            int clanTier = Clan.PlayerClan?.Tier ?? 0;
            float baseCount = 15f;
            float scaled = baseCount * (1f + clanTier * 0.8f);

            float hours = CreationTime.ElapsedHoursUntilNow;
            float decayed = scaled / (1f + hours * 0.5f);

            int result = Math.Max(1, (int)decayed);
            return Math.Min(result, 50);
        }

        // --- KLAN SEVï¿½YESï¿½NE Gï¿½RE ASKER Tï¿½ERï¿½ ---
        public string[] GetTroopPool()
        {
            int clanTier = Clan.PlayerClan?.Tier ?? 0;
            string targetCulture = GetTargetCultureId();

            List<string> pool = new List<string>();
            pool.AddRange(GetTroopsByTier(targetCulture, clanTier));
            pool.Add("looter");
            return pool.ToArray();
        }

        // --- LOOT KUTUSU SAYISI ---
        public int GetLootBoxCount()
        {
            int clanTier = Clan.PlayerClan?.Tier ?? 0;
            int baseBoxes = 3 + (TotalDead / 50);
            int scaled = baseBoxes + clanTier;
            return Math.Max(3, Math.Min(scaled, 15));
        }

        // --- KUTU BAï¿½INA ALTIN ---
        public int GetGoldPerBox()
        {
            int clanTier = Clan.PlayerClan?.Tier ?? 0;

            switch (clanTier)
            {
                case 0: return MBRandom.RandomInt(50, 100);
                case 1: return MBRandom.RandomInt(100, 200);
                case 2: return MBRandom.RandomInt(150, 300);
                case 3: return MBRandom.RandomInt(200, 400);
                case 4: return MBRandom.RandomInt(300, 500);
                case 5: return MBRandom.RandomInt(400, 700);
                default: return MBRandom.RandomInt(500, 800);
            }
        }

        // --- YAï¿½MALANACAK TARAFIN Kï¿½LTï¿½Rï¿½ ---
        public string GetTargetCultureId()
        {
            string playerFactionId = Clan.PlayerClan?.Kingdom?.StringId ?? "";

            if (!string.IsNullOrEmpty(playerFactionId))
            {
                if (AttackerFactionId == playerFactionId)
                    return DefenderCultureId;
                if (DefenderFactionId == playerFactionId)
                    return AttackerCultureId;
            }

            return MBRandom.RandomInt(2) == 0 ? AttackerCultureId : DefenderCultureId;
        }

        // --- ï¿½ï¿½ SAVAï¿½ MI? ---
        public bool IsPlayerCivilWar()
        {
            string playerFactionId = Clan.PlayerClan?.Kingdom?.StringId ?? "";
            if (string.IsNullOrEmpty(playerFactionId)) return false;
            return AttackerFactionId == playerFactionId && DefenderFactionId == playerFactionId;
        }

        // --- COOLDOWN KONTROLï¿½ ---
        public bool CanNotifyAgain()
        {
            float hoursSinceLastNotify = (float)CampaignTime.Now.ToHours - LastNotifiedHour;
            return hoursSinceLastNotify > 15f;
        }

        public void MarkNotified()
        {
            LastNotifiedHour = (float)CampaignTime.Now.ToHours;
        }

        // --- Bï¿½RLï¿½K Lï¿½STESï¿½ ---
        private string[] GetTroopsByTier(string cultureId, int clanTier)
        {
            if (clanTier <= 1) return GetRecruitsForCulture(cultureId);
            if (clanTier <= 3) return GetMidTroopsForCulture(cultureId);
            if (clanTier <= 5) return GetHighTroopsForCulture(cultureId);
            return GetEliteTroopsForCulture(cultureId);
        }

        private string[] GetRecruitsForCulture(string c)
        {
            switch (c)
            {
                case "empire": return new[] { "imperial_recruit", "imperial_vigla_recruit" };
                case "sturgia": return new[] { "sturgian_recruit", "sturgian_warrior_son" };
                case "vlandia": return new[] { "vlandian_recruit", "vlandian_levy_crossbowman" };
                case "aserai": return new[] { "aserai_recruit", "aserai_tribal_horseman" };
                case "khuzait": return new[] { "khuzait_nomad", "khuzait_hunter" };
                case "battania": return new[] { "battanian_volunteer", "battanian_pickpocket" };
                default: return new[] { "looter" };
            }
        }

        private string[] GetMidTroopsForCulture(string c)
        {
            switch (c)
            {
                case "empire": return new[] { "imperial_recruit", "imperial_infantryman", "imperial_vigla_recruit" };
                case "sturgia": return new[] { "sturgian_recruit", "sturgian_soldier", "sturgian_warrior" };
                case "vlandia": return new[] { "vlandian_recruit", "vlandian_footman", "vlandian_crossbowman" };
                case "aserai": return new[] { "aserai_recruit", "aserai_footman", "aserai_skirmisher" };
                case "khuzait": return new[] { "khuzait_nomad", "khuzait_tribal_warrior", "khuzait_hunter" };
                case "battania": return new[] { "battanian_volunteer", "battanian_clan_warrior", "battanian_trained_warrior" };
                default: return new[] { "looter", "imperial_recruit" };
            }
        }

        private string[] GetHighTroopsForCulture(string c)
        {
            switch (c)
            {
                case "empire": return new[] { "imperial_infantryman", "imperial_trained_infantryman", "imperial_legionary" };
                case "sturgia": return new[] { "sturgian_soldier", "sturgian_veteran_warrior", "sturgian_spearman" };
                case "vlandia": return new[] { "vlandian_footman", "vlandian_swordsman", "vlandian_sergeant" };
                case "aserai": return new[] { "aserai_footman", "aserai_veteran_infantry", "aserai_master_archer" };
                case "khuzait": return new[] { "khuzait_tribal_warrior", "khuzait_horseman", "khuzait_horse_archer" };
                case "battania": return new[] { "battanian_trained_warrior", "battanian_veteran_falxman", "battanian_hero" };
                default: return new[] { "imperial_infantryman", "looter" };
            }
        }

        private string[] GetEliteTroopsForCulture(string c)
        {
            switch (c)
            {
                case "empire": return new[] { "imperial_trained_infantryman", "imperial_legionary", "imperial_palatine_guard" };
                case "sturgia": return new[] { "sturgian_veteran_warrior", "sturgian_heroic_line_breaker", "sturgian_ulfhednar" };
                case "vlandia": return new[] { "vlandian_swordsman", "vlandian_sergeant", "vlandian_banner_knight" };
                case "aserai": return new[] { "aserai_veteran_infantry", "aserai_master_archer", "aserai_vanguard_faris" };
                case "khuzait": return new[] { "khuzait_horseman", "khuzait_horse_archer", "khuzait_khan_guard" };
                case "battania": return new[] { "battanian_veteran_falxman", "battanian_hero", "battanian_fian_champion" };
                default: return new[] { "imperial_legionary", "imperial_trained_infantryman" };
            }
        }

        // --- Bï¿½LDï¿½Rï¿½M METNï¿½ ---
        public string GetNotificationText()
        {
            int enemyCount = GetEnemyCount();
            int hoursAgo = (int)CreationTime.ElapsedHoursUntilNow;
            string timeStr = hoursAgo < 1 ? "recently" : $"{hoursAgo}h ago";

            return $"Battlefield ruins ({timeStr}): ~{enemyCount} scavengers linger. {TotalDead} fell in battle.";
        }

        // --- AutoGenerated Save Methods (1.3 Pattern) ---
        internal static void AutoGeneratedStaticCollectObjectsBattlefieldData(object o, List<object> collectedObjects)
        {
            ((BattlefieldData)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
        }

        protected void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
        {
            // primitive fields ï¿½ no object references to collect
        }

        internal static object AutoGeneratedGetMemberValuePositionX(object o) { return ((BattlefieldData)o).PositionX; }
        internal static object AutoGeneratedGetMemberValuePositionY(object o) { return ((BattlefieldData)o).PositionY; }
        internal static object AutoGeneratedGetMemberValueCreationTime(object o) { return ((BattlefieldData)o).CreationTime; }
        internal static object AutoGeneratedGetMemberValueTotalDead(object o) { return ((BattlefieldData)o).TotalDead; }
        internal static object AutoGeneratedGetMemberValueAttackerCultureId(object o) { return ((BattlefieldData)o).AttackerCultureId; }
        internal static object AutoGeneratedGetMemberValueDefenderCultureId(object o) { return ((BattlefieldData)o).DefenderCultureId; }
        internal static object AutoGeneratedGetMemberValueAttackerFactionId(object o) { return ((BattlefieldData)o).AttackerFactionId; }
        internal static object AutoGeneratedGetMemberValueDefenderFactionId(object o) { return ((BattlefieldData)o).DefenderFactionId; }
        internal static object AutoGeneratedGetMemberValueAlreadyLooted(object o) { return ((BattlefieldData)o).AlreadyLooted; }
        internal static object AutoGeneratedGetMemberValueLastNotifiedHour(object o) { return ((BattlefieldData)o).LastNotifiedHour; }
    }

    // =============================================
    // 2. SAVE TYPE DEFINER (1.3 Pattern)
    // Decompile'dan: VillageNeedsToolsIssueTypeDefiner base(600000)
    //                FamilyFeudIssueTypeDefiner base(1087000)
    // Benzersiz ID: 950000 (ï¿½akï¿½ï¿½ma ï¿½nleme)
    // =============================================
    /* public class BattlefieldDataTypeDefiner : SaveableTypeDefiner
    {
        public BattlefieldDataTypeDefiner()
            : base(950000)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(BattlefieldData), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<BattlefieldData>));
        }

        
    } */


}

