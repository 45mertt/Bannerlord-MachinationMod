using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace RebellionsAndDemographics
{
    public class WarCabinetDialogBehavior : CampaignBehaviorBase
    {
        private List<PersuasionTask> _currentTasks;
        private Hero _lastConversedHero = null;
        private int _currentTaskIndex = 0;
        private float _currentMoodModifier = 0f;
        private int _talkedLordsCount = 0;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Giriş: Zaten konuşulduysa
            starter.AddPlayerLine("wc_already_talked", "start", "close_window",
                "{=wc_at}My Liege, we have already discussed this matter. My vote is cast.",
                Condition_AlreadyPersuaded,
                CleanUp_Conversation,
                2000);

            // Giriş: Normal Başlangıç
            starter.AddPlayerLine("wc_start", "start", "wc_intro",
                "{=wc_s}{INTRO_TEXT}",
                Condition_CanSeeLine,
                Consequence_InitStart,
                1000);

            // Lordun Cevabı
            starter.AddDialogLine("wc_reply", "wc_intro", "wc_persuade",
                "{=wc_r}{REPLY_TEXT}",
                null, null);

            // --- İKNA SEÇENEKLERİ (1, 2, 3, 4) ---
            starter.AddPlayerLine("wc_opt_1", "wc_persuade", "wc_result", "{=!}{WC_OPT_1} {WC_CHANCE_1}", Condition_SetupOption1Text, Consequence_Option1, 100, Condition_Clickable1, Delegate_GetOption1Args);
            starter.AddPlayerLine("wc_opt_2", "wc_persuade", "wc_result", "{=!}{WC_OPT_2} {WC_CHANCE_2}", Condition_SetupOption2Text, Consequence_Option2, 100, Condition_Clickable2, Delegate_GetOption2Args);
            starter.AddPlayerLine("wc_opt_3", "wc_persuade", "wc_result", "{=!}{WC_OPT_3} {WC_CHANCE_3}", Condition_SetupOption3Text, Consequence_Option3, 100, Condition_Clickable3, Delegate_GetOption3Args);
            // Yedek seçenek (gerekirse)
            starter.AddPlayerLine("wc_opt_4", "wc_persuade", "wc_result", "{=!}{WC_OPT_4} {WC_CHANCE_4}", Condition_SetupOption4Text, Consequence_Option4, 100, Condition_Clickable4, Delegate_GetOption4Args);

            // Vazgeçme
            starter.AddPlayerLine("wc_leave", "wc_persuade", "close_window",
                "{=wc_l}I need more time to think.",
                null,
                CleanUp_Conversation);

            // --- TEPKİLER VE SONUÇLAR ---

            // Ara Tepki (Hmm, mantıklı... vs)
            starter.AddDialogLine("wc_res_line", "wc_result", "wc_check_progress",
                "{=wc_reaction_line}{PERSUASION_REACTION}",
                Condition_ReactionAndApply,
                null);

            // Kritik Başarısızlık (Kesin Red)
            starter.AddDialogLine("wc_crit_fail_end", "wc_check_progress", "close_window",
                "{=wc_crit_refuse}This is madness! I will absolutely not support this.",
                Condition_IsCriticalFail,
                Consequence_Failure_ScoreCalculation);

            // Genel Başarısızlık (İkna edemedin ama kızmadı)
            starter.AddDialogLine("wc_fail_general", "wc_check_progress", "close_window",
                "{=wc_fail_polite}You make some valid points, but I remain unconvinced. I cannot vote for this.",
                Condition_IsGeneralFailure,
                Consequence_Failure_ScoreCalculation,
                1000);

            // Başarı (İkna oldu)
            starter.AddDialogLine("wc_success", "wc_check_progress", "close_window",
                "{=wc_succ}Very well, my Liege. Your wisdom has swayed me. You have my full support.",
                Condition_IsSuccess,
                Consequence_Success_ScoreCalculation);

            // Devam (Sıradaki soruya geç)
            starter.AddDialogLine("wc_continue", "wc_check_progress", "wc_persuade",
                "{=wc_cont}I see your point. But there is another concern... [if:convo_thinking]",
                Condition_ShouldContinue,
                null);
        }

        private bool Condition_CanSeeLine()
        {
            if (Mission.Current == null || !Mission.Current.HasMissionBehavior<WarCabinetMissionLogic>()) return false;
            if (Hero.OneToOneConversationHero == null) return false;
            if (WarCabinetData.CurrentType != WarCabinetData.CouncilType.ProvokeWar) return false;
            return true;
        }

        private void Consequence_InitStart()
        {
            if (_talkedLordsCount > 200) _talkedLordsCount = 0;

            _lastConversedHero = Hero.OneToOneConversationHero;
            // Ruh hali değişkeni
            _currentMoodModifier = MBRandom.RandomFloatRanged(-1.0f, 1.0f);

            // Görevleri tam olarak oluştur
            CreatePersuasionTasks();

            // Metinleri hazırla
            MBTextManager.SetTextVariable("INTRO_TEXT", new TextObject("{=wc_intro}Your Majesty! Our armies are at your command. What are your orders?"));
            MBTextManager.SetTextVariable("REPLY_TEXT", new TextObject("{=wc_reply_doubt}I have doubts about this path. Why is it necessary?"));

            // İkna sistemini başlat
            if (!ConversationManager.GetPersuasionIsActive())
            {
                ConversationManager.StartPersuasion(3f, 1f, 0f, 2f, 2f);
            }
        }

        // --- TAM İKNA AĞACI (Military, Economic, Political) ---
        private void CreatePersuasionTasks()
        {
            _currentTasks = new List<PersuasionTask>();
            _currentTaskIndex = 0;

            // --- GÖREV 1: ASKERİ GEREKLİLİK ---
            var task1 = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));
            task1.SpokenLine = new TextObject("{=wc_t1_q}Our forces are tired. Why risk open conflict now?");

            // Seçenek A: Liderlik / Cesaret (Zor ama etkili)
            task1.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Valor, TraitEffect.Positive),
                false, new TextObject("{=wc_t1_a}A show of force is the only language they understand!")));

            // Seçenek B: Taktik / Hesapçılık (Normal)
            task1.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Calculating, TraitEffect.Positive),
                true, new TextObject("{=wc_t1_b}Strategically, striking now prevents a larger threat later.")));

            // Seçenek C: Haydutluk / Merhamet (Negatif - Kirli Savaş)
            task1.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Roguery, DefaultTraits.Mercy, TraitEffect.Negative,
                GetDynamicStrength(DefaultTraits.Mercy, TraitEffect.Negative),
                false, new TextObject("{=wc_t1_c}We strike while they sleep. It will be a massacre, not a battle.")));

            _currentTasks.Add(task1);

            // --- GÖREV 2: EKONOMİK DURUM ---
            var task2 = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));
            task2.SpokenLine = new TextObject("{=wc_t2_q}And the cost? The treasury cannot bleed forever.");

            // Seçenek A: İdare / Cömertlik
            task2.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Steward, DefaultTraits.Generosity, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Generosity, TraitEffect.Positive),
                true, new TextObject("{=wc_t2_a}I have secured new supply lines. We are well provisioned.")));

            // Seçenek B: Ticaret / Hesapçılık
            task2.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Calculating, TraitEffect.Positive),
                false, new TextObject("{=wc_t2_b}The spoils of war will outweigh the costs tenfold.")));

            // Seçenek C: Cazibe / Onur
            task2.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Honor, TraitEffect.Positive),
                false, new TextObject("{=wc_t2_c}Our duty is not measured in gold, but in integrity.")));

            _currentTasks.Add(task2);

            // --- GÖREV 3: POLİTİK İSTİKRAR ---
            var task3 = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));
            task3.SpokenLine = new TextObject("{=wc_t3_q}The other clans may see this as tyranny. How do we ensure their loyalty?");

            // Seçenek A: Cazibe / Merhamet
            task3.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Mercy, TraitEffect.Positive),
                true, new TextObject("{=wc_t3_a}We shall show clemency to those who surrender, proving our benevolence.")));

            // Seçenek B: Liderlik / Hesapçılık
            task3.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Calculating, TraitEffect.Positive),
                false, new TextObject("{=wc_t3_b}We will promise them land and titles from the conquered territories.")));

            // Seçenek C: İzizcilik / Cesaret
            task3.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Scouting, DefaultTraits.Valor, TraitEffect.Positive,
                GetDynamicStrength(DefaultTraits.Valor, TraitEffect.Positive),
                false, new TextObject("{=wc_t3_c}They respect strength. Victory will silence any dissent.")));

            _currentTasks.Add(task3);
        }

        private void Consequence_Success_ScoreCalculation()
        {
            CleanUp_Conversation();
            WarCabinetData.PersuadedLords.Add(Hero.OneToOneConversationHero);

            int points = 3;
            WarCabinetData.AccumulatedScore += points;
            _talkedLordsCount++;

            int currentSupport = WarCabinetData.InitialSupportPercentage + WarCabinetData.AccumulatedScore;
            currentSupport = MBMath.ClampInt(currentSupport, 0, 100);

            // {=wc_msg_convinced}{HERO} convinced! (+3% Support).
            TextObject msg = new TextObject("{=wc_msg_convinced}{HERO} convinced! (+3% Support).");
            msg.SetTextVariable("HERO", Hero.OneToOneConversationHero.Name);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));

            CheckFinalOutcome();
        }

        private void Consequence_Failure_ScoreCalculation()
        {
            CleanUp_Conversation();
            WarCabinetData.PersuadedLords.Add(Hero.OneToOneConversationHero);

            int penalty = 3;
            WarCabinetData.AccumulatedScore -= penalty;
            _talkedLordsCount++;

            int currentSupport = WarCabinetData.InitialSupportPercentage + WarCabinetData.AccumulatedScore;
            currentSupport = MBMath.ClampInt(currentSupport, 0, 100);

            // {=wc_msg_refused}{HERO} refused! (-3% Support).
            TextObject msg = new TextObject("{=wc_msg_refused}{HERO} refused! (-3% Support).");
            msg.SetTextVariable("HERO", Hero.OneToOneConversationHero.Name);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));

            CheckFinalOutcome();
        }

        private void CheckFinalOutcome()
        {
            var kingdom = Clan.PlayerClan.Kingdom;
            if (kingdom == null) return;

            var activeLords = kingdom.Clans
                .Where(c => c != Clan.PlayerClan && c.Leader != null && !c.IsEliminated && c.Leader.IsAlive && !c.Leader.IsPrisoner)
                .Select(c => c.Leader)
                .ToList();

            int totalPossibleLords = activeLords.Count;
            int maxPossibleScore = totalPossibleLords * 3;
            int threshold = (int)(maxPossibleScore * 0.6f);

            // {=wc_msg_score}Votes Cast: {COUNT}/{TOTAL} - Current Score: {SCORE} (Needed: {NEED})
            TextObject msg = new TextObject("{=wc_msg_score}Votes Cast: {COUNT}/{TOTAL} - Current Score: {SCORE} (Needed: {NEED})");
            msg.SetTextVariable("COUNT", _talkedLordsCount);
            msg.SetTextVariable("TOTAL", totalPossibleLords);
            msg.SetTextVariable("SCORE", WarCabinetData.AccumulatedScore);
            msg.SetTextVariable("NEED", threshold);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));

            if (_talkedLordsCount >= totalPossibleLords)
            {
                if (WarCabinetData.AccumulatedScore >= threshold)
                {
                    var target = WarCabinetData.TargetFaction;
                    if (target != null && WarCabinetData.CurrentType == WarCabinetData.CouncilType.ProvokeWar)
                    {
                        DeclareWarAction.ApplyByDefault(kingdom, target);

                        TextObject title = new TextObject("{=wc_inquiry_war_title}WAR DECLARED");
                        TextObject text = new TextObject("{=wc_inquiry_war_text}The Council has spoken! With {SCORE} points (Needs {NEED}), the motion passes. We march to war!");
                        text.SetTextVariable("SCORE", WarCabinetData.AccumulatedScore);
                        text.SetTextVariable("NEED", threshold);

                        InformationManager.ShowInquiry(new InquiryData(title.ToString(), text.ToString(), true, false, new TextObject("{=wc_btn_glory}To Glory!").ToString(), "", null, null), true);
                    }
                }
                else
                {
                    TextObject title = new TextObject("{=wc_inquiry_denied_title}MOTION DENIED");
                    TextObject text = new TextObject("{=wc_inquiry_denied_text}The Council is divided. With only {SCORE} points (Needs {NEED}), you failed to gather enough support.");
                    text.SetTextVariable("SCORE", WarCabinetData.AccumulatedScore);
                    text.SetTextVariable("NEED", threshold);

                    InformationManager.ShowInquiry(new InquiryData(title.ToString(), text.ToString(), true, false, new TextObject("{=wc_btn_dismiss}Dismiss Council").ToString(), "", null, null), true);
                }

                WarCabinetData.Reset();
                _talkedLordsCount = 0;
            }
        }

        private void CleanUp_Conversation()
        {
            ConversationManager.EndPersuasion();
            _currentTasks = null;
            _lastConversedHero = null;
            _currentTaskIndex = 0;
            _currentMoodModifier = 0f;
        }

        private bool Condition_AlreadyPersuaded()
        {
            return Hero.OneToOneConversationHero != null && WarCabinetData.PersuadedLords.Contains(Hero.OneToOneConversationHero);
        }

        private PersuasionArgumentStrength GetDynamicStrength(TraitObject trait, TraitEffect effect)
        {
            int traitLevel = Hero.OneToOneConversationHero.GetTraitLevel(trait);
            int baseStrength = (int)PersuasionArgumentStrength.Normal;

            if (effect == TraitEffect.Positive)
            {
                if (traitLevel > 0) baseStrength += 1;
                if (traitLevel > 1) baseStrength += 1;
                if (traitLevel < 0) baseStrength -= 1;
            }
            else
            {
                if (traitLevel > 0) baseStrength -= 2;
            }

            int relation = Hero.OneToOneConversationHero.GetRelation(Hero.MainHero);
            if (relation > 20) baseStrength += 1;
            if (relation < -20) baseStrength -= 1;

            baseStrength += (int)_currentMoodModifier;
            return (PersuasionArgumentStrength)MBMath.ClampInt(baseStrength, -3, 3);
        }

        private PersuasionTask GetCurrentTask()
        {
            if (_currentTasks == null || _currentTasks.Count == 0) return null;
            if (_currentTaskIndex >= _currentTasks.Count) return _currentTasks.Last();
            return _currentTasks[_currentTaskIndex];
        }

        private PersuasionOptionArgs GetOptionArgs(int index)
        {
            var task = GetCurrentTask();
            return (task != null && task.Options.Count > index) ? task.Options[index] : null;
        }

        private PersuasionOptionArgs Delegate_GetOption1Args() { return GetOptionArgs(0); }
        private PersuasionOptionArgs Delegate_GetOption2Args() { return GetOptionArgs(1); }
        private PersuasionOptionArgs Delegate_GetOption3Args() { return GetOptionArgs(2); }
        private PersuasionOptionArgs Delegate_GetOption4Args() { return GetOptionArgs(3); }

        private bool SetupOptionText(int index, string textVar, string chanceVar)
        {
            var opt = GetOptionArgs(index);
            if (opt == null) return false;
            MBTextManager.SetTextVariable(textVar, opt.Line);
            MBTextManager.SetTextVariable(chanceVar, PersuasionHelper.ShowSuccess(opt));
            return true;
        }

        private bool Condition_SetupOption1Text() { return SetupOptionText(0, "WC_OPT_1", "WC_CHANCE_1"); }
        private bool Condition_SetupOption2Text() { return SetupOptionText(1, "WC_OPT_2", "WC_CHANCE_2"); }
        private bool Condition_SetupOption3Text() { return SetupOptionText(2, "WC_OPT_3", "WC_CHANCE_3"); }
        private bool Condition_SetupOption4Text() { return SetupOptionText(3, "WC_OPT_4", "WC_CHANCE_4"); }

        private bool CheckClickable(int index, out TextObject hint)
        {
            hint = TextObject.GetEmpty();
            var opt = GetOptionArgs(index);
            if (opt == null) return false;
            if (opt.IsBlocked)
            {
                hint = new TextObject("{=blocked}Argument used.");
                return false;
            }
            return true;
        }

        private bool Condition_Clickable1(out TextObject h) { return CheckClickable(0, out h); }
        private bool Condition_Clickable2(out TextObject h) { return CheckClickable(1, out h); }
        private bool Condition_Clickable3(out TextObject h) { return CheckClickable(2, out h); }
        private bool Condition_Clickable4(out TextObject h) { return CheckClickable(3, out h); }

        private void Consequence_Option1() { GetOptionArgs(0).BlockTheOption(true); }
        private void Consequence_Option2() { GetOptionArgs(1).BlockTheOption(true); }
        private void Consequence_Option3() { GetOptionArgs(2).BlockTheOption(true); }
        private void Consequence_Option4() { GetOptionArgs(3).BlockTheOption(true); }

        private bool Condition_IsFailure() => ConversationManager.GetPersuasionIsFailure() || (ConversationManager.GetPersuasionChosenOptions().LastOrDefault()?.Item2 == PersuasionOptionResult.CriticalFailure);
        private bool Condition_IsGeneralFailure() => ConversationManager.GetPersuasionIsFailure() && !Condition_IsCriticalFail();
        private bool Condition_IsCriticalFail() { var o = ConversationManager.GetPersuasionChosenOptions().LastOrDefault(); return o != null && o.Item2 == PersuasionOptionResult.CriticalFailure; }
        private bool Condition_IsSuccess() => ConversationManager.GetPersuasionProgressSatisfied();
        private bool Condition_ShouldContinue() => !ConversationManager.GetPersuasionProgressSatisfied() && !ConversationManager.GetPersuasionIsFailure() && !Condition_IsCriticalFail();

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
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=good}That implies a fair point."));
                }
                else if (result == PersuasionOptionResult.CriticalFailure)
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=crit_fail}I am offended by that suggestion!"));
                }
                else
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=bad}I am not convinced."));
                }

                if (result != PersuasionOptionResult.CriticalFailure &&
                    !ConversationManager.GetPersuasionIsFailure() &&
                    !ConversationManager.GetPersuasionProgressSatisfied())
                {
                    _currentTaskIndex++;
                }
            }
            return true;
        }
    }
}

