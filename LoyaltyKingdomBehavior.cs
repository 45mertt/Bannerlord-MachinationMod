using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class LoyaltyKingdomBehavior : CampaignBehaviorBase
    {
        public static LoyaltyKingdomBehavior Instance { get; private set; }

        private LoyaltyKingdomData _data;
        
        // Active simple tasks for the player
        public enum LoyaltyTaskType { None, Grain, Tools, WarHorses }
        private LoyaltyTaskType _activeTask = LoyaltyTaskType.None;
        private int _taskAmountRequired = 0;
        private Hero _taskGiver = null;

        public LoyaltyKingdomBehavior()
        {
            Instance = this;
            _data = new LoyaltyKingdomData();
        }

        public float GetClanLoyalty(Clan clan) => _data.GetLoyalty(clan);

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            // Passive gains: capturing a settlement gives loyalty
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (capturer != null && capturer.LeaderHero != null && capturer.LeaderHero.Clan != null && prisoner.MapFaction != capturer.LeaderHero.MapFaction)
            {
                // Captured an enemy noble
                _data.AddLoyalty(capturer.LeaderHero.Clan, 5f);
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (capturerHero != null && capturerHero.Clan != null && capturerHero.Clan.Kingdom != null)
            {
                _data.AddLoyalty(capturerHero.Clan, 25f);
            }
        }

        private void OnDailyTick()
        {
            // Small passive loyalty gain for clans over time based on influence maybe?
            foreach (var clan in Clan.All)
            {
                if (clan.IsUnderMercenaryService || clan.Kingdom == null) continue;
                if (!(clan.Kingdom != null && clan.Kingdom.RulingClan == clan))
                {
                    // Lose a tiny bit of loyalty over time if not proving oneself (decay)
                    // _data.AddLoyalty(clan, -0.5f);
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Player asks about proving loyalty
            starter.AddPlayerLine("rad_loyalty_ask", "hero_main_options", "rad_loyalty_response",
                "{=rad_loyalty_01}My lord, how can I prove my loyalty to the realm?",
                () => {
                    if (Hero.OneToOneConversationHero == null) return false;
                    var lord = Hero.OneToOneConversationHero;
                    // Only ask lords of your own kingdom
                    if (Hero.MainHero.MapFaction == null || lord.MapFaction != Hero.MainHero.MapFaction) return false;
                    if (lord.Clan == Hero.MainHero.Clan) return false;
                    return _activeTask == LoyaltyTaskType.None;
                },
                null);

            starter.AddDialogLine("rad_loyalty_response", "rad_loyalty_response", "rad_loyalty_options",
                "{=rad_loyalty_02}The council always remembers those who serve the realm. {TASK_DESC}",
                () => {
                    // Generate a random task
                    int rand = MBRandom.RandomInt(1, 4);
                    if (rand == 1) {
                        _activeTask = LoyaltyTaskType.Grain;
                        _taskAmountRequired = 100;
                        MBTextManager.SetTextVariable("TASK_DESC", new TextObject("{=rad_loyalty_t1}Our armies are starving. Bring us 100 Grain to prove your worth."));
                    } else if (rand == 2) {
                        _activeTask = LoyaltyTaskType.Tools;
                        _taskAmountRequired = 20;
                        MBTextManager.SetTextVariable("TASK_DESC", new TextObject("{=rad_loyalty_t2}Our siege engineers need supplies. Bring us 20 Tools."));
                    } else {
                        _activeTask = LoyaltyTaskType.WarHorses;
                        _taskAmountRequired = 10;
                        MBTextManager.SetTextVariable("TASK_DESC", new TextObject("{=rad_loyalty_t3}Our cavalry needs fresh mounts. Bring us 10 War Horses."));
                    }
                    _taskGiver = Hero.OneToOneConversationHero;
                    return true;
                }, null);

            starter.AddPlayerLine("rad_loyalty_accept", "rad_loyalty_options", "close_window",
                "{=rad_loyalty_03}I will return with the supplies.",
                null, null);
                
            starter.AddPlayerLine("rad_loyalty_reject", "rad_loyalty_options", "close_window",
                "{=rad_loyalty_04}I don't have time for this right now.",
                null, () => {
                    _activeTask = LoyaltyTaskType.None;
                    _taskGiver = null;
                });

            // Handing in the task
            starter.AddPlayerLine("rad_loyalty_hand_in", "hero_main_options", "rad_loyalty_hand_in_response",
                "{=rad_loyalty_05}I brought the supplies you requested for the realm.",
                () => {
                    if (_activeTask == LoyaltyTaskType.None || Hero.OneToOneConversationHero != _taskGiver) return false;
                    return HasEnoughSupplies();
                },
                null);

            starter.AddDialogLine("rad_loyalty_hand_in_response", "rad_loyalty_hand_in_response", "close_window",
                "{=rad_loyalty_06}Excellent work! The realm will not forget your dedication. I will speak highly of you in the council.",
                null,
                () => {
                    TakeSupplies();
                    _data.AddLoyalty(Hero.MainHero.Clan, 30f);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_loyalty_msg}You have gained 30 Loyalty/Merit in the kingdom!").ToString(), Colors.Green));
                    _activeTask = LoyaltyTaskType.None;
                    _taskGiver = null;
                });
                
            // Check status
            starter.AddPlayerLine("rad_loyalty_status", "hero_main_options", "rad_loyalty_status_response",
                "{=rad_loyalty_07}How is my standing in the kingdom council?",
                () => {
                    if (Hero.OneToOneConversationHero == null || Hero.MainHero.MapFaction == null) return false;
                    return Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction && Hero.OneToOneConversationHero.IsLord;
                }, null);
                
            starter.AddDialogLine("rad_loyalty_status_response", "rad_loyalty_status_response", "hero_main_options",
                "{STATUS_DESC}",
                () => {
                    float loyalty = _data.GetLoyalty(Hero.MainHero.Clan);
                    if (loyalty < 50f) {
                        MBTextManager.SetTextVariable("STATUS_DESC", new TextObject("{=rad_loyalty_s1}Honestly? The council barely considers you a true part of the realm yet. You need to prove yourself."));
                    } else if (loyalty < 150f) {
                        MBTextManager.SetTextVariable("STATUS_DESC", new TextObject("{=rad_loyalty_s2}You are known and respected, but the senior clans still hold more weight than you."));
                    } else {
                        MBTextManager.SetTextVariable("STATUS_DESC", new TextObject("{=rad_loyalty_s3}You are a pillar of our kingdom. The ruler and the council trust you implicitly."));
                    }
                    return true;
                }, null);
        }

        private bool HasEnoughSupplies()
        {
            if (PartyBase.MainParty == null || PartyBase.MainParty.ItemRoster == null) return false;
            
            string itemId = _activeTask == LoyaltyTaskType.Grain ? "grain" : (_activeTask == LoyaltyTaskType.Tools ? "tools" : "war_horse");
            int count = 0;
            
            for (int i = 0; i < PartyBase.MainParty.ItemRoster.Count; i++)
            {
                var element = PartyBase.MainParty.ItemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.StringId.Contains(itemId))
                {
                    count += element.Amount;
                }
            }
            return count >= _taskAmountRequired;
        }

        private void TakeSupplies()
        {
            string itemId = _activeTask == LoyaltyTaskType.Grain ? "grain" : (_activeTask == LoyaltyTaskType.Tools ? "tools" : "war_horse");
            int remaining = _taskAmountRequired;
            
            for (int i = PartyBase.MainParty.ItemRoster.Count - 1; i >= 0; i--)
            {
                var element = PartyBase.MainParty.ItemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.StringId.Contains(itemId))
                {
                    int toTake = Math.Min(remaining, element.Amount);
                    PartyBase.MainParty.ItemRoster.AddToCounts(element.EquipmentElement, -toTake);
                    remaining -= toTake;
                    if (remaining <= 0) break;
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (_data == null) _data = new LoyaltyKingdomData();
            _data.SyncData(dataStore);
            
            dataStore.SyncData("_rad_loyalty_active_task", ref _activeTask);
            dataStore.SyncData("_rad_loyalty_task_amount", ref _taskAmountRequired);
            dataStore.SyncData("_rad_loyalty_task_giver", ref _taskGiver);
        }
    }
}
