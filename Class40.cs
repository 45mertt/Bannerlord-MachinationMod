using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class StrikeData
    {
        [SaveableField(1)] public int StolenTroopCount;
        [SaveableField(2)] public bool IsActive;
    }

    /* public class StrikeSaveDefiner : SaveableTypeDefiner
    {
        public StrikeSaveDefiner() : base(998_112_334) { }
        
        protected override void DefineClassTypes() { AddClassDefinition(typeof(StrikeData), 1); }
        
        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<Settlement, StrikeData>));
        }
    } */

    public static class StrikeManager
    {
        private static Dictionary<Settlement, StrikeData> _strikeRecords = new Dictionary<Settlement, StrikeData>();
        public static Dictionary<Settlement, StrikeData> StrikeRecords { get { return _strikeRecords ?? (_strikeRecords = new Dictionary<Settlement, StrikeData>()); } set { _strikeRecords = value; } }

        public static void StartStrike(Settlement town)
        {
            if (StrikeRecords.ContainsKey(town) && StrikeRecords[town].IsActive) return;

            if (town.Town.GarrisonParty == null || town.Town.GarrisonParty.MemberRoster.TotalManCount < 10) return;

            int totalGarrison = town.Town.GarrisonParty.MemberRoster.TotalManCount;
            // Garnizonun %40'Ä±nÄ± sokaÄŸa dÃ¶kÃ¼yoruz
            int strikers = (int)(totalGarrison * 0.4f);

            // Manuel Silme Ä°ÅŸlemi (API uyumlu)
            var roster = town.Town.GarrisonParty.MemberRoster;
            int removedCount = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                if (removedCount >= strikers) break;

                // KahramanlarÄ± (Hero) silme!
                if (!roster.GetCharacterAtIndex(i).IsHero)
                {
                    int countInStack = roster.GetElementNumber(i);
                    int toRemove = Math.Min(countInStack, strikers - removedCount);

                    if (toRemove > 0)
                    {
                        roster.AddToCountsAtIndex(i, -toRemove);
                        removedCount += toRemove;
                    }
                }
            }

            StrikeData data = new StrikeData { IsActive = true, StolenTroopCount = strikers };
            if (StrikeRecords.ContainsKey(town)) StrikeRecords[town] = data;
            else StrikeRecords.Add(town, data);
        }

        public static void EndStrike(Settlement town, bool restoreTroops)
        {
            if (!StrikeRecords.ContainsKey(town)) return;

            if (restoreTroops && town.Town.GarrisonParty != null)
            {
                int count = StrikeRecords[town].StolenTroopCount;
                CharacterObject troop = town.Culture.EliteBasicTroop;
                // EÄŸer elit birlik yoksa standart birlik ver
                if (troop == null) troop = town.Culture.BasicTroop;

                if (troop != null)
                {
                    town.Town.GarrisonParty.AddElementToMemberRoster(troop, count);
                }
            }

            StrikeRecords.Remove(town);
        }
    }
}

