using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace ClassLibrary22
{
    public class KingdomEmbargoBehavior : CampaignBehaviorBase
    {
        // Saldırgan Krallık -> Hedef Krallık listesi
        private List<EmbargoData> _activeEmbargoes = new List<EmbargoData>();

        public List<EmbargoData> ActiveEmbargoesList 
        { 
            get { return _activeEmbargoes ?? (_activeEmbargoes = new List<EmbargoData>()); }
            set { _activeEmbargoes = value; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public static List<EmbargoData> ActiveEmbargoes { get; private set; } = new List<EmbargoData>();

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var list = ActiveEmbargoesList;
                list.RemoveAll(x => x == null || x.Aggressor == null || x.Target == null || x.Aggressor.IsEliminated || x.Target.IsEliminated);
            }
            dataStore.SyncData("_activeEmbargoes", ref _activeEmbargoes);
            ActiveEmbargoes = ActiveEmbargoesList;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ActiveEmbargoes = ActiveEmbargoesList;
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Oyuncunun Kral ile diyalog başlangıcı
            starter.AddPlayerLine("player_propose_embargo", "hero_main_options", "player_embargo_proposal_target",
                "{=rad_auto_245}I want to discuss a commercial matter. We should embargo a kingdom.",
                () => Hero.OneToOneConversationHero != null && 
                      Hero.OneToOneConversationHero.IsFactionLeader && 
                      Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction,
                null);

            // Hedef krallığı seçme aşaması
            starter.AddDialogLine("king_ask_embargo_target", "player_embargo_proposal_target", "player_embargo_select_target",
                "{=rad_auto_243}Hmm, a trade embargo? Which kingdom do you suggest we close our borders against?",
                null, null);

            // Hedef seçenekleri
            starter.AddPlayerLine("player_embargo_target_1", "player_embargo_select_target", "king_embargo_response",
                "{=rad_auto_246}We must impose an embargo against the kingdom {TARGET_FACTION_NAME}.",
                () => 
                {
                    var validTargets = Kingdom.All.Where(k => k != Hero.MainHero.MapFaction).ToList();
                    if (validTargets.Count > 0)
                    {
                        var target = validTargets.GetRandomElement();
                        MBTextManager.SetTextVariable("TARGET_FACTION_NAME", target.Name);
                        TempEmbargoTarget = target;
                        return true;
                    }
                    return false;
                }, null);

            starter.AddPlayerLine("player_embargo_target_cancel", "player_embargo_select_target", "hero_main_options",
                "{=rad_auto_247}Never mind, now is not the time.", null, null);

            starter.AddDialogLine("king_embargo_response", "king_embargo_response", "hero_main_options",
                "{=rad_auto_244}This would be a very drastic move. However, we can implement this if you can convince the council using your Influence (300 Influence).",
                null, null);

            starter.AddPlayerLine("player_embargo_confirm", "king_embargo_response", "hero_main_options",
                "{=rad_auto_248}I will use my influence and make this decision. (Spend 300 Influence)",
                () => Clan.PlayerClan.Influence >= 300 && TempEmbargoTarget != null,
                () => 
                {
                    ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -300);
                    StartEmbargo(Hero.MainHero.MapFaction as Kingdom, TempEmbargoTarget);
                    InformationManager.DisplayMessage(new InformationMessage($"{Hero.MainHero.MapFaction.Name} has launched an embargo on the kingdom {TempEmbargoTarget.Name}!", Colors.Red));
                });
            
            starter.AddPlayerLine("player_embargo_reject", "king_embargo_response", "hero_main_options",
                "{=rad_auto_249}I gave up, we can't pay this much.", null, null);
        }

        public static Kingdom TempEmbargoTarget = null;

        private void StartEmbargo(Kingdom aggressor, Kingdom target)
        {
            if (!_activeEmbargoes.Any(e => e.Aggressor == aggressor && e.Target == target))
            {
                _activeEmbargoes.Add(new EmbargoData(aggressor, target, CampaignTime.Now));
            }
        }

        private void OnDailyTick()
        {
            foreach (var embargo in _activeEmbargoes.ToList())
            {
                if (embargo.Aggressor.IsEliminated || embargo.Target.IsEliminated)
                {
                    _activeEmbargoes.Remove(embargo);
                    continue;
                }

                // Makro Cezalar
                float aggressorProsperity = embargo.Aggressor.Settlements.Where(s => s.IsTown).Sum(s => s.Town.Prosperity);
                float targetProsperity = embargo.Target.Settlements.Where(s => s.IsTown).Sum(s => s.Town.Prosperity);

                float powerRatio = 1.0f;
                if (targetProsperity > 0)
                {
                    powerRatio = aggressorProsperity / targetProsperity;
                }
                
                powerRatio = MathF.Clamp(powerRatio, 0.3f, 3.0f);

                // Hedef şehirleri yıprat
                foreach (var settlement in embargo.Target.Settlements.Where(s => s.IsTown))
                {
                    float prosLoss = MBRandom.RandomFloatRanged(5f, 20f) * powerRatio;
                    settlement.Town.Prosperity -= prosLoss;

                    float secLoss = MBRandom.RandomFloatRanged(0.5f, 2.0f) * powerRatio;
                    settlement.Town.Security -= secLoss;

                    float loyLoss = MBRandom.RandomFloatRanged(0.5f, 1.5f) * powerRatio;
                    settlement.Town.Loyalty -= loyLoss;

                    if (settlement.Town.Prosperity < 0) settlement.Town.Prosperity = 0;
                    if (settlement.Town.Security < 0) settlement.Town.Security = 0;
                    if (settlement.Town.Loyalty < 0) settlement.Town.Loyalty = 0;
                }

                // Ters Tepki
                foreach (var settlement in embargo.Aggressor.Settlements.Where(s => s.IsTown))
                {
                    float backfireProsLoss = MBRandom.RandomFloatRanged(5f, 20f) * powerRatio * 0.25f;
                    float backfireSecLoss = MBRandom.RandomFloatRanged(0.5f, 2.0f) * powerRatio * 0.25f;

                    settlement.Town.Prosperity -= backfireProsLoss;
                    settlement.Town.Security -= backfireSecLoss;
                }

                if (MBRandom.RandomFloat < 0.05f)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Due to the embargo of {embargo.Aggressor.Name}, famine and insecurity are occurring in the cities of {embargo.Target.Name}!", Colors.Magenta));
                }
            }
        }
    }

    public class EmbargoData
    {
        public EmbargoData() {}
        [SaveableField(1)] public Kingdom Aggressor;
        [SaveableField(2)] public Kingdom Target;
        [SaveableField(3)] public CampaignTime StartTime;

        public EmbargoData(Kingdom aggressor, Kingdom target, CampaignTime startTime)
        {
            Aggressor = aggressor;
            Target = target;
            StartTime = startTime;
        }
    }
}
