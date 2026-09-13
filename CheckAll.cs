using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

class Program {
    static void Main() {
        var dll = Assembly.LoadFrom(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22\bin\Debug\ClassLibrary22.dll"");
        var vars = new string[] { ""_activeAiOffers"", ""_activeDailyTributes"", ""_activeEmbargoes"", ""_activeMigrationVillages"", ""_activePlagueCity"", ""_activePlagueDaysLeft"", ""_activePolicies"", ""_activeQuest"", ""_activeSpeculations"", ""_activeStrikes"", ""_activeTask"", ""_activeTenders"", ""_activeTradeWars"", ""_advisorTown"", ""_battlefields"", ""_battleTriggered"", ""_budgetGiven"", ""_castleData"", ""_ceasefireDailyTributeWeReceive"", ""_ceasefireStartTimes"", ""_cityDebts"", ""_clanLoyalty"", ""_clanUnrest"", ""_coalitionWarningSent"", ""_conferenceCooldowns"", ""_conferenceDeadlines"", ""_conferenceLocations"", ""_confiscatedBy"", ""_confiscatedFromUsBy"", ""_confiscatedWorkshops"", ""_confiscationDeadline"", ""_councilCity"", ""_daysAfterRetreat"", ""_empireCapitals"", ""_enemyFaction"", ""_extremePolicyStart"", ""_festivalLocations"", ""_festivalPoints"", ""_gatheringSettlement"", ""_gatheringStartTime"", ""_hasOfferedBribeBefore"", ""_heroBlackmailMaterial"", ""_intrigueCooldowns"", ""_intrigueHateCooldowns"", ""_introTriggered"", ""_investedVillages"", ""_invitedLords"", ""_isCouncilGathering"", ""_isFestivalActive"", ""_isGathering"", ""_isJudgmentPending"", ""_isReadyForCouncil"", ""_isRebellionScheduled"", ""_isRetreatActive"", ""_isRumorActive"", ""_isRumorSeeded"", ""_isSilenced"", ""_isStoryModeActive"", ""_kingdomAgendas"", ""_kingdomCasualties"", ""_lastCouncilTime"", ""_lastDisputeTime"", ""_lastDivanTime"", ""_lastGlobalRebellionTime"", ""_lastNpcOfferTime"", ""_lastProphecyTimes"", ""_lastTenderTime"", ""_lasttier"", ""_marketPriceModifiers"", ""_modifierExpiry"", ""_monopolyDays"", ""_monopolyWarningDays"", ""_nextPlagueDate"", ""_npcWorkshopHealth"", ""_originalNorthClans"", ""_originalSouthClans"", ""_originalWestClans"", ""_peaceTreaties"", ""_pendingAlliances"", ""_playerSabotageTokens"", ""_playerShares"", ""_policyTargets"", ""_populationRegistry"", ""_processedArmies"", ""_questcompanion"", ""_questDeadline"", ""_rendezvousDeadline"", ""_rumorSourceLord"", ""_rumorTimerDays"", ""_sabotageDebuffDays"", ""_savedEnemyScorePercent"", ""_savedMaxCap"", ""_savedOurScorePercent"", ""_scenarioStarted"", ""_scheduledRebellionTime"", ""_schismTriggered"", ""_smugglingActive"", ""_splitTriggered"", ""_startStoryMode"", ""_startTime"", ""_supplyChainBonus"", ""_targetHero"", ""_targetLord"", ""_targetSettlement"", ""_targetTime"", ""_taskAmountRequired"", ""_taskGiver"", ""_tenderPrices"", ""_unificationTriggered"", ""_villageGarrisons"", ""_waitingForMissionToEnd"", ""_waitStartTime"", ""_warCooldowns"", ""_warnedSettlements"", ""_workshopDisabledDays"", ""_workshopLevels"", ""_workshopModifiers"", ""_workshopSafetyBonus"", ""_workshopStrategies"", ""ActiveSabotageDaysLeft"", ""ActiveSabotageKingdom"", ""ActiveSpyDaysLeft"", ""ActiveSpyKingdom"", ""LastTreasonOfferTime"", ""levels"", ""PendingDuelComplaints"", ""specs"", ""trainSettlements"", ""trainTimes"", ""_confiscatedTownIds"" };
        var foundTypes = new HashSet<string>();
        foreach(var t in dll.GetTypes()) {
            foreach(var v in vars) {
                var field = t.GetField(v, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null && field.FieldType.IsGenericType) {
                    foundTypes.Add(field.FieldType.ToString());
                }
            }
            foreach(var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (m.CustomAttributes.Any(a => a.AttributeType.Name.Contains(""Saveable""))) {
                    Type pt = null;
                    if (m is FieldInfo f) pt = f.FieldType;
                    if (m is PropertyInfo p) pt = p.PropertyType;
                    if (pt != null && pt.IsGenericType) {
                        foundTypes.Add(pt.ToString());
                    }
                }
            }
        }
        foreach(var ft in foundTypes) {
            Console.WriteLine(ft);
        }
    }
}
