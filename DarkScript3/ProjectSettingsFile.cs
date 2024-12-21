using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkScript3
{
    // Extremely limited readonly copy of DSMapStudio/Smithbox ProjectSettings just to extract project data.
    // This could be moved into a shared library if desired.
    public class ProjectSettingsFile
    {
        // Project file contents
        public ProjectSettings Settings { get; set; }
        // Project file path, which exists
        public string FilePath { get; set; }
        // Project event directory path, which is null if it is not supported.
        // If present, it but may not exist, but the outer directory should.
        public string ProjectEventDirectory { get; set; }
        // Game event directory path, which is null if it does not exist or is not supported.
        public string GameEventDirectory { get; set; }
        // Prefix for use with NameMetadata and other resources
        public string ResourcePrefix { get; set; }

        // https://github.com/soulsmods/DSMapStudio/blob/master/StudioCore/GameType.cs
        public enum GameType
        {
            Undefined = 0,
            // DSMS names
            DemonsSouls = 1,
            DarkSoulsPTDE = 2,
            DarkSoulsRemastered = 3,
            DarkSoulsIISOTFS = 4,
            DarkSoulsIII = 5,
            Bloodborne = 6,
            Sekiro = 7,
            EldenRing = 8,
            // Smithbox names
            // Duplicates are fine but makes reflection name lookup ambiguous
            DES = 1,
            DS1 = 2,
            DS1R = 3,
            DS2S = 4,
            DS3 = 5,
            BB = 6,
            SDT = 7,
            ER = 8,
            AC6 = 9,
            DS2 = 10,
            AC4 = 11,
            ACFA = 12,
            ACV = 13,
            ACVD = 14,
        }

        // https://github.com/soulsmods/DSMapStudio/blob/master/StudioCore/Editor/ProjectSettings.cs
        public class ProjectSettings
        {
            public string ProjectName { get; set; } = "";
            public string GameRoot { get; set; } = "";
            public GameType GameType { get; set; } = GameType.Undefined;
        }

        private static readonly GameType[] eventDirGames = new[]
        {
            GameType.DarkSoulsPTDE, GameType.DarkSoulsRemastered,
            GameType.DarkSoulsIII, GameType.Bloodborne,
            GameType.Sekiro, GameType.EldenRing,
            GameType.AC6
        };
        private static readonly Dictionary<GameType, string> resourcePrefixHint = new Dictionary<GameType, string>
        {
            [GameType.DarkSoulsPTDE] = "ds1",
            [GameType.DarkSoulsRemastered] = "ds1",
            // DS2 has no resource files at present, but probably use scholar for both when that comes
            [GameType.DarkSoulsIISOTFS] = "ds2scholar",
            [GameType.DarkSoulsIII] = "ds2scholar",
            [GameType.Bloodborne] = "bb",
            [GameType.Sekiro] = "sekiro",
            [GameType.EldenRing] = "er",
            [GameType.AC6] = "ac6",
        };

        // Expects a fully specified valid project JSON path.
        // This may throw an exception.
        public static ProjectSettingsFile LoadProjectFile(string projectJsonPath)
        {
            // Wrapper to rewrite exceptions
            try
            {
                return LoadProject(projectJsonPath);
            }
            catch (JsonException je)
            {
                throw new IOException($"Failed to parse {projectJsonPath}", je);
            }
            catch (IOException ie)
            {
                throw new IOException($"Failed to load {projectJsonPath}", ie);
            }
        }

        public static bool TryGetEmevdFileProject(string emevdPath, out ProjectSettingsFile project)
        {
            project = null;
            try
            {
                // The emevd file may not exist, just require that the parent directory does
                FileInfo emevdInfo = new FileInfo(emevdPath);
                if (emevdInfo.Directory?.Name == "event"
                    && emevdInfo.Directory.Parent is DirectoryInfo eventParent
                    && eventParent.Exists)
                {
                    string projectJson = Path.Combine(eventParent.FullName, "project.json");
                    if (File.Exists(projectJson))
                    {
                        project = LoadProject(projectJson);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Invalid filename, or JSON parsing issues.
                // Generally should not happen, but this is best-effort.
            }
            return false;
        }

        private static ProjectSettingsFile LoadProject(string jsonPath)
        {
            jsonPath = Path.GetFullPath(jsonPath);
            string input = File.ReadAllText(jsonPath);
            ProjectSettings settings = JsonConvert.DeserializeObject<ProjectSettings>(input);
            ProjectSettingsFile file = new ProjectSettingsFile
            {
                Settings = settings,
                FilePath = jsonPath,
            };
            if (eventDirGames.Contains(settings.GameType))
            {
                file.ProjectEventDirectory = Path.Combine(Path.GetDirectoryName(jsonPath), "event");
                if (settings.GameRoot != null && Directory.Exists(settings.GameRoot))
                {
                    string gameDir = Path.Combine(Path.GetFullPath(settings.GameRoot), "event");
                    if (Directory.Exists(gameDir))
                    {
                        file.GameEventDirectory = gameDir;
                    }
                }
            }
            if (resourcePrefixHint.TryGetValue(settings.GameType, out string prefix))
            {
                file.ResourcePrefix = prefix;
            }
            return file;
        }
    }
}
