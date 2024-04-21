using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SoulsFormats;
using SoapstoneLib.Proto;

namespace TestConsoleApp
{
    internal class FmgData
    {
        private static readonly Dictionary<FromSoftGame, string> gamePathParts = new Dictionary<FromSoftGame, string>
        {
            [FromSoftGame.DemonsSouls] = "Demons Souls",
            [FromSoftGame.DarkSoulsPtde] = "Dark Souls Prepare to Die Edition",
            [FromSoftGame.DarkSoulsRemastered] = "DARK SOULS REMASTERED",
            [FromSoftGame.DarkSouls2] = "Dark Souls II",
            [FromSoftGame.DarkSouls2Sotfs] = "Dark Souls II Scholar of the First Sin",
            [FromSoftGame.Bloodborne] = "Bloodborne",
            [FromSoftGame.DarkSouls3] = "DARK SOULS III",
            [FromSoftGame.Sekiro] = "Sekiro",
            [FromSoftGame.EldenRing] = "ELDEN RING",
        };

        public class FmgInfo
        {
            public FromSoftGame Game { get; set; }
            public string FmgPath { get; set; }
            public string FmgName { get; set; }
            public string Category { get; set; }
            public string BinderPath { get; set; }
            public int BinderID { get; set; } = -1;

            public static string GetName(string path)
            {
                if (!path.EndsWith(".fmg")) throw new Exception(path);
                return Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            }

            public override string ToString() => BinderPath == null ? $"{Game} {FmgPath}" : $"{Game} {BinderPath} {BinderID}:{FmgPath}";
        }

        public static void Run(string[] args)
        {
            Dictionary<FromSoftGame, string> gamePaths = new Dictionary<FromSoftGame, string>();
            foreach (KeyValuePair<FromSoftGame, string> entry in gamePathParts)
            {
                string pathPart = entry.Value.ToLowerInvariant();
                string gameArg = args.Where(a => a.ToLowerInvariant().Split('\\').Contains(pathPart)).FirstOrDefault();
                if (gameArg == null)
                {
                    throw new Exception($"Path for {entry.Key} not provided: path part \"{entry.Value}\" not found");
                }
                gamePaths[entry.Key] = gameArg;
            }
            List<FmgInfo> infos = new List<FmgInfo>();
            void process(string relPath, string path, FromSoftGame game)
            {
                // Filter out non-FMGs
                if ((!relPath.ToLowerInvariant().StartsWith("msg") && !relPath.ToLowerInvariant().StartsWith(@"menu\text"))
                    // Only use dlc2 msgbnds
                    || (game == FromSoftGame.DarkSouls3 && relPath.Contains("dlc1.msgbnd"))
                    // DCX and non-DCX versions are present in DeS
                    || (game == FromSoftGame.DemonsSouls && relPath.EndsWith("msgbnd"))
                    // Japanese is not the main language in this case, jpnjp is
                    || ((game == FromSoftGame.Bloodborne || game == FromSoftGame.Sekiro) && relPath.Contains(@"\japanese\"))
                    // Don't care about these at present. The first is DeS, sellregion is BB and later
                    || relPath.EndsWith("sample.msgbnd.dcx")
                    || relPath.EndsWith("sellregion.msgbnd.dcx")
                    || relPath.EndsWith("ngword.msgbnd.dcx"))
                {
                    return;
                }
                byte[] bndBytes;
                if (path.EndsWith("bnd.dcx") && DCX.Is(path))
                {
                    bndBytes = DCX.Decompress(path);
                }
                else if (path.EndsWith("bnd"))
                {
                    bndBytes = File.ReadAllBytes(path);
                }
                else
                {
                    if (path.EndsWith(".fmg"))
                    {
                        infos.Add(new FmgInfo
                        {
                            Game = game,
                            FmgPath = relPath,
                            FmgName = FmgInfo.GetName(relPath),
                        });
                    }
                    return;
                }
                IBinder bnd;
                if (BND3.IsRead(bndBytes, out BND3 bnd3))
                {
                    bnd = bnd3;
                }
                else if (BND4.IsRead(bndBytes, out BND4 bnd4))
                {
                    bnd = bnd4;
                }
                else
                {
                    return;
                }
                foreach (BinderFile bndFile in bnd.Files)
                {
                    string fileName = bndFile.Name;
                    FmgInfo info = new FmgInfo
                    {
                        Game = game,
                        FmgPath = fileName,
                        FmgName = FmgInfo.GetName(fileName),
                        BinderPath = relPath,
                        BinderID = bndFile.ID,
                    };
                    infos.Add(info);
                }
            }
            void dirSearch(string dir, string basePath, FromSoftGame game)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName == "vanilla" || dirName == "dcx" || dirName.StartsWith("old_patch"))
                {
                    return;
                }
                foreach (string path in Directory.GetFiles(dir))
                {
                    if (!path.StartsWith(basePath))
                    {
                        throw new Exception($"Bad prefix {path}");
                    }
                    string subPath = path.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (subPath.EndsWith(".txt") && subPath.StartsWith("msg"))
                    {
                        continue;
                    }
                    process(subPath, path, game);
                }
                foreach (string subDir in Directory.GetDirectories(dir))
                {
                    dirSearch(subDir, basePath, game);
                }
            }
            foreach (KeyValuePair<FromSoftGame, string> entry in gamePaths)
            {
                if (args.Contains("key"))
                {
                    Console.WriteLine(entry.Key);
                }
                string gamePath = Path.GetFullPath(entry.Value);
                dirSearch(gamePath, gamePath, entry.Key);
            }
            List<string> cats = new List<string> { "item", "menu" };
            string capitalize(string s) => s.Substring(0, 1).ToUpperInvariant() + s.Substring(1);
            string getCategory(FmgInfo info)
            {
                if (info.BinderPath == null)
                {
                    return "none";
                }
                foreach (string cat in cats)
                {
                    if (info.BinderPath.Contains(@"\" + cat))
                    {
                        return cat;
                    }
                }
                throw new Exception($"Unknown category in {info}");
            }
            string getLanguage(FmgInfo info)
            {
                string path = info.BinderPath ?? info.FmgPath;
                string lang;
                if (path.StartsWith(@"msg\"))
                {
                    lang = path.Split('\\')[1].ToLowerInvariant();
                    if (info.Game == FromSoftGame.DemonsSouls && lang.Contains("msgbnd"))
                    {
                        lang = "japanese";
                    }
                }
                else if (path.StartsWith(@"menu\text\"))
                {
                    lang = path.Split('\\')[2].ToLowerInvariant();
                }
                else throw new Exception($"Unknown language in {info}");
                return lang;
            }
            List<string> suffixes = new List<string> { "_DLC1", "_DLC2", "_Patch" };
            string getBaseEnumName(string name)
            {
                foreach (string suffix in suffixes)
                {
                    if (name.EndsWith(suffix))
                    {
                        return name.Substring(0, name.Length - suffix.Length);
                    }
                }
                return name;
            }
            Dictionary<(int, string), string> keyNames = new();
            foreach (string text in fmgEnums)
            {
                string[] parts = text.Split('/');
                if (parts[2].Length > 0)
                {
                    keyNames[(int.Parse(parts[0]), parts[1])] = parts[2];
                }
            }
            if (args.Contains("key"))
            {
                SortedDictionary<(int, string), List<FmgInfo>> byKey = new();
                foreach (FmgInfo info in infos)
                {
                    if (info.FmgName.EndsWith("_00"))
                    {
                        continue;
                    }
                    (int, string) key = (info.BinderID, info.FmgName);
                    AddMulti(byKey, key, info);
                    Console.WriteLine(info);
                }
                string showAll<T>(List<FmgInfo> infos, Func<FmgInfo, T> func) => string.Join(", ", infos.Select(func).Distinct());
                foreach (KeyValuePair<(int, string), List<FmgInfo>> entry in byKey)
                {
                    int id = entry.Key.Item1;
                    string name = entry.Key.Item2;
                    keyNames.TryGetValue(entry.Key, out string enumName);
                    if (enumName == null)
                    {
                        foreach (string suffix in suffixes)
                        {
                            // The FMG name can be used to infer the enum name (for _dlc1 and _dlc2, which are lowercase suffixes)
                            if (name.EndsWith(suffix.ToLowerInvariant()))
                            {
                                string shortName = name.Substring(0, name.Length - suffix.Length);
                                List<string> otherNames = keyNames.Where(e => e.Key.Item2 == shortName).Select(e => e.Value).ToList();
                                if (otherNames.Count == 1)
                                {
                                    enumName = otherNames[0] + suffix;
                                    break;
                                }
                            }
                        }
                    }
                    string comment = $"{showAll(entry.Value, getCategory)} [{showAll(entry.Value, i => i.Game)}]";
                    Console.WriteLine($"\"{id}/{name}/{enumName}\", // {comment}");
                }
                Console.WriteLine();
                Dictionary<string, List<FmgInfo>> langInfos = infos.GroupBy(getLanguage).ToDictionary(e => e.Key, e => e.ToList());
                foreach (KeyValuePair<string, List<FmgInfo>> entry in langInfos)
                {
                    string comment = $"[{showAll(entry.Value, i => i.Game)}]";
                    languageEnums.TryGetValue(entry.Key, out string name);
                    Console.WriteLine($"[\"{entry.Key}\"] = \"{name}\", // {comment}");
                }
            }
            if (args.Contains("data"))
            {
                List<string> langs = new() { "Unspecified" };
                langs.AddRange(languageEnums.Values.OrderBy(s => s).Distinct());
                List<string> types = new() { "Unspecified" };
                types.AddRange(keyNames.Values.OrderBy(s => s).Distinct());
                string quote(string s) => $"\"{s}\"";
                SortedDictionary<FromSoftGame, Dictionary<string, IDictionary<string, string>>> gameData = new();
                foreach (FromSoftGame game in gamePaths.Keys)
                {
                    // This is based on FmgGameInfo. Reflection-based serialization might be a bit cleaner than this?
                    SortedDictionary<string, string> byType = new();
                    SortedDictionary<string, List<string>> overrides = new();
                    SortedDictionary<string, List<string>> byFmgName = new();
                    SortedDictionary<int, List<string>> byBinderID = new();
                    SortedDictionary<string, string> toLanguageEnum = new();
                    foreach (FmgInfo info in infos)
                    {
                        if (info.Game != game || info.FmgName.EndsWith("_00"))
                        {
                            continue;
                        }
                        (int, string) key = (info.BinderID, info.FmgName);
                        if (!keyNames.TryGetValue(key, out string type))
                        {
                            throw new Exception($"Unrecognized FMG {info}");
                        }
                        string baseType = getBaseEnumName(type);
                        string category = capitalize(getCategory(info));
                        string nameSrc = quote(info.FmgName);
                        string newSrc = $"new FmgKeyInfo(FromSoftGame.{game}, FmgCategory.{category}, FmgType.{type}, FmgType.{baseType}, {nameSrc}, {info.BinderID})";
                        string typeSrc = $"FmgType.{type}";
                        if (byType.TryGetValue(typeSrc, out string existNew))
                        {
                            if (newSrc != existNew)
                            {
                                throw new Exception($"Mismatched constructors per type: {newSrc} vs {existNew}");
                            }
                        }
                        byType[typeSrc] = newSrc;
                        string refSrc = typeSrc;
                        if (type != baseType)
                        {
                            AddMulti(overrides, $"FmgType.{baseType}", refSrc);
                        }
                        AddMulti(byFmgName, nameSrc, refSrc);
                        if (info.BinderID >= 0)
                        {
                            AddMulti(byBinderID, info.BinderID, refSrc);
                        }
                        string lang = getLanguage(info);
                        toLanguageEnum[quote(lang)] = $"FmgLanguage.{languageEnums[lang]}";
                    }
                    if (game == FromSoftGame.DarkSouls2 || game == FromSoftGame.DarkSouls2Sotfs)
                    {
                        // Global version of Steam DS2 doesn't have Japanese text, so add it here artificially
                        // Still, it won't be available for many modders.
                        toLanguageEnum[quote("japanese")] = "FmgLanguage.Japanese";
                    }
                    string refList(IEnumerable<string> refs) => $"new List<FmgType> {{ {string.Join(", ", refs.Distinct())} }}";
                    gameData[game] = new()
                    {
                        ["ByType"] = byType,
                        ["Overrides"] = overrides.ToDictionary(e => e.Key, e => refList(Enumerable.Reverse(e.Value))),
                        ["ByFmgName"] = byFmgName.ToDictionary(e => e.Key, e => refList(e.Value)),
                        ["ByBinderID"] = byBinderID.ToDictionary(e => e.Key.ToString(), e => refList(e.Value)),
                        ["ToLanguageEnum"] = toLanguageEnum,
                        ["FromLanguageEnum"] = toLanguageEnum.ToDictionary(e => e.Value, e => e.Key),
                    };
                }
                string indent = "            ";
                string indent2 = "    " + indent;
                string indent3 = "    " + indent2;
                Console.Write(@$"using System.Collections.Generic;
using SoapstoneLib.Proto;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// This file is autogenerated by TestConsoleApp.
// DO NOT MANUALLY EDIT THIS FILE.

namespace SoapstoneLib
{{
    public static partial class SoulsFmg
    {{
        /// <summary>
        /// The language categorization for any FMG in any supported game.
        /// 
        /// This corresponds to a language folder in the game files.
        /// 
        /// Do not rely on the integer value of this enum, as it may change over time. Only use the name.
        /// </summary>
        public enum FmgLanguage
        {{
{string.Join("\n", langs.Select(s => $"{indent}{s},"))}
        }}

        /// <summary>
        /// Type specifier for any item or menu FMG in any supported game.
        /// 
        /// Each FMG file within a game has a unique type, within any given language.
        /// 
        /// Do not rely on the integer value of this enum, as it may change over time. Only use the name.
        /// </summary>
        public enum FmgType
        {{
{string.Join("\n", types.Select(s => $"{indent}{s},"))}
        }}

        internal static bool TryGetFmgGameInfo(FromSoftGame game, out FmgGameInfo info)
        {{
            if (fmgGames == null)
            {{
                fmgGames = MakeFmgGames();
            }}
            return fmgGames.TryGetValue(game, out info);
        }}

        // Lazily initialize this to keep things lightweight if FMG is not needed.
        // Not synchronizing initialization should be fine.
        private static Dictionary<FromSoftGame, FmgGameInfo> fmgGames;
        private static Dictionary<FromSoftGame, FmgGameInfo> MakeFmgGames() => new()
        {{
{string.Join("\n", gameData.Select(e => @$"{indent}[FromSoftGame.{e.Key}] = new FmgGameInfo
{indent}{{
{string.Join("\n", e.Value.Select(e2 => @$"{indent2}{e2.Key} = new()
{indent2}{{
{string.Join("\n", e2.Value.Select(e3 => @$"{indent3}[{e3.Key}] = {e3.Value},"))}
{indent2}}},"))}
{indent}}},"))}
        }};
    }}
}}
".Replace("\r\n", "\n"));
            }
        }

        private static void AddMulti<K, V, T>(IDictionary<K, T> dict, K key, V value) where T : ICollection<V>, new()
        {
            if (!dict.TryGetValue(key, out T col))
            {
                dict[key] = col = new T();
            }
            col.Add(value);
        }

        private static readonly Dictionary<string, string> languageEnums = new()
        {
            ["japanese"] = "Japanese", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered, Bloodborne, Sekiro]
            ["asia_english"] = "AsiaEnglish", // [DemonsSouls]
            ["french"] = "French", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["german"] = "German", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered]
            ["italian"] = "Italian", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["korean"] = "Korean", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["na_english"] = "English", // [DemonsSouls]
            ["spanish"] = "SpainSpanish", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["tchinese"] = "TraditionalChinese", // [DemonsSouls, DarkSoulsPtde, DarkSoulsRemastered]
            ["uk_english"] = "BritishEnglish", // [DemonsSouls]
            ["english"] = "English", // [DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["polish"] = "Polish", // [DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["russian"] = "Russian", // [DarkSoulsPtde, DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["nspanish"] = "Spanish", // [DarkSoulsRemastered]
            ["portuguese"] = "BrazilPortuguese", // [DarkSoulsRemastered, DarkSouls2, DarkSouls2Sotfs]
            ["schinese"] = "SimplifiedChinese", // [DarkSoulsRemastered]
            ["chinese"] = "TraditionalChinese", // [DarkSouls2, DarkSouls2Sotfs]
            ["germany"] = "German", // [DarkSouls2, DarkSouls2Sotfs]
            ["neutralspanish"] = "Spanish", // [DarkSouls2Sotfs]
            ["dandk"] = "Danish", // [Bloodborne]
            ["deude"] = "German", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["enggb"] = "BritishEnglish", // [Bloodborne]
            ["engus"] = "English", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["finfi"] = "Finnish", // [Bloodborne]
            ["frafr"] = "French", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["itait"] = "Italian", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["jpnjp"] = "Japanese", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["korkr"] = "Korean", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["nldnl"] = "Dutch", // [Bloodborne]
            ["norno"] = "Norwegian", // [Bloodborne]
            ["polpl"] = "Polish", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["porbr"] = "BrazilPortuguese", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["porpt"] = "PortugalPortuguese", // [Bloodborne]
            ["rusru"] = "Russian", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["spaar"] = "Spanish", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["spaes"] = "SpainSpanish", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["swese"] = "Swedish", // [Bloodborne]
            ["turtr"] = "Turkish", // [Bloodborne]
            ["zhocn"] = "SimplifiedChinese", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["zhotw"] = "TraditionalChinese", // [Bloodborne, DarkSouls3, Sekiro, EldenRing]
            ["thath"] = "Thai", // [Sekiro, EldenRing]
        };

        private static readonly List<string> fmgEnums = new()
        {
            "-1/bloodmessageconjunction/BloodMsgConjunction", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/bloodmessagesentence/BloodMsgSentence", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/bloodmessageword/BloodMsgWord", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/bloodmessagewordcategory/BloodMsgWordCategory", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/bofire/BonfireMenu", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/bonfirename/BonfireName", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/charamaking/CharaMaking", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/charaname/CharaName", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/common/DS2Common", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/dconlymessage/SystemMessageDC", // none [DarkSouls2Sotfs]
            "-1/detailedexplanation/ItemCaption", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/iconhelp/IconHelp", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/ingamemenu/InGameMenu", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/ingamesystem/InGameSystem", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/itemname/ItemName", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/keyguide/KeyGuide", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/mapevent/EventTextForMap", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/mapname/PlaceName", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/npcmenu/NpcMenu", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/pluralselect/PluralSelect", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/prologue/Prologue", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/shop/DS2Shop", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/simpleexplanation/ItemInfo", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/staffroll/StaffRoll", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/titleflow/TitleFlow", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/titlemenu/TitleMenu", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/weapontype/WeaponType", // none [DarkSouls2, DarkSouls2Sotfs]
            "-1/win32onlymessage/SystemMessageWindows", // none [DarkSouls2, DarkSouls2Sotfs]
            "1/Conversation_/TalkMsg", // menu [DarkSoulsRemastered]
            "1/TalkMsg/TalkMsg", // menu [EldenRing]
            "1/会話/TalkMsg", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "2/Blood_writing_/BloodMsg", // menu [DarkSoulsRemastered]
            "2/BloodMsg/BloodMsg", // menu [EldenRing]
            "2/血文字/BloodMsg", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "3/Movie_subtitles_/MovieSubtitle", // menu [DarkSoulsRemastered]
            "3/MovieSubtitle/MovieSubtitle", // menu [EldenRing]
            "3/ムービー字幕/MovieSubtitle", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "4/TalkMsg_FemalePC_Alt/TalkMsgFemalePCAlt", // menu [EldenRing]
            "4/死因/CauseOfDeath", // menu [Bloodborne]
            "10/GoodsName/GoodsName", // item [EldenRing]
            "10/Item_name_/GoodsName", // item [DarkSoulsRemastered]
            "10/アイテム名/GoodsName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "11/Weapon_name_/WeaponName", // item [DarkSoulsRemastered]
            "11/WeaponName/WeaponName", // item [EldenRing]
            "11/武器名/WeaponName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "12/Armor_name_/ProtectorName", // item [DarkSoulsRemastered]
            "12/ProtectorName/ProtectorName", // item [EldenRing]
            "12/防具名/ProtectorName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "13/Accessory_name_/AccessoryName", // item [DarkSoulsRemastered]
            "13/AccessoryName/AccessoryName", // item [EldenRing]
            "13/アクセサリ名/AccessoryName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "14/Magic_name_/MagicName", // item [DarkSoulsRemastered]
            "14/MagicName/MagicName", // item [EldenRing]
            "14/魔法名/MagicName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "15/Feature_name_/FeatureName", // item [DarkSoulsRemastered]
            "15/特徴名/FeatureName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne]
            "16/Feature_description_/FeatureInfo", // item [DarkSoulsRemastered]
            "16/特徴説明/FeatureInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne]
            "17/Feature_long_desc_/FeatureCaption", // item [DarkSoulsRemastered]
            "17/特徴うんちく/FeatureCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne]
            "18/NPC_name_/NpcName", // item [DarkSoulsRemastered]
            "18/NpcName/NpcName", // item [EldenRing]
            "18/NPC名/NpcName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "19/Place_name_/PlaceName", // item [DarkSoulsRemastered]
            "19/PlaceName/PlaceName", // item [EldenRing]
            "19/地名/PlaceName", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "20/GoodsInfo/GoodsInfo", // item [EldenRing]
            "20/Item_description_/GoodsInfo", // item [DarkSoulsRemastered]
            "20/アイテム説明/GoodsInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "21/Weapon_description_/WeaponInfo", // item [DarkSoulsRemastered]
            "21/WeaponInfo/WeaponInfo", // item [EldenRing]
            "21/武器説明/WeaponInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "22/Armor_description_/ProtectorInfo", // item [DarkSoulsRemastered]
            "22/ProtectorInfo/ProtectorInfo", // item [EldenRing]
            "22/防具説明/ProtectorInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "23/Accessory_description_/AccessoryInfo", // item [DarkSoulsRemastered]
            "23/AccessoryInfo/AccessoryInfo", // item [EldenRing]
            "23/アクセサリ説明/AccessoryInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "24/GoodsCaption/GoodsCaption", // item [EldenRing]
            "24/Item_long_desc_/GoodsCaption", // item [DarkSoulsRemastered]
            "24/アイテムうんちく/GoodsCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "25/Weapon_long_desc_/WeaponCaption", // item [DarkSoulsRemastered]
            "25/WeaponCaption/WeaponCaption", // item [EldenRing]
            "25/武器うんちく/WeaponCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "26/Armor_long_desc_/ProtectorCaption", // item [DarkSoulsRemastered]
            "26/ProtectorCaption/ProtectorCaption", // item [EldenRing]
            "26/防具うんちく/ProtectorCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "27/Accessory_long_desc_/AccessoryCaption", // item [DarkSoulsRemastered]
            "27/AccessoryCaption/AccessoryCaption", // item [EldenRing]
            "27/アクセサリうんちく/AccessoryCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "28/Magic_description_/MagicInfo", // item [DarkSoulsRemastered]
            "28/MagicInfo/MagicInfo", // item [EldenRing]
            "28/魔法説明/MagicInfo", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "29/Magic_long_desc_/MagicCaption", // item [DarkSoulsRemastered]
            "29/MagicCaption/MagicCaption", // item [EldenRing]
            "29/魔法うんちく/MagicCaption", // item [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "30/Event_text_/EventText", // menu [DarkSoulsRemastered]
            "30/イベントテキスト/EventText", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "31/NetworkMessage/NetworkMessage", // menu [EldenRing]
            "31/魔石名/GemName", // menu [Bloodborne]
            "32/ActionButtonText/ActionButtonText", // menu [EldenRing]
            "32/魔石説明/GemInfo", // menu [Bloodborne]
            "33/EventTextForTalk/EventTextForTalk", // menu [EldenRing]
            "33/魔石うんちく/GemCaption", // menu [Bloodborne]
            "34/EventTextForMap/EventTextForMap", // menu [EldenRing]
            "34/魔石接頭語/GemPrefix", // menu [Bloodborne]
            "35/GemName/GemName", // item [EldenRing]
            "35/魔石効果/GemEffect", // menu [Bloodborne]
            "36/GemInfo/GemInfo", // item [EldenRing]
            "37/GemCaption/GemCaption", // item [EldenRing]
            "40/戦技種別/Skills", // item [DarkSouls3, Sekiro]
            "41/GoodsDialog/GoodsDialog", // item [EldenRing]
            "42/ArtsName/ArtsName", // item [EldenRing]
            "43/ArtsCaption/ArtsCaption", // item [EldenRing]
            "44/WeaponEffect/WeaponEffect", // item [EldenRing]
            "45/GemEffect/GemEffect", // item [EldenRing]
            "46/GoodsInfo2/GoodsInfo2", // item [EldenRing]
            "70/Ingame_menu_/InGameMenu", // menu [DarkSoulsRemastered]
            "70/インゲームメニュー/InGameMenu", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "76/Menu_general_text_/MenuGeneralText", // menu [DarkSoulsRemastered]
            "76/メニュー共通テキスト/MenuGeneralText", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "77/Menu_others_/MenuOther", // menu [DarkSoulsRemastered]
            "77/メニューその他/MenuOther", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "78/Dialogue_/Dialogues", // menu [DarkSoulsRemastered]
            "78/ダイアログ/Dialogues", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "79/Key_guide_/KeyGuide", // menu [DarkSoulsRemastered]
            "79/キーガイド/KeyGuide", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "80/Single_line_help_/LineHelp", // menu [DarkSoulsRemastered]
            "80/一行ヘルプ/LineHelp", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "81/Item_help_/MenuContext", // menu [DarkSoulsRemastered]
            "81/項目ヘルプ/MenuContext", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "90/Text_display_tag_list_/TextDisplayTagList", // menu [DarkSoulsRemastered]
            "90/テキスト表示用タグ一覧/TextDisplayTagList", // menu [DemonsSouls, DarkSoulsPtde, Bloodborne, DarkSouls3, Sekiro]
            "91/System_specific_tags_win32_/SystemTagsWindows", // menu [DarkSoulsRemastered]
            "91/機種別タグ_win32/SystemTagsWindows", // menu [DarkSoulsPtde]
            "91/機種別タグ_win64/SystemTagsWindows", // menu [Bloodborne]
            "92/System_message_win32_/SystemMessageWindows", // menu [DarkSoulsRemastered]
            "92/システムメッセージ_win32/SystemMessageWindows", // menu [DarkSoulsPtde]
            "92/システムメッセージ_win64/SystemMessageWindows", // menu [Bloodborne, Sekiro]
            "100/Item_long_desc_/GoodsCaption_Patch", // item [DarkSoulsRemastered]
            "100/アイテムうんちくパッチ/GoodsCaption_Patch", // menu [DarkSoulsPtde]
            "101/Event_text_/EventText_Patch", // menu [DarkSoulsRemastered]
            "101/イベントテキストパッチ/EventText_Patch", // menu [DarkSoulsPtde]
            "102/Dialogue_/Dialogues_Patch", // menu [DarkSoulsRemastered]
            "102/ダイアログパッチ/Dialogues_Patch", // menu [DarkSoulsPtde]
            "103/System_message_win32_/SystemMessageWindows_Patch", // menu [DarkSoulsRemastered]
            "103/システムメッセージ_win32パッチ/SystemMessageWindows_Patch", // menu [DarkSoulsPtde]
            "104/Conversation_/TalkMsg_Patch", // menu [DarkSoulsRemastered]
            "104/会話パッチ/TalkMsg_Patch", // menu [DarkSoulsPtde]
            "105/Magic_long_desc_/MagicCaption_Patch", // item [DarkSoulsRemastered]
            "105/魔法うんちくパッチ/MagicCaption_Patch", // menu [DarkSoulsPtde]
            "106/Weapon_long_desc_/WeaponCaption_Patch", // item [DarkSoulsRemastered]
            "106/武器うんちくパッチ/WeaponCaption_Patch", // menu [DarkSoulsPtde]
            "107/Blood_writing_/BloodMsg_Patch", // menu [DarkSoulsRemastered]
            "107/血文字パッチ/BloodMsg_Patch", // menu [DarkSoulsPtde]
            "108/Armor_long_desc_/ProtectorCaption_Patch", // item [DarkSoulsRemastered]
            "108/防具うんちくパッチ/ProtectorCaption_Patch", // menu [DarkSoulsPtde]
            "109/Accessory_long_desc_/AccessoryCaption_Patch", // item [DarkSoulsRemastered]
            "109/アクセサリうんちくパッチ/AccessoryCaption_Patch", // menu [DarkSoulsPtde]
            "110/Item_description_/GoodsInfo_Patch", // item [DarkSoulsRemastered]
            "110/アイテム説明パッチ/GoodsInfo_Patch", // menu [DarkSoulsPtde]
            "111/Item_name_/GoodsName_Patch", // item [DarkSoulsRemastered]
            "111/アイテム名パッチ/GoodsName_Patch", // menu [DarkSoulsPtde]
            "112/Accessory_description_/AccessoryInfo_Patch", // item [DarkSoulsRemastered]
            "112/アクセサリ説明パッチ/AccessoryInfo_Patch", // menu [DarkSoulsPtde]
            "113/Accessory_name_/AccessoryName_Patch", // item [DarkSoulsRemastered]
            "113/アクセサリ名パッチ/AccessoryName_Patch", // menu [DarkSoulsPtde]
            "114/Weapon_description_/WeaponInfo_Patch", // item [DarkSoulsRemastered]
            "114/武器説明パッチ/WeaponInfo_Patch", // menu [DarkSoulsPtde]
            "115/Weapon_name_/WeaponName_Patch", // item [DarkSoulsRemastered]
            "115/武器名パッチ/WeaponName_Patch", // menu [DarkSoulsPtde]
            "116/Armor_description_/ProtectorInfo_Patch", // item [DarkSoulsRemastered]
            "116/防具説明パッチ/ProtectorInfo_Patch", // menu [DarkSoulsPtde]
            "117/Armor_name_/ProtectorName_Patch", // item [DarkSoulsRemastered]
            "117/防具名パッチ/ProtectorName_Patch", // menu [DarkSoulsPtde]
            "118/Magic_name_/MagicName_Patch", // item [DarkSoulsRemastered]
            "118/魔法名パッチ/MagicName_Patch", // menu [DarkSoulsPtde]
            "119/NPC_name_/NpcName_Patch", // item [DarkSoulsRemastered]
            "119/NPC名パッチ/NpcName_Patch", // menu [DarkSoulsPtde]
            "120/Place_name_/PlaceName_Patch", // item [DarkSoulsRemastered]
            "120/地名パッチ/PlaceName_Patch", // menu [DarkSoulsPtde]
            "121/Single_line_help_/LineHelp_Patch", // menu [DarkSoulsRemastered]
            "121/一行ヘルプパッチ/LineHelp_Patch", // menu [DarkSoulsPtde]
            "122/Key_guide_/KeyGuide_Patch", // menu [DarkSoulsRemastered]
            "122/キーガイドパッチ/KeyGuide_Patch", // menu [DarkSoulsPtde]
            "123/Menu_others_/MenuOther_Patch", // menu [DarkSoulsRemastered]
            "123/メニューその他パッチ/MenuOther_Patch", // menu [DarkSoulsPtde]
            "124/Menu_general_text_/MenuGeneralText_Patch", // menu [DarkSoulsRemastered]
            "124/メニュー共通テキストパッチ/MenuGeneralText_Patch", // menu [DarkSoulsPtde]
            "200/FDP_メニューテキスト/GameMenuText", // menu [DarkSouls3, Sekiro]
            "200/GR_MenuText/GameMenuText", // menu [EldenRing]
            "200/NTC_メニューテキスト/GameMenuText", // menu [Sekiro]
            "200/SP_メニューテキスト/GameMenuText", // menu [Bloodborne]
            "201/FDP_一行ヘルプ/GameLineHelp", // menu [DarkSouls3, Sekiro]
            "201/GR_LineHelp/GameLineHelp", // menu [EldenRing]
            "201/NTC_一行ヘルプ/GameLineHelp", // menu [Sekiro]
            "201/SP_一行ヘルプ/GameLineHelp", // menu [Bloodborne]
            "202/FDP_キーガイド/GameKeyGuide", // menu [DarkSouls3, Sekiro]
            "202/GR_KeyGuide/GameKeyGuide", // menu [EldenRing]
            "202/NTC_キーガイド/GameKeyGuide", // menu [Sekiro]
            "202/SP_キーガイド/GameKeyGuide", // menu [Bloodborne]
            "203/FDP_システムメッセージ_win64/GameSystemMessageWindows", // menu [DarkSouls3, Sekiro]
            "203/GR_System_Message_win64/GameSystemMessageWindows", // menu [EldenRing]
            "203/NTC_システムメッセージ_win64/GameSystemMessageWindows", // menu [Sekiro]
            "203/SP_システムメッセージ_win64/GameSystemMessageWindows", // menu [Bloodborne]
            "204/FDP_ダイアログ/GameDialogues", // menu [DarkSouls3, Sekiro]
            "204/GR_Dialogues/GameDialogues", // menu [EldenRing]
            "204/NTC_ダイアログ/GameDialogues", // menu [Sekiro]
            "204/SP_ダイアログ/GameDialogues", // menu [Bloodborne]
            "205/FDP_システムメッセージ_ps4/GameSystemMessagePS4", // menu [DarkSouls3]
            "205/LoadingTitle/LoadingTitle", // menu [EldenRing]
            "205/ローディングテキスト/LoadingText", // menu [Sekiro]
            "206/FDP_システムメッセージ_xboxone/GameSystemMessageXboxOne", // menu [DarkSouls3]
            "206/LoadingText/LoadingText", // menu [EldenRing]
            "206/ローディングタイトル/LoadingTitle", // menu [Sekiro]
            "207/TutorialTitle/TutorialTitle", // menu [EldenRing]
            "208/TutorialBody/TutorialBody", // menu [EldenRing]
            "209/TextEmbedImageName_win64/TextEmbedImageNameWindows", // menu [EldenRing]
            "210/ToS_win64/TosWindows", // menu [EldenRing]
            "210/アイテム名_dlc1/GoodsName_DLC1", // item [DarkSouls3]
            "211/武器名_dlc1/WeaponName_DLC1", // item [DarkSouls3]
            "212/防具名_dlc1/ProtectorName_DLC1", // item [DarkSouls3]
            "213/アクセサリ名_dlc1/AccessoryName_DLC1", // item [DarkSouls3]
            "214/魔法名_dlc1/MagicName_DLC1", // item [DarkSouls3]
            "215/NPC名_dlc1/NpcName_DLC1", // item [DarkSouls3]
            "216/地名_dlc1/PlaceName_DLC1", // item [DarkSouls3]
            "217/アイテム説明_dlc1/GoodsInfo_DLC1", // item [DarkSouls3]
            "220/アクセサリ説明_dlc1/AccessoryInfo_DLC1", // item [DarkSouls3]
            "221/アイテムうんちく_dlc1/GoodsCaption_DLC1", // item [DarkSouls3]
            "222/武器うんちく_dlc1/WeaponCaption_DLC1", // item [DarkSouls3]
            "223/防具うんちく_dlc1/ProtectorCaption_DLC1", // item [DarkSouls3]
            "224/アクセサリうんちく_dlc1/AccessoryCaption_DLC1", // item [DarkSouls3]
            "225/魔法説明_dlc1/MagicInfo_DLC1", // item [DarkSouls3]
            "226/魔法うんちく_dlc1/MagicCaption_DLC1", // item [DarkSouls3]
            "230/会話_dlc1/TalkMsg_DLC1", // menu [DarkSouls3]
            "231/イベントテキスト_dlc1/EventText_DLC1", // menu [DarkSouls3]
            "232/FDP_メニューテキスト_dlc1/GameMenuText_DLC1", // menu [DarkSouls3]
            "233/FDP_一行ヘルプ_dlc1/GameLineHelp_DLC1", // menu [DarkSouls3]
            "235/FDP_システムメッセージ_win64_dlc1/GameSystemMessageWindows_DLC1", // menu [DarkSouls3]
            "236/FDP_ダイアログ_dlc1/GameDialogues_DLC1", // menu [DarkSouls3]
            "237/FDP_システムメッセージ_ps4_dlc1/GameSystemMessagePS4_DLC1", // menu [DarkSouls3]
            "238/FDP_システムメッセージ_xboxone_dlc1/GameSystemMessageXboxOne_DLC1", // menu [DarkSouls3]
            "239/血文字_dlc1/BloodMsg_DLC1", // menu [DarkSouls3]
            "250/アイテム名_dlc2/GoodsName_DLC2", // item [DarkSouls3]
            "251/武器名_dlc2/WeaponName_DLC2", // item [DarkSouls3]
            "252/防具名_dlc2/ProtectorName_DLC2", // item [DarkSouls3]
            "253/アクセサリ名_dlc2/AccessoryName_DLC2", // item [DarkSouls3]
            "254/魔法名_dlc2/MagicName_DLC2", // item [DarkSouls3]
            "255/NPC名_dlc2/NpcName_DLC2", // item [DarkSouls3]
            "256/地名_dlc2/PlaceName_DLC2", // item [DarkSouls3]
            "257/アイテム説明_dlc2/GoodsInfo_DLC2", // item [DarkSouls3]
            "260/アクセサリ説明_dlc2/AccessoryInfo_DLC2", // item [DarkSouls3]
            "261/アイテムうんちく_dlc2/GoodsCaption_DLC2", // item [DarkSouls3]
            "262/武器うんちく_dlc2/WeaponCaption_DLC2", // item [DarkSouls3]
            "263/防具うんちく_dlc2/ProtectorCaption_DLC2", // item [DarkSouls3]
            "264/アクセサリうんちく_dlc2/AccessoryCaption_DLC2", // item [DarkSouls3]
            "265/魔法説明_dlc2/MagicInfo_DLC2", // item [DarkSouls3]
            "266/魔法うんちく_dlc2/MagicCaption_DLC2", // item [DarkSouls3]
            "270/会話_dlc2/TalkMsg_DLC2", // menu [DarkSouls3]
            "271/イベントテキスト_dlc2/EventText_DLC2", // menu [DarkSouls3]
            "272/FDP_メニューテキスト_dlc2/GameMenuText_DLC2", // menu [DarkSouls3]
            "273/FDP_一行ヘルプ_dlc2/GameLineHelp_DLC2", // menu [DarkSouls3]
            "275/FDP_システムメッセージ_win64_dlc2/GameSystemMessageWindows_DLC2", // menu [DarkSouls3]
            "276/FDP_ダイアログ_dlc2/GameDialogues_DLC2", // menu [DarkSouls3]
            "277/FDP_システムメッセージ_ps4_dlc2/GameSystemMessagePS4_DLC2", // menu [DarkSouls3]
            "278/FDP_システムメッセージ_xboxone_dlc2/GameSystemMessageXboxOne_DLC2", // menu [DarkSouls3]
            "279/血文字_dlc2/BloodMsg_DLC2", // menu [DarkSouls3]
        };
    }
}
