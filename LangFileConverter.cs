using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LoennPluginHelper
{
    public static class LangFileConverter
    {
        public static void ToAhorn(string loennLangFilePath)
        {
            string[] lines = File.ReadAllLines(loennLangFilePath.Trim('"'));
            string ahornFileContents = "";
            foreach (var lineRaw in lines)
            {
                string line = lineRaw.Trim();
                if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                {
                    // comment
                    ahornFileContents += line + "\n";
                } else
                {
                    // tooltips
                    if ((line.StartsWith("entities.") || line.StartsWith("triggers.")) && line.Contains(".description."))
                    {
                        ahornFileContents += "placements." + line.Replace(".description.", ".tooltips.") + "\n";
                    }
                }

            }

            System.Console.WriteLine(ahornFileContents);
        }


        private static Regex SentenceCaseRegex = new("[a-z][A-Z]", RegexOptions.Compiled);
        public static string ToSentenceCase(this string str)
        {
            return SentenceCaseRegex.Replace(str, m => $"{m.Value[0]} {m.Value[1]}");
        }

        public static void ToLonn(string ahornLangFilePath)
        {
            string[] lines = File.ReadAllLines(ahornLangFilePath.Trim('"'));

            List<string> encounteredEntities = new();

            string file = "#TODO: Edit this, so that lonn can append [modname] to entity placements automatically.\nmods.EverestYamlName.name=Mod Display Name\n";
            foreach (var lineRaw in lines)
            {
                string line = lineRaw.Trim();
                if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                {
                    // comment
                    file += line + "\n";
                }
                else
                {
                    // tooltips
                    if (line.StartsWith("placements.") && line.Contains(".tooltips."))
                    {
                        var dotSplit = line.Split('.');
                        var entityName = dotSplit[2];

                        if (!encounteredEntities.Contains(entityName))
                        {
                            encounteredEntities.Add(entityName);
                            file += $"{dotSplit[1]}.{entityName}.placements.name.default={entityName[(entityName.IndexOf('/')+1)..].ToSentenceCase()}\n";
                        }

                        file += line["placements.".Length..]
                                .Replace(".tooltips.", ".attributes.description.")
                                + "\n";
                    }
                }

            }

            System.Console.WriteLine(file);
        }
    }
}
