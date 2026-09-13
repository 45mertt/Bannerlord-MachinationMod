using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System.Collections.Generic;

namespace RebellionsAndDemographics
{
    // 1. Kısım: Ayarların Tanımlandığı Sınıf (MCM)
    public class RebellionSettings : AttributeGlobalSettings<RebellionSettings>
    {
        public override string Id => "RebellionsAndDemographics_v1";
        public override string DisplayName => "Rebellions & Demographics";
        public override string FolderName => "RebellionsAndDemographics";
        public override string FormatType => "json";

        // --- AYARLAR ---

        [SettingPropertyBool("{=set_ai_rebel_title}Enable AI Rebellions", Order = 1, RequireRestart = false, HintText = "{=set_ai_rebel_hint}If enabled, AI clans can start rebellions on their own based on stress levels.")]
        [SettingPropertyGroup("{=set_group_general}General Settings")]
        public bool EnableAIRebellions { get; set; } = false;

        [SettingPropertyBool("{=set_rebel_banner_title}Change Rebel Banners", Order = 2, RequireRestart = false, HintText = "{=set_rebel_banner_hint}If enabled, rebel clans will invert their banner colors. Disable this to keep custom banners intact.")]
        [SettingPropertyGroup("{=set_group_general}General Settings")]
        public bool ChangeRebelBannerColors { get; set; } = true;

        // --- EKLENEN KISIM: HİKAYE VE ZAMAN AYARLARI ---
        [SettingPropertyBool("Neretzes's Folly", Order = 3, RequireRestart = true, HintText = "Activates the Battle of Pendraic flashbacks, the unification of the Empire, and the great schism scenario in 1082.")]
        [SettingPropertyGroup("Story & Scenario")]
        public bool EnableNeretzesFolly { get; set; } = false;

        [SettingPropertyBool("Enable Scavenger Events", Order = 4, RequireRestart = false, HintText = "If enabled, looters and loot boxes may appear in battlefields after large battles.")]
        [SettingPropertyGroup("{=set_group_general}General Settings")]
        public bool EnableScavengerEvents { get; set; } = false;

        [SettingPropertyFloatingInteger("Workshop Revenue Multiplier", 0.1f, 5.0f, Order = 5, RequireRestart = false, HintText = "Multiplies the daily income from workshops. 1.0 = Normal, 2.0 = 2x Revenue, 0.5 = Half Revenue.")]
        [SettingPropertyGroup("Workshop Settings")]
        public float WorkshopIncomeMultiplier { get; set; } = 1.0f;

        // --- EKLENEN KISIM: DEBUG MOD ---
        [SettingPropertyBool("{=set_debug_title}Enable Debug Mode", Order = 99, RequireRestart = false, HintText = "{=set_debug_hint}Enables developer options and cheat menus to modify population data manually. Keep disabled for normal gameplay.")]
        [SettingPropertyGroup("{=set_group_debug}Debug & Cheat")]
        public bool EnableDebugMode { get; set; } = false;
    }

    // 2. Kısım: Kod içinde ayarlara ulaşmak için hızlı erişim Sınıfı
    public static class ModSettings
    {
        public static bool EnableAIRebellions
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.EnableAIRebellions;
                }
                return false;
            }
        }

        public static bool ChangeRebelBannerColors
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.ChangeRebelBannerColors;
                }
                return true; // Varsayılan olarak açık
            }
        }

        // Yeni eklenen Hikaye ayarı hızlı erişimi
        public static bool EnableNeretzesFolly
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.EnableNeretzesFolly;
                }
                return false; // Varsayılan kapalı
            }
        }

        // Yeni eklenen Tarih ayarı hızlı erişimi
        public static bool EnableScavengerEvents
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.EnableScavengerEvents;
                }
                return false;
            }
        }

        public static float WorkshopIncomeMultiplier
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.WorkshopIncomeMultiplier;
                }
                return 1.0f;
            }
        }

        public static bool EnableWarPeacePopup
        {
            get => true;
        }

        public static bool EnableDecisionPopup
        {
            get => true;
        }

        public static bool EnableRansomPopup
        {
            get => true;
        }

        public static bool EnableFamilyEventPopup
        {
            get => true;
        }

        public static bool OnlyPlayerKingdomEvents
        {
            get => true;
        }

        // Yeni eklenen Debug ayarı hızlı erişimi
        public static bool EnableDebugMode
        {
            get
            {
                if (RebellionSettings.Instance != null)
                {
                    return RebellionSettings.Instance.EnableDebugMode;
                }
                return false; // Varsayılan kapalı
            }
        }
    }
}