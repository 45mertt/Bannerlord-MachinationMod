using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class CorruptionData
    {
        [SaveableField(1)] public int ProjectedTax; // Oyunun hesapladÄ±ÄŸÄ± ideal vergi
        [SaveableField(2)] public int ActualTax;    // Hazineye giren (Ã‡alÄ±nmÄ±ÅŸ hali)
        [SaveableField(3)] public Hero Culprit;     // SuÃ§lu
        [SaveableField(4)] public bool IsExposed;   // Defter incelendi mi?
        [SaveableField(5)] public CampaignTime LastResolvedDate; // Son olay ne zaman bitti?
    }

    /* public class CorruptionSaveDefiner : SaveableTypeDefiner
    {
        public CorruptionSaveDefiner() : base(198_456_998) { }
        
        protected override void DefineClassTypes() { AddClassDefinition(typeof(CorruptionData), 1); }
        protected override void DefineContainerDefinitions() { ConstructContainerDefinition(typeof(Dictionary<Settlement, CorruptionData>)); }
    } */

    public static class CorruptionManager
    {
        public static Dictionary<Settlement, CorruptionData> _cityRecords = new Dictionary<Settlement, CorruptionData>();
        public static Dictionary<Settlement, CorruptionData> CityRecords { get { return _cityRecords ?? (_cityRecords = new Dictionary<Settlement, CorruptionData>()); } set { _cityRecords = value; } }

        public static void CalculateDailyCorruption(Settlement town)
        {
            if (!town.IsTown) return;

            // --- 1. SOÄUMA KONTROLÃœ (COOLDOWN) ---
            if (CityRecords.TryGetValue(town, out CorruptionData existingData))
            {
                // EÄŸer son olay 15 gÃ¼nden kÄ±saysa yeni olay yaratma
                if (existingData.LastResolvedDate != CampaignTime.Never && existingData.LastResolvedDate.ElapsedDaysUntilNow < 15f)
                    return;
            }

            // --- 2. GERÃ‡EKÃ‡Ä° VERGÄ° HESABI (OYUN MOTORUNDAN) ---
            // Bu formÃ¼l oyunun arayÃ¼zÃ¼nde gÃ¶rdÃ¼ÄŸÃ¼n rakamÄ± verir.
            int modelTax = (int)Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(town.Town).ResultNumber * 200;

            int stolenAmount = 0;
            Hero currentCulprit = null;

            // --- 3. YOLSUZLUK Ä°HTÄ°MALÄ° (REFAH VE GÃœVENLÄ°K FAKTÃ–RÃœ) ---
            float corruptionChance = 0.05f; // Taban ÅŸans %5 (Ã‡ok dÃ¼ÅŸÃ¼rdÃ¼k)

            if (town.Town.Security < 40) corruptionChance += 0.1f; // GÃ¼venlik yoksa artar
            if (town.Town.Prosperity < 2500) corruptionChance += 0.1f; // Fakirlik varsa artar
            if (town.Town.Loyalty > 80) corruptionChance -= 0.05f; // Sadakat yÃ¼ksekse azalÄ±r

            // AdaylarÄ± Tara
            List<Hero> suspects = new List<Hero>(town.Notables);
            if (town.Town.Governor != null) suspects.Add(town.Town.Governor);

            foreach (var suspect in suspects)
            {
                // Karakter Analizi
                int honor = suspect.GetTraitLevel(DefaultTraits.Honor);
                int greed = suspect.GetTraitLevel(DefaultTraits.Generosity) < 0 ? 1 : 0;

                // Sadece "Onursuz" veya "Cimri" olanlar Ã§alar
                if (honor < 0 || greed > 0)
                {
                    if (MBRandom.RandomFloat < corruptionChance)
                    {
                        // Ã‡alÄ±nacak Miktar (%5 ile %15 arasÄ± - Oyuncuyu batÄ±rmasÄ±n)
                        int steal = (int)(modelTax * (0.25f + (greed * 0.15f)));
                        stolenAmount = steal;
                        currentCulprit = suspect;
                        break; // Sadece bir suÃ§lu seÃ§iyoruz
                    }
                }
            }

            // --- 4. KAYIT ---
            CorruptionData data = new CorruptionData
            {
                ProjectedTax = modelTax, // ArtÄ±k paneldekiyle tutacak
                ActualTax = modelTax - stolenAmount,
                Culprit = currentCulprit,
                IsExposed = false,
                LastResolvedDate = existingData?.LastResolvedDate ?? CampaignTime.Never
            };

            if (CityRecords.ContainsKey(town)) CityRecords[town] = data;
            else CityRecords.Add(town, data);
        }
    }
}

