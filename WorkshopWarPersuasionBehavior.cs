    using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using Helpers;

namespace RebellionsAndDemographics
{
    public class WorkshopWarPersuasionBehavior : CampaignBehaviorBase
    {
        public static WorkshopWarPersuasionBehavior Instance { get; private set; }

        private List<PersuasionTask> _currentTasks;
        
        // Bu kralliklardan alacakli/savas istegimiz var
        private List<string> _pendingWarKingdomIds = new List<string>();
        public List<string> PendingWarKingdomIds { get { return _pendingWarKingdomIds ?? (_pendingWarKingdomIds = new List<string>()); } set { _pendingWarKingdomIds = value; } }

        public WorkshopWarPersuasionBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var pending = PendingWarKingdomIds;
                pending.RemoveAll(x => x == null);
            }
            dataStore.SyncData("_pendingWarKingdomIds", ref _pendingWarKingdomIds);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Başlangıç - Oyuncu (Biz kral ile konusuyoruz)
            starter.AddPlayerLine("war_persuasion_start", "hero_main_options", "war_persuasion_target_selection",
                "My lord, enemy kingdoms have seized our workshops and trade. It's time to declare war!",
                () => 
                {
                    if (Hero.OneToOneConversationHero == null) return false;
                    if (Hero.MainHero.MapFaction == null || Hero.OneToOneConversationHero != Hero.MainHero.MapFaction.Leader) return false;
                    
                    // Sadece bizim tekelimize el koyan ve su an savasta OLMADIGIMIZ kralliklar var mi?
                    return Kingdom.All.Any(k => 
                        WorkshopMonopolyBehavior.Instance.IsConfiscatedBy(k) && 
                        !Hero.MainHero.MapFaction.IsAtWarWith(k) && 
                        k != Hero.MainHero.MapFaction);
                }, null);

            // Hedef Secme
            starter.AddDialogLine("war_persuasion_target_resp", "war_persuasion_target_selection", "war_persuasion_target_list",
                "Which insolent kingdom dared to confiscate the property of our subjects?", null, null);

            starter.AddPlayerLine("war_persuasion_target_item", "war_persuasion_target_list", "war_persuasion_intro",
                "The kingdom of {TARGET_KINGDOM}, my lord.",
                () =>
                {
                    // Su an savasta olmadigimiz ve bize el koymus bir krallik sec (ilkini aliyoruz kolaylik icin, veya coklu secim yapmaliyiz ama Bannerlord dinamik liste yerine teke tek secim destekler)
                    var targetKingdom = Kingdom.All.FirstOrDefault(k => 
                        WorkshopMonopolyBehavior.Instance.IsConfiscatedBy(k) && 
                        !Hero.MainHero.MapFaction.IsAtWarWith(k) && 
                        k != Hero.MainHero.MapFaction);
                    
                    if (targetKingdom == null) return false;

                    MBTextManager.SetTextVariable("TARGET_KINGDOM", targetKingdom.Name);
                    return true;
                },
                () =>
                {
                    var targetKingdom = Kingdom.All.FirstOrDefault(k => 
                        WorkshopMonopolyBehavior.Instance.IsConfiscatedBy(k) && 
                        !Hero.MainHero.MapFaction.IsAtWarWith(k) && 
                        k != Hero.MainHero.MapFaction);
                        
                    if (targetKingdom != null)
                    {
                        if (!_pendingWarKingdomIds.Contains(targetKingdom.StringId))
                            _pendingWarKingdomIds.Add(targetKingdom.StringId);
                        SetupPersuasion();
                    }
                });

            starter.AddPlayerLine("war_persuasion_target_cancel", "war_persuasion_target_list", "close_window",
                "Never mind, my lord, now is not the time.", null, null);

            // Kralın Tepkisi
            starter.AddDialogLine("war_persuasion_intro_resp", "war_persuasion_intro", "war_persuasion_options",
                "War is a big decision. Should I sacrifice thousands of lives for a few workshops? You must convince me.", null, null);

            // Oyuncunun Argümanları
            starter.AddPlayerLine("war_persuasion_arg_1", "war_persuasion_options", "war_persuasion_reaction", "{=!}{ARG1_TEXT} {ARG1_CHANCE}",
                Condition_SetupOption1Text, Consequence_Option1, 100, Condition_Clickable1, Delegate_GetOption1Args);

            starter.AddPlayerLine("war_persuasion_arg_2", "war_persuasion_options", "war_persuasion_reaction", "{=!}{ARG2_TEXT} {ARG2_CHANCE}",
                Condition_SetupOption2Text, Consequence_Option2, 100, Condition_Clickable2, Delegate_GetOption2Args);

            starter.AddPlayerLine("war_persuasion_arg_3", "war_persuasion_options", "war_persuasion_reaction", "{=!}{ARG3_TEXT} {ARG3_CHANCE}",
                Condition_SetupOption3Text, Consequence_Option3, 100, Condition_Clickable3, Delegate_GetOption3Args);

            starter.AddPlayerLine("war_persuasion_arg_4", "war_persuasion_options", "war_persuasion_reaction", "{=!}{ARG4_TEXT} {ARG4_CHANCE}",
                Condition_SetupOption4Text, Consequence_Option4, 100, Condition_Clickable4, Delegate_GetOption4Args);

            // Çıkış / Basarisizlik (Sıfır Ceza)
            starter.AddPlayerLine("war_persuasion_exit", "war_persuasion_options", "war_persuasion_fail", "I give up, my lord. You are right.", null, 
                () => 
                {
                    ConversationManager.EndPersuasion();
                    _pendingWarKingdomIds.Clear();
                });
                
            starter.AddDialogLine("war_persuasion_fail_resp", "war_persuasion_fail", "close_window", "The way of mind is one. Keep trading instead of fighting.", null, null);

            // Reaksiyon
            starter.AddDialogLine("war_persuasion_reaction_line", "war_persuasion_reaction", "war_persuasion_next", "{PERSUASION_REACTION}", Condition_ReactionAndApply, null);

            // Sonraki veya Başarısızlık Kontrolü
            starter.AddDialogLine("war_persuasion_next_line", "war_persuasion_next", "war_persuasion_options", "Do you have another argument?", Condition_ShouldContinue, null);

            // İkna Tamamen Basarisiz Oldu (Sıfır ceza)
            starter.AddDialogLine("war_persuasion_failed_final", "war_persuasion_next", "close_window", "You couldn't convince me. I would not enter into a great destruction like war with these excuses.", 
                () => ConversationManager.GetPersuasionIsFailure(), 
                () => 
                {
                    ConversationManager.EndPersuasion();
                    _pendingWarKingdomIds.Clear();
                });

            // Başarı
            starter.AddDialogLine("war_persuasion_success", "war_persuasion_next", "close_window", "You are right! Seizing your property is a direct insult to my crown. Raise your banner, we are going to war!",
                () => ConversationManager.GetPersuasionProgressSatisfied(),
                () => 
                {
                    ApplyWar();
                    ConversationManager.EndPersuasion();
                });
        }

        private void SetupPersuasion()
        {
            _currentTasks = new List<PersuasionTask>();
            PersuasionTask task = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999)); // Ihtiyac olan basari puani

            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("My usurped workshops were the source of income for our kingdom. If we remain silent, the economy will collapse.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Authoritarian, TraitEffect.Positive, PersuasionArgumentStrength.Hard, false, new TextObject("They challenge your authority. A king wouldn't tolerate this!")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, PersuasionArgumentStrength.Easy, true, new TextObject("Our people expect honor from your crown. We must take back what is ours with blood.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("First they will take our goods, then our castles. Let's hit before they hit.")));

            _currentTasks.Add(task);
            ConversationManager.StartPersuasion(3f, 1f, 0f, 2f, 2f);
        }

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
                hintText = new TextObject("{=rad_wwp_01}You have already used this option.");
                return false;
            }
            return true;
        }

        private bool Condition_ShouldContinue() => !ConversationManager.GetPersuasionProgressSatisfied() && !ConversationManager.GetPersuasionIsFailure();

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
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("You have a point..."));
                }
                else if (result == PersuasionOptionResult.CriticalFailure)
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("These are not sufficient reasons for a king to go to war."));
                }
                else
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("This didn't seem very reasonable..."));
                }
            }
            return true;
        }

        private void ApplyWar()
        {
            var targetId = _pendingWarKingdomIds.FirstOrDefault();
            if (string.IsNullOrEmpty(targetId)) return;

            var targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == targetId);
            if (targetFaction != null && Hero.MainHero.MapFaction != null && !Hero.MainHero.MapFaction.IsAtWarWith(targetFaction))
            {
                DeclareWarAction.ApplyByDefault(Hero.MainHero.MapFaction, targetFaction);
                InformationManager.DisplayMessage(new InformationMessage($"The kingdom {Hero.MainHero.MapFaction.Name} has declared WAR on the kingdom {targetFaction.Name} in response to the workshop usurpations!", Colors.Red));
            }
            _pendingWarKingdomIds.Clear();
        }
    }
}
