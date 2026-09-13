using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using SandBox.Missions.MissionLogics;
using SandBox.View;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class WorkshopViolenceBehavior : CampaignBehaviorBase
    {
        public static WorkshopViolenceBehavior Instance;

        private Dictionary<string, bool> _extortionActiveInTown = new Dictionary<string, bool>();
        public Dictionary<string, bool> ExtortionActiveInTown { get { return _extortionActiveInTown ?? (_extortionActiveInTown = new Dictionary<string, bool>()); } set { _extortionActiveInTown = value; } }

        private Dictionary<string, bool> _workerRiotActiveInTown = new Dictionary<string, bool>();
        public Dictionary<string, bool> WorkerRiotActiveInTown { get { return _workerRiotActiveInTown ?? (_workerRiotActiveInTown = new Dictionary<string, bool>()); } set { _workerRiotActiveInTown = value; } }

        private Dictionary<string, int> _workshopDisabledDays = new Dictionary<string, int>();
        public Dictionary<string, int> WorkshopDisabledDays { get { return _workshopDisabledDays ?? (_workshopDisabledDays = new Dictionary<string, int>()); } set { _workshopDisabledDays = value; } }

        private Dictionary<string, float> _workshopSafetyBonus = new Dictionary<string, float>();
        public Dictionary<string, float> WorkshopSafetyBonus { get { return _workshopSafetyBonus ?? (_workshopSafetyBonus = new Dictionary<string, float>()); } set { _workshopSafetyBonus = value; } }

        private List<CharacterObject> _selectedTroops = new List<CharacterObject>();
        private string _pendingMissionType = "";
        private int _dailyTickCounter = 0;

        public WorkshopViolenceBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // Village event yok, DailyTick'te kontrol edilecek
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private bool _launchMissionRequested = false;

        private void OnTick(float dt)
        {
            if (_launchMissionRequested && Campaign.Current.CurrentMenuContext == null)
            {
                _launchMissionRequested = false;
                LaunchMission(_pendingMissionType);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in ExtortionActiveInTown.ToList()) { if (!(pair.Key != null)) ExtortionActiveInTown.Remove(pair.Key); }
                foreach (var pair in WorkerRiotActiveInTown.ToList()) { if (!(pair.Key != null)) WorkerRiotActiveInTown.Remove(pair.Key); }
                foreach (var pair in WorkshopDisabledDays.ToList()) { if (!(pair.Key != null)) WorkshopDisabledDays.Remove(pair.Key); }
                foreach (var pair in WorkshopSafetyBonus.ToList()) { if (!(pair.Key != null)) WorkshopSafetyBonus.Remove(pair.Key); }
            }
            dataStore.SyncData("_extortionActiveInTown", ref _extortionActiveInTown);
            dataStore.SyncData("_workerRiotActiveInTown", ref _workerRiotActiveInTown);
            dataStore.SyncData("_workshopDisabledDays", ref _workshopDisabledDays);
            dataStore.SyncData("_workshopSafetyBonus", ref _workshopSafetyBonus);

            if (_extortionActiveInTown == null) _extortionActiveInTown = new Dictionary<string, bool>();
            if (_workerRiotActiveInTown == null) _workerRiotActiveInTown = new Dictionary<string, bool>();
            if (_workshopDisabledDays == null) _workshopDisabledDays = new Dictionary<string, int>();
            if (_workshopSafetyBonus == null) _workshopSafetyBonus = new Dictionary<string, float>();
        }

        private void OnDailyTick()
        {
            _dailyTickCounter++;
            bool isWeekly = (_dailyTickCounter % 7 == 0);

            var disabledKeys = _workshopDisabledDays.Keys.ToList();
            foreach (var k in disabledKeys)
            {
                if (_workshopDisabledDays[k] > 0)
                {
                    _workshopDisabledDays[k]--;
                }
            }

            if (isWeekly)
            {
                foreach (var town in Town.AllTowns)
                {
                    if (town.Workshops.Any(w => w.Owner == Hero.MainHero))
                    {
                        if (MBRandom.RandomFloat < 0.20f)
                        {
                            _extortionActiveInTown[town.Settlement.StringId] = true;
                            InformationManager.DisplayMessage(new InformationMessage("Extortion gang warning!" + town.Name.ToString() + "They were organized in the city.", Colors.Red));
                        }
                        if (MBRandom.RandomFloat < 0.15f)
                        {
                            _workerRiotActiveInTown[town.Settlement.StringId] = true;
                            InformationManager.DisplayMessage(new InformationMessage("Workers' rebellion has begun!" + town.Name.ToString(), Colors.Red));
                        }
                    }
                }
            }

            if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown && Settlement.CurrentSettlement.Town.Workshops.Any(w => w.Owner == Hero.MainHero))
            {
                if (MBRandom.RandomFloat < 0.30f)
                {
                    InformationManager.ShowInquiry(new InquiryData("Night Raid", 
                        "Attack warning! There is an attack on your workshop! Defend?", 
                        true, true, "Evet", "Hayir",
                        () => {
                            _selectedTroops.Clear(); // no allies
                            _pendingMissionType = "night_raid";
                            LaunchMission("night_raid");
                        }, 
                        () => {
                            _workshopDisabledDays[Settlement.CurrentSettlement.StringId] = 7;
                            InformationManager.DisplayMessage(new InformationMessage("The workshop has been damaged and will be closed for 7 days.", Colors.Red));
                        }));
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_workshop_menu", "workshop_hostile_takeover", "Smash (Forcibly Seize) the Rival Workshop",
                (args) =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    var town = Settlement.CurrentSettlement.Town;
                    var playerWorkshops = town.Workshops.Where(w => w.Owner == Hero.MainHero).ToList();
                    if (playerWorkshops.Count == 0) return false;
                    
                    bool hasNpcSameType = false;
                    foreach (var pw in playerWorkshops)
                    {
                        if (town.Workshops.Any(w => w.Owner != Hero.MainHero && w.WorkshopType == pw.WorkshopType))
                        {
                            hasNpcSameType = true;
                            break;
                        }
                    }
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    return hasNpcSameType;
                },
                (args) =>
                {
                    _pendingMissionType = "takeover";
                    ShowTroopSelectionAndLaunch();
                });

            starter.AddGameMenuOption("town_workshop_menu", "workshop_extortion_raid", "Attack the Extortion Gang",
                (args) =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    return _extortionActiveInTown.TryGetValue(Settlement.CurrentSettlement.StringId, out bool active) && active;
                },
                (args) =>
                {
                    _pendingMissionType = "extortion";
                    ShowTroopSelectionAndLaunch();
                });

            starter.AddGameMenuOption("town_workshop_menu", "workshop_riot_control", "Suppress Workers' Revolt",
                (args) =>
                {
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    return _workerRiotActiveInTown.TryGetValue(Settlement.CurrentSettlement.StringId, out bool active) && active;
                },
                (args) =>
                {
                    _pendingMissionType = "riot";
                    ShowTroopSelectionAndLaunch();
                });
        }

        private void ShowTroopSelectionAndLaunch()
        {
            List<InquiryElement> elements = new List<InquiryElement>();
            foreach (var troopRosterElement in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
                if (!troopRosterElement.Character.IsHero && troopRosterElement.Number > 0)
                {
                    elements.Add(new InquiryElement(troopRosterElement.Character, troopRosterElement.Character.Name.ToString(), null));
                }
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Select Troops", "Choose the men you will take with you (Max 10)", elements, true, 1, 10, "Basla", "Iptal",
                (selectedElements) =>
                {
                    _selectedTroops.Clear();
                    foreach (var element in selectedElements)
                    {
                        _selectedTroops.Add(element.Identifier as CharacterObject);
                    }
                    _launchMissionRequested = true;
                }, null));
        }

        private void LaunchMission(string missionType)
        {
            string sceneName = "khuzait_castle_keep_a_l1_interior";

            MissionLogic[] logicList = new MissionLogic[]
            {
                new MissionOptionsComponent(),
                new WorkshopViolenceMissionLogic(missionType, _selectedTroops)
            };

            Mission mission = MissionState.OpenNew(
                "WorkshopViolence",
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, DecalAtlasGroup.All),
                (m) => logicList,
                true, true
            );

            if (mission != null)
            {
                mission.AddMissionBehavior(SandBoxViewCreator.CreateMissionConversationView(mission));
                mission.AddMissionBehavior(ViewCreator.CreateMissionMainAgentEquipmentController(mission));
                mission.AddMissionBehavior(ViewCreator.CreateMissionAgentStatusUIHandler(mission));
                mission.AddMissionBehavior(ViewCreator.CreateMissionSingleplayerEscapeMenu(false));
                mission.AddMissionBehavior(ViewCreator.CreateMissionLeaveView());
            }
        }
    }

    public class WorkshopViolenceMissionLogic : MissionLogic
    {
        private string _missionType;
        private List<CharacterObject> _allies;
        private bool _isInitialized = false;
        private float _missionTime = 0f;

        public WorkshopViolenceMissionLogic(string missionType, List<CharacterObject> allies)
        {
            _missionType = missionType;
            _allies = allies ?? new List<CharacterObject>();
        }

        public override void OnMissionTick(float dt)
        {
            if (Mission.Current == null || Mission.Current.MissionEnded) return;

            base.OnMissionTick(dt);

            if (!_isInitialized)
            {
                _isInitialized = true;
                SetupScene();
                return;
            }

            _missionTime += dt;
            if (_missionTime > 2.0f)
            {
                CheckEndConditions();
            }
        }

        private void SetupScene()
        {
            Scene scene = Mission.Current.Scene;
            MatrixFrame centerFrame = MatrixFrame.Identity;
            GameEntity anchor = scene.FindEntityWithTag("sp_throne");
            
            if (anchor != null)
            {
                centerFrame = anchor.GetGlobalFrame();
                Vec3 forward = centerFrame.rotation.f;
                forward.Normalize();
                centerFrame.origin += forward * 6.0f;
            }
            else
            {
                scene.GetBoundingBox(out Vec3 min, out Vec3 max);
                centerFrame.origin = (min + max) * 0.5f;
            }

            centerFrame.origin.z = GetGroundZ(scene, centerFrame.origin);

            // Spawn Player
            AgentBuildData playerBuildData = new AgentBuildData(new PartyAgentOrigin(PartyBase.MainParty, CharacterObject.PlayerCharacter))
                .InitialPosition(centerFrame.origin)
                .InitialDirection(centerFrame.rotation.f.AsVec2)
                .CivilianEquipment(false)
                .NoHorses(true)
                .Team(Mission.Current.PlayerTeam)
                .Controller(AgentControllerType.Player);
            Mission.Current.SpawnAgent(playerBuildData);

            // Spawn Allies
            MatrixFrame allyFrame = centerFrame;
            allyFrame.origin -= centerFrame.rotation.f * 2.0f;
            foreach (var ally in _allies)
            {
                allyFrame.origin.z = GetGroundZ(scene, allyFrame.origin);
                AgentBuildData allyBuildData = new AgentBuildData(new PartyAgentOrigin(PartyBase.MainParty, ally))
                    .InitialPosition(allyFrame.origin)
                    .InitialDirection(allyFrame.rotation.f.AsVec2)
                    .CivilianEquipment(false)
                    .NoHorses(true)
                    .Team(Mission.Current.PlayerTeam)
                    .Controller(AgentControllerType.AI);
                Mission.Current.SpawnAgent(allyBuildData);
            }

            // Spawn Enemies
            int enemyCount = (_missionType == "riot") ? 20 : 12;
            CharacterObject enemyChar = CharacterObject.Find("looter");
            if (enemyChar == null) enemyChar = CharacterObject.All.FirstOrDefault(c => c.IsBasicTroop);

            MatrixFrame enemyFrame = centerFrame;
            enemyFrame.origin += centerFrame.rotation.f * 10.0f;
            for (int i = 0; i < enemyCount; i++)
            {
                enemyFrame.origin.z = GetGroundZ(scene, enemyFrame.origin);
                AgentBuildData enemyBuildData = new AgentBuildData(new SimpleAgentOrigin(enemyChar, -1, null, default(UniqueTroopDescriptor)))
                    .InitialPosition(enemyFrame.origin + new Vec3(MBRandom.RandomFloat * 4f - 2f, MBRandom.RandomFloat * 4f - 2f, 0))
                    .InitialDirection((-centerFrame.rotation.f).AsVec2)
                    .CivilianEquipment(false)
                    .NoHorses(true)
                    .Team(Mission.Current.PlayerEnemyTeam)
                    .Controller(AgentControllerType.AI);
                Mission.Current.SpawnAgent(enemyBuildData);
            }
        }

        private float GetGroundZ(Scene scene, Vec3 pos)
        {
            Vec3 groundPos;
            scene.GetGroundHeightAtPosition(pos, out groundPos, BodyFlags.CommonCollisionExcludeFlags);
            return groundPos.z;
        }

        private void CheckEndConditions()
        {
            bool enemiesAlive = false;
            bool playerAlive = false;

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent.IsActive())
                {
                    if (agent.Team != null)
                    {
                        if (agent.Team == Mission.Current.PlayerEnemyTeam) enemiesAlive = true;
                        if (agent.IsMainAgent) playerAlive = true;
                    }
                }
            }

            if (!enemiesAlive)
            {
                InformationManager.DisplayMessage(new InformationMessage("Mission successful!", Colors.Green));
                if (_missionType == "riot" && Settlement.CurrentSettlement != null)
                {
                    WorkshopCouncilJudgmentBehavior.Instance?.QueueJudgment(Settlement.CurrentSettlement.StringId, CouncilJudgmentCrisisType.WorkerRevolt);
                }
                Mission.Current.EndMission();
            }
            else if (!playerAlive)
            {
                InformationManager.DisplayMessage(new InformationMessage("Mission failed!", Colors.Red));
                Mission.Current.EndMission();
            }
        }
    }
}
