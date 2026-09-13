using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace RebellionsAndDemographics
{
    public class SiegeCompanionEventModel : DefaultSiegeEventModel
    {
        // -------------------------------------------------------
        // Helper: Check if the party has a companion with the required spec AND they are physically in the roster
        // -------------------------------------------------------
        private (bool hasSpec, int level) GetPartySpec(MobileParty party, SiegeSpecialization spec)
        {
            if (party == null || SiegeCompanionBehavior.Instance == null) return (false, 0);

            var data = SiegeCompanionBehavior.Instance.Data;
            foreach (var elem in party.MemberRoster.GetTroopRoster())
            {
                if (elem.Character.IsHero && elem.Character.HeroObject != null)
                {
                    var heroId = elem.Character.HeroObject.StringId;
                    if (data.CompanionSpecs.ContainsKey(heroId) && data.CompanionSpecs[heroId] == spec)
                    {
                        // Check if they are currently training
                        if (!data.CompanionTrainingEndTime.ContainsKey(heroId))
                        {
                            return (true, data.GetLevel(heroId));
                        }
                    }
                }
            }
            return (false, 0);
        }

        // -------------------------------------------------------
        // ATTACKER: Ram
        // -------------------------------------------------------
        public override IEnumerable<SiegeEngineType> GetAvailableAttackerRamSiegeEngines(PartyBase party)
        {
            if (party?.MobileParty == null || party.MobileParty != MobileParty.MainParty)
            {
                foreach (var e in base.GetAvailableAttackerRamSiegeEngines(party)) yield return e;
                yield break;
            }

            var (found, level) = GetPartySpec(party.MobileParty, SiegeSpecialization.Ram);
            if (found)
            {
                yield return DefaultSiegeEngineTypes.Ram;
                if (level >= 3) yield return DefaultSiegeEngineTypes.Ram; // 2nd slot at L3+
            }
        }

        // -------------------------------------------------------
        // ATTACKER: Tower
        // -------------------------------------------------------
        public override IEnumerable<SiegeEngineType> GetAvailableAttackerTowerSiegeEngines(PartyBase party)
        {
            if (party?.MobileParty == null || party.MobileParty != MobileParty.MainParty)
            {
                foreach (var e in base.GetAvailableAttackerTowerSiegeEngines(party)) yield return e;
                yield break;
            }

            var (found, level) = GetPartySpec(party.MobileParty, SiegeSpecialization.Tower);
            if (found)
            {
                yield return DefaultSiegeEngineTypes.SiegeTower;
                if (level >= 3) yield return DefaultSiegeEngineTypes.SiegeTower;
            }
        }

        // -------------------------------------------------------
        // ATTACKER: Ranged
        // -------------------------------------------------------
        public override IEnumerable<SiegeEngineType> GetAvailableAttackerRangedSiegeEngines(PartyBase party)
        {
            if (party?.MobileParty == null || party.MobileParty != MobileParty.MainParty)
            {
                foreach (var e in base.GetAvailableAttackerRangedSiegeEngines(party)) yield return e;
                yield break;
            }

            var (hasBallista, bLevel) = GetPartySpec(party.MobileParty, SiegeSpecialization.Ballista);
            if (hasBallista)
            {
                yield return DefaultSiegeEngineTypes.Ballista;
                if (bLevel >= 2) yield return DefaultSiegeEngineTypes.FireBallista;
            }

            var (hasOnager, oLevel) = GetPartySpec(party.MobileParty, SiegeSpecialization.Onager);
            if (hasOnager)
            {
                yield return DefaultSiegeEngineTypes.Onager;
                if (oLevel >= 2) yield return DefaultSiegeEngineTypes.FireOnager;
            }

            var (hasTrebuchet, tLevel) = GetPartySpec(party.MobileParty, SiegeSpecialization.Trebuchet);
            if (hasTrebuchet)
            {
                yield return DefaultSiegeEngineTypes.Trebuchet;
                if (tLevel >= 2) yield return DefaultSiegeEngineTypes.Bricole;
            }
        }

        // -------------------------------------------------------
        // DEFENDER: Level Calculation & Ranged
        // -------------------------------------------------------
        private int CalculateDefenderLevel(Settlement settlement)
        {
            if (settlement == null || settlement.Town == null) return 1;
            
            Town town = settlement.Town;
            float score = 0f;
            
            score += town.GetWallLevel() * 10f; // 10-30
            score += town.Prosperity / 200f; // 0-50
            
            int garrison = town.GarrisonParty != null ? town.GarrisonParty.MemberRoster.TotalManCount : 0;
            int militia = town.Settlement.MilitiaPartyComponent != null ? town.Settlement.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount : 0;
            score += (garrison + militia) / 20f; // 0-50
            
            if (town.Governor != null)
            {
                score += town.Governor.GetSkillValue(DefaultSkills.Engineering) / 5f; // 0-40
            }

            if (score < 40f) return 1;
            if (score < 70f) return 2;
            if (score < 100f) return 3;
            if (score < 130f) return 4;
            return 5;
        }

        public override IEnumerable<SiegeEngineType> GetAvailableDefenderSiegeEngines(PartyBase party)
        {
            if (party?.Settlement == null)
            {
                foreach (var e in base.GetAvailableDefenderSiegeEngines(party)) yield return e;
                yield break;
            }

            int defLevel = CalculateDefenderLevel(party.Settlement);

            yield return DefaultSiegeEngineTypes.Ballista;
            if (defLevel >= 2) yield return DefaultSiegeEngineTypes.FireBallista;
            if (defLevel >= 3) yield return DefaultSiegeEngineTypes.Catapult;
            if (defLevel >= 4) yield return DefaultSiegeEngineTypes.FireCatapult;
            if (defLevel >= 5) 
            {
                // Extra slots for L5
                yield return DefaultSiegeEngineTypes.FireBallista;
                yield return DefaultSiegeEngineTypes.FireCatapult;
            }
        }

        // -------------------------------------------------------
        // STAT SCALING (HP, Damage, Build Speed)
        // -------------------------------------------------------
        private SiegeSpecialization GetSpecForEngine(SiegeEngineType engine)
        {
            if (engine == DefaultSiegeEngineTypes.Ram) return SiegeSpecialization.Ram;
            if (engine == DefaultSiegeEngineTypes.SiegeTower) return SiegeSpecialization.Tower;
            if (engine == DefaultSiegeEngineTypes.Ballista || engine == DefaultSiegeEngineTypes.FireBallista) return SiegeSpecialization.Ballista;
            if (engine == DefaultSiegeEngineTypes.Onager || engine == DefaultSiegeEngineTypes.FireOnager) return SiegeSpecialization.Onager;
            if (engine == DefaultSiegeEngineTypes.Trebuchet || engine == DefaultSiegeEngineTypes.Bricole) return SiegeSpecialization.Trebuchet;
            return SiegeSpecialization.None;
        }

        private float GetLevelMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 0.8f;
                case 2: return 1.0f;
                case 3: return 1.25f;
                case 4: return 1.5f;
                case 5: return 2.0f;
                default: return 1.0f;
            }
        }

        protected MobileParty GetEffectiveSiegePartyForSideInternal(SiegeEvent siegeEvent, BattleSideEnum side)
        {
            if (side == BattleSideEnum.Attacker)
            {
                foreach (var p in siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType())
                    if (p.MobileParty != null) return p.MobileParty;
            }
            return null;
        }

        public override float GetSiegeEngineHitPoints(SiegeEvent siegeEvent, SiegeEngineType siegeEngine, BattleSideEnum battleSide)
        {
            float baseHp = base.GetSiegeEngineHitPoints(siegeEvent, siegeEngine, battleSide);

            if (battleSide == BattleSideEnum.Attacker && SiegeCompanionBehavior.Instance != null)
            {
                var spec = GetSpecForEngine(siegeEngine);
                if (spec != SiegeSpecialization.None)
                {
                    var party = GetEffectiveSiegePartyForSideInternal(siegeEvent, battleSide);
                    if (party != null && party == MobileParty.MainParty)
                    {
                        var (_, level) = GetPartySpec(party, spec);
                        if (level > 0) baseHp *= GetLevelMultiplier(level);
                    }
                }
            }

            if (battleSide == BattleSideEnum.Defender)
            {
                var settlement = siegeEvent.BesiegedSettlement;
                if (settlement != null)
                {
                    int defLevel = CalculateDefenderLevel(settlement);
                    baseHp *= GetLevelMultiplier(defLevel);
                }
            }

            return baseHp;
        }

        public override float GetSiegeEngineDamage(SiegeEvent siegeEvent, BattleSideEnum battleSide, SiegeEngineType siegeEngine, SiegeBombardTargets target)
        {
            float baseDmg = base.GetSiegeEngineDamage(siegeEvent, battleSide, siegeEngine, target);

            if (battleSide == BattleSideEnum.Attacker && SiegeCompanionBehavior.Instance != null)
            {
                var spec = GetSpecForEngine(siegeEngine);
                if (spec != SiegeSpecialization.None)
                {
                    var party = GetEffectiveSiegePartyForSideInternal(siegeEvent, battleSide);
                    if (party != null && party == MobileParty.MainParty)
                    {
                        var (_, level) = GetPartySpec(party, spec);
                        if (level > 0) baseDmg *= GetLevelMultiplier(level);
                    }
                }
            }

            if (battleSide == BattleSideEnum.Defender)
            {
                var settlement = siegeEvent.BesiegedSettlement;
                if (settlement != null)
                {
                    int defLevel = CalculateDefenderLevel(settlement);
                    baseDmg *= GetLevelMultiplier(defLevel);
                }
            }

            return baseDmg;
        }

        public override float GetConstructionProgressPerHour(SiegeEngineType type, SiegeEvent siegeEvent, ISiegeEventSide side)
        {
            float baseSpeed = base.GetConstructionProgressPerHour(type, siegeEvent, side);

            if (side.BattleSide == BattleSideEnum.Attacker && SiegeCompanionBehavior.Instance != null)
            {
                var spec = GetSpecForEngine(type);
                if (spec != SiegeSpecialization.None)
                {
                    var party = GetEffectiveSiegePartyForSideInternal(siegeEvent, side.BattleSide);
                    if (party != null && party == MobileParty.MainParty)
                    {
                        var (_, level) = GetPartySpec(party, spec);
                        if (level > 0) baseSpeed *= GetLevelMultiplier(level);
                    }
                }
            }

            if (side.BattleSide == BattleSideEnum.Defender)
            {
                var settlement = siegeEvent.BesiegedSettlement;
                if (settlement != null)
                {
                    int defLevel = CalculateDefenderLevel(settlement);
                    baseSpeed *= GetLevelMultiplier(defLevel);
                }
            }

            return baseSpeed;
        }
    }
}
