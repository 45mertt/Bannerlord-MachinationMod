using TaleWorlds.SaveSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using SandBox.View;
using TaleWorlds.MountAndBlade.View;

namespace ClassLibrary22
{
    public class PeaceConferenceBehavior : CampaignBehaviorBase
    {
        public static PeaceConferenceBehavior Instance { get; private set; }
        public static bool IsConferencePeace = false;

        private float _currentTension = 0f;
        private int _requestedGold = 0;
        private int _requestedDailyTribute = 0;
        private int _enemyRequestedGold = 0;
        private int _enemyRequestedDailyTribute = 0;
        private float _ourWarScore = 0f;
        private float _enemyWarScore = 0f;
        private int _ourScorePercent = 50;
        private int _enemyScorePercent = 50;
        private int _maxTazminatCap = 0;
        private float _npcTensionLimit = 100f;
        private bool _ultimatumGiven = false;
        private Settlement _requestedFief = null;
        private bool _inConference = false;

        // Ateskes ve Haberci sistemi
        private Dictionary<Kingdom, CampaignTime> _conferenceDeadlines = new Dictionary<Kingdom, CampaignTime>();
        public Dictionary<Kingdom, CampaignTime> ConferenceDeadlines { get { return _conferenceDeadlines ?? (_conferenceDeadlines = new Dictionary<Kingdom, CampaignTime>()); } set { _conferenceDeadlines = value; } }

        private Dictionary<Kingdom, Settlement> _conferenceLocations = new Dictionary<Kingdom, Settlement>();
        public Dictionary<Kingdom, Settlement> ConferenceLocations { get { return _conferenceLocations ?? (_conferenceLocations = new Dictionary<Kingdom, Settlement>()); } set { _conferenceLocations = value; } }

        private Dictionary<Kingdom, CampaignTime> _conferenceCooldowns = new Dictionary<Kingdom, CampaignTime>();
        public Dictionary<Kingdom, CampaignTime> ConferenceCooldowns { get { return _conferenceCooldowns ?? (_conferenceCooldowns = new Dictionary<Kingdom, CampaignTime>()); } set { _conferenceCooldowns = value; } }

        private Dictionary<Kingdom, CampaignTime> _ceasefireStartTimes = new Dictionary<Kingdom, CampaignTime>();
        public Dictionary<Kingdom, CampaignTime> CeasefireStartTimes { get { return _ceasefireStartTimes ?? (_ceasefireStartTimes = new Dictionary<Kingdom, CampaignTime>()); } set { _ceasefireStartTimes = value; } }

        private Dictionary<Kingdom, int> _ceasefireDailyTributeWeReceive = new Dictionary<Kingdom, int>();
        public Dictionary<Kingdom, int> CeasefireDailyTributeWeReceive { get { return _ceasefireDailyTributeWeReceive ?? (_ceasefireDailyTributeWeReceive = new Dictionary<Kingdom, int>()); } set { _ceasefireDailyTributeWeReceive = value; } }

        private Dictionary<Kingdom, int> _savedOurScorePercent = new Dictionary<Kingdom, int>();
        public Dictionary<Kingdom, int> SavedOurScorePercent { get { return _savedOurScorePercent ?? (_savedOurScorePercent = new Dictionary<Kingdom, int>()); } set { _savedOurScorePercent = value; } }

        private Dictionary<Kingdom, int> _savedEnemyScorePercent = new Dictionary<Kingdom, int>();
        public Dictionary<Kingdom, int> SavedEnemyScorePercent { get { return _savedEnemyScorePercent ?? (_savedEnemyScorePercent = new Dictionary<Kingdom, int>()); } set { _savedEnemyScorePercent = value; } }

        private Dictionary<Kingdom, int> _savedMaxCap = new Dictionary<Kingdom, int>();
        public Dictionary<Kingdom, int> SavedMaxCap { get { return _savedMaxCap ?? (_savedMaxCap = new Dictionary<Kingdom, int>()); } set { _savedMaxCap = value; } }

        private Dictionary<Kingdom, int> _activeDailyTributes = new Dictionary<Kingdom, int>();
        public Dictionary<Kingdom, int> ActiveDailyTributes { get { return _activeDailyTributes ?? (_activeDailyTributes = new Dictionary<Kingdom, int>()); } set { _activeDailyTributes = value; } }
        // Pozitif: biz oduyoruz, Negatif: onlar oduyorlar

        private Kingdom _currentEnemyFaction;
        private List<Settlement> _requestedFiefs = new List<Settlement>();

        public PeaceConferenceBehavior()
        {
            Instance = this;
        }

        public void SetRequestedGoldFromLogic(int amount)
        {
            _requestedGold = Math.Min(amount, _maxTazminatCap);
            RecalculateTension();
            
            if (_requestedGold < amount)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage($"The maximum amount you can claim based on your battle score is: {_maxTazminatCap}.", TaleWorlds.Library.Colors.Red));
            }
            CheckTensionLimitWarning();
        }

        public void SetRequestedTributeFromLogic(int amount)
        {
            _requestedDailyTribute = amount;
            RecalculateTension();
            CheckTensionLimitWarning();
        }

        public void RecalculateTension()
        {
            _currentTension = 0f;
            
            float costMultiplier = 1f;
            if (_ourScorePercent < 60f)
            {
                costMultiplier = 4f; // Extremely high tension if advantage is very low (50-59%)
            }
            else if (_ourScorePercent < 75f)
            {
                costMultiplier = 2f; // High tension for moderate advantage
            }

            _currentTension += (_requestedGold / 10000f) * 5f * costMultiplier;
            _currentTension += (_requestedDailyTribute / 100f) * 3f * costMultiplier;
            
            for (int i = 0; i < _requestedFiefs.Count; i++)
            {
                _currentTension += GetFiefDemandTension(_requestedFiefs[i], i);
            }
        }

        private void CheckTensionLimitWarning()
        {
            if (_currentTension > 140f)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_21}WARNING: Your demands exceed the other party's tolerance limit! They will not accept it.").ToString(), TaleWorlds.Library.Colors.Red));
            }
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TextObject("{=rad_pc_01}Demands Updated. Tension: {TENSION}/140").SetTextVariable("TENSION", _currentTension.ToString("F0")).ToString(), TaleWorlds.Library.Colors.Yellow));
        }

        private float GetFiefDemandTension(Settlement fief, int requestIndex)
        {
            float scoreAdvantage = Math.Max(1f, _ourScorePercent - 50f); // 0 ile 50 arasi
            
            // E?er ustunlugumuz 40 ise (90'a 10), costMultiplier = 1f.
            // E?er ustunlugumuz 0 ise (50'ye 50), costMultiplier = 5f.
            float costMultiplier = 5f - (scoreAdvantage / 10f); 
            if (costMultiplier < 0.5f) costMultiplier = 0.5f;

            float baseTension = fief.IsTown ? 80f : 40f; 
            
            // Her fazladan sehir katlanarak artar.
            float stackMultiplier = (float)Math.Pow(2, requestIndex);

            return baseTension * costMultiplier * stackMultiplier;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in ConferenceDeadlines.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) ConferenceDeadlines.Remove(pair.Key); }
                foreach (var pair in ConferenceLocations.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated && pair.Value != null)) ConferenceLocations.Remove(pair.Key); }
                foreach (var pair in ConferenceCooldowns.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) ConferenceCooldowns.Remove(pair.Key); }
                foreach (var pair in CeasefireStartTimes.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) CeasefireStartTimes.Remove(pair.Key); }
                foreach (var pair in CeasefireDailyTributeWeReceive.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) CeasefireDailyTributeWeReceive.Remove(pair.Key); }
                foreach (var pair in ActiveDailyTributes.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) ActiveDailyTributes.Remove(pair.Key); }
                foreach (var pair in SavedOurScorePercent.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) SavedOurScorePercent.Remove(pair.Key); }
                foreach (var pair in SavedEnemyScorePercent.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) SavedEnemyScorePercent.Remove(pair.Key); }
                foreach (var pair in SavedMaxCap.ToList()) { if (!(pair.Key != null && !pair.Key.IsEliminated)) SavedMaxCap.Remove(pair.Key); }
            }
            dataStore.SyncData("_conferenceDeadlines", ref _conferenceDeadlines);
            dataStore.SyncData("_conferenceLocations", ref _conferenceLocations);
            dataStore.SyncData("_conferenceCooldowns", ref _conferenceCooldowns);
            dataStore.SyncData("_ceasefireStartTimes", ref _ceasefireStartTimes);
            dataStore.SyncData("_ceasefireDailyTributeWeReceive", ref _ceasefireDailyTributeWeReceive);
            dataStore.SyncData("_activeDailyTributes", ref _activeDailyTributes);
            dataStore.SyncData("_savedOurScorePercent", ref _savedOurScorePercent);
            dataStore.SyncData("_savedEnemyScorePercent", ref _savedEnemyScorePercent);
            dataStore.SyncData("_savedMaxCap", ref _savedMaxCap);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddMessengerDialogs(starter);
            AddConferenceDialogs(starter);
        }

        private void OnMakePeace(TaleWorlds.CampaignSystem.IFaction side1Faction, TaleWorlds.CampaignSystem.IFaction side2Faction, MakePeaceAction.MakePeaceDetail detail)
        {
            if (Hero.MainHero == null || (Hero.MainHero.MapFaction as Kingdom) == null) return;

            if (side1Faction == (Hero.MainHero.MapFaction as Kingdom) || side2Faction == (Hero.MainHero.MapFaction as Kingdom))
            {
                Kingdom enemyFaction = ((side1Faction == (Hero.MainHero.MapFaction as Kingdom)) ? side2Faction : side1Faction) as Kingdom;

                // Savas skorlari OnDailyTick tarafindan onceden kaydedildigi icin
                // burada tekrar GUNCELLEMEYIN! Cunku OnMakePeace cagrildiginda StanceLink sifirlanmis oluyor.
                // LoadSavedWarScores kullanilinca zaten en son kaydedilen gunun verisi alinacak.

                if (IsConferencePeace) return;

                if (_conferenceDeadlines.ContainsKey(enemyFaction)) return; // Zaten planlandiysa yoksay

                // Harac miktarini hesapla (oyun kurallari cercevesinde telafi edebilmek icin)
                StanceLink stance = (Hero.MainHero.MapFaction as Kingdom).GetStanceWith(enemyFaction);
                int weReceive = 0;
                if (stance != null)
                {
                    weReceive = stance.GetDailyTributeToPay(enemyFaction) - stance.GetDailyTributeToPay((Hero.MainHero.MapFaction as Kingdom));
                }
                
                _ceasefireStartTimes[enemyFaction] = CampaignTime.Now;
                _ceasefireDailyTributeWeReceive[enemyFaction] = weReceive;

                if (Hero.MainHero.IsFactionLeader)
                {
                    InformationManager.ShowInquiry(new InquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_pc_29}Ceasefire Declared").ToString(),
                        new TaleWorlds.Localization.TextObject("{=rad_pc_15}A ceasefire was made with {FACTION_NAME}. The kings and ambassadors will gather in a neutral area within 7 days to discuss the exact terms. You must travel to the negotiation venue yourself!").SetTextVariable("FACTION_NAME", enemyFaction.Name).ToString(),
                        true, false, "Devam", "", 
                        () => { ScheduleConference(enemyFaction); }, null));
                }
                else
                {
                    InformationManager.ShowInquiry(new InquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_pc_29}Ceasefire Declared").ToString(),
                        new TaleWorlds.Localization.TextObject("{=rad_pc_16}A ceasefire was made with {FACTION_NAME}. The kings sat at the table to discuss the exact terms. Our king gave you a secret mission in the process!").SetTextVariable("FACTION_NAME", enemyFaction.Name).ToString(),
                        true, false, "View Task", "", 
                        () => { 
                            if (PeaceConferenceQuestBehavior.Instance != null)
                            {
                                int ourScore = _savedOurScorePercent.ContainsKey(enemyFaction) ? _savedOurScorePercent[enemyFaction] : 50;
                                PeaceConferenceQuestBehavior.Instance.StartQuest(enemyFaction, ourScore);
                            }
                        }, null));
                }
            }
        }

        private void SaveWarScoresBeforePeace(Kingdom ourFaction, Kingdom enemyFaction)
        {
            // StanceLink henuz sifirlanmadan skorlari hesapla ve kaydet
            StanceLink stance = ourFaction.GetStanceWith(enemyFaction);
            if (stance == null || !stance.IsAtWar) return; // Savas yoksa bir sey kaydetme

            int ourSieges = stance.GetSuccessfulSieges(ourFaction);
            int ourRaids = stance.GetSuccessfulRaids(ourFaction);
            int enemyCasualties = stance.GetCasualties(enemyFaction);
            int ourCasualties = stance.GetCasualties(ourFaction);
            int enemySieges = stance.GetSuccessfulSieges(enemyFaction);
            int enemyRaids = stance.GetSuccessfulRaids(enemyFaction);

            float ourScore = (ourSieges * 10000f) + (ourRaids * 500f) + enemyCasualties - (ourCasualties * 0.3f);
            float enemyScore = (enemySieges * 10000f) + (enemyRaids * 500f) + ourCasualties - (enemyCasualties * 0.3f);

            if (ourScore < 1f) ourScore = 1f;
            if (enemyScore < 1f) enemyScore = 1f;

            float total = ourScore + enemyScore;
            int ourPct = (int)Math.Round((ourScore / total) * 100f);
            int enemyPct = 100 - ourPct;

            _savedOurScorePercent[enemyFaction] = ourPct;
            _savedEnemyScorePercent[enemyFaction] = enemyPct;

            // Tazminat cap: skor farki * 20000 + baz 50000, minimum 10000, maksimum 1000000
            int scoreDiff = Math.Abs(ourPct - enemyPct);
            int cap = 50000 + (scoreDiff * 20000);
            cap = Math.Max(1000000, Math.Min(10000000, cap));
            _savedMaxCap[enemyFaction] = cap;

            InformationManager.DisplayMessage(new InformationMessage(
                $"Battle Score Saved: Us %{ourPct} - Enemy %{enemyPct} | Siege: {ourSieges} vs {enemySieges}, Plunder: {ourRaids} vs {enemyRaids}, Casualties: {ourCasualties} vs {enemyCasualties}", Colors.Cyan));
        }

        // =========================================================
        // SAVAS SKORU HESAPLAMA
        // =========================================================
        private void LoadSavedWarScores(Kingdom enemyFaction)
        {
            // Onceden kaydedilmis savas skorlarini yukle
            if (_savedOurScorePercent.TryGetValue(enemyFaction, out int ourPct))
            {
                _ourScorePercent = ourPct;
            }
            else
            {
                _ourScorePercent = 50; // Veri yoksa esit
            }

            if (_savedEnemyScorePercent.TryGetValue(enemyFaction, out int enemyPct))
            {
                _enemyScorePercent = enemyPct;
            }
            else
            {
                _enemyScorePercent = 50;
            }

            if (_savedMaxCap.TryGetValue(enemyFaction, out int cap))
            {
                _maxTazminatCap = cap;
            }
            else
            {
                _maxTazminatCap = 1000000;
            }
        }

        private float CalculateNpcTensionLimit(Hero enemyLeader)
        {
            // Baz sinir: 100
            float limit = 100f;

            // Karakter ozelliklerine gore ayarlama (-2 ile +2 arasi)
            int calculating = enemyLeader.GetTraitLevel(DefaultTraits.Calculating); // -2 dusuncesiz, +2 hesapci
            int mercy = enemyLeader.GetTraitLevel(DefaultTraits.Mercy);             // -2 zalim, +2 merhametli
            int valor = enemyLeader.GetTraitLevel(DefaultTraits.Valor);             // -2 temkinli, +2 cesur/atilgan
            int honor = enemyLeader.GetTraitLevel(DefaultTraits.Honor);             // -2 sahteci, +2 onurlu

            // Hesapci (sabir artar) -> sinir yukselir
            limit += calculating * 10f;
            // Merhametli -> daha sabirli
            limit += mercy * 5f;
            // Cesur/Atilgan -> daha cabuk patlar
            limit -= valor * 8f;
            // Onurlu -> hakarete daha duyarli ama genel sabirli
            limit += honor * 3f;

            // Siniri 60 ile 140 arasinda tut
            limit = MathF.Clamp(limit, 60f, 140f);

            return limit;
        }

        private int CalculateEnemyGoldDemand()
        {
            // Dusmanin skor ustunlugune gore tazminat talebi
            int scoreDiff = _enemyScorePercent - _ourScorePercent;
            if (scoreDiff <= 5) return 5000;
            if (scoreDiff <= 15) return 15000;
            if (scoreDiff <= 25) return 35000;
            if (scoreDiff <= 40) return 60000;
            return 100000;
        }

        private int CalculateEnemyTributeDemand()
        {
            // Dusmanin skor ustunlugune gore gunluk harac talebi
            int scoreDiff = _enemyScorePercent - _ourScorePercent;
            if (scoreDiff <= 5) return 100;
            if (scoreDiff <= 15) return 200;
            if (scoreDiff <= 25) return 350;
            if (scoreDiff <= 40) return 500;
            return 750;
        }

        // =========================================================
        // HABERCILER VE ARAYUZ
        // =========================================================
        private void AddMessengerDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("player_send_messenger", "hero_main_options", "messenger_response",
                "{=rad_auto_226}Send a messenger to the enemy. Let's meet in a neutral area to discuss peace terms. (5,000 Gold, 30 Influence)",
                () => 
                {
                    if (Hero.OneToOneConversationHero != null && 
                        (Hero.OneToOneConversationHero.MapFaction as Kingdom) == (Hero.MainHero.MapFaction as Kingdom) && Hero.OneToOneConversationHero.IsLord &&
                        Hero.MainHero.IsFactionLeader && 
                        Hero.MainHero.Gold >= 5000 &&
                        Hero.MainHero.Clan.Influence >= 30f)
                    {
                        return Kingdom.All.Any(k => k.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom)) && !_conferenceCooldowns.ContainsKey(k));
                    }
                    return false;
                }, 
                null);

            starter.AddDialogLine("messenger_response", "messenger_response", "messenger_select_enemy",
                "{=rad_auto_213}You command, sir. Which kingdom would you like us to send messengers to?",
                null, null);

            for (int i = 0; i < 8; i++) // Maksimum 8 kralliga karsi ayni anda savasta olunabilir
            {
                int index = i;
                starter.AddPlayerLine($"messenger_pick_enemy_{index}", "messenger_select_enemy", "messenger_confirm",
                    "{=rad_auto_227}{ENEMY_FACTION_" + index + "} send it to the kingdom.",
                    () => 
                    {
                        var enemies = Kingdom.All.Where(k => k.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom)) && !_conferenceCooldowns.ContainsKey(k)).ToList();
                        if (enemies.Count > index)
                        {
                            MBTextManager.SetTextVariable("ENEMY_FACTION_" + index, enemies[index].Name);
                            return true;
                        }
                        return false;
                    }, 
                    () => 
                    {
                        var enemies = Kingdom.All.Where(k => k.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom)) && !_conferenceCooldowns.ContainsKey(k)).ToList();
                        if (enemies.Count > index)
                        {
                            Kingdom chosenEnemy = enemies[index];
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 5000);
                            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -30f);
                            
                            SaveWarScoresBeforePeace((Hero.MainHero.MapFaction as Kingdom), chosenEnemy);

                            _currentEnemyFaction = chosenEnemy; // Cok onemli: Hangi dusmani sectigimizi belirtir

                            IsConferencePeace = true;
                            MakePeaceAction.Apply((Hero.MainHero.MapFaction as Kingdom), chosenEnemy);
                            IsConferencePeace = false;

                            _ceasefireStartTimes[chosenEnemy] = CampaignTime.Now;
                            _ceasefireDailyTributeWeReceive[chosenEnemy] = 0; // Manuel barista harac yok
                            ScheduleConference(chosenEnemy);
                        }
                    });
            }

            starter.AddPlayerLine("messenger_cancel", "messenger_select_enemy", "hero_main_options",
                "{=rad_auto_228}I gave up.", null, null);

            starter.AddDialogLine("messenger_confirm", "messenger_confirm", "hero_main_options",
                "{=rad_auto_214}I'm sending our messenger on his way immediately. You must appear at the Lord's Hall in {NEUTRAL_TOWN} within 7 days.",
                () => 
                {
                    if (_currentEnemyFaction != null && _conferenceLocations.TryGetValue(_currentEnemyFaction, out Settlement town))
                    {
                        MBTextManager.SetTextVariable("NEUTRAL_TOWN", town.Name);
                        return true;
                    }
                    return false;
                }, null);
        }

        private void ScheduleConference(Kingdom enemyFaction)
        {
            // 1. Oncelik: Bize ve onlara ait olmayan, ve HER IKI tarafla da BARI?TA olan sehirleri sec
            var neutralTowns = Settlement.All.Where(s => 
                s.IsTown && 
                s.MapFaction != (Hero.MainHero.MapFaction as Kingdom) && 
                s.MapFaction != enemyFaction &&
                !s.MapFaction.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom)) &&
                !s.MapFaction.IsAtWarWith(enemyFaction)).ToList();

            // 2. Eger oyle bir sehir yoksa: Bize dusman OLMAYAN herhangi bir baska krallik sehri
            if (neutralTowns.Count == 0)
            {
                neutralTowns = Settlement.All.Where(s => 
                    s.IsTown && 
                    s.MapFaction != (Hero.MainHero.MapFaction as Kingdom) && 
                    s.MapFaction != enemyFaction &&
                    !s.MapFaction.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom))).ToList();
            }

            // 3. Eger o da yoksa: Bize ve dusmana ait olmayan HERHANGI bir sehir
            if (neutralTowns.Count == 0)
            {
                neutralTowns = Settlement.All.Where(s => 
                    s.IsTown && 
                    s.MapFaction != (Hero.MainHero.MapFaction as Kingdom) && 
                    s.MapFaction != enemyFaction).ToList();
            }

            // Secilen listeden bize en yakin olan sehri bul
            Settlement neutralTown = neutralTowns.OrderBy(s => s.GatePosition.DistanceSquared(MobileParty.MainParty.GetPosition2D)).FirstOrDefault();
            
            if (neutralTown == null) neutralTown = Settlement.All.FirstOrDefault(s => s.IsTown);

            _conferenceDeadlines[enemyFaction] = CampaignTime.DaysFromNow(7f);
            _conferenceLocations[enemyFaction] = neutralTown;
            
            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_17}You must travel to {TOWN_NAME} for negotiations within 7 days!").SetTextVariable("TOWN_NAME", neutralTown.Name).ToString(), Colors.Yellow));
        }

        private void OnDailyTick()
        {
            // Savas skorlarini her gun kaydet ki MakePeace oldugunda StanceLink silinmeden onceki veriyi bilelim
            if (Hero.MainHero != null && (Hero.MainHero.MapFaction as Kingdom) != null)
            {
                var enemies = Kingdom.All.Where(k => k.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom))).ToList();
                foreach (var enemy in enemies)
                {
                    SaveWarScoresBeforePeace((Hero.MainHero.MapFaction as Kingdom), enemy);
                }
            }

            var expiredCooldowns = _conferenceCooldowns.Where(kvp => kvp.Value.IsPast).Select(kvp => kvp.Key).ToList();
            foreach (var f in expiredCooldowns) _conferenceCooldowns.Remove(f);

            // Deadline'i kaciranlar (Zamaninda sehre gitmeyenler)
            var missedConferences = _conferenceDeadlines.Where(kvp => kvp.Value.IsPast).ToList();
            foreach (var kvp in missedConferences)
            {
                Kingdom enemyFaction = kvp.Key;
                _conferenceDeadlines.Remove(enemyFaction);
                _conferenceLocations.Remove(enemyFaction);

                if (!(Hero.MainHero.MapFaction as Kingdom).IsAtWarWith(enemyFaction))
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_22}Didn't get to the negotiation table on time! {FACTION_NAME} took this as an insult.").SetTextVariable("FACTION_NAME", enemyFaction.Name).ToString(), Colors.Red));
                    FinalizePeaceFailure(enemyFaction);
                }
            }

            // Savas cikan sehirlerin yerini degistir
            var relocationList = new List<Kingdom>();
            foreach (var kvp in _conferenceLocations)
            {
                Kingdom enemyFaction = kvp.Key;
                Settlement currentTown = kvp.Value;
                
                // Eger sehrin kralligi bize veya dusmana savas actiysa
                if (currentTown.MapFaction.IsAtWarWith((Hero.MainHero.MapFaction as Kingdom)) || currentTown.MapFaction.IsAtWarWith(enemyFaction))
                {
                    relocationList.Add(enemyFaction);
                }
            }
            foreach (var enemy in relocationList)
            {
                ScheduleConference(enemy); // Yeni sehir bulur ve sureyi 7 gun uzatir
                InformationManager.DisplayMessage(new InformationMessage($"Negotiation location with {enemy.Name} has been updated as the war situation has changed!", Colors.Yellow));
            }

            // ============================
            // GUNLUK HARAC SISTEMI
            // ============================
            var tributesToRemove = new List<Kingdom>();
            foreach (var kvp in _activeDailyTributes)
            {
                Kingdom faction = kvp.Key;
                int amount = kvp.Value; // Pozitif = biz oduyoruz, Negatif = onlar oduyorlar

                // Savas baslarsa haraci durdur
                if ((Hero.MainHero.MapFaction as Kingdom).IsAtWarWith(faction))
                {
                    tributesToRemove.Add(faction);
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_23}The war with {FACTION_NAME} has begun! Tribute payments stopped.").SetTextVariable("FACTION_NAME", faction.Name).ToString(), Colors.Red));
                    continue;
                }

                if (amount > 0)
                {
                    DistributeTributeDebt((Hero.MainHero.MapFaction as Kingdom), faction, amount);
                }
                else if (amount < 0)
                {
                    DistributeTributeReward(faction, (Hero.MainHero.MapFaction as Kingdom), Math.Abs(amount));
                }
            }
            foreach (var f in tributesToRemove) _activeDailyTributes.Remove(f);
        }

        private void Start3DConferenceMission(Kingdom enemyFaction, Settlement town)
        {
            Hero enemyKing = enemyFaction.Leader;
            Hero ourKing = (Hero.MainHero.MapFaction as Kingdom).Leader;

            // Savas Konseyi mantigi: Hep ayni sabit sorunsuz sahnede acilir
            string sceneName = "khuzait_castle_keep_a_l1_interior";

            Mission mission = TaleWorlds.MountAndBlade.MissionState.OpenNew("PeaceConferenceMission",
                  SandBox.SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, TaleWorlds.Engine.DecalAtlasGroup.All),
                  (m) => new TaleWorlds.MountAndBlade.MissionBehavior[]
                  {
                      new TaleWorlds.MountAndBlade.Source.Missions.MissionOptionsComponent(),
                      new PeaceConferenceMissionLogic(enemyKing, ourKing)
                  }, true, true);

            if (mission != null)
            {
                mission.AddMissionBehavior(SandBox.View.SandBoxViewCreator.CreateMissionConversationView(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionMainAgentEquipmentController(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionAgentStatusUIHandler(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionSingleplayerEscapeMenu(false));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionLeaveView());
            }
        }

        // =========================================================
        // ANA DIYALOG AGACI
        // =========================================================
        private void AddConferenceDialogs(CampaignGameStarter starter)
        {
            // Sehir Menusu: Muzakereye Katil
            starter.AddGameMenuOption("town", "peace_conference_enter", "{=rad_auto_240}Join the Peace Negotiation", 
                (MenuCallbackArgs args) => 
                {
                    if (_conferenceLocations.ContainsValue(Settlement.CurrentSettlement))
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                        return true;
                    }
                    return false;
                },
                (MenuCallbackArgs args) => 
                {
                    var kvp = _conferenceLocations.FirstOrDefault(x => x.Value == Settlement.CurrentSettlement);
                    if (kvp.Key != null)
                    {
                        _currentEnemyFaction = kvp.Key;
                        _currentTension = 0f;
                        _requestedGold = 0;
                        _requestedDailyTribute = 0;
                        _enemyRequestedGold = 0;
                        _enemyRequestedDailyTribute = 0;
                        _requestedFiefs.Clear();
                        _ultimatumGiven = false;
                        _conferenceDeadlines.Remove(_currentEnemyFaction);
                        _conferenceLocations.Remove(_currentEnemyFaction);

                        // Onceden kaydedilmis savas skorlarini yukle
                        LoadSavedWarScores(_currentEnemyFaction);
                        // NPC gerilim sinirini hesapla
                        _npcTensionLimit = CalculateNpcTensionLimit(_currentEnemyFaction.Leader);

                        Start3DConferenceMission(_currentEnemyFaction, Settlement.CurrentSettlement);
                    }
                }, false, 2);

            // ============================
            // BASLANGIC DIYALOGLARI
            // ============================
            starter.AddDialogLine("peace_conference_start_enemy_king", "start", "peace_conference_start_player_options",
                "{=rad_pc_08}Since you came all the way from the battlefield, you must have something to say. Why are we here?",
                () => { 
                    var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    return logic != null && Hero.OneToOneConversationHero == logic.EnemyKing; 
                }, null, 1000);
            
            starter.AddDialogLine("peace_conference_start_our_king", "start", "close_window",
                "{=rad_auto_215}I will lead the meetings. You just stand silently and watch.",
                () => { 
                    var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    return logic != null && Hero.OneToOneConversationHero == logic.OurKing; 
                }, null, 1000);

            starter.AddDialogLine("peace_conference_start_other", "start", "close_window",
                "{=rad_auto_216}Now is not the time to talk. We await the decision of our kings.",
                () => { 
                    var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    return logic != null && Hero.OneToOneConversationHero != logic.EnemyKing && Hero.OneToOneConversationHero != logic.OurKing; 
                }, null, 1000);

            starter.AddPlayerLine("peace_conference_start_active", "peace_conference_start_player_options", "peace_conference_agenda_start",
                "{=rad_pc_09}[Start Negotiation] We have come to negotiate the terms of war. Let's sit at the table.",
                () => { 
                    var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    return logic != null && logic.OurKing == Hero.MainHero; 
                }, null);

            starter.AddPlayerLine("peace_conference_start_passive", "peace_conference_start_player_options", "peace_conf_passive_eval",
                "{=rad_auto_229}[Listen silently] (The kings begin to deliberate)",
                () => { 
                    var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    return logic != null && logic.OurKing != Hero.MainHero; 
                }, null);

            // ============================
            // ANA MUZAKERE MASASI
            // ============================
            starter.AddDialogLine("peace_conference_agenda", "peace_conference_agenda_start", "peace_conference_player_choice",
                "M?zakere Masas? ? Sava? Durumu: Biz %{OUR_SCORE} - D??man %{ENEMY_SCORE} | Gerilim: {TENSION}/{TENSION_LIMIT}\n?artlar? belirleyin.|M?zakere Masas? ? Sava? Durumu: Biz %{OUR_SCORE} - D??man %{ENEMY_SCORE} | Gerilim: {TENSION}/{TENSION_LIMIT}\n?artlar? belirleyin.",
                () => 
                {
                    if (!Hero.MainHero.IsFactionLeader) return false;
                    if (TaleWorlds.MountAndBlade.Mission.Current != null && Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsFactionLeader)
                    {
                        MBTextManager.SetTextVariable("OUR_SCORE", _ourScorePercent.ToString());
                        MBTextManager.SetTextVariable("ENEMY_SCORE", _enemyScorePercent.ToString());
                        MBTextManager.SetTextVariable("TENSION", MathF.Round(_currentTension).ToString());
                        MBTextManager.SetTextVariable("TENSION_LIMIT", "100");
                        return true;
                    }
                    return false;
                }, null);

            // Pasif Katilim
            starter.AddPlayerLine("peace_conf_passive_wait", "peace_conf_passive_eval", "peace_conf_passive_result",
                "{=rad_auto_230}[Listen quietly]", null, null);

            starter.AddDialogLine("peace_conf_passive_result", "peace_conf_passive_result", "close_window",
                "{=rad_auto_217}[{ENEMY_KING}]: The decision has been made. I'm signing the agreement.",
                null, 
                () => 
                { 
                    MBTextManager.SetTextVariable("ENEMY_KING", Hero.OneToOneConversationHero.Name);
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), (Hero.OneToOneConversationHero.MapFaction as Kingdom), 0, 0);
                });

            // ============================
            // SENARYO A: BIZ KAZANIYORSAK ? Toplu Tazminat
            // ============================
            starter.AddPlayerLine("peace_conf_topic_gold", "peace_conference_player_choice", "close_window",
                "{=rad_pc_10}Determine the amount of lump sum compensation. (Entry from keyboard) [Max: {MAX_GOLD} Gold]",
                () => 
                {
                    MBTextManager.SetTextVariable("MAX_GOLD", _maxTazminatCap.ToString());
                    return _ourScorePercent >= _enemyScorePercent; // Sadece kazanirken gosterilir
                }, 
                () => 
                {
                    if (Mission.Current != null)
                    {
                        var logic = Mission.Current.GetMissionBehavior<PeaceConferenceMissionLogic>();
                        if (logic != null) logic.RequestGoldInput();
                    }
                });

            // SENARYO A: BIZ KAZANIYORSAK ? Gunluk Harac
            starter.AddPlayerLine("peace_conf_topic_tribute", "peace_conference_player_choice", "close_window",
                "{=rad_pc_11}Set the daily tribute amount. (Entry from keyboard)",
                () => _ourScorePercent >= _enemyScorePercent, 
                () => 
                {
                    if (Mission.Current != null)
                    {
                        var logic = Mission.Current.GetMissionBehavior<PeaceConferenceMissionLogic>();
                        if (logic != null) logic.RequestTributeInput();
                    }
                });

            // SENARYO A: BIZ KAZANIYORSAK ? Fief Talep Et
            starter.AddPlayerLine("peace_conf_topic_fief", "peace_conference_player_choice", "close_window",
                "{=rad_pc_12}Request that they transfer a settlement.",
                () => _ourScorePercent >= 90, 
                () => 
                {
                    var elements = new List<TaleWorlds.Core.InquiryElement>();
                    var enemyFiefs = Settlement.All.Where(s => (s.IsTown || s.IsCastle) && s.MapFaction == _currentEnemyFaction && !_requestedFiefs.Contains(s)).ToList();
                    
                    foreach (var fief in enemyFiefs)
                    {
                        float tensionCost = GetFiefDemandTension(fief, _requestedFiefs.Count);
                        elements.Add(new TaleWorlds.Core.InquiryElement(fief, $"{fief.Name} (Tension: +{tensionCost:F0})", null));
                    }

                    if (elements.Count == 0)
                    {
                        TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_19}The enemy has no more land to claim.").ToString(), TaleWorlds.Library.Colors.Red));
                        var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                        logic?.RestartConversation();
                        return;
                    }
                    
                    var missionLogic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                    if (missionLogic != null) missionLogic.IsWaitingForInput = true;

                    TaleWorlds.Core.MBInformationManager.ShowMultiSelectionInquiry(new TaleWorlds.Core.MultiSelectionInquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_auto_241}Claim Land").ToString(),
                        new TaleWorlds.Localization.TextObject("{=rad_auto_242}Which settlement do you claim from the enemy?").ToString(),
                        elements, true, 1, 1, "Request", "Cancel",
                        (List<TaleWorlds.Core.InquiryElement> list) => 
                        {
                            if (list.Count > 0 && list[0].Identifier is Settlement chosenFief)
                            {
                                _requestedFiefs.Add(chosenFief);
                                RecalculateTension();
                                CheckTensionLimitWarning();
                            }
                            var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                            logic?.RestartConversation();
                        },
                        (List<TaleWorlds.Core.InquiryElement> list) => 
                        {
                            var logic = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<PeaceConferenceMissionLogic>();
                            logic?.RestartConversation();
                        }
                    ));
                });

            // ============================
            // SENARYO B: BIZ KAYBEDIYORSAK ? Dusman Talepleri
            // ============================
            starter.AddPlayerLine("peace_conf_ask_demands", "peace_conference_player_choice", "peace_conf_enemy_demands",
                "{=rad_pc_18}What are your terms? What do you demand?",
                () => _ourScorePercent < _enemyScorePercent, 
                null);

            starter.AddDialogLine("peace_conf_enemy_demands_text", "peace_conf_enemy_demands", "peace_conf_enemy_demands_choice",
                "{=rad_pc_03}We are winning the war. If you want peace, accept defeat! You will pay us {ENEMY_GOLD} gold in bulk compensation and a daily tribute of {ENEMY_TRIBUTE} denars. (Tension: {TENSION}/{TENSION_LIMIT})",
                null,
                () =>
                {
                    _enemyRequestedGold = CalculateEnemyGoldDemand();
                    _enemyRequestedDailyTribute = CalculateEnemyTributeDemand();
                    MBTextManager.SetTextVariable("ENEMY_GOLD", _enemyRequestedGold.ToString());
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                    MBTextManager.SetTextVariable("TENSION", MathF.Round(_currentTension).ToString());
                    MBTextManager.SetTextVariable("TENSION_LIMIT", "100");
                });

            // Oyuncu kabul eder
            starter.AddPlayerLine("peace_conf_enemy_accept", "peace_conf_enemy_demands_choice", "peace_conf_enemy_finalize_accept",
                "{=rad_pc_20}I accept the terms. We will pay the money. (Tension: +0)",
                null, null);

            for (int i = 1; i <= 4; i++)
            {
                int index = i;
                starter.AddPlayerLine($"peace_conf_enemy_haggle_{index}", "peace_conf_enemy_demands_choice", "peace_conf_enemy_haggle_response",
                    "Toplu alt?n tazminat?nda %{HAGGLE_PCT_" + index + "} indirim yaparsan?z anla?abiliriz. (Gerilim: +{HAGGLE_TEN_" + index + "})",
                    () => 
                    {
                        if (_ultimatumGiven) return false;
                        int pct = GetHagglePercentage(index);
                        MBTextManager.SetTextVariable("HAGGLE_PCT_" + index, pct.ToString());
                        MBTextManager.SetTextVariable("HAGGLE_TEN_" + index, GetHaggleTension(index).ToString());
                        return true;
                    },
                    () => 
                    { 
                        _currentTension += GetHaggleTension(index); 
                        float multiplier = 1f - (GetHagglePercentage(index) / 100f);
                        _enemyRequestedGold = (int)(_enemyRequestedGold * multiplier); 
                    });
            }

            // Gunluk Harac pazarligi ? miktar bazli
            starter.AddPlayerLine("peace_conf_enemy_tribute_haggle", "peace_conf_enemy_demands_choice", "peace_conf_enemy_tribute_counter",
                "{=rad_auto_232}I object to the daily fee. Let's set a lower amount.",
                () => !_ultimatumGiven && _enemyRequestedDailyTribute > 0,
                null);

            // Masayi devir (her zaman gorunur)
            starter.AddPlayerLine("peace_conf_enemy_reject", "peace_conf_enemy_demands_choice", "close_window",
                "{=rad_auto_233}This is unacceptable! The war will continue! (Turn over the table)",
                null, 
                () => { FinalizePeaceFailure(_currentEnemyFaction); });

            // ============================
            // DUSMANIN PAZARLIK YANITI
            // ============================
            // Ultimatom verilmisse ? son teklif
            starter.AddDialogLine("peace_conf_enemy_ultimatum", "peace_conf_enemy_haggle_response", "peace_conf_ultimatum_choice",
                "{=rad_auto_218}You're making me lose my patience! This is my final offer: {ENEMY_GOLD} gold and {ENEMY_TRIBUTE} Denars per day! Accept it, or we'll topple the table and continue the war!",
                () => _currentTension >= _npcTensionLimit,
                () =>
                {
                    _ultimatumGiven = true;
                    MBTextManager.SetTextVariable("ENEMY_GOLD", _enemyRequestedGold.ToString());
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                });

            // Normal pazarlik yaniti ? teklifi kabul ama daha az indirimle karsi teklif yapar
            starter.AddDialogLine("peace_conf_enemy_counter", "peace_conf_enemy_haggle_response", "peace_conf_enemy_demands_choice",
                "{=rad_pc_04}Hmm... Alright, I can be a bit flexible. My new offer: {ENEMY_GOLD} gold and a daily tribute of {ENEMY_TRIBUTE} denars. (Tension: {TENSION}/{TENSION_LIMIT})",
                () => _currentTension < _npcTensionLimit,
                () =>
                {
                    MBTextManager.SetTextVariable("ENEMY_GOLD", _enemyRequestedGold.ToString());
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                    MBTextManager.SetTextVariable("TENSION", MathF.Round(_currentTension).ToString());
                    MBTextManager.SetTextVariable("TENSION_LIMIT", "100");
                });

            // Ultimatom secenekleri
            starter.AddPlayerLine("peace_conf_ultimatum_accept", "peace_conf_ultimatum_choice", "peace_conf_enemy_finalize_accept",
                "{=rad_auto_234}OK, I accept.",
                null, null);

            starter.AddPlayerLine("peace_conf_ultimatum_reject", "peace_conf_ultimatum_choice", "close_window",
                "{=rad_auto_235}Never! The war will continue! (Turn over the table)",
                null, 
                () => { FinalizePeaceFailure(_currentEnemyFaction); });

            // ============================
            // GUNLUK HARAC PAZARLIGI (Miktar bazli)
            // ============================
            starter.AddDialogLine("peace_conf_tribute_counter_text", "peace_conf_enemy_tribute_counter", "peace_conf_tribute_counter_options",
                "{=rad_pc_05}I demand {ENEMY_TRIBUTE} denars as a daily tribute. How much do you offer? (Tension: {TENSION}/{TENSION_LIMIT})",
                null,
                () =>
                {
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                    MBTextManager.SetTextVariable("TENSION", MathF.Round(_currentTension).ToString());
                    MBTextManager.SetTextVariable("TENSION_LIMIT", "100");
                });

            // Harac pazarlik secenekleri: (Yuzdelik)
            for (int i = 1; i <= 4; i++)
            {
                int index = i;
                starter.AddPlayerLine($"peace_conf_tribute_offer_{index}", "peace_conf_tribute_counter_options", "peace_conf_enemy_haggle_response",
                    "G?nl?k hara? %{TRIBUTE_PCT_" + index + "} daha az olmal?! (Gerilim: +{TRIBUTE_TEN_" + index + "})",
                    () =>
                    {
                        if (_enemyRequestedDailyTribute <= 0) return false;
                        int pct = GetTributeHagglePercentage(index);
                        MBTextManager.SetTextVariable("TRIBUTE_PCT_" + index, pct.ToString());
                        MBTextManager.SetTextVariable("TRIBUTE_TEN_" + index, GetTributeHaggleTension(index).ToString());
                        return true;
                    },
                    () => 
                    { 
                        _currentTension += GetTributeHaggleTension(index); 
                        float multiplier = 1f - (GetTributeHagglePercentage(index) / 100f);
                        _enemyRequestedDailyTribute = (int)(_enemyRequestedDailyTribute * multiplier); 
                    });
            }
            starter.AddPlayerLine("peace_conf_tribute_accept", "peace_conf_tribute_counter_options", "peace_conf_enemy_demands_choice",
                "{=rad_auto_237}Ok, {ENEMY_TRIBUTE} I accept Denars.",
                () =>
                {
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                    return true;
                },
                null);

            // ============================
            // DUSMANIN TALEPLERINI KABUL ? Finalize
            // ============================
            starter.AddDialogLine("peace_conf_enemy_finalize_text", "peace_conf_enemy_finalize_accept", "peace_conf_enemy_fief_ultimatum_check",
                "{=rad_auto_219}A smart decision. The agreement was signed. You will pay {ENEMY_GOLD} gold compensation and {ENEMY_TRIBUTE} Denar tribute daily.",
                null,
                () =>
                {
                    MBTextManager.SetTextVariable("ENEMY_GOLD", _enemyRequestedGold.ToString());
                    MBTextManager.SetTextVariable("ENEMY_TRIBUTE", _enemyRequestedDailyTribute.ToString());
                });

            // Agir yenilgi durumu varsa toprak ultimatomu ekle
            starter.AddDialogLine("peace_conf_enemy_fief_ultimatum_yes", "peace_conf_enemy_fief_ultimatum_check", "peace_conf_enemy_fief_ultimatum_choice",
                "{=rad_auto_220}But wait! Don't think it's over! You can't get away that easily. In addition to paying us compensation, you will also immediately transfer the settlement {ENEMY_WANTED_FIEF} to us! Otherwise this table will topple over!",
                () => 
                {
                    // Eger skorumuz 15'ten kucukse agir maglubiyet.
                    if (_enemyScorePercent >= 90)
                    {
                        var ourFiefs = Settlement.All.Where(s => (s.IsTown || s.IsCastle) && s.MapFaction == (Hero.MainHero.MapFaction as Kingdom)).ToList();
                        if (ourFiefs.Count > 0)
                        {
                            // Dusmanin ortasina en yakin sehrimiz
                            var targetFief = ourFiefs.OrderBy(s => s.GatePosition.DistanceSquared(_currentEnemyFaction.FactionMidSettlement.GatePosition)).First();
                            _requestedFiefs.Clear();
                            _requestedFiefs.Add(targetFief);
                            MBTextManager.SetTextVariable("ENEMY_WANTED_FIEF", targetFief.Name);
                            return true;
                        }
                    }
                    return false;
                },
                null);

            // Agir yenilgi yoksa direk bitir
            starter.AddDialogLine("peace_conf_enemy_fief_ultimatum_no", "peace_conf_enemy_fief_ultimatum_check", "close_window",
                "{=rad_auto_221}The meeting is over.",
                () => _enemyScorePercent < 90 || !_requestedFiefs.Any(),
                () => 
                {
                    DistributeGoldDebt((Hero.MainHero.MapFaction as Kingdom), _enemyRequestedGold);
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), _currentEnemyFaction, 0, _enemyRequestedDailyTribute);
                });

            starter.AddPlayerLine("peace_conf_enemy_fief_ult_accept", "peace_conf_enemy_fief_ultimatum_choice", "close_window",
                "{=rad_auto_238}Damn... Okay, I give the city away. As long as we make peace.",
                null,
                () => 
                {
                    DistributeGoldDebt((Hero.MainHero.MapFaction as Kingdom), _enemyRequestedGold);
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), _currentEnemyFaction, 0, _enemyRequestedDailyTribute);
                });

            starter.AddPlayerLine("peace_conf_enemy_fief_ult_reject", "peace_conf_enemy_fief_ultimatum_choice", "close_window",
                "{=rad_auto_239}This is too much! I never give! The war will continue!",
                null,
                () => 
                {
                    FinalizePeaceFailure(_currentEnemyFaction);
                });

            // ============================
            // SENARYO A DEVAM: Kazaniyorsak ? Dusmanin Pazarligi
            // ============================
            // Barisi imzala secenegi (kazaniyorsak)
            starter.AddPlayerLine("peace_conf_sign", "peace_conference_player_choice", "peace_conf_evaluate",
                "{=rad_pc_13}We completed the draft. You will pay us extra {REQUESTED_GOLD} gold compensation and daily {REQUESTED_TRIBUTE} Denar tribute. Let's declare peace under these conditions.",
                () => 
                {
                    MBTextManager.SetTextVariable("REQUESTED_GOLD", _requestedGold);
                    MBTextManager.SetTextVariable("REQUESTED_TRIBUTE", _requestedDailyTribute);
                    return _ourScorePercent >= _enemyScorePercent;
                }, null);

            // Masayi devir (her zaman)
            starter.AddPlayerLine("peace_conf_leave", "peace_conference_player_choice", "close_window",
                "{=rad_pc_14}There is no peace without negotiation. I'm suspending the discussions. The war will continue! (Turn over the table)", 
                null, 
                () => 
                { 
                    FinalizePeaceFailure(_currentEnemyFaction);
                });

            // ============================
            // DUSMANIN DEGERLENDIRMESI (Biz kazaniyorsak)
            // ============================
            // Ultimatom verildi: Masa devrilir
            starter.AddDialogLine("peace_conf_evaluate_ultimatum", "peace_conf_evaluate", "close_window",
                "{=rad_auto_222}Are you making fun of me?! This is not a negotiation, it's an insult! I'M TURNING THE TABLE DOWN!",
                () => _currentTension >= _npcTensionLimit, 
                () => 
                { 
                    FinalizePeaceFailure(_currentEnemyFaction);
                });

            // Dusman pazarlik yapmak istiyor (gerilim sinirin %60'i uzerindeyse)
            starter.AddDialogLine("peace_conf_evaluate_haggle", "peace_conf_evaluate", "peace_conf_our_haggle_response",
                "{=rad_pc_06}These terms are too heavy! I can accept if you make a %{HAGGLE_PERCENT} discount. (Tension: {TENSION}/{TENSION_LIMIT})",
                () => _currentTension >= _npcTensionLimit * 0.4f && _currentTension < _npcTensionLimit,
                () =>
                {
                    // Gerilime gore indirim yuzdesi belirle (daha dusuk gerilim = daha dusuk indirim talebi)
                    float ratio = _currentTension / _npcTensionLimit;
                    int hagglePercent;
                    if (ratio > 0.8f) hagglePercent = 40;
                    else if (ratio > 0.6f) hagglePercent = 30;
                    else hagglePercent = 20;
                    MBTextManager.SetTextVariable("HAGGLE_PERCENT", hagglePercent.ToString());
                    MBTextManager.SetTextVariable("TENSION", MathF.Round(_currentTension).ToString());
                    MBTextManager.SetTextVariable("TENSION_LIMIT", "100");
                });

            // Kabul eder (gerilim dusukse)
            starter.AddDialogLine("peace_conf_evaluate_accept", "peace_conf_evaluate", "close_window",
                "{=rad_auto_223}The conditions are harsh, but I do not want my people to suffer any longer. I'm signing the agreement.",
                () => _currentTension < _npcTensionLimit * 0.4f, 
                () => 
                { 
                    // Kazanan taraf biziz; altin tum klanlara dagitilir
                    DistributeGoldReward(_currentEnemyFaction, _requestedGold);
                    // Harac ile baris yap (negatif: onlar odeyecek)
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), _currentEnemyFaction, 0, -_requestedDailyTribute);
                });

            // ============================
            // DUSMANIN PAZARLIK YAPMASI (Biz kazaniyorsak, bize indirim istedigi zaman)
            // ============================
            // Oyuncu kabul eder
            starter.AddPlayerLine("peace_conf_our_haggle_accept", "peace_conf_our_haggle_response", "close_window",
                "{=rad_pc_25}Tamam, indirimi kabul ediyorum. Antla?may? imzalayal?m.",
                null,
                () =>
                {
                    // Indirimli miktar zaten _requestedGold uzerinden yapilacak
                    float ratio = _currentTension / _npcTensionLimit;
                    int hagglePercent;
                    if (ratio > 0.8f) hagglePercent = 40;
                    else if (ratio > 0.6f) hagglePercent = 30;
                    else hagglePercent = 20;
                    int discountedGold = (int)(_requestedGold * (1f - hagglePercent / 100f));
                    DistributeGoldReward(_currentEnemyFaction, discountedGold);
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), _currentEnemyFaction, 0, -_requestedDailyTribute);
                });

            // Oyuncu reddeder: indirim vermem diyor ? daha dusuk secenekler sunulur
            starter.AddPlayerLine("peace_conf_our_haggle_less_10", "peace_conf_our_haggle_response", "peace_conf_evaluate_counter",
                "{=rad_pc_07}En fazla %10 inebilirim. (Gerilim: +15)",
                () => !_ultimatumGiven,
                () => { _currentTension += 15f; _requestedGold = (int)(_requestedGold * 0.90f); });

            starter.AddPlayerLine("peace_conf_our_haggle_less_5", "peace_conf_our_haggle_response", "peace_conf_evaluate_counter",
                "{=rad_pc_26}En fazla %5 inebilirim. (Gerilim: +10)",
                () => !_ultimatumGiven,
                () => { _currentTension += 10f; _requestedGold = (int)(_requestedGold * 0.95f); });

            starter.AddPlayerLine("peace_conf_our_haggle_refuse", "peace_conf_our_haggle_response", "peace_conf_evaluate_counter",
                "{=rad_pc_27}Kesinlikle hay?r! ?lk teklifim ge?erlidir! (Gerilim: +25)",
                null,
                () => { _currentTension += 25f; });

            // ============================
            // COUNTER-OFFER EVALUATION
            // ============================
            starter.AddDialogLine("peace_conf_evaluate_counter_ultimatum", "peace_conf_evaluate_counter", "close_window",
                "{=rad_auto_224}Are you making fun of me?! This is not a negotiation, it's an insult! I'M TURNING THE TABLE DOWN!",
                () => _currentTension >= _npcTensionLimit, 
                () => { FinalizePeaceFailure(_currentEnemyFaction); });

            starter.AddDialogLine("peace_conf_evaluate_counter_accept", "peace_conf_evaluate_counter", "close_window",
                "{=rad_auto_225}There is some truth in what he says... Well, we are satisfied with this much. I'm signing the agreement.",
                () => _currentTension < _npcTensionLimit, 
                () => 
                { 
                    DistributeGoldReward(_currentEnemyFaction, _requestedGold);
                    FinalizePeaceSuccess((Hero.MainHero.MapFaction as Kingdom), _currentEnemyFaction, 0, -_requestedDailyTribute);
                });
        }

        // =========================================================
        // ALTIN DAGITIM ve BORC SISTEMI
        // =========================================================
        private void DistributeGoldReward(Kingdom loserFaction, int totalGold)
        {
            // Kazanan tarafin (bizim) lordlarina dagit
            if (totalGold <= 0) return;

            var kingdom = (Hero.MainHero.MapFaction as Kingdom) as Kingdom;
            if (kingdom == null) return;
            var clans = kingdom.Clans.Where(c => !c.IsEliminated).ToList();
            if (clans.Count == 0) return;

            int perClan = totalGold / clans.Count;
            int remainder = totalGold % clans.Count;

            foreach (var clan in clans)
            {
                int amount = perClan + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;
                
                if (clan.Leader == Hero.MainHero)
                {
                    Hero.MainHero.Gold += amount;
                    InformationManager.DisplayMessage(new InformationMessage($"[War Compensation] Received {amount} Denars as your share!", Colors.Green));
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(loserFaction.Leader, clan.Leader, amount);
                }
            }

            InformationManager.DisplayMessage(new InformationMessage($"{totalGold} gold compensation distributed to {clans.Count} clans. (~{perClan} per Clan)", Colors.Green));
        }

        private void DistributeGoldDebt(Kingdom ourFaction, int totalDebt)
        {
            // Kaybeden tarafin (bizim) lordlarindan esit olarak kesilir
            if (totalDebt <= 0) return;

            var kingdom = ourFaction as Kingdom;
            if (kingdom == null) return;
            var clans = kingdom.Clans.Where(c => !c.IsEliminated).ToList();
            if (clans.Count == 0) return;

            int perClan = totalDebt / clans.Count;
            int remainder = totalDebt % clans.Count;

            foreach (var clan in clans)
            {
                int amount = perClan + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;

                Hero clanLeader = clan.Leader;
                if (clanLeader.Gold >= amount)
                {
                    GiveGoldAction.ApplyBetweenCharacters(clanLeader, _currentEnemyFaction.Leader, amount);
                }
                else
                {
                    // Yeterli parasi yoksa, elindeki kadar ver, geri kalan borc olarak kalir
                    int canPay = Math.Max(0, clanLeader.Gold - 100); // En az 100 altin birakalim
                    if (canPay > 0)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(clanLeader, _currentEnemyFaction.Leader, canPay);
                    }
                    int unpaid = amount - canPay;
                    InformationManager.DisplayMessage(new InformationMessage($"{clanLeader.Name} was unable to pay the {unpaid} gold of the compensation. It was recorded as a debt.", Colors.Red));
                }
            }

            InformationManager.DisplayMessage(new InformationMessage($"{totalDebt} gold compensation debt divided among {clans.Count} clans. (~{perClan} per Clan)", Colors.Red));
        }

        // =========================================================
        // FINALIZE
        // =========================================================
        private void FinalizePeaceSuccess(Kingdom side1, Kingdom side2, int lumpSumGold, int dailyTribute)
        {
            IsConferencePeace = true;
            if (!side1.IsAtWarWith(side2))
            {
                // DeclareWarAction.ApplyByDefault(side1, side2);
            }
            MakePeaceAction.Apply(side1, side2);
            IsConferencePeace = false;

            // Oyunun kendi kendine (Native) uyguladigi haraci hesapla
            StanceLink stance = side1.GetStanceWith(side2);
            int nativeTributeWePay = 0;
            if (stance != null)
            {
                nativeTributeWePay = stance.GetDailyTributeToPay(side1) - stance.GetDailyTributeToPay(side2);
            }

            // Anlasilan ile oyunun uyguladigi arasindaki fark
            int diffToPay = dailyTribute - nativeTributeWePay;
            
            if (diffToPay != 0)
            {
                // Pozitif = biz ekstra oduyoruz, Negatif = onlar bize ekstra oduyor
                _activeDailyTributes[side2] = diffToPay;
                InformationManager.DisplayMessage(new InformationMessage(
                    diffToPay > 0 
                        ? $"According to the agreement, in addition to the tribute collected by the game, {diffToPay} more Denars will be deducted from the clans daily." 
                        : $"According to the agreement, in addition to the tribute collected by the game, {Math.Abs(diffToPay)} additional daily Denars will be distributed to the clans.", 
                    TaleWorlds.Library.Colors.Yellow));
            }

            // Sehir transferleri
            if (_requestedFiefs.Count > 0)
            {
                foreach (var fief in _requestedFiefs)
                {
                    Kingdom receiver = (fief.MapFaction == side1 ? side2 : side1) as Kingdom;
                    ChangeOwnerOfSettlementAction.ApplyByDefault(receiver.Leader, fief);
                    if (receiver != null)
                    {
                        if (receiver == TaleWorlds.CampaignSystem.Clan.PlayerClan.Kingdom)
                        {
                            if (TaleWorlds.CampaignSystem.Hero.MainHero.IsFactionLeader)
                            {
                                receiver.AddDecision(new TaleWorlds.CampaignSystem.Election.SettlementClaimantDecision(receiver.Leader.Clan, fief, null, null), false);
                            }
                            else
                            {
                                int roll = TaleWorlds.Core.MBRandom.RandomInt(1, 100);
                                if (roll > 40)
                                {
                                    ChangeOwnerOfSettlementAction.ApplyByDefault(TaleWorlds.CampaignSystem.Hero.MainHero, fief);
                                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_243}The King decided to grant " + fief.Name.ToString() + " to you as a reward!").ToString(), TaleWorlds.Library.Colors.Green));
                                }
                                else
                                {
                                    var otherClans = receiver.Clans.Where(c => c != TaleWorlds.CampaignSystem.Clan.PlayerClan && c.Leader != receiver.Leader).ToList();
                                    if (otherClans.Count > 0)
                                    {
                                        var luckyClan = otherClans[TaleWorlds.Core.MBRandom.RandomInt(otherClans.Count)];
                                        ChangeOwnerOfSettlementAction.ApplyByDefault(luckyClan.Leader, fief);
                                        TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_244}The King granted " + fief.Name.ToString() + " to clan " + luckyClan.Name.ToString() + " instead of you.").ToString(), TaleWorlds.Library.Colors.Red));
                                    }
                                }
                            }
                        }
                        else
                        {
                            receiver.AddDecision(new TaleWorlds.CampaignSystem.Election.SettlementClaimantDecision(receiver.Leader.Clan, fief, null, null), false);
                        }
                    }
                }
            }

            // Kaydedilmis skorlari temizle
            _savedOurScorePercent.Remove(side2);
            _savedEnemyScorePercent.Remove(side2);
            _savedMaxCap.Remove(side2);

            ClearCeasefireTracking(side2);
            InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_24}The peace treaty was signed!").ToString(), Colors.Green));
        }

        private void FinalizePeaceFailure(Kingdom enemyFaction)
        {
            _conferenceCooldowns[enemyFaction] = CampaignTime.DaysFromNow(10f);

            // Savas yeniden baslar
            IsConferencePeace = true;
            if (!(Hero.MainHero.MapFaction as Kingdom).IsAtWarWith(enemyFaction))
            {
                DeclareWarAction.ApplyByDefault((Hero.MainHero.MapFaction as Kingdom), enemyFaction);
            }
            IsConferencePeace = false;

            // Ateskes boyunca oyunun haksiz yere kestigi haraclari telafi et
            if (_ceasefireStartTimes.TryGetValue(enemyFaction, out CampaignTime startTime) && 
                _ceasefireDailyTributeWeReceive.TryGetValue(enemyFaction, out int dailyTribute))
            {
                float daysPassed = (float)(CampaignTime.Now - startTime).ToDays;
                if (daysPassed > 0 && dailyTribute != 0)
                {
                    int totalPaidToUs = (int)(daysPassed * dailyTribute);
                    
                    if (totalPaidToUs > 0)
                    {
                        // Onlar bize odedi, geri veriyoruz
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, enemyFaction.Leader, totalPaidToUs);
                        InformationManager.DisplayMessage(new InformationMessage($"Negotiations collapsed. {totalPaidToUs} Denars paid by {enemyFaction.Name} during the ceasefire were refunded.", Colors.Red));
                    }
                    else if (totalPaidToUs < 0)
                    {
                        // Biz onlara odedik, geri aliyoruz
                        GiveGoldAction.ApplyBetweenCharacters(enemyFaction.Leader, Hero.MainHero, -totalPaidToUs);
                        InformationManager.DisplayMessage(new InformationMessage($"Negotiations collapsed. {-totalPaidToUs} Denars paid by us during the ceasefire were refunded.", Colors.Red));
                    }
                }
            }

            ClearCeasefireTracking(enemyFaction);
        }

        private void ClearCeasefireTracking(Kingdom enemyFaction)
        {
            _ceasefireStartTimes.Remove(enemyFaction);
            _ceasefireDailyTributeWeReceive.Remove(enemyFaction);
        }

        private void DistributeTributeReward(Kingdom payer, Kingdom receiver, int amount)
        {
            var clans = (receiver as Kingdom)?.Clans.Where(c => !c.IsEliminated).ToList();
            if (clans == null || clans.Count == 0) return;
            
            int perClan = amount / clans.Count;
            int remainder = amount % clans.Count;
            foreach (var clan in clans)
            {
                int pay = perClan + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;
                if (pay > 0) 
                {
                    if (clan.Leader == Hero.MainHero)
                    {
                        Hero.MainHero.Gold += pay;
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_pc_28}[Sava? Harac?] +{PAY} Denar").SetTextVariable("PAY", pay).ToString(), Colors.Green));
                    }
                    else
                    {
                        GiveGoldAction.ApplyBetweenCharacters(payer.Leader, clan.Leader, pay);
                    }
                }
            }
        }
        
        private void DistributeTributeDebt(Kingdom payer, Kingdom receiver, int amount)
        {
            var clans = (payer as Kingdom)?.Clans.Where(c => !c.IsEliminated).ToList();
            if (clans == null || clans.Count == 0) return;
            
            int perClan = amount / clans.Count;
            int remainder = amount % clans.Count;
            foreach (var clan in clans)
            {
                int pay = perClan + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;
                if (pay > 0) GiveGoldAction.ApplyBetweenCharacters(clan.Leader, receiver.Leader, pay);
            }
        }

        private int GetHagglePercentage(int optionIndex)
        {
            float ratio = Math.Min(1f, _ourScorePercent / 50f);
            float maxHaggle = (ratio * ratio) * 80f; // Ustelsel egri
            if (maxHaggle < 1f) maxHaggle = 1f;

            float step = maxHaggle / 4f;
            int pct = (int)Math.Round(step * optionIndex);
            return pct > 0 ? pct : 1; 
        }

        private int GetHaggleTension(int optionIndex)
        {
            if (optionIndex == 1) return 15;
            if (optionIndex == 2) return 25;
            if (optionIndex == 3) return 40;
            return 60;
        }

        private int GetTributeHagglePercentage(int optionIndex)
        {
            float ratio = Math.Min(1f, _ourScorePercent / 50f);
            float maxHaggle = (ratio * ratio) * 50f; 
            if (maxHaggle < 1f) maxHaggle = 1f;
            
            float step = maxHaggle / 4f;
            int pct = (int)Math.Round(step * optionIndex);
            return pct > 0 ? pct : 1;
        }

        private int GetTributeHaggleTension(int optionIndex)
        {
            if (optionIndex == 1) return 10;
            if (optionIndex == 2) return 18;
            if (optionIndex == 3) return 30;
            return 45;
        }
    }
}
