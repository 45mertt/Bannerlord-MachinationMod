using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace RebellionsAndDemographics
{
    public class LocalCouncilConversationBehavior : CampaignBehaviorBase
    {
        private int _activeScenarioIndex = 0;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // {=loc_1}The council is assembled, My Lord.
            starter.AddDialogLine("loc_gov_intro", "start", "loc_council_main",
                "{=loc_1}The council is assembled, My Lord.",
                Condition_IsLocalCouncil,
                () => DetermineScenario());

            // {=loc_2}We await your guidance.
            starter.AddDialogLine("loc_main_hub", "loc_council_main", "loc_council_options",
                "{=loc_2}We await your guidance.",
                null, null);

            // {=loc_agenda}What is on the agenda today?
            starter.AddPlayerLine("loc_ask_agenda", "loc_council_options", "loc_agenda_response",
                "{=loc_agenda}What is on the agenda today?",
                null, null);

            // {=loc_corr_text}My Lord, I reviewed the ledgers...
            starter.AddDialogLine("loc_agenda_corruption", "loc_agenda_response", "loc_corruption_choices",
                "{=loc_corr_text}My Lord, I reviewed the ledgers... There is a significant sum missing! One of the tax collectors has embezzled funds and colluded with merchants. The city is furious!",
                () => _activeScenarioIndex == 1, null);

            // {=loc_talent_text}The city smith has approached us...
            starter.AddDialogLine("loc_agenda_talent", "loc_agenda_response", "loc_talent_choices",
                "{=loc_talent_text}The city smith has approached us. He says: 'My Lord, my son is an unbending warrior. He seeks glory under your banner.'",
                () => _activeScenarioIndex == 2, null);

            // {=loc_routine_text}Matters are stable...
            starter.AddDialogLine("loc_agenda_routine", "loc_agenda_response", "loc_council_options",
                "{=loc_routine_text}Matters are stable, My Lord. Production is steady.",
                () => _activeScenarioIndex == 0, null);

            // {=loc_corr_exec}Execute the traitor! (Security+, Prosperity-)
            starter.AddPlayerLine("loc_corr_opt_1", "loc_corruption_choices", "loc_council_options",
                "{=loc_corr_exec}Execute the traitor! (Security+, Prosperity-)",
                null,
                () => ApplyCorruptionOutcome(true));

            // {=loc_corr_seize}Seize the assets and exile him. (Gold+, Security-)
            starter.AddPlayerLine("loc_corr_opt_2", "loc_corruption_choices", "loc_council_options",
                "{=loc_corr_seize}Seize the assets and exile him. (Gold+, Security-)",
                null,
                () => ApplyCorruptionOutcome(false));

            // {=loc_talent_accept}I have room in my ranks...
            starter.AddPlayerLine("loc_talent_opt_1", "loc_talent_choices", "loc_council_options",
                "{=loc_talent_accept}I have room in my ranks. Let him come. (Recruit T3 Troop)",
                null,
                () => ApplyTalentOutcome(true));

            // {=loc_talent_reject}I have no room...
            starter.AddPlayerLine("loc_talent_opt_2", "loc_talent_choices", "loc_council_options",
                "{=loc_talent_reject}I have no room for inexperienced soldiers.",
                null, null);

            // {=loc_l}Dismissed.
            starter.AddPlayerLine("loc_leave", "loc_council_options", "close_window",
                "{=loc_l}Dismissed.", null, null);
        }

        private bool Condition_IsLocalCouncil()
        {
            return Mission.Current != null && Mission.Current.HasMissionBehavior<LocalCouncilMissionLogic>();
        }

        private void DetermineScenario()
        {
            Settlement town = Settlement.CurrentSettlement;
            if (town == null) return;

            float corruptionChance = (100f - town.Town.Security) * 0.5f;
            if (town.Town.Prosperity > 6000) corruptionChance += 10f;

            if (MBRandom.RandomFloat * 100f < corruptionChance)
            {
                _activeScenarioIndex = 1;
                return;
            }

            if (MBRandom.RandomFloat < 0.3f)
            {
                _activeScenarioIndex = 2;
                return;
            }

            _activeScenarioIndex = 0;
        }

        private void ApplyCorruptionOutcome(bool execute)
        {
            Settlement town = Settlement.CurrentSettlement;
            if (execute)
            {
                town.Town.Security += 15f;
                town.Town.Prosperity -= 50f;
                town.Town.Loyalty += 5f;
            }
            else
            {
                town.Town.Security -= 10f;
                int stolen = 150000;
                if (CorruptionManager.CityRecords.TryGetValue(town, out CorruptionData cd))
                {
                    stolen = cd.ProjectedTax - cd.ActualTax;
                }
                Hero.MainHero.ChangeHeroGold(stolen);
            }
            _activeScenarioIndex = 0; // FIX: Reset scenario to prevent loop
        }

        private void ApplyTalentOutcome(bool accept)
        {
            Settlement town = Settlement.CurrentSettlement;
            if (accept)
            {
                CharacterObject troop = town.Culture.EliteBasicTroop;
                if (troop != null)
                {
                    MobileParty.MainParty.AddElementToMemberRoster(troop, 1);
                    // {=loc_msg_recruit}A young warrior joined your party.
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=loc_msg_recruit}A young warrior joined your party.").ToString(), Colors.Green));
                }
            }
            _activeScenarioIndex = 0; // FIX: Reset scenario to prevent loop
        }
    }
}
