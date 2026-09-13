using System;
using System.Reflection;
using System.IO;

class Program {
    static void Main() {
        try {
            Assembly asm = Assembly.LoadFrom(@"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.SaveSystem.dll");
            Type t = asm.GetType("TaleWorlds.SaveSystem.Save.ContainerSaveData");
            if (t != null) {
                var prop = t.GetProperty("CapturedElementCount", BindingFlags.Public | BindingFlags.Instance);
                Console.WriteLine("Property CapturedElementCount found: " + (prop != null));
                if (prop != null) {
                    Console.WriteLine("Getter: " + prop.GetGetMethod(true)?.Name);
                }
                
                // Let's get the fields of ContainerSaveData
                foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)) {
                    Console.WriteLine("Field: " + f.Name + " (" + f.FieldType.Name + ")");
                }
            }
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }
}
