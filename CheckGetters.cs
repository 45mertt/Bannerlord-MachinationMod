using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        var files = Directory.GetFiles(@""C:\Users\snrto\source\repos\ClassLibrary22\ClassLibrary22"", ""*.cs"", SearchOption.AllDirectories);
        foreach (var file in files) {
            string content = File.ReadAllText(file);
            var matches = Regex.Matches(content, @""\[SaveableProperty.*?\].*?public\s+[_a-zA-Z0-9<>, ]+\s+([_a-zA-Z0-9]+)\s*\{\s*get\s*\{([^\}]+)\}"", RegexOptions.Singleline);
            foreach (Match m in matches) {
                string getter = m.Groups[2].Value.Trim();
                if (getter.Contains(""new "") && !getter.Contains(""="")) {
                    Console.WriteLine(file + "" -> "" + m.Groups[1].Value + "" : "" + getter);
                }
            }
        }
    }
}
