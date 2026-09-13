using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class LordLocationDialogBehavior : CampaignBehaviorBase
    {
        private static List<IFaction> _factionList = new List<IFaction>();
        private static int _factionPage = 0;

        private static List<Hero> _heroList = new List<Hero>();
        private static int _heroPage = 0;
        private static Hero _selectedHero = null;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("ask_location_start", "hero_main_options", "ask_location_faction",
                "{=rad_loc_01}I need to know the whereabouts of someone.",
                () => {
                    if (Hero.OneToOneConversationHero == null) return false;
                    if (Hero.OneToOneConversationHero.IsPrisoner) return false; 
                    if (Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction != null && 
                        Hero.OneToOneConversationHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) return false; 
                    return true;
                },
                () => {
                    _factionList.Clear();
                    foreach (var k in Kingdom.All)
                    {
                        if (!k.IsEliminated) _factionList.Add(k);
                    }
                    // _factionList = _factionList.OrderBy(f => f.Name.ToString()).ToList(); // FIXED: Reassigning collections during Save causes drift
                    _factionPage = 0;
                }, 100);

            starter.AddDialogLine("ask_location_faction", "ask_location_faction", "ask_location_faction_select",
                "{=rad_loc_02}Certainly. Whose faction does this person belong to?",
                null, null);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                starter.AddPlayerLine("ask_location_faction_" + index, "ask_location_faction_select", "ask_location_hero",
                    "{FACTION_NAME_" + index + "}",
                    () => {
                        int pos = _factionPage * 4 + index;
                        if (pos < _factionList.Count)
                        {
                            MBTextManager.SetTextVariable("FACTION_NAME_" + index, _factionList[pos].Name);
                            return true;
                        }
                        return false;
                    },
                    () => {
                        int pos = _factionPage * 4 + index;
                        var selectedFaction = _factionList[pos];
                        _heroList.Clear();
                        
                        foreach (var hero in Hero.AllAliveHeroes)
                        {
                            if (hero.IsLord && hero.MapFaction == selectedFaction)
                            {
                                _heroList.Add(hero);
                            }
                        }
                        // _heroList = _heroList.OrderBy(h => h.Name.ToString()).ToList(); // FIXED: Reassigning collections during Save causes drift
                        _heroPage = 0;
                    });
            }

            starter.AddPlayerLine("ask_location_faction_next", "ask_location_faction_select", "ask_location_faction_next_npc",
                "{=rad_loc_03}Show me other factions...",
                () => _factionList.Count > (_factionPage + 1) * 4,
                () => { _factionPage++; });

            starter.AddDialogLine("ask_location_faction_next_npc", "ask_location_faction_next_npc", "ask_location_faction_select",
                "{=rad_loc_04}Who else?", null, null);

            starter.AddPlayerLine("ask_location_faction_cancel", "ask_location_faction_select", "lord_pretalk",
                "{=rad_loc_05}Never mind.", null, null);

            starter.AddDialogLine("ask_location_hero", "ask_location_hero", "ask_location_hero_select",
                "{=rad_loc_06}Who exactly are you looking for?", null, null);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                starter.AddPlayerLine("ask_location_hero_" + index, "ask_location_hero_select", "ask_location_result",
                    "{HERO_NAME_" + index + "}",
                    () => {
                        int pos = _heroPage * 4 + index;
                        if (pos < _heroList.Count)
                        {
                            MBTextManager.SetTextVariable("HERO_NAME_" + index, _heroList[pos].Name);
                            return true;
                        }
                        return false;
                    },
                    () => {
                        int pos = _heroPage * 4 + index;
                        _selectedHero = _heroList[pos];
                    });
            }

            starter.AddPlayerLine("ask_location_hero_next", "ask_location_hero_select", "ask_location_hero_next_npc",
                "{=rad_loc_03}Show me other lords...",
                () => _heroList.Count > (_heroPage + 1) * 4,
                () => { _heroPage++; });

            starter.AddDialogLine("ask_location_hero_next_npc", "ask_location_hero_next_npc", "ask_location_hero_select",
                "{=rad_loc_04}Who else?", null, null);

            starter.AddPlayerLine("ask_location_hero_cancel", "ask_location_hero_select", "ask_location_faction",
                "{=rad_loc_07}Wait, let me choose the faction again.", null, null);

            starter.AddDialogLine("ask_location_result", "ask_location_result", "lord_pretalk",
                "{LOCATION_RESULT_TEXT}",
                () => {
                    if (_selectedHero == null) return false;
                    string resultText = GetHeroLocationString(_selectedHero);
                    MBTextManager.SetTextVariable("LOCATION_RESULT_TEXT", resultText);
                    return true;
                }, null);
        }

        private string GetHeroLocationString(Hero hero)
        {
            TextObject t;
            if (hero.IsDead)
            {
                t = new TextObject("{=rad_loc_res_dead}I heard {HERO_NAME} passed away, may he rest in peace.");
            }
            else if (hero.IsPrisoner)
            {
                if (hero.PartyBelongedToAsPrisoner != null)
                {
                    if (hero.PartyBelongedToAsPrisoner.Settlement != null)
                    {
                        t = new TextObject("{=rad_loc_res_pris_set}He is currently held captive in {SETTLEMENT}.");
                        t.SetTextVariable("SETTLEMENT", hero.PartyBelongedToAsPrisoner.Settlement.Name);
                    }
                    else if (hero.PartyBelongedToAsPrisoner.MobileParty != null)
                    {
                        t = new TextObject("{=rad_loc_res_pris_party}He is a prisoner of {PARTY}.");
                        t.SetTextVariable("PARTY", hero.PartyBelongedToAsPrisoner.MobileParty.Name);
                    }
                    else
                    {
                        t = new TextObject("{=rad_loc_res_pris_unk}He is a prisoner somewhere.");
                    }
                }
                else
                {
                    t = new TextObject("{=rad_loc_res_pris_unk}He is a prisoner somewhere.");
                }
            }
            else if (hero.CurrentSettlement != null)
            {
                t = new TextObject("{=rad_loc_res_set}I received word that he is currently at {SETTLEMENT}.");
                t.SetTextVariable("SETTLEMENT", hero.CurrentSettlement.Name);
            }
            else if (hero.PartyBelongedTo != null)
            {
                var closest = Settlement.All.Where(s => s.IsTown || s.IsVillage || s.IsCastle)
                                .OrderBy(s => s.GatePosition.DistanceSquared(new TaleWorlds.Library.Vec2(hero.PartyBelongedTo.Position.X, hero.PartyBelongedTo.Position.Y)))
                                .FirstOrDefault();
                if (closest != null)
                {
                    t = new TextObject("{=rad_loc_res_near}He was last seen in the vicinity of {SETTLEMENT}.");
                    t.SetTextVariable("SETTLEMENT", closest.Name);
                }
                else
                {
                    t = new TextObject("{=rad_loc_res_unk}I have no idea where he is right now.");
                }
            }
            else
            {
                t = new TextObject("{=rad_loc_res_unk}I have no idea where he is right now.");
            }
            
            t.SetTextVariable("HERO_NAME", hero.Name);
            return t.ToString();
        }
    }
}
