using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class WorkshopTradeDialogueBehavior : CampaignBehaviorBase
    {
        private Hero _tradingLord;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private bool IsKingdomLord()
        {
            if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsPrisoner) return false;
            if (Hero.MainHero.MapFaction == null || Hero.OneToOneConversationHero.MapFaction == null) return false;
            if (Hero.OneToOneConversationHero.MapFaction != Hero.MainHero.MapFaction) return false;
            if (Hero.OneToOneConversationHero.Clan == Hero.MainHero.Clan) return false;
            if (!Hero.MainHero.MapFaction.IsKingdomFaction) return false;
            if (!Hero.OneToOneConversationHero.IsLord) return false;
            return Hero.OneToOneConversationHero == Hero.OneToOneConversationHero.Clan?.Leader;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("rad_ws_trade_start", "hero_main_options", "rad_ws_trade_lord_resp",
                "{=rad_ws_trade_01}I would like to discuss a workshop trade with you.",
                IsKingdomLord,
                () => { _tradingLord = Hero.OneToOneConversationHero; });

            starter.AddDialogLine("rad_ws_trade_lord_resp", "rad_ws_trade_lord_resp", "lord_pretalk",
                "{=rad_ws_trade_02}Workshop trade, you say? A bold proposition. Let us see what is available.",
                null,
                OpenCitySelectionMenu);
        }

        private void OpenCitySelectionMenu()
        {
            if (_tradingLord == null) return;
            var kingdom = Hero.MainHero.Clan?.Kingdom;
            if (kingdom == null) return;

            var cityElements = new List<InquiryElement>();
            foreach (var town in Town.AllTowns.Where(t => t.MapFaction == kingdom))
            {
                if (town.Workshops != null && town.Workshops.Length > 0)
                    cityElements.Add(new InquiryElement(town.Settlement, town.Name.ToString(), null));
            }

            if (cityElements.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_ws_no_city}There are no cities with workshops in our realm.").ToString(),
                    Colors.Red));
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=rad_ws_city_title}Select a City").ToString(),
                new TextObject("{=rad_ws_city_desc}Which city's workshops would you like to view?").ToString(),
                cityElements, true, 1, 1,
                new TextObject("{=rad_ws_confirm}Confirm").ToString(),
                new TextObject("{=rad_ws_cancel}Cancel").ToString(),
                (List<InquiryElement> selected) =>
                {
                    if (selected != null && selected.Count > 0)
                    {
                        var settlement = selected[0].Identifier as Settlement;
                        if (settlement?.Town != null) OpenWorkshopSelectionMenu(settlement.Town);
                    }
                },
                null));
        }

        private void OpenWorkshopSelectionMenu(Town town)
        {
            if (town?.Workshops == null) return;

            var workshopElements = new List<InquiryElement>();
            var workshops = town.Workshops;

            for (int i = 0; i < workshops.Length; i++)
            {
                var w = workshops[i];
                if (w == null) continue;

                string ownerName = w.Owner != null ? w.Owner.Name.ToString() : new TextObject("{=rad_ws_empty}Empty").ToString();
                bool isOurs = w.Owner == Hero.MainHero;
                string label = "Workshop " + (i + 1) + " - " + ownerName + (isOurs ? " (Yours)" : "");
                workshopElements.Add(new InquiryElement(w, label, null));
            }

            if (workshopElements.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_ws_no_workshops}No workshops found in this city.").ToString()));
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=rad_ws_wk_title}Workshops in ").ToString() + town.Name.ToString(),
                new TextObject("{=rad_ws_wk_desc}Select a workshop to negotiate. You can sell yours or buy others.").ToString(),
                workshopElements, true, 1, 1,
                new TextObject("{=rad_ws_trade_btn}Negotiate").ToString(),
                new TextObject("{=rad_ws_cancel}Cancel").ToString(),
                (List<InquiryElement> selected) =>
                {
                    if (selected != null && selected.Count > 0)
                    {
                        var workshop = selected[0].Identifier as Workshop;
                        if (workshop != null) OnWorkshopSelected(workshop);
                    }
                },
                null));
        }

        private void OnWorkshopSelected(Workshop workshop)
        {
            if (workshop == null || _tradingLord == null) return;

            bool playerOwns = workshop.Owner == Hero.MainHero;
            int relation = _tradingLord.GetRelation(Hero.MainHero);
            int basePrice = 10000 + (_tradingLord.Clan != null ? _tradingLord.Clan.Tier * 2000 : 0);
            int finalPrice = Math.Max(3000, basePrice - (relation * 150));

            if (playerOwns)
            {
                string title = new TextObject("{=rad_ws_sell_title}Sell Your Workshop").ToString();
                string desc = new TextObject("{=rad_ws_sell_desc}Sell this workshop to ").ToString() + _tradingLord.Name +
                              new TextObject("{=rad_ws_for} for ").ToString() + finalPrice + " Denars?";

                InformationManager.ShowInquiry(new InquiryData(
                    title, desc, true, true,
                    new TextObject("{=rad_ws_sell_btn}Sell").ToString(),
                    new TextObject("{=rad_ws_cancel}Cancel").ToString(),
                    () =>
                    {
                        ChangeOwnerOfWorkshopAction.ApplyByPlayerSelling(workshop, _tradingLord, workshop.WorkshopType);
                        Hero.MainHero.Gold += finalPrice;
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_ws_sold}Workshop sold for ").ToString() + finalPrice + " Denars!",
                            Colors.Green));
                    },
                    null));
            }
            else
            {
                string currentOwner = workshop.Owner != null ? workshop.Owner.Name.ToString() : new TextObject("{=rad_ws_no_one}no one").ToString();
                string title = new TextObject("{=rad_ws_buy_title}Acquire Workshop").ToString();
                string desc = new TextObject("{=rad_ws_buy_desc}Buy the workshop (owned by ").ToString() + currentOwner +
                              new TextObject("{=rad_ws_buy_price}) for ").ToString() + finalPrice + " Denars?";

                InformationManager.ShowInquiry(new InquiryData(
                    title, desc, true, true,
                    new TextObject("{=rad_ws_buy_btn}Buy").ToString(),
                    new TextObject("{=rad_ws_cancel}Cancel").ToString(),
                    () =>
                    {
                        if (Hero.MainHero.Gold < finalPrice)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=rad_ws_no_gold}You do not have enough gold!").ToString(), Colors.Red));
                            return;
                        }
                        Hero.MainHero.Gold -= finalPrice;
                        _tradingLord.Gold += finalPrice / 2;
                        ChangeOwnerOfWorkshopAction.ApplyByPlayerBuying(workshop);
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=rad_ws_bought}Workshop acquired for ").ToString() + finalPrice + " Denars!",
                            Colors.Green));
                    },
                    null));
            }
        }
    }
}
