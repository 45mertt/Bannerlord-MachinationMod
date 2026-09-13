using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace RebellionsAndDemographics
{
    public class WarCabinetPersuasionBehavior : CampaignBehaviorBase
    {
        private List<PersuasionTask> _currentTasks;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }
        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // {=wc_p_start}My Lord, we must discuss peace with {TARGET_FACTION}.
            starter.AddPlayerLine("wc_peace_start", "start", "wc_peace_intro",
                "{=wc_p_start}My Lord, we must discuss peace with {TARGET_FACTION}.",
                () => WarCabinetData.CurrentType == WarCabinetData.CouncilType.PreventWar && IsCouncilMission(),
                () => {
                    MBTextManager.SetTextVariable("TARGET_FACTION", WarCabinetData.TargetFaction?.Name ?? new TextObject("Enemy"));
                    SetupPersuasion();
                });

            // {=wc_p_resp}Peace? Give me a good reason.
            starter.AddDialogLine("wc_peace_response", "wc_peace_intro", "wc_peace_options",
                "{=wc_p_resp}Peace? Give me a good reason.", null, null);

            // {=wc_arg_1}{ARG1} {CHANCE1}
            starter.AddPlayerLine("wc_arg_1", "wc_peace_options", "wc_reaction", "{=wc_arg_1}{ARG1} {CHANCE1}",
                () => SetupOption(0, "ARG1", "CHANCE1"), () => ApplyOption(0));

            starter.AddPlayerLine("wc_arg_2", "wc_peace_options", "wc_reaction", "{=wc_arg_1}{ARG2} {CHANCE2}",
                () => SetupOption(1, "ARG2", "CHANCE2"), () => ApplyOption(1));

            // {=wc_exit}Never mind.
            starter.AddPlayerLine("wc_arg_exit", "wc_peace_options", "close_window", "{=wc_exit}Never mind.", null, () => ConversationManager.EndPersuasion());

            // {=wc_reaction_line}{PERSUASION_REACTION}
            starter.AddDialogLine("wc_reaction_line", "wc_reaction", "wc_next", "{=wc_reaction_line}{PERSUASION_REACTION}", Condition_Reaction, null);

            // {=wc_next}Anything else?
            starter.AddDialogLine("wc_next_line", "wc_next", "wc_peace_options", "{=wc_next}Anything else?", () => !ConversationManager.GetPersuasionProgressSatisfied(), null);

            // {=wc_succ}Very well. I will support peace.
            starter.AddDialogLine("wc_success", "wc_next", "close_window", "{=wc_succ}Very well. I will support peace.",
                () => ConversationManager.GetPersuasionProgressSatisfied(),
                Consequence_SuccessOpenBarter);
        }

        private bool IsCouncilMission() => Mission.Current != null && Mission.Current.HasMissionBehavior<WarCouncilMissionLogic>();

        private void SetupPersuasion()
        {
            _currentTasks = new List<PersuasionTask>();
            PersuasionTask task = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));

            // {=arg_log}Our supplies are running low.
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Easy, false, new TextObject("{=arg_log}Our supplies are running low.")));
            // {=arg_hum}People are dying for nothing.
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, PersuasionArgumentStrength.Normal, true, new TextObject("{=arg_hum}People are dying for nothing.")));

            _currentTasks.Add(task);
            ConversationManager.StartPersuasion(1f, 1f, 0f, 2f, 2f);
        }

        private bool SetupOption(int index, string txt, string chance)
        {
            var task = _currentTasks.FirstOrDefault();
            if (task == null || task.Options.Count <= index) return false;
            var opt = task.Options[index];
            MBTextManager.SetTextVariable(txt, opt.Line);
            MBTextManager.SetTextVariable(chance, PersuasionHelper.ShowSuccess(opt));
            return !opt.IsBlocked;
        }

        private void ApplyOption(int index)
        {
            var task = _currentTasks.FirstOrDefault();
            if (task != null) task.ApplyEffects(0, 0);
        }

        private bool Condition_Reaction()
        {
            // {=think}Hmm...
            MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=think}Hmm..."));
            return true;
        }

        private void Consequence_SuccessOpenBarter()
        {
            ConversationManager.EndPersuasion();

            BarterManager.Instance.StartBarterOffer(
                Hero.MainHero,
                Hero.OneToOneConversationHero,
                PartyBase.MainParty,
                Hero.OneToOneConversationHero.PartyBelongedTo?.Party,
                null,
                null,
                0,
                false
            );
        }
    }
}

