using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

class Program {
    static void Main() {
        try {
            Assembly mod = Assembly.LoadFrom(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22\bin\Debug\ClassLibrary22.dll"");
            var collections = new HashSet<string>();
            foreach(var type in mod.GetTypes()) {
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  .Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name.Contains(""Saveable"")));
                foreach(var m in members) {
                    Type t = null;
                    if (m is FieldInfo f) t = f.FieldType;
                    if (m is PropertyInfo p) t = p.PropertyType;
                    if (t != null && t.IsGenericType) {
                        collections.Add(t.ToString());
                    }
                }
            }
            foreach(var c in collections) {
                Console.WriteLine(c);
            }
        } catch(Exception ex) {
            Console.WriteLine(ex);
        }
    }
}
