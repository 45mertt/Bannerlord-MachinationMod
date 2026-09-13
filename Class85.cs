using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic; // For List<>
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements; // For Settlement
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem; // For MBObjectManager
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace RebellionsAndDemographics
{
    public class PendraicStoryOrchestrator : CampaignBehaviorBase
    {
        public enum PendraicStage
        {
            NotStarted = 0,
            BattlePending = 1,      // Savaş Bekleniyor
            NeretzesPending = 2,    // Neretzes Bekleniyor
            ArenicosPending = 3,    // Arenicos Bekleniyor
            OlekPending = 4,        // Olek Bekleniyor
            Finished = 5            // Bitti
        }
        // Bu metodu PendraicStoryOrchestrator sınıfının en altına ekle
        private void UnifyEmpire()
        {
            Kingdom empireWest = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");

            if (empireWest != null)
            {
                // 1. Krallığın adını değiştir
                empireWest.ChangeKingdomName(new TextObject("{=!}Calradic Empire"), new TextObject("{=!}Calradic Empire"), new TaleWorlds.Localization.TextObject("{=!}"));

                // 2. Lideri Arenicos yap (Eğer hayattaysa ve bizim oluşturduğumuz hero ise)
                InformationManager.DisplayMessage(new InformationMessage("UnifyEmpire: Arenicos is searching...", Colors.White));
                Hero arenicos = Hero.AllAliveHeroes.FirstOrDefault(h => !h.IsWanderer && h.Name.ToString() == "Arenicos");
                
                if (arenicos != null)
                {
                     InformationManager.DisplayMessage(new InformationMessage(
                         new TextObject("{=rad_085_02}UnifyEmpire: Existing Arenicos Found! Name: {NAME}, Clan: {CLAN}, Age: {AGE}, Status: {IS_ALIVE}")
                         .SetTextVariable("NAME", arenicos.Name)
                         .SetTextVariable("CLAN", arenicos.Clan?.Name)
                         .SetTextVariable("AGE", arenicos.Age)
                         .SetTextVariable("IS_ALIVE", arenicos.IsAlive ? 1 : 0).ToString(), Colors.Yellow));
                }

                if (arenicos == null)
                {
                    try 
                    {
                        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_03}Arenicos not found, creating...").ToString(), Colors.Yellow));

                        // A. Pendraic Klanını Oluştur
                        Clan pendraic = MBObjectManager.Instance.CreateObject<Clan>("clan_pendraic");
                        TextObject clanName = new TextObject("{=pendraic_nm}Clan Pendraic");
                        pendraic.ChangeClanName(clanName, clanName);
                        pendraic.Culture = empireWest.Culture;
                        pendraic.Banner = Banner.CreateRandomBanner();
                        pendraic.UpdateBannerColor(0xFF880000, 0xFFFFD700); // Kırmızı - Altın
                        
                        // Ansiklopedi metni ve temel değerler (Crash Engelleme)
                        // Reflection kullanarak Text değerlerini zorla dolduruyoruz
                        FieldInfo clanEncyclopediaTextField = typeof(Clan).GetField("_encyclopediaText", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (clanEncyclopediaTextField != null)
                        {
                            clanEncyclopediaTextField.SetValue(pendraic, new TextObject("{=!}The Pendraic Clan is the ruling family of the Calradic Empire, forged in the aftermath of the great battle."));
                        }
                        
                        // B. Arenicos'u Oluştur (Gerçek Değerlerle)
                        // Template Fallback Logic: Eğer LordTemplates boşsa, genel havuzdan empire lord bul.
                        CharacterObject template = empireWest.Culture.LordTemplates.FirstOrDefault();
                        if (template == null)
                        {
                            template = CharacterObject.All.FirstOrDefault(x => x.Culture != null && x.Culture.StringId == "empire" && x.Occupation == Occupation.Lord && x.IsHero);
                        }

                        if (template != null)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=rad_085_04}Using Template: {TEMPLATE}").SetTextVariable("TEMPLATE", template.StringId).ToString(), Colors.White));
                            arenicos = HeroCreator.CreateSpecialHero(template, empireWest.Settlements.FirstOrDefault(), pendraic, null, 55); // 55 Yaşında
                            // Yaş ve Görünüm Zorlaması
                            arenicos.SetBirthDay(CampaignTime.Now - CampaignTime.Years(55));
                            DynamicBodyProperties dynamicBodyProps = new DynamicBodyProperties(55f, 0.5f, 0.5f);
                            arenicos.CharacterObject.UpdatePlayerCharacterBodyProperties(new BodyProperties(dynamicBodyProps, arenicos.BodyProperties.StaticProperties), arenicos.CharacterObject.Race, arenicos.CharacterObject.IsFemale);
                            TextObject name = new TextObject("{=arenicos_nm}Arenicos");
                            arenicos.SetName(name, name);
                            
                            FieldInfo heroEncyclopediaTextField = typeof(Hero).GetField("_encyclopediaText", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (heroEncyclopediaTextField != null)
                            {
                                heroEncyclopediaTextField.SetValue(arenicos, new TextObject("{=!}Emperor Arenicos, the leader of the Calradic Empire."));
                            }
                            
                            // Özellikler (Traits)
                            arenicos.SetTraitLevel(DefaultTraits.Honor, 2);
                            arenicos.SetTraitLevel(DefaultTraits.Valor, 1);
                            arenicos.SetTraitLevel(DefaultTraits.Calculating, 2);
                            arenicos.SetTraitLevel(DefaultTraits.Commander, 2);
                            
                            // Yetenekler (Skills) - Gerçek bir İmparator gibi
                            arenicos.HeroDeveloper.SetInitialLevel(25);
                            arenicos.HeroDeveloper.AddSkillXp(DefaultSkills.Leadership, 50000); // Yüksek Liderlik
                            arenicos.HeroDeveloper.AddSkillXp(DefaultSkills.Tactics, 40000);   // İyi Taktisyen
                            arenicos.HeroDeveloper.AddSkillXp(DefaultSkills.Charm, 40000);     // Etkileyici
                            arenicos.HeroDeveloper.AddSkillXp(DefaultSkills.Steward, 45000);   // İyi Yönetici

                            // CRASH FIX: Karakterin ansiklopedide ve oyunda düzgün görünmesi için Ekstra Parametreler.
                            arenicos.ChangeState(Hero.CharacterStates.Active);

                            // Klan liderliği ve soylu statüsü
                            pendraic.SetLeader(arenicos);
                            
                            CampaignEventDispatcher.Instance.OnHeroCreated(arenicos, false);

                            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_05}Arenicos Successfully Created.").ToString(), Colors.Green));
                        }
                        else
                        {
                             InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_06}ERROR: Empire Lord Template Not Found! Arenicos COULD NOT BE CREATED.").ToString(), Colors.Red));
                        }
                        
                        // C. Pendraic Klanını Krallığa Ekle
                        if (pendraic.Kingdom != empireWest)
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(pendraic, empireWest);
                        }
                    }
                    catch (Exception ex)
                    {
                         InformationManager.DisplayMessage(new InformationMessage(
                             new TextObject("{=rad_085_07}Arenicos Creation Error: {ERROR}").SetTextVariable("ERROR", ex.Message).ToString(), Colors.Red));
                    }
                }

                if (arenicos != null && arenicos.IsAlive)
                {
                    // Arenicos'u klan lideri ve krallık lideri yap
                    if (arenicos.Clan != null)
                    {
                        if (arenicos.Clan.Kingdom != empireWest)
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(arenicos.Clan, empireWest);
                        }
                        
                        // ZORLA LİDERLİK:
                        empireWest.RulingClan = arenicos.Clan;
                        // Gerekirse reflection ile _rulingClan field'ını da force edebiliriz ama property usually works.
                        
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_085_08}Emperor Arenicos Ascended to the Throne! (Current Ruler: {RULER})")
                            .SetTextVariable("RULER", empireWest.RulingClan.Name).ToString(), Colors.Yellow));
                    }
                }
                else
                {
                     InformationManager.DisplayMessage(new InformationMessage(
                         new TextObject("{=rad_085_09}UnifyEmpire: Failed to make Arenicos Ruler. Arenicos exists: {EXISTS}")
                         .SetTextVariable("EXISTS", arenicos != null ? 1 : 0).ToString(), Colors.Red));
                }

                // 3. Diğer İmparatorluk klanlarını topla (Kuzey ve Güney)
                // ÖNCE LISTELERI TEMIZLEMEK GEREKIR (Eğer daha önce unite edildiyse temizle)
                if (_originalNorthClans == null) _originalNorthClans = new List<string>();
                if (_originalSouthClans == null) _originalSouthClans = new List<string>();
                if (_originalWestClans == null) _originalWestClans = new List<string>();
                
                _originalNorthClans.Clear();
                _originalSouthClans.Clear();
                _originalWestClans.Clear();

                // Tüm klanları kontrol etmeden önce mevcut imparatorlukları bul
                Kingdom empireN = Kingdom.All.FirstOrDefault(k => k.StringId == "empire");
                Kingdom empireS = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_s");
                
                // Mevcut Batı klanlarını da kaydetmek gerekir mi? Evet, çünkü onları da birleştiriyoruz ve sonra ayıracağız.
                // Batı zaten empireWest, ama loop içinde handle edebiliriz.

                foreach (Clan clan in Clan.All.ToList())
                {
                    // İmparatorluk kültüründeyse, minör fraksiyon değilse ve zaten bizim krallıkta değilse (veya bizim krallıktaysa da kaydetmeliyiz eğer split edilecekse)
                    // Ancak UnifyEmpire mantığı "collect others" üzerine kurulu.
                    
                    if (clan.Culture.StringId == "empire" && !clan.IsMinorFaction)
                    {
                        // 1. KAYIT AL
                        if (clan.Kingdom != null)
                        {
                            if (clan.Kingdom.StringId == "empire")
                            {
                                _originalNorthClans.Add(clan.StringId);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_085_10}DEBUG: {CLAN_NAME} ({CLAN_ID}) -> Added to North List")
                                    .SetTextVariable("CLAN_NAME", clan.Name)
                                    .SetTextVariable("CLAN_ID", clan.StringId).ToString(), Colors.Gray));
                            }
                            else if (clan.Kingdom.StringId == "empire_s")
                            {
                                _originalSouthClans.Add(clan.StringId);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_085_11}DEBUG: {CLAN_NAME} ({CLAN_ID}) -> Added to South List")
                                    .SetTextVariable("CLAN_NAME", clan.Name)
                                    .SetTextVariable("CLAN_ID", clan.StringId).ToString(), Colors.Gray));
                            }
                            else if (clan.Kingdom == empireWest) // empire_w
                            {
                                _originalWestClans.Add(clan.StringId);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_085_12}DEBUG: {CLAN_NAME} ({CLAN_ID}) -> Added to West List")
                                    .SetTextVariable("CLAN_NAME", clan.Name)
                                    .SetTextVariable("CLAN_ID", clan.StringId).ToString(), Colors.Gray));
                            }
                        }

                        // 2. TAŞI (Eğer zaten West'te değilse)
                        if (clan.MapFaction != empireWest && clan.Kingdom != null)
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(clan, empireWest);
                        }
                    }
                }

                // 4. Kuzey ve Güney İmparatorluklarını Yok Et (1082'de geri gelecekler)
                
                if (empireN != null) 
                {
                    // Debug: Kuzey İmparatorluğu Durumu
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_085_13}Northern Empire Found. Clan Count: {COUNT}")
                        .SetTextVariable("COUNT", empireN.Clans.Count).ToString(), Colors.Yellow));
                    DestroyKingdomAction.Apply(empireN);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_14}Northern Empire Destroyed.").ToString(), Colors.Green));
                }
                else
                {
                     InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_15}ERROR: Northern Empire (empire) Not Found!").ToString(), Colors.Red));
                     // Debug: Mevcut Krallıkları Listele
                     string kingdoms = string.Join(", ", Kingdom.All.Select(k => $"{k.Name} ({k.StringId})"));
                     InformationManager.DisplayMessage(new InformationMessage(
                         new TextObject("{=rad_085_16}Existing Kingdoms: {KINGDOMS}")
                         .SetTextVariable("KINGDOMS", kingdoms).ToString(), Colors.White));
                }

                if (empireS != null) 
                {
                    DestroyKingdomAction.Apply(empireS);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_17}Southern Empire Destroyed.").ToString(), Colors.Green));
                }

                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_18}The Empire United After the Battle!").ToString(), Colors.Red));
            }
        }

        private PendraicStage _currentStage = PendraicStage.NotStarted;
        private bool _triggerPendingConversation = false;
        private bool _waitingForMissionToEnd = false;
        private bool _introTriggered = false;

        public static Hero SharedNeretzesHero { get; set; }
        
        private bool _isStoryModeActive = false; // Restore this
        private bool _startStoryMode = false;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick); // Add DailyTick
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        private List<string> _originalNorthClans = new List<string>();
        private List<string> _originalSouthClans = new List<string>();
        private List<string> _originalWestClans = new List<string>();

        public override void SyncData(IDataStore dataStore)
        {
            int stageInt = (int)_currentStage;
            dataStore.SyncData("_pendraicStoryStage", ref stageInt);
            _currentStage = (PendraicStage)stageInt;
            
            dataStore.SyncData("_pendraicWaitingForMission", ref _waitingForMissionToEnd);
            dataStore.SyncData("_pendraicIntroTriggered", ref _introTriggered);
            dataStore.SyncData("_pendraicIsStoryModeActive", ref _isStoryModeActive);
            dataStore.SyncData("_pendraicStartStoryMode", ref _startStoryMode);
            dataStore.SyncData("_pendraicUnificationTriggered", ref _unificationTriggered);
            dataStore.SyncData("_pendraicSplitTriggered", ref _splitTriggered);

            dataStore.SyncData("_originalNorthClans", ref _originalNorthClans);
            dataStore.SyncData("_originalSouthClans", ref _originalSouthClans);
            dataStore.SyncData("_originalWestClans", ref _originalWestClans);
        }

        private bool _unificationTriggered = false;
        private bool _splitTriggered = false;

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            if (!ModSettings.EnableNeretzesFolly) return;
            // Sadece bayrağı kaldır, hemen çalıştırma (Kullanıcı İsteği: Spring 10)
            _startStoryMode = true;
        }

        private void OnDailyTick()
        {
            // Kullanıcı İsteği: İlkbahar 10'da çalışsın (UnifyEmpire - Oyun Başlangıcı)
            if (_startStoryMode && !_unificationTriggered && CampaignTime.Now.GetDayOfSeason >= 7)
            {
                _unificationTriggered = true;
                InitializeStoryMode(); // Arenicos'u oluşturur ve birleştirir.
            }
            if (!ModSettings.EnableNeretzesFolly) return;

            // Kullanıcı İsteği: 1082'de Ayrılma (Force Split)
            if (!_splitTriggered && CampaignTime.Now.GetYear >= 1082)
            {
                _splitTriggered = true;

                // Mid-game save kontrolü: Eğer oyun 1082'yi geçmiş bir kayıtsa (ör. 1084)
                // ya da hikaye moduyla birleşme (UnifyEmpire) yaşanmadıysa, bölme işlemini atla.
                if (CampaignTime.Now.GetYear > 1082 || !_unificationTriggered)
                {
                    return; // Bölünmeyi atla
                }

                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_19}The Year 1082 Has Arrived: The Empire is Splitting...").ToString(), Colors.Red));
                ForceEmpireSplitAndWars();
            }

            // 4. MANUEL TETİKLEME (CTRL + SHIFT + V) - Kullanıcı İsteği
            if (TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl) &&
                TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftShift) &&
                TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.V))
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_20}Manual Split and Diplomacy Scenario Triggered...").ToString(), Colors.Cyan));
                ForceEmpireSplitAndWars();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (!ModSettings.EnableNeretzesFolly) return;
            // DİYALOGLARI KAYDET
            PendraicBattleLogic.SetupBattleDialogs();
            ArenicosCutsceneLogic.SetupArenicosDialogs();
            OlekCutsceneLogic.SetupOlekDialogs();
            PendraicNeretzesDeathLogic.SetupNeretzesDialogs();
            
        }


        private void InitializeStoryMode()
        {
            // 1. Önce UnifyEmpire metodunu güvenli şekilde çalıştır
            try 
            {
                UnifyEmpire();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_085_21}UnifyEmpire Error: {ERROR}").SetTextVariable("ERROR", ex.Message).ToString(), Colors.Red));
            }

            try
            {
                // Zaten var mı kontrolü (Hem ID hem de isim olarak kontrol et)
                if (Kingdom.All.Any(k => k.StringId == "calradic_empire" || k.Name.ToString() == "Calradic Empire")) 
                {
                    // UnifyEmpire başarılı olduysa veya daha önce çalıştıysa buradan çık.
                    // Aşağıdaki "Creating a New Kingdom" kodları çalışırsa çakışma olur.
                    return; 
                }

                Kingdom empireW = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
                Kingdom empireN = Kingdom.All.FirstOrDefault(k => k.StringId == "empire");
                Kingdom empireS = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_s");

                // 1. YENİ KRALLIK OLUŞTUR: CALRADIC EMPIRE
                // ILSpy referansına göre MBObjectManager.CreateObject<Kingdom> kullanılmalı.
                Kingdom calradia = MBObjectManager.Instance.CreateObject<Kingdom>("calradic_empire");
                
                TaleWorlds.Localization.TextObject newName = new TaleWorlds.Localization.TextObject("Calradic Empire");
                TaleWorlds.Localization.TextObject title = new TaleWorlds.Localization.TextObject("Empire");
                
                // Renkler: Koyu Kırmızı - Altın
                uint calradiaRed = 0xFF880000;
                uint calradiaGold = 0xFFFFD700;

                // Kültür ve Başkent (Safe Check)
                CultureObject empireCulture = empireW?.Culture ?? empireN?.Culture ?? empireS?.Culture;
                Settlement capital = empireW?.Settlements.FirstOrDefault() ?? empireN?.Settlements.FirstOrDefault() ?? empireS?.Settlements.FirstOrDefault();
                
                // Fallback: Eğer yerleşim yoksa, kültürden rastgele bir yer seç (Nadir durum)
                if (capital == null && empireCulture != null) 
                    capital = Settlement.All.FirstOrDefault(s => s.Culture == empireCulture && s.IsTown);

                if (capital == null)
                {
                     // Hala null ise (kültür de bulunamadıysa), rastgele bir town al
                     capital = Settlement.All.FirstOrDefault(s => s.IsTown);
                }

                if (empireCulture == null && capital != null)
                   empireCulture = capital.Culture;


                Banner banner = empireW?.Banner != null ? new Banner(empireW.Banner.Serialize()) : Banner.CreateRandomBanner();
                banner.ChangePrimaryColor(calradiaRed);
                banner.ChangeIconColors(calradiaGold);

                // InitializeKingdom - ILSpy parametrelerine uygun
                calradia.InitializeKingdom(
                    newName,            // name
                    newName,            // informalName
                    empireCulture,      // culture
                    banner,             // banner
                    calradiaRed,        // color1
                    calradiaGold,       // color2
                    capital,            // initialHomeSettlement
                    newName,            // encyclopediaText
                    newName,            // encyclopediaTitle
                    title               // encyclopediaRulerTitle
                );

                // 2. LİDER: ARENICOS
                Hero arenicos = Hero.AllAliveHeroes.FirstOrDefault(h => !h.IsWanderer && h.Name.ToString() == "Arenicos");

                // CRASH & DUPLICATION FIX: Burada ikinci kez Pendraic klanını veya Arenicos'u ASLA YARATMIYORUZ.
                // UnifyEmpire zaten bu işi yaptı. O yüzden if (arenicos == null) bloğunu TEMİZLİYORUZ.
                
                if (arenicos != null)
                {
                    if (arenicos.Clan.Kingdom != calradia)
                    {
                        ChangeKingdomAction.ApplyByJoinToKingdom(arenicos.Clan, calradia);
                    }
                    calradia.RulingClan = arenicos.Clan;
                }

                // 3. TÜM KLANLARI TAŞI (Kuzey + Güney + BATI)
                // DestroyKingdomAction klanları yok ediyor, bu yüzden önce HEPSİNİ taşımalıyız.
                List<Kingdom> targets = new List<Kingdom>();
                if (empireN != null) targets.Add(empireN);
                if (empireS != null) targets.Add(empireS);
                if (empireW != null) targets.Add(empireW); 
                
                // Listenin boş olma ihtimaline karşı check
                foreach (var oldEmpire in targets)
                {
                    if (oldEmpire == null) continue;

                    // Liste kopyası üzerinde dönüyoruz (Modify sırasında hata almamak için)
                    foreach (Clan c in oldEmpire.Clans.ToList())
                    {
                        if (c != oldEmpire.RulingClan && c.MapFaction != calradia && c.Kingdom != null)
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(c, calradia);
                        }
                    }
                    
                    // Ruling Clan en son
                    if (oldEmpire.RulingClan != null && oldEmpire.RulingClan.MapFaction != calradia)
                    {
                         ChangeKingdomAction.ApplyByJoinToKingdom(oldEmpire.RulingClan, calradia);
                    }
                }

                // Sahipsiz Empire klanlarını da topla
                foreach (Clan c in Clan.All.Where(x => x.Culture != null && x.Culture.StringId == "empire" && x.Kingdom == null && x.Leader.IsAlive).ToList())
                {
                     ChangeKingdomAction.ApplyByJoinToKingdom(c, calradia);
                }

                // 4. ESKİ KRALLIKLARI YOK ET
                // Artık içleri boş olmalı, güvenle yok edebiliriz.
                if (empireN != null) DestroyKingdomAction.Apply(empireN);
                if (empireS != null) DestroyKingdomAction.Apply(empireS);
                if (empireW != null) DestroyKingdomAction.Apply(empireW);


                // 5. RENKLERİ GÜNCELLE
                foreach (var clan in calradia.Clans)
                {
                    clan.UpdateBannerColor(calradiaRed, calradiaGold);
                    if (clan.Banner != null)
                    {
                        clan.Banner.ChangePrimaryColor(calradiaRed);
                        clan.Banner.ChangeIconColors(calradiaGold);
                         clan.Banner.ChangeBackgroundColor(calradiaRed, calradiaRed);
                    }
                }

                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_22}1077: Calradia Under One Banner!").ToString(), Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_085_23}Init Error: {ERROR}").SetTextVariable("ERROR", ex.Message).ToString(), Colors.Red));
            }
        }



        private void OnTick(float dt)
        {
            if (!ModSettings.EnableNeretzesFolly) return;
            // Mid-game save kontrolü: Sadece 1077 yılında çalışsın, aksi takdirde bitir
            if (CampaignTime.Now.GetYear != 1077 && _currentStage != PendraicStage.Finished)
            {
                _introTriggered = true;
                _currentStage = PendraicStage.Finished;
            }

            // 1. BAŞLANGIÇ (INTRO)
            if (!_introTriggered && _currentStage == PendraicStage.NotStarted)
            {
                if (Campaign.Current.ConversationManager.OneToOneConversationCharacter == null &&
                    Campaign.Current.CurrentMenuContext == null &&
                    MobileParty.MainParty.MapEvent == null)
                {
                    _introTriggered = true;
                    _currentStage = PendraicStage.BattlePending;

                    InformationManager.ShowInquiry(new InquiryData(
                        new TextObject("{=rad_085_24}Shadows of the Past").ToString(),
                        new TextObject("{=rad_085_25}You spoke with your sibling and set off... But an old memory in your mind takes you to the Battle of Pendraic. Do you want to remember that day?").ToString(),
                        true, false, new TextObject("{=rad_085_26}Remember (Part 1: The Battle)").ToString(), "",
                        () =>
                        {
                            PendraicBattleLogic.OpenBattleScene();
                            _waitingForMissionToEnd = true;
                        },
                        null
                    ), true);
                }
            }

            // 2. GEÇİŞ KONTROLÜ (FIXED - ARTIK ATLAMAZ)
            if (_waitingForMissionToEnd)
            {
                // DÜZELTME: Sadece Mission.Current kontrolü yetmez, oyunun durumuna (GameState) bakmalıyız.
                // Eğer oyun "MissionState" içindeyse (yani yükleniyor veya oynanıyorsa) KESİNLİKLE bekle.
                if (GameStateManager.Current.ActiveState is MissionState)
                {
                    return;
                }

                // Eğer hala menüdeysek veya harita olayı (savaş vb.) varsa bekle
                if (Campaign.Current.CurrentMenuContext != null || MobileParty.MainParty.MapEvent != null)
                {
                    return;
                }

                // Buraya geldiysek: MissionState değiliz, Menüde değiliz, Savaşta değiliz.
                // Demek ki sahne bitti ve sağ salim Haritaya (MapState) döndük.

                _waitingForMissionToEnd = false;

                // --- ZİNCİRLEME REAKSİYON ---

                // A. SAVAŞ BİTTİ -> NERETZES'E GEÇ
                if (_currentStage == PendraicStage.BattlePending)
                {
                    _currentStage = PendraicStage.NeretzesPending;

                    InformationManager.ShowInquiry(new InquiryData(
                        new TextObject("{=rad_085_27}Fall of the Emperor").ToString(),
                        new TextObject("{=rad_085_28}Things went wrong on the battlefield. You saw Neretzes's banner fall. You are about to relive that moment...").ToString(),
                        true, false, new TextObject("{=rad_085_29}Remember that moment").ToString(), "",
                        () =>
                        {
                            PendraicNeretzesDeathLogic.OpenNeretzesScene();
                            _waitingForMissionToEnd = true;
                        },
                        null
                    ), true);
                }

                // B. NERETZES BİTTİ -> ARENICOS'A GEÇ
                else if (_currentStage == PendraicStage.NeretzesPending)
                {
                    _currentStage = PendraicStage.ArenicosPending;

                    InformationManager.ShowInquiry(new InquiryData(
                        new TextObject("{=rad_085_30}Chaos of Battle").ToString(),
                        new TextObject("{=rad_085_31}The Emperor died, the lines broke. But the chaos of that day didn't end there. Do you remember Arenicos retreating his army?").ToString(),
                        true, false, new TextObject("{=rad_085_32}Continue (Part 2: Escape)").ToString(), "",
                        () =>
                        {
                            ArenicosCutsceneLogic.OpenArenicosScene();
                            _waitingForMissionToEnd = true;
                        },
                        null
                    ), true);
                }

                // C. ARENICOS BİTTİ -> OLEK'E GEÇ
                else if (_currentStage == PendraicStage.ArenicosPending)
                {
                    _currentStage = PendraicStage.OlekPending;

                    InformationManager.ShowInquiry(new InquiryData(
                        new TextObject("{=rad_085_33}The North's Betrayal").ToString(),
                        new TextObject("{=rad_085_34}Arenicos managed to escape. But another tragedy was unfolding on the northern front. The fate of the banner between Olek and Raganvad...").ToString(),
                        true, false, new TextObject("{=rad_085_35}Final Part (Part 3: Betrayal)").ToString(), "",
                        () =>
                        {
                            OlekCutsceneLogic.OpenOlekScene();
                            _waitingForMissionToEnd = true;
                        },
                        null
                    ), true);
                }

                // D. OLEK BİTTİ -> FİNAL
                else if (_currentStage == PendraicStage.OlekPending)
                {
                    _currentStage = PendraicStage.Finished;

                    InformationManager.ShowInquiry(new InquiryData(
                        new TextObject("{=rad_085_36}The Memory Ends").ToString(),
                        new TextObject("{=rad_085_37}You opened your eyes. The roar of the battlefield gave way to the wind of Calradia. You remembered the past, now is the time to shape the future.").ToString(),
                        true, false, new TextObject("{=rad_085_38}I've gathered myself").ToString(), "",
                        () =>
                        {
                            // Konuşma iptal edildi, sessizce kapat
                        },
                        null
                    ), true);
                }
            }

            // 3. KARDEŞLE KONUŞMA (İPTAL EDİLDİ)

            // 4. MANUEL TETİKLEME (CTRL + SHIFT + V) - Kullanıcı İsteği (GÜNCELLENDİ: BÖLÜNME VE SAVAŞ)
            if (TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl) &&
                TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftShift) &&
                TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.V))
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_20}Manual Split and Diplomacy Scenario Triggered...").ToString(), Colors.Cyan));
                ForceEmpireSplitAndWars();
            }
        }

        private void ForceEmpireSplitAndWars()
        {
            try
            {
                Kingdom west = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w"); // "Calradic Empire" olmuştu, ama StringId değişmez genellikle. ChangeKingdomName sadece ismi değiştirir.
                // UYARI: Eğer UnifyEmpire StringId'yi DEĞİŞTİRMEDİYSE "empire_w" hala duruyor.
                // Eğer StringId değişmediyse, "calradic_empire" aramak yanlış olabilir.
                // UnifyEmpire koduna baktık: empireWest.ChangeKingdomName(...) kullanıyor. StringId değişmez.
                // Bu yüzden "west" hala "empire_w" ID'sine sahip ve adı "Calradic Empire".

                Kingdom north = Kingdom.All.FirstOrDefault(k => k.StringId == "empire");
                Kingdom south = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_s");
                
                Kingdom vlandia = Kingdom.All.FirstOrDefault(k => k.StringId == "vlandia");
                Kingdom sturgia = Kingdom.All.FirstOrDefault(k => k.StringId == "sturgia");
                Kingdom aserai = Kingdom.All.FirstOrDefault(k => k.StringId == "aserai");
                Kingdom khuzait = Kingdom.All.FirstOrDefault(k => k.StringId == "khuzait");
                Kingdom battania = Kingdom.All.FirstOrDefault(k => k.StringId == "battania");

                // 0. ARENICOS SUİKASTI VE GARİOS'UN BAŞA GEÇMESİ
                Hero arenicos = Hero.AllAliveHeroes.FirstOrDefault(h => !h.IsWanderer && h.Name.ToString() == "Arenicos");
                if (arenicos != null)
                {
                    KillCharacterAction.ApplyByMurder(arenicos, null, true);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_39}BREAKING NEWS: Emperor Arenicos was assassinated! The Empire is fracturing!").ToString(), Colors.Red));
                }

                if (west != null)
                {
                    Hero garios = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Garios"));
                    if (garios != null && garios.Clan != null)
                    {
                        if (garios.Clan.Kingdom != west)
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(garios.Clan, west);
                        }
                        west.RulingClan = garios.Clan;
                        west.RulingClan.SetLeader(garios);
                        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_40}Garios has seized control of the Western Empire!").ToString(), Colors.Yellow));
                    }
                }

                // 1. İMPARATORLUKLARI DİRİLT (Gerekirse)
                ResurrectKingdom(north);
                ResurrectKingdom(south);
                ResurrectKingdom(west); // West zaten hayatta ama olsun

                // 2. KLANLARI GERİ GÖNDER
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_41}--- Clans Are Returning Home ---").ToString(), Colors.Cyan));
                
                // West (Calradic Empire) içindeki klanları alalım
                // Eğer UnifyEmpire'da sadece empire_w kullanıldıysa, tüm empire klanları orada.
                
                if (west != null)
                {
                    List<Clan> allClans = west.Clans.ToList();
                    int movedNorth = 0;
                    int movedSouth = 0;
                    int movedWest = 0; // Kalanlar

                    foreach (Clan clan in allClans)
                    {
                        if (clan.IsEliminated || !clan.Leader.IsAlive) continue;
                        if (clan == west.RulingClan) continue; // Şimdilik ruling clan kalsın (Arenicos)

                        bool moved = false;

                        // KUZEY KONTROLÜ
                        if (_originalNorthClans != null && _originalNorthClans.Contains(clan.StringId))
                        {
                            if (north != null && clan.Kingdom != north)
                            {
                                ChangeKingdomAction.ApplyByJoinToKingdom(clan, north);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_085_42}DEBUG: {CLAN_NAME} -> Joined North (Fiefs: {COUNT})")
                                    .SetTextVariable("CLAN_NAME", clan.Name)
                                    .SetTextVariable("COUNT", clan.Settlements.Count).ToString(), Colors.Green));
                                movedNorth++;
                                moved = true;
                            }
                        }
                        // GÜNEY KONTROLÜ
                        else if (_originalSouthClans != null && _originalSouthClans.Contains(clan.StringId))
                        {
                            if (south != null && clan.Kingdom != south)
                            {
                                ChangeKingdomAction.ApplyByJoinToKingdom(clan, south);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=rad_085_43}DEBUG: {CLAN_NAME} -> Joined South (Fiefs: {COUNT})")
                                    .SetTextVariable("CLAN_NAME", clan.Name)
                                    .SetTextVariable("COUNT", clan.Settlements.Count).ToString(), Colors.Green));
                                movedSouth++;
                                moved = true;
                            }
                        }
                        // BATI KONTROLÜ (Zaten West'teler ama explicit kontrol)
                        else if (_originalWestClans != null && _originalWestClans.Contains(clan.StringId))
                        {
                            // Zaten West'teler, bir şey yapmaya gerek yok ama loglayalım
                             InformationManager.DisplayMessage(new InformationMessage(
                                 new TextObject("{=rad_085_44}DEBUG: {CLAN_NAME} -> Stayed in West (Home)")
                                 .SetTextVariable("CLAN_NAME", clan.Name).ToString(), Colors.Gray));
                             movedWest++;
                             moved = true;
                        }

                        if (!moved)
                        {
                             InformationManager.DisplayMessage(new InformationMessage(
                                 new TextObject("{=rad_085_45}WARNING: No recorded kingdom found for {CLAN_NAME} ({CLAN_ID})! Stayed in West.")
                                 .SetTextVariable("CLAN_NAME", clan.Name)
                                 .SetTextVariable("CLAN_ID", clan.StringId).ToString(), Colors.Red));
                        }
                    }
                    
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_085_46}Transfer Report: North({NORTH}), South({SOUTH})")
                        .SetTextVariable("NORTH", movedNorth)
                        .SetTextVariable("SOUTH", movedSouth).ToString(), Colors.White));
                }

                // 3. İMPARATORLUK İÇ BARIŞI (Kendi aralarında barış)
                if (west != null && north != null) MakePeace(west, north);
                if (west != null && south != null) MakePeace(west, south);
                if (north != null && south != null) MakePeace(north, south);
                
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_47}Peace Established Between Empires (North-South-West)").ToString(), Colors.Green));

                // 4. DIŞ SAVAŞLAR
                // Batı vs Batanya
                if (west != null && battania != null) DeclareWar(west, battania);
                
                // Güney vs Aserai
                if (south != null && aserai != null) DeclareWar(south, aserai);

                // Kuzey vs Kuzait
                if (north != null && khuzait != null) DeclareWar(north, khuzait);

                // Vlandia vs Sturgia
                if (vlandia != null && sturgia != null) DeclareWar(vlandia, sturgia);

                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_48}Scenario Wars Started!").ToString(), Colors.Red));
                
                // Eğer Arenicos "empire_w" (Batı) lideriyse ve Batı aslında Unified Empire ise,
                // Şimdi Batı tekrar sadece Batı oldu. İsim düzeltmeli miyiz?
                // Kullanıcı isteğine göre: Eğer tekrar bölündülerse, Batı eski haline dönmeli mi?
                // Şimdilik sadece bölünmeyi yapalım. İsim "Calradic Empire" kalabilir veya değiştirilebilir.
                if (west != null)
                {
                     west.ChangeKingdomName(new TextObject("{=!}Western Empire"), new TextObject("{=!}Western Empire"), new TaleWorlds.Localization.TextObject("{=!}"));
                     InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_49}Western Empire Reverted to Old Name.").ToString(), Colors.Green));
                }

            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_085_50}Force Split Error: {ERROR}").SetTextVariable("ERROR", ex.Message).ToString(), Colors.Red));
            }
        }

        private void ResurrectKingdom(Kingdom k)
        {
            if (k == null) return;
            if (k.IsEliminated)
            {
                // Reflection ile _isEliminated field'ını false yap
                var field = typeof(Kingdom).GetField("_isEliminated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(k, false);
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_085_51}{KINGDOM} Reestablished!")
                        .SetTextVariable("KINGDOM", k.Name).ToString(), Colors.Green));
                }
            }
        }

        private void MakePeace(Kingdom k1, Kingdom k2)
        {
            if (FactionManager.IsAtWarAgainstFaction(k1, k2))
            {
                MakePeaceAction.Apply(k1, k2);
            }
        }

        private void DeclareWar(Kingdom k1, Kingdom k2)
        {
            if (!FactionManager.IsAtWarAgainstFaction(k1, k2))
            {
                DeclareWarAction.ApplyByDefault(k1, k2);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_085_52}Declaration of War: {KINGDOM1} vs {KINGDOM2}")
                    .SetTextVariable("KINGDOM1", k1.Name)
                    .SetTextVariable("KINGDOM2", k2.Name).ToString(), Colors.Red));
            }
        }
    }
}
