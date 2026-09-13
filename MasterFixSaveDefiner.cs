using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using RebellionsAndDemographics;

namespace ClassLibrary22
{
    /* public class MasterFixSaveDefiner : SaveableTypeDefiner
    {
        public MasterFixSaveDefiner() : base(999_123_456) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CastleData), 1);
            AddClassDefinition(typeof(EmbargoData), 2);
            AddClassDefinition(typeof(VirtualWorkshopShare), 3);
            AddClassDefinition(typeof(WorkshopEventData), 4);
            AddClassDefinition(typeof(TradeWar), 5);
            AddClassDefinition(typeof(SpeculationData), 6);
            AddClassDefinition(typeof(PendingAllianceData), 7);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(CastleDoctrine), 10);
            AddEnumDefinition(typeof(TreasuryStrategy), 11);
            AddEnumDefinition(typeof(WorkshopStrategy), 12);
            AddEnumDefinition(typeof(SpeculationType), 13);
            AddEnumDefinition(typeof(LordIntrigueBehavior.IntrigueOfferType), 14);
            AddEnumDefinition(typeof(ConferenceQuestType), 15);
            AddEnumDefinition(typeof(SiegeSpecialization), 16);
        }

        protected override void DefineContainerDefinitions()
        {
            // === Master Consolidated Container Definitions ===
            // By putting all of them here, we ensure no duplicates exist which could crash the SaveSystem initialization.

            // Lists
            try { ConstructContainerDefinition(typeof(List<string>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<Hero>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<CharacterObject>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<PolicyObject>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<PendingAllianceData>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<BattlefieldData>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<TradeWar>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<EmbargoData>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<VirtualWorkshopShare>)); } catch { }
            try { ConstructContainerDefinition(typeof(List<WorkshopEventData>)); } catch { }

            // HashSets
            try { ConstructContainerDefinition(typeof(HashSet<string>)); } catch { }
            try { ConstructContainerDefinition(typeof(HashSet<TaleWorlds.CampaignSystem.Hero>)); } catch { }
            try { ConstructContainerDefinition(typeof(HashSet<TaleWorlds.MountAndBlade.Agent>)); } catch { }


            // Dictionaries - string keys
            try { ConstructContainerDefinition(typeof(Dictionary<string, int>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, float>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, double>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, bool>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, string>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, CampaignTime>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, TroopRoster>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, SpeculationData>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<string, SiegeSpecialization>)); } catch { }

            // Dictionaries - Settlement keys
            try { ConstructContainerDefinition(typeof(Dictionary<Settlement, string>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Settlement, SettlementPopulationData>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Settlement, StrikeData>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Settlement, CastleData>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Settlement, CorruptionData>)); } catch { }

            // Dictionaries - Hero keys
            try { ConstructContainerDefinition(typeof(Dictionary<Hero, int>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Hero, CampaignTime>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Hero, LordIntrigueBehavior.IntrigueOfferType>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Hero, WarOrder>)); } catch { }

            // Dictionaries - Kingdom keys
            try { ConstructContainerDefinition(typeof(Dictionary<Kingdom, int>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Kingdom, bool>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Kingdom, Settlement>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Kingdom, CampaignTime>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Kingdom, List<PolicyObject>>)); } catch { }

            // Dictionaries - Clan keys
            try { ConstructContainerDefinition(typeof(Dictionary<Clan, int>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Clan, Settlement>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Clan, CampaignTime>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Clan, float>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Clan, bool>)); } catch { }

            // Dictionaries - Workshop keys
            try { ConstructContainerDefinition(typeof(Dictionary<Workshop, int>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Workshop, bool>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Workshop, Hero>)); } catch { }
            try { ConstructContainerDefinition(typeof(Dictionary<Workshop, WorkshopStrategy>)); } catch { }
        }
    } */
}

