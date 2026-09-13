using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using RebellionsAndDemographics; 

namespace ClassLibrary22
{
    public class FakeDataStore : IDataStore
    {
        public HashSet<Type> SyncedTypes = new HashSet<Type>();
        
        public bool IsSaving => true;
        public bool IsLoading => false;

        public bool SyncData<T>(string key, ref T data)
        {
            SyncedTypes.Add(typeof(T));
            return true;
        }
    }

    public class FakeSaveValidator : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private bool _f11Pressed = false;

        private void OnTick(float dt)
        {
            if (Input.IsKeyDown(InputKey.F11) && !_f11Pressed)
            {
                _f11Pressed = true;
                RunFakeSaveAnalysis();
            }
            else if (!Input.IsKeyDown(InputKey.F11))
            {
                _f11Pressed = false;
            }
        }

        private void RunFakeSaveAnalysis()
        {
            InformationManager.DisplayMessage(new InformationMessage("Starting Fake Save Analysis...", Colors.Yellow));

            try
            {
                FakeDataStore fakeStore = new FakeDataStore();
                foreach (var behavior in Campaign.Current.CampaignBehaviorManager.GetBehaviors<CampaignBehaviorBase>())
                {
                    if (behavior.GetType().Assembly == this.GetType().Assembly)
                    {
                        try { behavior.SyncData(fakeStore); } catch { }
                    }
                }

                HashSet<Type> saveableMemberTypes = new HashSet<Type>();
                foreach (var type in this.GetType().Assembly.GetTypes())
                {
                    var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Saveable")));
                    
                    foreach (var member in members)
                    {
                        if (member is FieldInfo fi) saveableMemberTypes.Add(fi.FieldType);
                        if (member is PropertyInfo pi) saveableMemberTypes.Add(pi.PropertyType);
                    }
                }

                HashSet<Type> allUsedTypes = new HashSet<Type>(fakeStore.SyncedTypes);
                allUsedTypes.UnionWith(saveableMemberTypes);

                HashSet<Type> requiredContainers = new HashSet<Type>();
                foreach (var t in allUsedTypes)
                {
                    ExtractContainers(t, requiredContainers);
                }

                List<string> registeredContainers = new List<string> {
                    "Dictionary<String, CampaignTime>", "Dictionary<Kingdom, Int32>", "Dictionary<Settlement, CastleData>",
                    "Dictionary<Settlement, String>", "Dictionary<String, TroopRoster>", "List<Hero>", "Dictionary<String, Int32>",
                    "List<PendingAllianceData>", "Dictionary<Settlement, CorruptionData>", "Dictionary<Hero, WarOrder>",
                    "Dictionary<Settlement, SettlementPopulationData>", "List<String>", "Dictionary<String, Boolean>",
                    "List<BattlefieldData>", "Dictionary<Kingdom, Settlement>", "Dictionary<Hero, Int32>", "List<EmbargoData>",
                    "Dictionary<Hero, IntrigueOfferType>", "Dictionary<Hero, CampaignTime>", "Dictionary<Clan, Single>",
                    "Dictionary<Kingdom, Boolean>", "Dictionary<Kingdom, CampaignTime>", "Dictionary<Hero, Boolean>",
                    "Dictionary<String, Single>", "List<VirtualWorkshopShare>", "Dictionary<Workshop, Int32>",
                    "Dictionary<Workshop, Boolean>", "Dictionary<Workshop, Hero>", "Dictionary<Workshop, WorkshopStrategy>",
                    "Dictionary<String, String>", "List<TradeWar>", "Dictionary<String, SpeculationData>",
                    "List<CharacterObject>", "List<PolicyObject>", "List<WorkshopEventData>", "List<SpeculationData>",
                    "Dictionary<Kingdom, List<PolicyObject>>", "HashSet<String>", "HashSet<Hero>", "HashSet<Agent>",
                    "Dictionary<String, Double>", "Dictionary<String, SiegeSpecialization>", "Dictionary<Clan, Int32>",
                    "Dictionary<Clan, Settlement>", "Dictionary<Clan, CampaignTime>", "Dictionary<Clan, Boolean>",
                    "Dictionary<Settlement, Int32>", "Dictionary<Settlement, CampaignTime>", "Dictionary<String, Kingdom>",
                    "Dictionary<Hero, Single>", "List<Kingdom>", "List<Settlement>", "Dictionary<Settlement, StrikeData>"
                };

                List<string> output = new List<string>();
                output.Add("=== FAKE SAVE VALIDATION REPORT ===");
                output.Add("");
                
                int missingCount = 0;
                foreach (var container in requiredContainers)
                {
                    string name = GetFriendlyName(container);
                    if (!registeredContainers.Contains(name) && !IsNativeContainer(name))
                    {
                        output.Add("[ERROR] MISSING CONTAINER DEFINITION: " + name);
                        missingCount++;
                    }
                    else
                    {
                        output.Add("[OK] " + name);
                    }
                }

                string path = TaleWorlds.Engine.Utilities.GetBasePath() + "Modules/Machination/FakeSaveReport.txt";
                System.IO.File.WriteAllLines(path, output);
                
                if (missingCount > 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Fake Save: Found {missingCount} missing containers! Check FakeSaveReport.txt", Colors.Red));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("Fake Save: All containers are registered! It should not crash.", Colors.Green));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Fake Save Error: " + ex.Message, Colors.Red));
            }
        }

        private bool IsNativeContainer(string name)
        {
            // Bannerlord natively registers these, but we don't know exactly which ones.
            // We just let the user see the list of 'missing' ones and they can add them if needed.
            return false;
        }

        private string GetFriendlyName(Type type)
        {
            if (type.IsGenericType)
            {
                string name = type.Name.Split((char)96)[0];
                var args = type.GetGenericArguments();
                string argNames = string.Join(", ", args.Select(a => GetFriendlyName(a)));
                return name + "<" + argNames + ">";
            }
            return type.Name;
        }

        private void ExtractContainers(Type type, HashSet<Type> containers)
        {
            if (type == null) return;
            if (type.IsGenericType)
            {
                var gen = type.GetGenericTypeDefinition();
                if (gen == typeof(List<>) || gen == typeof(Dictionary<,>) || gen == typeof(Queue<>) || gen == typeof(HashSet<>))
                {
                    containers.Add(type);
                    foreach (var arg in type.GetGenericArguments())
                    {
                        ExtractContainers(arg, containers);
                    }
                }
            }
            else if (type.IsArray)
            {
                ExtractContainers(type.GetElementType(), containers);
            }
        }
    }
}
