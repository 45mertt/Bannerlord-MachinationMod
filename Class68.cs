using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace RebellionsAndDemographics
{
    public class PendraicBattleBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTick(float dt)
        {
            if (Input.IsKeyDown(InputKey.LeftControl) && Input.IsKeyDown(InputKey.LeftShift) && Input.IsKeyPressed(InputKey.X))
            {
                try { StartPendraicWar(); }
                catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage("BATTLE ERROR:" + ex.Message, Colors.Red)); }
            }
        }

        private void StartPendraicWar()
        {
            if (Campaign.Current == null) return;

            Kingdom empire = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
            Kingdom battania = Kingdom.All.FirstOrDefault(k => k.StringId == "battania");
            Kingdom sturgia = Kingdom.All.FirstOrDefault(k => k.StringId == "sturgia");
            Kingdom vlandia = Kingdom.All.FirstOrDefault(k => k.StringId == "vlandia");
            Kingdom khuzait = Kingdom.All.FirstOrDefault(k => k.StringId == "khuzait");
            Kingdom aserai = Kingdom.All.FirstOrDefault(k => k.StringId == "aserai");

            if (empire == null) return;

            UnifyEmpire(empire);
            Hero neretzes = PrepareNeretzes(empire);

            MakePeace(empire, khuzait); MakePeace(empire, aserai); MakePeace(khuzait, aserai);
            MakePeace(vlandia, sturgia); MakePeace(vlandia, battania); MakePeace(sturgia, battania);

            List<Kingdom> redTeam = new List<Kingdom> { empire, khuzait, aserai };
            List<Kingdom> blueTeam = new List<Kingdom> { vlandia, sturgia, battania };

            foreach (var red in redTeam)
                foreach (var blue in blueTeam)
                    DeclareWar(red, blue);

            Settlement pendraic = Settlement.Find("castle_B4");
            if (pendraic != null)
            {
                Vec2 gatePos = new Vec2(pendraic.GatePosition.X, pendraic.GatePosition.Y);

                List<Kingdom> allParticipants = new List<Kingdom>();
                allParticipants.AddRange(redTeam);
                allParticipants.AddRange(blueTeam);

                int count = MassTeleport(allParticipants, gatePos, neretzes);

                if (neretzes.PartyBelongedTo != null) ForceTeleportParty(neretzes.PartyBelongedTo, gatePos);

                Vec2 playerPos = new Vec2(gatePos.X + 5f, gatePos.Y + 5f);
                ForceTeleportParty(MobileParty.MainParty, playerPos);

                InformationManager.DisplayMessage(new InformationMessage("BATTLE OF PENDRAIC:" + count + "The army has deployed!", Colors.Green));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: Pendraic Castle (castle_B4) not found!", Colors.Red));
            }
        }

        private void ForceTeleportParty(MobileParty party, Vec2 targetPos)
        {
            if (party == null) return;

            if (party.Army != null)
            {
                if (party.Army.LeaderParty == party) DisbandArmyAction.ApplyByUnknownReason(party.Army);
                else party.Army = null;
            }

            if (party.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(party);

            // FIX: Position2D yerine Position kullanıp CampaignVec2 atıyoruz.
            party.Position = new CampaignVec2(targetPos, true);

            party.SetMoveModeHold();
            party.Party.SetVisualAsDirty();
        }

        private int MassTeleport(List<Kingdom> kingdoms, Vec2 targetCenter, Hero excludeHero)
        {
            int count = 0;
            Random rand = new Random();
            foreach (var kingdom in kingdoms)
            {
                foreach (var clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan.IsBanditFaction) continue;
                    foreach (var warParty in clan.WarPartyComponents)
                    {
                        MobileParty party = warParty.MobileParty;
                        if (party != null && party.LeaderHero != null && party.LeaderHero != excludeHero && party.IsActive)
                        {
                            float rx = (float)(rand.NextDouble() * 6.0 - 3.0);
                            float ry = (float)(rand.NextDouble() * 6.0 - 3.0);
                            ForceTeleportParty(party, new Vec2(targetCenter.X + rx, targetCenter.Y + ry));
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private void UnifyEmpire(Kingdom targetEmpire)
        {
            TextObject newName = new TextObject("{=!}Calradic Empire");
            targetEmpire.ChangeKingdomName(newName, newName, new TaleWorlds.Localization.TextObject("{=!}"));
            foreach (Clan clan in Clan.All.ToList())
            {
                if (clan.Culture.StringId == "empire" && clan.MapFaction != targetEmpire && !clan.IsEliminated && !clan.IsBanditFaction && clan.Kingdom != null)
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, targetEmpire, showNotification: false);
            }
        }

        private Hero PrepareNeretzes(Kingdom empire)
        {
            Clan rulingClan = empire.RulingClan;
            if (!rulingClan.Name.ToString().Contains("Neretzes"))
                rulingClan.ChangeClanName(new TextObject("{=!}Neretzes"), new TextObject("{=!}Neretzes"));

            Hero emperor = rulingClan.Leader;
            if (!emperor.Name.ToString().Contains("Neretzes"))
            {
                TextObject name = new TextObject("{=!}Drosios Neretzes");
                emperor.SetName(name, name);
                try
                {
                    FieldInfo bodyField = typeof(Hero).GetField("_bodyProperties", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (bodyField != null)
                        bodyField.SetValue(emperor, new BodyProperties(new DynamicBodyProperties(60f, 0.5f, 0.5f), emperor.BodyProperties.StaticProperties));
                }
                catch { }
            }

            if (emperor.PartyBelongedTo == null)
            {
                // FIX: CampaignVec2 oluşturma hatası giderildi.
                CampaignVec2 spawnPos;
                if (MobileParty.MainParty != null) spawnPos = MobileParty.MainParty.Position;
                else spawnPos = new CampaignVec2(new Vec2(500f, 500f), true);

                MobileParty party = LordPartyComponent.CreateLordParty("neretzes_party", emperor, spawnPos, 1f, null, emperor);
                if (party != null) party.MemberRoster.AddToCounts(emperor.Culture.EliteBasicTroop, 150);
            }

            emperor.ChangeState(Hero.CharacterStates.Active);
            return emperor;
        }

        private void DeclareWar(Kingdom k1, Kingdom k2) { if (k1 != null && k2 != null && !k1.IsAtWarWith(k2)) DeclareWarAction.ApplyByDefault(k1, k2); }
        private void MakePeace(Kingdom k1, Kingdom k2) { if (k1 != null && k2 != null && k1.IsAtWarWith(k2)) MakePeaceAction.Apply(k1, k2); }
    }
}
