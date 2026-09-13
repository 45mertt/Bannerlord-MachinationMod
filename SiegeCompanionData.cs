using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace RebellionsAndDemographics
{
    public enum SiegeSpecialization
    {
        None,
        Ram,
        Tower,
        Ballista,
        Onager,
        Trebuchet
    }

    public class SiegeCompanionData
    {
        // companion_id -> spec
        public Dictionary<string, SiegeSpecialization> CompanionSpecs = new Dictionary<string, SiegeSpecialization>();
        
        // companion_id -> level (1 to 5)
        public Dictionary<string, int> CompanionLevels = new Dictionary<string, int>();
        
        // companion_id -> training end time
        public Dictionary<string, CampaignTime> CompanionTrainingEndTime = new Dictionary<string, CampaignTime>();

        // companion_id -> training target settlement id
        public Dictionary<string, string> CompanionTrainingSettlement = new Dictionary<string, string>();

        public void AssignSpecialization(string heroId, SiegeSpecialization spec)
        {
            CompanionSpecs[heroId] = spec;
            if (!CompanionLevels.ContainsKey(heroId))
                CompanionLevels[heroId] = 0; // 0 means currently in initial training
        }

        public int GetLevel(string heroId)
        {
            if (CompanionLevels.TryGetValue(heroId, out int lvl))
                return lvl;
            return 0;
        }
    }
}
