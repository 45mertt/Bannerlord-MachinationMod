using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class MonopolyTreasonBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Spy/Messenger approaching the player on map
            starter.AddDialogLine("monopoly_messenger_start", "start", "monopoly_messenger_reply", 
                "{=rad_auto_325}Sir... I am coming on behalf of {ENEMY_KING_NAME}. Your workshop monopoly in the cities attracted my king's attention. We have a very profitable offer for you.", 
                () => 
                {
                    if (Hero.OneToOneConversationHero != null) return false;
                    if (CharacterObject.OneToOneConversationCharacter == null) return false;
                    if (CharacterObject.OneToOneConversationCharacter.Occupation != Occupation.Wanderer && CharacterObject.OneToOneConversationCharacter.Occupation != Occupation.Mercenary) return false;
                    
                    // Logic to trigger only when the player meets criteria (cooldown + >50% monopoly + at war)
                    return CheckIfMessengerShouldAppear();
                }, 
                null, 100, null);

            starter.AddPlayerLine("monopoly_messenger_reply_1", "monopoly_messenger_reply", "monopoly_messenger_offer", 
                "{=rad_auto_330}Tell me, what is this offer?", null, null);
            starter.AddPlayerLine("monopoly_messenger_reply_2", "monopoly_messenger_reply", "close_window", 
                "{=rad_auto_331}I have no use for spies from an enemy kingdom! Go away!", null, () => { WorkshopKingdomBehavior.Instance.LastTreasonOfferTime = (float)CampaignTime.Now.ToDays; });

            starter.AddDialogLine("monopoly_messenger_offer", "monopoly_messenger_offer", "monopoly_messenger_choices", 
                "{=rad_auto_326}You have two options: Either you will stop the production in your workshops and collapse the economy of this kingdom (Economic Sabotage), or you will connect your workshops to our spy network. You will be handsomely rewarded for both.", 
                null, null);

            starter.AddPlayerLine("monopoly_messenger_choice_sabotage", "monopoly_messenger_choices", "monopoly_messenger_end_sabotage", 
                "{=rad_auto_332}I accept Economic Sabotage. I will stop production in the workshops.", null, 
                () => { AcceptSabotage(); });
            
            starter.AddPlayerLine("monopoly_messenger_choice_spy", "monopoly_messenger_choices", "monopoly_messenger_end_spy", 
                "{=rad_auto_333}I agree to establish a Spy Network. I will provide insider information.", null, 
                () => { AcceptSpyNetwork(); });

            starter.AddPlayerLine("monopoly_messenger_choice_refuse", "monopoly_messenger_choices", "close_window", 
                "{=rad_auto_334}I reject your offer. I will not betray my king.", null, 
                () => { WorkshopKingdomBehavior.Instance.LastTreasonOfferTime = (float)CampaignTime.Now.ToDays; });

            starter.AddDialogLine("monopoly_messenger_end_sabotage", "monopoly_messenger_end_sabotage", "close_window", 
                "{=rad_auto_327}My king will be very happy with your decision. The deal has begun.", null, null);
            
            starter.AddDialogLine("monopoly_messenger_end_spy", "monopoly_messenger_end_spy", "close_window", 
                "{=rad_auto_328}Information network was established. Your loyalty will be generously rewarded.", null, null);

            // King Reporting Dialog
            starter.AddPlayerLine("monopoly_report_king", "hero_main_options", "monopoly_report_king_response", 
                "{=rad_auto_335}My King, an offer of betrayal has come to me from the enemy kingdom. They want to collapse our economy by using my workshops.", 
                () => 
                {
                    if (Hero.OneToOneConversationHero == null || Hero.MainHero.MapFaction == null) return false;
                    return Hero.OneToOneConversationHero == Hero.MainHero.MapFaction.Leader && HasUnreportedOffer();
                }, null, 100, null);

            starter.AddDialogLine("monopoly_report_king_response", "monopoly_report_king_response", "hero_main_options", 
                "{=rad_auto_329}This is very valuable intelligence! Your loyalty to us is admirable. We will make you pay for this arrogance on the battlefield!", 
                null, 
                () => 
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 15);
                    GainKingdomInfluenceAction.ApplyForDefault(Hero.MainHero, 50f);
                    WorkshopKingdomBehavior.Instance.LastTreasonOfferTime = (float)CampaignTime.Now.ToDays;
                });
        }

        private bool CheckIfMessengerShouldAppear()
        {
            if (WorkshopKingdomBehavior.Instance == null) return false;
            if (Hero.MainHero.MapFaction == null || !Hero.MainHero.MapFaction.IsKingdomFaction) return false;
            if (Hero.MainHero.MapFaction.Leader == Hero.MainHero) return false;
            
            float lastOfferDays = WorkshopKingdomBehavior.Instance.LastTreasonOfferTime;
            if (CampaignTime.Now.ToDays - lastOfferDays < 30) return false; // 30 days cooldown

            // Check monopoly
            var kingdom = Hero.MainHero.MapFaction as Kingdom;
            int totalKingdomWorkshops = kingdom.Settlements.Where(s => s.IsTown && s.Town != null && s.Town.Workshops != null).Sum(s => s.Town.Workshops.Length);
            if (totalKingdomWorkshops == 0) return false;

            int playerShareCount = WorkshopKingdomBehavior.Instance.PlayerShares.Count(s => kingdom.Settlements.Any(ks => ks.StringId == s.SettlementId));
            float ratio = (float)playerShareCount / totalKingdomWorkshops;

            if (ratio < 0.5f) return false;

            var enemyKingdom = Kingdom.All.FirstOrDefault(k => k != kingdom && k.IsAtWarWith(kingdom) && k.Leader != null);
            if (enemyKingdom == null) return false;

            MBTextManager.SetTextVariable("ENEMY_KING_NAME", enemyKingdom.Leader.Name);
            return true;
        }

        private void AcceptSabotage()
        {
            WorkshopKingdomBehavior.Instance.LastTreasonOfferTime = (float)CampaignTime.Now.ToDays;
            var kingdom = Hero.MainHero.MapFaction as Kingdom;
            var enemyKingdom = Kingdom.All.FirstOrDefault(k => k != kingdom && k.IsAtWarWith(kingdom));
            
            WorkshopKingdomBehavior.Instance.ActiveSabotageKingdom = enemyKingdom;
            WorkshopKingdomBehavior.Instance.ActiveSabotageDaysLeft = 10;
            
            foreach (var share in WorkshopKingdomBehavior.Instance.PlayerShares.Where(s => kingdom.Settlements.Any(ks => ks.StringId == s.SettlementId)))
            {
                share.StrategyType = -1; // -1 means sabotage
            }
        }

        private void AcceptSpyNetwork()
        {
            WorkshopKingdomBehavior.Instance.LastTreasonOfferTime = (float)CampaignTime.Now.ToDays;
            var kingdom = Hero.MainHero.MapFaction as Kingdom;
            var enemyKingdom = Kingdom.All.FirstOrDefault(k => k != kingdom && k.IsAtWarWith(kingdom));
            
            WorkshopKingdomBehavior.Instance.ActiveSpyKingdom = enemyKingdom;
            WorkshopKingdomBehavior.Instance.ActiveSpyDaysLeft = 20;
        }

        private bool HasUnreportedOffer()
        {
            float lastOfferDays = WorkshopKingdomBehavior.Instance.LastTreasonOfferTime;
            return (CampaignTime.Now.ToDays - lastOfferDays > 30) && CheckIfMessengerShouldAppear(); // Simplified check for demonstration
        }
    }
}
