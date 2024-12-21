using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkScript3
{
    public class ProgramVersion
    {
        public static readonly string VERSION = "3.5";

        public static string GetCompatibilityMessage(string fileName, string resourceString, string fileVer)
        {
            int cmp = CompareVersions(VERSION, fileVer);
            if (cmp == 0)
            {
                return "";
            }
            StringBuilder sb = new StringBuilder();
            if (cmp > 0)
            {
                string version = fileVer == null ? "an unknown version" : $"older version {fileVer}";
                sb.AppendLine($"{fileName} was previously saved using {version} of DarkScript3.");
            }
            else
            {
                sb.AppendLine($"{fileName} was previously saved using newer version {fileVer} of DarkScript3.");
                sb.AppendLine("Download the latest version to ensure compatibility.");
            }
            List<IncompatibilityEntry> entries = intentionalIncompatibilies
                .Where(compat => CompareVersions(compat.Version, fileVer) > 0
                    && (compat.Games == null || compat.Games.Any(g => resourceString.StartsWith(g + "-common"))))
                .ToList();
            if (entries.Count > 0)
            {
                sb.AppendLine();
                sb.Append($"Using {VERSION} may involve fixing these known incompatibilities:");
                foreach (IncompatibilityEntry entry in entries)
                {
                    sb.AppendLine();
                    sb.Append($"- {entry.Text}");
                }
            }
            return sb.ToString();
        }

        // 0 if the same, 1 if this > var (given version is older), -1 is this < ver (given version is newer)
        public static int CompareVersions(string thisVer, string otherVer)
        {
            if (otherVer == null)
            {
                return 1;
            }
            List<int> thisList = ParseSemanticVersion(thisVer);
            List<int> otherList = ParseSemanticVersion(otherVer);
            for (int i = 0; i < Math.Max(thisList.Count, otherList.Count); i++)
            {
                int thisPart = i < thisList.Count ? thisList[i] : 0;
                int otherPart = i < otherList.Count ? otherList[i] : 0;
                int cmp = thisPart.CompareTo(otherPart);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            return 0;
        }

        private static List<int> ParseSemanticVersion(string version)
        {
            // Remove tag
            version = version.Split('-')[0];
            // This does no real validation at all
            return version.Split('.').Select(part => int.TryParse(part, out int val) ? val : 0).ToList();
        }

        private static readonly List<IncompatibilityEntry> intentionalIncompatibilies = new List<IncompatibilityEntry>
        {
            new IncompatibilityEntry("3.2", "ModifyBowProperties requires additional arg unknown8 with default value 1", new List<string> { "ds2", "ds2scholar" }),
            new IncompatibilityEntry("3.2", "ApplySoulScalingToWeapon requires additional arg unknown2 with default value 0", new List<string> { "ds2", "ds2scholar" }),
            new IncompatibilityEntry("3.2", "Goto/End/SkipIfPlayerInoutsideArea require additional arg numberOfTargetCharacters with default value 1", new List<string> { "ds3" }),
            new IncompatibilityEntry("3.2", "DisplayHollowArenaPvpMessage is now DisplayGenericDialogGloballyAndSetEventFlags. Get the new lines from vanilla files.", new List<string> { "sekiro" }),
            new IncompatibilityEntry("3.2", "JavaScript is run in strict mode, so all variable assignments must be declared with let/const/var"),
            new IncompatibilityEntry("3.2.2", "EndIfPlayerInoutsideArea requires additional arg numberOfTargetCharacters with default value 1", new List<string> { "sekiro" }),
            new IncompatibilityEntry("3.5", "Typed event initialization adds an extra parsing step before saving"),
            new IncompatibilityEntry("3.5", "Typed event initialization requires that arguments are passed into instructions directly, not modified or used elsewhere"),
        };

        private class IncompatibilityEntry
        {
            // The version at which this incompatibility was introduced
            public string Version { get; set; }
            // Description and mitigation of incompatibility
            public string Text { get; set; }
            // If game-specific, the games
            public List<string> Games { get; set; }

            public IncompatibilityEntry(string version, string text, List<string> games = null)
            {
                Version = version;
                Text = text;
                Games = games;
            }
        }
    }
}
