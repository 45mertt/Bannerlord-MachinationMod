using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    // Spekülasyon Tipleri
    public enum SpeculationType
    {
        Plague,           // Salgın (Fiyatları ve refahı düşürür)
        BanditThreat,     // Haydut Baskını (Hammadde girmez, fiyat düşer)
        WarRumor,         // Savaş Dedikodusu (Demir/Silah fiyatı fırlar, gıda fırlar)
        Tournament,       // Turnuva (Bira/Şarap fiyatı fırlar)
        TradeBoom         // Ticaret Patlaması (Her şeyin fiyatı fırlar)
    }

    public class SpeculationData
    {
        public SpeculationData() {}
        [SaveableField(1)] public SpeculationType Type;
        [SaveableField(2)] public CampaignTime EndTime;
        [SaveableField(3)] public string SettlementId;
        [SaveableField(4)] public bool IsActive;

        public SpeculationData(SpeculationType type, CampaignTime endTime, string settlementId)
        {
            Type = type;
            EndTime = endTime;
            SettlementId = settlementId;
            IsActive = true;
        }
    }

    public class WorkshopSpeculationBehavior : CampaignBehaviorBase
    {
        // Sehir kimligi -> Aktif Spekulasyon
        private Dictionary<string, SpeculationData> _activeSpeculations = new Dictionary<string, SpeculationData>();
        public Dictionary<string, SpeculationData> ActiveSpeculations { get { return _activeSpeculations ?? (_activeSpeculations = new Dictionary<string, SpeculationData>()); } set { _activeSpeculations = value; } }

        // Ikna degiskenleri (Class59 mimarisi)
        private List<PersuasionTask> _currentTasks;
        private string _targetTownIdForPersuasion;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in ActiveSpeculations.ToList()) { if (!(pair.Key != null && pair.Value != null)) ActiveSpeculations.Remove(pair.Key); }
            }
            dataStore.SyncData("_activeSpeculations", ref _activeSpeculations);
        }

        private void OnDailyTick()
        {
            // Rastgele yeni spekülasyonlar üretme (Günlük %5 ihtimalle bir şehre vurur)
            if (MBRandom.RandomFloat < 0.05f)
            {
                var randomTown = Town.AllTowns.GetRandomElementWithPredicate(t => t.IsTown);
                if (randomTown != null && !_activeSpeculations.ContainsKey(randomTown.StringId))
                {
                    GenerateRandomSpeculation(randomTown);
                }
            }

            // Süresi bitenleri temizle ve etkileri uygula
            List<string> toRemove = new List<string>();
            foreach (var kvp in _activeSpeculations)
            {
                if (kvp.Value.EndTime.IsPast)
                {
                    toRemove.Add(kvp.Key);
                }
                else if (kvp.Value.IsActive)
                {
                    ApplySpeculationEffects(kvp.Key, kvp.Value);
                }
            }

            foreach (var key in toRemove)
            {
                _activeSpeculations.Remove(key);
            }
        }

        private void GenerateRandomSpeculation(Town town)
        {
            Array values = Enum.GetValues(typeof(SpeculationType));
            SpeculationType randomType = (SpeculationType)values.GetValue(MBRandom.RandomInt(values.Length));
            
            // Spekülasyon 1 ila 3 hafta sürer
            var duration = CampaignTime.Days(MBRandom.RandomInt(7, 21));
            var spec = new SpeculationData(randomType, CampaignTime.Now + duration, town.StringId);
            _activeSpeculations[town.StringId] = spec;

            string msg = "";
            switch (randomType)
            {
                case SpeculationType.Plague:
                    msg = $"Rumors of a plague outbreak have spread in {town.Name}! Markets are crashing.";
                    break;
                case SpeculationType.BanditThreat:
                    msg = $"It is rumored that a huge bandit army has gathered around {town.Name}. Trade came to a halt.";
                    break;
                case SpeculationType.WarRumor:
                    msg = $"There are rumors that a big war will soon break out in {town.Name}. Weapons and food are stockpiled.";
                    break;
                case SpeculationType.Tournament:
                    msg = $"It is said that a tremendous championship will be held in {town.Name}. Entertainment and liquor stocks skyrocketed!";
                    break;
                case SpeculationType.TradeBoom:
                    msg = $"{town.Name} is experiencing its golden age! The guilds whisper that trade will boom.";
                    break;
            }

            if (town.MapFaction == Hero.MainHero.MapFaction || town.IsOwnerUnassigned == false)
            {
                InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Magenta));
            }
        }

        private void ApplySpeculationEffects(string townId, SpeculationData spec)
        {
            var town = Town.AllTowns.FirstOrDefault(t => t.StringId == townId);
            if (town == null) return;

            // Etkiler
            if (spec.Type == SpeculationType.Plague || spec.Type == SpeculationType.BanditThreat)
            {
                town.Prosperity -= 15f; // Her gün refah düşer
            }
            else if (spec.Type == SpeculationType.TradeBoom)
            {
                town.Prosperity += 10f;
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // İleri gelenlerle konuşurken spekülasyon varsa müdahale seçeneği
            starter.AddPlayerLine("speculation_start", "hero_main_options", "speculation_intro",
                "{=rad_auto_108}Rumors in the city are destroying the market. We must put an end to this.",
                Condition_CanIntervene,
                () => { _targetTownIdForPersuasion = Settlement.CurrentSettlement.StringId; });

            starter.AddDialogLine("speculation_intro", "speculation_intro", "speculation_options",
                "{=rad_auto_101}You are right, but it is not easy to convince the public. Why should we believe you?",
                null, SetupPersuasion);

            // Oyuncunun Argümanları (4 seçenek)
            starter.AddPlayerLine("spec_arg_1", "speculation_options", "speculation_reaction", "{=!}{ARG1_TEXT} {ARG1_CHANCE}",
                Condition_SetupOption1Text, Consequence_Option1, 100, Condition_Clickable1, Delegate_GetOption1Args);

            starter.AddPlayerLine("spec_arg_2", "speculation_options", "speculation_reaction", "{=!}{ARG2_TEXT} {ARG2_CHANCE}",
                Condition_SetupOption2Text, Consequence_Option2, 100, Condition_Clickable2, Delegate_GetOption2Args);

            starter.AddPlayerLine("spec_arg_3", "speculation_options", "speculation_reaction", "{=!}{ARG3_TEXT} {ARG3_CHANCE}",
                Condition_SetupOption3Text, Consequence_Option3, 100, Condition_Clickable3, Delegate_GetOption3Args);

            starter.AddPlayerLine("spec_arg_4", "speculation_options", "speculation_reaction", "{=!}{ARG4_TEXT} {ARG4_CHANCE}",
                Condition_SetupOption4Text, Consequence_Option4, 100, Condition_Clickable4, Delegate_GetOption4Args);

            // Vazgeçme
            starter.AddPlayerLine("spec_arg_exit", "speculation_options", "close_window", "{=rad_auto_109}I gave up, let the rumors continue.", null,
                () => { ConversationManager.EndPersuasion(); });

            // Reaksiyon ve Sonuçlar
            starter.AddDialogLine("speculation_reaction_line", "speculation_reaction", "speculation_next", "{=rad_auto_102}{PERSUASION_REACTION}", Condition_ReactionAndApply, null);
            starter.AddDialogLine("speculation_next_line", "speculation_next", "speculation_options", "{=rad_auto_103}Do you have another argument?", Condition_ShouldContinue, null);

            starter.AddDialogLine("speculation_success", "speculation_next", "close_window", "{=rad_auto_104}You convinced me. I will tell the truth to the guilds and the public.",
                Condition_IsSuccess,
                () =>
                {
                    EndSpeculationSuccessfully();
                    ConversationManager.EndPersuasion();
                });
            
            starter.AddDialogLine("speculation_fail", "speculation_next", "close_window", "{=rad_auto_105}What you say is not believable at all. Rumors are real!",
                Condition_IsFailure,
                () =>
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_114}You couldn't stop the rumors!").ToString(), Colors.Red));
                    ConversationManager.EndPersuasion();
                });

            // AKBABA LORD DİYALOGLARI
            starter.AddPlayerLine("vulture_lord_intro", "hero_main_options", "vulture_lord_offer",
                "{=rad_auto_110}I heard you are interested in investments in this city.",
                Condition_IsVultureLordAvailable,
                null);

            starter.AddDialogLine("vulture_lord_offer_negative", "vulture_lord_offer", "vulture_lord_answer",
                "{=rad_auto_106}You see the current market... {SPECULATION_REASON}. Your workshop will soon go bankrupt. I'll give you {VULTURE_PRICE} Denar, sell it immediately and get rid of it.",
                Condition_VultureNegativeOffer,
                null);

            starter.AddDialogLine("vulture_lord_offer_positive", "vulture_lord_offer", "vulture_lord_answer",
                "{=rad_auto_107}The market is on fire! {SPECULATION_REASON}. I can leave my profitable workshop here to you for {VULTURE_PRICE} Denars, don't miss the opportunity.",
                Condition_VulturePositiveOffer,
                null);

            // Oyuncunun Cevapları
            starter.AddPlayerLine("vulture_lord_accept_sell", "vulture_lord_answer", "close_window",
                "{=rad_auto_111}I accept, take it and keep it.",
                Condition_CanSellToVulture,
                () => { SellWorkshopToVulture(); });

            starter.AddPlayerLine("vulture_lord_accept_buy", "vulture_lord_answer", "close_window",
                "{=rad_auto_112}I agree, I buy the workshop.",
                Condition_CanBuyFromVulture,
                () => { BuyWorkshopFromVulture(); });

            starter.AddPlayerLine("vulture_lord_reject", "vulture_lord_answer", "close_window",
                "{=rad_auto_113}Are you making fun of me? Never!",
                null,
                null);
        }

        // AKBABA LORD KOSULLARI
        private bool Condition_IsVultureLordAvailable()
        {
            if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || !hero.IsLord || hero.Clan == Hero.MainHero.Clan) return false;
            
            return _activeSpeculations.ContainsKey(Settlement.CurrentSettlement.StringId);
        }

        private bool Condition_VultureNegativeOffer()
        {
            if (!_activeSpeculations.TryGetValue(Settlement.CurrentSettlement.StringId, out var spec)) return false;
            if (spec.Type != SpeculationType.Plague && spec.Type != SpeculationType.BanditThreat) return false;
            
            // Oyuncunun atölyesi var mı?
            bool hasWorkshop = Settlement.CurrentSettlement.Town.Workshops.Any(w => w.Owner == Hero.MainHero);
            if (!hasWorkshop) return false;

            MBTextManager.SetTextVariable("SPECULATION_REASON", spec.Type == SpeculationType.Plague ? "The epidemic is decimating the city" : "Bandits are everywhere");
            MBTextManager.SetTextVariable("VULTURE_PRICE", 5000);
            return true;
        }

        private bool Condition_VulturePositiveOffer()
        {
            if (!_activeSpeculations.TryGetValue(Settlement.CurrentSettlement.StringId, out var spec)) return false;
            if (spec.Type != SpeculationType.TradeBoom && spec.Type != SpeculationType.Tournament && spec.Type != SpeculationType.WarRumor) return false;

            // Lordun atölyesi var mı?
            var hero = Hero.OneToOneConversationHero;
            bool lordHasWorkshop = Settlement.CurrentSettlement.Town.Workshops.Any(w => w.Owner == hero);
            if (!lordHasWorkshop) return false;

            MBTextManager.SetTextVariable("SPECULATION_REASON", "Look, trade is booming, everyone is after goods.");
            MBTextManager.SetTextVariable("VULTURE_PRICE", 35000);
            return true;
        }

        private bool Condition_CanSellToVulture()
        {
            return Condition_VultureNegativeOffer();
        }

        private bool Condition_CanBuyFromVulture()
        {
            if (!Condition_VulturePositiveOffer()) return false;
            return Hero.MainHero.Gold >= 35000;
        }

        private void SellWorkshopToVulture()
        {
            var ws = Settlement.CurrentSettlement.Town.Workshops.FirstOrDefault(w => w.Owner == Hero.MainHero);
            if (ws != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.OneToOneConversationHero, Hero.MainHero, 5000, true);
                ChangeOwnerOfWorkshopAction.ApplyByDeath(ws, Hero.OneToOneConversationHero);
                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_115}You sold your workshop to an opportunistic lord.").ToString(), Colors.Red));
            }
        }

        private void BuyWorkshopFromVulture()
        {
            var hero = Hero.OneToOneConversationHero;
            var ws = Settlement.CurrentSettlement.Town.Workshops.FirstOrDefault(w => w.Owner == hero);
            if (ws != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, 35000, true);
                ChangeOwnerOfWorkshopAction.ApplyByDeath(ws, Hero.MainHero);
                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_116}You bought the workshop at an exorbitant price.").ToString(), Colors.Green));
            }
        }

        private bool Condition_CanIntervene()
        {
            if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
            if (Hero.OneToOneConversationHero == null || !Hero.OneToOneConversationHero.IsNotable) return false;
            
            return _activeSpeculations.ContainsKey(Settlement.CurrentSettlement.StringId);
        }

        private void SetupPersuasion()
        {
            _currentTasks = new List<PersuasionTask>();
            PersuasionTask task = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));

            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Authoritarian, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("No one can harm you while you are under my protection.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, PersuasionArgumentStrength.Easy, false, new TextObject("We must calm the public, panic harms everyone.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Hard, false, new TextObject("We can share our losses among the guilds.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("I will cut out the tongues of those who spread this lie.")));

            _currentTasks.Add(task);
            ConversationManager.StartPersuasion(3f, 1f, 0f, 2f, 2f); // 3 adımlı
        }

        private void EndSpeculationSuccessfully()
        {
            if (_activeSpeculations.ContainsKey(_targetTownIdForPersuasion))
            {
                _activeSpeculations.Remove(_targetTownIdForPersuasion);
                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_117}Speculation successfully blocked! Markets returned to normal.").ToString(), Colors.Green));
            }
        }

        // DELEGATE METHODLARI (Class59 Mimarisi)
        private bool Condition_SetupOption1Text() { return SetupOptionText(0, "ARG1_TEXT", "ARG1_CHANCE"); }
        private bool Condition_SetupOption2Text() { return SetupOptionText(1, "ARG2_TEXT", "ARG2_CHANCE"); }
        private bool Condition_SetupOption3Text() { return SetupOptionText(2, "ARG3_TEXT", "ARG3_CHANCE"); }
        private bool Condition_SetupOption4Text() { return SetupOptionText(3, "ARG4_TEXT", "ARG4_CHANCE"); }

        private void Consequence_Option1() { GetOptionArgs(0)?.BlockTheOption(true); }
        private void Consequence_Option2() { GetOptionArgs(1)?.BlockTheOption(true); }
        private void Consequence_Option3() { GetOptionArgs(2)?.BlockTheOption(true); }
        private void Consequence_Option4() { GetOptionArgs(3)?.BlockTheOption(true); }

        private bool Condition_Clickable1(out TextObject h) { return CheckClickable(0, out h); }
        private bool Condition_Clickable2(out TextObject h) { return CheckClickable(1, out h); }
        private bool Condition_Clickable3(out TextObject h) { return CheckClickable(2, out h); }
        private bool Condition_Clickable4(out TextObject h) { return CheckClickable(3, out h); }

        private PersuasionOptionArgs Delegate_GetOption1Args() { return GetOptionArgs(0); }
        private PersuasionOptionArgs Delegate_GetOption2Args() { return GetOptionArgs(1); }
        private PersuasionOptionArgs Delegate_GetOption3Args() { return GetOptionArgs(2); }
        private PersuasionOptionArgs Delegate_GetOption4Args() { return GetOptionArgs(3); }

        private PersuasionOptionArgs GetOptionArgs(int index)
        {
            var task = _currentTasks?.FirstOrDefault();
            return (task != null && task.Options.Count > index) ? task.Options[index] : null;
        }

        private bool SetupOptionText(int index, string textVar, string chanceVar)
        {
            var opt = GetOptionArgs(index);
            if (opt == null) return false;
            MBTextManager.SetTextVariable(textVar, opt.Line);
            MBTextManager.SetTextVariable(chanceVar, PersuasionHelper.ShowSuccess(opt));
            return true;
        }

        private bool CheckClickable(int index, out TextObject hintText)
        {
            hintText = new TextObject("");
            var opt = GetOptionArgs(index);
            if (opt == null) return false;
            if (opt.IsBlocked)
            {
                hintText = new TextObject("{=rad_wsb_01}You have already used this option.");
                return false;
            }
            return true;
        }

        private bool Condition_ShouldContinue() => !ConversationManager.GetPersuasionProgressSatisfied() && !ConversationManager.GetPersuasionIsFailure();
        private bool Condition_IsSuccess() => ConversationManager.GetPersuasionProgressSatisfied();
        private bool Condition_IsFailure() => ConversationManager.GetPersuasionIsFailure();

        private bool Condition_ReactionAndApply()
        {
            var chosen = ConversationManager.GetPersuasionChosenOptions().LastOrDefault();
            if (chosen != null)
            {
                var task = _currentTasks.FirstOrDefault(t => t.Options.Contains(chosen.Item1));
                float diff = Campaign.Current.Models.PersuasionModel.GetDifficulty(PersuasionDifficulty.Medium);
                float moveToNext, blockRandom;
                Campaign.Current.Models.PersuasionModel.GetEffectChances(chosen.Item1, out moveToNext, out blockRandom, diff);

                if (task != null) task.ApplyEffects(moveToNext, blockRandom);

                var result = chosen.Item2;
                if (result == PersuasionOptionResult.Success || result == PersuasionOptionResult.CriticalSuccess)
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Hmm... A logical argument."));
                else if (result == PersuasionOptionResult.CriticalFailure)
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Don't talk nonsense! Who believes this?"));
                else
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Doesn't seem very reasonable to me..."));
            }
            return true;
        }
    }
}

