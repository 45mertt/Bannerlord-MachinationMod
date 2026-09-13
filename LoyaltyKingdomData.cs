using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics
{
    public class LoyaltyKingdomData
    {
        [SaveableField(1)]
        private Dictionary<Clan, float> _clanLoyalty = new Dictionary<Clan, float>();

        public float GetLoyalty(Clan clan)
        {
            if (clan == null) return 0f;
            if ((clan.Kingdom != null && clan.Kingdom.RulingClan == clan)) return 1000f; // Rulers are always loyal
            
            if (_clanLoyalty.TryGetValue(clan, out float loyalty))
            {
                return loyalty;
            }
            return 0f;
        }

        public void AddLoyalty(Clan clan, float amount)
        {
            if (clan == null) return;
            if ((clan.Kingdom != null && clan.Kingdom.RulingClan == clan)) return;

            if (_clanLoyalty.ContainsKey(clan))
            {
                _clanLoyalty[clan] += amount;
            }
            else
            {
                _clanLoyalty[clan] = amount;
            }
        }

        public void SetLoyalty(Clan clan, float amount)
        {
            if (clan == null) return;
            if ((clan.Kingdom != null && clan.Kingdom.RulingClan == clan)) return;
            _clanLoyalty[clan] = amount;
        }

        internal void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_rad_clan_loyalty", ref _clanLoyalty);
        }
    }
}
