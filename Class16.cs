using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class ShadowGarrisonBehavior : CampaignBehaviorBase
    {
        // Singleton Instance: Diğer sınıfların (Limiter gibi) buraya ulaşmasını sağlar.
        public static ShadowGarrisonBehavior Instance { get; private set; }

        private Dictionary<string, TroopRoster> _villageGarrisons = new Dictionary<string, TroopRoster>();
        [SaveableProperty(1)]
        public Dictionary<string, TroopRoster> VillageGarrisons { get { return _villageGarrisons ?? (_villageGarrisons = new Dictionary<string, TroopRoster>()); } set { _villageGarrisons = value; } }

        private MobileParty _tempGarrisonParty = null;
        private string _currentSettlementId = null;

        public ShadowGarrisonBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in VillageGarrisons.ToList()) { if (!(pair.Key != null && pair.Value != null)) VillageGarrisons.Remove(pair.Key); }
            }
            dataStore.SyncData("_villageGarrisons", ref _villageGarrisons);
        }

        // DİĞER MODÜLLERİN ERİŞMESİ İÇİN GEREKLİ METOT
        public TroopRoster GetGarrison(Settlement settlement)
        {
            if (!settlement.IsVillage) return null;

            // Eğer liste henüz oluşmamışsa null dönme, boş liste dön (Hata önleyici)
            if (_villageGarrisons == null) return null;

            if (!_villageGarrisons.ContainsKey(settlement.StringId))
                return null; // Henüz garnizonu yoksa null dön

            return _villageGarrisons[settlement.StringId];
        }

        // --- Geriye kalan metotlar (ApplyTrainingResults, OnMapEventStarted vb.) aynen kalacak ---
        // Sadece yukarıdaki GetGarrison metodunu biraz daha güvenli hale getirdim.
        // Aşağısı senin attığın kodun aynısıdır, kopyala-yapıştır yaparken sorun olmasın diye tam halini veriyorum.

        public void ApplyTrainingResults(Settlement settlement, int totalXpToDistribute)
        {
            var militiaParty = settlement.MilitiaPartyComponent?.Party;
            TroopRoster convertedFromMilitia = TroopRoster.CreateDummyTroopRoster();
            int militiaPromotedCount = 0;

            if (militiaParty != null && militiaParty.MemberRoster.TotalManCount > 0)
            {
                var troops = militiaParty.MemberRoster.GetTroopRoster().ToList();
                int totalMilitia = militiaParty.MemberRoster.TotalManCount;
                int baseXpPerMilitia = totalXpToDistribute / Math.Max(1, totalMilitia);
                int conversionThreshold = 500;

                foreach (var element in troops)
                {
                    if (element.Character == null) continue;
                    if (element.Character.Tier <= 3 && baseXpPerMilitia >= conversionThreshold)
                    {
                        CharacterObject targetTroop = GetRegularTarget(element.Character, settlement.Culture);
                        if (targetTroop != null)
                        {
                            int numberToConvert = element.Number;
                            militiaParty.MemberRoster.AddToCounts(element.Character, -numberToConvert);
                            convertedFromMilitia.AddToCounts(targetTroop, numberToConvert);
                            militiaPromotedCount += numberToConvert;
                        }
                    }
                }
            }

            var garrisonRoster = GetGarrisonOrCreate(settlement); // Yardımcı metot kullanıyoruz
            int garrisonPromotedCount = 0;

            if (garrisonRoster != null && garrisonRoster.TotalManCount > 0)
            {
                var garrisonTroops = garrisonRoster.GetTroopRoster().ToList();
                int totalGarrison = garrisonRoster.TotalManCount;
                int baseXpPerGarrison = (int)((totalXpToDistribute / Math.Max(1, totalGarrison)) * 0.8f);

                foreach (var element in garrisonTroops)
                {
                    if (element.Character == null) continue;

                    if (baseXpPerGarrison > 400 && element.Character.UpgradeTargets.Length > 0)
                    {
                        var target = element.Character.UpgradeTargets[0];
                        int promoNum = element.Number / 2;
                        if (promoNum > 0)
                        {
                            garrisonRoster.AddToCounts(element.Character, -promoNum);
                            garrisonRoster.AddToCounts(target, promoNum);
                            garrisonPromotedCount += promoNum;
                        }
                    }
                }
            }

            if (militiaPromotedCount > 0)
            {
                if (garrisonRoster == null) garrisonRoster = TroopRoster.CreateDummyTroopRoster();
                garrisonRoster.Add(convertedFromMilitia);
                SaveGarrisonData(settlement.StringId, garrisonRoster);
            }

            TextObject msg = new TextObject("{=str_shadow_train_result}Training Result:{newline}{MIL_NUM} Militia joined the professional army.{newline}{GAR_NUM} Garrison troops were promoted!");
            msg.SetTextVariable("MIL_NUM", militiaPromotedCount);
            msg.SetTextVariable("GAR_NUM", garrisonPromotedCount);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
        }

        private CharacterObject GetRegularTarget(CharacterObject militia, CultureObject culture)
        {
            if (militia.UpgradeTargets != null && militia.UpgradeTargets.Length > 0)
                return militia.UpgradeTargets[0];
            var basicTroop = culture.BasicTroop;
            if (basicTroop != null && basicTroop.UpgradeTargets.Length > 0)
                return basicTroop.UpgradeTargets[0];
            return basicTroop;
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            Settlement settlement = mapEvent.MapEventSettlement;
            if (settlement == null || !settlement.IsVillage) return;

            TroopRoster storedRoster = GetGarrison(settlement);
            if (storedRoster == null || storedRoster.TotalManCount <= 0) return;

            if (mapEvent.AttackerSide.LeaderParty?.MapFaction != settlement.MapFaction)
            {
                string partyId = "shadow_defenders_" + settlement.StringId;
                TextObject customName = new TextObject("{=str_guards}{SETTLEMENT} Garrison Unit");
                customName.SetTextVariable("SETTLEMENT", settlement.Name);

                MobileParty defenseParty = MobileParty.CreateParty(partyId, new CustomPartyComponent(customName, settlement));

                defenseParty.InitializeMobilePartyAtPosition(storedRoster, TroopRoster.CreateDummyTroopRoster(), settlement.GatePosition);
                defenseParty.SetPartyUsedByQuest(true);
                defenseParty.Ai.DisableAi();

                defenseParty.Party.SetCustomOwner(settlement.OwnerClan.Leader);
                defenseParty.MapEventSide = mapEvent.DefenderSide;

                TextObject msg = new TextObject("{=str_shadow_defend_start}{SETTLEMENT}: {COUNT} Garrison troops joined the defense!");
                msg.SetTextVariable("SETTLEMENT", settlement.Name);
                msg.SetTextVariable("COUNT", storedRoster.TotalManCount);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
            }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            Settlement settlement = mapEvent.MapEventSettlement;
            if (settlement == null || !settlement.IsVillage) return;

            var partiesToCheck = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties).ToList();

            foreach (var sideParty in partiesToCheck)
            {
                if (sideParty.Party.IsMobile && sideParty.Party.MobileParty.StringId.StartsWith("shadow_defenders_" + settlement.StringId))
                {
                    MobileParty shadowParty = sideParty.Party.MobileParty;

                    TroopRoster survivors = TroopRoster.CreateDummyTroopRoster();
                    if (shadowParty.MemberRoster.TotalManCount > 0)
                        survivors.Add(shadowParty.MemberRoster);

                    SaveGarrisonData(settlement.StringId, survivors);
                    DestroyPartyAction.Apply(null, shadowParty);
                    break;
                }
            }
        }

        public void OpenGarrisonManagement(Settlement settlement)
        {
            if (!settlement.IsVillage) return;
            if (_tempGarrisonParty != null) CleanupTempParty();

            _currentSettlementId = settlement.StringId;
            TroopRoster roster = GetGarrisonOrCreate(settlement);

            TextObject pName = new TextObject("{=str_garr_mng}{SETTLEMENT} Guard Unit");
            pName.SetTextVariable("SETTLEMENT", settlement.Name);

            _tempGarrisonParty = MobileParty.CreateParty("shadow_garrison_mng_" + settlement.StringId, new CustomPartyComponent(pName, null));
            _tempGarrisonParty.InitializeMobilePartyAtPosition(roster, TroopRoster.CreateDummyTroopRoster(), settlement.GatePosition);
            _tempGarrisonParty.IsVisible = false;
            _tempGarrisonParty.SetPartyUsedByQuest(true);

            int garrisonLimit = 20 + (int)(settlement.Village.Hearth * 0.5f);

            var partyLogic = new PartyScreenLogic();
            var initData = new PartyScreenLogicInitializationData
            {
                LeftOwnerParty = PartyBase.MainParty,
                RightOwnerParty = _tempGarrisonParty.Party,
                LeftMemberRoster = PartyBase.MainParty.MemberRoster,
                RightMemberRoster = _tempGarrisonParty.MemberRoster,
                LeftPrisonerRoster = MobileParty.MainParty.PrisonRoster,
                RightPrisonerRoster = _tempGarrisonParty.PrisonRoster,
                LeftLeaderHero = Hero.MainHero,
                RightLeaderHero = null,
                LeftPartyName = Hero.MainHero.Name,
                RightPartyName = pName,
                Header = new TextObject("{=str_manage}Shadow Garrison Storage"),
                RightPartyMembersSizeLimit = garrisonLimit,
                RightPartyPrisonersSizeLimit = 0,
                LeftPartyMembersSizeLimit = PartyBase.MainParty.PartySizeLimit,
                LeftPartyPrisonersSizeLimit = PartyBase.MainParty.PrisonerSizeLimit,
                PartyPresentationDoneButtonDelegate = OnPartyScreenDone
            };

            partyLogic.Initialize(initData);
            var partyState = Game.Current.GameStateManager.CreateState<PartyState>();
            partyState.PartyScreenLogic = partyLogic;
            Game.Current.GameStateManager.PushState(partyState);
        }

        private bool OnPartyScreenDone(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, FlattenedTroopRoster takenPrisonerRoster, FlattenedTroopRoster releasedPrisonerRoster, bool isForced, PartyBase leftParty, PartyBase rightParty)
        {
            if (_tempGarrisonParty != null && _currentSettlementId != null)
            {
                TroopRoster finalRoster = TroopRoster.CreateDummyTroopRoster();
                finalRoster.Add(rightMemberRoster);
                SaveGarrisonData(_currentSettlementId, finalRoster);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=str_mig_updated}Garrison updated.").ToString(), Colors.Green));
            }
            return true;
        }

        private void OnTick(float dt)
        {
            if (_tempGarrisonParty != null && !(Game.Current.GameStateManager.ActiveState is PartyState))
            {
                CleanupTempParty();
            }
        }

        private void CleanupTempParty()
        {
            if (_tempGarrisonParty != null)
            {
                DestroyPartyAction.Apply(null, _tempGarrisonParty);
                _tempGarrisonParty = null;
                _currentSettlementId = null;
            }
        }

        private void SaveGarrisonData(string settlementId, TroopRoster roster)
        {
            if (_villageGarrisons.ContainsKey(settlementId))
                _villageGarrisons[settlementId] = roster;
            else
                _villageGarrisons.Add(settlementId, roster);
        }

        // Kendi içinde kullanması için Create eden versiyon
        private TroopRoster GetGarrisonOrCreate(Settlement settlement)
        {
            if (!_villageGarrisons.ContainsKey(settlement.StringId))
                _villageGarrisons.Add(settlement.StringId, TroopRoster.CreateDummyTroopRoster());
            return _villageGarrisons[settlement.StringId];
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (!settlement.IsVillage || settlement.OwnerClan != Clan.PlayerClan) return;
            if (_currentSettlementId == settlement.StringId) return;

            var roster = GetGarrison(settlement);
            if (roster == null || roster.TotalManCount == 0) return;

            int totalWage = 0;
            foreach (var element in roster.GetTroopRoster())
                if (element.Character != null) totalWage += element.Character.TroopWage * element.Number;

            int villageTaxIncome = (int)(settlement.Village.TradeTaxAccumulated / 7);

            if (villageTaxIncome >= totalWage)
            {
                settlement.Village.TradeTaxAccumulated -= totalWage;
            }
            else
            {
                int deficit = totalWage - villageTaxIncome;
                settlement.Village.TradeTaxAccumulated = 0;
                if (Hero.MainHero.Gold >= deficit)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, deficit, true);
                }
                else
                {
                    int deserters = Math.Max(1, roster.TotalManCount / 10);
                    var firstElement = roster.GetTroopRoster().FirstOrDefault(x => x.Character != null);
                    if (firstElement.Character != null)
                        roster.AddToCounts(firstElement.Character, -deserters);

                    TextObject msg = new TextObject("{=str_shadow_wage_fail}{SETTLEMENT}: {COUNT} troops deserted due to unpaid wages!");
                    msg.SetTextVariable("SETTLEMENT", settlement.Name);
                    msg.SetTextVariable("COUNT", deserters);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                }
            }
        }
    }

    public class CustomPartyComponent : PartyComponent
    {
        [SaveableField(1)]
        private TextObject _customName;

        [SaveableField(2)]
        private Settlement _linkedSettlement;

        public CustomPartyComponent()
        {
            _customName = new TextObject("{=str_shadow_def}Shadow Garrison");
        }

        public CustomPartyComponent(TextObject name, Settlement settlement)
        {
            _customName = name;
            _linkedSettlement = settlement;
        }

        public override TextObject Name => _customName;
        public override Settlement HomeSettlement => _linkedSettlement;
        public override Hero PartyOwner => _linkedSettlement != null ? _linkedSettlement.OwnerClan.Leader : Hero.MainHero;
        public override Hero Leader => null;
        public override Banner GetDefaultComponentBanner() => PartyOwner?.Clan?.Banner;
    }
}
