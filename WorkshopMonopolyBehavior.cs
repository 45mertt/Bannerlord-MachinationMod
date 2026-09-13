using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class WorkshopMonopolyBehavior : CampaignBehaviorBase
    {
        public static WorkshopMonopolyBehavior Instance { get; private set; }

        // Kingdom ID -> kac gundur tekeliz
        private Dictionary<string, int> _monopolyDays = new Dictionary<string, int>();
        [SaveableProperty(1)]
        public Dictionary<string, int> MonopolyDays { get { return _monopolyDays ?? (_monopolyDays = new Dictionary<string, int>()); } set { _monopolyDays = value; } }
        
        // Elci ile gorusme
        [SaveableField(2)] private string _rendezvousTownId;
        [SaveableField(3)] private string _rendezvousTargetKingdomId;
        [SaveableField(4)] private CampaignTime _rendezvousDeadline;
        
        // Kacakcilik gorevi
        [SaveableField(5)] private bool _smugglingActive;
        [SaveableField(6)] private string _smugglingTargetKingdomId;
        
        // El koyulan kralliklar (Savas sebebi icin)
        private List<string> _confiscatedFromUsBy = new List<string>();
        [SaveableProperty(7)]
        public List<string> ConfiscatedFromUsBy { get { return _confiscatedFromUsBy ?? (_confiscatedFromUsBy = new List<string>()); } set { _confiscatedFromUsBy = value; } }

        public WorkshopMonopolyBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var pair in MonopolyDays.ToList()) { if (!(pair.Key != null)) MonopolyDays.Remove(pair.Key); }
                var confiscated = ConfiscatedFromUsBy;
                confiscated.RemoveAll(x => x == null);
            }

            dataStore.SyncData("_monopolyDays", ref _monopolyDays);
            dataStore.SyncData("_rendezvousTownId", ref _rendezvousTownId);
            dataStore.SyncData("_rendezvousTargetKingdomId", ref _rendezvousTargetKingdomId);
            dataStore.SyncData("_rendezvousDeadline", ref _rendezvousDeadline);
            dataStore.SyncData("_smugglingActive", ref _smugglingActive);
            dataStore.SyncData("_smugglingTargetKingdomId", ref _smugglingTargetKingdomId);
            dataStore.SyncData("_confiscatedFromUsBy", ref _confiscatedFromUsBy);
        }

        [SaveableField(99)] private int _lastSeasonProcessed = -1;

        private void OnDailyTick()
        {
            int currentSeason = (int)CampaignTime.Now.GetSeasonOfYear;
            if (_lastSeasonProcessed != currentSeason)
            {
                _lastSeasonProcessed = currentSeason;
                CalculateMonopolyStates();
                TriggerLightSeasonalEvents();
            }
            
            HandleRendezvousDeadline();
        }

        private void TriggerLightSeasonalEvents()
        {
            if (WorkshopKingdomBehavior.Instance == null) return;
            var playerShares = WorkshopKingdomBehavior.Instance.GetAllShares();
            if (playerShares.Count > 0)
            {
                if (MBRandom.RandomFloat < 0.5f)
                {
                    int bonus = 2000 * playerShares.Count;
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, bonus, true);
                    InformationManager.DisplayMessage(new InformationMessage($"[Seasonal Event] New trade routes opened! +{bonus} Denars", Colors.Green));
                }
                else
                {
                    int loss = 500 * playerShares.Count;
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, loss, true);
                    InformationManager.DisplayMessage(new InformationMessage($"[Seasonal Event] Local tradesmen inflated raw material prices! -{loss} Denar", Colors.Red));
                }
            }
        }

        private void CalculateMonopolyStates()
        {
            if (Hero.MainHero == null || Clan.PlayerClan == null) return;

            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;
                if (Hero.MainHero.IsFactionLeader && kingdom == Hero.MainHero.MapFaction) continue; // Kral kendi kralliginda tekel yuzunden ceza almaz

                int totalWorkshops = 0;
                int playerShares = 0;

                foreach (var settlement in kingdom.Settlements.Where(s => s.IsTown))
                {
                    int maxShares = settlement.Town.Workshops != null ? settlement.Town.Workshops.Length : 4;
                    if (maxShares > 4) maxShares = 4;
                    totalWorkshops += maxShares;

                    var shares = WorkshopKingdomBehavior.Instance?.GetSharesInSettlement(settlement.StringId);
                    if (shares != null) playerShares += shares.Count;
                }

                int requiredForMonopoly = (totalWorkshops / 2) + (totalWorkshops % 2);

                if (totalWorkshops > 0 && playerShares >= requiredForMonopoly)
                {
                    if (!_monopolyDays.ContainsKey(kingdom.StringId))
                        _monopolyDays[kingdom.StringId] = 0;

                    _monopolyDays[kingdom.StringId]++;
                    int seasons = _monopolyDays[kingdom.StringId];

                    ProcessMonopolyDay(kingdom, seasons);
                }
                else
                {
                    if (_monopolyDays.ContainsKey(kingdom.StringId))
                        _monopolyDays.Remove(kingdom.StringId);
                }
            }
        }

        private void ProcessMonopolyDay(Kingdom kingdom, int seasons)
        {
            // Eskiden gün olarak çalışan sistem şimdi mevsim (season) olarak çalışıyor.
            // 4 Mevsim = 1 Yıl.
            if (seasons == 1)
            {
                InformationManager.ShowInquiry(new InquiryData("All Eyes on You", 
                    $"The nobles of {kingdom.Name} are becoming unhappy with your monopolization of the kingdom's trade.", 
                    true, false, "Understood", "", null, null));
            }
            else if (seasons == 4) // 1 Yıl Sonra İhanet
            {
                TriggerEmissaryLetter(kingdom);
            }
            else if (seasons == 5)
            {
                InformationManager.ShowInquiry(new InquiryData("Royal Ultimatum", 
                    new TextObject("{=rad_wmo_01}A final warning came from the kingdom of {KINGDOM}: If you do not hand over your workshops or cut prices, they will be forcibly confiscated!\n(Tip: Change your workshop's strategy to \'Guild Standards\' or drop your monopoly share below 50% to prevent confiscation.)").SetTextVariable("KINGDOM", kingdom.Name).ToString(), 
                    true, false, "Kahretsin", "", null, null));
            }
            else if (seasons == 6)
            {
                if (!_smugglingActive)
                {
                    _smugglingActive = true;
                    _smugglingTargetKingdomId = kingdom.StringId;
                    InformationManager.ShowInquiry(new InquiryData("Leaked Intelligence", 
                        new TextObject("{=rad_wmo_02}The kingdom of {KINGDOM} will soon confiscate all your workshops! Go to the cities immediately and start smuggling your goods out at night!\n(Or change your workshop's strategy to \'Guild Standards\' / drop your monopoly share to prevent confiscation.)").SetTextVariable("KINGDOM", kingdom.Name).ToString(), 
                        true, false, "I must hurry!", "", null, null));
                }
            }
            else if (seasons >= 7)
            {
                ExecuteConfiscation(kingdom);
                _monopolyDays.Remove(kingdom.StringId); // Reset
            }
        }

        private void TriggerEmissaryLetter(Kingdom currentKingdom)
        {
            // En guclu dusman kralligi bul
            var rival = Kingdom.All.Where(k => k != currentKingdom && !k.IsEliminated && k.IsAtWarWith(currentKingdom))
                .OrderByDescending(k => k.Settlements.Count).FirstOrDefault();

            if (rival == null)
            {
                rival = Kingdom.All.Where(k => k != currentKingdom && !k.IsEliminated)
                    .OrderByDescending(k => k.Settlements.Count).FirstOrDefault();
            }

            if (rival != null)
            {
                var neutralTowns = Town.AllTowns.Where(t => t.MapFaction != currentKingdom && t.MapFaction != rival).ToList();
                if (neutralTowns.Count == 0) neutralTowns = Town.AllTowns.ToList();

                var meetingTown = neutralTowns.GetRandomElementInefficiently();
                
                _rendezvousTownId = meetingTown.Settlement.StringId;
                _rendezvousTargetKingdomId = rival.StringId;
                _rendezvousDeadline = CampaignTime.DaysFromNow(10f);

                InformationManager.ShowInquiry(new InquiryData("Secret Letter", 
                    new TextObject("{=rad_wmo_03}A letter with a secret seal arrived from the kingdom of {RIVAL}:\n\n\'We have a very profitable matter to discuss with you. Please come to the city of {TOWN} within 10 days.\'").SetTextVariable("RIVAL", rival.Name).SetTextVariable("TOWN", meetingTown.Settlement.Name).ToString(), 
                    true, false, "Accept", "", null, null));
            }
        }

        private void HandleRendezvousDeadline()
        {
            if (!string.IsNullOrEmpty(_rendezvousTownId) && _rendezvousDeadline.IsPast)
            {
                InformationManager.DisplayMessage(new InformationMessage("The meeting time with the secret messenger has expired. Opportunity missed.", Colors.Red));
                _rendezvousTownId = null;
                _rendezvousTargetKingdomId = null;
            }
        }

        private void ExecuteConfiscation(Kingdom kingdom)
        {
            bool confiscatedAny = false;
            foreach (var settlement in kingdom.Settlements.Where(s => s.IsTown))
            {
                var workshops = settlement.Town.Workshops.Where(w => w.Owner == Hero.MainHero).ToList();
                foreach (var ws in workshops)
                {
                    ChangeOwnerOfWorkshopAction.ApplyByDeath(ws, kingdom.Leader); // El koyma
                    confiscatedAny = true;
                }
            }

            _smugglingActive = false; // Gorev bitti
            
            if (confiscatedAny)
            {
                InformationManager.ShowInquiry(new InquiryData("CONFUSED!", 
                    $"ALL your workshops in the kingdom {kingdom.Name} have been seized by the government! All your investments have been deleted.", 
                    true, false, "They will pay for this!", "", null, null));
                
                if (!_confiscatedFromUsBy.Contains(kingdom.StringId))
                    _confiscatedFromUsBy.Add(kingdom.StringId);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Gizli Gorusme Menusu
            starter.AddGameMenuOption("town_wait_menus", "workshop_rendezvous", "Meet the stranger at the inn (Intelligence)",
                (args) =>
                {
                    if (string.IsNullOrEmpty(_rendezvousTownId) || string.IsNullOrEmpty(_rendezvousTargetKingdomId)) return false;
                    if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement.StringId != _rendezvousTownId) return false;
                    
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                (args) =>
                {
                    var targetKingdom = Kingdom.All.FirstOrDefault(k => k.StringId == _rendezvousTargetKingdomId);
                    if (targetKingdom != null && targetKingdom.Leader != null)
                    {
                        StartRendezvousMission(targetKingdom.Leader, Settlement.CurrentSettlement);
                    }
                });

            // Kacakcilik Menusu
            starter.AddGameMenuOption("town_workshop_menu", "workshop_smuggle_assets", "Evacuate the Workshop at Night (Smuggle Goods)",
                (args) =>
                {
                    if (!_smugglingActive) return false;
                    if (Settlement.CurrentSettlement == null || !Settlement.CurrentSettlement.IsTown) return false;
                    if (Settlement.CurrentSettlement.MapFaction.StringId != _smugglingTargetKingdomId) return false;
                    
                    bool hasWorkshop = Settlement.CurrentSettlement.Town.Workshops.Any(w => w.Owner == Hero.MainHero);
                    args.optionLeaveType = GameMenuOption.LeaveType.Escape;
                    return hasWorkshop;
                },
                (args) =>
                {
                    var workshops = Settlement.CurrentSettlement.Town.Workshops.Where(w => w.Owner == Hero.MainHero).ToList();
                    int totalGoldRescued = 0;
                    foreach (var ws in workshops)
                    {
                        int value = 7000; // Standart atolye degerinin %70'i civari
                        totalGoldRescued += value;
                        ChangeOwnerOfWorkshopAction.ApplyByDeath(ws, Settlement.CurrentSettlement.MapFaction.Leader);
                    }
                    
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, totalGoldRescued, true);
                    InformationManager.DisplayMessage(new InformationMessage($"Workshops were successfully evacuated! {totalGoldRescued} Denar was kidnapped.", Colors.Green));
                });

            // Rendezvous Dialogs
            starter.AddDialogLine("rendezvous_intro", "start", "rendezvous_offer",
                "You've finally arrived. I heard that you are managing the trade of your kingdom from one hand. We need friends like you.",
                () => TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<WorkshopRendezvousMissionLogic>() != null && Hero.OneToOneConversationHero != null,
                null);

            starter.AddPlayerLine("rendezvous_reply_1", "rendezvous_offer", "rendezvous_deal",
                "What do you offer?",
                null, null);

            starter.AddDialogLine("rendezvous_deal_explain", "rendezvous_deal", "rendezvous_decision",
                "Destroy the kingdom you are in from the inside. Dry their market and smuggle your goods and join us. If you seek asylum, we will give you a large amount of gold and land. Do we have an agreement?",
                null, null);

            starter.AddPlayerLine("rendezvous_accept", "rendezvous_decision", "close_window",
                "I accept. Once I get my hands on the reins, I will bring down the kingdom and join you.",
                null,
                () => 
                {
                    _rendezvousTownId = null; // Gorusme bitti
                    InformationManager.DisplayMessage(new InformationMessage("A treason agreement was made! There is no turning back now.", Colors.Red));
                });

            starter.AddPlayerLine("rendezvous_reject", "rendezvous_decision", "close_window",
                "Never! I am not a traitor.",
                null,
                () => 
                {
                    _rendezvousTownId = null; 
                    _rendezvousTargetKingdomId = null;
                });
        }
        
        private void StartRendezvousMission(Hero emissary, Settlement town)
        {
            var sceneName = town.LocationComplex.GetScene("tavern", 1);
            Mission mission = TaleWorlds.MountAndBlade.MissionState.OpenNew("WorkshopRendezvousMission",
                  SandBox.SandBoxMissions.CreateSandBoxMissionInitializerRecord(sceneName, "", false, TaleWorlds.Engine.DecalAtlasGroup.All),
                  (m) => new TaleWorlds.MountAndBlade.MissionBehavior[]
                  {
                      new TaleWorlds.MountAndBlade.Source.Missions.MissionOptionsComponent(),
                      new WorkshopRendezvousMissionLogic(emissary)
                  });
            
            if (mission != null)
            {
                mission.AddMissionBehavior(SandBox.View.SandBoxViewCreator.CreateMissionConversationView(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionMainAgentEquipmentController(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionAgentStatusUIHandler(mission));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionSingleplayerEscapeMenu(false));
                mission.AddMissionBehavior(TaleWorlds.MountAndBlade.View.ViewCreator.CreateMissionLeaveView());
            }
        }
        
        public bool IsConfiscatedBy(Kingdom kingdom)
        {
            return _confiscatedFromUsBy.Contains(kingdom.StringId);
        }
    }
}
