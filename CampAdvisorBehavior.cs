using TaleWorlds.SaveSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class CampAdvisorBehavior : CampaignBehaviorBase
    {
        private Settlement _advisorTown;
        private CharacterObject _advisorCharacter;
        private Dictionary<string, CampaignTime> _lastProphecyTimes = new Dictionary<string, CampaignTime>();
        private Dictionary<Kingdom, int> _kingdomCasualties = new Dictionary<Kingdom, int>();

        public Dictionary<string, CampaignTime> LastProphecyTimes 
        { 
            get { return _lastProphecyTimes ?? (_lastProphecyTimes = new Dictionary<string, CampaignTime>()); }
            set { _lastProphecyTimes = value; }
        }

        public Dictionary<Kingdom, int> KingdomCasualties 
        { 
            get { return _kingdomCasualties ?? (_kingdomCasualties = new Dictionary<Kingdom, int>()); }
            set { _kingdomCasualties = value; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in LastProphecyTimes.ToList()) { if (!(pair.Key != null)) LastProphecyTimes.Remove(pair.Key); }
                // _kingdomCasualties = KingdomCasualties
                //     .Where(pair => pair.Key != null && !pair.Key.IsEliminated)
                //     .ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            dataStore.SyncData("_advisorTown", ref _advisorTown);
            dataStore.SyncData("_lastProphecyTimes", ref _lastProphecyTimes);
            dataStore.SyncData("_kingdomCasualties", ref _kingdomCasualties);

            if (dataStore.IsLoading)
            {
                if (_lastProphecyTimes == null) _lastProphecyTimes = new Dictionary<string, CampaignTime>();
                if (_kingdomCasualties == null) _kingdomCasualties = new Dictionary<Kingdom, int>();
                
                _kingdomCasualties = _kingdomCasualties
                    .Where(pair => pair.Key != null && !pair.Key.IsEliminated)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter) { CheckAdvisorLocation(); }
        private void OnNewGameCreated(CampaignGameStarter starter) { CheckAdvisorLocation(); }

        private void OnDailyTick()
        {
            CheckAdvisorLocation();
        }

        private void CheckAdvisorLocation()
        {
            // Eer ehir yoksa veya ehir oyuncunun dmanysa / oyuncunun krallnda deilse yeni ehir se
            bool needsRelocation = false;
            if (_advisorTown == null || !_advisorTown.IsTown)
            {
                needsRelocation = true;
            }
            else
            {
                // Krallk mant: Oyuncunun krall varsa, ehir o krallkta olmal.
                if (Hero.MainHero.MapFaction != null && Hero.MainHero.MapFaction.IsKingdomFaction)
                {
                    if (_advisorTown.MapFaction != Hero.MainHero.MapFaction)
                        needsRelocation = true;
                }
                else
                {
                    // Oyuncu bamszsa dman ehirde olmasn
                    if (_advisorTown.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
                        needsRelocation = true;
                }
            }

            if (needsRelocation)
            {
                RelocateAdvisor();
            }
        }

        private void RelocateAdvisor()
        {
            List<Settlement> possibleTowns = new List<Settlement>();

            if (Hero.MainHero.MapFaction != null && Hero.MainHero.MapFaction.IsKingdomFaction)
            {
                possibleTowns = Settlement.All.Where(s => s.IsTown && s.MapFaction == Hero.MainHero.MapFaction).ToList();
            }

            if (possibleTowns.Count == 0)
            {
                // Bamsz veya kralln ehri yok
                possibleTowns = Settlement.All.Where(s => s.IsTown && !s.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)).ToList();
            }

            if (possibleTowns.Count > 0)
            {
                _advisorTown = possibleTowns[MBRandom.RandomInt(possibleTowns.Count)];
            }
            else
            {
                // ok zor durum, herhangi bir ehir
                _advisorTown = Settlement.All.FirstOrDefault(s => s.IsTown);
            }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent.AttackerSide?.LeaderParty?.MapFaction is Kingdom k1)
            {
                if (!_kingdomCasualties.ContainsKey(k1)) _kingdomCasualties[k1] = 0;
                _kingdomCasualties[k1] += mapEvent.AttackerSide.TroopCasualties;
            }
            if (mapEvent.DefenderSide?.LeaderParty?.MapFaction is Kingdom k2)
            {
                if (!_kingdomCasualties.ContainsKey(k2)) _kingdomCasualties[k2] = 0;
                _kingdomCasualties[k2] += mapEvent.DefenderSide.TroopCasualties;
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (_advisorCharacter == null)
            {
                _advisorCharacter = CharacterObject.All.FirstOrDefault(c => c.Occupation == Occupation.Townsfolk && c.Culture?.StringId == "empire");
                if (_advisorCharacter == null) _advisorCharacter = CharacterObject.PlayerCharacter;
            }

            // ============================
            // TAVERNA DYALOGU (BLGE ADAM NEREDE?)
            // ============================
            starter.AddPlayerLine("tavern_ask_advisor", "tavernkeeper_talk", "tavern_ask_advisor_response", 
                "{=rad_adv_01}They say a wise man who knows many things lives in seclusion around here... Do you know where he is?", 
                () => true, null, 100);

            starter.AddDialogLine("tavern_ask_advisor_response", "tavern_ask_advisor_response", "tavernkeeper_talk", 
                "{=rad_adv_02}Ah, you're talking about him... Finding him isn't easy. Last I heard, he had locked himself in a dark room in the keep of {ADVISOR_TOWN}.", 
                () => {
                    if (_advisorTown != null)
                    {
                        MBTextManager.SetTextVariable("ADVISOR_TOWN", _advisorTown.Name);
                        return true;
                    }
                    return false;
                }, null);

            // ============================
            // MEN (SADECE ADVISOR'UN BULUNDUU EHRDE)
            // ============================
            starter.AddGameMenuOption("town_keep", "town_keep_advisor", "{=rad_adv_03}Enter the Seclusion Room (Speak with the Wise Man)",
                (MenuCallbackArgs args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return Settlement.CurrentSettlement == _advisorTown;
                },
                (MenuCallbackArgs args) => {
                    CampaignMapConversation.OpenConversation(
                        new ConversationCharacterData(CharacterObject.PlayerCharacter),
                        new ConversationCharacterData(_advisorCharacter)
                    );
                }, false, 4);

            // ============================
            // ANA DYALOG
            // ============================
            starter.AddDialogLine("advisor_start", "start", "advisor_options", 
                "{=rad_adv_04}The decisions that shape the future are hidden in the shadows of the past, child. What do the winds of Calradia whisper today? What do you wish to know from me?", 
                () => Hero.OneToOneConversationHero == null && CharacterObject.OneToOneConversationCharacter == _advisorCharacter, null, 1000);

            starter.AddPlayerLine("advisor_ask_world", "advisor_options", "advisor_world_reading", 
                "{=rad_adv_05}What do you see in this picture? How do you interpret the course of the world?", null, null);

            starter.AddPlayerLine("advisor_ask_intel", "advisor_options", "advisor_intel_demand", 
                "{=rad_adv_06}What gossip reaches your ears? What is being talked about?", null, null);

            starter.AddPlayerLine("advisor_leave", "advisor_options", "close_window", 
                "{=rad_adv_07}This is enough for now. (Leave)", null, null);

            // ============================
            // DNYA OKUMASI (CRETSZ)
            // ============================
            starter.AddDialogLine("advisor_world_reading", "advisor_world_reading", "advisor_options", 
                "{=rad_auto_336}{ADVISOR_WORLD_READING}", 
                () => {
                    MBTextManager.SetTextVariable("ADVISOR_WORLD_READING", GenerateWorldReading());
                    return true;
                }, null);

            // ============================
            // STHBARAT (MSTK - CRETL)
            // ============================
            starter.AddDialogLine("advisor_intel_demand", "advisor_intel_demand", "advisor_intel_money_options", 
                "{=rad_adv_10}You want to hear what the birds whisper and the earth trembles, huh? But my birds are hungry, my network has needs. If you donate 1000 Denars, I can lift the veil of secrets.", 
                null, null);

            starter.AddPlayerLine("advisor_intel_pay", "advisor_intel_money_options", "advisor_intel_give", 
                "{=rad_adv_11}Here is 1000 Denars. Meet the needs of your network.", 
                () => Hero.MainHero.Gold >= 1000, 
                () => GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, true));

            starter.AddPlayerLine("advisor_intel_decline", "advisor_intel_money_options", "advisor_intel_decline_npc", 
                "{=rad_adv_08}I can't afford that right now.", null, null);

            starter.AddDialogLine("advisor_intel_decline_npc", "advisor_intel_decline_npc", "advisor_options", 
                "{=rad_adv_09}I understand. Birds can't fly when they're hungry. What else would you like to know?", null, null);

            starter.AddDialogLine("advisor_intel_give", "advisor_intel_give", "advisor_options", 
                "{=rad_auto_337}{ADVISOR_INTEL_TEXT}", 
                () => {
                    MBTextManager.SetTextVariable("ADVISOR_INTEL_TEXT", GenerateMysticIntelligence());
                    return true;
                }, null);
        }

        // ============================================
        // 1. DNYA OKUMA SSTEM (Dinamik)
        // ============================================
        private string GenerateWorldReading()
        {
            int casualtyScore = 0;
            Kingdom worstCasualtyKingdom = null;
            if (_kingdomCasualties.Count > 0)
            {
                worstCasualtyKingdom = _kingdomCasualties.OrderByDescending(k => k.Value).First().Key;
                casualtyScore = (_kingdomCasualties[worstCasualtyKingdom] > 5000) ? 80 : 30;
            }

            int karmaScore = 0;
            int honor = Hero.MainHero.GetTraitLevel(DefaultTraits.Honor);
            int mercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (honor < 0 || mercy < 0) karmaScore = 90;
            else if (honor > 0 && mercy > 0) karmaScore = 60;

            int famineScore = 0;
            Town starvingTown = Town.AllTowns.FirstOrDefault(t => t.FoodStocks < 50 && t.Prosperity < 2000);
            if (starvingTown != null) famineScore = 70;

            if (karmaScore >= casualtyScore && karmaScore >= famineScore)
            {
                if (honor < 0 || mercy < 0)
                {
                    TextObject t = new TextObject("{=rad_adv_12}The smell of blood seeping from your sword reaches all the way here. The heads you take may bring you a throne, but how do you silence the screams of those headless bodies when you sleep at night? Calradia doesn't call you 'Hero' {HERO_NAME}, they call you 'Nightmare'.");
                    t.SetTextVariable("HERO_NAME", Hero.MainHero.Name);
                    return t.ToString();
                }
                else
                {
                    return new TextObject("{=rad_adv_13}I heard you released the enemy you captured. Today's cruel lords call this 'weakness', but they are wrong. Mercy is a greater power than swinging a sword. Trees break, but flexible reeds survive the storm.").ToString();
                }
            }
            else if (famineScore >= casualtyScore)
            {
                TextObject t = new TextObject("{=rad_adv_14}The jingling sounds of caravans coming from the south have ceased. Bread is running out in the bakeries of {TOWN_NAME}, merchants are hiding their goods. A hungry person knows no rules, {HERO_NAME}. Soon, it won't be swords talking in that city, but the rebellion brought by hunger.");
                t.SetTextVariable("TOWN_NAME", starvingTown.Name);
                t.SetTextVariable("HERO_NAME", Hero.MainHero.Name);
                return t.ToString();
            }
            else if (worstCasualtyKingdom != null)
            {
                TextObject t = new TextObject("{=rad_adv_15}Every child born in the households of {KINGDOM_NAME} falls to the ground before they can hold a sword. Even if they think they are winning the wars, their people are perishing. If this war continues a little longer, they won't find men to plow the fields.");
                t.SetTextVariable("KINGDOM_NAME", worstCasualtyKingdom.Name);
                return t.ToString();
            }
            
            return new TextObject("{=rad_adv_16}The world seems quiet right now, but this is nothing more than the calm before the storm.").ToString();
        }

        // ============================================
        // 2. MSTK STHBARAT SSTEM (Olaslk Dili)
        // ============================================
        private string GenerateMysticIntelligence()
        {
            List<Tuple<int, string, string>> intelOptions = new List<Tuple<int, string, string>>();

            if (CheckCooldown("army_intel"))
            {
                var armies = MobileParty.All.Where(p => p.Army != null && p.Army.LeaderParty == p && p.DefaultBehavior == AiBehavior.BesiegeSettlement && p.TargetSettlement != null);
                if (armies.Any())
                {
                    var targetArmy = armies.First();
                    int score = (targetArmy.TargetSettlement.MapFaction == Hero.MainHero.MapFaction) ? 100 : 40;
                    TextObject t = new TextObject("{=rad_adv_17}The wind brings dark clouds... Most likely {ARMY_LEADER} has gathered the clans. It seems their path will lead to the walls of {TARGET_TOWN}. Siege towers may appear on the horizon.");
                    t.SetTextVariable("ARMY_LEADER", targetArmy.LeaderHero != null ? targetArmy.LeaderHero.Name.ToString() : "a commander");
                    t.SetTextVariable("TARGET_TOWN", targetArmy.TargetSettlement.Name);
                    intelOptions.Add(new Tuple<int, string, string>(score, "army_intel", t.ToString()));
                }
            }

            if (CheckCooldown("defection_intel"))
            {
                var angryClans = Clan.All.Where(c => !c.IsMinorFaction && c.Kingdom != null && c.Leader != null && c.Kingdom.Leader != c.Leader && c.Fiefs.Count == 0 && c.Leader.Gold < 20000);
                if (angryClans.Any())
                {
                    var defectingClan = angryClans.First();
                    TextObject t = new TextObject("{=rad_adv_18}Another loyalty may be about to break... The leader of the {CLAN_NAME} clan, {CLAN_LEADER}, is cutting his ties to the kingdom. His pockets are empty, he has no property. Don't be surprised if he changes his banner soon and draws his sword against his old friends.");
                    t.SetTextVariable("CLAN_NAME", defectingClan.Name);
                    t.SetTextVariable("CLAN_LEADER", defectingClan.Leader.Name);
                    intelOptions.Add(new Tuple<int, string, string>(70, "defection_intel", t.ToString()));
                }
            }

            if (CheckCooldown("rebellion_intel"))
            {
                var angryTowns = Town.AllTowns.Where(t => t.Loyalty < 25);
                if (angryTowns.Any())
                {
                    var town = angryTowns.First();
                    int score = (town.MapFaction != Hero.MainHero.MapFaction) ? 60 : 80;
                    TextObject t = new TextObject("{=rad_adv_19}Just as red-hot iron makes a sound when plunged into water, the people of {TOWN_NAME} are boiling like that. Very soon, it might not be the kingdom's guards patrolling the streets of that city, but irregular gangs formed by the people themselves...");
                    t.SetTextVariable("TOWN_NAME", town.Name);
                    intelOptions.Add(new Tuple<int, string, string>(score, "rebellion_intel", t.ToString()));
                }
            }

            if (intelOptions.Count > 0)
            {
                var bestIntel = intelOptions.OrderByDescending(x => x.Item1).First();
                _lastProphecyTimes[bestIntel.Item2] = CampaignTime.Now; 
                return bestIntel.Item3;
            }

            return new TextObject("{=rad_adv_20}My birds can't see any new danger on the horizon lately. The darkness seems to have scattered a bit.").ToString();
        }

        private bool CheckCooldown(string intelType)
        {
            if (_lastProphecyTimes.TryGetValue(intelType, out CampaignTime lastTime))
            {
                if (lastTime.ElapsedDaysUntilNow < 7f)
                    return false;
            }
            return true;
        }
    }
}
