using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        var files = Directory.GetFiles(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22"", ""*.cs"", SearchOption.AllDirectories);
        foreach (var file in files) {
            string txt = File.ReadAllText(file);
            // Look for variables of type object, or base Bannerlord types that are assigned custom classes
            if (txt.Contains(""object "") && txt.Contains(""[Saveable"")) {
                Console.WriteLine(""Found 'object' in Saveable in "" + file);
            }
            if (txt.Contains(""SyncData"") && txt.Contains(""ref object"")) {
                Console.WriteLine(""Found 'ref object' in SyncData in "" + file);
            }
        }
    }
}
