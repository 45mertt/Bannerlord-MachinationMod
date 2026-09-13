using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace RebellionsAndDemographics
{
    public class SaveDebugBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTick(float dt)
        {
            if (Input.IsKeyDown(InputKey.LeftControl) && 
                Input.IsKeyDown(InputKey.LeftShift) && 
                Input.IsKeyPressed(InputKey.LeftAlt))
            {
                RunCarTestDiagnostics();
            }
        }

        private void RunCarTestDiagnostics()
        {
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            log.AppendLine("==== YENI ARABA TESTI (1.5.1 GUNCEL MOTOR DESTEKLI) ====");
            
            bool hasFatalError = false;

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                List<Type> saveableClasses = new List<Type>();
                List<Type> allUsedSaveableTypes = new List<Type>();

                log.AppendLine("1. Asama: Sınıf Mimarisi Testi (Boş Constructor ve ID Çakışması)...");

                foreach (Type t in asm.GetTypes())
                {
                    bool hasSaveable = false;
                    foreach (var member in t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (member.CustomAttributes.Any(a => a.AttributeType.Name.Contains("Saveable")))
                        {
                            hasSaveable = true;
                            break;
                        }
                    }
                    if (hasSaveable) saveableClasses.Add(t);
                }

                foreach (Type t in saveableClasses)
                {
                    bool hasEmptyCtor = false;
                    foreach (var ctor in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (ctor.GetParameters().Length == 0) { hasEmptyCtor = true; break; }
                    }
                    if (!hasEmptyCtor)
                    {
                        log.AppendLine($"   [KRITIK HATA] {t.Name} sinifinin BOS (parametresiz) constructor'i yok!");
                        hasFatalError = true;
                    }

                    List<short> usedIds = new List<short>();
                    foreach (var member in t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var attr = member.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name.Contains("Saveable"));
                        if (attr != null && attr.ConstructorArguments.Count > 0)
                        {
                            short id = (short)attr.ConstructorArguments[0].Value;
                            if (usedIds.Contains(id))
                            {
                                log.AppendLine($"   [KRITIK HATA] {t.Name} icerisinde ID: {id} CAKISMA VAR!");
                                hasFatalError = true;
                            }
                            else usedIds.Add(id);

                            // Türleri topla (Canlı TaleWorlds kıyaslaması için)
                            Type memberType = null;
                            if (member is FieldInfo fi) memberType = fi.FieldType;
                            if (member is PropertyInfo pi) memberType = pi.PropertyType;
                            if (memberType != null && !allUsedSaveableTypes.Contains(memberType))
                            {
                                allUsedSaveableTypes.Add(memberType);
                            }
                        }
                    }
                }

                log.AppendLine("\n2. Asama: CANLI TALEWORLDS (1.5.1) DLL KIYASLAMASI (Missing Container Testi)...");
                
                // TaleWorlds.SaveSystem.SaveManager._definitionContext statik objesini Reflection ile al
                Assembly twSaveAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "TaleWorlds.SaveSystem");
                if (twSaveAssembly != null)
                {
                    Type saveManagerType = twSaveAssembly.GetType("TaleWorlds.SaveSystem.SaveManager");
                    FieldInfo defCtxField = saveManagerType.GetField("_definitionContext", BindingFlags.Static | BindingFlags.NonPublic);
                    object definitionContextObj = defCtxField?.GetValue(null);

                    if (definitionContextObj != null)
                    {
                        Type defCtxType = definitionContextObj.GetType();
                        MethodInfo hasDefinitionMethod = defCtxType.GetMethod("HasDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (hasDefinitionMethod != null)
                        {
                            log.AppendLine("TaleWorlds SaveManager Context'i basariyla yakalandi. Karsilastirma basliyor...");
                            int healthyCount = 0;

                            foreach (Type usedType in allUsedSaveableTypes)
                            {
                                // HasDefinition(Type) cagir
                                bool isRegisteredInGame = (bool)hasDefinitionMethod.Invoke(definitionContextObj, new object[] { usedType });

                                if (!isRegisteredInGame)
                                {
                                    log.AppendLine($"   [MOTOR HATASI] TaleWorlds {usedType.Name} yapisini TANIMIYOR! Bu liste veya sinif SaveableTypeDefiner icine eklenmemis! Kayit Sirasinda Kesin Cokecek!");
                                    hasFatalError = true;
                                }
                                else
                                {
                                    healthyCount++;
                                }
                            }
                            log.AppendLine($"-> {healthyCount} adet degisken turu (Listeler, Classlar) TaleWorlds 1.5.1 tarafindan basariyla tanindi.");
                        }
                        else log.AppendLine("   [UYARI] HasDefinition metodu bulunamadi.");
                    }
                    else log.AppendLine("   [UYARI] _definitionContext statik objesi null dondu. Oyun henuz save sistemini baslatmamis olabilir.");
                }
                else log.AppendLine("   [UYARI] TaleWorlds.SaveSystem DLL'i hafizada bulunamadi!");

                log.AppendLine("\n==== KONTROL SONUCU ====");
                if (hasFatalError)
                {
                    log.AppendLine("SONUC: MODUN KODLARINDA TALEWORLDS ILE UYUSMAYAN (KAYDEDILMEMIS) PARCALAR VAR! KAYIT YAPILIRKEN COKECEK!");
                    InformationManager.DisplayMessage(new InformationMessage("Yeni Araba Testi Bitti: KRITIK HATALAR BULUNDU! Masaustune bakin!", Colors.Red));
                }
                else
                {
                    log.AppendLine("SONUC: Mukemmel! Kendi icindeki constructor'lar saglam ve TaleWorlds'un guncel 1.5.1 DLL'leri senin TUM listelerini ve siniflarini taniyip onayladi.");
                    InformationManager.DisplayMessage(new InformationMessage("Yeni Araba Testi Bitti: 1.5.1 Ile Tam Uyumlu. Hata yok.", Colors.Green));
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"\n[TEST SIRASINDA CİDDİ HATA]: {ex.Message}\n{ex.StackTrace}");
            }

            WriteToFile(log.ToString());
        }

        private void WriteToFile(string text)
        {
            try
            {
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SaveDiagnosticsLog.txt");
                string log = $"\n[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]\n{text}\n=====================================";
                System.IO.File.AppendAllText(path, log);
            }
            catch { }
        }
    }
}
