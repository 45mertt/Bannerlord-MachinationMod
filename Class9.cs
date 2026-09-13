using HarmonyLib;
using RebellionsAndDemographics;
using RebellionsAndDemographics.Views;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using ClassLibrary22; // Cheat kodlarını (PendraicPreparationBehavior vb.) tanıması için gerekli

namespace RebellionsAndDemographics
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                var harmony = new Harmony("com.rebellions.and.demographics");
                harmony.PatchAll();
                RebellionsAndDemographics.Diagnostics.SaveCrashLogger.ApplyManualPatches(harmony);
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MOD_HARMONY_ERROR.txt");
                File.WriteAllText(logPath, "Harmony Patch Hatası:\n" + ex.ToString());
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            try
            {
                if (game.GameType is Campaign)
                {
                    CampaignGameStarter starter = (CampaignGameStarter)gameStarterObject;

                    // -------------------------------------------------------------------
                    // 0. URGENT/CRITICAL BEHAVIORS
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new MessengerEventBehavior());

                    // -------------------------------------------------------------------
                    // 0. CHEAT & SENARYO MOD�LLER� (Ctrl + Alt + X)
                    // -------------------------------------------------------------------
                    // Haritada (Campaign Map) iken �al��acak cheat kodu:
                    starter.AddBehavior(new PendraicPreparationBehavior());

                    starter.AddBehavior(new StoryModeKiller());

                    // -------------------------------------------------------------------
                    // 1. TEMEL MEKAN�KLER
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new AgendaPoolBehavior());
                    starter.AddBehavior(new EmpireSenateBehavior());
                    starter.AddBehavior(new NordSturgiaFestivalBehavior());
                    starter.AddBehavior(new VlandiaCouncilBehavior());
                    starter.AddBehavior(new AseraiDivanBehavior());
                    starter.AddBehavior(new FestivalDialogueBehavior());
                    starter.AddBehavior(new PopulationBehavior());
                    starter.AddBehavior(new PlagueBehavior());
                    starter.AddBehavior(new PlagueDialogs());
                    starter.AddBehavior(new RebellionCoreBehavior()); starter.AddBehavior(new CampaignMapStuckFixBehavior());
                    starter.AddBehavior(new SchismCultureBehavior());
                    starter.AddBehavior(new RecruitmentLimiterBehavior());
                    starter.AddBehavior(new SchismMenuBehavior());
                    starter.AddBehavior(new DiplomacyDialogBehavior());
                    starter.AddBehavior(new DemographicsBehavior());
                    starter.AddBehavior(new ShadowGarrisonBehavior());
                    starter.AddBehavior(new MainVillageMenuBehavior());

                    // -------------------------------------------------------------------
                    // 2. KONSEY & SAVAŞ ODASI
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new CouncilCampaignBehavior());
                    starter.AddBehavior(new CouncilDisputeBehavior());
                    starter.AddBehavior(new WarEmergencyBehavior());
                    starter.AddBehavior(new WarCouncilConversationBehavior());
                    starter.AddBehavior(new WarCouncilEnforcerBehavior());
                    starter.AddBehavior(new WarCouncilInputBehavior());
                    starter.AddBehavior(new FiefLimitBehavior());

                    // -------------------------------------------------------------------
                    // 3. SAHA KOMUTASI
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new FieldCommandInputBehavior());
                    starter.AddBehavior(new FieldCommandConversationBehavior());
                    starter.AddBehavior(new FieldCommandOrderManager());

                    // -------------------------------------------------------------------
                    // 4. YÖNETİM & DEDEKTİFLİK
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new GovernmentStabilityBehavior());
                    starter.AddBehavior(new IncognitoCampaignBehavior());
                    starter.AddBehavior(new WarCabinetPersuasionBehavior());
                    starter.AddBehavior(new WarCabinetDialogBehavior());

                    // -------------------------------------------------------------------
                    // 5. ŞEHİR MECLİSİ, İSTİHBARAT ve YOLSUZLUK
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new LocalCouncilCampaignBehavior());
                    starter.AddBehavior(new LocalCouncilConversationBehavior());
                    starter.AddBehavior(new IncognitoConversationBehavior());
                    starter.AddBehavior(new CorruptionCampaignBehavior());
                    starter.AddBehavior(new WorkshopCouncilJudgmentBehavior());

                    // -------------------------------------------------------------------
                    // 6. GREV SİSTEMİ
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new StrikeCampaignBehavior());
                    starter.AddBehavior(new StrikeConversationBehavior());
                    starter.AddBehavior(new PostStrikeBehavior());

                    starter.AddBehavior(new PendraicCinematicCheat());

                    // -------------------------------------------------------------------
                    // 7. İAŞE (FIRIN SİSTEMİ)
                    // -------------------------------------------------------------------
                    
                    starter.AddModel(new CustomCampaignTimeModel());

                    // Diğer yardımcılar
                    starter.AddBehavior(new VanillaQuestSilencer());
                    starter.AddBehavior(new PendraicOlekCheat());
                    starter.AddBehavior(new PendraicArenicosCheat());
                    starter.AddBehavior(new EmpireSchismCheat());
                    starter.AddBehavior(new PendraicStoryOrchestrator());
                    starter.AddBehavior(new EmpireTimelineBehavior()); // 1082 Schism Event
                    starter.AddBehavior(new ScavengerCampaignBehavior());
                    starter.AddBehavior(new RebellionsAndDemographics.HybridReality.HybridRealityInputBehavior());

                    // -------------------------------------------------------------------
                    // 8. ATÖLYE KRALLIĞI & TİCARİ MÜSADERE
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new WorkshopKingdomBehavior());
                    starter.AddBehavior(new WorkshopTradeDiplomacyBehavior());
                    starter.AddBehavior(new WorkshopMarketBehavior());
                    starter.AddBehavior(new EmbargoPersuasionBehavior());
                    starter.AddBehavior(new KingdomEmbargoBehavior());
                    starter.AddBehavior(new EmbargoDefectionBehavior());
                    starter.AddBehavior(new PeaceConferenceBehavior());
                    starter.AddBehavior(new PeaceConferenceQuestBehavior());
                    starter.AddBehavior(new WorkshopEconomyBehavior());
                    starter.AddBehavior(new WorkshopMonopolyBehavior());
                    starter.AddBehavior(new WorkshopWarPersuasionBehavior());
                    starter.AddBehavior(new WorkshopViolenceBehavior());
                    starter.AddBehavior(new WorkshopCrisisConversationBehavior());
                    starter.AddBehavior(new WorkshopSaleConversationBehavior());
                    starter.AddBehavior(new WorkshopTradeDialogueBehavior());
                    starter.AddBehavior(new MonopolyTreasonBehavior());
                    starter.AddBehavior(new CouncilVotingBehavior());

                    // -------------------------------------------------------------------
                    // 9. HİYERARŞİ, HÜRMET VE BİLGE ADAM
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new HierarchyEnforcementBehavior());
                    starter.AddBehavior(new RulerReverenceBehavior());
                    starter.AddBehavior(new CampAdvisorBehavior());
                    starter.AddBehavior(new LordIntrigueBehavior()); starter.AddBehavior(new LordLocationDialogBehavior());

                    // -------------------------------------------------------------------
                    // 10. CASTLE DYNAMICS
                    // -------------------------------------------------------------------
                    starter.AddBehavior(new CastleDynamicsBehavior());
                    starter.AddBehavior(new LoyaltyKingdomBehavior());
                    starter.AddBehavior(new SiegeCompanionBehavior());
                    starter.AddBehavior(new VillageBarracksBehavior());
                    starter.AddBehavior(new SaveDebugBehavior());
                    starter.AddBehavior(new ClassLibrary22.SaveDiagnosticBehavior());
                    starter.AddBehavior(new ClassLibrary22.FakeSaveValidator());
                    starter.AddModel(new CastleDynamicsPartySpeedModel());
                    starter.AddModel(new CastleDynamicsFoodConsumptionModel());
                    starter.AddModel(new CastleDynamicsMapVisibilityModel());
                    starter.AddModel(new CastleDynamicsSettlementTaxModel());
                    starter.AddModel(new CastleDynamicsSettlementMilitiaModel());
                    starter.AddModel(new CastleDynamicsSettlementSecurityModel());
                    starter.AddModel(new CastleDynamicsSettlementProsperityModel());
                    starter.AddModel(new SiegeCompanionEventModel());
                    starter.AddModel(new HardcoreInventoryCapacityModel());
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MOD_CRASH_LOG.txt");
                File.WriteAllText(logPath, "OnGameStart Hatası (Bir Behavior Çöküyor):\n" + ex.ToString());
            }
        }


        // -----------------------------------------------------------------------
        // CTRL + SHIFT + Z  =>  Mock Save Dump (Diagnostics)
        // -----------------------------------------------------------------------
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            if (Input.IsKeyDown(InputKey.LeftControl)
                && Input.IsKeyDown(InputKey.LeftShift)
                && Input.IsKeyPressed(InputKey.Z))
            {
                RebellionsAndDemographics.Diagnostics.SaveDumpManager.RunMockSave();
            }

            if (Input.IsKeyDown(InputKey.LeftControl)
                && Input.IsKeyDown(InputKey.LeftShift)
                && Input.IsKeyPressed(InputKey.Q))
            {
                string title = "Test Ba�ar�l�!";
                string message = "Y�ce Lordum,\n\nE�er bu ekran� okuyabiliyorsan�z, aray�z �ablonumuz ba�ar�yla entegre edilmi� demektir. Native karakter yaratma ekran�n�n t�m estetik detaylar� bu panele aktar�ld�.\n\nSava� ve bar�� mesajlar� art�k bu destans� ekranda belirecek.";
                ClassLibrary22.MessengerPopupManager.Open(title, message);
            }

            InputLogger.CheckHotkeys();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            if (Campaign.Current == null) return;

            // -----------------------------------------------------------------------
            // 1. PENDRAIC BATTLE CHEAT (Savaş/Köy İçi Diyalog)
            // -----------------------------------------------------------------------
            // Savaş (Combat) veya Sivil Kıyafet gerektiren yerler (Köy/Şehir içi) yeterli.
            // Hatalı olan 'MissionMode.None' kontrolü kaldırıldı.
            if (mission.CombatType == Mission.MissionCombatType.Combat ||
                mission.DoesMissionRequireCivilianEquipment)
            {
                // Sahnede Ctrl+Alt+X yapınca kardeşinle konuşmayı açar.
                mission.AddMissionBehavior(new PendraicBattleCheat1());
            }

            if (mission.DoesMissionRequireCivilianEquipment)
            {
                mission.AddMissionBehavior(new CastleDynamicsMissionBehavior());
            }

            // -----------------------------------------------------------------------
            // 2. PENDRAIC CUTSCENE (Diyalog Sahnesi)
            // -----------------------------------------------------------------------
            if (mission.Scene != null && mission.SceneName == "pendraic_battle")
            {
                mission.AddMissionBehavior(new PendraicCutsceneLogic());
            }

            // -----------------------------------------------------------------------
            // 3. VEBA MASKESİ MANTIĞI (İptal edildi - Artık 2D harita menüsü kullanılacak)
            // -----------------------------------------------------------------------
        }
    }
}
