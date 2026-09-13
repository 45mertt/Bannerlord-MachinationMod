using System.Linq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public enum WorkshopLevel
    {
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5
    }

    public enum WorkshopStrategy
    {
        Normal = 0,
        Guild = 1,
        WarProduction = 2,
        Smuggling = 3,
        Sweatshop = 4,
        Hoarding = 5
    }

    public static class WorkshopUpgradeCost
    {
        public static (int gold, string materialDescription) GetUpgradeCost(WorkshopLevel currentLevel)
        {
            switch (currentLevel)
            {
                case WorkshopLevel.Level1:
                    return (5000, "Basic Tools");
                case WorkshopLevel.Level2:
                    return (12000, "Refined Iron");
                case WorkshopLevel.Level3:
                    return (25000, "Master Crafting Materials");
                case WorkshopLevel.Level4:
                    return (50000, "Legendary Components");
                default:
                    return (0, "Max Level");
            }
        }
    }

    public class WorkshopKingdomData
    {
        private Dictionary<Workshop, int> _workshopLevels = new Dictionary<Workshop, int>();
        [SaveableProperty(1)]
        public Dictionary<Workshop, int> WorkshopLevels { get { return _workshopLevels ?? (_workshopLevels = new Dictionary<Workshop, int>()); } set { _workshopLevels = value; } }

        private Dictionary<Workshop, bool> _confiscatedWorkshops = new Dictionary<Workshop, bool>();
        [SaveableProperty(2)]
        public Dictionary<Workshop, bool> ConfiscatedWorkshops { get { return _confiscatedWorkshops ?? (_confiscatedWorkshops = new Dictionary<Workshop, bool>()); } set { _confiscatedWorkshops = value; } }

        private Dictionary<Workshop, Hero> _confiscatedBy = new Dictionary<Workshop, Hero>();
        [SaveableProperty(3)]
        public Dictionary<Workshop, Hero> ConfiscatedBy { get { return _confiscatedBy ?? (_confiscatedBy = new Dictionary<Workshop, Hero>()); } set { _confiscatedBy = value; } }

        private Dictionary<string, float> _marketPriceModifiers = new Dictionary<string, float>();
        [SaveableProperty(4)]
        public Dictionary<string, float> MarketPriceModifiers { get { return _marketPriceModifiers ?? (_marketPriceModifiers = new Dictionary<string, float>()); } set { _marketPriceModifiers = value; } }

        private Dictionary<Workshop, WorkshopStrategy> _workshopStrategies = new Dictionary<Workshop, WorkshopStrategy>();
        [SaveableProperty(5)]
        public Dictionary<Workshop, WorkshopStrategy> WorkshopStrategies { get { return _workshopStrategies ?? (_workshopStrategies = new Dictionary<Workshop, WorkshopStrategy>()); } set { _workshopStrategies = value; } }

        public WorkshopKingdomData()
        {
        }

        public int GetLevel(Workshop w)
        {
            if (WorkshopLevels.TryGetValue(w, out int level))
            {
                return level;
            }
            return 1;
        }

        public void SetLevel(Workshop w, int level)
        {
            WorkshopLevels[w] = level;
        }

        public bool IsConfiscated(Workshop w)
        {
            if (ConfiscatedWorkshops.TryGetValue(w, out bool isConfiscated))
            {
                return isConfiscated;
            }
            return false;
        }

        public void SetConfiscated(Workshop w, Hero by)
        {
            ConfiscatedWorkshops[w] = true;
            ConfiscatedBy[w] = by;
        }

        public void ClearConfiscation(Workshop w)
        {
            ConfiscatedWorkshops.Remove(w);
            ConfiscatedBy.Remove(w);
        }

        public float GetPriceModifier(string categoryId)
        {
            if (MarketPriceModifiers.TryGetValue(categoryId, out float modifier))
            {
                return modifier;
            }
            return 1f;
        }

        public void SetPriceModifier(string categoryId, float modifier)
        {
            MarketPriceModifiers[categoryId] = modifier;
        }

        public WorkshopStrategy GetStrategy(Workshop w)
        {
            if (WorkshopStrategies.TryGetValue(w, out WorkshopStrategy strategy))
            {
                return strategy;
            }
            return WorkshopStrategy.Normal;
        }

        public void SetStrategy(Workshop w, WorkshopStrategy strategy)
        {
            WorkshopStrategies[w] = strategy;
        }

        public void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in WorkshopLevels.ToList()) { if (!(pair.Key != null)) WorkshopLevels.Remove(pair.Key); }
                foreach (var pair in ConfiscatedWorkshops.ToList()) { if (!(pair.Key != null)) ConfiscatedWorkshops.Remove(pair.Key); }
                foreach (var pair in ConfiscatedBy.ToList()) { if (!(pair.Key != null && pair.Value != null)) ConfiscatedBy.Remove(pair.Key); }
                foreach (var pair in MarketPriceModifiers.ToList()) { if (!(pair.Key != null)) MarketPriceModifiers.Remove(pair.Key); }
                foreach (var pair in WorkshopStrategies.ToList()) { if (!(pair.Key != null)) WorkshopStrategies.Remove(pair.Key); }
            }

            dataStore.SyncData("_workshopLevels", ref _workshopLevels);
            dataStore.SyncData("_confiscatedWorkshops", ref _confiscatedWorkshops);
            dataStore.SyncData("_confiscatedBy", ref _confiscatedBy);
            dataStore.SyncData("_marketPriceModifiers", ref _marketPriceModifiers);
            dataStore.SyncData("_workshopStrategies", ref _workshopStrategies);
        }
    }

    public class WorkshopEventData
    {
        [SaveableField(1)] public Workshop TargetWorkshop;
        [SaveableField(2)] public Hero OwnerHero;
        [SaveableField(3)] public Kingdom EnemyFaction;
        [SaveableField(4)] public CampaignTime EventTime;
    }
}
