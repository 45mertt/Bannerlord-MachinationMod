using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;

namespace ClassLibrary22
{
    public enum CastleDoctrine
    {
        Traditional = 0,
        IronDiscipline = 1,
        TradeHub = 2,
        FrontierOutpost = 3,
        AgrarianFocus = 4,
        CulturalHub = 5,
        WarCamp = 6,
        FortifiedTown = 7,
        SpyNetwork = 8,
        FeudalHeadquarters = 9
    }
    public enum TreasuryStrategy
    {
        Balanced = 0,
        WarPrep = 1,
        Happiness = 2
    }

    public class CastleData
    {
        [SaveableProperty(1)]
        public CampaignTime LastInspectionTime { get; set; } = CampaignTime.Now;
        [SaveableProperty(2)]
        public CastleDoctrine ActiveDoctrine { get; set; } = CastleDoctrine.Traditional;
        [SaveableProperty(3)]
        public int StabilityScore { get; set; } = 50;
        [SaveableProperty(4)]
        public bool HasPendingCrisis { get; set; } = false;
        [SaveableProperty(5)]
        public CampaignTime NextCrisisCheckTime { get; set; } = CampaignTime.Now;
        [SaveableProperty(6)]
        public int Treasury { get; set; } = 0;
        [SaveableProperty(7)]
        public bool AreSoldiersWorking { get; set; } = false;
        [SaveableProperty(8)]
        public bool HasSabotageCrisis { get; set; } = false;
        [SaveableProperty(9)]
        public TreasuryStrategy ActiveTreasuryStrategy { get; set; } = TreasuryStrategy.Balanced;
        [SaveableProperty(10)]
        public int ActiveCrisisId { get; set; } = 0;
    }

    public class CastleDynamicsBehavior : CampaignBehaviorBase
    {
        private Dictionary<Settlement, CastleData> _castleData = new Dictionary<Settlement, CastleData>();

        private CastleCrisisScenario _activeCrisisScenario = null;
        private int _currentCrisisStageIdx = 0;

        [SaveableProperty(1)]
        public Dictionary<Settlement, CastleData> CastleDataDict
        {
            get { return _castleData ?? (_castleData = new Dictionary<Settlement, CastleData>()); }
            set { _castleData = value; }
        }

        private CampaignTime _lastUpdate;

        // Dinamik Problemler İçin Değişkenler
        private HashSet<Agent> _solvedAgents = new HashSet<Agent>();
        private CastleProblem _activeProblem;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in CastleDataDict.ToList()) { if (!(pair.Key != null && pair.Value != null)) CastleDataDict.Remove(pair.Key); }
            }
            dataStore.SyncData("_castleData", ref _castleData);
            if (_castleData == null)
            {
                _castleData = new Dictionary<Settlement, CastleData>();
            }
        }

        public CastleData GetCastleData(Settlement settlement)
        {
            if (settlement == null || (!settlement.IsCastle && !settlement.IsTown)) return null;
            if (settlement.OwnerClan != Clan.PlayerClan) return null;

            if (!CastleDataDict.ContainsKey(settlement))
            {
                CastleDataDict.Add(settlement, new CastleData()
                {
                    NextCrisisCheckTime = CampaignTime.Now + CampaignTime.Days(21)
                });
            }
            // Sabotaj çözüldü vs...
            return CastleDataDict[settlement];
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (CastleDataDict.ContainsKey(settlement) && oldOwner == Hero.MainHero && newOwner != Hero.MainHero)
            {
                CastleDataDict[settlement].Treasury = 0; // Kaleyi kaybedersen hazineyi de kaybedersin!
            }
        }

        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            // Yeni oyun oturumu başladığında agent listesini sıfırla ki eski save'den kalanlar sorun yapmasın
            _solvedAgents.Clear();
            CastleCrisisDialogs.InitializeScenarios(ResolveCrisis);
            AddMenus(campaignGameStarter);
            AddDialogs(campaignGameStarter);
        }

        private void DailyTickSettlement(Settlement settlement)
        {
            var data = GetCastleData(settlement);
            if (data == null) return;

            // Denetim kontrolü
            if (data.LastInspectionTime.ElapsedDaysUntilNow > 5f)
            {
                data.StabilityScore = Math.Max(0, data.StabilityScore - 1);
            }

            // Kriz (Muhafız Sorunu) Kontrolü
            if (!data.HasPendingCrisis && data.NextCrisisCheckTime.IsPast)
            {
                data.HasPendingCrisis = true;
                data.NextCrisisCheckTime = CampaignTime.Now + CampaignTime.Days(42);

                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=CD_07}Local Crisis").ToString(),
                    new TextObject("{=CD_08} A local matter has arisen. You must personally go to the courtyard and speak with the guards to resolve the issue, otherwise stability will continue to drop.").SetTextVariable("SETTLEMENT", settlement.Name).ToString(),
                    true, false, new TextObject("{=CD_09}Understood").ToString(), "", null, null), false);
            }

            if (data.HasPendingCrisis)
            {
                data.StabilityScore = Math.Max(0, data.StabilityScore - 2);
            }

            // Asker Çalıştırma Etkisi
            if (data.AreSoldiersWorking)
            {
                int garrisonSize = settlement.Town?.GarrisonParty?.MemberRoster?.TotalHealthyCount ?? 50;
                int income = garrisonSize * 2;
                data.Treasury += income;
                if (settlement.Town != null)
                {
                    settlement.Town.Loyalty = Math.Max(0, settlement.Town.Loyalty - 1f);
                }
            }

            // Hazine Stratejisi Etkileri
            if (data.ActiveTreasuryStrategy == TreasuryStrategy.WarPrep)
            {
                if (data.Treasury >= 500)
                {
                    data.Treasury -= 500;
                    if (settlement.Town?.GarrisonParty != null)
                    {
                        // 1. Give XP to existing troops
                        settlement.Town.GarrisonParty.MemberRoster.AddXpToTroop(settlement.Town.GarrisonParty.MemberRoster.GetCharacterAtIndex(MBRandom.RandomInt(settlement.Town.GarrisonParty.MemberRoster.Count)), 500);
                        // 2. Add some low tier troops if under limit
                        if (settlement.Town.GarrisonParty.MemberRoster.TotalHealthyCount < settlement.Town.GarrisonParty.Party.PartySizeLimit)
                        {
                            var culture = settlement.Culture;
                            var basicTroop = culture.BasicTroop;
                            if (basicTroop != null)
                            {
                                settlement.Town.GarrisonParty.MemberRoster.AddToCounts(basicTroop, 2);
                            }
                        }
                    }
                }
                else
                {
                    data.ActiveTreasuryStrategy = TreasuryStrategy.Balanced;
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=CD_WARN_FUNDS}Treasury depleted! Preparation for War strategy cancelled, reverted to Balanced strategy.").ToString(), Colors.Red));
                }
            }
            else if (data.ActiveTreasuryStrategy == TreasuryStrategy.Happiness)
            {
                int cost = 500;
                if (settlement.Town != null)
                {
                    cost = (int)(1000 - (settlement.Town.Loyalty * 8) - (settlement.Town.Prosperity / 20));
                    cost = Math.Max(100, cost);
                    cost += MBRandom.RandomInt(-50, 50);
                    cost = Math.Max(50, cost);
                }

                if (data.Treasury >= cost)
                {
                    data.Treasury -= cost;
                    if (settlement.Town != null)
                    {
                        settlement.Town.Loyalty += 1f;
                        settlement.Town.Prosperity += 10f;
                    }
                }
                else
                {
                    data.ActiveTreasuryStrategy = TreasuryStrategy.Balanced;
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=CD_WARN_FUNDS2}Treasury depleted! Happiness Focus strategy cancelled, reverted to Balanced strategy.").ToString(), Colors.Red));
                }
            }

            // Sabotaj Krizi İhtimali
            /*
            if (!data.HasSabotageCrisis && MBRandom.RandomFloat < 0.015f) // %3 ihtimalle her gün sabotaj çıkabilir
            {
                data.HasSabotageCrisis = true;
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=CD_32}Sabotage Threat!").ToString(),
                    new TextObject("{=CD_33}Suspicious activities have been reported in {SETTLEMENT}. Spies or traitors might be trying to cripple our defenses. You must travel there, find the traitor in the courtyard, and execute them!").SetTextVariable("SETTLEMENT", settlement.Name).ToString(),
                    true, false, new TextObject("{=CD_09}Understood").ToString(), "", null, null), false);
            }
            */

            if (data.HasSabotageCrisis)
            {
                if (settlement.Town != null)
                {
                    settlement.Town.Security = Math.Max(0, settlement.Town.Security - 2f);
                }
            }
        }

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("castle", "castle_headquarters", "{=CD_01}Go to Headquarters",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("castle_headquarters_menu");
                },
                false, 1);

            starter.AddGameMenuOption("town_keep", "town_headquarters", "{=CD_01}Go to Headquarters",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("castle_headquarters_menu");
                },
                false, 1);

            starter.AddGameMenu("castle_headquarters_menu", "{=CD_02}You are at the Headquarters. From here, you can set the military and civilian doctrines for this settlement, and issue orders to the administrators.\n\n{HEADQUARTERS_DESC}",
                (MenuCallbackArgs args) =>
                {
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null)
                    {
                        string desc = new TextObject("{=CD_03}Current Doctrine: {DOCTRINE}\nStability Score: {SCORE}/100\nLast Inspected: {DAYS} days ago.\nTreasury: {TREASURY} Denars\nSoldier Labor: {LABOR}")
                            .SetTextVariable("DOCTRINE", GetDoctrineName(data.ActiveDoctrine))
                            .SetTextVariable("SCORE", data.StabilityScore)
                            .SetTextVariable("DAYS", (int)data.LastInspectionTime.ElapsedDaysUntilNow)
                            .SetTextVariable("TREASURY", data.Treasury)
                            .SetTextVariable("LABOR", data.AreSoldiersWorking ? new TextObject("{=CD_25}Active (Earning Denars, Losing Loyalty)") : new TextObject("{=CD_26}Inactive (Resting)"))
                            .ToString();

                        if (data.HasPendingCrisis)
                            desc += "\n\n" + new TextObject("{=CD_04}ATTENTION: There is an unresolved crisis! Go inspect the courtyard to speak with the guards.").ToString();

                        MBTextManager.SetTextVariable("HEADQUARTERS_DESC", desc);
                    }
                });

            starter.AddGameMenuOption("castle_headquarters_menu", "castle_change_doctrine", "{=CD_05}Set Doctrine",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    ShowDoctrineSelectionInquiry();
                });

            starter.AddGameMenuOption("castle_headquarters_menu", "castle_headquarters_open_treasury", "{=CD_TREAS_OPEN}Treasury Management",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("castle_headquarters_treasury_menu");
                });

            starter.AddGameMenuOption("castle_headquarters_menu", "castle_headquarters_leave", "{=CD_06}Go Back",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.ExitToLast();
                }, true, -1, false, null);

            starter.AddGameMenu("castle_headquarters_treasury_menu", "{=CD_TREAS_DESC}The treasury is a fund allocated to prevent sudden crises and shape the economic prosperity of the settlement.\n\nCurrent Strategy: {STRATEGY}\nTreasury: {TREASURY} Denars\nSoldier Labor: {LABOR}",
                (MenuCallbackArgs args) =>
                {
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null)
                    {
                        string strategyText = data.ActiveTreasuryStrategy == TreasuryStrategy.Balanced ? new TextObject("{=CD_STRAT_BAL}Balanced").ToString() :
                                              data.ActiveTreasuryStrategy == TreasuryStrategy.WarPrep ? new TextObject("{=CD_STRAT_WAR}Preparation for War").ToString() :
                                              new TextObject("{=CD_STRAT_HAP}Happiness Focus").ToString();

                        MBTextManager.SetTextVariable("STRATEGY", strategyText);
                        MBTextManager.SetTextVariable("TREASURY", data.Treasury);
                        MBTextManager.SetTextVariable("LABOR", data.AreSoldiersWorking ? new TextObject("{=CD_25}Active").ToString() : new TextObject("{=CD_26}Inactive (Resting)").ToString());
                    }
                });

            starter.AddGameMenuOption("castle_headquarters_treasury_menu", "castle_headquarters_treasury_strategy", "{=CD_TREAS_STRAT}Change Strategy",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    ShowStrategySelectionInquiry();
                });

            starter.AddGameMenuOption("castle_headquarters_treasury_menu", "castle_headquarters_treasury_toggle_labor", "{=CD_29}Toggle Soldier Labor",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null)
                    {
                        data.AreSoldiersWorking = !data.AreSoldiersWorking;
                        InformationManager.DisplayMessage(new InformationMessage(
                            data.AreSoldiersWorking ? new TextObject("{=CD_30}Soldiers have been ordered to work and generate income!").ToString() : new TextObject("{=CD_31}Soldiers have returned to their normal duties.").ToString(), Colors.Green));
                    }
                    GameMenu.SwitchToMenu("castle_headquarters_treasury_menu");
                });

            starter.AddGameMenuOption("castle_headquarters_treasury_menu", "castle_headquarters_treasury_invest", "{=CD_27}Invest 10,000 Denars",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return Hero.MainHero.Gold >= 10000;
                },
                (MenuCallbackArgs args) =>
                {
                    Hero.MainHero.ChangeHeroGold(-10000);
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null) data.Treasury += 10000;
                    GameMenu.SwitchToMenu("castle_headquarters_treasury_menu");
                });

            starter.AddGameMenuOption("castle_headquarters_treasury_menu", "castle_headquarters_treasury_withdraw", "{=CD_28}Withdraw 10,000 Denars",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    return data != null && data.Treasury >= 10000;
                },
                (MenuCallbackArgs args) =>
                {
                    Hero.MainHero.ChangeHeroGold(10000);
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null) data.Treasury -= 10000;
                    GameMenu.SwitchToMenu("castle_headquarters_treasury_menu");
                });

            starter.AddGameMenuOption("castle_headquarters_treasury_menu", "castle_headquarters_treasury_back", "{=CD_06}Go Back",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("castle_headquarters_menu");
                }, true, -1, false, null);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Vanilla muhafız diyalogunu (100 priority falandır) ezmesi için 500 veriyoruz.
            starter.AddDialogLine("castle_officer_start", "start", "castle_officer_options",
                "{=CD_16}Welcome, commander. We await your orders.",
                () => IsCastleOfficerAgent() && !HasPendingCrisis(), null, 500);

            starter.AddDialogLine("castle_officer_crisis_dyn", "start", "castle_officer_crisis_dyn_options",
                "{CD_CRISIS_NPC_TEXT}",
                () => {
                    if (IsCastleOfficerAgent() && HasPendingCrisis())
                    {
                        if (_activeCrisisScenario == null)
                        {
                            _activeCrisisScenario = CastleCrisisDialogs.Scenarios[MBRandom.RandomInt(CastleCrisisDialogs.Scenarios.Count)];
                            _currentCrisisStageIdx = 0;
                        }
                        MBTextManager.SetTextVariable("CD_CRISIS_NPC_TEXT", _activeCrisisScenario.Stages[_currentCrisisStageIdx].NpcText);
                        return true;
                    }
                    return false;
                }, null, 510);

            starter.AddDialogLine("castle_officer_crisis_dyn_loop", "castle_officer_crisis_dyn_res", "castle_officer_crisis_dyn_options",
                "{CD_CRISIS_NPC_TEXT}",
                () => {
                    if (_activeCrisisScenario == null || _currentCrisisStageIdx >= _activeCrisisScenario.Stages.Count) return false;
                    MBTextManager.SetTextVariable("CD_CRISIS_NPC_TEXT", _activeCrisisScenario.Stages[_currentCrisisStageIdx].NpcText);
                    return true;
                }, null);

            starter.AddDialogLine("castle_officer_crisis_dyn_end", "castle_officer_crisis_dyn_res", "close_window",
                "{CD_CRISIS_END_TEXT}",
                () => {
                    if (_activeCrisisScenario != null && _currentCrisisStageIdx >= _activeCrisisScenario.Stages.Count)
                    {
                        MBTextManager.SetTextVariable("CD_CRISIS_END_TEXT", "{=CD_CRISIS_FIN}As you command, my Lord. It will be done. (Crisis Resolved)");
                        _activeCrisisScenario = null;

                        var data = GetCastleData(Settlement.CurrentSettlement);
                        if (data != null) data.HasPendingCrisis = false;

                        return true;
                    }
                    return false;
                }, null);

            starter.AddPlayerLine("castle_officer_crisis_dyn_opt1", "castle_officer_crisis_dyn_options", "castle_officer_crisis_dyn_res",
                "{CD_CRISIS_OPT1_TEXT}",
                () => {
                    if (_activeCrisisScenario == null || _currentCrisisStageIdx >= _activeCrisisScenario.Stages.Count) return false;
                    var stage = _activeCrisisScenario.Stages[_currentCrisisStageIdx];
                    if (string.IsNullOrEmpty(stage.Opt1Text)) return false;
                    MBTextManager.SetTextVariable("CD_CRISIS_OPT1_TEXT", stage.Opt1Text);
                    return true;
                },
                () => {
                    if (_activeCrisisScenario == null) return;
                    var stage = _activeCrisisScenario.Stages[_currentCrisisStageIdx];
                    InformationManager.DisplayMessage(new InformationMessage(stage.Opt1ResultText, Colors.White));
                    stage.Opt1Action?.Invoke();
                    _currentCrisisStageIdx++;
                });

            starter.AddPlayerLine("castle_officer_crisis_dyn_opt2", "castle_officer_crisis_dyn_options", "castle_officer_crisis_dyn_res",
                "{CD_CRISIS_OPT2_TEXT}",
                () => {
                    if (_activeCrisisScenario == null || _currentCrisisStageIdx >= _activeCrisisScenario.Stages.Count) return false;
                    var stage = _activeCrisisScenario.Stages[_currentCrisisStageIdx];
                    if (string.IsNullOrEmpty(stage.Opt2Text)) return false;
                    MBTextManager.SetTextVariable("CD_CRISIS_OPT2_TEXT", stage.Opt2Text);
                    return true;
                },
                () => {
                    if (_activeCrisisScenario == null) return;
                    var stage = _activeCrisisScenario.Stages[_currentCrisisStageIdx];
                    InformationManager.DisplayMessage(new InformationMessage(stage.Opt2ResultText, Colors.White));
                    stage.Opt2Action?.Invoke();
                    _currentCrisisStageIdx++;
                });

            string[] inspectAnswers = new string[] {
                "{=CD_INSP_1}Everything is in order, commander. Our troops are ready for battle!",
                "{=CD_INSP_2}Discipline in the garrison is at its peak, sir. Your presence has boosted our morale even further.",
                "{=CD_INSP_3}Guard shifts have been completed flawlessly, there are no weak points on the walls, commander.",
                "{=CD_INSP_4}Our supply situation is perfectly fine and the soldiers' morale is high. We await your command.",
                "{=CD_INSP_5}A few soldiers showed indiscipline, but we handled it immediately. Everything is under control now, my lord.",
                "{=CD_INSP_6}Weapon maintenance is complete, our armory is full. You can inspect us anytime, commander.",
                "{=CD_INSP_7}The soldiers are very excited that you came, my lord. The condition of the troops is excellent.",
                "{=CD_INSP_8}Siege equipment has been overhauled, patrols on the walls have been increased. You can rest easy.",
                "{=CD_INSP_9}Some new recruits are undergoing training, other than that, all our veteran soldiers are on duty on the walls.",
                "{=CD_INSP_10}Everything is in its right place, commander. In accordance with your orders, we are always ready to defend the castle!"
            };

            starter.AddPlayerLine("castle_officer_inspect", "castle_officer_options", "castle_officer_inspect_ans",
                "{=CD_21}I came to inspect the walls and the garrison. Give me a status report.",
                null,
                () =>
                {
                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null)
                    {
                        data.LastInspectionTime = CampaignTime.Now;
                        data.StabilityScore = Math.Min(100, data.StabilityScore + 5);
                        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=CD_22}Garrison and Headquarters inspected. Troops' morale increased.").ToString(), Colors.Green));
                    }

                    string randomAns = inspectAnswers[TaleWorlds.Core.MBRandom.RandomInt(inspectAnswers.Length)];
                    MBTextManager.SetTextVariable("CD_INSPECT_ANS_TEXT", new TextObject(randomAns));
                });

            starter.AddDialogLine("castle_officer_inspect_ans", "castle_officer_inspect_ans", "close_window",
                "{CD_INSPECT_ANS_TEXT}",
                null, null);

            starter.AddDialogLine("castle_officer_start_dynamic", "start", "castle_officer_options",
                "{=CD_23}At your command, my lord.",
                () => IsCastleOfficerAgent() && !CastleDynamicsMissionBehavior.IsBattleActive, null, 500);

            // Eski (Sabotaj krizi ile ilgili) satırları kapatmıştık, kalsın veya gerekirse yenisini ekleriz.

            // YENİ: Dinamik Problem Sorma
            starter.AddPlayerLine("cd_ask_dynamic_problem", "castle_officer_options", "cd_dynamic_problem_reply",
                "{=CD_ASK_PROBLEM}How are things in the settlement? Any pressing matters?",
                () => IsCastleOfficerAgent() && !_solvedAgents.Contains((Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent),
                () =>
                {
                    _solvedAgents.Add((Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent);
                    _activeProblem = CastleProblemManager.GetRandomValidProblem(Settlement.CurrentSettlement, GetCastleData(Settlement.CurrentSettlement));
                    if (_activeProblem != null)
                    {
                        MBTextManager.SetTextVariable("CD_DYN_DESC", new TextObject(_activeProblem.DescriptionTextId));
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("CD_DYN_DESC", new TextObject("{=CD_NO_PRB}Everything is peaceful today, commander."));
                    }
                });

            starter.AddDialogLine("cd_dynamic_problem_reply", "cd_dynamic_problem_reply", "cd_dynamic_problem_choices",
                "{CD_DYN_DESC}",
                null, null);

            // Çözüm 1
            starter.AddPlayerLine("cd_dynamic_s1", "cd_dynamic_problem_choices", "cd_dynamic_result",
                "{CD_DYN_S1}",
                () => {
                    if (_activeProblem == null || _activeProblem.Solutions.Count < 1) return false;
                    MBTextManager.SetTextVariable("CD_DYN_S1", new TextObject(_activeProblem.Solutions[0].DescriptionTextId));
                    return true;
                },
                () => {
                    _activeProblem.Solutions[0].OnSelected?.Invoke(Settlement.CurrentSettlement, GetCastleData(Settlement.CurrentSettlement));
                });

            // Çözüm 2
            starter.AddPlayerLine("cd_dynamic_s2", "cd_dynamic_problem_choices", "cd_dynamic_result",
                "{CD_DYN_S2}",
                () => {
                    if (_activeProblem == null || _activeProblem.Solutions.Count < 2) return false;
                    MBTextManager.SetTextVariable("CD_DYN_S2", new TextObject(_activeProblem.Solutions[1].DescriptionTextId));
                    return true;
                },
                () => {
                    _activeProblem.Solutions[1].OnSelected?.Invoke(Settlement.CurrentSettlement, GetCastleData(Settlement.CurrentSettlement));
                });

            // Çözüm 3
            starter.AddPlayerLine("cd_dynamic_s3", "cd_dynamic_problem_choices", "cd_dynamic_result",
                "{CD_DYN_S3}",
                () => {
                    if (_activeProblem == null || _activeProblem.Solutions.Count < 3) return false;
                    MBTextManager.SetTextVariable("CD_DYN_S3", new TextObject(_activeProblem.Solutions[2].DescriptionTextId));
                    return true;
                },
                () => {
                    _activeProblem.Solutions[2].OnSelected?.Invoke(Settlement.CurrentSettlement, GetCastleData(Settlement.CurrentSettlement));
                });

            // Eğer problem yoksa tek seçenek (Leave)
            starter.AddPlayerLine("cd_dynamic_leave", "cd_dynamic_problem_choices", "close_window",
                "{=CD_24}Return to your duties.",
                () => _activeProblem == null, null);

            starter.AddDialogLine("cd_dynamic_result", "cd_dynamic_result", "close_window",
                "{=CD_DYN_DONE}It shall be done, commander. We will enact your orders immediately.",
                null, null);

            // Mevcut leave seçeneği (Soru sormadan çıkmak)
            starter.AddPlayerLine("castle_officer_leave", "castle_officer_options", "close_window",
                "{=CD_24}Nothing for now. Return to your duties.",
                null, null);

            // Saboteur / Spy Dialogs
            starter.AddDialogLine("castle_saboteur_start", "start", "castle_saboteur_options",
                "{=CD_35}I am just a poor merchant, my lord. Please, do not hurt me!",
                () => IsSaboteurAgent() && HasSabotageCrisis(), null, 600);

            starter.AddPlayerLine("castle_saboteur_attack", "castle_saboteur_options", "castle_saboteur_fight",
                "{=CD_36}You are a traitor trying to sabotage the headquarters! Guards, execute him!",
                null, null);

            starter.AddPlayerLine("castle_saboteur_leave", "castle_saboteur_options", "close_window",
                "{=CD_37}Alright, you may pass.",
                null, null);

            // GENERIC GUARD REPLY INTERCEPT (Player's own settlements)
            string[] guardInputTokens = new string[] { "hero_main_options", "town_or_village_player", "town_guard_talk", "guard_talk" };
            foreach (var token in guardInputTokens)
            {
                starter.AddPlayerLine("cd_guard_generic_reply_" + token, token, "close_window",
                "{=CD_G_RPLY}Carry on with your duties, soldier.",
                () => Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan && Campaign.Current.ConversationManager.OneToOneConversationCharacter != null && (Campaign.Current.ConversationManager.OneToOneConversationCharacter.Occupation == Occupation.Guard || Campaign.Current.ConversationManager.OneToOneConversationCharacter.Occupation == Occupation.Soldier || Campaign.Current.ConversationManager.OneToOneConversationCharacter.Occupation == Occupation.PrisonGuard),
                null, 1000);
            }


            starter.AddDialogLine("castle_saboteur_fight", "castle_saboteur_fight", "close_window",
                "{=CD_38}You will never take me alive!",
                null,
                () =>
                {
                    // Şüpheliyi düşman takıma geçir (Mission mode)
                    if (Mission.Current != null && Campaign.Current.ConversationManager.OneToOneConversationAgent != null)
                    {
                        var agent = (Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent;
                        var attackerTeam = Mission.Current.AttackerTeam;
                        if (attackerTeam == null)
                        {
                            attackerTeam = Mission.Current.Teams.Add(BattleSideEnum.Attacker, uint.MaxValue, uint.MaxValue, null, true, false, true);
                            attackerTeam.SetIsEnemyOf(Mission.Current.PlayerTeam, true);
                        }
                        agent.SetTeam(attackerTeam, true);
                        agent.SetWatchState(Agent.WatchState.Alarmed);
                    }

                    var data = GetCastleData(Settlement.CurrentSettlement);
                    if (data != null) data.HasSabotageCrisis = false;
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=CD_39}The traitor revealed himself! Kill him!").ToString(), Colors.Red));
                });
        }

        private bool IsSaboteurAgent()
        {
            var agent = Campaign.Current.ConversationManager.OneToOneConversationAgent;
            return agent != null && agent.Character != null && agent.Character.StringId == "looter"; // Spawn ederken looter veya özel id kullanacağız
        }

        private bool IsCastleOfficerAgent()
        {
            var agent = Campaign.Current.ConversationManager.OneToOneConversationAgent;
            return agent != null && CastleDynamicsMissionBehavior.Bodyguards.Contains(agent);
        }

        private bool HasPendingCrisis()
        {
            var data = GetCastleData(Settlement.CurrentSettlement);
            return data != null && data.HasPendingCrisis;
        }

        private bool HasSabotageCrisis()
        {
            // Sabotaj sistemi şimdilik kapalı, her zaman false dön!
            return false;

            // var data = GetCastleData(Settlement.CurrentSettlement);
            // return data != null && data.HasSabotageCrisis;
        }

        private void ResolveCrisis(bool isHardDecision)
        {
            var data = GetCastleData(Settlement.CurrentSettlement);
            if (data != null)
            {
                data.HasPendingCrisis = false;
                if (isHardDecision)
                {
                    data.StabilityScore = Math.Min(100, data.StabilityScore + 5);
                    if (Settlement.CurrentSettlement.Town != null) Settlement.CurrentSettlement.Town.Security += 5f;
                }
                else
                {
                    Hero.MainHero.ChangeHeroGold(-1000);
                    data.StabilityScore = Math.Min(100, data.StabilityScore + 10);
                    if (Settlement.CurrentSettlement.Town != null) Settlement.CurrentSettlement.Town.Prosperity += 20f;
                }
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=CD_10}The crisis is resolved and headquarters stability is restored.").ToString(), Colors.Green));
            }
        }

        private void ShowDoctrineSelectionInquiry()
        {
            var data = GetCastleData(Settlement.CurrentSettlement);
            if (data == null) return;
            CastleDoctrineUIManager.Open(data, () => GameMenu.SwitchToMenu("castle_headquarters_menu"));
        }

        private void ShowStrategySelectionInquiry()
        {
            var data = GetCastleData(Settlement.CurrentSettlement);
            if (data == null) return;
            CastleTreasuryUIManager.Open(data, () => GameMenu.SwitchToMenu("castle_headquarters_treasury_menu"));
        }

        public string GetDoctrineName(CastleDoctrine doc)
        {
            switch (doc)
            {
                case CastleDoctrine.Traditional: return new TextObject("{=CD_DOC_0}Traditional (No Bonus or Penalty)").ToString();
                case CastleDoctrine.IronDiscipline: return new TextObject("{=CD_DOC_1}Iron Discipline (+Militia, +Security, -Prosperity)").ToString();
                case CastleDoctrine.TradeHub: return new TextObject("{=CD_DOC_2}Trade Hub (+Tax, +Food, -Security)").ToString();
                case CastleDoctrine.FrontierOutpost: return new TextObject("{=CD_DOC_3}Frontier Outpost (+Radar Range, +Logistics, -Tax)").ToString();
                case CastleDoctrine.AgrarianFocus: return new TextObject("{=CD_DOC_4}Agrarian Focus (+Food, +Village Growth, -Militia)").ToString();
                case CastleDoctrine.CulturalHub: return new TextObject("{=CD_DOC_5}Cultural Hub (+Loyalty, +Influence, -Build Speed)").ToString();
                case CastleDoctrine.WarCamp: return new TextObject("{=CD_DOC_6}War Camp (+Garrison XP, +Recruitment, -Prosperity)").ToString();
                case CastleDoctrine.FortifiedTown: return new TextObject("{=CD_DOC_7}Fortified Town (+Wall HP, +Security, -Tax)").ToString();
                case CastleDoctrine.SpyNetwork: return new TextObject("{=CD_DOC_8}Spy Network (+Radar Range, +Security, -Loyalty)").ToString();
                case CastleDoctrine.FeudalHeadquarters: return new TextObject("{=CD_DOC_9}Feudal Headquarters (+Influence, +Militia, -Food)").ToString();
                default: return new TextObject("{=CD_DOC_10}Unknown").ToString();
            }
        }
    }

    // ==========================================
    // Castle Doctrine UI
    // ==========================================
    public class DoctrineItemVM : TaleWorlds.Library.ViewModel
    {
        private string _name;
        private int _doctrineId;
        private System.Action<int> _onSelect;

        [TaleWorlds.Library.DataSourceProperty]
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChangedWithValue(value, "Name"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public int DoctrineId { get => _doctrineId; set { if (_doctrineId != value) { _doctrineId = value; OnPropertyChangedWithValue(value, "DoctrineId"); } } }

        public DoctrineItemVM(string name, int id, System.Action<int> onSelect)
        {
            Name = name;
            DoctrineId = id;
            _onSelect = onSelect;
        }

        public void ExecuteSelectDoctrine()
        {
            _onSelect?.Invoke(DoctrineId);
        }
    }

    public class CastleDoctrineVM : TaleWorlds.Library.ViewModel
    {
        private CastleData _data;
        private System.Action _onClose;
        private TaleWorlds.Library.MBBindingList<DoctrineItemVM> _doctrines;
        private string _selectedDoctrineDesc;
        private int _selectedDoctrineId = -1;

        [TaleWorlds.Library.DataSourceProperty]
        public TaleWorlds.Library.MBBindingList<DoctrineItemVM> Doctrines { get => _doctrines; set { if (_doctrines != value) { _doctrines = value; OnPropertyChangedWithValue(value, "Doctrines"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public string SelectedDoctrineDesc { get => _selectedDoctrineDesc; set { if (_selectedDoctrineDesc != value) { _selectedDoctrineDesc = value; OnPropertyChangedWithValue(value, "SelectedDoctrineDesc"); } } }

        public CastleDoctrineVM(CastleData data, System.Action onClose)
        {
            _data = data;
            _onClose = onClose;
            Doctrines = new TaleWorlds.Library.MBBindingList<DoctrineItemVM>();
            SelectedDoctrineDesc = new TaleWorlds.Localization.TextObject("{=CD_12}Select the new doctrine to be implemented for this settlement. Each doctrine has its own unique strengths and weaknesses.").ToString();

            var b = TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<CastleDynamicsBehavior>();
            if (b != null)
            {
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.Traditional), (int)CastleDoctrine.Traditional, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.IronDiscipline), (int)CastleDoctrine.IronDiscipline, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.TradeHub), (int)CastleDoctrine.TradeHub, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.FrontierOutpost), (int)CastleDoctrine.FrontierOutpost, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.AgrarianFocus), (int)CastleDoctrine.AgrarianFocus, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.CulturalHub), (int)CastleDoctrine.CulturalHub, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.WarCamp), (int)CastleDoctrine.WarCamp, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.FortifiedTown), (int)CastleDoctrine.FortifiedTown, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.SpyNetwork), (int)CastleDoctrine.SpyNetwork, ExecuteSelectAndAcceptDoctrine));
                Doctrines.Add(new DoctrineItemVM(b.GetDoctrineName(CastleDoctrine.FeudalHeadquarters), (int)CastleDoctrine.FeudalHeadquarters, ExecuteSelectAndAcceptDoctrine));
            }
        }

        public void ExecuteSelectAndAcceptDoctrine(int doctrineId)
        {
            ExecuteSelectDoctrine(doctrineId);
            ExecuteAccept();
        }

        public void ExecuteSelectDoctrine(int doctrineId)
        {
            InputLogger.Log($"[CastleDoctrineUI] User clicked to select doctrine: {doctrineId}");
            _selectedDoctrineId = doctrineId;
            var b = TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<CastleDynamicsBehavior>();
            SelectedDoctrineDesc = b != null ? b.GetDoctrineName((CastleDoctrine)doctrineId) : "";
        }

        public void ExecuteAccept()
        {
            InputLogger.Log($"[CastleDoctrineUI] User clicked Accept with selected doctrine ID: {_selectedDoctrineId}");
            if (_selectedDoctrineId != -1)
            {
                _data.ActiveDoctrine = (CastleDoctrine)_selectedDoctrineId;
                var b = TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<CastleDynamicsBehavior>();
                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=CD_15}New doctrine set: {DOCTRINE}").SetTextVariable("DOCTRINE", b?.GetDoctrineName((CastleDoctrine)_selectedDoctrineId)).ToString(), TaleWorlds.Library.Colors.Green));
            }
            CastleDoctrineUIManager.Close();
        }

        public void ExecuteCancel()
        {
            InputLogger.Log("[CastleDoctrineUI] User clicked Cancel");
            CastleDoctrineUIManager.Close();
        }
    }

    public static class CastleDoctrineUIManager
    {
        private static TaleWorlds.Engine.GauntletUI.GauntletLayer _layer;
        private static CastleDoctrineVM _vm;
        private static System.Action _onCloseAction;

        public static void Open(CastleData data, System.Action onClose)
        {
            if (_layer != null) return;
            _onCloseAction = onClose;
            _vm = new CastleDoctrineVM(data, Close);
            _layer = new TaleWorlds.Engine.GauntletUI.GauntletLayer("CastleDoctrineUI", 1000);
            _layer.LoadMovie("CastleDoctrineUI", _vm);
            _layer.IsFocusLayer = true;
            _layer.InputRestrictions.SetInputRestrictions(true, TaleWorlds.Library.InputUsageMask.All);
            TaleWorlds.ScreenSystem.ScreenManager.TopScreen.AddLayer(_layer);
            TaleWorlds.ScreenSystem.ScreenManager.TrySetFocus(_layer);
            if (TaleWorlds.CampaignSystem.Campaign.Current != null) TaleWorlds.CampaignSystem.Campaign.Current.TimeControlMode = TaleWorlds.CampaignSystem.CampaignTimeControlMode.Stop;
        }

        public static void Close()
        {
            if (_layer != null)
            {
                TaleWorlds.ScreenSystem.ScreenManager.TopScreen.RemoveLayer(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                _layer = null;
            }
            if (_vm != null)
            {
                _vm.OnFinalize();
                _vm = null;
            }
            _onCloseAction?.Invoke();
            _onCloseAction = null;
        }
    }

    // ==========================================
    // Castle Treasury UI
    // ==========================================
    public class StrategyItemVM : TaleWorlds.Library.ViewModel
    {
        private string _name;
        private int _strategyId;
        private System.Action<int> _onSelect;

        [TaleWorlds.Library.DataSourceProperty]
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChangedWithValue(value, "Name"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public int StrategyId { get => _strategyId; set { if (_strategyId != value) { _strategyId = value; OnPropertyChangedWithValue(value, "StrategyId"); } } }

        public StrategyItemVM(string name, int id, System.Action<int> onSelect)
        {
            Name = name;
            StrategyId = id;
            _onSelect = onSelect;
        }

        public void ExecuteSelectStrategy()
        {
            _onSelect?.Invoke(StrategyId);
        }
    }

    public class CastleTreasuryVM : TaleWorlds.Library.ViewModel
    {
        private CastleData _data;
        private System.Action _onClose;
        private TaleWorlds.Library.MBBindingList<StrategyItemVM> _strategies;
        private string _selectedStrategyDesc;
        private string _treasuryBalanceText;
        private int _selectedStrategyId = -1;

        [TaleWorlds.Library.DataSourceProperty]
        public TaleWorlds.Library.MBBindingList<StrategyItemVM> Strategies { get => _strategies; set { if (_strategies != value) { _strategies = value; OnPropertyChangedWithValue(value, "Strategies"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public string SelectedStrategyDesc { get => _selectedStrategyDesc; set { if (_selectedStrategyDesc != value) { _selectedStrategyDesc = value; OnPropertyChangedWithValue(value, "SelectedStrategyDesc"); } } }

        [TaleWorlds.Library.DataSourceProperty]
        public string TreasuryBalanceText { get => _treasuryBalanceText; set { if (_treasuryBalanceText != value) { _treasuryBalanceText = value; OnPropertyChangedWithValue(value, "TreasuryBalanceText"); } } }

        public CastleTreasuryVM(CastleData data)
        {
            _data = data;
            Strategies = new TaleWorlds.Library.MBBindingList<StrategyItemVM>();
            SelectedStrategyDesc = new TaleWorlds.Localization.TextObject("{=CD_TREAS_STRAT_DESC}Select the new treasury spending strategy for this settlement.").ToString();
            TreasuryBalanceText = $"Current Balance: {data.Treasury} Denars";

            Strategies.Add(new StrategyItemVM(new TaleWorlds.Localization.TextObject("{=CD_STRAT_BAL}Balanced").ToString(), (int)TreasuryStrategy.Balanced, ExecuteSelectAndAcceptStrategy));
            Strategies.Add(new StrategyItemVM(new TaleWorlds.Localization.TextObject("{=CD_STRAT_WAR}Preparation for War").ToString(), (int)TreasuryStrategy.WarPrep, ExecuteSelectAndAcceptStrategy));
            Strategies.Add(new StrategyItemVM(new TaleWorlds.Localization.TextObject("{=CD_STRAT_HAP}Happiness Focus").ToString(), (int)TreasuryStrategy.Happiness, ExecuteSelectAndAcceptStrategy));
        }

        public void ExecuteSelectAndAcceptStrategy(int strategyId)
        {
            ExecuteSelectStrategy(strategyId);
            ExecuteAccept();
        }

        public void ExecuteSelectStrategy(int id)
        {
            InputLogger.Log($"[CastleTreasuryUI] User clicked to select strategy: {id}");
            _selectedStrategyId = id;
            var strategy = (TreasuryStrategy)id;
            if (strategy == TreasuryStrategy.Balanced) SelectedStrategyDesc = new TaleWorlds.Localization.TextObject("{=CD_DESC_BAL}The treasury will only hold funds for emergency situations. No daily spending.").ToString();
            else if (strategy == TreasuryStrategy.WarPrep) SelectedStrategyDesc = new TaleWorlds.Localization.TextObject("{=CD_DESC_WAR}The treasury spends 500 Denars daily to grant 500 XP to a random garrison troop, and recruits 2 basic troops if under limit.").ToString();
            else if (strategy == TreasuryStrategy.Happiness) SelectedStrategyDesc = new TaleWorlds.Localization.TextObject("{=CD_DESC_HAP}The treasury spends funds dynamically based on current loyalty and prosperity. Increases loyalty by +1 and prosperity by +10 daily.").ToString();
        }

        public void ExecuteAccept()
        {
            InputLogger.Log($"[CastleTreasuryUI] User clicked Accept with selected strategy ID: {_selectedStrategyId}");
            if (_selectedStrategyId != -1)
            {
                _data.ActiveTreasuryStrategy = (TreasuryStrategy)_selectedStrategyId;
                InformationManager.DisplayMessage(new InformationMessage(new TaleWorlds.Localization.TextObject("{=CD_STRAT_SET}New strategy set!").ToString(), TaleWorlds.Library.Colors.Green));
            }
            CastleTreasuryUIManager.Close();
        }

        public void ExecuteCancel()
        {
            InputLogger.Log("[CastleTreasuryUI] User clicked Cancel");
            CastleTreasuryUIManager.Close();
        }
    }

    public static class CastleTreasuryUIManager
    {
        private static TaleWorlds.Engine.GauntletUI.GauntletLayer _layer;
        private static CastleTreasuryVM _vm;
        private static System.Action _onCloseAction;

        public static void Open(CastleData data, System.Action onClose)
        {
            if (_layer != null) return;
            _onCloseAction = onClose;
            _vm = new CastleTreasuryVM(data);
            _layer = new TaleWorlds.Engine.GauntletUI.GauntletLayer("CastleTreasuryUI", 1000);
            _layer.LoadMovie("CastleTreasuryUI", _vm);
            _layer.IsFocusLayer = true;
            _layer.InputRestrictions.SetInputRestrictions(true, TaleWorlds.Library.InputUsageMask.All);
            TaleWorlds.ScreenSystem.ScreenManager.TopScreen.AddLayer(_layer);
            TaleWorlds.ScreenSystem.ScreenManager.TrySetFocus(_layer);
            if (TaleWorlds.CampaignSystem.Campaign.Current != null) TaleWorlds.CampaignSystem.Campaign.Current.TimeControlMode = TaleWorlds.CampaignSystem.CampaignTimeControlMode.Stop;
        }

        public static void Close()
        {
            if (_layer != null)
            {
                TaleWorlds.ScreenSystem.ScreenManager.TopScreen.RemoveLayer(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                _layer = null;
            }
            if (_vm != null)
            {
                _vm.OnFinalize();
                _vm = null;
            }
            _onCloseAction?.Invoke();
            _onCloseAction = null;
        }
    }
}