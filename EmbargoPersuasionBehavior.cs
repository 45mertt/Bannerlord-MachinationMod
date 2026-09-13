using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
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

namespace RebellionsAndDemographics
{
    public class EmbargoPersuasionBehavior : CampaignBehaviorBase
    {
        public static EmbargoPersuasionBehavior Instance;

        private string _pendingEmbargoTownId = null;
        private string _targetFactionId = null;
        private CampaignTime _waitStartTime = CampaignTime.Never;
        private bool _isReadyForCouncil = false;
        private List<PersuasionTask> _currentTasks;

        public EmbargoPersuasionBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_pendingEmbargoTownId", ref _pendingEmbargoTownId);
            dataStore.SyncData("_targetFactionId", ref _targetFactionId);
            dataStore.SyncData("_waitStartTime", ref _waitStartTime);
            dataStore.SyncData("_isReadyForCouncil", ref _isReadyForCouncil);
        }

        public void RequestEmbargo(Settlement town, IFaction targetFaction)
        {
            if (Hero.MainHero.MapFaction?.Leader == null || Hero.MainHero.MapFaction.Leader == Hero.MainHero)
            {
                // Kendi krallığının lideri bizsek veya krallık yoksa direkt onayla
                InformationManager.DisplayMessage(new InformationMessage($"Since you are the king, the embargo on the {targetFaction.Name} kingdom was directly approved.", Colors.Green));
                ApplyEmbargo(targetFaction.StringId);
                return;
            }

            _pendingEmbargoTownId = town.StringId;
            _targetFactionId = targetFaction.StringId;
            _waitStartTime = CampaignTime.Now;
            _isReadyForCouncil = false;
            InformationManager.DisplayMessage(new InformationMessage("An embargo request was forwarded to the king. News is awaited...", Colors.Yellow));
        }

        private void OnHourlyTick()
        {
            if (_pendingEmbargoTownId != null && !_isReadyForCouncil)
            {
                // 12 ila 24 saat arasında bir sürede kabul edilsin (şimdilik 2 saat yapalım test için)
                if (_waitStartTime.ElapsedHoursUntilNow >= 2f)
                {
                    _isReadyForCouncil = true;
                    InformationManager.DisplayMessage(new InformationMessage("The king summons you to the embargo assembly!", Colors.Red));
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Bekleme durumu menüsü (Tıklanamaz)
            starter.AddGameMenuOption("town_workshop_menu", "embargo_wait", "Kral / Vezir bekleniyor...",
                args =>
                {
                    if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement.StringId != _pendingEmbargoTownId) return false;
                    if (_isReadyForCouncil) return false;

                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    args.Tooltip = new TextObject("News is expected from the king regarding the embargo council.");
                    args.IsEnabled = false;
                    return true;
                },
                args => { });

            // Meclise katıl butonu
            starter.AddGameMenuOption("town_workshop_menu", "embargo_council_join", "Join the Assembly (Embargo Permission)",
                args =>
                {
                    if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement.StringId != _pendingEmbargoTownId) return false;
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return _isReadyForCouncil;
                },
                args =>
                {
                    StartCouncilConversation();
                });

            AddDialogs(starter);
        }

        private void StartCouncilConversation()
        {
            Hero king = Hero.MainHero.MapFaction?.Leader;
            if (king == null) return;

            CampaignMapConversation.OpenConversation(new ConversationCharacterData(Hero.MainHero.CharacterObject, null, false, false, false, false, false, false), new ConversationCharacterData(king.CharacterObject, null, false, false, false, false, false, false));
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Başlangıç - Oyuncu
            starter.AddPlayerLine("embargo_conv_start", "hero_main_options", "embargo_conv_intro",
                "My lord, we must use our commercial power in {TARGET_SETTLEMENT} and impose an embargo on {TARGET_FACTION} territory.",
                () => 
                {
                    return _isReadyForCouncil && Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero == Hero.MainHero.MapFaction?.Leader;
                }, 
                () =>
                {
                    var town = Settlement.All.FirstOrDefault(s => s.StringId == _pendingEmbargoTownId);
                    var targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == _targetFactionId);
                    MBTextManager.SetTextVariable("TARGET_SETTLEMENT", town?.Name ?? new TextObject("City"));
                    MBTextManager.SetTextVariable("TARGET_FACTION", targetFaction?.Name ?? new TextObject("enemy"));
                    SetupPersuasion();
                });

            // Kralın Tepkisi
            starter.AddDialogLine("embargo_conv_resp", "embargo_conv_intro", "embargo_conv_options",
                "Embargo? You know this will anger the trade guilds and provoke the enemy. Give me a good reason.", null, null);

            // Oyuncunun Argümanları
            starter.AddPlayerLine("embargo_arg_1", "embargo_conv_options", "embargo_conv_reaction", "{=!}{ARG1_TEXT} {ARG1_CHANCE}",
                Condition_SetupOption1Text, Consequence_Option1, 100, Condition_Clickable1, Delegate_GetOption1Args);

            starter.AddPlayerLine("embargo_arg_2", "embargo_conv_options", "embargo_conv_reaction", "{=!}{ARG2_TEXT} {ARG2_CHANCE}",
                Condition_SetupOption2Text, Consequence_Option2, 100, Condition_Clickable2, Delegate_GetOption2Args);

            starter.AddPlayerLine("embargo_arg_3", "embargo_conv_options", "embargo_conv_reaction", "{=!}{ARG3_TEXT} {ARG3_CHANCE}",
                Condition_SetupOption3Text, Consequence_Option3, 100, Condition_Clickable3, Delegate_GetOption3Args);

            starter.AddPlayerLine("embargo_arg_4", "embargo_conv_options", "embargo_conv_reaction", "{=!}{ARG4_TEXT} {ARG4_CHANCE}",
                Condition_SetupOption4Text, Consequence_Option4, 100, Condition_Clickable4, Delegate_GetOption4Args);

            // Çıkış
            starter.AddPlayerLine("embargo_arg_exit", "embargo_conv_options", "close_window", "I give up, my lord.", null, 
                () => 
                {
                    ConversationManager.EndPersuasion();
                    _isReadyForCouncil = false;
                    _pendingEmbargoTownId = null;
                });

            // Reaksiyon
            starter.AddDialogLine("embargo_conv_reaction_line", "embargo_conv_reaction", "embargo_conv_next", "{PERSUASION_REACTION}", Condition_ReactionAndApply, null);

            // Sonraki
            starter.AddDialogLine("embargo_conv_next_line", "embargo_conv_next", "embargo_conv_options", "Do you have another argument?", Condition_ShouldContinue, null);

            // Başarı
            starter.AddDialogLine("embargo_conv_success", "embargo_conv_next", "close_window", "You are right. We will use the city's economy as a weapon. Embargo approved!",
                () => ConversationManager.GetPersuasionProgressSatisfied(),
                () => 
                {
                    ApplyEmbargo(_targetFactionId);
                    _isReadyForCouncil = false;
                    _pendingEmbargoTownId = null;
                    _targetFactionId = null;
                    ConversationManager.EndPersuasion();
                });
        }

        private void SetupPersuasion()
        {
            _currentTasks = new List<PersuasionTask>();
            PersuasionTask task = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));

            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("Our economy is strong enough to handle this move, but theirs is not.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Authoritarian, TraitEffect.Positive, PersuasionArgumentStrength.Hard, false, new TextObject("We must show them our strength so that they submit to us.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, PersuasionArgumentStrength.Easy, true, new TextObject("This is the noblest way to achieve victory without shedding blood.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("When they become weak, we easily crush them.")));

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
                hintText = new TextObject("{=*}Bu seçeneği zaten kullandınız.");
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
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Hmm... A logical argument."));
                }
                else if (result == PersuasionOptionResult.CriticalFailure)
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Don't talk nonsense! I can never accept this."));
                }
                else
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("Doesn't seem very reasonable to me..."));
                }
            }
            return true;
        }

        private void ApplyEmbargo(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return;

            var targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == factionId);
            if (targetFaction == null) return;

            InformationManager.DisplayMessage(new InformationMessage($"The decision to embargo the {targetFaction.Name} kingdom was passed by the parliament and implemented!", Colors.Red));
            
            // YENİ TRADE WAR SİSTEMİNE BAĞLA
            var marketBehavior = Campaign.Current.GetCampaignBehavior<WorkshopMarketBehavior>();
            if (marketBehavior != null)
            {
                marketBehavior.AddTradeWar(Hero.MainHero, targetFaction);
            }
        }
    }
}

