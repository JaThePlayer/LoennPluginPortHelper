using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LoennPluginHelper
{
    class Program
    {
        public static bool JaUtils = false;
        public static Regex HexColorValidator = new Regex("^(?:[0-9a-fA-F]{3}){1,2}$", RegexOptions.Compiled);
        public static Regex AhornSettingFunc = new Regex("Ahorn\\.(.*)\\(.*\\) = (.*)", RegexOptions.Compiled);

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            while (true)
            {
                Console.WriteLine("options: entity, langToLonn");
                string option = Console.ReadLine();

                switch (option)
                {
                    //case "mapdef":
                    //    Console.WriteLine("Paste in the @mapdef line from your plugin");
                    //    Console.WriteLine(MapdefToPlacement(Console.ReadLine()).Content);
                    //    break;
                    case "entity": 
                        {
                            Console.WriteLine("Give the path to the .jl file");
                            var input = Console.ReadLine();
                            Console.WriteLine();
                            Console.WriteLine(AhornTriggerToLonnTrigger(File.ReadAllText(input)));
                            Console.WriteLine();
                        }
                        break;
                    case "langToLonn":
                        {
                            Console.WriteLine("Give the path to the ahorn .lang file");
                            var input = Console.ReadLine();
                            Console.WriteLine();
                            LangFileConverter.ToLonn(input);
                        }
                        break;
                    //case "jautils":
                    //    JaUtils = !JaUtils;
                    //    break;
                }

            }

        }

        static List<string> blacklistedParameterNames = new List<string>() { "x", "y", "nodes" };

        static string AhornTriggerToLonnTrigger(string arg)
        {
            string[] lines = arg.Trim().Split("\n");

            string className = "";
            string entityStringID = "";
            string placementString = "\n";
            string fieldInfoString = "";

            foreach (var line in lines)
            {
                if (line.StartsWith("@mapdef"))
                {
                    var placement = MapdefToPlacement(line);
                    className = placement.ClassName;
                    className = char.ToLower(className[0]) + className[1..];
                    placementString += placement.Content;
                    entityStringID = placement.EntityStringID;
                    fieldInfoString = placement.FieldInfoString;
                    break;
                }
            }

            string plugin = JaUtils
                ? @$"local {className} = {{}}
{className}.name = {entityStringID}
{className}.depth = 0 -- TODO: Set this value to correspond to the value used in c#. This can also be a function {className}.depth(room, entity), if the depth can change based on field values
{placementString}
"
                : @$"local {className} = {{}}
{className}.name = {entityStringID}
{className}.depth = 0 -- TODO: Set this value to correspond to the value used in c#. This can also be a function {className}.depth(room, entity), if the depth can change based on field values
{className}.placements = {{{placementString}
}}
{fieldInfoString}";

            foreach (Match settingFunc in AhornSettingFunc.Matches(arg))
            {
                var funcName = settingFunc.Groups[1].Value.Trim();
                var value = settingFunc.Groups[2].Value.Trim();

                switch (funcName)
                {
                    case "resizable":
                        plugin += $"{className}.canResize = {{ {value} }}\n";
                        break;
                    case "minimumSize":
                        plugin += $"{className}.minimumSize = {{ {value} }}\n";
                        break;
                    case "nodeLimits":
                        plugin += $"{className}.nodeLineRenderType = \"line\"\n";
                        plugin += $"{className}.nodeLimits = {{ {value} }}\n";
                        break;
                }
            }
            plugin = plugin.Replace("\r\n", "\n");

            plugin += $"\n--TODO: Rendering\n\nreturn {className}";

            return plugin;
        }

        static LonnPlacement MapdefToPlacement(string arg)
        {
            string[] split = arg.Trim().Split();

            string entityStringID = split[2].Trim();
            string ctorName = split[3].Remove(split[3].IndexOf('('));

            string[] ctorArgs = arg[(arg.IndexOf('(') + 1)..].Trim().Split(",");

            var juliaParameters = JuliaParameter.CreateParameterList(ctorArgs);

            LonnPlacement lonnPlacement = new() { EntityStringID = entityStringID, ClassName = ctorName };

            if (JaUtils)
                lonnPlacement.GenerateContentJaUtils(juliaParameters);
            else
                lonnPlacement.GenerateContentNormal(juliaParameters);

            return lonnPlacement;
        }

        static string CamelCaseToSnakeCase(string camelCase)
        {
            string snakeCase = char.ToLower(camelCase[0]).ToString();
            for (int i = 1; i < camelCase.Length; i++)
            {
                if (char.IsUpper(camelCase[i]))
                    snakeCase += '_';
                snakeCase += char.ToLower(camelCase[i]);
            }

            return snakeCase;
        }

        public struct LonnPlacement
        {
            public string Content;
            public string EntityStringID;
            public string ClassName;

            public string FieldInfoString;

            public void GenerateContentNormal(List<JuliaParameter> juliaParameters)
            {
                Content = @$"    {{
        name = ""default"", 
        data = {{
";
                foreach (var param in juliaParameters)
                {
                    if (!blacklistedParameterNames.Contains(param.Name) && param.DefaultValue != null)
                    {
                        Content += $"            {param.Name} = {param.ConvertDefaultValueToLua()},\n";
                    }
                }
                Content += @"        }
    }";

                FieldInfoString = $"\n{char.ToLower(ClassName[0]) + ClassName[1..]}.fieldInformation = {{\n";

                foreach (var item in juliaParameters.Where(p => p.IsHexColor()))
                {
                    FieldInfoString += 
$@"    {item.Name} = {{
        fieldType = ""color"",
        allowXNAColors = true,
    }}
";
                }

                FieldInfoString += "}\n";
            }

            public void GenerateContentJaUtils(List<JuliaParameter> juliaParameters)
            {
                string className = char.ToLower(ClassName[0]) + ClassName[1..];
                Content = $"jautils.createPlacementsPreserveOrder({className}, \"default\", {{\n";
                foreach (var param in juliaParameters)
                {
                    if (!blacklistedParameterNames.Contains(param.Name) && param.DefaultValue != null)
                    {
                        Content += $"   {{ \"{param.Name}\", {param.ConvertDefaultValueToLua()}{(param.IsHexColor() ? ", \"color\"" : "")} }},\n";
                    }
                }
                Content += "})";
            }
        }

        public struct JuliaParameter
        {
            public string Name;
            public string Type;
            public string DefaultValue;

            public JuliaParameter(string stringRepresentation)
            {
                // absolutely horrible code right here
                int seperatorIndex = stringRepresentation.IndexOf(':');
                int equalSignIndex = stringRepresentation.IndexOf('=');
                if (seperatorIndex == -1)
                {

                    Name = stringRepresentation[..equalSignIndex].Trim();
                    DefaultValue = stringRepresentation[(equalSignIndex + 1)..].Trim();
                    Type = null;
                }
                else
                {
                    Name = stringRepresentation[..seperatorIndex].Trim();
                    if (equalSignIndex == -1)
                    {
                        Type = stringRepresentation[(seperatorIndex + 2)..].Trim();
                        DefaultValue = null;
                    }
                    else
                    {
                        Type = stringRepresentation[(seperatorIndex + 2)..equalSignIndex].Trim();
                        DefaultValue = stringRepresentation[(equalSignIndex + 1)..].Trim();
                    }
                }
            }

            public string ConvertDefaultValueToLua()
            {
                if (DefaultValue == "Maple.defaultBlockWidth" || DefaultValue == "Maple.defaultBlockHeight")
                    return "16";

                return DefaultValue;
            }

            public override string ToString()
            {
                string ret = Name;
                if (Type is not null)
                    ret += "::" + Type;
                if (DefaultValue is not null)
                    ret += "=" + DefaultValue;

                return ret;
            }

            public static List<JuliaParameter> CreateParameterList(string[] stringArgs)
            {
                List<JuliaParameter> juliaParameters = new List<JuliaParameter>();

                foreach (var stringArg in stringArgs)
                {
                    juliaParameters.Add(new JuliaParameter(stringArg.TrimEnd(')').TrimEnd(',')));
                }

                return juliaParameters;
            }

            public bool IsHexColor()
            {
                if (Type != "String")
                    return false;

                string actualValue = DefaultValue.Trim('"').TrimEnd('#');
                if (actualValue.Length == 6 && HexColorValidator.IsMatch(actualValue))
                    return true;

                //if (xnaColors.Contains(actualValue))
                //    return true;

                return false;
            }
        }

        static List<string> xnaColors = new List<string>() {

        "Transparent",
        "AliceBlue",
        "AntiqueWhite",
        "Aqua",
        "Aquamarine",
        "Azure",
        "Beige",
        "Bisque",
        "Black",
        "BlanchedAlmond",
        "Blue",
        "BlueViolet",
        "Brown",
        "BurlyWood",
        "CadetBlue",
        "Chartreuse",
        "Chocolate",
        "Coral",
        "CornflowerBlue",
        "Cornsilk",
        "Crimson",
        "Cyan",
        "DarkBlue",
        "DarkCyan",
        "DarkGoldenrod",
        "DarkGray",
        "DarkGreen",
        "DarkKhaki",
        "DarkMagenta",
        "DarkOliveGreen",
        "DarkOrange",
        "DarkOrchid",
        "DarkRed",
        "DarkSalmon",
        "DarkSeaGreen",
        "DarkSlateBlue",
        "DarkSlateGray",
        "DarkTurquoise",
        "DarkViolet",
        "DeepPink",
        "DeepSkyBlue",
        "DimGray",
        "DodgerBlue",
        "Firebrick",
        "FloralWhite",
        "ForestGreen",
        "Fuchsia",
        "Gainsboro",
        "GhostWhite",
        "Gold",
        "Goldenrod",
        "Gray",
        "Green",
        "GreenYellow",
        "Honeydew",
        "HotPink",
        "IndianRed",
        "Indigo",
        "Ivory",
        "Khaki",
        "Lavender",
        "LavenderBlush",
        "LawnGreen",
        "LemonChiffon",
        "LightBlue",
        "LightCoral",
        "LightCyan",
        "LightGoldenrodYellow",
        "LightGray",
        "LightGreen",
        "LightPink",
        "LightSalmon",
        "LightSeaGreen",
        "LightSkyBlue",
        "LightSlateGray",
        "LightSteelBlue",
        "LightYellow",
        "Lime",
        "LimeGreen",
        "Linen",
        "Magenta",
        "Maroon",
        "MediumAquamarine",
        "MediumBlue",
        "MediumOrchid",
        "MediumPurple",
        "MediumSeaGreen",
        "MediumSlateBlue",
        "MediumSpringGreen",
        "MediumTurquoise",
        "MediumVioletRed",
        "MidnightBlue",
        "MintCream",
        "MistyRose",
        "Moccasin",
        "NavajoWhite",
        "Navy",
        "OldLace",
        "Olive",
        "OliveDrab",
        "Orange",
        "OrangeRed",
        "Orchid",
        "PaleGoldenrod",
        "PaleGreen",
        "PaleTurquoise",
        "PaleVioletRed",
        "PapayaWhip",
        "PeachPuff",
        "Peru",
        "Pink",
        "Plum",
        "PowderBlue",
        "Purple",
        "Red",
        "RosyBrown",
        "RoyalBlue",
        "SaddleBrown",
        "Salmon",
        "SandyBrown",
        "SeaGreen",
        "SeaShell",
        "Sienna",
        "Silver",
        "SkyBlue",
        "SlateBlue",
        "SlateGray",
        "Snow",
        "SpringGreen",
        "SteelBlue",
        "Tan",
        "Teal",
        "Thistle",
        "Tomato",
        "Turquoise",
        "Violet",
        "Wheat",
        "White",
        "WhiteSmoke",
        "Yellow",
        "YellowGreen",
        };
    }
}
