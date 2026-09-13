using System;
using System.Collections.Generic; // HashSet kullanımı için eklendi
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;
using RebellionsAndDemographics;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ClassLibrary22
{
    public class MessengerEventBehavior : CampaignBehaviorBase
    {
        // Hafıza değişkenlerimiz
        private int _lasttier = 0;
        private List<string> _warnedSettlements = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnMarriageOfferedToPlayerEvent.AddNonSerializedListener(this, OnMarriageOfferedToPlayer);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnPeaceOfferedToPlayerEvent.AddNonSerializedListener(this, OnPeaceOfferedToPlayer);
            CampaignEvents.KingdomDecisionAdded.AddNonSerializedListener(this, OnKingdomDecisionAdded);
            CampaignEvents.OnRansomOfferedToPlayerEvent.AddNonSerializedListener(this, OnRansomOfferedToPlayer);
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnClanTierIncreased);
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStartedEvent);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, SadakatProblemi);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Oyun kaydedilirken verileri tutuyoruz
            dataStore.SyncData("_lasttier", ref _lasttier);
            dataStore.SyncData("_warnedSettlements", ref _warnedSettlements);
        }

        private bool IsPlayerInvolved(Hero hero)
        {
            if (!ModSettings.OnlyPlayerKingdomEvents) return true;
            return hero?.Clan == Clan.PlayerClan || hero?.MapFaction == Clan.PlayerClan?.MapFaction;
        }

        private bool IsPlayerInvolved(IFaction faction1, IFaction faction2)
        {
            if (!ModSettings.OnlyPlayerKingdomEvents) return true;
            return faction1 == Clan.PlayerClan?.MapFaction || faction2 == Clan.PlayerClan?.MapFaction;
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (ModSettings.EnableFamilyEventPopup && IsPlayerInvolved(victim))
            {
                TextObject title = new TextObject("{=msg_hero_killed_title}Tragic Loss");
                TextObject message = new TextObject("{=msg_hero_killed_msg}My Lord,\n\nI write this letter to you with trembling hands and great sorrow. Unfortunately, the feared has happened and bitter news has knocked on our door. {VICTIM_NAME}, has passed away in a relentless struggle. This loss will create an irreplaceable void not only for our clan but for the entire realm.\n\nWhile our tears flow like a flood, we must know that our enemies will not sit idle even on this bitter day. May the deceased rest in peace, their sword will never be forgotten...");
                message.SetTextVariable("VICTIM_NAME", victim.Name);

                MessengerPopupManager.Open(title.ToString(), message.ToString());
            }
        }

        private void OnMarriageOfferedToPlayer(Hero hero1, Hero hero2)
        {
            if (ModSettings.EnableFamilyEventPopup)
            {
                TextObject title = new TextObject("{=msg_marriage_title}Blessed Union");
                TextObject message = new TextObject("{=msg_marriage_msg}My Lord,\n\nI bring you very important news from across the realms that will unite hearts. A blessed marriage proposal has been made for {HERO_1} and {HERO_2} in order to strengthen the bonds between noble clans and look to the future with hope.\n\nThis glorious union will bring together not only two bodies, but also our banners and armies. If you approve this proposal, the wedding festivities will last for weeks and your name will be remembered with glory. Your decision is awaited...");
                message.SetTextVariable("HERO_1", hero1.Name);
                message.SetTextVariable("HERO_2", hero2.Name);

                MessengerPopupManager.Open(title.ToString(), message.ToString());
            }
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            

            if (!ModSettings.EnableWarPeacePopup) return;
            if (!IsPlayerInvolved(faction1, faction2)) return;

            IFaction ourFaction = faction1 == Clan.PlayerClan.MapFaction ? faction1 : faction2;
            IFaction enemyFaction = faction1 == Clan.PlayerClan.MapFaction ? faction2 : faction1;

            float ourStrength = ourFaction.CurrentTotalStrength;
            float enemyStrength = enemyFaction.CurrentTotalStrength;

            TextObject title = new TextObject("{=msg_war_title}Declaration of War!");
            TextObject message;

            if (ourStrength > enemyStrength * 1.5f)
            {
                message = new TextObject("{=msg_war_weak_enemy}My Lord,\n\nThe audacious {ENEMY_FACTION} has had the gall to declare war on us. According to our spies, their armies are weak and unequipped. It is time to unsheathe our swords and show them our true power. They will pay for this disrespect with their blood!");
            }
            else if (enemyStrength > ourStrength * 1.5f)
            {
                message = new TextObject("{=msg_war_strong_enemy}My Lord,\n\nI send you this letter with bitter news. {ENEMY_FACTION}, which is vastly superior to us in numbers and strength, has officially declared war and is marching upon us. Dark clouds are gathering above us, the people are anxious. We need a defense plan urgently. May the Gods be with us...");
            }
            else
            {
                message = new TextObject("{=msg_war_equal_enemy}My Lord,\n\nTaking their border violations one step further, {ENEMY_FACTION} has officially declared war. Our forces are evenly matched. The fate of this relentless war will be determined by your intelligence, your tactics, and the courage of our soldiers. Our troops await your command!");
            }

            message.SetTextVariable("ENEMY_FACTION", enemyFaction.Name);
            MessengerPopupManager.Open(title.ToString(), message.ToString());
        }

        private void OnPeaceOfferedToPlayer(IFaction opponentFaction, int tributeAmount, int somethingElse)
        {
            TextObject debugMsg = new TextObject("{=msg_peace_debug}DEBUG: OnPeaceOfferedToPlayer Triggered! {OPPONENT_FACTION}");
            debugMsg.SetTextVariable("OPPONENT_FACTION", opponentFaction?.Name ?? new TextObject("{=msg_unknown}Unknown"));
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(debugMsg.ToString()));

            if (!ModSettings.EnableWarPeacePopup) return;

            TextObject title = new TextObject("{=msg_peace_title}Peace Offer");
            TextObject message = new TextObject("{=msg_peace_msg}My Lord,\n\nAn envoy from the kingdom of {OPPONENT_FACTION} has reached our court from the war-torn lands. They are offering a peace treaty to stop the bloodshed and sheathe the swords.\n\nIf you accept this offer, our soldiers will return home and we will have the opportunity to heal our wounds. The envoy is waiting at the council door to deliver your decision...");
            message.SetTextVariable("OPPONENT_FACTION", opponentFaction.Name);

            MessengerPopupManager.Open(title.ToString(), message.ToString());
        }

        private void OnKingdomDecisionAdded(KingdomDecision decision, bool isPlayerInvolved)
        {
            if (!ModSettings.EnableDecisionPopup) return;
            if (decision.Kingdom != Clan.PlayerClan.Kingdom) return;

            TextObject title = new TextObject("{=msg_decision_title}The Council Gathers");
            TextObject message;

            if (decision is SettlementClaimantDecision settlementDecision)
            {
                message = new TextObject("{=msg_decision_settlement}My Lord,\n\nThe fate of the lands we conquered is about to be determined. The council of lords has gathered for {SETTLEMENT_NAME}. Your vote and influence will determine who will be blessed with these fertile lands. The council is in need of your wisdom and justice.");
                message.SetTextVariable("SETTLEMENT_NAME", settlementDecision.Settlement.Name);
            }
            else if (decision is KingdomPolicyDecision policyDecision)
            {
                message = new TextObject("{=msg_decision_policy}My Lord,\n\nA new draft law that will affect the foundation stones of our kingdom has been presented to the council. The lords are divided and heated discussions are taking place. You must attend the council and cast your vote to shape the future of the kingdom.");
            }
            else
            {
                message = new TextObject("{=msg_decision_other}My Lord,\n\nThe council of lords has convened urgently for an important decision concerning our kingdom. Your decision will directly affect the fate of the kingdom. You are expected to take your place in the council hall.");
            }

            MessengerPopupManager.Open(title.ToString(), message.ToString());
        }

        private void OnRansomOfferedToPlayer(Hero captiveHero)
        {
            if (!ModSettings.EnableRansomPopup) return;

            TextObject title = new TextObject("{=msg_ransom_title}Ransom Offer");
            TextObject message = new TextObject("{=msg_ransom_msg}My Lord,\n\nAn intermediary has reached our camp for {CAPTIVE_HERO}, whom we hold in our dungeons. They demand their freedom in exchange for a purse full of gold.\n\nWe can accept this offer and fill our treasury, or continue to let our enemy rot in the dungeons. Your decision is awaited...");
            message.SetTextVariable("CAPTIVE_HERO", captiveHero.Name);

            MessengerPopupManager.Open(title.ToString(), message.ToString());
        }

        private void OnClanTierIncreased(Clan clan)
        {
            if (clan == Clan.PlayerClan && clan.Tier > _lasttier)
            {
                _lasttier = clan.Tier;
                TextObject title = new TextObject("{=msg_clan_tier_title}A New Era of Glory");
                TextObject message = new TextObject("{=msg_clan_tier_msg}My Lord,\n\nGreat news travels fast across Calradia! The triumphs, valor, and honor of {CLAN_NAME} are now sung by bards in every tavern and spoken with respect in the halls of kings.\n\nOur clan has officially reached Tier {TIER_LEVEL}. With this newfound renown, more warriors will be eager to join under your banner, and other nobles will hold your council in higher regard. May your glory continue to echo through the ages!");

                message.SetTextVariable("CLAN_NAME", clan.Name);
                message.SetTextVariable("TIER_LEVEL", clan.Tier);

                MessengerPopupManager.Open(title.ToString(), message.ToString());
            }
        }

        private void OnSiegeEventStartedEvent(SiegeEvent olay)
        {
            if (olay.BesiegedSettlement?.OwnerClan == Clan.PlayerClan)
            {
                TextObject title = new TextObject("{=msg_siege_title}Stronghold Under Siege!");
                TextObject message = new TextObject("{=msg_siege_msg}My Lord,\n\nDire news from our scouts! Hostile forces have surrounded {SETTLEMENT_NAME} and commenced siege operations. The garrison is manning the walls, but the enemy is constructing war machines and preparing for a bloody assault.\n\nOur defenders await your relief force before the walls are breached and all is lost. Make haste, my Lord!");

                message.SetTextVariable("SETTLEMENT_NAME", olay.BesiegedSettlement.Name);

                MessengerPopupManager.Open(title.ToString(), message.ToString());
            }
        }

        private void SadakatProblemi(Settlement settlement)
        {
            if (settlement == null || !settlement.IsTown || settlement.OwnerClan != Clan.PlayerClan)
                return;

            float loyalty = settlement.Town.Loyalty;
            string settlementId = settlement.StringId;

            // Sadakat toparlanmışsa kilidi kaldır
            if (loyalty >= 30f && _warnedSettlements.Contains(settlementId))
            {
                _warnedSettlements.Remove(settlementId);
            }
            // Sadakat 20'nin altında ve henüz uyarılmamışsa
            else if (loyalty < 20f && !_warnedSettlements.Contains(settlementId))
            {
                _warnedSettlements.Add(settlementId);

                TextObject title = null;
                TextObject message = null;

                Random rnd = new Random();
                int sayi = rnd.Next(1, 4);

                if (sayi == 1)
                {
                    title = new TextObject("{=msg_low_loyalty_title_1}Whispers of Rebellion in {SETTLEMENT_NAME}!");
                    message = new TextObject("{=msg_low_loyalty_msg_1}My Lord,\n\nDark omens reach us from {SETTLEMENT_NAME}. The streets are rife with sedition, and the common folk openly curse your banners. Town criers report that loyalty has plummeted to a critical level of {LOYALTY_LEVEL}.\n\nSecret assemblies gather in the shadows of the alleys, and our magistrates fear an open rebellion will ignite at any moment. Unless order is restored and grievances are answered, the gates will soon be barred against your rule!");
                }
                else if (sayi == 2)
                {
                    title = new TextObject("{=msg_low_loyalty_title_2}Unrest and Anarchy at {SETTLEMENT_NAME}!");
                    message = new TextObject("{=msg_low_loyalty_msg_2}My Lord,\n\nOur bailiffs in {SETTLEMENT_NAME} send word of grave disorder. The merchant guilds have closed their doors, grain stores are looted, and local rabble-rousers are arming the mob. Loyalty stands at a dire {LOYALTY_LEVEL}.\n\nOur garrison can barely keep the peace, and the citizens view us as conquerors rather than rightful protectors. If this defiance is not quelled swiftly, we risk losing the settlement entirely to an armed uprising!");
                }
                else
                {
                    title = new TextObject("{=msg_low_loyalty_title_3}Treason Brewing in {SETTLEMENT_NAME}");
                    message = new TextObject("{=msg_low_loyalty_msg_3}My Lord,\n\nThe garrison commander of {SETTLEMENT_NAME} warns that local agitators are arming the lower districts. The loyalty of the town has plunged to dangerous depths of {LOYALTY_LEVEL}. A rebellion is at hand!");
                }

                title.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                // Loyalty küsüratlı gelir, 15.3 gibi okunaklı göstermek için formatlıyoruz:
                message.SetTextVariable("LOYALTY_LEVEL", loyalty.ToString("0.0"));

                MessengerPopupManager.Open(title.ToString(), message.ToString());
            }
        }
    }
}
