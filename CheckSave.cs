using System;
using System.Reflection;

class Program {
    static void Main() {
        try {
            Assembly asm = Assembly.LoadFrom(@"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.SaveSystem.dll");
            Type t = asm.GetType("TaleWorlds.SaveSystem.SaveableTypeDefiner");
            if (t != null) {
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                    if (m.Name.Contains("Container")) {
                        Console.WriteLine(m.ToString());
                    }
                }
            } else {
                Console.WriteLine("Type not found");
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
