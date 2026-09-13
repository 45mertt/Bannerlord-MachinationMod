using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program {
    static void Main() {
        var files = Directory.GetFiles(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22"", ""*.cs"");
        var types = new HashSet<string>();
        foreach(var f in files) {
            string txt = File.ReadAllText(f);
            var m1 = Regex.Matches(txt, @""dataStore\.SyncData[^\(]*\([^,]+,\s*ref\s+([a-zA-Z0-9_]+)\)"");
            foreach(Match m in m1) {
                var varName = m.Groups[1].Value;
                // find the variable type definition
                var m2 = Regex.Match(txt, @""(Dictionary<[^>]+>|List<[^>]+>)\s+"" + varName + @""\s*[;=]"");
                if (m2.Success) {
                    types.Add(m2.Groups[1].Value.Replace(""System.Collections.Generic."", """").Replace("" "", """").Replace(""TaleWorlds.CampaignSystem."", """").Replace(""TaleWorlds.CampaignSystem.Settlements.Workshops."", """").Replace(""TaleWorlds.CampaignSystem.Settlements."", """").Replace(""ClassLibrary22."", """").Replace(""RebellionsAndDemographics."", """"));
                }
            }
        }
        foreach(var t in types) {
            Console.WriteLine(t);
        }
    }
}
