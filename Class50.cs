using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    /* public class GovernmentSaveDefiner : SaveableTypeDefiner
    {
        public GovernmentSaveDefiner() : base(198_456_800) { }
        
    } */

    public class GovernmentStabilityBehavior : CampaignBehaviorBase
    {
        public static GovernmentStabilityBehavior Instance { get; private set; }

        private Dictionary<string, bool> _activeStrikes = new Dictionary<string, bool>();
        private Dictionary<string, int> _cityDebts = new Dictionary<string, int>();
        private Dictionary<string, CampaignTime> _warCooldowns = new Dictionary<string, CampaignTime>();

        public GovernmentStabilityBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_activeStrikes", ref _activeStrikes);
            dataStore.SyncData("_cityDebts", ref _cityDebts);
            dataStore.SyncData("_warCooldowns", ref _warCooldowns);
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (!settlement.IsTown || settlement.OwnerClan != Clan.PlayerClan) return;

            int dailyWage = settlement.Town.GarrisonParty?.TotalWage ?? 0;

            if (Hero.MainHero.Gold < dailyWage)
            {
                AddDebt(settlement, dailyWage);
                // {=gov_msg_debt}{CITY}: Wages unpaid! Debt increasing.
                TextObject msg = new TextObject("{=gov_msg_debt}{CITY}: Wages unpaid! Debt increasing.");
                msg.SetTextVariable("CITY", settlement.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
            }
            else
            {
                if (GetDebt(settlement) > 0 && Hero.MainHero.Gold >= GetDebt(settlement))
                {
                    PayDebt(settlement);
                }
            }

            if (GetDebt(settlement) > 50000 && !IsStrikeActive(settlement))
            {
                SetStrike(settlement, true);
                // {=gov_inquiry_strike_title}GARRISON STRIKE!
                // {=gov_inquiry_strike_text}{CITY} garrison has gone on strike...
                // {=gov_btn_understood}Understood
                TextObject title = new TextObject("{=gov_inquiry_strike_title}GARRISON STRIKE!");
                TextObject text = new TextObject("{=gov_inquiry_strike_text}{CITY} garrison has gone on strike due to unpaid wages! They will not defend the city efficiently.");
                text.SetTextVariable("CITY", settlement.Name);

                InformationManager.ShowInquiry(new InquiryData(title.ToString(), text.ToString(), true, false, new TextObject("{=gov_btn_understood}Understood").ToString(), "", null, null), true);
            }
            else if (GetDebt(settlement) <= 0 && IsStrikeActive(settlement))
            {
                SetStrike(settlement, false);
                // {=gov_msg_strike_end}{CITY} garrison returned to duty.
                TextObject msg = new TextObject("{=gov_msg_strike_end}{CITY} garrison returned to duty.");
                msg.SetTextVariable("CITY", settlement.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
            }
        }

        private void OnHourlyTick()
        {
            List<string> expiredKeys = new List<string>();
            foreach (var kvp in _warCooldowns)
            {
                if (kvp.Value.IsPast) expiredKeys.Add(kvp.Key);
            }

            foreach (var key in expiredKeys)
            {
                _warCooldowns.Remove(key);
            }
        }

        public bool IsStrikeActive(Settlement settlement)
        {
            if (settlement == null) return false;
            return _activeStrikes.ContainsKey(settlement.StringId) && _activeStrikes[settlement.StringId];
        }

        public void SetStrike(Settlement settlement, bool isActive)
        {
            if (isActive) _activeStrikes[settlement.StringId] = true;
            else _activeStrikes.Remove(settlement.StringId);
        }

        public int GetDebt(Settlement settlement) => _cityDebts.ContainsKey(settlement.StringId) ? _cityDebts[settlement.StringId] : 0;

        public void AddDebt(Settlement settlement, int amount)
        {
            if (!_cityDebts.ContainsKey(settlement.StringId)) _cityDebts[settlement.StringId] = 0;
            _cityDebts[settlement.StringId] += amount;
        }

        public void PayDebt(Settlement settlement)
        {
            if (_cityDebts.ContainsKey(settlement.StringId)) _cityDebts[settlement.StringId] = 0;
        }

        public bool IsWarRestricted(Kingdom kingdom)
        {
            if (kingdom == null) return false;
            return _warCooldowns.ContainsKey(kingdom.StringId) && _warCooldowns[kingdom.StringId].IsFuture;
        }

        public void SetWarCooldown(Kingdom kingdom, float days)
        {
            if (kingdom == null) return;
            CampaignTime expireTime = CampaignTime.DaysFromNow(days);
            if (_warCooldowns.ContainsKey(kingdom.StringId)) _warCooldowns[kingdom.StringId] = expireTime;
            else _warCooldowns.Add(kingdom.StringId, expireTime);
        }
    }
}

