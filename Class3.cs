using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    // ========================================================================
    // KISIM 1: KAYIT SÝSTEMÝ (SAVE DATA)
    // ========================================================================
    public class PendingAllianceData
    {
        public PendingAllianceData() {}
        [SaveableField(1)] public Kingdom RebelKingdom;
        [SaveableField(2)] public CampaignTime ActivationTime;
        [SaveableField(3)] public Hero RebelLeader;
        [SaveableField(4)] public Kingdom TargetKingdom;

        public PendingAllianceData(Kingdom rebel, Kingdom target, CampaignTime t, Hero l)
        {
            RebelKingdom = rebel;
            TargetKingdom = target;
            ActivationTime = t;
            RebelLeader = l;
        }
    }

    /* public class RebelSaveDefiner : SaveableTypeDefiner
    {
        public RebelSaveDefiner() : base(198_456_790) { }

        protected override void DefineClassTypes()
        {
        }

        

    } */

    // ========================================================================
    // KISIM 2: ANA BEHAVIOR SINIFI
    // ========================================================================
    public class RebellionCoreBehavior : CampaignBehaviorBase
    {
        public static RebellionCoreBehavior Instance { get; private set; }

        // --- VERÝ SAKLAMA DEÐÝÞKENLERÝ ---
        private Dictionary<string, int> _clanUnrest = new Dictionary<string, int>();
        public Dictionary<string, int> ClanUnrest { get { return _clanUnrest ?? (_clanUnrest = new Dictionary<string, int>()); } set { _clanUnrest = value; } }

        private Dictionary<string, CampaignTime> _peaceTreaties = new Dictionary<string, CampaignTime>();
        public Dictionary<string, CampaignTime> PeaceTreaties { get { return _peaceTreaties ?? (_peaceTreaties = new Dictionary<string, CampaignTime>()); } set { _peaceTreaties = value; } }

        private List<PendingAllianceData> _pendingAlliances = new List<PendingAllianceData>();
        public List<PendingAllianceData> PendingAlliances { get { return _pendingAlliances ?? (_pendingAlliances = new List<PendingAllianceData>()); } set { _pendingAlliances = value; } }

        // DÜZELTME: Baþlangýç deðeri 'Never' olarak atanmalý.
        private CampaignTime _lastGlobalRebellionTime = CampaignTime.Never;

        // DÜZELTME: Stres eþiði 500'den 1000'e çýkarýldý. Artýk isyanlar hemen tetiklenmeyecek.
        private const int UNREST_THRESHOLD = 1000;
        private const int MAX_UNREST = 1500;

        public RebellionCoreBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in ClanUnrest.ToList()) { if (!(pair.Key != null)) ClanUnrest.Remove(pair.Key); }
                foreach (var pair in PeaceTreaties.ToList()) { if (!(pair.Key != null)) PeaceTreaties.Remove(pair.Key); }
                var pending = PendingAlliances;
                // pending.RemoveAll(x => x == null);
            }
            dataStore.SyncData("_clanUnrest", ref _clanUnrest);
            dataStore.SyncData("_peaceTreaties", ref _peaceTreaties);
            dataStore.SyncData("_lastGlobalRebellionTime", ref _lastGlobalRebellionTime);
            dataStore.SyncData("_pendingAlliances", ref _pendingAlliances);
        }
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // DÜZELTME: Eðer yeni oyunsa veya veri yoksa, isyanlarý 1 yýl boyunca yasakla.
            // Bu, Kuzey Ýmparatorluðu'nun ilk hafta parçalanmasýný engeller.
            if (_lastGlobalRebellionTime == CampaignTime.Never)
            {
                _lastGlobalRebellionTime = CampaignTime.YearsFromNow(1f);
            }
        }

        // ========================================================================
        // KISIM 3: SAATLÝK KONTROLLER (ÝTTÝFAK VE DÝPLOMASÝ ZAMANLAYICISI)
        // ========================================================================
        private void OnHourlyTick()
        {
            ProcessPendingAlliances();
        }


        private void ProcessPendingAlliances()
        {
            if (_pendingAlliances == null || _pendingAlliances.Count == 0) return;

            // Zamaný gelmiþ ittifaklarý bul
            var readyToActivate = _pendingAlliances.Where(x => x.ActivationTime.IsPast).ToList();

            foreach (var pending in readyToActivate)
            {
                // Krallýklar hala hayattaysa ve oyuncu krallýðý varsa iþlemi yap
                if (pending.RebelKingdom != null && !pending.RebelKingdom.IsEliminated &&
                    Clan.PlayerClan.Kingdom != null)
                {
                    ActivateAllianceAndTrade(pending.RebelKingdom, pending.TargetKingdom, pending.RebelLeader);
                }
                _pendingAlliances.Remove(pending);
            }
        }

        private void ActivateAllianceAndTrade(Kingdom rebelKingdom, Kingdom targetKingdom, Hero leaderHero)
        {
            if (Clan.PlayerClan.Kingdom == null) return;

            // A. Hedef Krallýða (Eski Dostlara) Savaþ Ýlan Et
            if (targetKingdom != null && !targetKingdom.IsEliminated)
            {
                if (!FactionManager.IsAtWarAgainstFaction(Clan.PlayerClan.Kingdom, targetKingdom))
                {
                    DeclareWarAction.ApplyByDefault(Clan.PlayerClan.Kingdom, targetKingdom);
                }

                // Ýliþki Cezasý (Vatan Hainliði Bedeli -100)
                foreach (var clan in targetKingdom.Clans)
                {
                    if (clan.Leader != null && clan != targetKingdom.RulingClan)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, clan.Leader, -100);
                    }
                }
            }

            // B. Ýsyancýlarla Barýþ (Eðer savaþ varsa)
            if (FactionManager.IsAtWarAgainstFaction(Clan.PlayerClan.Kingdom, rebelKingdom))
            {
                MakePeaceAction.Apply(Clan.PlayerClan.Kingdom, rebelKingdom);
            }

            // C. Ýsyancý Liderle Ýliþki Artýþý (+60)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, leaderHero, 60);

            // D. Resmi Ýttifak ve Ticaret (Reflection ile Diplomasi Zorlama)
            try
            {
                var campaignAssembly = typeof(Campaign).Assembly;

                // 1. Ýttifak (Alliance)
                Type allianceInterface = campaignAssembly.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.IAllianceCampaignBehavior");
                if (allianceInterface != null)
                {
                    MethodInfo getBehavior = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(allianceInterface);
                    object allianceBehavior = getBehavior.Invoke(Campaign.Current, null);

                    if (allianceBehavior != null)
                    {
                        MethodInfo startAlliance = allianceInterface.GetMethod("StartAlliance");
                        if (startAlliance != null)
                        {
                            startAlliance.Invoke(allianceBehavior, new object[] { Clan.PlayerClan.Kingdom, rebelKingdom });
                        }
                    }
                }

                // 2. Ticaret Anlaþmasý (Trade Agreement)
                Type tradeInterface = campaignAssembly.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.ITradeAgreementsCampaignBehavior");
                if (tradeInterface != null)
                {
                    MethodInfo getBehavior = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(tradeInterface);
                    object tradeBehavior = getBehavior.Invoke(Campaign.Current, null);

                    if (tradeBehavior != null)
                    {
                        MethodInfo makeTrade = tradeInterface.GetMethod("MakeTradeAgreement");
                        if (makeTrade != null)
                        {
                            makeTrade.Invoke(tradeBehavior, new object[] { Clan.PlayerClan.Kingdom, rebelKingdom, 50f });
                        }
                    }
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=str_turmoil_pact_complete}Diplomatic Mission Complete: We are now Allied with the Rebels!").ToString(),
                    Colors.Green));
            }
            catch (Exception)
            {
                // Reflection hatasý olursa oyun çökmesin diye yutuyoruz. Ýliþki artýþý yeterli.
            }
        }

        // ========================================================================
        // KISIM 4: GÜNLÜK DÖNGÜ (STRES, OYUN SONU VE OTOMATÝK ÝSYAN)
        // ========================================================================
        private void OnDailyTick()
        {
            // 1. YENÝ: Oyun Sonu (Sýfýr Toprak) Kontrolü
            // Her gün krallýklarýn toprak durumunu kontrol eder.
            CheckEndgameConditions();

            if (_lastGlobalRebellionTime == CampaignTime.Never) _lastGlobalRebellionTime = CampaignTime.Now;

            // 2. AYAR KONTROLÜ: Sadece ayar açýksa AI otomatik isyan çýkarýr
            if (ModSettings.EnableAIRebellions)
            {
                // Yýlda en az bir kere büyük isyan zorlamasý (Eski koddan)
                if (_lastGlobalRebellionTime.ElapsedYearsUntilNow >= 12.0f)
                {
                    ForceHardRebellion();
                }

                // Tüm krallýklarý tara ve stres seviyesini ölç
                foreach (var kingdom in Kingdom.All.ToList())
                {
                    if (kingdom.IsEliminated) continue;
                    List<Clan> potentialRebels = new List<Clan>();

                    foreach (var clan in kingdom.Clans.ToList())
                    {
                        // Filtreleme: Oyuncu, Kral, Paralý Askerler hariç
                        if (clan == Clan.PlayerClan) continue;
                        if (clan == kingdom.RulingClan || clan.IsEliminated || clan.IsUnderMercenaryService) continue;
                        if (_peaceTreaties.ContainsKey(clan.StringId) && _peaceTreaties[clan.StringId].IsFuture) continue;

                        // Stres Hesaplama
                        int relation = clan.GetRelationWithClan(kingdom.RulingClan);
                        int dailyStress = 1;
                        if (relation < -10) dailyStress += 2;
                        if (clan.Culture != kingdom.Culture) dailyStress += 1;

                        AddStress(clan, dailyStress);

                        // Eþik deðerini geçtiyse aday havuzuna ekle
                        if (GetStress(clan) >= UNREST_THRESHOLD)
                        {
                            potentialRebels.Add(clan);
                        }
                    }

                    // Ýsyan sýklýðý düþürüldü ve 10 yýl soðuma süresi eklendi
                    if (potentialRebels.Count >= 2 && MBRandom.RandomInt(1, 1000) <= 5)
                    {
                        if (_lastGlobalRebellionTime.ElapsedYearsUntilNow >= 10.0f)
                        {
                            TryStartRebellion(kingdom, potentialRebels, false);
                        }
                    }
                }
            }
        }

        // ========================================================================
        // KISIM 5: ÝSYAN BAÞLATMA VE BÜYÜK PATLAMA (REBEL LAUNCH)
        // ========================================================================

        // Dýþarýdan çaðýrmak için public wrapper
        public void LaunchRebellion(Kingdom loyalistKingdom, List<Clan> rebelClans, bool isPlayerBacked)
        {
            ExecuteRebellion(loyalistKingdom, rebelClans, isPlayerBacked);
        }

        private void ExecuteRebellion(Kingdom parentKingdom, List<Clan> rebelClans, bool isPlayerBacked = false)
        {
            SoundEvent.PlaySound2D("event:/ui/mission/horns/attack");
            _lastGlobalRebellionTime = CampaignTime.Now;

            Clan leader = rebelClans[0];
            foreach (var clan in rebelClans) CleanUpClanState(clan);

            // Yeni Krallýk Ýsmi
            TextObject movementName = new TextObject("{=str_turmoil_movement_name}{CLAN_NAME}'s Movement");
            movementName.SetTextVariable("CLAN_NAME", leader.Name);

            // Yeni Krallýk Yarat
            Kingdom rebelKingdom = Kingdom.CreateKingdom("rebel_" + MBRandom.RandomInt(99999));

            // 1. Sancak Renklerini Hesapla (Varsayýlan: Ters Çevir)
            uint rebelPrimaryColor = parentKingdom.SecondaryBannerColor;
            uint rebelSecondaryColor = parentKingdom.PrimaryBannerColor;

            // Eðer renkler aynýysa veya krallýk çok küçükse rastgele yap
            if (rebelPrimaryColor == rebelSecondaryColor || parentKingdom.Clans.Count < 2)
            {
                rebelPrimaryColor = BannerManager.GetColor(MBRandom.RandomInt(1, 50));
                rebelSecondaryColor = BannerManager.GetColor(MBRandom.RandomInt(51, 100));
            }

            // --- DÜZELTME BURASI ---
            // Eðer "Flag Change" ayarý KAPALIYSA, renkleri liderin orijinal renklerine geri çek.
            // Böylece krallýk kurulurken yanlýþ (ters) renklerle kurulmaz.
            if (!ModSettings.ChangeRebelBannerColors)
            {
                rebelPrimaryColor = leader.Banner.GetPrimaryColor();
                rebelSecondaryColor = leader.Banner.GetFirstIconColor();
            }

            // 2. Banner Nesnesini Oluþtur
            Banner customBanner = new Banner(leader.Banner.Serialize());

            // Renkleri uygula (Ayar kapalýysa zaten kendi rengini uygular, deðiþiklik olmaz)
            customBanner.ChangePrimaryColor(rebelPrimaryColor);
            customBanner.ChangeIconColors(rebelSecondaryColor);

            // 3. Krallýðý Baþlat (Artýk 'rebelPrimaryColor' doðru deðeri taþýyor)
            rebelKingdom.InitializeKingdom(movementName, movementName, parentKingdom.Culture, customBanner,
                rebelPrimaryColor, rebelSecondaryColor, leader.InitialHomeSettlement,
                new TextObject(""), new TextObject(""), new TextObject(""));

            // Liderlik Atamasý
            rebelKingdom.RulingClan = leader;
            ChangeKingdomAction.ApplyByJoinToKingdom(leader, rebelKingdom);
            rebelKingdom.RulingClan = leader; // Tekrar atayarak garantiliyoruz

            // CAN SUYU (Para ve Nüfuz)
            foreach (var c in rebelClans)
            {
                if (c.Leader != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, c.Leader, 300000, true);
                    ChangeClanInfluenceAction.Apply(c, 500);
                }
            }

            // Diðer müttefikleri taþý
            foreach (var ally in rebelClans.Skip(1))
            {
                ChangeKingdomAction.ApplyByJoinToKingdom(ally, rebelKingdom);

                // GÜNCELLENDÝ: Müttefik Sancak Kontrolü
                // Sadece ayar AÇIKSA müttefiklerin sancaðýný deðiþtir.
                if (ModSettings.ChangeRebelBannerColors)
                {
                    ally.Banner.ChangePrimaryColor(rebelPrimaryColor);
                    ally.Banner.ChangeIconColors(rebelSecondaryColor);
                }

                if (ally.Leader != null && ally.Leader.PartyBelongedTo != null)
                {
                    ally.Leader.PartyBelongedTo.Party.SetVisualAsDirty();
                }
            }

            // Ana savaþ ilaný (Sadýklar vs Ýsyancýlar)
            DeclareWarAction.ApplyByDefault(parentKingdom, rebelKingdom);

            // --- ÝSYANCILARI GÜÇLENDÝR ---
            if (isPlayerBacked)
            {
                BoostRebelCapabilities(rebelKingdom);
                TriggerLoyalistResponse(parentKingdom, rebelKingdom, isPlayerBacked);
            }

            // --- DÝPLOMASÝ BEKLEME SÝSTEMÝ ---
            if (isPlayerBacked && Clan.PlayerClan.Kingdom != null)
            {
                if (_pendingAlliances == null) _pendingAlliances = new List<PendingAllianceData>();

                _pendingAlliances.Add(new PendingAllianceData(
                    rebelKingdom,
                    parentKingdom,
                    CampaignTime.DaysFromNow(1.0f),
                    leader.Leader
                ));

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=str_turmoil_envoy_sent}Envoys sent. Diplomatic pacts will be signed in 1 day.").ToString(),
                    Colors.Yellow));
            }

            // Bilgilendirme Ekraný
            TextObject title = new TextObject("{=str_turmoil_civil_war_title}CIVIL WAR");
            TextObject info;

            if (parentKingdom.Clans.Contains(Clan.PlayerClan))
                info = new TextObject("{=str_turmoil_civil_war_player}EMERGENCY! Our kingdom {KINGDOM} has fractured! {REBEL_KINGDOM} has risen under King {LEADER}!");
            else
                info = new TextObject("{=str_turmoil_civil_war_ai}NEWS: Civil War in {KINGDOM}! {REBEL_KINGDOM} has been formed by {LEADER}.");

            info.SetTextVariable("KINGDOM", parentKingdom.Name);
            info.SetTextVariable("REBEL_KINGDOM", movementName);
            info.SetTextVariable("LEADER", leader.Leader.Name);

            InformationManager.ShowInquiry(new InquiryData(title.ToString(), info.ToString(), true, false, "OK", "", null, null), true);
        }

        // ========================================================================
        // KISIM 6: ÝSYANCI GÜÇLENDÝRMESÝ (THE BIG BANG)
        // ========================================================================
        private void BoostRebelCapabilities(Kingdom rebelKingdom)
        {
            if (rebelKingdom == null) return;

            foreach (var settlement in rebelKingdom.Settlements)
            {
                if (settlement.IsTown || settlement.IsCastle)
                {
                    // 1. ERZAK DEPOLARINI FULLE
                    if (settlement.Town != null)
                    {
                        settlement.Town.FoodStocks = settlement.Town.FoodStocksUpperLimit();
                        settlement.Town.Loyalty = 100f; // Sadakat sorunu olmasýn
                        settlement.Town.Security = 100f;
                    }

                    // 2. GARNÝZONA ELÝT ASKER IÞINLA
                    if (settlement.Town.GarrisonParty != null)
                    {
                        var culture = settlement.Culture;
                        // Kültürün en iyi askerini bul
                        CharacterObject eliteTroop = culture.EliteBasicTroop ?? culture.BasicTroop;

                        if (eliteTroop != null)
                        {
                            // Garnizon limitine kadar doldur
                            int limit = settlement.Town.GarrisonParty.Party.PartySizeLimit;
                            int current = settlement.Town.GarrisonParty.MemberRoster.TotalManCount;
                            int needed = limit - current;

                            // En az 100 tane ekle, yer varsa fulle
                            int amountToAdd = Math.Min(needed, 150);

                            if (amountToAdd > 0)
                            {
                                settlement.Town.GarrisonParty.MemberRoster.AddToCounts(eliteTroop, amountToAdd);
                            }
                        }

                        // Garnizonun morali tavan yapsýn
                        settlement.Town.GarrisonParty.RecentEventsMorale += 100;
                    }
                }
            }

            // 3. KRALLIK KASASINA PARA EKLE
            // Lordlar asker toplayabilsin diye
            foreach (var clan in rebelKingdom.Clans)
            {
                if (clan.Leader != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, clan.Leader, 500000, true);
                }
            }

            TextObject msg = new TextObject("{=str_turmoil_rebel_boost}Rebel Strongholds have been fully supplied and reinforced!");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
        }

        // ========================================================================
        // KISIM 7: SADIKLARIN TEPKÝSÝ (KARÞI ÝTTÝFAK VE SEFERBERLÝK)
        // ========================================================================
        // --- DÜZELTME 3: Ýttifak Bulunamama ve Crash Hatasý ---
        // DÜZELTME: Lider kontrolü ve Null Check eklendi (Crash engellemek için)
        private void TriggerLoyalistResponse(Kingdom loyalistKingdom, Kingdom rebelKingdom, bool isPlayerBacked)
        {
            // Temel Güvenlik Kontrolleri
            if (loyalistKingdom == null || rebelKingdom == null) return;
            if (loyalistKingdom.Leader == null) return;

            Kingdom ally = null;
            int bestRelation = -100;

            // 1. En iyi dostu ara
            foreach (var k in Kingdom.All)
            {
                // Kendisi, isyancýlar veya lideri olmayan krallýklar hariç
                if (k == loyalistKingdom || k == rebelKingdom || k.IsEliminated || k.Leader == null) continue;

                // Zaten düþmansa müttefik olamaz
                if (FactionManager.IsAtWarAgainstFaction(loyalistKingdom, k)) continue;

                // --- KRÝTÝK DÜZELTME: OYUNCU FÝLTRESÝ ---
                // Eðer isyaný oyuncu çýkardýysa (isPlayerBacked), kurban krallýk gidip
                // oyuncunun krallýðýndan yardým isteyemez!
                if (isPlayerBacked && k == Clan.PlayerClan.Kingdom) continue;
                // ----------------------------------------

                int relation = (int)k.Leader.GetRelation(loyalistKingdom.Leader);

                // Dost bulma eþiði (Ýliþkisi pozitif olanlarý tercih et)
                if (relation > 0 && relation > bestRelation)
                {
                    bestRelation = relation;
                    ally = k;
                }
            }

            // 2. Dost yoksa, RASTGELE bir krallýðý zorla (Çökmemesi için fallback)
            if (ally == null)
            {
                var potentialAllies = Kingdom.All.Where(k =>
                    k != loyalistKingdom &&
                    k != rebelKingdom &&
                    !k.IsEliminated &&
                    k.Leader != null &&
                    !FactionManager.IsAtWarAgainstFaction(loyalistKingdom, k) &&
                    // Burada da oyuncu filtresini ekliyoruz
                    (!isPlayerBacked || k != Clan.PlayerClan.Kingdom)
                    ).ToList();

                if (potentialAllies.Count > 0)
                {
                    ally = potentialAllies.GetRandomElement();
                }
            }

            if (ally != null)
            {
                // --- SAVAÞ ÝLANI DÜZELTMESÝ ---
                // Müttefik isyancýlara KESÝN OLARAK savaþ açmalý.
                // Önce iliþkiyi düþürüyoruz ki oyun motoru savaþý mantýklý bulsun ve hemen barýþ yapmasýn.
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(ally.Leader, rebelKingdom.Leader, -40);

                if (!FactionManager.IsAtWarAgainstFaction(ally, rebelKingdom))
                {
                    DeclareWarAction.ApplyByDefault(ally, rebelKingdom);
                }

                // Eðer oyuncu isyancýlara katýldýysa (RebelKingdom oyuncu ise), müttefik oyuncuya da savaþ açmalý
                if (Clan.PlayerClan.Kingdom == rebelKingdom && !FactionManager.IsAtWarAgainstFaction(ally, Clan.PlayerClan.Kingdom))
                {
                    DeclareWarAction.ApplyByDefault(ally, Clan.PlayerClan.Kingdom);
                }

                // Reflection ile Resmi Ýttifak (Alliance) Kur
                try
                {
                    var campaignAssembly = typeof(Campaign).Assembly;
                    Type allianceInterface = campaignAssembly.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.IAllianceCampaignBehavior");
                    if (allianceInterface != null)
                    {
                        MethodInfo getBehavior = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(allianceInterface);
                        object behavior = getBehavior.Invoke(Campaign.Current, null);
                        allianceInterface.GetMethod("StartAlliance")?.Invoke(behavior, new object[] { loyalistKingdom, ally });
                    }
                }
                catch { /* Hata olursa yut, oyun çökmesin */ }

                TextObject msg = new TextObject("{=str_turmoil_loyalist_alliance}CRITICAL: {LOYALIST} has formed an ALLIANCE with {ALLY} against the rebels!");
                msg.SetTextVariable("LOYALIST", loyalistKingdom.Name);
                msg.SetTextVariable("ALLY", ally.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
            }
            else
            {
                TextObject msg = new TextObject("{=str_turmoil_loyalist_alone}{KINGDOM} implies for help, but stands alone!");
                msg.SetTextVariable("KINGDOM", loyalistKingdom.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
            }
        }

        // DÜZELTME: Null Notable (Eþraf) hatasý giderildi.

        // ========================================================================
        // KISIM 8: OYUN SONU (ENDGAME) VE ÝLTÝCA SÝSTEMÝ
        // ========================================================================
        private void CheckEndgameConditions()
        {
            foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated).ToList())
            {
                // SIFIR TOPRAK KURALI
                if (kingdom.Settlements.Count == 0 && kingdom.Clans.Count > 0)
                {
                    bool isRebelFaction = kingdom.StringId.Contains("rebel_");

                    if (isRebelFaction)
                    {
                        // ÝSYANCILAR KAYBETTÝ (Bizim desteklediklerimiz)
                        ProcessRebelDefeat(kingdom);
                    }
                    else
                    {
                        // SADIKLAR KAYBETTÝ
                        // Düþmaný bul
                        var enemyRebel = Kingdom.All.FirstOrDefault(k => FactionManager.IsAtWarAgainstFaction(k, kingdom) && k.StringId.Contains("rebel_"));

                        if (enemyRebel != null)
                        {
                            ProcessLoyalistDefeat(kingdom, enemyRebel);
                        }
                    }
                }
            }
        }

        // SENARYO A: SADIKLAR KAYBETTÝ -> ÝLTÝCA
        private void ProcessLoyalistDefeat(Kingdom losers, Kingdom winners)
        {
            // Sýðýnacak müttefik bul
            Kingdom asylum = Kingdom.All.FirstOrDefault(k =>
                k != losers &&
                k != winners &&
                !k.IsEliminated &&
                !FactionManager.IsAtWarAgainstFaction(k, losers));

            if (asylum != null)
            {
                TextObject msg = new TextObject("{=str_turmoil_gov_fall}GOVERNMENT COLLAPSE: {LOSER} has fallen! The King and loyal lords have defected to {ASYLUM}!");
                msg.SetTextVariable("LOSER", losers.Name);
                msg.SetTextVariable("ASYLUM", asylum.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));

                foreach (var clan in losers.Clans.ToList())
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, asylum);
                }

                DestroyKingdomAction.Apply(losers);
            }
            else
            {
                TextObject msg = new TextObject("{=str_turmoil_kingdom_destroyed}{KINGDOM} has been completely destroyed. The lords have gone into exile.");
                msg.SetTextVariable("KINGDOM", losers.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Gray));
                DestroyKingdomAction.Apply(losers);
            }
        }

        // SENARYO B: ÝSYANCILAR KAYBETTÝ -> OYUNCUYA SIÐINMA
        private void ProcessRebelDefeat(Kingdom rebels)
        {
            // Bu kýsým oyuncuya sorulduðu için sadece 1 kere çalýþmalý.

            TextObject title = new TextObject("{=str_turmoil_rebellion_crushed_title}Rebellion Crushed!");
            TextObject text = new TextObject("{=str_turmoil_rebellion_crushed_text}The rebels' last stronghold has fallen. Their leader is at our gates demanding asylum.{newline}{newline}ACCEPT: They join us, but the world will turn against us.{newline}REFUSE: They will be executed.");
            TextObject btnAccept = new TextObject("{=str_turmoil_btn_asylum}Accept (Asylum)");
            TextObject btnRefuse = new TextObject("{=str_turmoil_btn_execution}Refuse (Leave to Die)");

            InformationManager.ShowInquiry(new InquiryData(
                title.ToString(),
                text.ToString(),
                true, true,
                btnAccept.ToString(), btnRefuse.ToString(),
                () =>
                {
                    // KABUL
                    foreach (var clan in rebels.Clans.ToList())
                    {
                        ChangeKingdomAction.ApplyByJoinToKingdom(clan, Clan.PlayerClan.Kingdom);
                    }

                    TextObject msg = new TextObject("{=str_turmoil_asylum_war}The world has turned against us for harboring traitors! War is inevitable!");
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));

                    // Ýsyancýlarýn düþmanlarý bize savaþ açar
                    foreach (var enemy in Kingdom.All.Where(k => FactionManager.IsAtWarAgainstFaction(k, rebels)))
                    {
                        if (!FactionManager.IsAtWarAgainstFaction(enemy, Clan.PlayerClan.Kingdom))
                            DeclareWarAction.ApplyByDefault(enemy, Clan.PlayerClan.Kingdom);
                    }
                    DestroyKingdomAction.Apply(rebels);
                },
                () =>
                {
                    // RED
                    TextObject msg = new TextObject("{=str_turmoil_execution_msg}Rebel leaders were captured and executed by the King. Their clans have been wiped from history.");
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Gray));

                    foreach (var clan in rebels.Clans.ToList())
                    {
                        if (clan.Leader != null) KillCharacterAction.ApplyByExecution(clan.Leader, rebels.Leader);
                        DestroyClanAction.Apply(clan);
                    }
                    DestroyKingdomAction.Apply(rebels);
                }
            ), true);
        }

        // ========================================================================
        // KISIM 9: YARDIMCI VE DEBUG METOTLARI (EKSÝKSÝZ)
        // ========================================================================

        private void TryStartRebellion(Kingdom kingdom, List<Clan> pool, bool isForcedDebug)
        {
            var safePool = pool.Where(c => c != kingdom.RulingClan && c != Clan.PlayerClan).ToList();
            Clan leaderClan = safePool.OrderBy(c => c.GetRelationWithClan(kingdom.RulingClan)).FirstOrDefault();

            if (isForcedDebug && leaderClan == null && kingdom.Clans.Count >= 3)
            {
                var randoms = kingdom.Clans.Where(c => c != kingdom.RulingClan && c != Clan.PlayerClan).Take(2).ToList();
                if (randoms.Count > 0) leaderClan = randoms[0];
            }

            if (leaderClan == null) return;

            int packSize = DetermineRebelPackSize();
            IEnumerable<Clan> sourceList;
            if (isForcedDebug && safePool.Count < packSize)
                sourceList = kingdom.Clans.Where(c => c != kingdom.RulingClan && c != Clan.PlayerClan);
            else
                sourceList = safePool;

            var allies = sourceList.Where(c => c != leaderClan && c != kingdom.RulingClan && c != Clan.PlayerClan)
                                   .OrderByDescending(c => c.GetRelationWithClan(leaderClan))
                                   .Take(packSize - 1)
                                   .ToList();

            if (allies.Count < 1) return;

            List<Clan> rebelGroup = new List<Clan> { leaderClan };
            rebelGroup.AddRange(allies);

            if (kingdom.RulingClan == Clan.PlayerClan)
            {
                TriggerPlayerUltimatum(kingdom, rebelGroup);
            }
            else
            {
                ExecuteRebellion(kingdom, rebelGroup, false);
            }
        }

        // --- DÜZELTME 1: "The Lord Who Is Not a King" Hatasý Giderildi ---
        // --- GÜNCELLENMÝÞ MANTIK: SADECE VASSALLAR ÝSYAN EDEBÝLÝR ---
        // ========================================================================
        // SADECE KRALIN BAÞLATABÝLECEÐÝ ÝSYAN MANTIÐI
        // ========================================================================
        // ========================================================================
        // KIÞKIRTMA MANTIÐI (OYUNCU PARAYI VERÝR, YABANCI LORD ÝSYAN EDER)
        // ========================================================================
        public bool ForcePlayerIntervention(Hero leaderHero, int targetAllyCount)
        {
            // 1. Güvenlik
            if (leaderHero == null || leaderHero.Clan == null || leaderHero.Clan.Kingdom == null) return false;
            Kingdom kingdom = leaderHero.Clan.Kingdom;

            // 2. ESKÝ "JUST THE KING" KONTROLÜNÜ KALDIRDIK.
            // Çünkü artýk sen (Kral olmayan) bir Lordu (Kral olmayan) kýþkýrtýyorsun.
            // Ýsyan eden kiþi (leaderHero) zaten RulingClan OLMAMALI.

            if (leaderHero.Clan == kingdom.RulingClan)
            {
                // Eðer yanlýþlýkla Kralý kýþkýrtmaya çalýþýrsan (Diyalogda engelledik ama olsun)
                return false;
            }

            // 3. Müttefik Bulma (Kendi Krallýðýnýn içinden)
            List<Clan> potentialAllies = new List<Clan>();
            foreach (var clan in kingdom.Clans)
            {
                // Kral hariç, isyaný baþlatan (leaderHero) hariç, oyuncu hariç
                if (clan != kingdom.RulingClan && clan != leaderHero.Clan && !clan.IsEliminated && clan != Clan.PlayerClan)
                {
                    potentialAllies.Add(clan);
                }
            }

            // Ýliþkiye göre en iyileri seç (Veya rastgele)
            var selectedAllies = potentialAllies.OrderByDescending(c => c.GetRelationWithClan(leaderHero.Clan))
                                                .Take(targetAllyCount - 1) // -1 çünkü liderin kendisi de var
                                                .ToList();

            // Yeterli adam yoksa bile eldekilerle baþlat (Parayý aldýk bir kere)
            if (selectedAllies.Count == 0 && targetAllyCount > 1)
            {
                // Tek baþýna isyan edecek
                TextObject msg = new TextObject("{=str_turmoil_rebel_alone}{LEADER} couldn't find allies, but will rebel alone!");
                msg.SetTextVariable("LEADER", leaderHero.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
            }

            // 4. Ýsyan Grubunu Kur
            List<Clan> rebelGroup = new List<Clan> { leaderHero.Clan };
            rebelGroup.AddRange(selectedAllies);

            // 5. Baþlat (isPlayerBacked = true, yani arkasýnda sen varsýn)
            ExecuteRebellion(kingdom, rebelGroup, true);
            return true;
        }

        // --- DÜZELTME 2: Maliyet Hesaplama (100 Milyon Hatasý Giderildi) ---
        // Fiyat farkýnýn net olmasý için katsayýlarý artýrdým
        // --- GÜNCELLENMÝÞ FÝYAT HESAPLAMA (KESÝN FARK OLUÞTURUR) ---
        public long CalculatePlayerBribeCost(int targetAllyCount)
        {
            // Basit ve etkili formül:
            // 3 Klan = 3 Milyon
            // 4 Klan = 5 Milyon (Katlanarak artar)
            // 5 Klan = 8 Milyon

            long basePrice = 15340406; // Taban fiyat
            long multiplier = targetAllyCount * targetAllyCount; // Karesi ile artar (Exponential)

            return basePrice + (multiplier * 216523);
        }

        // --- YENÝ NÜFUZ MALÝYETÝ ---
        public float CalculatePlayerInfluenceCost(int targetAllyCount)
        {
            // 3 Klan = 300 Nüfuz
            // 5 Klan = 1000 Nüfuz
            return targetAllyCount * 150f + (targetAllyCount > 3 ? 200f : 0f);
        }
        // --- SÜPER GÜÇLENDÝRME (BUFF) ---
        // Ýsyancý krallýk kurulduðu an çaðrýlýr.

        private void TriggerPlayerUltimatum(Kingdom kingdom, List<Clan> rebels)
        {
            int totalCost = CalculateHushMoney(rebels);
            string rebelNames = string.Join(", ", rebels.Select(c => c.Name));

            TextObject title = new TextObject("{=str_turmoil_rebellion_title}REBELLION THREAT!");
            TextObject text = new TextObject("{=str_turmoil_rebellion_ultimatum}My Lord! {REBEL_NAMES} ({COUNT} Clans) are threatening to secede.{newline}{newline}Their Demand: {COST} Denars.{newline}What are your orders?");
            text.SetTextVariable("REBEL_NAMES", rebelNames);
            text.SetTextVariable("COUNT", rebels.Count);
            text.SetTextVariable("COST", totalCost.ToString("N0"));

            TextObject btnPay = new TextObject("{=str_turmoil_btn_pay}Pay ({COST})");
            btnPay.SetTextVariable("COST", totalCost.ToString("N0"));

            TextObject btnWar = new TextObject("{=str_turmoil_btn_war}Refuse (War)");

            InformationManager.ShowInquiry(new InquiryData(
                title.ToString(),
                text.ToString(),
                true, true,
                btnPay.ToString(), btnWar.ToString(),
                () =>
                {
                    if (Hero.MainHero.Gold >= totalCost)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, totalCost, true);
                        ApplyPeaceTreaty(rebels);
                        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=str_turmoil_loyalty_secured}Gold distributed. Loyalty secured.").ToString(), Colors.Green));
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=str_turmoil_insufficient_funds}Insufficient funds! War is inevitable.").ToString(), Colors.Red));
                        ExecuteRebellion(kingdom, rebels, false);
                    }
                },
                () => ExecuteRebellion(kingdom, rebels, false)
            ), true);
        }

        private int CalculateHushMoney(List<Clan> clans)
        {
            return clans.Sum(c => (c.Tier * 50000) + (c.Fiefs.Count * 100000));
        }



        private void ForceHardRebellion()
        {
            Kingdom target = Kingdom.All.OrderByDescending(k => k.Clans.Sum(c => GetStress(c))).FirstOrDefault();
            if (target != null)
            {
                var validRebels = target.Clans.Where(c => c != target.RulingClan && c != Clan.PlayerClan).ToList();
                if (validRebels.Count > 0)
                {
                    TryStartRebellion(target, validRebels, true);
                }
            }
            _lastGlobalRebellionTime = CampaignTime.Now;
        }

        // TOOLKIT HELPER METOTLAR
        public void DebugTriggerPlayerRebellion()
        {
            Kingdom myKingdom = Clan.PlayerClan.Kingdom;
            // GÜNCELLENDÝ: Hata mesajý
            if (myKingdom == null) { InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=str_turmoil_not_in_kingdom}You are not in a kingdom.").ToString(), Colors.Red)); return; }
            var clans = myKingdom.Clans.Where(c => c != Clan.PlayerClan && c != myKingdom.RulingClan).ToList();
            TryStartRebellion(myKingdom, clans, true);
        }

        public void DebugTriggerRandomRebellion()
        {
            var targets = Kingdom.All.Where(k => k != Clan.PlayerClan.Kingdom && !k.IsEliminated && k.Clans.Count >= 4).ToList();
            if (targets.Count == 0) return;
            Kingdom victim = targets[MBRandom.RandomInt(targets.Count)];
            var validRebels = victim.Clans.Where(c => c != victim.RulingClan && c != Clan.PlayerClan).ToList();
            if (validRebels.Count > 0) TryStartRebellion(victim, validRebels, true);
        }

        public void ShowUnrestReport()
        {
            string report = "";
            int count = 0;
            foreach (var kvp in _clanUnrest)
            {
                if (kvp.Value > 100)
                {
                    var c = Clan.All.FirstOrDefault(x => x.StringId == kvp.Key);
                    if (c != null) { report += $"{c.Name}: {kvp.Value}\n"; count++; }
                }
            }

            // GÜNCELLENDÝ: Rapor Popup
            if (count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData(new TextObject("{=str_turmoil_intel_title}Intelligence Report").ToString(), new TextObject("{=str_turmoil_intel_none}No clans at risk.").ToString(), true, false, "Close", "", null, null), true);
            }
            else
            {
                TextObject title = new TextObject("{=str_turmoil_intel_title}Intelligence Report");
                TextObject content = new TextObject("{=str_turmoil_intel_found}Total {COUNT} Risky Clans:{newline}{newline}{REPORT}");
                content.SetTextVariable("COUNT", count);
                content.SetTextVariable("REPORT", report);
                InformationManager.ShowInquiry(new InquiryData(title.ToString(), content.ToString(), true, false, "Close", "", null, null), true);
            }
        }

        public int GetRebelPoolCount(Clan leaderClan)
        {
            if (leaderClan.Kingdom == null) return 0;
            return leaderClan.Kingdom.Clans.Count(c => c != leaderClan.Kingdom.RulingClan && c != Clan.PlayerClan);
        }

        private void ApplyPeaceTreaty(List<Clan> clans)
        {
            CampaignTime expire = CampaignTime.YearsFromNow(5f);
            foreach (var c in clans)
            {
                _clanUnrest[c.StringId] = 0;
                if (!_peaceTreaties.ContainsKey(c.StringId)) _peaceTreaties.Add(c.StringId, expire);
                else _peaceTreaties[c.StringId] = expire;
            }
        }

        private void CleanUpClanState(Clan clan)
        {
            foreach (var hero in clan.Heroes)
            {
                if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.Army != null)
                {
                    if (hero.PartyBelongedTo.Army.LeaderParty == hero.PartyBelongedTo)
                        DisbandArmyAction.ApplyByUnknownReason(hero.PartyBelongedTo.Army);
                    else
                        hero.PartyBelongedTo.Army = null;
                }
            }
        }

        public void AddStress(Clan clan, int amount) { if (!_clanUnrest.ContainsKey(clan.StringId)) _clanUnrest[clan.StringId] = 0; _clanUnrest[clan.StringId] += amount; }
        public void IncreaseUnrest(Kingdom kingdom) { if (kingdom != null) foreach (var c in kingdom.Clans) if (c != kingdom.RulingClan && !c.IsEliminated) AddStress(c, 20); }
        public int GetStress(Clan clan) => _clanUnrest.ContainsKey(clan.StringId) ? _clanUnrest[clan.StringId] : 0;

        private int DetermineRebelPackSize()
        {
            int r = MBRandom.RandomInt(1, 101);
            if (r <= 50) return 2;
            if (r <= 75) return 3;
            if (r <= 95) return 4;
            return 5;
        }

        // ========================================================================
        // HARMONY YAMASI: SAVAÞ KÝLÝDÝ (CRASH FIX VERSÝYONU)
        // ========================================================================
        [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.KingdomDecisionProposalBehavior), "ConsiderPeace")]
        public static class ForceWarPatch
        {
            public static bool Prefix(Clan clan, IFaction otherFaction, ref bool __result)
            {
                Kingdom kingdom = clan.Kingdom;
                if (kingdom == null || otherFaction == null) return true;

                Kingdom otherKingdom = otherFaction as Kingdom;

                bool isRebel1 = kingdom.StringId != null && kingdom.StringId.StartsWith("rebel_");
                bool isRebel2 = otherKingdom != null && otherKingdom.StringId != null && otherKingdom.StringId.StartsWith("rebel_");

                if (isRebel1 || isRebel2)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }


    }

}

