using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class KingdomAgendaWrapper
    {
        [SaveableField(1)]
        public List<PolicyObject> Agendas = new List<PolicyObject>();
    }

    public class AgendaPoolBehavior : CampaignBehaviorBase
    {
        private Dictionary<Kingdom, KingdomAgendaWrapper> _kingdomAgendas = new Dictionary<Kingdom, KingdomAgendaWrapper>();

        public Dictionary<Kingdom, KingdomAgendaWrapper> KingdomAgendas 
        { 
            get { return _kingdomAgendas ?? (_kingdomAgendas = new Dictionary<Kingdom, KingdomAgendaWrapper>()); }
            set { _kingdomAgendas = value; }
        }

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var dict = KingdomAgendas;
                var deadKeys = dict.Keys.Where(k => 
                    k == null || 
                    k.IsEliminated || 
                    string.IsNullOrEmpty(k.StringId) || 
                    !Kingdom.All.Contains(k)).ToList();

                foreach (var key in deadKeys)
                {
                    dict.Remove(key);
                }

                foreach (var wrapper in dict.Values)
                {
                    if (wrapper != null && wrapper.Agendas != null)
                        wrapper.Agendas.RemoveAll(x => x == null);
                }
            }

            dataStore.SyncData("_kingdomAgendas", ref _kingdomAgendas);

            if (dataStore.IsLoading)
            {
                if (_kingdomAgendas == null)
                {
                    _kingdomAgendas = new Dictionary<Kingdom, KingdomAgendaWrapper>();
                }
            }
        }

        public void AddPolicyToAgenda(Kingdom kingdom, PolicyObject policy)
        {
            if (!KingdomAgendas.ContainsKey(kingdom))
            {
                KingdomAgendas[kingdom] = new KingdomAgendaWrapper();
            }
            
            if (KingdomAgendas[kingdom].Agendas == null) KingdomAgendas[kingdom].Agendas = new List<PolicyObject>();
            
            if (!KingdomAgendas[kingdom].Agendas.Contains(policy))
            {
                KingdomAgendas[kingdom].Agendas.Add(policy);
            }
        }

        public void RemovePolicyFromAgenda(Kingdom kingdom, PolicyObject policy)
        {
            if (KingdomAgendas.ContainsKey(kingdom) && KingdomAgendas[kingdom].Agendas != null)
            {
                KingdomAgendas[kingdom].Agendas.Remove(policy);
            }
        }

        public List<PolicyObject> GetAgendaForKingdom(Kingdom kingdom)
        {
            if (KingdomAgendas.ContainsKey(kingdom) && KingdomAgendas[kingdom].Agendas != null)
            {
                return KingdomAgendas[kingdom].Agendas;
            }
            return new List<PolicyObject>();
        }
        
        public bool HasAgenda(Kingdom kingdom)
        {
            return KingdomAgendas.ContainsKey(kingdom) && KingdomAgendas[kingdom].Agendas != null && KingdomAgendas[kingdom].Agendas.Count > 0;
        }
    }
}
