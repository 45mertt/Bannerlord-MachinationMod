using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace RebellionsAndDemographics
{
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_settlement_wait_on_init")]
    public class WaitMenuCrashPatch
    {
        public static bool Prefix(MenuCallbackArgs args)
        {
            // Safely check if we have a valid encounter settlement
            bool hasEncounterSettlement = false;
            try 
            {
                if (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement != null)
                {
                    hasEncounterSettlement = true;
                }
            } 
            catch 
            { 
                hasEncounterSettlement = false; 
            }

            if (!hasEncounterSettlement)
            {
                if (Settlement.CurrentSettlement != null)
                {
                    if (PlayerEncounter.Current != null)
                    {
                        try { PlayerEncounter.Finish(true); } catch { }
                    }
                    PlayerEncounter.Start();
                    PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, Settlement.CurrentSettlement.Party);
                    return true;
                }
                else
                {
                    if (PlayerEncounter.Current != null)
                    {
                        try { PlayerEncounter.Finish(true); } catch { }
                    }
                    GameMenu.ExitToLast();
                    return false;
                }
            }
            return true;
        }
    }
}
