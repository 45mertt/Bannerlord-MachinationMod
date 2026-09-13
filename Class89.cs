using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModTemplate.Behaviors
{
    public class CustomDialogTutorialBehavior : CampaignBehaviorBase
    {
        // Oyuncunun daha önce borç teklifi yapıp yapmadığını tutan örnek kayıt değişkeni
        private bool _hasOfferedBribeBefore = false;
        private const int BRIBE_AMOUNT = 1500;

        public override void RegisterEvents()
        {
            // Diyalogları oyuna CampaignGameStarter aracılığıyla tanıtmak için OnSessionLaunched dinlenir
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Değişkenlerin save dosyasına kaydedilip geri yüklenmesi
            dataStore.SyncData("_hasOfferedBribeBefore", ref _hasOfferedBribeBefore);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddClassicManualDialogs(starter);
            AddFluentDialogFlow(starter);
        }

        #region 1. Klasik Manuel Yöntem (AddDialogLine & AddPlayerLine)

        private void AddClassicManualDialogs(CampaignGameStarter starter)
        {
            // -------------------------------------------------------------
            // ADIM 1: Standart Lord Menüsüne (hero_main_options) Yeni Seçenek Ekleme
            // -------------------------------------------------------------
            starter.AddPlayerLine(
                id: "custom_lord_special_request_option",
                inputToken: "hero_main_options",
                outputToken: "custom_lord_response_state",
                text: "{=mod_dial_01}Seninle özel bir mesele hakkında görüşmek istiyorum.",
                conditionDelegate: CustomLordRequestCondition,
                consequenceDelegate: null,
                priority: 105 // Standart seçeneklerin önüne geçmesi için öncelik
            );

            // -------------------------------------------------------------
            // ADIM 2: NPC'nin Yanıtı (Dinamik TextObject Değişkeni Kullanımı)
            // -------------------------------------------------------------
            starter.AddDialogLine(
                id: "custom_lord_response_line",
                inputToken: "custom_lord_response_state",
                outputToken: "custom_player_decision_state",
                text: "{=mod_dial_02}Dinliyorum {PLAYER_NAME}. {SETTLEMENT_NAME} topraklarında huzurumu bozmayacak bir teklifin vardır umarım?",
                conditionDelegate: CustomLordGreetingCondition,
                consequenceDelegate: null,
                priority: 100
            );

            // -------------------------------------------------------------
            // ADIM 3: Oyuncu Karar Ağacı (Player Options)
            // -------------------------------------------------------------

            // Seçenek A: Rüşvet / Hediye Teklifi (Şartlı Seçenek: Parası yetiyor mu?)
            starter.AddPlayerLine(
                id: "custom_player_give_bribe",
                inputToken: "custom_player_decision_state",
                outputToken: "custom_lord_accepts_bribe",
                text: "{=mod_dial_03}Dostluğumuzun pekişmesi için sana {BRIBE_GOLD} dinar takdim etmek isterim.",
                conditionDelegate: CustomPlayerHasGoldCondition,
                consequenceDelegate: CustomPlayerGivesBribeConsequence,
                priority: 100
            );

            // Seçenek B: Tehdit Etme (Savaş / İlişki Düşüşü Sonucu)
            starter.AddPlayerLine(
                id: "custom_player_threaten",
                inputToken: "custom_player_decision_state",
                outputToken: "custom_lord_angry_reply",
                text: "{=mod_dial_04}Bana zorluk çıkarırsan kılıcım konuşur!",
                conditionDelegate: null,
                consequenceDelegate: null,
                priority: 100
            );

            // Seçenek C: Geri Dönüş (Ana Menüye Dön)
            starter.AddPlayerLine(
                id: "custom_player_cancel_to_main",
                inputToken: "custom_player_decision_state",
                outputToken: "hero_main_options",
                text: "{=mod_dial_05}Şimdilik önemli değil, başka bir konuya geçelim.",
                conditionDelegate: null,
                consequenceDelegate: null,
                priority: 100
            );

            // -------------------------------------------------------------
            // ADIM 4: NPC Yanıtları ve Kapanış Durumları
            // -------------------------------------------------------------

            // Rüşvet sonrası NPC memnuniyeti -> Pencereyi Kapat
            starter.AddDialogLine(
                id: "custom_lord_accepts_bribe_reply",
                inputToken: "custom_lord_accepts_bribe",
                outputToken: "close_window",
                text: "{=mod_dial_06}Cömertliğin gözümden kaçmadı. Bu desteğini unutmayacağım.",
                conditionDelegate: null,
                consequenceDelegate: null,
                priority: 100
            );

            // Tehdit sonrası NPC öfkesi -> İlişki düşer ve menü kapanır
            starter.AddDialogLine(
                id: "custom_lord_angry_reply_line",
                inputToken: "custom_lord_angry_reply",
                outputToken: "close_window",
                text: "{=mod_dial_07}Cüretine hayret ediyorum! Bu küstahlığın bedelini ödeyeceksin!",
                conditionDelegate: null,
                consequenceDelegate: CustomLordThreatenedConsequence,
                priority: 100
            );
        }

        #region Condition ve Consequence Fonksiyonları

        private bool CustomLordRequestCondition()
        {
            // Konuşulan karakter bir Lord ve hayatta mı?
            Hero conversationHero = Hero.OneToOneConversationHero;
            return conversationHero != null && conversationHero.IsLord && conversationHero.IsAlive;
        }

        private bool CustomLordGreetingCondition()
        {
            // Dinamik TextObject değişkenlerini besleme
            MBTextManager.SetTextVariable("PLAYER_NAME", Hero.MainHero.Name);

            Settlement currentSettlement = Settlement.CurrentSettlement ?? Hero.OneToOneConversationHero?.HomeSettlement;
            MBTextManager.SetTextVariable("SETTLEMENT_NAME", currentSettlement != null ? currentSettlement.Name : new TextObject("{=mod_dial_def}bu"));

            return true;
        }

        private bool CustomPlayerHasGoldCondition()
        {
            // Oyuncunun parası kontrol edilir ve dinamik para miktarı metne yazılır
            MBTextManager.SetTextVariable("BRIBE_GOLD", BRIBE_AMOUNT);
            return Hero.MainHero.Gold >= BRIBE_AMOUNT;
        }

        private void CustomPlayerGivesBribeConsequence()
        {
            // Para transferi ve ilişki artışı
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, BRIBE_AMOUNT);
            ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, 10);

            _hasOfferedBribeBefore = true;
            InformationManager.DisplayMessage(new InformationMessage("İlişkiniz 10 puan arttı.", Colors.Green));
        }

        private void CustomLordThreatenedConsequence()
        {
            // İlişki düşüşü
            ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, -15);
            InformationManager.DisplayMessage(new InformationMessage("İlişkiniz 15 puan azaldı.", Colors.Red));
        }

        #endregion

        #endregion

        #region 2. Fluent DialogFlow Yöntemi (Bağımsız Karşılaşma Ağacı)

        private void AddFluentDialogFlow(CampaignGameStarter starter)
        {
            // Haritada bir Haydut Lideri ile karşılaşıldığında devreye giren yüksek öncelikli (120) akış
            starter.AddDialogFlow(
                DialogFlow.CreateDialogFlow("start", 120)
                    // 1. NPC Başlangıç Cümlesi
                    .NpcLine("{=mod_df_01}Dur bakalım yolcu! Bu topraklardan geçişin bir bedeli var.")
                        .Condition(BanditEncounterCondition)

                    // 2. Oyuncu Seçenekleri Başlangıcı
                    .BeginPlayerOptions()
                        // Seçenek 1: Parayı Ver ve Kurtul
                        .PlayerOption("{=mod_df_02}Sorun istemiyorum, alın şu 500 dinarı ve yoluma gideyim.")
                            .Condition(() => Hero.MainHero.Gold >= 500)
                            .Consequence(() =>
                            {
                                GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, MobileParty.MainParty.Party, 500);
                            })
                            .NpcLine("{=mod_df_03}Akıllıca bir seçim. Yolun açık olsun.")
                            .CloseDialog()

                        // Seçenek 2: Savaşı Başlat
                        .PlayerOption("{=mod_df_04}Size verecek tek bir kuruşum bile yok. Kılıçlarınızı çekin!")
                            .Consequence(() =>
                            {
                                PlayerEncounter.LeaveEncounter = false;
                            })
                            .NpcLine("{=mod_df_05}Öyleyse cesedinden alacağız!")
                            .CloseDialog()
                    .EndPlayerOptions()
            );
        }

        private bool BanditEncounterCondition()
        {
            CharacterObject character = CharacterObject.OneToOneConversationCharacter;
            return character != null && character.Occupation == Occupation.Bandit;
        }

        #endregion
    }
}