using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RebellionsAndDemographics
{
    public class FiefLimitBehavior : CampaignBehaviorBase
    {
        private CampaignTime _confiscationDeadline = CampaignTime.Never;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("rad_fief_limit_deadline", ref _confiscationDeadline);
            }
            catch { }
        }

        public static (int MaxTowns, int MaxCastles) GetFiefLimits(int tier)
        {
            switch (tier)
            {
                case 1: return (0, 0);
                case 2: return (0, 1);
                case 3: return (1, 2);
                case 4: return (2, 3);
                case 5: return (3, 5);
                default: return (999, 999); // Tier 6 is unlimited
            }
        }

        private bool IsVassalSubjectToLimits()
        {
            var clan = Hero.MainHero.Clan;
            if (clan == null || clan.Kingdom == null) return false;
            if (clan.Kingdom.RulingClan == clan) return false; // Ruler has no limits
            if (clan.Tier >= 6) return false; // Tier 6 has no limits
            return true;
        }

        private bool IsOverLimit(out int excessTowns, out int excessCastles)
        {
            excessTowns = 0;
            excessCastles = 0;

            if (!IsVassalSubjectToLimits()) return false;

            var limits = GetFiefLimits(Hero.MainHero.Clan.Tier);
            int towns = Hero.MainHero.Clan.Settlements.Count(s => s.IsTown);
            int castles = Hero.MainHero.Clan.Settlements.Count(s => s.IsCastle);

            if (towns > limits.MaxTowns) excessTowns = towns - limits.MaxTowns;
            if (castles > limits.MaxCastles) excessCastles = castles - limits.MaxCastles;

            return excessTowns > 0 || excessCastles > 0;
        }

        private void OnDailyTick()
        {
            if (!IsOverLimit(out int ext, out int exc))
            {
                _confiscationDeadline = CampaignTime.Never;
                return;
            }

            if (_confiscationDeadline == CampaignTime.Never)
            {
                _confiscationDeadline = CampaignTime.DaysFromNow(3f); // 3 days to talk to the king
                InformationManager.ShowInquiry(new InquiryData(
                    new TaleWorlds.Localization.TextObject("{=rad_fief_warn_title}Urgent Summons").ToString(),
                    new TaleWorlds.Localization.TextObject("{=rad_fief_warn_text}Your clan currently holds more fiefs than is permitted for your clan tier. The other lords are restless. You must speak with the King immediately to relinquish a fief, otherwise one will be forcefully confiscated in 3 days!").ToString(),
                    true, false,
                    new TaleWorlds.Localization.TextObject("{=rad_understood}Understood").ToString(),
                    "", null, null));
            }
            else if (_confiscationDeadline.IsPast)
            {
                // Forceful Confiscation
                Settlement toTake = Hero.MainHero.Clan.Settlements.FirstOrDefault(s => s.IsTown && ext > 0) ?? 
                                    Hero.MainHero.Clan.Settlements.FirstOrDefault(s => s.IsCastle && exc > 0);
                
                if (toTake != null)
                {
                    
                    Clan bestClan = null;
                    int minFiefs = 999;
                    foreach (var c in Hero.MainHero.Clan.Kingdom.Clans)
                    {
                        if (c != Hero.MainHero.Clan && !c.IsUnderMercenaryService && !c.IsMinorFaction)
                        {
                            int fiefCount = c.Settlements.Count;
                            if (fiefCount < minFiefs)
                            {
                                minFiefs = fiefCount;
                                bestClan = c;
                            }
                        }
                    }
                    if (bestClan == null) bestClan = Hero.MainHero.MapFaction.Leader.Clan;
                    ChangeOwnerOfSettlementAction.ApplyByKingDecision(bestClan.Leader, toTake);

                    InformationManager.ShowInquiry(new InquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_fief_seized_title}Fief Confiscated").ToString(),
                        new TaleWorlds.Localization.TextObject("{=rad_fief_seized_text}The King lost patience and forcefully seized " + toTake.Name + " from your clan.").ToString(),
                        true, false,
                        new TaleWorlds.Localization.TextObject("{=rad_understood}Understood").ToString(),
                        "", null, null));
                }
                
                // Re-evaluate next tick if still over limit
                _confiscationDeadline = CampaignTime.Never; 
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Talk to King about excess fiefs
            starter.AddPlayerLine("rad_fief_limit_talk", "hero_main_options", "rad_fief_limit_king",
                "{=rad_fief_talk_king}My Liege, I have received your summons regarding my estates.",
                () => {
                    return Hero.OneToOneConversationHero != null &&
                           Hero.MainHero.MapFaction != null &&
                           Hero.OneToOneConversationHero == Hero.MainHero.MapFaction.Leader &&
                           IsOverLimit(out _, out _);
                }, null);

            starter.AddDialogLine("rad_fief_limit_king", "rad_fief_limit_king", "rad_fief_limit_king_2",
                "{=rad_fief_king_res}Ah, {PLAYER.NAME}. Indeed. A clan of your standing holding so much land disrupts the balance of our realm. The other lords are growing envious.",
                null, null);

            starter.AddDialogLine("rad_fief_limit_king_2", "rad_fief_limit_king_2", "rad_fief_limit_options",
                "{=rad_fief_king_res2}You must relinquish one of your fiefs to the crown immediately.",
                null, null);

            starter.AddPlayerLine("rad_fief_limit_give", "rad_fief_limit_options", "close_window",
                "{=rad_fief_give_opt}As you wish, my King. I will choose which fief to surrender.",
                null, 
                () => {
                    // Open Inquiry to choose fief
                    List<InquiryElement> elements = new List<InquiryElement>();
                    foreach(var stl in Hero.MainHero.Clan.Settlements)
                    {
                        if (stl.IsTown || stl.IsCastle)
                        {
                            elements.Add(new InquiryElement(stl, stl.Name.ToString(), null));
                        }
                    }

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        new TaleWorlds.Localization.TextObject("{=rad_fief_relinq_title}Relinquish Fief").ToString(),
                        new TaleWorlds.Localization.TextObject("{=rad_fief_relinq_desc}Select a fief to return to the crown.").ToString(),
                        elements, true, 1, 1,
                        new TaleWorlds.Localization.TextObject("{=rad_surrender}Surrender").ToString(),
                        "", // No cancel button allowed
                        (List<InquiryElement> selected) => {
                            if (selected != null && selected.Count > 0)
                            {
                                Settlement stl = selected[0].Identifier as Settlement;
                                
                                Clan bestClan = null;
                                int minFiefs = 999;
                                foreach (var c in Hero.MainHero.Clan.Kingdom.Clans)
                                {
                                    if (c != Hero.MainHero.Clan && !c.IsUnderMercenaryService && !c.IsMinorFaction)
                                    {
                                        int fiefCount = c.Settlements.Count;
                                        if (fiefCount < minFiefs)
                                        {
                                            minFiefs = fiefCount;
                                            bestClan = c;
                                        }
                                    }
                                }
                                if (bestClan == null) bestClan = Hero.MainHero.MapFaction.Leader.Clan;
                                ChangeOwnerOfSettlementAction.ApplyByKingDecision(bestClan.Leader, stl);
                                InformationManager.DisplayMessage(new InformationMessage(stl.Name.ToString() + " has been surrendered to the crown and granted to " + bestClan.Name.ToString() + ".", Colors.Red));

                            }
                        }, 
                        null
                    ));
                });

            // Rebellious option
            starter.AddPlayerLine("rad_fief_limit_rebel", "rad_fief_limit_options", "lord_pretalk",
                "{=rad_fief_rebel_opt}I refuse. What is mine, remains mine.",
                null, 
                () => {
                    ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, -20);
                });
        }
    }
}
