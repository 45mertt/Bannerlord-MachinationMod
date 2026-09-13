using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace ClassLibrary22
{
    public class SaveDiagnosticBehavior : CampaignBehaviorBase
    {
        private static string LogPath => TaleWorlds.Engine.Utilities.GetBasePath() + "Modules/Machination/SaveDiagnostics.log";

        public override void RegisterEvents()
        {
            // Ýsteðe baðlý olarak auto-save öncesi çalýþmasý için
            // CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, RunSaveAudit);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Bu diagnostic behavior'un kendi verisi yok
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        // --- KONSOL KOMUTLARI ---

        [TaleWorlds.Library.CommandLineFunctionality.CommandLineArgumentFunction("run_save_audit", "mod")]
        public static string RunSaveAuditCommand(List<string> args)
        {
            InformationManager.DisplayMessage(new InformationMessage("Starting Mod Save Audit... Check log file for details.", Colors.Yellow));
            Log("=== STARTING SAVE AUDIT ===");
            
            int errors = 0;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var behaviors = Campaign.Current.CampaignBehaviorManager.GetBehaviors<CampaignBehaviorBase>()
                                .Where(b => b.GetType().Assembly == asm).ToList();

                Log($"Found {behaviors.Count} behaviors to audit.");

                var diagnosticStore = new DiagnosticDataStore();
                foreach (var behavior in behaviors)
                {
                    Log($"\n--- Auditing Behavior: {behavior.GetType().Name} ---");
                    try
                    {
                        behavior.SyncData(diagnosticStore);
                    }
                    catch (Exception ex)
                    {
                        Log($"[CRASH IN SYNCDATA] Behavior {behavior.GetType().Name} crashed during SyncData: {ex.Message}");
                        errors++;
                    }
                }

                errors += diagnosticStore.ErrorCount;
                
                string result = $"Save Audit Completed! Found {errors} potential crash points. Log saved to {LogPath}";
                Log("=== AUDIT COMPLETED ===\n");
                
                InformationManager.DisplayMessage(new InformationMessage(result, errors > 0 ? Colors.Red : Colors.Green));
                return result;
            }
            catch (Exception ex)
            {
                Log($"[FATAL ERROR IN AUDIT] {ex}");
                return "Fatal error during audit. Check log.";
            }
        }

        [TaleWorlds.Library.CommandLineFunctionality.CommandLineArgumentFunction("force_stress_save", "mod")]
        public static string ForceStressSaveCommand(List<string> args)
        {
            Log("=== STARTING STRESS TEST INJECTION ===");
            // Find specific behaviors and inject bad data
            var asm = Assembly.GetExecutingAssembly();
            var behaviors = Campaign.Current.CampaignBehaviorManager.GetBehaviors<CampaignBehaviorBase>()
                            .Where(b => b.GetType().Assembly == asm).ToList();

            int injected = 0;
            foreach (var b in behaviors)
            {
                // Inject null values into dictionaries/lists if possible using reflection
                var fields = b.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.FieldType.IsGenericType && (f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) || f.FieldType.GetGenericTypeDefinition() == typeof(List<>)));
                
                foreach (var f in fields)
                {
                    try
                    {
                        var val = f.GetValue(b);
                        if (val == null) continue;
                        
                        if (val is IList list)
                        {
                            if (list.Count > 0 && !list.GetType().GetGenericArguments()[0].IsValueType)
                            {
                                list.Add(null); // Inject null element
                                Log($"Injected NULL element into {b.GetType().Name}.{f.Name} (List)");
                                injected++;
                            }
                        }
                    }
                    catch { }
                }
            }

            Log($"Injected {injected} edge cases. Now attempting dry-run save audit...");
            RunSaveAuditCommand(null);
            return "Stress test injected and audited! Check log.";
        }
    }

    public class DiagnosticDataStore : IDataStore
    {
        public bool IsSaving => true;
        public bool IsLoading => false;
        public int ErrorCount = 0;

        private HashSet<object> _visitedObjects = new HashSet<object>();

        public bool SyncData<T>(string key, ref T data)
        {
            SaveDiagnosticBehavior.Log($"Checking Key: '{key}' | Type: {typeof(T).Name}");
            ValidateObject(data, key, typeof(T), "");
            return true;
        }

        private void ValidateObject(object obj, string rootKey, Type declaredType, string path)
        {
            if (obj == null) return;
            if (_visitedObjects.Contains(obj)) return; // Prevent circular reference loops
            
            Type objType = obj.GetType();
            if (objType.IsPrimitive || objType == typeof(string) || objType.IsEnum) return;

            _visitedObjects.Add(obj);

            // 1. Check for 'Ghost' objects (Dead heroes, eliminated kingdoms, destroyed settlements)
            if (obj is Hero hero)
            {
                if (hero.IsDead || hero.Clan == null)
                {
                    SaveDiagnosticBehavior.Log($"[WARNING] Ghost Hero detected at {rootKey}{path}: {hero.Name} (Dead: {hero.IsDead}, Clan Null: {hero.Clan == null})");
                    ErrorCount++;
                }
                return;
            }
            if (obj is Kingdom kingdom && kingdom.IsEliminated)
            {
                SaveDiagnosticBehavior.Log($"[WARNING] Eliminated Kingdom detected at {rootKey}{path}: {kingdom.Name}");
                ErrorCount++;
                return;
            }
            if (obj is Clan clan && clan.IsEliminated)
            {
                SaveDiagnosticBehavior.Log($"[WARNING] Eliminated Clan detected at {rootKey}{path}: {clan.Name}");
                ErrorCount++;
                return;
            }

            // 2. Mock Serialization logic for Collections (List, Dictionary)
            if (objType.IsGenericType)
            {
                var genericTypeDef = objType.GetGenericTypeDefinition();
                
                // Nested generic check (The notorious Bannerlord crash cause)
                if (path.Contains("[") && (genericTypeDef == typeof(List<>) || genericTypeDef == typeof(Dictionary<,>)))
                {
                    SaveDiagnosticBehavior.Log($"[CRITICAL] Nested Generic Container detected at {rootKey}{path}. Type: {objType.Name}. TaleWorlds SaveSystem is notorious for crashing on nested generic containers (e.g. Dictionary<K, List<V>>)! Consider wrapping the inner list in a custom SaveableClass.");
                    ErrorCount++;
                }

                if (obj is IDictionary dict)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (entry.Key == null)
                        {
                            SaveDiagnosticBehavior.Log($"[ERROR] Null Key in Dictionary {rootKey}{path}");
                            ErrorCount++;
                        }
                        else
                        {
                            ValidateObject(entry.Key, rootKey, entry.Key.GetType(), path + "[Key]");
                        }
                        
                        ValidateObject(entry.Value, rootKey, entry.Value?.GetType(), path + $"[{entry.Key}]");
                    }
                }
                else if (obj is IEnumerable enumerable)
                {
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        ValidateObject(item, rootKey, item?.GetType(), path + $"[{index}]");
                        index++;
                    }
                }
                return;
            }

            // 3. Drill down into Custom Saveable classes
            var saveableMembers = objType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Saveable")));

            foreach (var member in saveableMembers)
            {
                object memberValue = null;
                Type memberType = null;

                try
                {
                    if (member is FieldInfo fi)
                    {
                        memberValue = fi.GetValue(obj);
                        memberType = fi.FieldType;
                    }
                    else if (member is PropertyInfo pi)
                    {
                        memberValue = pi.GetValue(obj);
                        memberType = pi.PropertyType;
                    }
                }
                catch (Exception ex)
                {
                    SaveDiagnosticBehavior.Log($"[ERROR] Could not read property/field {member.Name} on {objType.Name}: {ex.Message}");
                    ErrorCount++;
                    continue;
                }

                ValidateObject(memberValue, rootKey, memberType, path + "." + member.Name);
            }
        }
    }
}
