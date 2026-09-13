using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModunuzunNamespacei
{
    public class VendettaCampaignBehavior : CampaignBehaviorBase
    {
        // Hedeflenen lordu ve görevi verecek yoldaşı hafızada tutan değişkenler
        private Hero _targetLord;
        private Hero _questcompanion;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // İki karakter de Save dosyasına kaydedilmeli ki oyun açıp kapandığında hafıza silinmesin
            dataStore.SyncData("_targetLord", ref _targetLord);
            dataStore.SyncData("_questcompanion", ref _questcompanion);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 1. Oyuncunun yoldaşa yönelteceği soru
            starter.AddPlayerLine(
                "vendetta_quest_inquiry",
                "hero_main_options",
                "vendetta_companion_response",
                "{=vendetta_01}Aklını kurcalayan geçmiş meseleden bahsetmek ister misin?",
                VendettaDialogCondition, // Tüm kilit bu fonksiyonda
                null
            );

            // 2. Yoldaşın intikam hedefini anlattığı cevap
            starter.AddDialogLine(
                "vendetta_companion_talk",
                "vendetta_companion_response",
                "hero_main_options",
                "{=vendetta_02}Komutan, ailemi yok eden {TARGET_LORD} hala serbest geziyor. Onunla bir hesabım var!",
                null,
                null
            );
        }

        // Diyaloğun menüde görünüp görünmeyeceğini denetleyen ana koşul
        private bool VendettaDialogCondition()
        {
            // 1. Adım: Henüz kurban yoldaş seçilmemişse gruptan birini seç
            if (_questcompanion == null)
            {
                kurbanYoldas();
            }

            // Eğer klanda şarta uyan hiç kimse yoksa ve hala null ise diyaloğu açma
            if (_questcompanion == null)
            {
                return false;
            }

            // 2. Adım: Şu an konuştuğumuz kişi, seçilen kurban yoldaşın ta kendisi mi?
            if (Hero.OneToOneConversationHero == _questcompanion)
            {
                // 3. Adım: Hedef lord henüz seçilmediyse veya seçilen lord öldüyse yeni lord bul
                if (_targetLord == null || !_targetLord.IsAlive)
                {
                    _targetLord = FindSuitableEnemyLord(_questcompanion);
                }

                // 4. Adım: Uygun bir lord da başarıyla bulunduysa metni ayarla ve seçeneği göster
                if (_targetLord != null)
                {
                    MBTextManager.SetTextVariable("TARGET_LORD", _targetLord.Name);
                    return true;
                }
            }

            // Şartların hiçbiri tutmadıysa seçeneği gizle
            return false;
        }

        // Calradia'daki lordları tarayıp tek bir geçerli hedef döndüren yardımcı fonksiyon
        private Hero FindSuitableEnemyLord(Hero companion)
        {
            foreach (Hero lord in Hero.AllAliveHeroes)
            {
                if (lord.IsLord && !lord.IsPrisoner)
                {
                    if (lord.Clan != Clan.PlayerClan && lord.MapFaction != Hero.MainHero.MapFaction)
                    {
                        if (lord.GetTraitLevel(DefaultTraits.Honor) < 0 || lord.GetTraitLevel(DefaultTraits.Mercy) < 0)
                        {
                            return lord; // Uygun ilk hedefi bulup döndür
                        }
                    }
                }
            }
            return null;
        }

        // Oyuncunun klanından intikamcı yoldaşı seçen fonksiyon
        private void kurbanYoldas()
        {
            for (int i = 0; i < Clan.PlayerClan.Companions.Count; i++)
            {
                Hero secilenYoldas = Clan.PlayerClan.Companions[i];

                // Parantezlerle gruplamaya dikkat: (Onursuz VEYA Haydut) VE (İlişki > 20)
                if ((secilenYoldas.GetTraitLevel(DefaultTraits.Honor) < 0 || secilenYoldas.GetTraitLevel(DefaultTraits.Thug) >= 1) &&
                    secilenYoldas.GetRelation(Hero.MainHero) > 20)
                {
                    _questcompanion = secilenYoldas;
                    break; // Şartı sağlayan ilk kişiyi bulduğumuzda gereksiz yere diğerlerini taramamak için döngüyü kırıyoruz
                }
            }
        }
    }
}