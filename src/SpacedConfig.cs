using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LotrNpcs
{
    /// <summary>
    /// Writes a mod config file that people can read: the game's own StoreModConfig output, with a blank line
    /// around each big section (1.0.4, owner: "Break up wordy sections with blank space").
    /// </summary>
    public static class SpacedConfig
    {
        // A member of the file's top object, or of an object one level in (a faction), or a long string in a list
        // there (an #Instructions line or heading).
        static readonly Regex Member = new Regex("^( {2}| {4})\"[^\"]*\": ");
        static readonly Regex LongText = new Regex("^ {4}\"(---|.{40,})");
        static readonly Regex Closer = new Regex("^ *[}\\]],?$");

        /// <summary>Adds the blank lines to indented JSON: before and after every top-level or second-level member
        /// that spans several lines, and between long text lines in a list. Public for tests.</summary>
        public static string Spaced(string json)
        {
            string[] lines = json.Replace("\r\n", "\n").Split('\n');
            var output = new List<string>(lines.Length * 2);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i > 0)
                {
                    string before = lines[i - 1];
                    bool opensBlock = line.EndsWith("{") || line.EndsWith("[");
                    bool afterBlock = Closer.IsMatch(before) && Indent(before) == Indent(line);
                    bool firstInBlock = before.EndsWith("{") || before.EndsWith("[");
                    if (!firstInBlock && ((Member.IsMatch(line) && (opensBlock || afterBlock)) || (LongText.IsMatch(line) && before.StartsWith("    \""))))
                        output.Add("");
                }
                output.Add(line);
            }
            return string.Join(Environment.NewLine, output);
        }

        static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

        /// <summary>Stores the config in ModConfig/file, spaced out. Falls back to the game's own writer.</summary>
        public static void Store(ICoreAPI api, object config, string file)
        {
            try
            {
                string text = Spaced(JsonConvert.SerializeObject(config, Formatting.Indented));
                File.WriteAllText(Path.Combine(GamePaths.ModConfig, file), text);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[lotrnpcs] Could not write ModConfig/" + file + " spaced out (" + e.Message + "); writing it plain.");
                api.StoreModConfig(config, file);
            }
        }
    }
}
