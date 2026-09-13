using ClassLibrary22;
using RebellionsAndDemographics;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class RebellionsSaveDefiner : SaveableTypeDefiner
    {
        public RebellionsSaveDefiner() : base(899_123_456) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CastleData), 1);
            AddClassDefinition(typeof(EmbargoData), 2);
            AddClassDefinition(typeof(VirtualWorkshopShare), 3);
            AddClassDefinition(typeof(WorkshopEventData), 4);
            AddClassDefinition(typeof(TradeWar), 5);
            AddClassDefinition(typeof(SpeculationData), 6);
            AddClassDefinition(typeof(PendingAllianceData), 7);
            AddClassDefinition(typeof(WorkshopKingdomData), 8);
            AddClassDefinition(typeof(SettlementPopulationData), 9);
            AddClassDefinition(typeof(CustomPartyComponent), 10);
            AddClassDefinition(typeof(StrikeData), 11);
            AddClassDefinition(typeof(WarOrder), 12);
            AddClassDefinition(typeof(CorruptionData), 13);
            AddClassDefinition(typeof(BattlefieldData), 14);
            AddClassDefinition(typeof(MonopolySlot), 15);
            AddClassDefinition(typeof(LoyaltyKingdomData), 16);
            AddClassDefinition(typeof(KingdomAgendaWrapper), 17);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(CastleDoctrine), 100);
            AddEnumDefinition(typeof(TreasuryStrategy), 101);
            AddEnumDefinition(typeof(WorkshopStrategy), 102);
            AddEnumDefinition(typeof(SpeculationType), 103);
            AddEnumDefinition(typeof(LordIntrigueBehavior.IntrigueOfferType), 104);
            AddEnumDefinition(typeof(ConferenceQuestType), 105);
            AddEnumDefinition(typeof(LoyaltyKingdomBehavior.LoyaltyTaskType), 108);
            AddEnumDefinition(typeof(SiegeSpecialization), 106);
            AddEnumDefinition(typeof(WarOrderType), 107);
        }

        private void SafeConstructContainerDefinition(System.Type type)
        {
            try
            {
                ConstructContainerDefinition(type);
            }
            catch
            {
                // Ignore if it's already registered natively or by another mod
            }
        }

        protected override void DefineContainerDefinitions()
        {
            SafeConstructContainerDefinition(typeof(Dictionary<string, CampaignTime>));
            SafeConstructContainerDefinition(typeof(Dictionary<Kingdom, int>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, CastleData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, string>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, TroopRoster>));
            SafeConstructContainerDefinition(typeof(List<Hero>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, int>));
            SafeConstructContainerDefinition(typeof(List<PendingAllianceData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, CorruptionData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, WarOrder>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, SettlementPopulationData>));
            SafeConstructContainerDefinition(typeof(List<string>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, bool>));

            SafeConstructContainerDefinition(typeof(List<BattlefieldData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Kingdom, Settlement>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, int>));
            SafeConstructContainerDefinition(typeof(List<EmbargoData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, LordIntrigueBehavior.IntrigueOfferType>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, CampaignTime>));
            SafeConstructContainerDefinition(typeof(Dictionary<Clan, float>));
            SafeConstructContainerDefinition(typeof(Dictionary<Kingdom, bool>));
            SafeConstructContainerDefinition(typeof(Dictionary<Kingdom, CampaignTime>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, bool>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, float>));
            SafeConstructContainerDefinition(typeof(List<VirtualWorkshopShare>));
            SafeConstructContainerDefinition(typeof(Dictionary<Workshop, int>));
            SafeConstructContainerDefinition(typeof(Dictionary<Workshop, bool>));
            SafeConstructContainerDefinition(typeof(Dictionary<Workshop, Hero>));
            SafeConstructContainerDefinition(typeof(Dictionary<Workshop, WorkshopStrategy>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, string>));
            SafeConstructContainerDefinition(typeof(List<TradeWar>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, SpeculationData>));

            SafeConstructContainerDefinition(typeof(List<CharacterObject>));
            SafeConstructContainerDefinition(typeof(List<PolicyObject>));
            SafeConstructContainerDefinition(typeof(List<WorkshopEventData>));
            SafeConstructContainerDefinition(typeof(List<SpeculationData>));
            SafeConstructContainerDefinition(typeof(Dictionary<Kingdom, KingdomAgendaWrapper>));

            // HashSet tanımlamaları mimari tarafından desteklenmediği için kaldırıldı.
            // Benzersiz veri gruplamaları için List<T> veya Dictionary<T, bool> kullanmalısın.

            SafeConstructContainerDefinition(typeof(Dictionary<string, double>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, SiegeSpecialization>));
            SafeConstructContainerDefinition(typeof(Dictionary<Clan, int>));
            SafeConstructContainerDefinition(typeof(Dictionary<Clan, Settlement>));
            SafeConstructContainerDefinition(typeof(Dictionary<Clan, CampaignTime>));
            SafeConstructContainerDefinition(typeof(Dictionary<Clan, bool>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, int>));
            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, CampaignTime>));
            SafeConstructContainerDefinition(typeof(Dictionary<string, Kingdom>));
            SafeConstructContainerDefinition(typeof(Dictionary<Hero, float>));
            SafeConstructContainerDefinition(typeof(List<Kingdom>));
            SafeConstructContainerDefinition(typeof(List<Settlement>));

            SafeConstructContainerDefinition(typeof(Dictionary<Settlement, StrikeData>));
        }
    }
}

