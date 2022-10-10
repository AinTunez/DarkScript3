using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SoapstoneLib;
using SoapstoneLib.Proto;
using static DarkScript3.DocAutocomplete;

namespace DarkScript3
{
    public class SoapstoneMetadata : IDisposable
    {
        private static readonly int checkIntervalMs = 5000;
        private readonly NameMetadata nameMetadata;
        private readonly SoapstoneClient.Provider provider;
        private readonly CancellationTokenSource cancelSource;

        private volatile InnerData currentData;
        private volatile bool resetData;

        public SoapstoneMetadata(NameMetadata nameMetadata)
        {
            this.nameMetadata = nameMetadata;
            provider = SoapstoneClient.GetProvider();
            cancelSource = new CancellationTokenSource();

            // Functionality to provide
            // Auto-complete (asynchronous?)
            // Asynchronous hover

            // For type data
            // If it doesn't contain "ID", the name may need to be simplified.
            // Left-Hand Side -> mode, Target Frames Min -> min frames, removing any left/right generally (probably keep min/max)
            // No direct autocomplete for it probably in that case (ObjAct Event Flag - needs ID probably - check in all games)
            // "State Info" could do a search of sorts. probably should also have ID
            // There are multi-part ids - like (enum type + item id), (map id + block id + etc)
            // Entity ID should try to distinguish part types
            State = ClientState.Uninitialized;
        }

        // Basic state-tracking.
        public enum ClientState
        {
            // Not trying to fetch metadata
            Uninitialized,
            // Trying to fetch in a loop
            Open,
            // Also in a loop, but temporarily not looking to fetch
            Closed,
            // Loop is terminated
            Disposed,
        }
        public ClientState State { get; private set; }

        public bool IsOpen() => State == ClientState.Open;
        public bool IsOpenable() => State != ClientState.Disposed;

        // For informational purposes. May be null.
        public int? LastPort => provider.LastPort;
        public string CurrentGameString => currentData?.CurrentGame.ToString();
        public string LastLoopResult { get; private set; }

        // This methods should not be used reentrantly, but slap some synchronization on to be extra safe.
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Open()
        {
            if (State == ClientState.Disposed)
            {
                throw new InvalidOperationException("Internal error: metadata client is already closed");
            }
            if (State == ClientState.Uninitialized)
            {
                _ = BackgroundFetchLoop();
                State = ClientState.Closed;
            }
            if (State == ClientState.Closed)
            {
                provider.Server = KnownServer.DSMapStudio;
                State = ClientState.Open;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (State == ClientState.Disposed)
            {
                throw new InvalidOperationException("Internal error: metadata client is already closed");
            }
            if (State == ClientState.Open)
            {
                provider.Server = null;
                State = ClientState.Closed;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            provider.Server = null;
            cancelSource.Cancel();
            State = ClientState.Disposed;
        }

        public void ResetData()
        {
            currentData = null;
            resetData = true;
        }

        private class InnerData
        {
            public int DataVersion { get; set; }
            // Assume a single instance of DSMS, but do at least check the games are compatible to use the data in emevd
            public FromSoftGame CurrentGame { get; set; }
            // Most recently seen FMG language, if FMGs are loaded
            public string CurrentLanguage { get; set; }
            // This should always be present
            public SortedSet<string> FullyLoadedMaps { get; set; } = new SortedSet<string>();
            // Map from entity id to (type, description)
            // This could use other namespaces, like objact event flags.
            public Dictionary<int, EntityData> MapEntities = new Dictionary<int, EntityData>();
            // Allow autocomplete items to be shared across all editors.
            // Processing happens in the UI thread so this should be safe.
            public List<DocAutocompleteItem> MapItems = new List<DocAutocompleteItem>();
            // Cached mapping from multi-type names to entities, currently using enum name as a key.
            public ConcurrentDictionary<string, List<DocAutocompleteItem>> MultiMapItems =
                new ConcurrentDictionary<string, List<DocAutocompleteItem>>();
            // Cached mapping from param name to ids
            public ConcurrentDictionary<string, Dictionary<int, EntryData>> ParamRowNames =
                new ConcurrentDictionary<string, Dictionary<int, EntryData>>();
            // Cached mapping from param name to items
            public ConcurrentDictionary<string, List<DocAutocompleteItem>> ParamRowItems =
                new ConcurrentDictionary<string, List<DocAutocompleteItem>>();
            // Cached mapping from multi-param names to items, currently using enum name as a key.
            public ConcurrentDictionary<string, List<DocAutocompleteItem>> MultiParamItems =
                new ConcurrentDictionary<string, List<DocAutocompleteItem>>();
            // Cached mapping from FMG enum to names
            public ConcurrentDictionary<string, Dictionary<int, EntryData>> FmgEntryNames =
                new ConcurrentDictionary<string, Dictionary<int, EntryData>>();
            // Cached mapping from FMG enum to items
            public ConcurrentDictionary<string, List<DocAutocompleteItem>> FmgEntryItems =
                new ConcurrentDictionary<string, List<DocAutocompleteItem>>();
            // Combine autocomplete items and lookup items together for map names.
            // Construct it lazily. It can only go from a null state to a non-null state.
            public SortedDictionary<string, DocAutocompleteItem> MapIntItems;
            public SortedDictionary<string, DocAutocompleteItem> MapPartsItems;

            public bool IsGameCompatible(string game)
            {
                // Somewhat lenient game-checking.
                switch (CurrentGame)
                {
                    case FromSoftGame.DarkSoulsPtde:
                        return game == "ds1";
                    case FromSoftGame.DarkSoulsRemastered:
                        return game == "ds1";
                    case FromSoftGame.DarkSouls2:
                        return game == "ds2" || game == "ds2scholar";
                    case FromSoftGame.DarkSouls2Sotfs:
                        return game == "ds2" || game == "ds2scholar";
                    case FromSoftGame.Bloodborne:
                        return game == "bb";
                    case FromSoftGame.DarkSouls3:
                        return game == "ds3";
                    case FromSoftGame.Sekiro:
                        return game == "sekiro";
                    case FromSoftGame.EldenRing:
                        return game == "er";
                }
                return false;
            }

            public string GetGameString()
            {
                // Used for finding NameMetadata
                switch (CurrentGame)
                {
                    case FromSoftGame.DarkSoulsPtde:
                        return "ds1";
                    case FromSoftGame.DarkSoulsRemastered:
                        return "ds1";
                    case FromSoftGame.DarkSouls2:
                        // Currently no names here, but scholar is generally a superset
                        return "ds2scholar";
                    case FromSoftGame.DarkSouls2Sotfs:
                        return "ds2scholar";
                    case FromSoftGame.Bloodborne:
                        return "bb";
                    case FromSoftGame.DarkSouls3:
                        return "ds3";
                    case FromSoftGame.Sekiro:
                        return "sekiro";
                    case FromSoftGame.EldenRing:
                        return "er";
                }
                return null;
            }
        }

        public class DisplayData
        {
            // Text to show in autocomplete and tooltip
            public string Desc { get; set; }
            // Additional text to type to autocomplete this entry, aside from the main id itself
            public List<string> MatchText = new List<string>();
        }

        private static byte[] ParseMap(string map)
        {
            try
            {
                byte[] parts = map.TrimStart('m').Split('_').Select(p => byte.Parse(p)).ToArray();
                if (parts.Length == 4)
                {
                    return parts;
                }
            }
            catch (Exception) { }
            return null;
        }

        // For lookup purposes, default -1 (meaning wildcard) to 0
        // To resolve wildcards, could instead match against the full map list.
        private static string FormatMap(IEnumerable<int> parts) =>
            "m" + string.Join("_", parts.Select(p => p == -1 ? "00" : $"{p:d2}"));

        // For any Elden Ring overworld emevd map, its big/medium parents.
        // It would also be possible to use the reverse mapping (the children of any big map), but
        // this would take up more space and probably more expensive if those maps are open.
        private static readonly IDictionary<string, string[]> parentMaps = new ConcurrentDictionary<string, string[]>();
        private static bool IsMapCompatible(string emevdMap, string entityMap)
        {
            // This unconditionally shows 10000 type entities (but partly prevented in metadata loop, for now).
            if (emevdMap == null || entityMap == null)
            {
                return entityMap == null;
            }
            if (emevdMap == entityMap)
            {
                return true;
            }
            if (emevdMap.StartsWith("m60") && entityMap.StartsWith("m60") && !entityMap.EndsWith("00"))
            {
                if (!parentMaps.TryGetValue(emevdMap, out string[] parents))
                {
                    byte[] parts = ParseMap(emevdMap);
                    if (parts != null && parts[0] == 60 && parts[3] == 0)
                    {
                        parents = new string[]
                        {
                            $"m60_{parts[1] / 4:d2}_{parts[2] / 4:d2}_02",
                            $"m60_{parts[1] / 2:d2}_{parts[2] / 2:d2}_01",
                        };
                    }
                    parentMaps[emevdMap] = parents;
                }
                if (parents != null)
                {
                    return parents.Contains(entityMap);
                }
            }
            return false;
        }

        private static readonly int minEntityId = 1000000;
        private static readonly Dictionary<int, string> selfNames = new Dictionary<int, string>
        {
            [10000] = "Player",
            [20000] = "Player",
            [35000] = "Spirit Summons",
            [40000] = "Torrent",
        };
        public class EntityData : DisplayData
        {
            // Entity ID, mainly for when this is given to ToolControl for opening purposes.
            public int ID { get; set; }
            // This field should not be accessed outside of SoapstoneMetadata (use other accessor methods)
            // It refers to the entry itself, or a representative entry in the case of a group.
            internal SoulsKey.MsbEntryKey Key { get; set; }
            // Entity's map, for filtering based on the event file
            public string Map => Key?.File.Map;
            // Additional filtering e.g. for enemy/generator ids depending on EMEDF annotation
            public string Type { get; set; }
            // Overall namespace for filtering
            public string Namespace { get; set; }

            public bool IsCompatible(string emevdMap, IReadOnlyCollection<string> types = null)
            {
                if (!IsMapCompatible(emevdMap, Map))
                {
                    // Support multi-level maps in Elden Ring
                    // There should not be many eligible entries in high-level maps.
                    return false;
                }
                if (types != null)
                {
                    return types.Any(type => type == Type || type == Namespace);
                }
                return true;
            }
        }

        // Simple param/FMG entries.
        // Some have quite a few entries, so hopefully this does not explode memory usage too much.
        // This is used both for autocomplete and opening.
        public class EntryData : DisplayData
        {
            // Internal to SoapstoneMetadata
            internal SoulsKey Key { get; set; }
        }

        private readonly IDictionary<string, List<string>> stringInfixes = new ConcurrentDictionary<string, List<string>>();
        private List<string> getSpaceBasedMatchText(string str)
        {
            if (!stringInfixes.TryGetValue(str, out List<string> infixes))
            {
                stringInfixes[str] = infixes = new List<string>();
                string[] parts = str.Split(' ');
                // This is slightly more complicated than deconstructing CamelCase since there is both removing spaces and punctuation
                // A bit scuffed, but this name is currently only used for autocomplete at present.
                // TODO: Share functionality with InstructionDocs? (a method that return parts)
                string word = "";
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    string part = AutocompleteNonWordRe.Replace(parts[i], "");
                    if (part.Length > 0)
                    {
                        part = char.ToUpperInvariant(part[0]) + part.Substring(1);
                        word = part + word;
                        infixes.Add(word);
                    }
                }
                // The first one should be the entire word as an autocomplete word
                infixes.Reverse();
            }
            return infixes;
        }

        // Store some info about groups. Keep it finite, though, as some groups have >2000 members (e.g. Carian Study Hall).
        private static readonly int maxGroupModels = 2;
        private class EntityGroupData
        {
            // Map, Type, Namespace should be set here
            public EntityData Initial { get; set; }
            // The first maxGroupModels models encountered
            public List<string> Models = new List<string>();
            public int Count { get; set; }
        }

        // The data version can be exposed for cache invalidation purposes in the future, though maybe it is not needed at all.
        public bool IsMapDataAvailable() => (currentData?.DataVersion ?? 0) > 0;

        private bool GetCompatibleData(string game, out InnerData outData)
        {
            InnerData data = currentData;
            if (data != null && data.IsGameCompatible(game))
            {
                outData = data;
                return true;
            }
            else
            {
                outData = null;
                return false;
            }
        }

        // There is a bit of duplication here, but at 2-3 instances there's not too much need for advanced abstraction.

        // Fetch methods are all async, to allow for background loads (map data is not currently).
        // Make sure to continue the task in the appropriate context.

        public Task<EntityData> GetEntityData(string game, int entityId)
        {
            EntityData result = null;
            if (GetCompatibleData(game, out InnerData data))
            {
                data.MapEntities.TryGetValue(entityId, out result);
            }
            return Task.FromResult(result);
        }

        // In this case, return empty string to indicate no row name but present entry.
        // Can fetch more advanced data in the future and return richer info.
        public async Task<EntryData> GetParamRow(string game, string type, int id)
        {
            await FetchParamRowNames(game, type);
            if (GetCompatibleData(game, out InnerData data))
            {
                if (data.ParamRowNames.TryGetValue(type, out Dictionary<int, EntryData> names)
                    && names.TryGetValue(id, out EntryData name))
                {
                    return name;
                }
            }
            return null;
        }

        public async Task<EntryData> GetFmgEntry(string game, string type, int id)
        {
            await FetchFmgEntryNames(game, type);
            if (GetCompatibleData(game, out InnerData data))
            {
                if (type == "PlaceName")
                {
                    // Special case: negative number used to mean "don't show if already here"
                    id = Math.Abs(id);
                }
                if (data.FmgEntryNames.TryGetValue(type, out Dictionary<int, EntryData> names)
                    && names.TryGetValue(id, out EntryData name))
                {
                    // From how we fetch it, null names should be filtered out before this point.
                    return name;
                }
            }
            return null;
        }

        public DisplayData GetMapNameData(string game, int mapId)
        {
            int[] parts = new[] { mapId / 1000000 % 100, mapId / 10000 % 100, mapId / 100 % 100, mapId % 100 };
            return GetMapNameData(game, FormatMap(parts));
        }

        public DisplayData GetMapNameData(string game, List<int> parts)
        {
            return GetMapNameData(game, FormatMap(parts));
        }

        public DisplayData GetMapNameData(string game, string mapId)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return null;
            }
            if (data.MapPartsItems == null)
            {
                InitializeMapNameItems(game, data);
            }
            return data.MapPartsItems.TryGetValue(mapId, out DocAutocompleteItem doc) ? doc.SubType as DisplayData : null;
        }

        public DisplayData GetEventFlagData(string game, int eventFlag)
        {
            // For now, this uses a hardcoded list.
            // TODO: make the list. Subsequent TODO: event flag metadata in DSMapStudio?
            if (!GetCompatibleData(game, out InnerData data))
            {
                return null;
            }
            Dictionary<string, string> eventFlags = nameMetadata.GetEventFlagNames(game);
            if (eventFlags == null || !eventFlags.TryGetValue(eventFlag.ToString(), out string text))
            {
                return null;
            }
            // Make these on the fly.
            // TODO: Big list for e.g. autocomplete could be reasonable too, but figure out how to do autocomplete filtering in a sane way.
            return new DisplayData
            {
                // Yet more TODO: Make a shared method for this kind of conditional text case?
                Desc = string.IsNullOrWhiteSpace(text) ? $"{eventFlag}" : $"{eventFlag}: {text}",
            };
        }

        public IEnumerable<DocAutocompleteItem> GetMapAutocompleteItems(string game, string map, IReadOnlyCollection<string> types = null)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return new List<DocAutocompleteItem>();
            }
            return data.MapItems.Where(item => ((EntityData)item.SubType).IsCompatible(map, types));
        }

        public IEnumerable<DocAutocompleteItem> GetMultiMapAutocompleteItems(string game, string map, EMEDF.DarkScriptType metaType)
        {
            if (!GetCompatibleData(game, out InnerData data) || metaType.OverrideTypes == null)
            {
                return new List<DocAutocompleteItem>();
            }
            if (!data.MultiMapItems.TryGetValue(metaType.OverrideEnum, out List<DocAutocompleteItem> items))
            {
                items = new List<DocAutocompleteItem>();
                foreach (DocAutocompleteItem item in data.MapItems)
                {
                    EntityData entity = item.SubType as EntityData;
                    if (entity != null
                        && (metaType.OverrideTypes.TryGetValue(entity.Namespace, out EMEDF.DarkScriptTypeOverride over)
                            || metaType.OverrideTypes.TryGetValue(entity.Type, out over)))
                    {
                        items.Add(item.CopyWithPrefix($"{over.DisplayValue}, "));
                    }
                }
                data.MultiMapItems[metaType.OverrideEnum] = items;
            }
            return items.Where(item => ((EntityData)item.SubType).IsCompatible(map));
        }

        public IEnumerable<DocAutocompleteItem> GetParamRowItems(string game, string name)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return new List<DocAutocompleteItem>();
            }
            return data.ParamRowItems.TryGetValue(name, out List<DocAutocompleteItem> items) ? items : new List<DocAutocompleteItem>();
        }

        public IEnumerable<DocAutocompleteItem> GetMultiParamAutocompleteItems(string game, EMEDF.DarkScriptType metaType)
        {
            if (!GetCompatibleData(game, out InnerData data) || metaType.OverrideTypes == null)
            {
                return new List<DocAutocompleteItem>();
            }
            if (!data.MultiParamItems.TryGetValue(metaType.OverrideEnum, out List<DocAutocompleteItem> items))
            {
                items = new List<DocAutocompleteItem>();
                bool allPresent = true;
                foreach (KeyValuePair<string, EMEDF.DarkScriptTypeOverride> over in metaType.OverrideTypes)
                {
                    if (data.ParamRowItems.TryGetValue(over.Key, out List<DocAutocompleteItem> overItems))
                    {
                        foreach (DocAutocompleteItem item in overItems)
                        {
                            items.Add(item.CopyWithPrefix($"{over.Value.DisplayValue}, "));
                        }
                    }
                    else
                    {
                        allPresent = false;
                    }
                }
                if (allPresent)
                {
                    data.MultiParamItems[metaType.OverrideEnum] = items;
                }
            }
            return items;
        }

        public IEnumerable<DocAutocompleteItem> GetFmgEntryItems(string game, string name)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return new List<DocAutocompleteItem>();
            }
            return data.FmgEntryItems.TryGetValue(name, out List<DocAutocompleteItem> items) ? items : new List<DocAutocompleteItem>();
        }

        public IEnumerable<DocAutocompleteItem> GetMapNameIntItems(string game)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return new List<DocAutocompleteItem>();
            }
            if (data.MapIntItems == null)
            {
                InitializeMapNameItems(game, data);
            }
            return data.MapIntItems.Values;
        }

        public IEnumerable<DocAutocompleteItem> GetMapNamePartsItems(string game)
        {
            if (!GetCompatibleData(game, out InnerData data))
            {
                return new List<DocAutocompleteItem>();
            }
            if (data.MapPartsItems == null)
            {
                InitializeMapNameItems(game, data);
            }
            return data.MapPartsItems.Values;
        }

        private void InitializeMapNameItems(string game, InnerData data)
        {
            // Set map items, even if set before
            SortedDictionary<string, DocAutocompleteItem> intItems = new SortedDictionary<string, DocAutocompleteItem>();
            SortedDictionary<string, DocAutocompleteItem> partsItems = new SortedDictionary<string, DocAutocompleteItem>();
            Dictionary<string, string> mapNames = nameMetadata.GetMapNames(game);
            if (mapNames != null)
            {
                foreach (KeyValuePair<string, string> entry in mapNames)
                {
                    byte[] parts = ParseMap(entry.Key);
                    if (parts != null)
                    {
                        int mapInt = int.Parse($"{parts[0]:d2}{parts[1]:d2}{parts[2]:d2}{parts[3]:d2}");
                        string mapArgs = $"{parts[0]}, {parts[1]}, {parts[2]}, {parts[3]}";
                        DisplayData val = new DisplayData
                        {
                            Desc = string.IsNullOrWhiteSpace(entry.Value) ? entry.Key : $"{entry.Key}: {entry.Value}",
                            MatchText = string.IsNullOrWhiteSpace(entry.Value) ? null : getSpaceBasedMatchText(entry.Value),
                        };
                        intItems[entry.Key] =
                            new DocAutocompleteItem(mapInt, val.Desc, AutocompleteCategory.Custom, FancyContextType.Any, false, val);
                        partsItems[entry.Key] =
                            new DocAutocompleteItem(mapArgs, val.Desc, AutocompleteCategory.Custom, FancyContextType.Any, false, val);
                    }
                }
            }
            data.MapIntItems = intItems;
            data.MapPartsItems = partsItems;
        }

        public async Task<bool> FetchParamRowNames(string game, string type)
        {
            if (!GetCompatibleData(game, out InnerData data) || !provider.TryGetClient(out SoapstoneClient client))
            {
                return false;
            }
            if (data.ParamRowNames.ContainsKey(type) && data.ParamRowItems.ContainsKey(type))
            {
                return false;
            }
            // TODO: For autocomplete, test awaiting a second here
            List<SoulsObject> results;
            try
            {
                // TODO: Use library for this, KnownResources
                EditorResource resource = new EditorResource { Type = EditorResourceType.Param, Game = data.CurrentGame };
                PropertySearch search = PropertySearch.AllOf(
                    new PropertySearch.Condition(PropertyComparisonType.Equal, "Param", type));
                RequestedProperties props = new RequestedProperties().Add("ID").AddNonTrivial("Name");
                results = await client.SearchObjects(resource, SoulsKey.GameParamRowKey.KeyType, search, props);
            }
            catch (Exception)
            {
                // This is most likely an unavailable exception or other temporary issue.
                // TODO: add some kind of debug mode to not swallow these
                return false;
            }
            Dictionary<int, EntryData> dict = new Dictionary<int, EntryData>();
            List<DocAutocompleteItem> items = new List<DocAutocompleteItem>();
            foreach (SoulsObject result in results)
            {
                if (!result.TryGetInt("ID", out int id))
                {
                    continue;
                }
                result.TryGetValue("Name", out string name);
                // Usually the first id applies in-game, if there are duplicates
                if (!dict.ContainsKey(id))
                {
                    EntryData entry = new EntryData
                    {
                        Key = result.Key,
                        Desc = name == null ? $"{id}" : $"{id}: {name}",
                    };
                    if (name != null)
                    {
                        entry.MatchText = getSpaceBasedMatchText(name);
                    }
                    dict[id] = entry;
                    items.Add(new DocAutocompleteItem(id, entry.Desc, AutocompleteCategory.Custom, FancyContextType.Any, false, entry));
                }
            }
            data.ParamRowNames[type] = dict;
            data.ParamRowItems[type] = items;
            return true;
        }

        private static readonly Regex newLineRe = new Regex(@"[\r\n]+");
        public async Task<bool> FetchFmgEntryNames(string game, string type)
        {
            if (!GetCompatibleData(game, out InnerData data) || !provider.TryGetClient(out SoapstoneClient client))
            {
                return false;
            }
            if (data.FmgEntryNames.ContainsKey(type) && data.FmgEntryItems.ContainsKey(type))
            {
                return false;
            }
            List<SoulsObject> results;
            try
            {
                // TODO: Use library for this, KnownResources
                EditorResource resource = new EditorResource { Type = EditorResourceType.Fmg, Game = data.CurrentGame };
                PropertySearch search = PropertySearch.AllOf(
                    new PropertySearch.Condition(PropertyComparisonType.Equal, "BaseFMG", type),
                    new PropertySearch.Condition(PropertyComparisonType.Matches, "Text", "."));
                RequestedProperties props = new RequestedProperties().Add("ID", "Text");
                results = await client.SearchObjects(resource, SoulsKey.FmgEntryKey.KeyType, search, props);
            }
            catch (Exception)
            {
                return false;
            }
            Dictionary<int, EntryData> dict = new Dictionary<int, EntryData>();
            List<DocAutocompleteItem> items = new List<DocAutocompleteItem>();
            foreach (SoulsObject result in results)
            {
                if (!result.TryGetInt("ID", out int id) || !result.TryGetValue("Text", out string text))
                {
                    continue;
                }
                if (!dict.ContainsKey(id) && !string.IsNullOrWhiteSpace(text))
                {
                    EntryData entry = new EntryData
                    {
                        Key = result.Key,
                        Desc = $"{id}: {text}",
                        MatchText = getSpaceBasedMatchText(text),
                    };
                    dict[id] = entry;
                    // Newlines cause lines to overlap in the autocomplete menu
                    string singleLine = newLineRe.Replace(entry.Desc, " ");
                    items.Add(new DocAutocompleteItem(id, singleLine, AutocompleteCategory.Custom, FancyContextType.Any, false, entry));
                }
            }
            data.FmgEntryNames[type] = dict;
            data.FmgEntryItems[type] = items;
            return true;
        }

        // Mutating methods

        public async Task OpenEntryData(string game, EntryData entry)
        {
            if (!GetCompatibleData(game, out InnerData data) || !provider.TryGetClient(out SoapstoneClient client))
            {
                return;
            }
            try
            {
                SoulsKey key = entry.Key;
                EditorResource resource;
                if (key is SoulsKey.FmgEntryKey fmgEntry)
                {
                    // Minor edge case: use currently loaded language instead of original fetch language, if possible.
                    if (Enum.TryParse(data.CurrentLanguage, out SoulsFmg.FmgLanguage lang))
                    {
                        key = new SoulsKey.FmgEntryKey(new SoulsKey.FmgKey(lang, fmgEntry.File.Type), fmgEntry.ID, fmgEntry.Index);
                    }
                    resource = new EditorResource { Type = EditorResourceType.Fmg, Game = data.CurrentGame };
                }
                else if (key is SoulsKey.GameParamRowKey)
                {
                    resource = new EditorResource { Type = EditorResourceType.Param, Game = data.CurrentGame };
                }
                else
                {
                    return;
                }
                await client.OpenObject(resource, key);
            }
            catch (Exception)
            {
            }
        }

        public async Task OpenEntityData(string game, EntityData entity)
        {
            if (!GetCompatibleData(game, out InnerData data) || !provider.TryGetClient(out SoapstoneClient client))
            {
                return;
            }
            try
            {
                EditorResource resource = new EditorResource { Type = EditorResourceType.Map, Game = data.CurrentGame };
                // For now, use this to identify groups, to search for those specifically.
                // Using entity.Key would just point us to one of the members of the group.
                if (entity.MatchText.Contains("group"))
                {
                    PropertySearch search = PropertySearch.Of(
                        new PropertySearch.Condition(PropertyComparisonType.Equal, "EntityGroupIDs", entity.ID));
                    await client.OpenSearch(resource, SoulsKey.MsbEntryKey.KeyType, search, true);
                }
                else if (entity.Key != null)
                {
                    await client.OpenObject(resource, entity.Key);
                }
            }
            catch (Exception)
            {
            }
        }

        // Beware: unlike other opening methods, this *will* throw an exception with a human-readable error message.
        public async Task OpenMap(string game, string mapName)
        {
            if (!provider.TryGetClient(out SoapstoneClient client))
            {
                throw new InvalidOperationException($"DSMapStudio is not running or not connected.\nCheck Metadata > Show DSMapStudio Connection Info for more details.");
            }
            if (!GetCompatibleData(game, out InnerData data))
            {
                throw new InvalidOperationException($"DSMapStudio does not appear to have project open for \"{game}\" (detected game: {CurrentGameString ?? "None"})");
            }
            EditorResource resource = new EditorResource { Type = EditorResourceType.Map, Game = data.CurrentGame, Name = mapName };
            await client.OpenResource(resource);
        }

        // Internal map fetch loop. This is a (premature) optimization to avoid a ton of map-heavy computation in DSMS,
        // and avoid a ton of constant allocation in both programs.
        private async Task BackgroundFetchLoop()
        {
            while (true)
            {
                // Only fetch map data in this loop, to avoid overloading both programs and introducing race conditions.
                try
                {
                    bool fresh = await BackgroundFetch();
                    if (fresh)
                    {
                        LastLoopResult = $"Most recent sync at {DateTime.Now}";
                    }
                }
                catch (Exception ex)
                {
                    LastLoopResult = $"Most recent sync at {DateTime.Now}: {ex}";
#if DEBUG
                    // Expose this in a more useful way? Keep trying anyway?
                    Console.WriteLine(ex);
#endif
                    // We'll want to ignore the exception only if it's an availability issue
                    // (how do this - ping? transforming exception?)
                    // return;
                }
                // Could potentially also use cancel token not to end the loop, but to request an immediate re-fetch.
                try
                {
                    await Task.Delay(checkIntervalMs, cancelSource.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private async Task<bool> BackgroundFetch()
        {
            if (!provider.TryGetClient(out SoapstoneClient client))
            {
                // I guess keep old stale data.
                // Return false for no attempted RPCs
                return false;
            }
            ServerInfoResponse response = await client.GetServerInfo();
            EditorResource project = response.Resources.Where(r => r.Type == EditorResourceType.Project).FirstOrDefault();
            if (project == null)
            {
                // Likewise allow stale data
                return true;
            }
            List<string> openMaps = response.Resources
                .Where(r => r.Type == EditorResourceType.Map && r.Name != null)
                .Select(r => r.Name)
                .ToList();
            string currentLanguage = response.Resources
                .Where(r => r.Type == EditorResourceType.Fmg)
                .Select(r => r.Name)
                .FirstOrDefault();
            InnerData pendingData = null;
            if (currentData == null || currentData.CurrentGame != project.Game || resetData)
            {
                pendingData = new InnerData
                {
                    CurrentGame = project.Game,
                    CurrentLanguage = currentLanguage,
                };
            }
            else
            {
                // Reload map data if all dictionaries are already set up (and may contain other data)
                // and the set of loaded maps has expanded.
                if (openMaps.Any(m => !currentData.FullyLoadedMaps.Contains(m)))
                {
                    // Could do a shallow copy to preserve param etc. fields, but probably fine to edit in-place all at the end
                    pendingData = currentData;
                }
            }
            if (pendingData == null)
            {
                return true;
            }
            Dictionary<int, EntityData> entities = new Dictionary<int, EntityData>();
            List<DocAutocompleteItem> items = new List<DocAutocompleteItem>();
            if (openMaps.Count > 0)
            {
                // Currently, this means search all open maps
                EditorResource mapResource = new EditorResource { Type = EditorResourceType.Map, Game = project.Game };
                // Anything with an entity id.
                // TODO: ObjActEntityID (and EventFlagID)
                PropertySearch search =  PropertySearch.AnyOf(
                    new PropertySearch.Condition(PropertyComparisonType.Greater, "EntityID", 0),
                    new PropertySearch.Condition(PropertyComparisonType.Greater, "EntityGroupIDs", 0));
                RequestedProperties props = new RequestedProperties()
                    .Add("Type", "Namespace", "ModelName")
                    .AddNonTrivial("EntityID", "EntityGroupIDs", "CharaInitID");
                List<SoulsObject> results = await client.SearchObjects(mapResource, SoulsKey.MsbEntryKey.KeyType, search, props);
                // Go through all results.
                // If something has an entity id, it gets filled in, and anything else after that is ignored.
                // If something is a group, it waits for n distinct models, and can transition at that point.
                Dictionary<int, EntityGroupData> groups = new Dictionary<int, EntityGroupData>();
                // TODO: Maybe we can get some of this metadata from DSMS at some point (Alias resource?)
                Dictionary<string, string> modelNames = nameMetadata.GetModelNames(pendingData.GetGameString());
                Dictionary<string, string> charaNames = nameMetadata.GetCharaNames(pendingData.GetGameString());
                foreach (SoulsObject entity in results)
                {
                    if (!entity.TryGetValue("Type", out string type)
                        || !entity.TryGetValue("Namespace", out string ns)
                        || entity.Key is not SoulsKey.MsbEntryKey entry)
                    {
                        continue;
                    }
                    entity.TryGetValue("ModelName", out string modelId);
                    if (entity.TryGetInt("EntityID", out int entityId))
                    {
                        if (entities.ContainsKey(entityId) || entityId < minEntityId) continue;
                        // Description should be like "ModelName (ThingName)" for parts, "ThingType ThingName" otherwise
                        EntityData entityData = new EntityData
                        {
                            ID = entityId,
                            Key = entry,
                            Type = type,
                            Namespace = ns,
                        };
                        if (ns == "Part")
                        {
                            entityData.MatchText.Add(entry.Name);
                            // Make different versions based on whether model name available
                            List<string> infixes = null;
                            if (entity.TryGetInt("CharaInitID", out int charaId)
                                && charaNames.TryGetValue(charaId.ToString(), out string charaName))
                            {
                                infixes = getSpaceBasedMatchText(charaName);
                            }
                            if ((infixes == null || infixes.Count == 0) && modelId != null && modelNames.TryGetValue(modelId, out string modelName))
                            {
                                infixes = getSpaceBasedMatchText(modelName);
                            }
                            // Make it clear this is a dummy on hover (excluded from autocomplete)
                            string special = type.StartsWith("Dummy") ? $"{type} " : "";
                            if (infixes == null || infixes.Count == 0)
                            {
                                entityData.Desc = $"{entityId}: {special}{entry.Name}";
                            }
                            else
                            {
                                entityData.Desc = $"{entityId}: {special}{infixes[0]} ({entry.Name})";
                                entityData.MatchText.AddRange(infixes);
                            }
                        }
                        else
                        {
                            string objType = type == "Other" ? type + ns : type;
                            entityData.Desc = $"{entityId}: {objType} {entry.Name}";
                            entityData.MatchText.Add(objType);
                            entityData.MatchText.Add(ns);
                        }
                        entities[entityId] = entityData;
                    }
                    if (entity.TryGetInts("EntityGroupIDs", out List<int> groupIds))
                    {
                        foreach (int groupId in groupIds)
                        {
                            // Entities take priority over groups in the game, as there can be collisions
                            if (entities.ContainsKey(groupId) || groupId < minEntityId) continue;
                            if (!groups.TryGetValue(groupId, out EntityGroupData group))
                            {
                                groups[groupId] = group = new EntityGroupData
                                {
                                    Initial = new EntityData
                                    {
                                        // Not strictly the entity id, which may not exist for this entity.
                                        // It is meant to be representative of any entity.
                                        ID = groupId,
                                        Key = entry,
                                        Type = type,
                                        Namespace = ns,
                                    },
                                };
                            }
                            group.Count++;
                            if (group.Models.Count <= maxGroupModels)
                            {
                                string modelDesc = entity.TryGetInt("CharaInitID", out int charaId) ? charaId.ToString() : modelId;
                                if (modelDesc != null && !group.Models.Contains(modelDesc))
                                {
                                    group.Models.Add(modelDesc);
                                }
                            }
                        }
                    }
                }
                foreach (KeyValuePair<int, EntityGroupData> entry in groups)
                {
                    int groupId = entry.Key;
                    if (entities.ContainsKey(groupId)) continue;
                    EntityGroupData group = entry.Value;
                    string models = "";
                    if (group.Models.Count > 0)
                    {
                        List<string> names = new List<string>();
                        for (int i = 0; i < group.Models.Count; i++)
                        {
                            if (i == maxGroupModels)
                            {
                                names.Add("etc");
                            }
                            else
                            {
                                string modelId = group.Models[i];
                                if (charaNames.TryGetValue(modelId, out string modelName) || modelNames.TryGetValue(modelId, out modelName))
                                {
                                    modelName = getSpaceBasedMatchText(modelName).FirstOrDefault();
                                }
                                names.Add(modelName ?? group.Models[i]);
                            }
                        }
                        models = $" ({string.Join(", ", names)})";
                    }
                    EntityData entityData = entities[groupId] = new EntityData
                    {
                        ID = groupId,
                        Key = group.Initial.Key,
                        Type = group.Initial.Type,
                        Namespace = group.Initial.Namespace,
                        Desc = $"{groupId}: {group.Count}x group{models}",
                    };
                    // Don't include models here for now. Also, use this to identify groups for DSMS opening purposes
                    // (can also just add a boolean or something to EntityData)
                    entityData.MatchText.Add("group");
                }
            }
            foreach (KeyValuePair<int, EntityData> entry in entities)
            {
                int id = entry.Key;
                EntityData entity = entry.Value;
                if (entity.Type.StartsWith("Dummy"))
                {
                    // Allow Dummy types above, mostly for hover info, but don't include it in autocomplete
                    continue;
                }
                items.Add(new DocAutocompleteItem(id, entity.Desc, AutocompleteCategory.Map, FancyContextType.Any, false, entity));
            }
            items.Sort();
            // Custom entity ids. Do this after creating autocomplete items, at least for the moment.
            // The issue is when there is only 10000 autocomplete and nothing else, this interferes with up/down arrow
            // keys in a ton of commands (like typing in a command, then wanting to go to a different line for an entity id).
            foreach (KeyValuePair<int, string> entry in selfNames)
            {
                int id = entry.Key;
                if (project.Game != FromSoftGame.EldenRing && id != 10000) continue;
                List<string> infixes = getSpaceBasedMatchText(entry.Value);
                EntityData entityData = entities[id] = new EntityData
                {
                    ID = id,
                    Type = "Self",
                    Namespace = "Part",
                    Desc = $"{id}: {infixes[0]}",
                    MatchText = infixes,
                };
            }
            pendingData.FullyLoadedMaps = new SortedSet<string>(openMaps);
            pendingData.MapEntities = entities;
            pendingData.MapItems = items;
            pendingData.MultiMapItems.Clear();
            pendingData.CurrentLanguage = currentLanguage;
            // Either set initial version to 1, or increment existing version
            pendingData.DataVersion++;
            currentData = pendingData;
            return true;
        }
    }
}
