using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    public class EmpireTimelineBehavior : CampaignBehaviorBase
    {
        private bool _schismTriggered = false;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_pendraicSchismTriggered", ref _schismTriggered);
        }

        private void OnTick(float dt)
        {
            if (!ModSettings.EnableNeretzesFolly) return;
            // CHEAT: Ctrl + Shift + L (Legacy) OR Ctrl + Shift + V (New)
            if (Input.IsKeyDown(InputKey.LeftControl) &&
                Input.IsKeyDown(InputKey.LeftShift) &&
                (Input.IsKeyPressed(InputKey.L) || Input.IsKeyPressed(InputKey.V)))
            {
                InformationManager.DisplayMessage(new InformationMessage("::: SCHISM DEBUG TRIGGERED :::", Colors.Red));
                TriggerEmpireSchism(true); // FORCE Trigger
            }
        }

        private void OnDailyTick()
        {
            if (!ModSettings.EnableNeretzesFolly) return;
            if (!_schismTriggered && CampaignTime.Now.GetYear >= 1082)
            {
                // Fix: Artık "empire_w" krallığının ismi "Calradic" ise bölünecek.
                Kingdom empireW = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
                // Fix: Artık "calradic_empire" krallığının varlığına bakıyoruz.
                Kingdom calradia = Kingdom.All.FirstOrDefault(k => k.StringId == "calradic_empire");
                if (calradia != null && !calradia.IsEliminated)
                {
                    TriggerEmpireSchism();
                }
                else
                {
                   if (CampaignTime.Now.GetYear > 1085) _schismTriggered = true; 
                }
            }
        }

        private void TriggerEmpireSchism(bool force = false)
        {
            if (_schismTriggered && !force) return;
            _schismTriggered = true;

            try
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_71}::: 1082: THE EMPIRE IS SPLITTING :::").ToString(), Colors.Red));

                // 1. ARENICOS'U ÖLDÜR
                Hero arenicos = ArenicosCutsceneLogic.GetTrueArenicos();
                if (arenicos != null && arenicos.IsAlive)
                {
                    KillCharacterAction.ApplyByMurder(arenicos, null, true);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_72}Emperor Arenicos fell victim to an assassination!").ToString(), Colors.Red));
                }

                // 2. TÜM SAVAŞLARI BİTİR (Barış)
                foreach (var kingdom in Kingdom.All)
                {
                    foreach (var enemy in Kingdom.All)
                    {
                        if (kingdom != enemy && kingdom.IsAtWarWith(enemy))
                        {
                            MakePeaceAction.Apply(kingdom, enemy);
                        }
                    }
                }

                // 3. İMPARATORLUĞU BÖL
                Kingdom calradia = Kingdom.All.FirstOrDefault(k => k.StringId == "calradic_empire");
                Kingdom empireW = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_w");
                Kingdom empireN = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_n");
                Kingdom empireS = Kingdom.All.FirstOrDefault(k => k.StringId == "empire_s");

                // --- RESURRECTION (DİRİLTME) ---
                // Hepsi geri geliyor
                if (empireN != null && empireN.IsEliminated) ResurrectKingdom(empireN);
                if (empireS != null && empireS.IsEliminated) ResurrectKingdom(empireS);
                if (empireW != null && empireW.IsEliminated) ResurrectKingdom(empireW);
                // -------------------------------

                if (empireW != null && empireN != null && empireS != null)
                {
                    // Batı'nın ismini düzelt (Eğer bozulduysa)
                    TextObject westName = new TextObject("{=empire_w_name}Western Empire");
                    empireW.InitializeKingdom(
                        westName, 
                        westName, 
                        empireW.Culture, 
                        empireW.Banner, 
                        empireW.Color, 
                        empireW.Color2, 
                        empireW.Settlements.FirstOrDefault(), 
                        westName, 
                        westName, 
                        westName
                    );

                    // Liderleri Ata (Garios, Lucon, Rhagaea)
                    Hero garios = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Garios"));
                    Hero lucon = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Lucon"));
                    Hero rhagaea = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Contains("Rhagaea"));

                    // Garios -> Batı
                    if (garios != null)
                    {
                        if (garios.Clan.Kingdom != empireW) ChangeKingdomAction.ApplyByJoinToKingdom(garios.Clan, empireW);
                        empireW.RulingClan = garios.Clan;
                    }

                    // Lucon -> Kuzey
                    if (lucon != null)
                    {
                        if (lucon.Clan.Kingdom != empireN) ChangeKingdomAction.ApplyByJoinToKingdom(lucon.Clan, empireN);
                        empireN.RulingClan = lucon.Clan;
                    }

                    // Rhagaea -> Güney
                    if (rhagaea != null)
                    {
                        if (rhagaea.Clan.Kingdom != empireS) ChangeKingdomAction.ApplyByJoinToKingdom(rhagaea.Clan, empireS);
                        empireS.RulingClan = rhagaea.Clan;
                    }

                    // Toprakları ve Klanları Dağıt (Calradia -> N/S/W)
                    DistributeLandsAndClans(empireN, empireS, empireW, calradia);
                }

                // 4. CALRADIC EMPIRE'I YOK ET
                if (calradia != null)
                {
                    DestroyKingdomAction.Apply(calradia);
                }

                // 5. OYUNCU SEÇİMİ (KOSULLU)
                // Oyuncu Calradia'daysa veya Empire kültürüyse
                bool playerIsEmpire = (Hero.MainHero.MapFaction == calradia) || (Hero.MainHero.MapFaction != null && Hero.MainHero.MapFaction.Culture.StringId == "empire");
                
                if (playerIsEmpire)
                {
                     InformationManager.ShowInquiry(new InquiryData(
     new TextObject("{=rad_085_73}The Empire Shattered").ToString(),
     new TextObject("{=rad_085_74}Arenicos is dead. The Empire is split into three. Under which banner will you fight?").ToString(),
     true, true,
     new TextObject("{=rad_085_75}North (Senate)").ToString(),
     new TextObject("{=rad_085_76}Others...").ToString(),
                         () => JoinFaction(empireN),
                        () => 
                        {
                            InformationManager.ShowInquiry(new InquiryData(
    new TextObject("{=rad_085_77}Choose a Side").ToString(),
    new TextObject("{=rad_085_78}Who will you support?").ToString(),
    true, true,
    new TextObject("{=rad_085_79}South (Rhagaea)").ToString(),
    new TextObject("{=rad_085_80}West (Garios)").ToString(),
                                () => JoinFaction(empireS),
                                () => JoinFaction(empireW),
                                "", 0f, null, null, null), true);
                        },
                        "", 0f, null, null, null), true);
                }

                // 6. YENİ SAVAŞLARI BAŞLAT
                DeclareSpecificWars(empireN, empireS, empireW);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Schism Error:" + ex.Message, Colors.Red));
            }
        }

        private void JoinFaction(Kingdom target)
        {
            if (target != null)
            {
                if (Hero.MainHero.Clan.Kingdom != target)
                    ChangeKingdomAction.ApplyByJoinToKingdom(Hero.MainHero.Clan, target);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_81}You have joined the ranks of {FACTION}!").SetTextVariable("FACTION", target.Name).ToString(), Colors.Green));
            }
        }

        private void DeclareSpecificWars(Kingdom n, Kingdom s, Kingdom w)
        {
            // 1. İÇ SAVAŞ
            DeclareWar(n, s);
            DeclareWar(s, w);
            DeclareWar(w, n);

            // 2. DIŞ SAVAŞLAR
            Kingdom khuzait = Kingdom.All.FirstOrDefault(k => k.StringId == "khuzait");
            Kingdom aserai = Kingdom.All.FirstOrDefault(k => k.StringId == "aserai");
            Kingdom battania = Kingdom.All.FirstOrDefault(k => k.StringId == "battania");
            Kingdom vlandia = Kingdom.All.FirstOrDefault(k => k.StringId == "vlandia");
            Kingdom sturgia = Kingdom.All.FirstOrDefault(k => k.StringId == "sturgia");

            // Kuzey vs Khuzait
            if (n != null && khuzait != null) DeclareWar(n, khuzait);

            // Güney vs Aserai
            if (s != null && aserai != null) DeclareWar(s, aserai);

            // Batı vs Battania
            if (w != null && battania != null) DeclareWar(w, battania);

            // Vlandia vs Sturgia
            if (vlandia != null && sturgia != null) DeclareWar(vlandia, sturgia);
        }

        private void DeclareWar(Kingdom k1, Kingdom k2)
        {
            if (k1 != null && k2 != null && !k1.IsAtWarWith(k2))
            {
                DeclareWarAction.ApplyByDefault(k1, k2);
            }
        }

        private void DistributeLandsAndClans(Kingdom north, Kingdom south, Kingdom west, Kingdom source)
        {
            // Şehir Listeleri (Hardcoded Coğrafya)
            List<string> northFiefs = new List<string> { "Epicrotea", "Diathma", "Saneopa", "Amprela", "Myzea", "Argon", "Gaos", "Mestricaro", "Cania", "Espinosa", "Ataconia" };
            List<string> southFiefs = new List<string> { "Lycaron", "Onira", "Danustica", "Vostrum", "Phycaon", "Poros", "Syratos", "Odrissia", "Coresia", "Morenia", "Melion" };
            List<string> westFiefs = new List<string> { "Zeonica", "Jalmarys", "Rhotae", "Lageta", "Amitlys", "Ortysia", "Thorios", "Garontor", "Thractora", "Hertelion", "Veron" };

            // 1. Yerleşimleri Dağıt (Zaten sahiplik değişmiyor ama emin olmak için)
            // Eğer source (Calradia) hepsine sahipse, sahiplikleri değiştir
             foreach (var settlement in Settlement.All)
            {
                if (settlement.MapFaction == source)
                {
                    Kingdom target = null;
                    if (northFiefs.Contains(settlement.Name.ToString())) target = north;
                    else if (southFiefs.Contains(settlement.Name.ToString())) target = south;
                    else if (westFiefs.Contains(settlement.Name.ToString())) target = west;
                    else target = west; // Varsayılan

                    if (target != null && settlement.MapFaction != target)
                    {
                        ChangeOwnerOfSettlementAction.ApplyByGift(settlement, target.Leader);
                    }
                }
            }

            // 2. Klanları Dağıt
            // Ruling Clan'lar hariç
            var imperialClans = Clan.All.Where(c => c.MapFaction == source && !c.IsEliminated && !c.IsBanditFaction && !c.IsMinorFaction && c != north.RulingClan && c != south.RulingClan && c != west.RulingClan).ToList();

            foreach (var clan in imperialClans)
            {
                int nScore = 0, sScore = 0, wScore = 0;
                foreach (var s in clan.Settlements)
                {
                    if (northFiefs.Contains(s.Name.ToString())) nScore++;
                    if (southFiefs.Contains(s.Name.ToString())) sScore++;
                    if (westFiefs.Contains(s.Name.ToString())) wScore++;
                }

                Kingdom finalTarget = west; // Varsayılan
                if (nScore > sScore && nScore > wScore) finalTarget = north;
                else if (sScore > nScore && sScore > wScore) finalTarget = south;
                else if (wScore > nScore && wScore > sScore) finalTarget = west;
                
                if (clan.Kingdom != finalTarget)
                {
                     ChangeKingdomAction.ApplyByJoinToKingdom(clan, finalTarget);
                }
            }
        }

        private void ResurrectKingdom(Kingdom k)
        {
            try
            {
                // Reflection ile _isEliminated field'ını false yap
                var field = typeof(Kingdom).GetField("_isEliminated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(k, false);
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_085_82}{KINGDOM} is rising again!").SetTextVariable("KINGDOM", k.Name).ToString(), Colors.Green));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Resurrection Fail:" + ex.Message, Colors.Red));
            }
        }
    }
}
