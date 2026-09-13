using SandBox;
using SandBox.Missions;
using SandBox.Missions.MissionLogics;
using SandBox.Conversation.MissionLogics;
using SandBox.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Encounters;
using SandBox.Missions.MissionLogics.Towns;

namespace RebellionsAndDemographics.HybridReality
{
    // 1. INPUT BEHAVIOR
    public class HybridRealityInputBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTick(float dt)
        {
            if (Campaign.Current != null &&
                Campaign.Current.CurrentMenuContext == null &&
                MobileParty.MainParty != null &&
                Game.Current.GameStateManager.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState)
            {
                if (Campaign.Current != null &&
                    Campaign.Current.CurrentMenuContext == null &&
                    MobileParty.MainParty != null &&
                    Game.Current.GameStateManager.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState)
                {
                    // Manuel BaÅŸlatma (Ctrl + Shift + J)
                    if (Input.IsKeyDown(InputKey.LeftControl) &&
                        Input.IsKeyDown(InputKey.LeftShift) &&
                        Input.IsKeyPressed(InputKey.J))
                    {
                        HybridRealityMissionManager.OpenHybridScene();
                    }
                    // Otomatik Devam (SÄ±nÄ±rdan GeÃ§iÅŸ)
                    else if (HybridRealityMissionManager.IsAutoReloading)
                    {
                        HybridRealityMissionManager.OpenHybridScene();
                    }
                    // Åehre VarÄ±ÅŸ â€” Sadece 2D haritaya dÃ¶n, settlement sahnesine girme
                    else if (HybridRealityMissionManager.TargetSettlement != null)
                    {
                        Settlement target = HybridRealityMissionManager.TargetSettlement;
                        HybridRealityMissionManager.TargetSettlement = null;

                        // Parti pozisyonunu tam ÅŸehre eÅŸitle
                        MobileParty.MainParty.Position = target.GatePosition;

                        // Oyuncu artÄ±k 2D haritada, isterse normal yoldan ÅŸehre girebilir
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_01}You have reached the vicinity of {TOWN_NAME}. You can enter the town from the map.")
                            .SetTextVariable("TOWN_NAME", target.Name.ToString()).ToString(), Colors.Green));
                    }

                    // --- 2D HARÄ°TAYA DÃœÅÃœNCE KAYIPLARI UYGULA ---
                    if (HybridRealityMissionManager.PendingKilled.Count > 0 || HybridRealityMissionManager.PendingWounded.Count > 0)
                    {
                        if (MobileParty.MainParty != null && MobileParty.MainParty.MemberRoster != null)
                        {
                            foreach (var kvp in HybridRealityMissionManager.PendingKilled)
                            {
                                MobileParty.MainParty.MemberRoster.AddToCounts(kvp.Key, -kvp.Value, false, 0, 0, true, -1);
                                TextObject msg = new TextObject("{=rad_101_02}[Casualty Confirmed] {COUNT} {TROOP_NAME} died in the field battle.");
                                msg.SetTextVariable("COUNT", kvp.Value);
                                msg.SetTextVariable("TROOP_NAME", kvp.Key.Name);
                                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                            }
                            
                            foreach (var kvp in HybridRealityMissionManager.PendingWounded)
                            {
                                MobileParty.MainParty.MemberRoster.AddToCounts(kvp.Key, 0, false, kvp.Value, 0, true, -1);
                                TextObject msg = new TextObject("{=rad_101_03}[Wound Confirmed] {COUNT} {TROOP_NAME} was wounded in the field battle.");
                                msg.SetTextVariable("COUNT", kvp.Value);
                                msg.SetTextVariable("TROOP_NAME", kvp.Key.Name);
                                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
                            }
                        }

                        // Listeleri temizle ki tekrarlamasÄ±n
                        HybridRealityMissionManager.PendingKilled.Clear();
                        HybridRealityMissionManager.PendingWounded.Clear();
                    }
                }
            }
        }

        // 2. MISSION MANAGER (ILSpy ile KanÄ±tlanmÄ±ÅŸ Engine Logic)
        public static class HybridRealityMissionManager
        {
            // Sahneler arasÄ± veri taÅŸÄ±ma (State Management)
            public static bool IsAutoReloading = false;
            public static Vec3? NextSpawnPosition = null;
            public static Vec2? NextSpawnDirection = null;
            public static Settlement TargetSettlement = null; // Hedef ÅŸehir

            // Ã–lÃ¼ ve yaralÄ±larÄ± global olarak tut (2D ekrana dÃ¶nÃ¼ldÃ¼ÄŸÃ¼nde uygulamak iÃ§in)
            public static Dictionary<CharacterObject, int> PendingKilled = new Dictionary<CharacterObject, int>();
            public static Dictionary<CharacterObject, int> PendingWounded = new Dictionary<CharacterObject, int>();

            // Deniz sahneleri listesi â€” bu sahneler atlanÄ±r
            private static readonly List<string> BlockedSeaScenes = new List<string>()
            {
                "battle_terrain_029"
            };

            // Hero dÃ¼ÅŸman popup kilidi
            private static bool _heroPopupShowing = false;

            public static void OpenHybridScene()
            {
                // EÄŸer otomatik reload ise flag'i kapat (bir sonraki dÃ¶ngÃ¼ye kadar)
                IsAutoReloading = false;

                // ============================================
                // GATE 0: YARALI KONTROLÃœ
                // ============================================
                if (Hero.MainHero.HitPoints < Hero.MainHero.CharacterObject.MaxHitPoints() * 0.2f)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_04}You are heavily wounded! You need to heal before roaming the 3D scene.").ToString(), Colors.Red));
                    return;
                }

                // ============================================
                // GATE 1: ORDU LÄ°DERLÄ°K KONTROLÃœ
                // Orduda lider deÄŸilsen 3D'ye giremezsin
                // ============================================
                if (MobileParty.MainParty.Army != null &&
                    MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_05}You are not the army leader! Leave the army or become the leader to enter 3D mode.").ToString(), Colors.Red));
                    return;
                }

                // Eski PlayerEncounter'Ä± temizle (Stale MapEvent crash'ini Ã¶nler)
                // PlayerEncounter.Start() bir MapEvent oluÅŸturur, campaign tick'te crash'e neden olur
                try
                {
                    if (PlayerEncounter.Current != null)
                    {
                        PlayerEncounter.Finish(true);
                    }
                }
                catch (System.Exception)
                {
                    // Temizlik hatasÄ± â€” gÃ¶rmezden gel
                }

                string calculatedSceneID = "empire_battle_terrain_001";
                int calculatedTerrainType = (int)TerrainType.Plain;

                try
                {
                    // --- ADIM 1: Wrapper'dan Veriyi Al (Reflection) ---
                    object mapSceneWrapper = Campaign.Current.MapSceneWrapper;
                    MethodInfo getPatchMethod = mapSceneWrapper.GetType().GetMethod("GetMapPatchAtPosition");
                    object mapPatchData = getPatchMethod.Invoke(mapSceneWrapper, new object[] { MobileParty.MainParty.Position });

                    // --- ADIM 2: Model'den Sahneyi Al (Reflection) ---
                    // DefaultSceneModel'e ulaÅŸÄ±yoruz.
                    // BattleSceneModel yerine doÄŸrudan DefaultSceneModel oluÅŸturuyoruz
                    object sceneModel = new TaleWorlds.CampaignSystem.GameComponents.DefaultSceneModel(); // Veya DefaultSceneModel

                    // EÄŸer model null ise manuel oluÅŸturmayÄ± dene (Yedek plan)
                    if (sceneModel == null)
                    {
                        // ILSpy'da gÃ¶rdÃ¼ÄŸÃ¼mÃ¼z sÄ±nÄ±fÄ± manuel Ã¼retiyoruz
                        sceneModel = new TaleWorlds.CampaignSystem.GameComponents.DefaultSceneModel();
                    }

                    if (sceneModel != null)
                    {
                        MethodInfo getSceneMethod = sceneModel.GetType().GetMethod("GetBattleSceneForMapPatch");
                        if (getSceneMethod != null)
                        {
                            // false = isNavalEncounter
                            calculatedSceneID = (string)getSceneMethod.Invoke(sceneModel, new object[] { mapPatchData, false });
                        }
                    }

                    // --- ADIM 3: TerrainType (Crash Ã–nleyici) ---
                    MethodInfo getTerrainMethod = mapSceneWrapper.GetType().GetMethod("GetTerrainTypeAtPosition");
                    if (getTerrainMethod != null)
                    {
                        calculatedTerrainType = (int)getTerrainMethod.Invoke(mapSceneWrapper, new object[] { MobileParty.MainParty.Position });
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_06}Engine Error (Fallback Scene): {ERROR}").SetTextVariable("ERROR", ex.Message).ToString(), Colors.Red));
                }

                // ============================================
                // GATE 2: DENÄ°Z SAHNESÄ° ATLASI
                // battle_terrain_029 vs. deniz sahneleri engellenir
                // ============================================
                if (BlockedSeaScenes.Contains(calculatedSceneID))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_07}âš“ Sea scene detected ({SCENE}). Returning to 2D map...").SetTextVariable("SCENE", calculatedSceneID).ToString(), Colors.Yellow));
                    // Gvenli bir 2D harita noktasna ta
                    var safePos = Campaign.Current.MapSceneWrapper.GetAccessiblePointNearPosition(MobileParty.MainParty.Position, 5f);
                    MobileParty.MainParty.Position = safePos;
                    
                    return;
                }

                // ============================================
                // GATE 3 KALDIRILDI - ArtÄ±k 3D sahnede dÃ¼ÅŸman engeli yok. Radar sistemi kullanÄ±lacak.
                // ============================================

                // HiÃ§bir engel yok â€” sahneyi baÅŸlat
                bool hasEnemies = false;
                Vec2 pMapPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == MobileParty.MainParty || !party.IsVisible) continue;
                    if (party.MapFaction != null && party.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
                    {
                        if (pMapPos.Distance(new Vec2(party.Position.X, party.Position.Y)) < 50f)
                        {
                            hasEnemies = true;
                            break;
                        }
                    }
                }

                LaunchMission(calculatedSceneID, calculatedTerrainType, hasEnemies);
            }

            /// <summary>
            /// Sahneyi gerÃ§ekten aÃ§ar. Gate kontrollerinden sonra Ã§aÄŸrÄ±lÄ±r.
            /// </summary>
            private static void LaunchMission(string sceneID, int terrainType, bool hasEnemies)
            {
                MissionInitializerRecord rec = new MissionInitializerRecord(sceneID)
                {
                    DoNotUseLoadingScreen = false,
                    PlayingInCampaignMode = true,
                    TerrainType = terrainType,
                    AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position),
                    DecalAtlasGroup = (int)DecalAtlasGroup.All
                    // SceneLevel satÄ±rÄ± tamamen silindi Ã§Ã¼nkÃ¼ API'de karÅŸÄ±lÄ±ÄŸÄ± yok
                };

                MissionState.OpenNew("HybridRealityWalk", rec,
                    (mission) => {
                        List<MissionBehavior> behaviors = new List<MissionBehavior>
                        {
                            new MissionOptionsComponent(), new NavmeshFixBehavior(),
                            new MissionBasicTeamLogic(),
                            new MissionAgentLookHandler(),
                            ViewCreator.CreateMissionLeaveView(),
                            ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                            ViewCreator.CreateMissionSingleplayerEscapeMenu(false),
                            ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                            new HybridMovementLogic(),
                            new LiveCompassMissionView()
                        };

                        return behaviors.ToArray();
                    }, true, true);
            }
        }

        // 3. HYBRID LOGIC (HÄ±z, DaÄŸ KeÃ§isi, Sonsuz DÃ¶ngÃ¼)
        public class HybridMovementLogic : MissionLogic
        {
            private Vec3 _lastPlayerPos;
            private Vec2 _lastSafeMapPos; // Deniz engeli iÃ§in son gÃ¼venli 2D harita pozisyonu
            private bool _isInitialized = false;
            private const float MapDistanceMultiplier = 0.1f; // Nether MantÄ±ÄŸÄ±: 100x daha hÄ±zlÄ± (Eskisi 0.001f idi)

            // Sahne merkezi (spawn noktasÄ±ndan hesaplanÄ±r)
            private Vec3 _centerPos;
            private float _missionStartTime = -1f;
            private float _tickTimer = 0f; // Periyodik kontroller iÃ§in
            private float _waterWarningCooldown = 0f; // Deniz uyarÄ±sÄ± spam engeli
            private bool _settlementPopupShown = false;
            private float _settlementPopupCooldown = 0f;
            private bool _navigationPopupShown = false; // Navigasyon popup kilidi

            // === MESAFE-TABANLI NAVÄ°GASYON ===
            private float _lastNavigationDistance = 0f; // Son popup gÃ¶sterildiÄŸindeki yÃ¼rÃ¼me mesafesi
            private const float NavigationPopupInterval = 200f; // Her 200m'de popup
            private List<string> _previouslyShownSettlements = new List<string>(); // SirkÃ¼lasyon: Ã¶nceki 5 ÅŸehri hariÃ§ tut

            // Escaped enemies
            private static List<string> _escapedEnemyPartyIds = new List<string>();
            private List<string> _bribedParties = new List<string>();

            // === YANINDAKI BiRLiKLER ===
            private List<Agent> _playerPartyAgents = new List<Agent>();
            private bool _partyTroopsSpawned = false;
            private enum TroopCommand { Follow, Attack, Retreat }
            private TroopCommand _currentTroopCommand = TroopCommand.Follow;

            // === GiZLi HAZiNELER ===
            private List<GameEntity> _treasureBoxes = new List<GameEntity>();
            private bool _treasuresSpawned = false;
            private int _treasuresCollected = 0;

            // === KESiF XP ===
            private float _totalDistanceWalked = 0f;
            private float _lastXpAwardDistance = 0f;
            private const float XpPerDistanceUnit = 50f;

            // === TUCCAR & TÃœCCAR NPC ===
            private float _allyFollowTimer = 0f;
            private List<string> _notifiedMerchants = new List<string>();
            private bool _merchantPopupShown = false;
            private bool _sceneMerchantSpawned = false;
            private Agent _sceneMerchantAgent = null;
            private float _merchantInteractionCooldown = 0f;

            public override void OnMissionTick(float dt)
            {
                base.OnMissionTick(dt);

                if (!_isInitialized)
                {
                    SpawnPlayerAgent();
                    _isInitialized = true;
                    _missionStartTime = Mission.Current.CurrentTime;
                    _lastSafeMapPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
                    // Kampanya zamanÄ±nÄ± aktif et (gece/gÃ¼ndÃ¼z dÃ¶ngÃ¼sÃ¼)
                    try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward; } catch { }
                    return;
                }

                // Oyuncu spawn olduktan sonra birlikleri spawn et (tek seferlik)
                if (!_partyTroopsSpawned && Agent.Main != null && Agent.Main.IsActive())
                {
                    SpawnPlayerPartyTroops();
                    _partyTroopsSpawned = true;
                }
                // Hazine: %30 ÅŸansla Ã§Ä±kar
                if (!_treasuresSpawned && Agent.Main != null && Agent.Main.IsActive())
                {
                    _treasuresSpawned = true;
                    if (MBRandom.RandomFloat < 0.30f)
                        SpawnHiddenTreasures();
                    else
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_08}No hidden treasure found in the area.").ToString(), Colors.Gray));
                }
                // TÃ¼ccar NPC: %20 ÅŸansla Ã§Ä±kar
                if (!_sceneMerchantSpawned && Agent.Main != null && Agent.Main.IsActive())
                {
                    _sceneMerchantSpawned = true;
                    if (MBRandom.RandomFloat < 0.20f)
                        SpawnSceneMerchant();
                }

                // Grace Period
                if (Mission.Current.CurrentTime - _missionStartTime < 3f)
                    return;

                if (Agent.Main != null && Agent.Main.IsActive())
                {
                    // 1. MESAFE-TABANLI NAVÄ°GASYON (SÄ±nÄ±r yerine)
                    CheckDistanceNavigation();

                    // 2. PERiYODiK KONTROLLER (0.3s tick â€” hÄ±zlÄ± spawn)
                    _tickTimer += dt;
                    if (_tickTimer > 0.3f)
                    {
                        CheckSettlements();
                        CheckSafety();
                        UpdateLivingWorld();
                        CheckMerchantCaravans();
                        CheckSceneMerchantInteraction();
                        _tickTimer = 0f;
                    }

                    // 3. KESiF XP (SÃ¼rekli)
                    UpdateScoutXP(dt);
                    UpdateAllyFollow(dt);

                    // Living World - Her frame hareket gÃ¼ncellemesi (Smooth olmasÄ± iÃ§in)
                    UpdateLivingWorldMovement(dt);

                    // 2. HIZ HÄ°LESÄ° KALDIRILDI
                    // Karakter artÄ±k nativ hÄ±zÄ±nda yÃ¼rÃ¼yecek.

                    // 3. DAÄ KEÃ‡Ä°SÄ° (Physics Override) KALDIRILDI
                    // SÃ¼rekli animasyonlarÄ± kestiÄŸi iÃ§in (ellerin havada kalmasÄ±) silindi.

                    // 4. HARÄ°TA SENKRONÄ°ZASYONU (Treadmill) + DENÄ°Z ENGELÄ°
                    Vec3 currentPos = Agent.Main.Position;
                    float distanceMoved = currentPos.Distance(_lastPlayerPos);

                    // Deniz uyarÄ± cooldown
                    if (_waterWarningCooldown > 0f) _waterWarningCooldown -= dt;

                    if (distanceMoved > 0.01f)
                    {
                        Vec3 delta = currentPos - _lastPlayerPos;
                        float currentMultiplier = MapDistanceMultiplier;

                        Vec2 newPos2D = new Vec2(MobileParty.MainParty.Position.X + (delta.x * currentMultiplier), MobileParty.MainParty.Position.Y + (delta.y * currentMultiplier));

                        // DENÄ°Z KONTROLÃœ: Yeni pozisyon su mu?
                        try
                        {
                            TerrainType nextTerrain = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(
                                new CampaignVec2(newPos2D, true));

                            if (nextTerrain == TerrainType.Water)
                            {
                                // Denize giremez! Geri teleport et
                                Agent.Main.TeleportToPosition(_lastPlayerPos);
                                // Harita pozisyonunu son gvenli yere geri al
                                var safe2D = Campaign.Current.MapSceneWrapper.GetAccessiblePointNearPosition(new TaleWorlds.CampaignSystem.CampaignVec2(_lastSafeMapPos, true), 5f);
                                MobileParty.MainParty.Position = safe2D;

                                if (_waterWarningCooldown <= 0f)
                                {
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        new TextObject("{=rad_101_09}âš  You are falling into the sea! You cannot go further.").ToString(), Colors.Red));
                                    _waterWarningCooldown = 3f; // 3 saniye spam engeli
                                }
                            }
                            else
                            {
                                // GÃ¼venli - pozisyonu gÃ¼ncelle
                                MobileParty.MainParty.Position = new CampaignVec2(newPos2D, MobileParty.MainParty.Position.IsOnLand);
                                _lastSafeMapPos = newPos2D;
                            }
                        }
                        catch
                        {
                            // Terrain kontrolÃ¼ baÅŸarÄ±sÄ±z olursa yine de gÃ¼ncelle
                            MobileParty.MainParty.Position = new CampaignVec2(newPos2D, MobileParty.MainParty.Position.IsOnLand);
                            _lastSafeMapPos = newPos2D;
                        }
                    }
                    _lastPlayerPos = currentPos;
                }
            }

            private void CheckSettlements()
            {
                if (Agent.Main == null || MobileParty.MainParty == null) return;

                // Popup cooldown gÃ¼ncelle
                if (_settlementPopupCooldown > 0f) _settlementPopupCooldown -= 1f;

                // Popup zaten aÃ§Ä±ksa veya cooldown'daysa tekrar kontrol etme
                if (_settlementPopupShown || _navigationPopupShown || _merchantPopupShown || _settlementPopupCooldown > 0f) return;

                // En yakÄ±n ÅŸehir veya kaleyi bul (kÃ¶yleri ATLA â€” crash yapmÄ±yorlar)
                Settlement nearest = null;
                float minDistance = 3.0f; // YaklaÅŸma mesafesi (Harita birimi)

                foreach (Settlement settlement in Settlement.All)
                {
                    // Sadece ÅŸehir ve kaleler â€” kÃ¶yleri atla
                    if (!settlement.IsTown && !settlement.IsCastle) continue;
                    if (!settlement.IsVisible) continue;

                    var partyPos = MobileParty.MainParty.Position;
                    var sPos = settlement.GatePosition;
                    float dist = new Vec2(partyPos.X, partyPos.Y).Distance(new Vec2(sPos.X, sPos.Y));
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = settlement;
                    }
                }

                if (nearest != null)
                {
                    // Åehir/Kale tespit edildi â€” Popup gÃ¶ster
                    _settlementPopupShown = true;
                    Settlement capturedSettlement = nearest; // Closure iÃ§in yakala

                    InformationManager.ShowInquiry(
                        new InquiryData(
                            new TextObject("{=rad_101_10}Settlement Detected").ToString(),
                            new TextObject("{=rad_101_11}You are near {SETTLEMENT}. Do you want to return to the 2D map and enter?").SetTextVariable("SETTLEMENT", capturedSettlement.Name).ToString(),
                            true, true,
                            new TextObject("{=rad_101_12}Yes").ToString(),
                            new TextObject("{=rad_101_13}No").ToString(),
                            () => // EVET callback
                            {
                                // Harita pozisyonunu ÅŸehre eÅŸitle
                                MobileParty.MainParty.Position = capturedSettlement.GatePosition;

                                // Mission'dan Ã§Ä±k â€” 2D haritaya dÃ¶n
                                HybridRealityMissionManager.TargetSettlement = capturedSettlement;
                                HybridRealityMissionManager.IsAutoReloading = false;
                                _settlementPopupShown = false;

                                if (Mission.Current != null && Mission.Current.CurrentState == Mission.State.Continuing)
                                    Mission.Current.EndMission();
                            },
                            () => // HAYIR callback
                            {
                                // Popup kapat, yÃ¼rÃ¼meye devam et
                                _settlementPopupShown = false;
                                _settlementPopupCooldown = 15f; // 15 saniye tekrar sorma
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_101_14}You continue walking.").ToString(), Colors.Yellow));
                            },
                            "", 0f, null, null, null),
                        true, false);
                }
            }

            private void CheckSafety()
            {
                if (MobileParty.MainParty == null) return;

                try
                {
                    var mapScene = Campaign.Current.MapSceneWrapper;
                    TerrainType terrain = mapScene.GetTerrainTypeAtPosition(MobileParty.MainParty.Position);

                    // Deniz artÄ±k map sync'te engelleniyor, ama backup olarak kontrol et
                    if (terrain == TerrainType.Water)
                    {
                        // Son gvenli pozisyona geri dn
                        var safe2D = Campaign.Current.MapSceneWrapper.GetAccessiblePointNearPosition(new TaleWorlds.CampaignSystem.CampaignVec2(_lastSafeMapPos, true), 5f);
                        MobileParty.MainParty.Position = safe2D;
                        if (Agent.Main != null)
                            Agent.Main.TeleportToPosition(_lastPlayerPos);
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_15}Rescued from the sea!").ToString(), Colors.Red));
                    }
                }
                catch { }
            }


            // --- LIVING WORLD (CANLI DÃœNYA) ---
            private Dictionary<MobileParty, List<Agent>> _spawnedParties = new Dictionary<MobileParty, List<Agent>>();
            private const float VisualRange = 100f; // 100 birim = ~1000m (Gözden uzak spawn)

            private void UpdateLivingWorld()
            {
                if (MobileParty.MainParty == null || Agent.Main == null) return;

                // 1. Party TaramasÄ±
                foreach (var party in MobileParty.All)
                {
                    if (!party.IsVisible || party == MobileParty.MainParty) continue;

                    // KaÃ§mÄ±ÅŸ dÃ¼ÅŸman kontrolÃ¼ â€” bu sahnede Ã§Ä±kmasÄ±n
                    string partyId = party.StringId ?? party.Name?.ToString() ?? "";
                    if (_escapedEnemyPartyIds.Contains(partyId)) continue;

                    // DÃ¼ÅŸmansa 3D'ye Asla Alma!
                    bool isHostile = party.MapFaction != null &&
                        party.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction);
                    if (isHostile) continue;

                    float dist = new Vec2(party.Position.X, party.Position.Y).Distance(
                        new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y));

                    // GÃ¶rÃ¼ÅŸ alanÄ±nda mÄ±?
                    if (dist < VisualRange)
                    {
                        if (!_spawnedParties.ContainsKey(party))
                        {
                            SpawnPartyAgent(party);
                        }
                    }
                    else
                    {
                        // Uzaktaysa sil
                        if (_spawnedParties.ContainsKey(party))
                        {
                            DespawnPartyAgent(party);
                        }
                    }
                }

                // Temizlik (Yok olmuÅŸ partiler)
                List<MobileParty> toRemove = new List<MobileParty>();
                foreach (var kvp in _spawnedParties)
                {
                    if (!kvp.Key.IsVisible || kvp.Key.MapEvent != null || kvp.Key.AttachedTo != null)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var p in toRemove) DespawnPartyAgent(p);
            }

            private void SpawnPartyAgent(MobileParty party)
            {
                if (_spawnedParties.ContainsKey(party)) return; // Ã‡ifte spawn engelle

                List<Agent> spawnedAgents = new List<Agent>();

                // 1. Lideri Spawnla
                // Captain mÃ¼lkÃ¼ yoksa EliteTroop kullanalÄ±m
                CharacterObject leaderChar = party.LeaderHero?.CharacterObject;
                if (leaderChar == null) leaderChar = party.Party.Culture.BasicTroop;

                if (leaderChar != null)
                {
                    Agent leader = SpawnSingleAgent(party, leaderChar, Vec2.Zero); // Merkezde
                    if (leader != null) spawnedAgents.Add(leader);
                }

                // 2. BirliÄŸini Spawnla â€” SÄ±kÄ± formasyon (lord atlÄ±larla birlikte)
                int count = 0;
                // Limit kaldÄ±rÄ±ldÄ±, tÃ¼m birliÄŸi veya olabildiÄŸince sÄ±ÄŸanÄ± spawnla
                int maxTroops = 150; 

                var roster = party.MemberRoster;
                if (roster.TotalManCount > 1)
                {
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var element = roster.GetElementCopyAtIndex(i);
                        if (element.Character.IsHero && element.Character == party.LeaderHero?.CharacterObject) continue;

                        int amountToSpawn = Math.Min(element.Number, maxTroops - count);
                        for (int j = 0; j < amountToSpawn; j++)
                        {
                            // SÄ±kÄ± formasyon: 1-3m arasÄ± (lorddan kopmayacak)
                            float angle = (float)MBRandom.RandomFloat * 6.28f;
                            float radius = 1f + (float)MBRandom.RandomFloat * 2f;
                            Vec2 offset = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

                            Agent troop = SpawnSingleAgent(party, element.Character, offset);
                            if (troop != null) spawnedAgents.Add(troop);

                            count++;
                            if (count >= maxTroops) break;
                        }
                        if (count >= maxTroops) break;
                    }
                }

                // DÃ–NGÃœ HATASI FIX: OluÅŸturduÄŸumuz ajanlarÄ± takibe alÄ±yoruz ki bir daha spawnlamasÄ±n
                _spawnedParties[party] = spawnedAgents; // Her zaman ekle ki sÃ¼rekli denemesin

                // Debug: KaÃ§ ajan baÅŸarÄ±yla spawnlandÄ±?
                if (spawnedAgents.Count > 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_16}âœ… {PARTY_NAME}: {COUNT} agents spawned.")
                        .SetTextVariable("PARTY_NAME", party.Name)
                        .SetTextVariable("COUNT", spawnedAgents.Count).ToString(), Colors.Green));
                }
            }

            private Agent SpawnSingleAgent(MobileParty party, CharacterObject character, Vec2 offsetFromCenter)
            {
                if (Agent.Main == null || Mission.Current.Scene == null) return null;

                // Parti yÃ¶nÃ¼nÃ¼ hesapla (harita delta â†’ sahne yÃ¶nÃ¼)
                Vec2 pPos = new Vec2(party.Position.X, party.Position.Y);
                Vec2 mPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
                Vec2 dirToParty = pPos - mPos;
                float mapDist = dirToParty.Length;
                if (mapDist > 0.001f) dirToParty = dirToParty * (1f / mapDist);
                else dirToParty = Vec2.Forward;

                // Sahne mesafesi: 15-40m arasÄ± (NavMesh iÃ§inde kalacak kadar yakÄ±n)
                float sceneDist = Math.Min(mapDist * (1f / MapDistanceMultiplier), 40f);
                sceneDist = Math.Max(sceneDist, 15f);

                // Spawn pozisyonu: Oyuncunun konumundan parti yÃ¶nÃ¼nde
                float rawX = Agent.Main.Position.x + dirToParty.X * sceneDist + offsetFromCenter.X;
                float rawY = Agent.Main.Position.y + dirToParty.Y * sceneDist + offsetFromCenter.Y;
                Vec3 rawPos = new Vec3(rawX, rawY, 0);

                // NavMesh'e snap et (FieldCommandSpawnLogic'in kanÄ±tlanmÄ±ÅŸ yÃ¶ntemi)
                Vec3 finalPos;
                WorldPosition worldPos = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, rawPos, false);
                Vec3 navMeshPos = worldPos.GetNavMeshVec3();

                if (navMeshPos.IsValid)
                {
                    finalPos = navMeshPos;
                }
                else
                {
                    // Fallback: GetHeightAtPoint ile dene
                    float z = 0;
                    if (!Mission.Current.Scene.GetHeightAtPoint(new Vec2(rawX, rawY), BodyFlags.CommonCollisionExcludeFlags, ref z))
                    {
                        // Bu pozisyon tamamen geÃ§ersiz â€” oyuncuya daha yakÄ±n dene
                        for (float factor = 0.7f; factor >= 0.2f; factor -= 0.1f)
                        {
                            float tryX = Agent.Main.Position.x + dirToParty.X * sceneDist * factor + offsetFromCenter.X;
                            float tryY = Agent.Main.Position.y + dirToParty.Y * sceneDist * factor + offsetFromCenter.Y;
                            Vec3 tryRaw = new Vec3(tryX, tryY, 0);
                            WorldPosition tryWP = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, tryRaw, false);
                            Vec3 tryNav = tryWP.GetNavMeshVec3();
                            if (tryNav.IsValid)
                            {
                                finalPos = tryNav;
                                goto spawnAgent;
                            }
                            float tryZ = 0;
                            if (Mission.Current.Scene.GetHeightAtPoint(new Vec2(tryX, tryY), BodyFlags.CommonCollisionExcludeFlags, ref tryZ))
                            {
                                finalPos = new Vec3(tryX, tryY, tryZ);
                                goto spawnAgent;
                            }
                        }
                        return null; // HiÃ§bir geÃ§erli pozisyon bulunamadÄ±
                    }
                    finalPos = new Vec3(rawX, rawY, z);
                }

                spawnAgent:
                // Team Belirleme
                bool isEnemy = party.MapFaction != null && party.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction);
                Team agentTeam = isEnemy ? Mission.Current.PlayerEnemyTeam : Mission.Current.PlayerTeam;

                // YÃ¶n: Partinin hareket yÃ¶nÃ¼ne baksÄ±n
                Vec2 lookDir = dirToParty;
                if (isEnemy) lookDir = -dirToParty; // DÃ¼ÅŸmanlar oyuncuya baksÄ±n

                try
                {
                    AgentBuildData agentBuildData = new AgentBuildData(character)
                        .Equipment(character.Equipment)
                        .InitialPosition(finalPos)
                        .InitialDirection(lookDir)
                        .NoHorses(false)
                        .Controller(AgentControllerType.AI)
                        .ClothingColor1(party.MapFaction != null ? party.MapFaction.Color : 0xFFFFFFFF)
                        .ClothingColor2(party.MapFaction != null ? party.MapFaction.Color2 : 0xFFFFFFFF);

                    if (character.HasMount())
                    {
                        ItemObject horseItem = character.Equipment[EquipmentIndex.ArmorItemEndSlot].Item;
                        if (horseItem != null)
                            agentBuildData.MountKey(MountCreationKey.GetRandomMountKeyString(horseItem, MBRandom.RandomInt()));
                    }

                    Agent newAgent = Mission.Current.SpawnAgent(agentBuildData);
                    if (newAgent != null)
                    {
                        newAgent.SetWatchState(Agent.WatchState.Alarmed);
                        newAgent.WieldInitialWeapons(); // Silah Ã§ek
                    }
                    return newAgent;
                }
                catch (System.Exception)
                {
                    return null; // Spawn hatasÄ± â€” sessizce geÃ§
                }
            }

            private void DespawnPartyAgent(MobileParty party)
            {
                if (_spawnedParties.TryGetValue(party, out List<Agent> agents))
                {
                    foreach (var agent in agents)
                    {
                        if (agent != null && agent.State == TaleWorlds.Core.AgentState.Active && agent.IsActive())
                        {
                            try { agent.FadeOut(false, true); } catch { } // NazikÃ§e yok ol
                        }
                    }
                    _spawnedParties.Remove(party);
                }
            }

            private void UpdateLivingWorldMovement(float dt)
            {
                if (Agent.Main == null || Mission.Current.Scene == null) return;

                foreach (var kvp in _spawnedParties)
                {
                    MobileParty party = kvp.Key;
                    List<Agent> agents = kvp.Value;

                    // Hedef Konum (ArtÄ±k tamamen barÄ±ÅŸÃ§Ä±l partiler)
                    // Dost/NÃ¶tr: Harita yÃ¶nÃ¼nde hareket
                    Vec3 partyTargetPos = CalculateScenePositionForParty(party);

                    // Lider agent
                    Agent leader = agents.Count > 0 ? agents[0] : null;

                    for (int i = 0; i < agents.Count; i++)
                    {
                        Agent agent = agents[i];
                        if (agent == null || !agent.IsActive()) continue;

                        // Oyuncuya bak (look-at davranÄ±ÅŸÄ±)
                        try
                        {
                            if (Agent.Main != null)
                            {
                                float distToPlayer = agent.Position.Distance(Agent.Main.Position);
                                if (distToPlayer < 15f) // 15m iÃ§indeyse oyuncuya bak
                                {
                                    agent.SetLookAgent(Agent.Main);
                                    Vec3 lookDir = Agent.Main.Position - agent.Position;
                                    lookDir.Normalize();
                                    agent.LookDirection = lookDir;
                                }
                            }
                        }
                        catch { }

                        Vec3 movePos = partyTargetPos;
                        if (i > 0) // TakipÃ§i ofseti
                        {
                            int seed = agent.Index * 123;
                            float angle = (seed % 360) * 0.017f;
                            float radius = 2f + (seed % 300) / 100f;
                            Vec3 offset = new Vec3((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius, 0);
                            movePos += offset;
                        }

                        // NavMesh'e snap et (hareket iÃ§in de kanÄ±tlanmÄ±ÅŸ yÃ¶ntem)
                        try
                        {
                            WorldPosition moveWP = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, movePos, false);
                            Vec3 navPos = moveWP.GetNavMeshVec3();
                            if (navPos.IsValid)
                            {
                                WorldPosition validWP = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, navPos, false);
                                if (validWP.IsValid)
                                {
                                    agent.SetScriptedPosition(ref validWP, false, Agent.AIScriptedFrameFlags.None);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            // Helper method to ensure position is valid on NavMesh
            private bool GetSafePosition(ref Vec3 position)
            {
                // NaN ve Infinity kontrolÃ¼
                if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return false;
                if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z)) return false;

                // 1. YÃ¼kseklik kontrolÃ¼
                float z = 0;
                Vec2 vPos = position.AsVec2;
                if (!Mission.Current.Scene.GetHeightAtPoint(vPos, BodyFlags.CommonCollisionExcludeFlags, ref z))
                {
                    return false; // GeÃ§ersiz yÃ¼kseklik â€” boÅŸluÄŸa dÃ¼ÅŸer
                }
                position.z = z;

                // 2. NavMesh kontrolÃ¼ â€” off-mesh pozisyonlarÄ± kesinlikle reddet
                WorldPosition wp = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, position, false);
                if (wp.IsValid && wp.GetNavMesh() != UIntPtr.Zero)
                {
                    return true;
                }

                // NavMesh Ã¼zerinde deÄŸil â€” PhysX crash'ini Ã¶nlemek iÃ§in reddet
                return false;
            }



            private const float MaxSceneSpawnDistance = 100f; // Max distance from player (boundary=120, bu iÃ§inde kalÄ±r)

            private Vec3 CalculateScenePositionForParty(MobileParty party)
            {
                // FormÃ¼l: ScenePos = PlayerScenePos + (PartyMapPos - PlayerMapPos) / Multiplier
                // Biz MapDistanceMultiplier = 0.1f kullanÄ±yoruz. 
                // Yani 10 metre scene = 1 map birimi.

                if (Agent.Main == null) return Vec3.Zero;

                // CampaignVec2 Ã§Ä±karma hatasÄ± -> Manuel Vec2 Ã§evrimi
                Vec2 pPos = new Vec2(party.Position.X, party.Position.Y);
                Vec2 mPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
                Vec2 deltaMap = pPos - mPos;
                Vec2 deltaScene = deltaMap * (1f / MapDistanceMultiplier); // BÃ¶lÃ¼yoruz Ã§Ã¼nkÃ¼ Multiplier kÃ¼Ã§Ã¼ltÃ¼yordu

                // CLAMP: Sahne sÄ±nÄ±rlarÄ± iÃ§inde kal (battle_terrain_001 kÃ¼Ã§Ã¼k bir harita)
                float dist = deltaScene.Length;
                if (dist > MaxSceneSpawnDistance)
                {
                    deltaScene = deltaScene * (MaxSceneSpawnDistance / dist);
                }

                // DÄ°KKAT: YÃ¶nler tutuyor mu? Bannerlord Map Y+ Kuzey, Scene Y+ Ä°leri. Genelde tutar.
                return Agent.Main.Position + new Vec3(deltaScene.X, deltaScene.Y, 0);
            }

            private void PerformEmergencyRescue(string reason)

            {
                if (MobileParty.MainParty == null) return;

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_101_17}DANGER: {REASON} Teleporting to a safe zone...").SetTextVariable("REASON", reason).ToString(), Colors.Red));

                // En yakÄ±n yerleÅŸime Ä±ÅŸÄ±nla
                Settlement nearest = null;
                float minDistance = 100000f;

                foreach (var s in Settlement.All)
                {
                    if (s.IsVisible && (s.IsTown || s.IsVillage))
                    {
                        var partyPos = MobileParty.MainParty.Position;
                        var sPos = s.GatePosition;
                        float d = new Vec2(partyPos.X, partyPos.Y).Distance(new Vec2(sPos.X, sPos.Y));
                        if (d < minDistance)
                        {
                            minDistance = d;
                            nearest = s;
                        }
                    }
                }
                if (nearest != null)
                {
                    MobileParty.MainParty.Position = nearest.Position;
                    HybridRealityMissionManager.TargetSettlement = nearest;

                    Utilities.EnableGlobalLoadingWindow();
                    Mission.Current.EndMission();
                }
            }
            private void CheckDistanceNavigation()
            {
                if (Agent.Main == null || MobileParty.MainParty == null) return;
                if (_settlementPopupShown || _navigationPopupShown || _merchantPopupShown) return;

                // Her 100m yÃ¼rÃ¼yÃ¼ÅŸte popup gÃ¶ster
                if (_totalDistanceWalked - _lastNavigationDistance < NavigationPopupInterval) return;

                // === DÃœÅMAN / YAÄMACI KONTROLÃœ (100m sÄ±nÄ±rÄ± dolduktan sonra) ===
                MobileParty closestEnemy = null;
                float closestDist = float.MaxValue;
                Vec2 playerMapPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == MobileParty.MainParty || !party.IsVisible) continue;
                    
                    // RÃ¼ÅŸvet verilen veya Ã¶nceden kaÃ§Ä±lanlarÄ± yoksay
                    string partyId = party.StringId ?? party.Name?.ToString() ?? "";
                    if (_escapedEnemyPartyIds.Contains(partyId) || _bribedParties.Contains(partyId)) continue;

                    if (party.MapFaction != null && party.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
                    {
                        float dist = playerMapPos.Distance(new Vec2(party.Position.X, party.Position.Y));
                        if (dist < 30f && dist < closestDist) // 30 birim yakÄ±nlÄ±k tehlikeli
                        {
                            closestDist = dist;
                            closestEnemy = party;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    _navigationPopupShown = true;
                    // DÃ¼ÅŸman var, savaÅŸ veya kaÃ§ popup'Ä± gÃ¶ster
                    string enemyName = closestEnemy.LeaderHero != null ? closestEnemy.LeaderHero.Name.ToString() : closestEnemy.Name.ToString();
                    InformationManager.ShowInquiry(
                        new InquiryData(
                            new TextObject("{=rad_101_18}Enemies Ahead!").ToString(),
                            new TextObject("{=rad_101_19}Enemy forces ({ENEMY_NAME}) are ambushing your path. What will you do?")
                            .SetTextVariable("ENEMY_NAME", enemyName).ToString(),
                            true, true,
                            new TextObject("{=rad_101_20}Fight!").ToString(),
                            new TextObject("{=rad_101_21}Flee (-80 Denars)").ToString(),
                            () => // SAVAÅ callback
                            {
                                _navigationPopupShown = false;
                                // Mission'dan Ã§Ä±k â€” 2D haritaya dÃ¶n ve oyuncu haritada savaÅŸsÄ±n.
                                HybridRealityMissionManager.IsAutoReloading = false;
                                if (Mission.Current != null && Mission.Current.CurrentState == Mission.State.Continuing)
                                    Mission.Current.EndMission();
                            },
                            () => // KAÃ‡ callback
                            {
                                _navigationPopupShown = false;
                                if (Hero.MainHero.Gold >= 80)
                                {
                                    Hero.MainHero.ChangeHeroGold(-80);
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        new TextObject("{=rad_101_22}The troops took a longer route and it cost -80 Denars. ({ENEMY_NAME} bypassed)")
                                        .SetTextVariable("ENEMY_NAME", enemyName).ToString(), Colors.Yellow));
                                    
                                    // DÃœÅMANI LÄ°STEYE EKLE: ArtÄ±k bu dÃ¼ÅŸman bu sahnede radar uyarÄ±larÄ±nÄ± tetiklemeyecek
                                    string pid = closestEnemy.StringId ?? closestEnemy.Name?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(pid))
                                    {
                                        _bribedParties.Add(pid);
                                    }
                                    
                                    // 3D Haritadan Ã‡IKMA! Oynamaya devam et - Radara takÄ±lmayacaklar.
                                }
                                else
                                {
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        new TextObject("{=rad_101_23}You don't have enough money to flee! You must fight.").ToString(), Colors.Red));
                                    // Paran yoksa savaÅŸa girmek zorunda (Yine 2D'ye atÄ±yoruz)
                                    HybridRealityMissionManager.IsAutoReloading = false;
                                    if (Mission.Current != null && Mission.Current.CurrentState == Mission.State.Continuing)
                                        Mission.Current.EndMission();
                                }
                            },
                            "", 0f, null, null, null),
                        true, false);
                    
                    // Mesafe sayacÄ±nÄ± gÃ¼ncelle (BÃ¶ylece bir 100m daha yÃ¼rÃ¼meden tekrar spamlamaz)
                    _lastNavigationDistance = _totalDistanceWalked;
                    return; 
                }

                // DÃ¼ÅŸman yoksa popup rutinine devam
                _navigationPopupShown = true;
                _lastNavigationDistance = _totalDistanceWalked;

                // === SÄ°RKÃœLASYONLU ÅEHÄ°R SEÃ‡Ä°MÄ° ===
                // Ã–nceki popup'ta gÃ¶sterilen ÅŸehirleri hariÃ§ tut â†’ kÄ±sÄ±r dÃ¶ngÃ¼ engeli
                var allTowns = new List<(Settlement s, float dist, string yonStr)>();

                foreach (Settlement s in Settlement.All)
                {
                    if ((!s.IsTown && !s.IsCastle) || !s.IsVisible) continue;
                    Vec2 sPos = new Vec2(s.Position.X, s.Position.Y);
                    float d = playerMapPos.Distance(sPos);
                    Vec2 dir = sPos - playerMapPos;
                    string yon = GetDirectionName(dir);
                    allTowns.Add((s, d, yon));
                }

                allTowns.Sort((a, b) => a.dist.CompareTo(b.dist));

                var playerFaction = Hero.MainHero?.MapFaction;
                var selectedTowns = new List<(Settlement s, float dist, string yonStr)>();
                bool foreignAdded = false;

                // 1) Ã–nce Ã¶nceki listede OLMAYAN en yakÄ±n 5 ÅŸehir (sirkÃ¼lasyon)
                int freshCount = 0;
                foreach (var t in allTowns)
                {
                    if (freshCount >= 5) break;
                    if (_previouslyShownSettlements.Contains(t.s.StringId)) continue;
                    selectedTowns.Add(t);
                    freshCount++;
                }

                // 2) EÄŸer 5'ten az bulunduysa, Ã¶ncekilerden de ekle (fallback)
                if (selectedTowns.Count < 5)
                {
                    foreach (var t in allTowns)
                    {
                        if (selectedTowns.Count >= 5) break;
                        if (selectedTowns.Any(x => x.s.StringId == t.s.StringId)) continue;
                        selectedTowns.Add(t);
                    }
                }

                // 3) 4 ek yakÄ±n ÅŸehir (Ã¶ncekilerden de olabilir, toplam ~9)
                foreach (var t in allTowns)
                {
                    if (selectedTowns.Count >= 9) break;
                    if (selectedTowns.Any(x => x.s.StringId == t.s.StringId)) continue;
                    selectedTowns.Add(t);
                }

                // 4) 1 farklÄ± krallÄ±k ÅŸehri
                foreach (var t in allTowns)
                {
                    if (foreignAdded) break;
                    if (selectedTowns.Any(x => x.s.StringId == t.s.StringId)) continue;
                    if (playerFaction != null && t.s.MapFaction != null && t.s.MapFaction != playerFaction)
                    {
                        selectedTowns.Add(t);
                        foreignAdded = true;
                    }
                }

                // SirkÃ¼lasyon kaydÄ±: bu popup'taki ÅŸehirleri kaydet, sonraki popup'ta hariÃ§ tutulacak
                _previouslyShownSettlements.Clear();
                foreach (var t in selectedTowns)
                    _previouslyShownSettlements.Add(t.s.StringId);

                int count = selectedTowns.Count;
                if (count == 0) { _navigationPopupShown = false; return; }

                string nearestName = selectedTowns[0].s.Name.ToString();
                string factionName = selectedTowns[0].s.MapFaction?.Name?.ToString() ?? "";

                var vm = new CitySelectionVM();
                vm.Description = new TextObject("{=rad_101_26}ğŸ—º You are near {SETTLEMENT} - {FACTION}")
                    .SetTextVariable("SETTLEMENT", nearestName)
                    .SetTextVariable("FACTION", factionName).ToString() + "\n\n" +
                    new TextObject("{=rad_101_27}You walked {DISTANCE}m. Where would you like to navigate?")
                    .SetTextVariable("DISTANCE", (int)_totalDistanceWalked).ToString();
                
                vm.ShowList = true;
                vm.ShowInput = false;
                vm.HasCancelButton = false;
                vm.HasAcceptButton = true;
                vm.AcceptButtonText = "Onayla";

                int selectedItemIndex = -1;

                for (int i = 0; i < count; i++)
                {
                    var t = selectedTowns[i];
                    string label = t.s.Name.ToString();
                    string kingdomName = t.s.MapFaction != null ? t.s.MapFaction.Name.ToString() : "";
                    string rightText = $"{kingdomName} - {t.yonStr}, {(int)t.dist} " + new TextObject("{=rad_101_25}units").ToString();
                    
                    vm.Items.Add(new CitySelectionItemVM(i, label, rightText, false, (item) => {
                        foreach(var x in vm.Items) x.IsSelected = false;
                        item.IsSelected = true;
                        selectedItemIndex = item.Index;
                    }));
                }

                vm.OnAccept = () => {
                    if (selectedItemIndex == -1) return; // Hiçbir şey seçilmediyse işlem yapma

                    CitySelectionUIManager.Close();
                    _navigationPopupShown = false;
                    
                    var chosen = selectedTowns[selectedItemIndex];

                    Vec2 targetMapPos = new Vec2(chosen.s.Position.X, chosen.s.Position.Y);
                    Vec2 dirToTown = targetMapPos - playerMapPos;
                    float distToTarget = dirToTown.Length;
                    if (distToTarget > 0.01f) dirToTown = dirToTown * (1f / distToTarget);

                    // ★ KAMPANYA HARİTASI POZİSYONUNU GÜNCELLE ★
                    float moveAmount = Math.Min(distToTarget * 0.70f, distToTarget - 1f);
                    if (moveAmount < 1f) moveAmount = distToTarget * 0.5f; 
                    Vec2 newMapPos = playerMapPos + dirToTown * moveAmount;
                    MobileParty.MainParty.Position = new CampaignVec2(newMapPos, true);

                    // Mesafe sayacını sıfırla
                    _lastNavigationDistance = _totalDistanceWalked;

                    // Sahne merkezinden tersi yöne spawn (3D sahne)
                    Vec3 nextPos = _centerPos + new Vec3(-dirToTown.X * 50f, -dirToTown.Y * 50f, 0);
                    nextPos.z = 0;

                    HybridRealityMissionManager.NextSpawnPosition = nextPos;
                    HybridRealityMissionManager.NextSpawnDirection = dirToTown;
                    HybridRealityMissionManager.IsAutoReloading = true;

                    Utilities.EnableGlobalLoadingWindow();
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_30}Moving towards {SETTLEMENT}...")
                        .SetTextVariable("SETTLEMENT", chosen.s.Name).ToString(), Colors.Green));
                    
                    if (Mission.Current != null && Mission.Current.CurrentState == Mission.State.Continuing)
                        Mission.Current.EndMission();
                };

                vm.OnCancel = () => {
                    CitySelectionUIManager.Close();
                    _navigationPopupShown = false;
                    // Oyuncuyu itme â€” burada kalsÄ±n, 100m sonra tekrar soracak
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_101_31}You keep walking. You will be asked again in 100m.").ToString(), Colors.Yellow));
                };

                CitySelectionUIManager.Open(vm);
            }

            private void SpawnPlayerAgent()
            {
                if (Mission.Teams.IsEmpty())
                {
                    // isPlayerGeneral=true, isPlayerSergeant=false, isShadow=false
                    Mission.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, false);
                    // isPlayerGeneral=false, isPlayerSergeant=false, isShadow=false
                    Mission.Teams.Add(BattleSideEnum.Defender, 0xFFFFFFFF, 0xFFFFFFFF, null, false, false, false);
                }

                float spawnX = _centerPos.x;
                float spawnY = _centerPos.y;
                float spawnZ = 0f;

                // SavaÅŸ spawn noktalarÄ±nÄ± kullan (fizik burada saÄŸlam)
                if (Mission.Current.Scene != null)
                {
                    GameEntity spawnEntity = null;
                    try
                    {
                        spawnEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                        if (spawnEntity == null)
                            spawnEntity = Mission.Current.Scene.FindEntityWithTag("sp_battle_set");
                        if (spawnEntity == null)
                            spawnEntity = Mission.Current.Scene.FindEntityWithTag("battle_set");
                    }
                    catch { }

                    if (spawnEntity != null)
                    {
                        spawnX = spawnEntity.GlobalPosition.x;
                        spawnY = spawnEntity.GlobalPosition.y;
                    }
                    else
                    {
                        Vec3 sMin, sMax;
                        Mission.Current.Scene.GetBoundingBox(out sMin, out sMax);
                        spawnX = (sMin.x + sMax.x) * 0.5f;
                        spawnY = (sMin.y + sMax.y) * 0.5f;
                    }
                    _centerPos = new Vec3(spawnX, spawnY, 0);
                }

                // EÄŸer Ã¶nceki sahneden gelen bir geÃ§iÅŸ verisi varsa onu kullan
                if (HybridRealityMissionManager.NextSpawnPosition.HasValue)
                {
                    spawnX = HybridRealityMissionManager.NextSpawnPosition.Value.x;
                    spawnY = HybridRealityMissionManager.NextSpawnPosition.Value.y;
                    // Veriyi temizle
                    HybridRealityMissionManager.NextSpawnPosition = null;
                }

                if (Mission.Current.Scene != null)
                {
                    if (!Mission.Current.Scene.GetHeightAtPoint(new Vec2(spawnX, spawnY), BodyFlags.CommonCollisionExcludeFlags, ref spawnZ))
                    {
                        spawnZ = 10f; // Don't use 100f, just a small height
                    }
                }

                Vec3 startPos = new Vec3(spawnX, spawnY, spawnZ);
                if (Mission.Current.Scene != null)
                {
                    WorldPosition wp = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, startPos, false);
                    Vec3 navPos = wp.GetNavMeshVec3();
                    if (navPos.IsValid)
                    {
                        startPos = navPos;
                    }
                    else
                    {
                        // Navmesh dışında (deniz vs) kaldıysa merkeze geri çek
                        startPos = _centerPos; 
                    }
                }
                
                startPos.z += 0.2f; // prevent clipping, remove +5f to stop dropping from sky

                Vec2 initialDir = Vec2.Forward;
                if (HybridRealityMissionManager.NextSpawnDirection.HasValue)
                {
                    initialDir = HybridRealityMissionManager.NextSpawnDirection.Value;
                    HybridRealityMissionManager.NextSpawnDirection = null;
                }

                AgentBuildData buildData = new AgentBuildData(Hero.MainHero.CharacterObject)
                    .Team(Mission.AttackerTeam)
                    .InitialPosition(startPos)
                    .InitialDirection(initialDir)
                    .NoHorses(false)
                    .Controller(AgentControllerType.Player);

                if (Hero.MainHero.CharacterObject.HasMount())
                {
                    ItemObject horseItem = Hero.MainHero.CharacterObject.Equipment[EquipmentIndex.ArmorItemEndSlot].Item;
                    buildData.MountKey(MountCreationKey.GetRandomMountKeyString(horseItem, 12345));
                }

                Agent player = Mission.SpawnAgent(buildData);
                _lastPlayerPos = player.Position;
                player.SetWatchState(Agent.WatchState.Alarmed);

                // Sahneye giriÅŸ bilgisi: YakÄ±ndaki birlikleri listele
                try
                {
                    int nearbyCount = 0;
                    string partyNames = "";
                    foreach (var party in MobileParty.All)
                    {
                        if (party == MobileParty.MainParty || !party.IsVisible) continue;
                        float d = new Vec2(party.Position.X, party.Position.Y).Distance(
                            new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y));
                        if (d < VisualRange)
                        {
                            nearbyCount++;
                            if (nearbyCount <= 10) // Ä°lk 10'unu gÃ¶ster
                            {
                                string factionInfo = party.MapFaction != null && party.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)
                                    ? "[ENEMY]" : "";
                                partyNames += party.Name + factionInfo + ", ";
                            }
                        }
                    }
                    if (nearbyCount > 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_32}ğŸ—º {COUNT} parties in scene: {PARTIES}")
                            .SetTextVariable("COUNT", nearbyCount)
                            .SetTextVariable("PARTIES", partyNames.TrimEnd(',', ' ')).ToString(), Colors.Yellow));
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_33}ğŸ—º No parties nearby.").ToString(), Colors.Gray));
                    }
                }
                catch { }
            }

            // =============================================
            // YANINDAKI BÄ°RLÄ°KLERÄ°N SPAWNI
            // Oyuncunun roster'Ä±ndan max 15 kiÅŸi
            // =============================================
            private void SpawnPlayerPartyTroops()
            {
                if (Agent.Main == null || MobileParty.MainParty == null) return;

                try
                {
                    var roster = MobileParty.MainParty.MemberRoster;
                    if (roster == null || roster.TotalManCount <= 1) return;

                    // MÃ¼ttefik limiti geniÅŸletildi
                    int maxTroops = 150;
                    int spawned = 0;

                    for (int i = 0; i < roster.Count && spawned < maxTroops; i++)
                    {
                        var element = roster.GetElementCopyAtIndex(i);
                        if (element.Character == null) continue;
                        if (element.Character.IsPlayerCharacter) continue;

                        int amount = Math.Min(element.Number, maxTroops - spawned);
                        for (int j = 0; j < amount; j++)
                        {
                            try
                            {
                                Vec3 pos = Agent.Main.Position;
                                Vec2 backDir = -Agent.Main.LookDirection.AsVec2;
                                if (backDir.Length > 0.01f) backDir = backDir * (1f / backDir.Length);
                                else backDir = Vec2.Forward;

                                // BAÅžLANGIÃ‡ FORMASYONU: Oyuncunun görüÅŸ alanÄ±nÄ±n dÄ±ÅŸÄ±nda, daha geride spawn
                                float lateralOffset = (spawned % 4 - 1.5f) * 1.5f; 
                                float depthOffset = (spawned / 4) * 2.0f + 15.0f;    // En az 15 metre arka

                                Vec3 spawnPos = pos;
                                spawnPos.x += backDir.X * depthOffset + backDir.Y * lateralOffset;
                                spawnPos.y += backDir.Y * depthOffset - backDir.X * lateralOffset;

                                float z = 0;
                                if (Mission.Current.Scene.GetHeightAtPoint(spawnPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref z))
                                    spawnPos.z = z;
                                else
                                    spawnPos.z = pos.z;

                                // NavMesh kontrolÃ¼: Denize dÃ¼ÅŸmemeleri iÃ§in gÃ¼venli zemin
                                WorldPosition wp = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, spawnPos, false);
                                Vec3 navPos = wp.GetNavMeshVec3();
                                if (navPos.IsValid) spawnPos = navPos;

                                AgentBuildData buildData = new AgentBuildData(element.Character)
                                    .Equipment(element.Character.Equipment)
                                    .InitialPosition(spawnPos)
                                    .InitialDirection(Agent.Main.LookDirection.AsVec2)
                                    .NoHorses(false) // SÃ¼variyse sÃ¼vari olarak gelsin
                                    .Controller(AgentControllerType.AI);

                                if (element.Character.HasMount())
                                {
                                    ItemObject horseItem = element.Character.Equipment[EquipmentIndex.ArmorItemEndSlot].Item;
                                    if (horseItem != null)
                                        buildData.MountKey(MountCreationKey.GetRandomMountKeyString(horseItem, MBRandom.RandomInt()));
                                }

                                Agent troop = Mission.Current.SpawnAgent(buildData);
                                if (troop != null)
                                {
                                    troop.SetWatchState(Agent.WatchState.Patrolling);
                                    troop.WieldInitialWeapons();

                                    _playerPartyAgents.Add(troop);
                                    spawned++;
                                }
                            }
                            catch { }
                        }
                    }

                    if (spawned > 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_34}ğŸ›¡ {COUNT} soldiers are by your side!")
                            .SetTextVariable("COUNT", spawned).ToString(), Colors.Green));
                    }
                }
                catch { }
            }

            // =============================================
            // GÄ°ZLÄ° HAZÄ°NELER â€” Kervan + Korumalar
            // =============================================
            private void SpawnHiddenTreasures()
            {
                if (Agent.Main == null || Mission.Current.Scene == null) return;

                int treasureCount = 2 + MBRandom.RandomInt(2); 

                for (int i = 0; i < treasureCount; i++)
                {
                    try
                    {
                        float angle = MBRandom.RandomFloat * 6.28f;
                        float radius = 25f + MBRandom.RandomFloat * 35f;

                        Vec3 pos = Agent.Main.Position;
                        pos.x += (float)Math.Cos(angle) * radius;
                        pos.y += (float)Math.Sin(angle) * radius;

                        float z = 0;
                        if (!Mission.Current.Scene.GetHeightAtPoint(pos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref z))
                            continue;
                        pos.z = z;

                        MatrixFrame frame = MatrixFrame.Identity;
                        frame.origin = pos;

                        GameEntity caravanEntity = GameEntity.Instantiate(Mission.Current.Scene, "caravan_scattered_goods_prop", frame);

                        if (caravanEntity != null)
                        {
                            foreach (GameEntity child in caravanEntity.GetChildren())
                            {
                                string childName = child.Name.ToLower();
                                if (childName.Contains("box") || childName.Contains("barrel") || childName.Contains("crate") || childName.Contains("sack"))
                                {
                                    // PhysX-safe: Ek fizik gÃ¶vdesi EKLEME (zaten var)
                                    child.CreateAndAddScriptComponent(typeof(DestructableComponent).Name, true);
                                    DestructableComponent destructible = child.GetFirstScriptOfType<DestructableComponent>();

                                    if (destructible != null)
                                    {
                                        destructible.MaxHitPoint = 80f;
                                        destructible.HitPoint = 80f;
                                        destructible.DestroyOnAnyHit = false;
                                        destructible.ParticleEffectOnDestroy = "psys_game_ballista_destruction_smoke";
                                        destructible.SoundEffectOnDestroy = "event:/mission/combat/shield/wood_flesh";
                                        destructible.OnDestroyed += OnTreasureDestroyed;
                                    }

                                    _treasureBoxes.Add(child);
                                }
                            }
                        }

                        // --- KORUMALAR (YAÄMACILAR) ---
                        CharacterObject banditChar = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>("looter");
                        if (banditChar == null) banditChar = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>("mountain_bandits_bandit");

                        if (banditChar != null)
                        {
                            int guardCount = 2 + MBRandom.RandomInt(2); // 2-3 koruma
                            for (int k = 0; k < guardCount; k++)
                            {
                                float gAngle = MBRandom.RandomFloat * 6.28f;
                                float gRadius = 2f + MBRandom.RandomFloat * 3f;
                                Vec3 gPos = pos;
                                gPos.x += (float)Math.Cos(gAngle) * gRadius;
                                gPos.y += (float)Math.Sin(gAngle) * gRadius;

                                float gz = 0;
                                Vec2 gVec2 = new Vec2(gPos.x, gPos.y);
                                if (Mission.Current.Scene.GetHeightAtPoint(gVec2, BodyFlags.CommonCollisionExcludeFlags, ref gz)) gPos.z = gz;
                                else continue; // NavMesh yok â€” bu noktayÄ± atla

                                // NavMesh kontrolÃ¼
                                WorldPosition guardWP = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, gPos, false);
                                Vec3 guardNav = guardWP.GetNavMeshVec3();
                                if (!guardNav.IsValid) continue; // GeÃ§erli NavMesh yok

                                gPos = guardNav; // NavMesh-safe pozisyon

                                AgentBuildData guardData = new AgentBuildData(banditChar)
                                    .InitialPosition(gPos)
                                    .InitialDirection(new Vec2(pos.x - gPos.x, pos.y - gPos.y).Normalized())
                                    .NoHorses(true)
                                    .Controller(AgentControllerType.AI)
                                    .Team(Mission.Current.DefenderTeam);

                                Agent guard = Mission.Current.SpawnAgent(guardData);
                                if (guard != null)
                                {
                                    guard.SetWatchState(Agent.WatchState.Alarmed);
                                    guard.WieldInitialWeapons(); // SilahlarÄ± Ã§ek
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (_treasureBoxes.Count > 0)
                {
                    // Information removed
                }
            }

            private void OnTreasureDestroyed(DestructableComponent target, Agent attackerAgent, in MissionWeapon weapon, ScriptComponentBehavior attackerScriptComponentBehavior, int inflictedDamage)
            {
                if (attackerAgent != null && attackerAgent.IsMainAgent)
                {
                    int clanTier = Clan.PlayerClan?.Tier ?? 0;
                    int goldAmount;

                    switch (clanTier)
                    {
                        case 0: goldAmount = MBRandom.RandomInt(100, 200); break;
                        case 1: goldAmount = MBRandom.RandomInt(150, 350); break;
                        case 2: goldAmount = MBRandom.RandomInt(200, 500); break;
                        case 3: goldAmount = MBRandom.RandomInt(300, 600); break;
                        case 4: goldAmount = MBRandom.RandomInt(400, 800); break;
                        case 5: goldAmount = MBRandom.RandomInt(500, 1000); break;
                        default: goldAmount = MBRandom.RandomInt(600, 1200); break;
                    }

                    if (Hero.MainHero != null)
                    {
                        Hero.MainHero.ChangeHeroGold(goldAmount);
                        _treasuresCollected++;
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_35}ğŸ† Hidden Treasure! +{GOLD} Denars (Total: {COLLECTED}/{TOTAL})")
                            .SetTextVariable("GOLD", goldAmount)
                            .SetTextVariable("COLLECTED", _treasuresCollected)
                            .SetTextVariable("TOTAL", _treasureBoxes.Count).ToString(), Colors.Green));
                    }
                }
            }

            // =============================================
            // KEÅÄ°F XP â€” YÃ¼rÃ¼dÃ¼kÃ§e Scout Skill
            // =============================================
            private void UpdateScoutXP(float dt)
            {
                if (Agent.Main == null || Hero.MainHero == null) return;

                Vec3 currentPos = Agent.Main.Position;
                float frameDist = currentPos.Distance(_lastPlayerPos);

                if (frameDist < 5f)
                {
                    _totalDistanceWalked += frameDist;
                }

                if (_totalDistanceWalked - _lastXpAwardDistance >= XpPerDistanceUnit)
                {
                    _lastXpAwardDistance = _totalDistanceWalked;

                    try
                    {
                        SkillObject scoutingSkill = DefaultSkills.Scouting;
                        if (scoutingSkill != null)
                        {
                            Hero.MainHero.AddSkillXp(scoutingSkill, 5f);
                        }

                        int totalMeters = (int)_totalDistanceWalked;
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_36}ğŸ§­ Scouting XP! (Walked {METERS}m) â€” Scouting +5 XP")
                            .SetTextVariable("METERS", totalMeters).ToString(), Colors.Cyan));
                    }
                    catch { }
                }
            }

            // =============================================
            // BÄ°RLÄ°K KOMUTLARI & TAKÄ°P
            // =============================================
            private void UpdateAllyFollow(float dt)
            {
                _allyFollowTimer += dt;
                if (_allyFollowTimer < 2.0f) return;
                _allyFollowTimer = 0f;

                if (Agent.Main == null || !Agent.Main.IsActive()) return;

                WorldPosition targetWP = Agent.Main.GetWorldPosition();
                Vec2 targetPos = targetWP.AsVec2;

                foreach (var agent in _playerPartyAgents)
                {
                    if (agent != null && agent.IsActive() && !agent.IsRetreating())
                    {
                        // Oyuncuya 5m'den uzaksa takip et (eskiden 15m'ydi)
                        if (agent.Position.AsVec2.Distance(targetPos) > 5f)
                        {
                            agent.SetScriptedPosition(ref targetWP, false, Agent.AIScriptedFrameFlags.None);
                        }
                        else
                        {
                            agent.DisableScriptedMovement();
                        }
                    }
                }
            }

            private void CheckMerchantCaravans()
            {
                if (MobileParty.MainParty == null || Agent.Main == null) return;
                if (_settlementPopupShown || _navigationPopupShown || _merchantPopupShown) return;

                Vec2 playerMapPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);

                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == MobileParty.MainParty || !party.IsVisible) continue;
                    if (!party.IsCaravan) continue;

                    string partyId = party.StringId ?? party.Name?.ToString() ?? "";
                    if (_notifiedMerchants.Contains(partyId)) continue;

                    float dist = playerMapPos.Distance(new Vec2(party.Position.X, party.Position.Y));

                    if (dist < 30f)
                    {
                        _notifiedMerchants.Add(partyId);
                        _merchantPopupShown = true;

                        string ownerName = party.LeaderHero?.Name?.ToString() ?? "Caravan Head";
                        int partySize = party.MemberRoster.TotalManCount;

                        // Karavanlar sahnede gezmeye devam edecek ama popup ile rahatsÄ±z etmeyecekler.
                        break;
                    }
                }
            }

            // =============================================
            // YÃ–N HESAPLAMA
            // =============================================
            private string GetDirectionName(Vec2 dir)
            {
                if (dir.Length < 0.01f) return "";
                dir = dir * (1f / dir.Length);
                float angle = (float)Math.Atan2(dir.X, dir.Y) * (180f / 3.14159f);
                if (angle < 0) angle += 360f;
                if (angle >= 337.5f || angle < 22.5f) return new TextObject("{=rad_101_38}North").ToString();
                if (angle < 67.5f) return new TextObject("{=rad_101_39}Northeast").ToString();
                if (angle < 112.5f) return new TextObject("{=rad_101_40}East").ToString();
                if (angle < 157.5f) return new TextObject("{=rad_101_41}Southeast").ToString();
                if (angle < 202.5f) return new TextObject("{=rad_101_42}South").ToString();
                if (angle < 247.5f) return new TextObject("{=rad_101_43}Southwest").ToString();
                if (angle < 292.5f) return new TextObject("{=rad_101_44}West").ToString();
                return new TextObject("{=rad_101_45}Northwest").ToString();
            }

            // =============================================
            // SAHNEDE TÃœCCAR NPC (%20 ÅŸansla spawn)
            // =============================================
            private void SpawnSceneMerchant()
            {
                if (Agent.Main == null || Mission.Current.Scene == null) return;

                try
                {
                    // TÃ¼ccar karakteri bul
                    CharacterObject merchantChar = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>("townsman");
                    if (merchantChar == null) merchantChar = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>("villager");
                    if (merchantChar == null) return;

                    // Oyuncudan 15-25m uzaÄŸa spawn
                    float angle = MBRandom.RandomFloat * 6.28f;
                    float radius = 15f + MBRandom.RandomFloat * 10f;
                    Vec3 mPos = Agent.Main.Position;
                    mPos.x += (float)Math.Cos(angle) * radius;
                    mPos.y += (float)Math.Sin(angle) * radius;

                    // NavMesh kontrolÃ¼
                    WorldPosition mWP = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, mPos, false);
                    Vec3 navPos = mWP.GetNavMeshVec3();
                    if (!navPos.IsValid) return;
                    mPos = navPos;

                    // Oyuncuya baksÄ±n
                    Vec2 dirToPlayer = new Vec2(Agent.Main.Position.x - mPos.x, Agent.Main.Position.y - mPos.y);
                    if (dirToPlayer.Length > 0.01f) dirToPlayer = dirToPlayer * (1f / dirToPlayer.Length);
                    else dirToPlayer = Vec2.Forward;

                    AgentBuildData merchantData = new AgentBuildData(merchantChar)
                        .Team(Mission.Current.PlayerTeam)
                        .InitialPosition(mPos)
                        .InitialDirection(dirToPlayer)
                        .NoHorses(true)
                        .Controller(AgentControllerType.AI);

                    _sceneMerchantAgent = Mission.Current.SpawnAgent(merchantData);
                    if (_sceneMerchantAgent != null)
                    {
                        _sceneMerchantAgent.SetWatchState(Agent.WatchState.Patrolling);
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_101_37}ğŸª There is a traveling merchant nearby! Approach them to trade.").ToString(), Colors.Cyan));
                    }
                }
                catch { }
            }

            private void CheckSceneMerchantInteraction()
            {
                if (_sceneMerchantAgent == null || !_sceneMerchantAgent.IsActive()) return;
                if (Agent.Main == null) return;
                if (_settlementPopupShown || _navigationPopupShown || _merchantPopupShown) return;
                if (_merchantInteractionCooldown > 0f) { _merchantInteractionCooldown -= 0.3f; return; }

                float dist = Agent.Main.Position.Distance(_sceneMerchantAgent.Position);
                if (dist < 5f)
                {
                    _merchantPopupShown = true;

                    _merchantPopupShown = true;
                    // Sessizce tÃ¼ccarÄ± gÃ¶zardÄ± et, sahne iÃ§i diyaloglarÄ± de devredÄ±ÅŸÄ± bÄ±rak.
                    _merchantInteractionCooldown = 30f;
                    _merchantPopupShown = false;
                }
            }
        }
    }

    // Custom MissionBehavior to spawn the player character manually
    public class SimplePlayerSpawner : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            if (Agent.Main == null && Mission.Current.CurrentState == Mission.State.Continuing)
            {
                SpawnPlayer();
            }
        }

        private void SpawnPlayer()
        {
            // Find a valid spawn point (e.g., "sp_player" often used in scenes)
            GameEntity spawnEnt = Mission.Scene.FindEntityWithTag("sp_player") ?? Mission.Scene.FindEntityWithTag("spawnpoint_player");
            MatrixFrame spawnFrame = MatrixFrame.Identity;

            if (spawnEnt != null)
            {
                spawnFrame = spawnEnt.GetGlobalFrame();
            }
            else
            {
                // Fallback: Use center of map or height at (0,0)
                float z = 0;
                if (Mission.Scene.GetHeightAtPoint(Vec2.Zero, BodyFlags.CommonCollisionExcludeFlags, ref z))
                {
                    spawnFrame.origin = new Vec3(0, 0, z + 1f);
                }
                else
                {
                    // Absolute last resort
                    spawnFrame.origin = new Vec3(100f, 100f, 10f); // Hope for the best
                }
            }

            // Validate Logic manually
            if (float.IsNaN(spawnFrame.origin.x) || float.IsNaN(spawnFrame.origin.y))
            {
                spawnFrame.origin = new Vec3(100, 100, 10);
            }

            // Build Agent Data
            CharacterObject playerChar = CharacterObject.PlayerCharacter;
            AgentBuildData agentBuildData = new AgentBuildData(playerChar)
                .Team(Mission.PlayerTeam)
                .InitialPosition(spawnFrame.origin);

            Vec2 vec = spawnFrame.rotation.f.AsVec2;
            vec.Normalize();
            agentBuildData.InitialDirection(vec);
            // agentBuildData.Controller(TaleWorlds.MountAndBlade.Agent.ControllerType.Player);

            Mission.SpawnAgent(agentBuildData);
        }
    }


    // Custom MissionBehavior to open the settlement menu on mission exit
    public class OpenSettlementMenuOnExitBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        protected override void OnEndMission()
        {
            try
            {
                if (PlayerEncounter.Current != null && Settlement.CurrentSettlement != null)
                {
                    string menuId = "town";
                    if (Settlement.CurrentSettlement.IsVillage) menuId = "village";
                    else if (Settlement.CurrentSettlement.IsCastle) menuId = "town_guard";

                    try
                    {
                        Campaign.Current.GameMenuManager.SetNextMenu(menuId);
                    }
                    catch (System.Exception)
                    {
                        // Menu ID invalid, ignore
                    }
                }
                else if (PlayerEncounter.Current != null)
                {
                    // No settlement context â€” we must clean up the encounter
                    // to prevent stale MapEvent with null MapFaction from crashing
                    try
                    {
                        PlayerEncounter.Finish(true);
                    }
                    catch (System.Exception)
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch (System.Exception)
            {
                // Safety net - never let OnEndMission crash
            }
        }
    }

    // =============================================
    // CANLI PUSULA (LIVE COMPASS) SÄ°STEMÄ°
    // =============================================
    public class LiveCompassVM : TaleWorlds.Library.ViewModel
    {
        private string _compassText;

        [TaleWorlds.Library.DataSourceProperty]
        public string CompassText
        {
            get => _compassText;
            set
            {
                if (value != _compassText)
                {
                    _compassText = value;
                    OnPropertyChangedWithValue(value, "CompassText");
                }
            }
        }

        public LiveCompassVM()
        {
            _compassText = "";
        }
    }

    public class LiveCompassMissionView : TaleWorlds.MountAndBlade.View.MissionViews.MissionView
    {
        private TaleWorlds.Engine.GauntletUI.GauntletLayer _gauntletLayer;
        private LiveCompassVM _dataSource;
        private float _tickTimer = 0f;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _dataSource = new LiveCompassVM();
            _gauntletLayer = new TaleWorlds.Engine.GauntletUI.GauntletLayer("LiveCompassUI", 100);
            _gauntletLayer.LoadMovie("LiveCompassUI", _dataSource);
            MissionScreen.AddLayer(_gauntletLayer);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            _tickTimer += dt;
            if (_tickTimer > 1.0f) // Saniyede bir gÃ¼ncelle
            {
                _tickTimer = 0f;
                UpdateCompass();
            }
        }

        private void UpdateCompass()
        {
            if (Agent.Main == null || MobileParty.MainParty == null || Mission.Current == null)
            {
                _dataSource.CompassText = "";
                return;
            }

            Vec2 lookDir = Agent.Main.LookDirection.AsVec2;
            if (lookDir.LengthSquared < 0.01f) return;
            lookDir.Normalize();

            Vec2 playerMapPos = new Vec2(MobileParty.MainParty.Position.X, MobileParty.MainParty.Position.Y);
            Settlement closestTown = null;
            float maxDot = 0.85f; // En azÄ±ndan o yÃ¶ne doÄŸru bakmalÄ± (~31 derece)
            float closestDist = float.MaxValue;

            foreach (var settlement in Settlement.All)
            {
                if ((!settlement.IsTown && !settlement.IsCastle) || !settlement.IsVisible) continue;
                Vec2 townPos = new Vec2(settlement.Position.X, settlement.Position.Y);
                Vec2 dirToTown = townPos - playerMapPos;
                float dist = dirToTown.Length;

                if (dist > 0.01f)
                {
                    dirToTown.Normalize();
                    float dot = lookDir.x * dirToTown.x + lookDir.y * dirToTown.y;
                    
                    if (dot > maxDot && dist < closestDist && dist < 100f) // Maks 100 map birimi
                    {
                        maxDot = dot;
                        closestDist = dist;
                        closestTown = settlement;
                    }
                }
            }

            if (closestTown != null)
            {
                _dataSource.CompassText = "Heading towards: " + closestTown.Name;
            }
            else
            {
                _dataSource.CompassText = "";
            }
        }

        public override void OnMissionScreenFinalize()
        {
            if (_gauntletLayer != null)
            {
                MissionScreen.RemoveLayer(_gauntletLayer);
                _gauntletLayer = null;
            }
            if (_dataSource != null)
            {
                _dataSource.OnFinalize();
                _dataSource = null;
            }
            base.OnMissionScreenFinalize();
        }
    }
}
