using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class HierarchyEnforcementBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // TIER 0-1: KRAL ÝLE ÝLK KARÞILAÞMA
            starter.AddDialogLine("hierarchy_king_low_tier", "start", "hierarchy_king_low_tier_player",
                "{HIERARCHY_KING_LOW}",
                () => {
                    if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsPrisoner) return false; if (Hero.MainHero != null && Hero.MainHero.IsFactionLeader) return false; if (Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return false; bool condition = Hero.OneToOneConversationHero != null && 
                           Hero.OneToOneConversationHero.IsFactionLeader && 
                           Hero.MainHero.Clan != null && Hero.MainHero.Clan.Tier < 2 &&
                           Hero.OneToOneConversationHero.GetRelation(Hero.MainHero) < 10;
                    if (condition) {
                        string[] greetings = {
                            new TextObject("{=rad_hierarchy_k1}How dare you appear before a ruler like me without permission? Who are you and why are you taking up my precious time?").ToString(),
                            new TextObject("{=rad_hierarchy_k2}Guards! What is this wretched commoner doing in my presence? Speak quickly, my patience wears thin.").ToString(),
                            new TextObject("{=rad_hierarchy_k3}Who are you? What accomplishment have you to come before my crown that you dare to speak to me face to face?").ToString()
                        };
                        MBTextManager.SetTextVariable("HIERARCHY_KING_LOW", greetings[MBRandom.RandomInt(greetings.Length)]);
                    }
                    return condition;
                }, null, 200);

            starter.AddPlayerLine("hierarchy_king_low_tier_p1", "hierarchy_king_low_tier_player", "hierarchy_king_low_tier_response",
                "{=rad_hierarchy_p1}[Bow] Forgive me, your majesty. I did not want to overstep my bounds, I merely wished to appear before your supreme presence and pay my respects.",
                null, null);

            starter.AddDialogLine("hierarchy_king_low_tier_response", "hierarchy_king_low_tier_response", "hero_main_options",
                "{=rad_hierarchy_r1}Huh... At least you know your place. Tell me, what do you want?",
                null, null);
                
            // TIER 2-3: KRAL ÝLE KARÞILAÞMA
            starter.AddDialogLine("hierarchy_king_mid_tier", "start", "hierarchy_king_mid_tier_player",
                "{HIERARCHY_KING_MID}",
                () => {
                    if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsPrisoner) return false; if (Hero.MainHero != null && Hero.MainHero.IsFactionLeader) return false; if (Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return false; bool condition = Hero.OneToOneConversationHero != null && 
                           Hero.OneToOneConversationHero.IsFactionLeader && 
                           Hero.MainHero.Clan != null && Hero.MainHero.Clan.Tier >= 2 && Hero.MainHero.Clan.Tier < 4 &&
                           Hero.OneToOneConversationHero.GetRelation(Hero.MainHero) < 20;
                    if (condition) {
                        string[] greetings = {
                            new TextObject("{=rad_hierarchy_k4}I've heard of you. But you have not yet accomplished anything great enough to sit at the table of kings. Tell me, what do you want from me?").ToString(),
                            new TextObject("{=rad_hierarchy_k5}Ah, leader of a rising clan... You may be talented, but in my eyes you still have a lot to prove. Tell me your problem.").ToString(),
                            new TextObject("{=rad_hierarchy_k6}Welcome... I hope you bring useful news for my kingdom, otherwise you are just wasting my time.").ToString()
                        };
                        MBTextManager.SetTextVariable("HIERARCHY_KING_MID", greetings[MBRandom.RandomInt(greetings.Length)]);
                    }
                    return condition;
                }, null, 190);

            starter.AddPlayerLine("hierarchy_king_mid_tier_p1", "hierarchy_king_mid_tier_player", "hierarchy_king_mid_tier_response",
                "{=rad_hierarchy_p2}I am grateful for your presence. Let me get to the point...",
                null, null);

            starter.AddDialogLine("hierarchy_king_mid_tier_resp", "hierarchy_king_mid_tier_response", "hero_main_options",
                "{=rad_hierarchy_r2}I am listening.", null, null);

            // TIER 4+: KRAL ÝLE KARÞILAÞMA
            starter.AddDialogLine("hierarchy_king_high_tier", "start", "hierarchy_king_high_tier_player",
                "{HIERARCHY_KING_HIGH}",
                () => {
                    if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsPrisoner) return false; if (Hero.MainHero != null && Hero.MainHero.IsFactionLeader) return false; if (Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return false; bool condition = Hero.OneToOneConversationHero != null && 
                           Hero.OneToOneConversationHero.IsFactionLeader && 
                           Hero.MainHero.Clan != null && Hero.MainHero.Clan.Tier >= 4 &&
                           Hero.OneToOneConversationHero.GetRelation(Hero.MainHero) > -10;
                    if (condition) {
                        string[] greetings = {
                            new TextObject("{=rad_hierarchy_k7}Oh, mighty leader! Your reputation precedes you, my friend. I would be glad to have great warriors like you on my court.").ToString(),
                            new TextObject("{=rad_hierarchy_k8}Here is one of the true conquerors of Calradia! Come closer, friend, we have a lot to talk about with you.").ToString(),
                            new TextObject("{=rad_hierarchy_k9}Nice to see you {PLAYER.NAME}. My doors are always wide open to the leader of a powerful clan like you!").ToString()
                        };
                        MBTextManager.SetTextVariable("HIERARCHY_KING_HIGH", greetings[MBRandom.RandomInt(greetings.Length)]);
                    }
                    return condition;
                }, null, 180);

            starter.AddPlayerLine("hierarchy_king_high_tier_p1", "hierarchy_king_high_tier_player", "hierarchy_king_high_tier_response",
                "{=rad_hierarchy_p3}The honor is mine, your majesty. I am here to talk about important issues.",
                null, null);
                
            starter.AddDialogLine("hierarchy_king_high_tier_resp", "hierarchy_king_high_tier_response", "hero_main_options",
                "{=rad_hierarchy_r3}Tell me.", null, null);
                
            // TIER 0-1: NORMAL LORD ÝLE KARÞILAÞMA
            starter.AddDialogLine("hierarchy_lord_low_tier", "start", "hierarchy_lord_low_tier_player",
                "{HIERARCHY_LORD_LOW}",
                () => {
                    if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsPrisoner) return false; if (Hero.MainHero != null && Hero.MainHero.IsFactionLeader) return false; if (Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return false; bool condition = Hero.OneToOneConversationHero != null && 
                           Hero.OneToOneConversationHero.IsLord && 
                           !Hero.OneToOneConversationHero.IsFactionLeader &&
                           Hero.MainHero.Clan != null && Hero.MainHero.Clan.Tier < 2 &&
                           Hero.OneToOneConversationHero.Clan != null && Hero.OneToOneConversationHero.Clan.Tier >= 3 &&
                           Hero.OneToOneConversationHero.GetRelation(Hero.MainHero) < 5;
                    if (condition) {
                        string[] greetings = {
                            new TextObject("{=rad_hierarchy_l1}It is not clear whether we are on the battlefield or at the fair... Everyone started to approach me. What do you want, commoner?").ToString(),
                            new TextObject("{=rad_hierarchy_l2}What is so important that it bothers me? Hurry up, the nobles have their hands full.").ToString(),
                            new TextObject("{=rad_hierarchy_l3}It is not clear whether you are a soldier, a merchant, or just a marauder. You must have a good reason to talk to me.").ToString()
                        };
                        MBTextManager.SetTextVariable("HIERARCHY_LORD_LOW", greetings[MBRandom.RandomInt(greetings.Length)]);
                    }
                    return condition;
                }, null, 150);

            starter.AddPlayerLine("hierarchy_lord_low_tier_p1", "hierarchy_lord_low_tier_player", "hierarchy_lord_low_tier_response",
                "{=rad_hierarchy_p4}I apologize for the inconvenience, my lord. I thought only my sword and my word could be worth anything.",
                null, null);

            starter.AddDialogLine("hierarchy_lord_low_tier_response", "hierarchy_lord_low_tier_response", "hero_main_options",
                "{=rad_hierarchy_r4}We will see... Tell me your problem.",
                null, null);

            // BÜTÜN DÜÞÜK TIER'LAR ÝÇÝN PAZARLIK (BARTER) ENGELÝ
            starter.AddDialogLine("hierarchy_block_barter_dialog", "lord_talk_speak_diplomacy_2", "hero_main_options",
                "{=rad_hierarchy_r5}I have no bargaining to do with an unknown like you! Do not come to me with an offer again without proving yourself.",
                () => {
                    return Hero.OneToOneConversationHero != null && 
                           !Hero.OneToOneConversationHero.IsFriend(Hero.MainHero) &&
                           Hero.MainHero.Clan != null && Hero.MainHero.Clan.Tier < 2;
                }, null, 200);
        }
    }
}
