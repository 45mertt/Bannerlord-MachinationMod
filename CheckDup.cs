using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

class Program {
    static void Main() {
        var dll = Assembly.LoadFrom(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22\bin\Debug\ClassLibrary22.dll"");
        foreach(var t in dll.GetTypes()) {
            var ids = new HashSet<int>();
            foreach(var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                foreach(var a in m.CustomAttributes) {
                    if (a.AttributeType.Name.Contains(""Saveable"")) {
                        int id = (int)a.ConstructorArguments[0].Value;
                        if (ids.Contains(id)) {
                            Console.WriteLine(""DUPLICATE ID "" + id + "" in class "" + t.FullName);
                        }
                        ids.Add(id);
                    }
                }
            }
        }
    }
}
