using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program {
    static void Main() {
        var files = Directory.GetFiles(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22"", ""*.cs"");
        var typesWithSaveable = new HashSet<string>();
        foreach(var f in files) {
            string txt = File.ReadAllText(f);
            if (txt.Contains(""[Saveable"")) {
                var m = Regex.Match(txt, @""(?:public|private|internal)\s+(?:sealed\s+)?class\s+([a-zA-Z0-9_]+)"");
                if (m.Success) {
                    typesWithSaveable.Add(m.Groups[1].Value);
                }
            }
        }
        foreach(var t in typesWithSaveable) {
            Console.WriteLine(t);
        }
    }
}
