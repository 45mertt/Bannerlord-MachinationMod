using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        var files = Directory.GetFiles(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22"", ""*.cs"");
        foreach(var f in files) {
            string txt = File.ReadAllText(f);
            var matches = Regex.Matches(txt, @""void\s+SyncData[^{]*\{([^{}]*\{[^{}]*\}[^{}]*)*[^{}]*\}"", RegexOptions.Singleline);
            foreach(Match m in matches) {
                var body = m.Value;
                var dicts = Regex.Matches(body, @""(Dictionary<[^>]+>|List<[^>]+>)"");
                foreach(Match d in dicts) {
                    Console.WriteLine(f + "" : "" + d.Value);
                }
            }
        }
    }
}
