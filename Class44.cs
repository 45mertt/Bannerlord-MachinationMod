using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace RebellionsAndDemographics
{
    public class WarCouncilEnforcerBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in WarStrategyManager.ActiveOrders.ToList()) { if (!(pair.Key != null && pair.Value != null)) WarStrategyManager.ActiveOrders.Remove(pair.Key); }
            }
            dataStore.SyncData("WarCouncil_ActiveOrders", ref WarStrategyManager._activeOrders);
        }

        private void OnHourlyTick()
        {
            if (WarStrategyManager.ActiveOrders == null || WarStrategyManager.ActiveOrders.Count == 0) return;

            var activeKeys = new List<Hero>(WarStrategyManager.ActiveOrders.Keys);

            foreach (var lord in activeKeys)
            {
                var order = WarStrategyManager.ActiveOrders[lord];

                if (lord == null || !lord.IsAlive || lord.IsPrisoner || lord.PartyBelongedTo == null)
                {
                    WarStrategyManager.ActiveOrders.Remove(lord);
                    continue;
                }

                if (CheckIfOrderCompleted(lord, order))
                {
                    WarStrategyManager.ActiveOrders.Remove(lord);
                    continue;
                }

                if (order.OrderType == WarOrderType.FormArmy)
                {
                    // HATA DÜZELTME: Army.AIBehavior yerine Lider Partiyi Zorluyoruz
                    if (lord.PartyBelongedTo.Army != null && lord.PartyBelongedTo.Army.LeaderParty == lord.PartyBelongedTo)
                    {
                        if (order.TargetSettlement != null)
                        {
                            // Lider partiye "Surround It" diyoruz. Ordu onu takip eder.
                            // Eğer zaten kuşatmıyorsa veya kuşatmaya gitmiyorsa zorla.
                            if (lord.PartyBelongedTo.TargetSettlement != order.TargetSettlement)
                            {
                                SafeSetBesiegeSettlement(lord.PartyBelongedTo, order.TargetSettlement);
                            }
                        }
                    }
                }
                // Diğer tipler için de (SiegeSpecific vb.) aynı mantığı uyguluyoruz
                else if (order.OrderType == WarOrderType.SiegeSpecific && order.TargetSettlement != null)
                {
                    if (lord.PartyBelongedTo.TargetSettlement != order.TargetSettlement)
                    {
                        SafeSetBesiegeSettlement(lord.PartyBelongedTo, order.TargetSettlement);
                    }
                }
            }
        }

        private bool CheckIfOrderCompleted(Hero lord, WarOrder order)
        {
            if (order.TargetSettlement != null && order.TargetSettlement.MapFaction == lord.MapFaction) return true;
            if (order.TargetFaction != null && !lord.MapFaction.IsAtWarWith(order.TargetFaction)) return true;
            return false;
        }

        // --- REFLECTION YARDIMCILARI ---
        // (Bu metodlar oyunun internal komutlarını zorla çalıştırır)

        private void SafeSetBesiegeSettlement(MobileParty actor, Settlement target)
        {
            try
            {
                // Önce oraya gitmesini, sonra kuşatmasını sağlayan karmaşık bir yapı yerine
                // Bannerlord'da genelde "SetMoveBesiegeSettlement" kullanılır.
                var method = typeof(MobileParty).GetMethod("SetMoveBesiegeSettlement", BindingFlags.Instance | BindingFlags.Public);
                if (method != null) method.Invoke(actor, new object[] { target });
            }
            catch { }
        }
    }
}
