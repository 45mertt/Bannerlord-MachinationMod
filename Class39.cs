using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class CorruptionCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, (s) => CorruptionManager.CalculateDailyCorruption(s));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenuOptions);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in CorruptionManager.CityRecords.ToList()) { if (!(pair.Key != null && pair.Value != null)) CorruptionManager.CityRecords.Remove(pair.Key); }
            }
            dataStore.SyncData("CityCorruptionRecords", ref CorruptionManager._cityRecords);
        }

        private void AddMenuOptions(CampaignGameStarter starter)
        {
            // {=cor_ledger}Inspect Financial Ledger
            starter.AddGameMenuOption("town_keep", "inspect_ledger", "{=cor_ledger}Inspect Financial Ledger",
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
                },
                (args) => ShowLedgerReport(), false, 0);

            // Gov Corruption Dialogues
            starter.AddPlayerLine("gov_cor_accuse", "hero_main_options", "gov_cor_response",
                "{=cor_accuse_line}According to the financial reports I received, there is corruption in the city. Rumor has it that {CULPRIT_NAME} is behind this.",
                () => {
                    if (Hero.OneToOneConversationHero != null && Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.Town != null && Settlement.CurrentSettlement.Town.Governor == Hero.OneToOneConversationHero)
                    {
                        if (CorruptionManager.CityRecords.TryGetValue(Settlement.CurrentSettlement, out CorruptionData cd) && cd.IsExposed && cd.ActualTax < cd.ProjectedTax && cd.Culprit != null)
                        {
                            TaleWorlds.Localization.MBTextManager.SetTextVariable("CULPRIT_NAME", cd.Culprit.Name.ToString());
                            return true;
                        }
                    }
                    return false;
                },
                null);

            starter.AddDialogLine("gov_cor_response_line", "gov_cor_response", "gov_cor_choices",
                "{=cor_resp_line}My Lord, this is a grave accusation. When I spoke with {CULPRIT_NAME}, they offered me this excuse: '{EXCUSE}' What are your orders?",
                () => {
                    var excuses = new TextObject[] {
                        new TextObject("{=cor_exc_1}I was just using that gold to secure the market!"),
                        new TextObject("{=cor_exc_2}I miscalculated the taxes, it was all an honest mistake."),
                        new TextObject("{=cor_exc_3}Those gold coins were donated to the city's orphans!"),
                        new TextObject("{=cor_exc_4}I was saving that money to repair the city walls."),
                        new TextObject("{=cor_exc_5}The guild master extorted me, I had no other choice."),
                        new TextObject("{=cor_exc_6}My rivals forged those ledgers, they are slandering me!"),
                        new TextObject("{=cor_exc_7}I diverted the funds to increase the soldiers' wages."),
                        new TextObject("{=cor_exc_8}My caravan was robbed, I borrowed it to cover the loss."),
                        new TextObject("{=cor_exc_9}I am of noble birth! I don't deal with such petty accounting."),
                        new TextObject("{=cor_exc_10}The new tax laws are too complex, my accountant made a mistake.")
                    };
                    if (CorruptionManager.CityRecords.TryGetValue(Settlement.CurrentSettlement, out CorruptionData cd) && cd.Culprit != null)
                    {
                        TaleWorlds.Localization.MBTextManager.SetTextVariable("CULPRIT_NAME", cd.Culprit.Name.ToString());
                    }
                    TaleWorlds.Localization.MBTextManager.SetTextVariable("EXCUSE", excuses[TaleWorlds.Core.MBRandom.RandomInt(excuses.Length)].ToString());
                    return true;
                },
                null);

            starter.AddPlayerLine("gov_cor_choice_1", "gov_cor_choices", "close_window",
                "{=cor_ch1}The punishment is clear: forced labor in the workshops. (Treasury loss is not compensated, City Production and Prosperity increase)",
                () => true,
                () => { ApplyGovCorChoice(1); });

            starter.AddPlayerLine("gov_cor_choice_2", "gov_cor_choices", "close_window",
                "{=cor_ch2}Confiscate all their assets! (A lump sum of gold is added to the treasury, Loyalty decreases)",
                () => true,
                () => { ApplyGovCorChoice(2); });

            starter.AddPlayerLine("gov_cor_choice_3", "gov_cor_choices", "close_window",
                "{=cor_ch3}Flog them in front of the public as a warning! (Loyalty and Security increase)",
                () => true,
                () => { ApplyGovCorChoice(3); });

            starter.AddPlayerLine("gov_cor_choice_4", "gov_cor_choices", "close_window",
                "{=cor_ch4}I'll ignore it, but I want my cut. (Personal gold increases, Corruption continues)",
                () => true,
                () => { ApplyGovCorChoice(4); });

            starter.AddPlayerLine("gov_cor_choice_5", "gov_cor_choices", "close_window",
                "{=cor_ch5}The penalty is death! Off with their head! (Character is killed, Security peaks, Relations drop)",
                () => true,
                () => { ApplyGovCorChoice(5); });
        }

        private void ApplyGovCorChoice(int choice)
        {
            Settlement town = Settlement.CurrentSettlement;
            if (town == null || town.Town == null) return;
            if (!CorruptionManager.CityRecords.TryGetValue(town, out CorruptionData data) || data.Culprit == null) return;

            Hero culprit = data.Culprit;
            int stolen = data.ProjectedTax - data.ActualTax;

            switch (choice)
            {
                case 1:
                    town.Town.Prosperity += 100f;
                    TextObject res1 = new TextObject("{=cor_res1}{CULPRIT_NAME} was forced into labor. City prosperity increased.");
                    res1.SetTextVariable("CULPRIT_NAME", culprit.Name);
                    InformationManager.DisplayMessage(new InformationMessage(res1.ToString(), Colors.Green));
                    break;
                case 2:
                    int seizedAmount = (int)(stolen * 1.2f);
                    TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, seizedAmount, true);
                    town.Town.Loyalty -= 10f;
                    TextObject res2 = new TextObject("{=cor_res2}{CULPRIT_NAME}'s assets were seized. Gained {AMOUNT} Denars.");
                    res2.SetTextVariable("CULPRIT_NAME", culprit.Name);
                    res2.SetTextVariable("AMOUNT", seizedAmount);
                    InformationManager.DisplayMessage(new InformationMessage(res2.ToString(), Colors.Yellow));
                    break;
                case 3:
                    town.Town.Security += 15f;
                    town.Town.Loyalty += 10f;
                    TextObject res3 = new TextObject("{=cor_res3}{CULPRIT_NAME} was flogged. The public is satisfied, security increased.");
                    res3.SetTextVariable("CULPRIT_NAME", culprit.Name);
                    InformationManager.DisplayMessage(new InformationMessage(res3.ToString(), Colors.Green));
                    break;
                case 4:
                    TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, stolen, true);
                    TextObject res4 = new TextObject("{=cor_res4}Bribe accepted. {AMOUNT} Denars entered your pockets.");
                    res4.SetTextVariable("AMOUNT", stolen);
                    InformationManager.DisplayMessage(new InformationMessage(res4.ToString(), Colors.Red));
                    return;
                case 5:
                    town.Town.Security += 30f;
                    TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyPlayerRelation(culprit, -100);
                    if (culprit.Clan != null && culprit.Clan.Leader != null)
                    {
                        TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyPlayerRelation(culprit.Clan.Leader, -30);
                    }
                    TaleWorlds.CampaignSystem.Actions.KillCharacterAction.ApplyByExecution(culprit, Hero.MainHero, true, true);
                    TextObject res5 = new TextObject("{=cor_res5}{CULPRIT_NAME} was executed! Security skyrocketed.");
                    res5.SetTextVariable("CULPRIT_NAME", culprit.Name);
                    InformationManager.DisplayMessage(new InformationMessage(res5.ToString(), Colors.Red));
                    break;
            }

            CorruptionManager.CityRecords.Remove(town);
        }

        private void ShowLedgerReport()
        {
            Settlement town = Settlement.CurrentSettlement;
            if (CorruptionManager.CityRecords.TryGetValue(town, out CorruptionData data))
            {
                int loss = data.ProjectedTax - data.ActualTax;
                TextObject statusText = loss > 0 ? new TextObject("{=cor_ledger_found}Corruption Detected!") : new TextObject("{=cor_ledger_clean}Accounts Balanced");

                TextObject reportMsg = new TextObject("{=cor_ledger_report}--- FINANCIAL REPORT ---\n\nCity: {CITY_NAME}\nProjected Revenue: {PROJ_TAX} Denars\nActual Revenue: {ACT_TAX} Denars\nLost Amount: {LOSS} Denars\n\nStatus: {STATUS}");
                reportMsg.SetTextVariable("CITY_NAME", town.Name);
                reportMsg.SetTextVariable("PROJ_TAX", data.ProjectedTax);
                reportMsg.SetTextVariable("ACT_TAX", data.ActualTax);
                reportMsg.SetTextVariable("LOSS", loss);
                reportMsg.SetTextVariable("STATUS", statusText.ToString());

                string message = reportMsg.ToString();

                if (loss > 0)
                {
                    TextObject noteMsg = new TextObject("{=cor_ledger_note}\n\nNote: Rumors on the street might reveal the culprit.");
                    message += noteMsg.ToString();
                    data.IsExposed = true;
                }

                TextObject title = new TextObject("{=cor_ledger_title}Financial Ledger");
                TextObject closeBtn = new TextObject("{=str_close}Close");

                InformationManager.ShowInquiry(new InquiryData(title.ToString(), message, true, false, closeBtn.ToString(), "", null, null));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=cor_msg_no_records}No records found. Wait until tomorrow.").ToString(), Colors.Gray));
            }
        }
    }
}