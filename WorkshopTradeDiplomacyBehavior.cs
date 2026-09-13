using TaleWorlds.CampaignSystem.Settlements;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class WorkshopTradeDiplomacyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Buy Trade Permit
            starter.AddPlayerLine("rad_workshop_trade_permit", "lord_talk_speak_diplomacy_2", "rad_workshop_trade_permit_response", 
                "{=rad_auto_246}I'd like to get a trade permit for one of your towns.", 
                () => {
                    if (Hero.OneToOneConversationHero == null) return false;
                    var lord = Hero.OneToOneConversationHero;
                    var towns = Settlement.All.Where(t => t.IsTown && (lord == t.OwnerClan?.Leader || lord == t.MapFaction?.Leader) && WorkshopKingdomBehavior.Instance != null && !WorkshopKingdomBehavior.Instance.HasTradePermit(t)).ToList();
                    return towns.Count > 0;
                }, 
                null);

            starter.AddDialogLine("rad_workshop_trade_permit_response", "rad_workshop_trade_permit_response", "rad_workshop_trade_permit_choice",
                "{=rad_auto_247}Ah, trade. The lifeblood of our cities... Which city are you interested in?",
                null, null);

            starter.AddPlayerLine("rad_workshop_trade_permit_ui", "rad_workshop_trade_permit_choice", "close_window",
                "{=rad_ws_permit_ui}(Select a town)",
                null,
                () => {
                    var lord = Hero.OneToOneConversationHero;
                    var vm = new CitySelectionVM();
                    vm.Description = new TextObject("{=rad_ws_permit_title}Select a town to purchase a trade permit from {LORD}").SetTextVariable("LORD", lord.Name).ToString();
                    vm.ShowList = true; vm.ShowInput = false; vm.HasAcceptButton = true; vm.HasCancelButton = true;
                    vm.AcceptButtonText = new TextObject("{=rad_btn_accept}Purchase").ToString();
                    vm.CancelButtonText = new TextObject("{=rad_btn_cancel}Cancel").ToString();
                    
                    var towns = Settlement.All.Where(t => t.IsTown && (lord == t.OwnerClan?.Leader || lord == t.MapFaction?.Leader) && !WorkshopKingdomBehavior.Instance.HasTradePermit(t)).ToList();
                    int relation = lord.GetRelation(Hero.MainHero);
                    
                    string selectedTag = null;
                    Action<CitySelectionItemVM, string> onSelect = (item, id) => {
                        foreach(var x in vm.Items) x.IsSelected = false;
                        item.IsSelected = true;
                        selectedTag = id;
                    };
                    
                    int idx = 0;
                    System.Collections.Generic.Dictionary<string, int> prices = new System.Collections.Generic.Dictionary<string, int>();
                    
                    foreach(var t in towns) {
                        int basePrice = 20000 + (int)(t.Town.Prosperity * 5);
                        int price = basePrice - (relation * 500);
                        if (price < 15000) price = 15000;
                        if (price > 100000) price = 100000;
                        
                        prices[t.StringId] = price;
                        
                        string itemName = t.Name.ToString() + " (Prosperity: " + (int)t.Town.Prosperity + ")";
                        string details = relation < -10 ? "Refused due to bad relation." : "Price: " + price + " Denars";
                        
                        vm.Items.Add(new CitySelectionItemVM(idx++, itemName, details, false, (item) => onSelect(item, t.StringId)));
                    }
                    
                    vm.OnAccept = () => {
                        CitySelectionUIManager.Close();
                        if (!string.IsNullOrEmpty(selectedTag) && relation >= -10) {
                            int price = prices[selectedTag];
                            if (Hero.MainHero.Gold >= price) {
                                var town = Settlement.All.FirstOrDefault(x => x.StringId == selectedTag);
                                if (town != null) {
                                    TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, price, true);
                                    WorkshopKingdomBehavior.Instance.GrantTradePermit(town);
                                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_251}Trade permit granted for " + town.Name + "!").ToString(), Colors.Green));
                                }
                            } else {
                                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_nomoney}You don't have enough gold.").ToString(), Colors.Red));
                            }
                        }
                    };
                    
                    vm.OnCancel = () => CitySelectionUIManager.Close();
                    CitySelectionUIManager.Open(vm);
                });
                
            starter.AddPlayerLine("rad_workshop_trade_permit_reject", "rad_workshop_trade_permit_choice", "close_window",
                "{=rad_auto_252}Nevermind.",
                null, null);

            // Buy Specific Workshop Slot
            starter.AddPlayerLine("rad_workshop_buy_slot", "lord_talk_speak_diplomacy_2", "rad_workshop_buy_slot_response", 
                "{=rad_auto_253}I would like to purchase your workshop share in {TOWN_NAME}.", 
                () => {
                    var town = Settlement.CurrentSettlement;
                    if (town == null || !town.IsTown) return false;
                    if (Hero.OneToOneConversationHero == null) return false;
                    if (WorkshopKingdomBehavior.Instance == null) return false;
                    if (!WorkshopKingdomBehavior.Instance.HasTradePermit(town)) return false;
                    
                    var slots = WorkshopKingdomBehavior.Instance.GetOrInitializeSlots(town);
                    bool ownsSlot = slots.Any(s => s.OwnerHeroId == Hero.OneToOneConversationHero.StringId);
                    if (!ownsSlot) return false;
                    
                    MBTextManager.SetTextVariable("TOWN_NAME", town.Name);
                    return true;
                }, 
                null);

            starter.AddDialogLine("rad_workshop_buy_slot_response", "rad_workshop_buy_slot_response", "rad_workshop_buy_slot_choice",
                "{=rad_auto_254}My workshop in {TOWN_NAME}? It generates good income... {RESPONSE_TEXT}",
                () => {
                    int relation = Hero.OneToOneConversationHero.GetRelation(Hero.MainHero);
                    if (relation < -10)
                    {
                        MBTextManager.SetTextVariable("RESPONSE_TEXT", new TaleWorlds.Localization.TextObject("{=rad_auto_255}I am not selling my property to the likes of you."));
                        return true;
                    }
                    
                    int price = 30000 - (relation * 250);
                    if (price < 15000) price = 15000;
                    if (price > 45000) price = 45000;
                    
                    MBTextManager.SetTextVariable("RESPONSE_TEXT", new TaleWorlds.Localization.TextObject("{=rad_auto_256}However, for {PRICE} Denars, I might transfer the deed to you.").SetTextVariable("PRICE", price).ToString());
                    return true;
                }, 
                null);

            starter.AddPlayerLine("rad_workshop_buy_slot_accept", "rad_workshop_buy_slot_choice", "close_window",
                "{=rad_auto_250}Yes, we have a deal.",
                () => {
                    int relation = Hero.OneToOneConversationHero.GetRelation(Hero.MainHero);
                    if (relation < -10) return false;
                    int price = 30000 - (relation * 250);
                    if (price < 15000) price = 15000;
                    if (price > 45000) price = 45000;
                    return Hero.MainHero.Gold >= price;
                },
                () => {
                    var town = Settlement.CurrentSettlement;
                    int relation = Hero.OneToOneConversationHero.GetRelation(Hero.MainHero);
                    int price = 30000 - (relation * 250);
                    if (price < 15000) price = 15000;
                    if (price > 45000) price = 45000;
                    
                    TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, price, true);
                    
                    var slots = WorkshopKingdomBehavior.Instance.GetOrInitializeSlots(town);
                    var slot = slots.FirstOrDefault(s => s.OwnerHeroId == Hero.OneToOneConversationHero.StringId);
                    if (slot != null)
                    {
                        slot.OwnerHeroId = Hero.MainHero.StringId;
                        
                        string newTag = Guid.NewGuid().ToString("N");
                        var newShare = new VirtualWorkshopShare()
                        {
                            SettlementId = town.StringId,
                            WorkshopTypeId = slot.WorkshopTypeId,
                            Name = "Workshop Share",
                            Trait = WorkshopKingdomBehavior.Instance.GetRandomTrait(),
                            Tag = newTag
                        };
                        WorkshopKingdomBehavior.Instance.AddPlayerShare(newShare);
                        InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=rad_auto_257}You acquired a new workshop share!").ToString(), Colors.Green));
                    }
                });

            starter.AddPlayerLine("rad_workshop_buy_slot_reject", "rad_workshop_buy_slot_choice", "close_window",
                "{=rad_auto_252}Nevermind.",
                null, null);

            // Give/Transfer Workshop
            starter.AddPlayerLine("rad_workshop_give_slot", "lord_talk_speak_diplomacy_2", "rad_workshop_give_response", 
                "{=rad_ws_give}I would like to transfer one of my workshops to you.", 
                () => {
                    if (Hero.OneToOneConversationHero == null) return false;
                    if (WorkshopKingdomBehavior.Instance == null) return false;
                    var myShares = WorkshopKingdomBehavior.Instance.PlayerShares;
                    return myShares != null && myShares.Count > 0;
                }, 
                null);

            starter.AddDialogLine("rad_workshop_give_response", "rad_workshop_give_response", "rad_workshop_give_choice",
                "{=rad_ws_give_resp}A generous offer indeed. Which workshop are you talking about?",
                null, null);

            starter.AddPlayerLine("rad_workshop_give_open_ui", "rad_workshop_give_choice", "close_window",
                "{=rad_ws_give_ui}(Select a workshop)",
                null,
                () => {
                    var lord = Hero.OneToOneConversationHero;
                    
                    var vm = new CitySelectionVM();
                    vm.Description = new TextObject("{=rad_ws_transfer_title}Select a workshop to transfer to {LORD}").SetTextVariable("LORD", lord.Name).ToString();
                    vm.ShowList = true; vm.ShowInput = false; vm.HasAcceptButton = true; vm.HasCancelButton = true;
                    vm.AcceptButtonText = new TextObject("{=rad_btn_accept}Transfer").ToString();
                    vm.CancelButtonText = new TextObject("{=rad_btn_cancel}Cancel").ToString();
                    
                    var shares = WorkshopKingdomBehavior.Instance.PlayerShares;
                    string selectedTag = null;
                    Action<CitySelectionItemVM, string> onSelect = (item, id) => {
                        foreach(var x in vm.Items) x.IsSelected = false;
                        item.IsSelected = true;
                        selectedTag = id;
                    };
                    
                    int idx = 0;
                    foreach(var s in shares) {
                        var settlement = Settlement.All.FirstOrDefault(x => x.StringId == s.SettlementId);
                        string loc = settlement != null ? settlement.Name.ToString() : "Unknown";
                        vm.Items.Add(new CitySelectionItemVM(idx++, s.Name + " (" + loc + ")", "Level: " + s.Level, false, (item) => onSelect(item, s.Tag)));
                    }
                    
                    vm.OnAccept = () => {
                        CitySelectionUIManager.Close();
                        if (!string.IsNullOrEmpty(selectedTag)) {
                            var share = WorkshopKingdomBehavior.Instance.PlayerShares.FirstOrDefault(x => x.Tag == selectedTag);
                            if (share != null) {
                                var town = Settlement.All.FirstOrDefault(x => x.StringId == share.SettlementId);
                                if (town != null) {
                                    var slots = WorkshopKingdomBehavior.Instance.GetOrInitializeSlots(town);
                                    var slot = slots.FirstOrDefault(x => x.OwnerHeroId == Hero.MainHero.StringId && x.WorkshopTypeId == share.WorkshopTypeId);
                                    if (slot != null) {
                                        slot.OwnerHeroId = lord.StringId; 
                                    }
                                }
                                WorkshopKingdomBehavior.Instance.RemoveShareCompletely(share);
                                TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyPlayerRelation(lord, 15, true, true);
                                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_ws_transferred}Workshop transferred to {LORD}!").SetTextVariable("LORD", lord.Name).ToString(), Colors.Green));
                            }
                        }
                    };
                    vm.OnCancel = () => CitySelectionUIManager.Close();
                    
                    CitySelectionUIManager.Open(vm);
                });
                
            starter.AddPlayerLine("rad_workshop_give_reject", "rad_workshop_give_choice", "close_window",
                "{=rad_auto_252}Nevermind.",
                null, null);

        }
    }
}
