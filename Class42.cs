using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization; // Eklendi
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public enum WarOrderType
    {
        None, SiegeSpecific, Patrol, FormArmy, JoinArmy
    }

    public class WarOrder
    {
        [SaveableField(1)] public WarOrderType OrderType;
        [SaveableField(2)] public Settlement TargetSettlement;
        [SaveableField(3)] public IFaction TargetFaction;
        [SaveableField(4)] public MobileParty TargetParty;
        [SaveableField(5)] public bool IsCompleted;
    }

    public static class WarStrategyManager
    {
        public static Dictionary<Hero, WarOrder> _activeOrders = new Dictionary<Hero, WarOrder>();
        public static Dictionary<Hero, WarOrder> ActiveOrders { get { return _activeOrders ?? (_activeOrders = new Dictionary<Hero, WarOrder>()); } set { _activeOrders = value; } }

        public static bool HeroHasOrder(Hero hero)
        {
            return ActiveOrders.ContainsKey(hero) && !ActiveOrders[hero].IsCompleted;
        }

        public static void Order_Siege(Hero hero, Settlement target)
        {
            if (hero == null || hero.PartyBelongedTo == null) return;

            if (hero.PartyBelongedTo.Army != null) DisbandOrLeaveArmy(hero);

            SetPartyAiAction.GetActionForBesiegingSettlement(hero.PartyBelongedTo, target, MobileParty.NavigationType.None, false);
            AssignOrder(hero, WarOrderType.SiegeSpecific, target, target.MapFaction);

            // {=ws_msg_siege}{HERO} is going to besiege {TARGET}.
            TextObject msg = new TextObject("{=ws_msg_siege}{HERO} is going to besiege {TARGET}.");
            msg.SetTextVariable("HERO", hero.Name);
            msg.SetTextVariable("TARGET", target.Name);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
        }

        public static void OnHourlyTick()
        {
            List<Hero> completedOrders = new List<Hero>();

            foreach (var kvp in ActiveOrders)
            {
                Hero leader = kvp.Key;
                WarOrder order = kvp.Value;

                if (leader == null || !leader.IsAlive || leader.PartyBelongedTo == null || leader.PartyBelongedTo.Army == null)
                {
                    completedOrders.Add(leader);
                    continue;
                }

                if (order.OrderType == WarOrderType.FormArmy && order.TargetSettlement != null)
                {
                    ManageSmartSiegeLogic(leader.PartyBelongedTo.Army, order.TargetSettlement);
                }
            }

            foreach (var h in completedOrders) ActiveOrders.Remove(h);
        }

        private static void ManageSmartSiegeLogic(Army army, Settlement target)
        {
            if (army == null || target == null) return;
            if (army.LeaderParty == null) return;

            if (army.LeaderParty.SiegeEvent != null) return;

            float armyPower = army.Parties.Sum(p => p.MemberRoster.TotalManCount);

            float garrisonStrength = 0;
            if (target.Town != null && target.Town.GarrisonParty != null)
            {
                garrisonStrength = target.Town.GarrisonParty.MemberRoster.TotalManCount;
            }
            float targetPower = garrisonStrength + target.Militia;

            float requiredPower = targetPower * 1.3f;

            Vec2 armyPos = army.LeaderParty.GetPosition2D;

            if (armyPower < requiredPower)
            {
                Settlement recruitmentPoint = Settlement.All
                    .Where(s => s.MapFaction == army.Kingdom && (s.IsVillage || s.IsTown) && !s.IsUnderSiege && !s.IsRaided)
                    .OrderBy(s => armyPos.DistanceSquared(new Vec2(s.GatePosition.X, s.GatePosition.Y)))
                    .FirstOrDefault();

                if (recruitmentPoint != null && army.LeaderParty.CurrentSettlement != recruitmentPoint)
                {
                    if (army.LeaderParty.DefaultBehavior != AiBehavior.GoToSettlement || army.AiBehaviorObject != recruitmentPoint)
                    {
                        SetPartyAiAction.GetActionForVisitingSettlement(army.LeaderParty, recruitmentPoint, MobileParty.NavigationType.None, false, false);
                    }
                }
            }
            else
            {
                if (army.LeaderParty.DefaultBehavior != AiBehavior.BesiegeSettlement)
                {
                    SetPartyAiAction.GetActionForBesiegingSettlement(army.LeaderParty, target, MobileParty.NavigationType.None, false);
                    // {=fc_msg_siege_army}{HERO}'s army is marching to besiege {TARGET}! (Reusing similar message)
                    TextObject msg = new TextObject("{=fc_msg_siege_army}{HERO}'s army is marching to besiege {TARGET}!");
                    msg.SetTextVariable("HERO", army.LeaderParty.LeaderHero.Name);
                    msg.SetTextVariable("TARGET", target.Name);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                }
            }
        }

        public static void DisbandOrLeaveArmy(Hero hero)
        {
            if (hero == null || hero.PartyBelongedTo == null || hero.PartyBelongedTo.Army == null) return;

            if (hero.PartyBelongedTo.Army.LeaderParty == hero.PartyBelongedTo)
            {
                DisbandArmyAction.ApplyByUnknownReason(hero.PartyBelongedTo.Army);
                // {=ws_msg_disband}{HERO} disbanded their army.
                TextObject msg = new TextObject("{=ws_msg_disband}{HERO} disbanded their army.");
                msg.SetTextVariable("HERO", hero.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
            }
            else
            {
                hero.PartyBelongedTo.Army = null;
                hero.PartyBelongedTo.SetMoveModeHold();
                // {=ws_msg_leave}{HERO} left their current army.
                TextObject msg = new TextObject("{=ws_msg_leave}{HERO} left their current army.");
                msg.SetTextVariable("HERO", hero.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
            }
        }

        public static void Order_FormArmy(Hero leader, Settlement target)
        {
            if (leader == null || leader.PartyBelongedTo == null || leader.Clan?.Kingdom == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_221}You must be part of a kingdom to form an army.").ToString(), Colors.Red));
                return;
            }

            if (leader.PartyBelongedTo.Army != null) DisbandOrLeaveArmy(leader);

            ChangeClanInfluenceAction.Apply(leader.Clan, 100);
            GiveGoldAction.ApplyBetweenCharacters(null, leader, 5000, true);

            leader.PartyBelongedTo.Army = new Army(leader.Clan.Kingdom, leader.PartyBelongedTo, Army.ArmyTypes.Besieger);

            Settlement gatheringPoint = FindSafeGatheringPoint(target, leader.MapFaction);

            if (gatheringPoint != null)
            {
                GatherArmyAction.Apply(leader.PartyBelongedTo, gatheringPoint);
                leader.PartyBelongedTo.Army.AiBehaviorObject = target;

                // {=fc_msg_siege_army} logic used as notification
                TextObject msg = new TextObject("{=fc_msg_siege_army}{HERO}'s army is marching to besiege {TARGET}!");
                msg.SetTextVariable("HERO", leader.Name);
                msg.SetTextVariable("TARGET", target.Name);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
            }
            else
            {
                GatherArmyAction.Apply(leader.PartyBelongedTo, target);
            }

            AssignOrder(leader, WarOrderType.FormArmy, target, target.MapFaction);
        }

        private static Settlement FindSafeGatheringPoint(Settlement enemyTarget, IFaction friendlyFaction)
        {
            Vec2 targetPos = new Vec2(enemyTarget.GatePosition.X, enemyTarget.GatePosition.Y);

            return Settlement.All
                .Where(s => s.MapFaction == friendlyFaction && s.IsFortification && !s.IsUnderSiege)
                .OrderBy(s => new Vec2(s.GatePosition.X, s.GatePosition.Y).DistanceSquared(targetPos))
                .FirstOrDefault();
        }

        public static void Order_JoinArmy(Hero follower, MobileParty armyLeaderParty)
        {
            if (follower == null || follower.PartyBelongedTo == null || armyLeaderParty == null) return;
            if (follower.PartyBelongedTo.Army != null && follower.PartyBelongedTo.Army == armyLeaderParty.Army) return;

            if (follower.PartyBelongedTo.Army != null) DisbandOrLeaveArmy(follower);

            SetPartyAiAction.GetActionForEscortingParty(follower.PartyBelongedTo, armyLeaderParty, MobileParty.NavigationType.None, false, false);

            var order = new WarOrder { OrderType = WarOrderType.JoinArmy, TargetParty = armyLeaderParty, IsCompleted = false };
            if (ActiveOrders.ContainsKey(follower)) ActiveOrders[follower] = order;
            else ActiveOrders.Add(follower, order);

            // Reusing patrol/move message logic for feedback
            InformationManager.DisplayMessage(new InformationMessage($"{follower.Name} moving to join army.", Colors.Green));
        }

        public static void Order_Patrol(Hero hero, Settlement region)
        {
            if (hero.PartyBelongedTo.Army != null) DisbandOrLeaveArmy(hero);

            SetPartyAiAction.GetActionForPatrollingAroundSettlement(hero.PartyBelongedTo, region, MobileParty.NavigationType.None, false, false);
            AssignOrder(hero, WarOrderType.Patrol, region, null);

            // {=ws_msg_patrol}{HERO} will patrol around {REGION}.
            TextObject msg = new TextObject("{=ws_msg_patrol}{HERO} will patrol around {REGION}.");
            msg.SetTextVariable("HERO", hero.Name);
            msg.SetTextVariable("REGION", region.Name);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
        }

        private static void AssignOrder(Hero lord, WarOrderType type, Settlement settlement = null, IFaction targetFaction = null)
        {
            if (lord == null) return;
            var order = new WarOrder { OrderType = type, TargetSettlement = settlement, TargetFaction = targetFaction, IsCompleted = false };
            if (ActiveOrders.ContainsKey(lord)) ActiveOrders[lord] = order;
            else ActiveOrders.Add(lord, order);
        }
    }

    /* public class WarStrategySaveDefiner : SaveableTypeDefiner
    {
        public WarStrategySaveDefiner() : base(999_888_777) { }
        protected override void DefineClassTypes() { AddClassDefinition(typeof(WarOrder), 1); }
        protected override void DefineEnumTypes() { AddEnumDefinition(typeof(WarOrderType), 2); }
        protected override void DefineContainerDefinitions() { ConstructContainerDefinition(typeof(Dictionary<Hero, WarOrder>)); }
        
    } */
}

