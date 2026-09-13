using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using SandBox.View;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ObjectSystem;

namespace RebellionsAndDemographics
{
    // ========================================================================
    // 1. CLASS: OPERASYONLAR (YÖNETİCİ & DİYALOG)
    // ========================================================================
    public static class PendraicScenarioOperations
    {
        public static (Army empireArmy, Army battaniaArmy) InitializePreparationPhase()
        {
            if (Campaign.Current == null) return (null, null);

            // 1. DİYALOGLARI KAYDET
            SetupPendraicDialogs();

            Kingdom empire = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
            Kingdom battania = Kingdom.All.FirstOrDefault(k => k.StringId == "battania");

            if (empire == null || battania == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: Kingdoms not found!", Colors.Red));
                return (null, null);
            }

            bool isBattaniaAligned = Hero.MainHero.Culture.StringId == "battania";
            Kingdom targetKingdom = isBattaniaAligned ? battania : empire;

            if (Clan.PlayerClan.Kingdom == null)
            {
                ChangeKingdomAction.ApplyByJoinFactionAsMercenary(Clan.PlayerClan, targetKingdom, awardMultiplier: 50, showNotification: true);
                InformationManager.DisplayMessage(new InformationMessage($"You have joined the ranks of {targetKingdom.Name} as a mercenary!", Colors.Green));
            }


            UnifyEmpire(empire);
            Hero neretzes = PrepareNeretzes(empire);
            PrepareArenicos(empire);

            foreach (var k in Kingdom.All) { if (k != null && k != empire && !k.IsEliminated) DeclareWar(empire, k); }

            Settlement empireTown = Settlement.Find("town_EN1");
            Settlement battaniaTown = Settlement.Find("town_B2");

            if (empireTown == null) empireTown = empire.Settlements.FirstOrDefault();
            if (battaniaTown == null) battaniaTown = battania.Settlements.FirstOrDefault();

            if (empireTown == null || battaniaTown == null) return (null, null);

            Vec2 empirePos = GetSafePosition(empireTown.GatePosition.ToVec2(), 10f);
            Vec2 alliancePos = GetSafePosition(battaniaTown.GatePosition.ToVec2(), 10f);

            Army empireArmy = CreateBalancedEmpireArmy(empire, neretzes, empirePos);

            List<Kingdom> blueKingdoms = new List<Kingdom> { battania };
            TriggerBrotherConversation();
            Army battaniaArmy = CreateGrandArmy(blueKingdoms, battania.Leader, alliancePos);


            return (empireArmy, battaniaArmy);
        }

        // --- DİYALOG SİSTEMİ ---
        private static void SetupPendraicDialogs()
        {
            // Priority 10000: Kesinlikle bu konuşma açılır.
            Campaign.Current.ConversationManager.AddDialogFlow(DialogFlow.CreateDialogFlow("start", 10000)
                .NpcLine(new TextObject("{=pendraic_die_1}Cough... cough... Blood... Blood everywhere...[if:convo_painful_voice][ib:suffering]"))
                .Condition(() => { return true; })
                .Consequence(() =>
                {
                    Hero.MainHero.AddSkillXp(DefaultSkills.Medicine, 50f);
                })
                .PlayerLine(new TextObject("{=pendraic_player_1}My Emperor! You must hold on! Call a healer!"))
                .NpcLine(new TextObject("{=pendraic_die_2}It's too late... He did this... Arenicos...[if:convo_angry_voice]"))
                .NpcLine(new TextObject("{=pendraic_die_3}He lured me into that trap. Made a deal with the barbarians... That treacherous snake... He wants my crown..."))
                .PlayerLine(new TextObject("{=pendraic_player_2}Arenicos? But he's your right hand!"))
                .NpcLine(new TextObject("{=pendraic_die_4}I should have seen the ambition in his eyes... Calradia will burn... Tell them... Tell them of his betrayal...[if:convo_dead_voice]"))
                .Consequence(() =>
                {
                    // --- ÖLÜM VE VERASET İŞLEMLERİ ---
                    Hero dyingNeretzes = Hero.OneToOneConversationHero;
                    Kingdom empire = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
                    Hero newLeaderArenicos = empire?.Heroes.FirstOrDefault(h => h.Name.ToString().Contains("Arenicos"));

                    if (dyingNeretzes != null && newLeaderArenicos != null && empire != null)
                    {
                        GainRenownAction.Apply(newLeaderArenicos, 5000f);
                        ChangeClanInfluenceAction.Apply(newLeaderArenicos.Clan, 10000f);
                        KillCharacterAction.ApplyByMurder(dyingNeretzes, null, true);

                        if (empire.RulingClan != null)
                        {
                            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(empire.RulingClan, newLeaderArenicos);
                        }

                        InformationManager.DisplayMessage(new InformationMessage("The Emperor is Dead! Arenicos Ascended the Throne!", Colors.Red));
                    }
                    else
                    {
                        if (dyingNeretzes != null) KillCharacterAction.ApplyByBattle(dyingNeretzes, null, true);
                    }

                    // DİKKAT: "Mission.Current.EndMission();" BURADAN SİLİNDİ.
                    // Kapanışı artık Logic tarafı 5 saniye bekleyip yapacak.
                })
                .CloseDialog());
        }
        public static bool IsOlekScenePending = false;
        public static void ExecuteGrandTeleportation(Army empireArmy, Army battaniaArmy, out MobileParty neretzesParty)
        {
            neretzesParty = null;
            if (empireArmy == null || battaniaArmy == null || empireArmy.LeaderParty == null || battaniaArmy.LeaderParty == null) return;

            InformationManager.DisplayMessage(new InformationMessage("!!! THE TIME HAS COME - PENDRAIC BATTLE!!!", Colors.Red));
            Vec2 battleCenter = GetSafePosition(new Vec2(264.76f, 517.78f), 1f);

            neretzesParty = empireArmy.LeaderParty;
            MobileParty caladogParty = battaniaArmy.LeaderParty;

            TeleportPartySafe(neretzesParty, battleCenter);
            TeleportPartySafe(caladogParty, battleCenter);

            if (MobileParty.MainParty != null)
            {
                TeleportPartySafe(MobileParty.MainParty, battleCenter);
                if (Clan.PlayerClan.Kingdom == empireArmy.Kingdom) MobileParty.MainParty.Army = empireArmy;
                else if (Clan.PlayerClan.Kingdom == battaniaArmy.Kingdom) MobileParty.MainParty.Army = battaniaArmy;
            }

            // DÜZELTME: NavigationType.Default kullanıldı.
            neretzesParty.SetMoveEngageParty(caladogParty, MobileParty.NavigationType.Default);
            caladogParty.SetMoveEngageParty(neretzesParty, MobileParty.NavigationType.Default);
        }
        // ========================================================================
        // 3. CLASS: ZAMANLAYICI (1. Saat Diyalog -> 49. Saat Savaş)
        // ========================================================================
        public class PendraicScenarioBehavior : CampaignBehaviorBase
        {
            private bool _scenarioStarted = false;
            private bool _battleTriggered = false;
            private CampaignTime _startTime;

            public override void RegisterEvents()
            {
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, (starter) =>
                {
                    if (!_scenarioStarted && _startTime == default) _startTime = CampaignTime.Now;
                });

                CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            }

            public override void SyncData(IDataStore dataStore)
            {
                dataStore.SyncData("_pendraicScenarioStarted", ref _scenarioStarted);
                dataStore.SyncData("_pendraicBattleTriggered", ref _battleTriggered);
                dataStore.SyncData("_pendraicStartTime", ref _startTime);
            }

            private void OnHourlyTick()
            {
                // Mid-game save kontrolü: Sadece 1077 yılında çalışsın
                if (CampaignTime.Now.GetYear != 1077)
                {
                    _scenarioStarted = true;
                    _battleTriggered = true;
                    return;
                }

                if (_battleTriggered) return;

                // Geçen süreyi hesapla
                double hoursElapsed = _startTime.ElapsedHoursUntilNow;

                // --- DEBUG EKLEMESİ BAŞLANGIÇ ---
                // Her saat başı ekrana bilgi yazdırıyoruz.
                // Eğer bu mesajı hiç görmüyorsan, Behavior hiç çalışmıyor demektir.
                // Eğer "Elapsed Time" çok garip sayılar çıkıyorsa, kayıt yükleme sorunu vardır.
                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Elapsed Time: {hoursElapsed:0.0} Hours | Has the Scenario Started: {_scenarioStarted}", Colors.Yellow));
                // --- DEBUG EKLEMESİ BİTİŞ ---

                // 1. SAAT: Hazırlık ve Kardeş Diyaloğu
                // 1. SAAT: Hazırlık ve Kardeş Diyaloğu
                if (!_scenarioStarted && hoursElapsed >= 1.0)
                {
                    InformationManager.DisplayMessage(new InformationMessage("!!! 1 HOUR IS UP - Map Talk Begins!!!", Colors.Green));

                    _scenarioStarted = true;
                    PendraicScenarioOperations.InitializePreparationPhase();

                    // --- HARİTADA KONUŞMA BAŞLATAN KOD BUDUR ---
                    // Kardeşi (veya yoldaşı) bul
                    Hero partner = Hero.MainHero.Siblings.FirstOrDefault(x => x.IsAlive) ?? Hero.MainHero.CompanionsInParty.FirstOrDefault();
                    
                    

                    if (partner != null)
                    {
                        // Harita üzerinde (Campaign Map) diyaloğu bu komut açar
                        CampaignMapConversation.OpenConversation(
                            new ConversationCharacterData(Hero.MainHero.CharacterObject, PartyBase.MainParty),
                            new ConversationCharacterData(partner.CharacterObject, PartyBase.MainParty)
                        );
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Partinde konuşacak kardeş/yoldaş yok!", Colors.Red));
                    }
                    // --------------------------------------------
                }

                // 49. SAAT: Savaş Başlangıcı
                if (_scenarioStarted && !_battleTriggered && hoursElapsed >= 49.0)
                {
                    InformationManager.DisplayMessage(new InformationMessage("!!! 49 HOURS ARE UP - The War Begins!!!", Colors.Red));
                    _battleTriggered = true;
                    PendraicScenarioOperations.StartGatheringAndBattle();
                }
            }
        }
        // PendraicScenarioOperations sınıfının içine, en alta ekle:
        public static void StartGatheringAndBattle()
        {
            try
            {
                // 1. GÜVENLİK: Mevcut bir karşılaşma varsa kapat
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Finish(true);
                }

                // 2. DÜŞMAN YARAT
                MobileParty enemyParty = MobileParty.CreateParty("pendraic_ghost_army", null);

                // DÜZELTME 1: SetCustomName yoksa PartyBase üzerinden isimlendirme yapılır.
                typeof(PartyBase).GetField("_customName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(enemyParty.Party, new TextObject("{=pendraic_enemy}Sturgia & Battania İttifakı"));

                // 3. ASKERLERİ HAZIRLA (TroopRoster oluşturuyoruz)
                // InitializeMobilePartyAtPosition metodu artık Clan değil, Roster istiyor.
                TroopRoster armyRoster = TroopRoster.CreateDummyTroopRoster();
                TroopRoster prisonerRoster = TroopRoster.CreateDummyTroopRoster(); // Boş esir listesi

                CultureObject battania = MBObjectManager.Instance.GetObject<CultureObject>("battania");
                CultureObject sturgia = MBObjectManager.Instance.GetObject<CultureObject>("sturgia");
                CultureObject empire = MBObjectManager.Instance.GetObject<CultureObject>("empire");

                // Askerleri Roster'a ekle
                CharacterObject archer = battania?.EliteBasicTroop ?? empire.EliteBasicTroop;
                if (archer != null) armyRoster.AddToCounts(archer, 150);

                CharacterObject infantry = sturgia?.BasicTroop ?? empire.BasicTroop;
                if (infantry != null) armyRoster.AddToCounts(infantry, 250);

                // DÜZELTME 2 ve 3: 
                // - Argüman 1: TroopRoster (Askerler)
                // - Argüman 2: TroopRoster (Esirler)
                // - Argüman 3: CampaignVec2 (Direkt Position veriyoruz, AsVec2 çevirisi yok)
                enemyParty.InitializeMobilePartyAtPosition(armyRoster, prisonerRoster, MobileParty.MainParty.Position);

                // Diğer ayarlar
                enemyParty.SetPartyUsedByQuest(true);
                Clan banditClan = Clan.All.FirstOrDefault(c => c.StringId == "looters") ?? Clan.All.First();
                enemyParty.ActualClan = banditClan;

                // Görünümü güncelle
                enemyParty.Party.SetVisualAsDirty();

                // 4. SAVAŞI BAŞLAT
                PlayerEncounter.Start();
                PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, enemyParty.Party);
                PlayerEncounter.StartBattle();

                InformationManager.DisplayMessage(new InformationMessage("!!! THE PENDRAIC BATTLE BEGINS!!!", Colors.Red));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("War Start Error:" + ex.Message, Colors.Red));
            }
        }

        public static void ForceRetreatAndDialog(Army empireArmy, MobileParty neretzesParty)
        {
            if (PlayerEncounter.Current != null) PlayerEncounter.Finish(true);

            Settlement epicrotea = Settlement.Find("town_EN1");
            if (epicrotea == null) return;
            Vec2 gatePos = epicrotea.GatePosition.ToVec2();

            if (empireArmy != null && empireArmy.LeaderParty != null) TeleportPartySafe(empireArmy.LeaderParty, gatePos);
            TeleportPartySafe(MobileParty.MainParty, gatePos);

            Kingdom battania = Kingdom.All.FirstOrDefault(k => k.StringId == "battania");
            if (battania != null && battania.Leader != null && battania.Leader.PartyBelongedTo != null)
                TeleportPartySafe(battania.Leader.PartyBelongedTo, new Vec2(gatePos.X + 10f, gatePos.Y));

            if (neretzesParty != null && neretzesParty.LeaderHero != null)
            {
                ConversationCharacterData playerData = new ConversationCharacterData(Hero.MainHero.CharacterObject, PartyBase.MainParty);
                ConversationCharacterData neretzesData = new ConversationCharacterData(neretzesParty.LeaderHero.CharacterObject, neretzesParty.Party);
                CampaignMapConversation.OpenConversation(playerData, neretzesData);
            }
        }

        public static void StartSecondBattleWithArenicos(ref Army empireArmy, MobileParty neretzesParty)
        {
            if (neretzesParty != null && neretzesParty.LeaderHero != null)
                KillCharacterAction.ApplyByBattle(neretzesParty.LeaderHero, null, true);

            Kingdom empire = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
            Hero arenicos = empire?.Heroes.FirstOrDefault(h => h.Name.ToString().Contains("Arenicos"));

            if (arenicos != null && empire != null)
            {
                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(empire.RulingClan, arenicos);
                if (arenicos.PartyBelongedTo != null)
                {
                    empireArmy = new Army(empire, arenicos.PartyBelongedTo, Army.ArmyTypes.Patrolling);
                    if (MobileParty.MainParty != null && Clan.PlayerClan.Kingdom == empire) MobileParty.MainParty.Army = empireArmy;
                }
            }
            InformationManager.DisplayMessage(new InformationMessage("ARENICOS TAKES COMMAND! LAST DEFENSE!", Colors.Red));
        }

        public static void EndScenarioAndLeaveKingdom()
        {
            if (Clan.PlayerClan.Kingdom != null)
            {
                ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(Clan.PlayerClan, showNotification: true);
                if (MobileParty.MainParty.Army != null) MobileParty.MainParty.Army = null;
                InformationManager.DisplayMessage(new InformationMessage("Scenario Completed. You are free.", Colors.Green));
            }
        }

        // --- SİNEMATİK SAHNE AÇMA ---
        public static void OpenPostBattleCinematic(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) sceneName = "village_empire_1";

            MissionState.OpenNew("PendraicCinematic",
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.All),
                (mission) => new MissionBehavior[]
                {
                    new MissionOptionsComponent(),
                    new CampaignMissionComponent(),
                    new MissionBasicTeamLogic(),
                    new MissionAgentLookHandler(),
                    new PendraicCutsceneLogic(),   // BİZİM LOGIC
                    new SandBox.Conversation.MissionLogics.MissionConversationLogic(),
                    new BasicLeaveMissionLogic(true),

                    // GÖRÜNTÜ OLUŞTURUCULAR (Sadece gerekli olanlar)
                    SandBoxViewCreator.CreateMissionConversationView(mission),
                    ViewCreator.CreateMissionLeaveView(),
                    ViewCreator.CreateMissionSingleplayerEscapeMenu(false)
                },
                true,
                true);
        }

        // YARDIMCI METOTLAR
        private static Army CreateBalancedEmpireArmy(Kingdom empire, Hero leader, Vec2 pos)
        {
            if (leader == null || leader.PartyBelongedTo == null)
            {
                if (leader != null)
                {
                    MobileParty p = LordPartyComponent.CreateLordParty("neretzes_party", leader, new CampaignVec2(pos, true), 1f, null, leader);
                    p.MemberRoster.AddToCounts(leader.Culture.EliteBasicTroop, 300);
                }
                else return null;
            }
            TeleportPartySafe(leader.PartyBelongedTo, pos);
            Army army = new Army(empire, leader.PartyBelongedTo, Army.ArmyTypes.Patrolling);
            Hero arenicos = empire.Heroes.FirstOrDefault(h => h.Name.ToString().Contains("Arenicos"));
            foreach (var clan in empire.Clans)
            {
                if (clan == leader.Clan || clan.IsEliminated) continue;
                foreach (var party in clan.WarPartyComponents)
                {
                    MobileParty mp = party.MobileParty;
                    if (mp == null || !mp.IsActive || mp.Army != null) continue;
                    bool invite = (mp.LeaderHero == arenicos) || (MBRandom.RandomFloat < 0.33f);
                    if (invite) { TeleportPartySafe(mp, pos); mp.Army = army; }
                }
            }
            return army;
        }

        private static Army CreateGrandArmy(List<Kingdom> kingdoms, Hero leader, Vec2 pos)
        {
            if (leader == null || leader.PartyBelongedTo == null) return null;
            TeleportPartySafe(leader.PartyBelongedTo, pos);
            Army army = new Army(leader.Clan.Kingdom, leader.PartyBelongedTo, Army.ArmyTypes.Patrolling);
            foreach (var kingdom in kingdoms)
            {
                if (kingdom == null) continue;
                foreach (var clan in kingdom.Clans)
                {
                    if (clan.IsEliminated) continue;
                    foreach (var warParty in clan.WarPartyComponents)
                    {
                        MobileParty mp = warParty.MobileParty;
                        if (mp != null && mp.IsActive && mp.Army == null && mp != MobileParty.MainParty && mp.LeaderHero != leader) { TeleportPartySafe(mp, pos); mp.Army = army; }
                    }
                }
            }
            return army;
        }

        public static void TriggerBrotherConversation()
        {
            Hero partner = Hero.MainHero.Siblings.FirstOrDefault(x => x.IsAlive) ?? Hero.MainHero.CompanionsInParty.FirstOrDefault();
            if (partner != null) CampaignMapConversation.OpenConversation(new ConversationCharacterData(Hero.MainHero.CharacterObject, PartyBase.MainParty), new ConversationCharacterData(partner.CharacterObject, PartyBase.MainParty));
        }
        private static void UnifyEmpire(Kingdom k)
        {
            if (k == null) return;
            k.ChangeKingdomName(new TextObject("{=!}Calradic Empire"), new TextObject("{=!}Calradic Empire"), new TaleWorlds.Localization.TextObject("{=!}"));
            foreach (Clan c in Clan.All.ToList()) if (c.Culture.StringId == "empire" && c.MapFaction != k && c.Kingdom != null) ChangeKingdomAction.ApplyByJoinToKingdom(c, k);
        }
        private static Hero PrepareNeretzes(Kingdom empire)
        {
            if (empire == null || empire.RulingClan == null) return null;
            Hero h = empire.RulingClan.Leader;
            if (h != null) { h.SetName(new TextObject("{=!}Drosios Neretzes"), new TextObject("{=!}Drosios Neretzes")); AddDragonBannerItems(h); }
            return h;
        }
        private static void PrepareArenicos(Kingdom empire)
        {
            if (empire == null) return;
            Hero h = empire.Heroes.FirstOrDefault(x => x.Name.ToString().Contains("Arenicos"));
            if (h == null && empire.Leader != null) { h = HeroCreator.CreateSpecialHero(empire.Leader.CharacterObject, empire.Settlements.FirstOrDefault(), empire.RulingClan, null, 40); h.SetName(new TextObject("{=!}Arenicos"), new TextObject("{=!}Arenicos")); }
            if (h != null) GainRenownAction.Apply(h, 20000f);
        }
        private static void AddDragonBannerItems(Hero hero)
        {
            try { string[] ids = { "dragon_banner", "dragon_banner_center", "dragon_banner_dragonhead", "dragon_banner_handle" }; foreach (string id in ids) { ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id); if (item != null) GiveItemAction.ApplyForHeroes(null, hero, new ItemRosterElement(item, 1)); } } catch { }
        }
        private static Vec2 GetSafePosition(Vec2 target, float radius)
        {
            if (Campaign.Current == null) return target;
            CampaignVec2 t = new CampaignVec2(target, true);
            return Campaign.Current.MapSceneWrapper.GetAccessiblePointNearPosition(t, radius).ToVec2();
        }
        private static void TeleportPartySafe(MobileParty party, Vec2 target)
        {
            if (party == null) return;
            if (party.Army != null && party.Army.LeaderParty != party) party.Army = null;
            if (party.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(party);
            party.Position = new CampaignVec2(target, true);
            party.SetMoveModeHold();
        }
        private static void DeclareWar(IFaction f1, IFaction f2) { if (f1 != null && f2 != null && !f1.IsAtWarWith(f2)) DeclareWarAction.ApplyByDefault(f1, f2); }
    }

    // ========================================================================
    // 2. CLASS: PENDRAIC CUTSCENE LOGIC (GÜVENLİ SPAWN & KONUŞMA)
    // ========================================================================
    // ========================================================================
    // 2. CLASS: PENDRAIC CUTSCENE LOGIC (GÜVENLİ SPAWN & KONUŞMA - DÜZELTİLMİŞ)
    // ========================================================================
    public class PendraicCutsceneLogic : MissionLogic
    {
        private bool _conversationStarted = false;
        private bool _conversationEnded = false;
        private float _endMissionTimer = 0f;

        // DÜZELTME: Karakterleri Tick'te (zamanla) değil, Başlangıçta (AfterStart) oluşturuyoruz.
        // Bu, oyunun "Where is the Player?" diye beklemesini ve donmasını (Deadlock) engeller.
        public override void AfterStart()
        {
            base.AfterStart();
            SpawnCharactersSafe();
        }

        public override void OnMissionTick(float dt)
        {
            // 1. Konuşma Başladı mı?
            if (!_conversationStarted && Mission.Current.Mode == MissionMode.Conversation)
            {
                _conversationStarted = true;
            }

            // 2. Konuşma Bitti mi? (Başlamıştı ama artık conversation modunda değiliz)
            if (_conversationStarted && !_conversationEnded && Mission.Current.Mode != MissionMode.Conversation)
            {
                _conversationEnded = true;
                InformationManager.DisplayMessage(new InformationMessage("A moment of silence...", Colors.Gray));
            }

            // 3. Konuşma bittikten sonra 5 saniye bekle ve kapat
            if (_conversationEnded)
            {
                _endMissionTimer += dt;
                if (_endMissionTimer > 5.0f)
                {
                    // Bir sonraki aşamaya geçiş sinyali
                    PendraicScenarioOperations.IsOlekScenePending = true;
                    InformationManager.DisplayMessage(new InformationMessage("Eyes darken... But the story is not over...", Colors.Red));
                    Mission.Current.EndMission();
                }
            }
        }

        private void SpawnCharactersSafe()
        {
            Mission.Scene.SetRainDensity(0.0f); // Yağmur yok, net görüntü

            // Takımları oluştur (Eğer yoksa)
            if (Mission.Teams.Count == 0)
            {
                Mission.Teams.Add(BattleSideEnum.Defender, 0xffffffff, 0xffffffff, null, true, false, true);
                Mission.Teams.Add(BattleSideEnum.Attacker, 0xffffffff, 0xffffffff, null, true, false, true);
                Mission.PlayerTeam = Mission.Teams.Attacker;
            }

            // --- MERKEZİ BUL ---
            Vec3 min, max;
            Mission.Scene.GetBoundingBox(out min, out max);
            Vec2 center2D = new Vec2((min.x + max.x) / 2f, (min.y + max.y) / 2f);

            // Fallback: Eğer sahne bounds hatalıysa güvenli bir nokta seç
            if (center2D.IsNonZero() == false) center2D = new Vec2(100, 100);

            float zPos = Mission.Scene.GetGroundHeightAtPosition(new Vec3(center2D.x, center2D.y, 100f));
            Vec3 centerPos = new Vec3(center2D.x, center2D.y, zPos);

            // --- 1. OYUNCU (SIRADAN ASKER KONUMU) ---
            MatrixFrame playerFrame = MatrixFrame.Identity;
            playerFrame.origin = centerPos + new Vec3(5f, -8f, 0);
            playerFrame.origin.z = Mission.Scene.GetGroundHeightAtPosition(playerFrame.origin);

            // Yüzü olaya (Neretzes'e) baksın
            Vec3 playerLookDir = (centerPos - playerFrame.origin).NormalizedCopy();
            playerFrame.rotation.f = playerLookDir;
            playerFrame.rotation.s = Vec3.CrossProduct(new Vec3(0, 0, 1), playerLookDir);
            playerFrame.rotation.u = new Vec3(0, 0, 1);

            Agent playerAgent = SpawnHero(Hero.MainHero, playerFrame, true);
            if (playerAgent != null)
            {
                playerAgent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, EquipmentIndex.WeaponItemBeginSlot, true, true, 0);
                Mission.MainAgent = playerAgent;
            }

            // --- 2. NERETZES (YERDE YATAN İMPARATOR) ---
            Agent neretzesAgent = null;
            Hero neretzes = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Neretzes"));

            if (neretzes != null)
            {
                MatrixFrame nFrame = MatrixFrame.Identity;
                nFrame.origin = centerPos; // Tam merkezde
                nFrame.origin.z = Mission.Scene.GetGroundHeightAtPosition(nFrame.origin);

                nFrame.rotation.f = new Vec3(0, -1, 0);
                nFrame.rotation.s = new Vec3(1, 0, 0);
                nFrame.rotation.u = new Vec3(0, 0, 1);

                neretzesAgent = SpawnHero(neretzes, nFrame, false);
                if (neretzesAgent != null)
                {
                    neretzesAgent.Health = 1; // Ölmek üzere
                    try
                    {
                        ActionIndexCache woundedAnim = ActionIndexCache.Create("act_fallen_guarded_1");
                        neretzesAgent.SetActionChannel(0, woundedAnim, true, 0, 0, 1f);
                        neretzesAgent.SetActionChannel(1, woundedAnim, true, 0, 0, 1f);
                    }
                    catch { }
                }
            }

            // --- 3. ARENICOS (BAŞUCUNDA) ---
            Hero arenicos = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Arenicos"));
            if (arenicos != null)
            {
                MatrixFrame aFrame = MatrixFrame.Identity;
                aFrame.origin = centerPos + new Vec3(1.5f, 0.5f, 0);
                aFrame.origin.z = Mission.Scene.GetGroundHeightAtPosition(aFrame.origin);

                Vec3 lookAtNeretzes = (centerPos - aFrame.origin).NormalizedCopy();
                aFrame.rotation.f = lookAtNeretzes;
                aFrame.rotation.s = Vec3.CrossProduct(new Vec3(0, 0, 1), lookAtNeretzes);
                aFrame.rotation.u = new Vec3(0, 0, 1);

                SpawnHero(arenicos, aFrame, false);
            }

            // --- 4. LORDLAR VE ORDULARI (YIĞINLA ASKER) ---
            var empireLords = Hero.AllAliveHeroes
                .Where(h => h.MapFaction != null && h.MapFaction.StringId == "empire_w" && h != neretzes && h != arenicos && h.IsLord)
                .Take(6)
                .ToList();

            float angleStep = (float)Math.PI / (empireLords.Count + 1);
            float currentAngle = 0;
            float radius = 4f;
            CharacterObject troopType = Hero.MainHero.Culture.EliteBasicTroop;

            foreach (var lord in empireLords)
            {
                currentAngle += angleStep;
                float xOffset = (float)Math.Cos(currentAngle) * radius;
                float yOffset = (float)Math.Sin(currentAngle) * radius + 2f;

                MatrixFrame lFrame = MatrixFrame.Identity;
                lFrame.origin = centerPos + new Vec3(xOffset, yOffset, 0);
                lFrame.origin.z = Mission.Scene.GetGroundHeightAtPosition(lFrame.origin);

                Vec3 lookDir = (centerPos - lFrame.origin).NormalizedCopy();
                lFrame.rotation.f = lookDir;
                lFrame.rotation.s = Vec3.CrossProduct(new Vec3(0, 0, 1), lookDir);
                lFrame.rotation.u = new Vec3(0, 0, 1);

                SpawnHero(lord, lFrame, false);

                // Arkalarına asker yığını
                if (troopType != null)
                {
                    for (int i = 0; i < 20; i++) // Sayıyı biraz azalttım performans için
                    {
                        float col = (i % 5) * 1.2f - 2.4f;
                        float row = (i / 5) * 1.2f + 2.0f;
                        Vec3 localPos = new Vec3(col, -row, 0);
                        MatrixFrame troopFrame = lFrame;
                        troopFrame.origin = lFrame.TransformToParent(localPos);
                        troopFrame.origin.z = Mission.Scene.GetGroundHeightAtPosition(troopFrame.origin);

                        try
                        {
                            AgentBuildData troopData = new AgentBuildData(troopType)
                                .Team(Mission.AttackerTeam)
                                .InitialPosition(troopFrame.origin)
                                .InitialDirection(lFrame.rotation.f.AsVec2)
                                .NoHorses(true)
                                .ClothingColor1(lord.MapFaction.Color)
                                .Controller(AgentControllerType.AI);
                            Mission.SpawnAgent(troopData);
                        }
                        catch { }
                    }
                }
            }

            // Ölüleri ekle
            AddCorpses(centerPos, 30);

            InformationManager.DisplayMessage(new InformationMessage("Cinematic Scene is Ready.", Colors.Green));

            // Konuşma Başlat
            if (playerAgent != null && neretzesAgent != null)
            {
                var convManager = Mission.GetMissionBehavior<SandBox.Conversation.MissionLogics.MissionConversationLogic>();
                if (convManager != null) convManager.StartConversation(neretzesAgent, true, false);
            }
        }

        private Agent SpawnHero(Hero hero, MatrixFrame frame, bool isPlayer)
        {
            if (hero == null || hero.CharacterObject == null) return null;
            try
            {
                AgentBuildData agentBuildData = new AgentBuildData(hero.CharacterObject)
                    .Team(Mission.AttackerTeam)
                    .InitialPosition(frame.origin)
                    .InitialDirection(frame.rotation.f.AsVec2)
                    .NoHorses(true)
                    .ClothingColor1(hero.MapFaction?.Color ?? 0xFFFFFFFF)
                    .Controller(isPlayer ? AgentControllerType.Player : AgentControllerType.AI);
                return Mission.SpawnAgent(agentBuildData);
            }
            catch { return null; }
        }

        private void AddCorpses(Vec3 center, int count)
        {
            CharacterObject troop = Hero.MainHero.Culture.EliteBasicTroop;
            if (troop == null) return;

            for (int i = 0; i < count; i++)
            {
                float offsetX = MBRandom.RandomFloat * 15f - 7.5f;
                float offsetY = MBRandom.RandomFloat * 15f - 7.5f;
                Vec3 pos = center;
                pos.x += offsetX;
                pos.y += offsetY;
                pos.z = Mission.Scene.GetGroundHeightAtPosition(pos);

                try
                {
                    AgentBuildData corpseData = new AgentBuildData(troop)
                        .InitialPosition(pos)
                        .Team(Mission.DefenderTeam)
                        .NoHorses(true);

                    Agent corpse = Mission.SpawnAgent(corpseData);
                    if (corpse != null)
                    {
                        corpse.Die(new Blow(corpse.Index) { DamageType = DamageTypes.Cut, BaseMagnitude = 1000f, GlobalPosition = pos });
                    }
                }
                catch { }
            }
        }
    }
}
