using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GungeonEID
{
    // The classes below mirror data/eid_data.json, which tools/scrape_wiki.py generates from the wiki.

    public class EidDataFile
    {
        public string source;
        public string fetched;
        public GunEntry[] guns;
        public ItemEntry[] items;
        public SynergyEntry[] synergies;
    }

    public class GunEntry
    {
        public string name, notes, quote, quality, type;
        public string dps, magazine, ammo, damage, fireRate, reload, shotSpeed, range, force, spread, gunClass;
        public int[] ids;
    }

    public class ItemEntry
    {
        public string name, type, quote, quality, effect;
        public int[] ids;
    }

    public class SynergyItemRef
    {
        public string name;
        public int[] ids;
    }

    public class SynergyEntry
    {
        public string name, effect;
        public SynergyItemRef[] mandatory;
        public SynergyItemRef[] optional;
        public int optionalNeeded;
    }

    /// <summary>The wiki data, looked up by pickup id, with the pickup's name as a fallback.</summary>
    public class EidDatabase
    {
        public EidDataFile File { get; private set; }

        private readonly Dictionary<int, GunEntry> _gunsById = new Dictionary<int, GunEntry>();
        private readonly Dictionary<int, ItemEntry> _itemsById = new Dictionary<int, ItemEntry>();
        private readonly Dictionary<string, GunEntry> _gunsByName = new Dictionary<string, GunEntry>();
        private readonly Dictionary<string, ItemEntry> _itemsByName = new Dictionary<string, ItemEntry>();
        private readonly Dictionary<int, List<SynergyEntry>> _synergiesById = new Dictionary<int, List<SynergyEntry>>();

        public static EidDatabase Load(string path)
        {
            var root = Json.Parse(System.IO.File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) throw new FormatException("eid_data.json: top level is not an object");
            var db = new EidDatabase { File = ReadFile(root) };
            db.Index();
            return db;
        }

        private static EidDataFile ReadFile(Dictionary<string, object> o)
        {
            var file = new EidDataFile
            {
                source = Json.Str(o, "source"),
                fetched = Json.Str(o, "fetched"),
            };
            var guns = new List<GunEntry>();
            foreach (Dictionary<string, object> g in Json.Arr(o, "guns"))
                guns.Add(new GunEntry
                {
                    name = Json.Str(g, "name"), notes = Json.Str(g, "notes"), quote = Json.Str(g, "quote"),
                    quality = Json.Str(g, "quality"), type = Json.Str(g, "type"), dps = Json.Str(g, "dps"),
                    magazine = Json.Str(g, "magazine"), ammo = Json.Str(g, "ammo"), damage = Json.Str(g, "damage"),
                    fireRate = Json.Str(g, "fireRate"), reload = Json.Str(g, "reload"), shotSpeed = Json.Str(g, "shotSpeed"),
                    range = Json.Str(g, "range"), force = Json.Str(g, "force"), spread = Json.Str(g, "spread"),
                    gunClass = Json.Str(g, "gunClass"), ids = Json.Ints(g, "ids"),
                });
            file.guns = guns.ToArray();

            var items = new List<ItemEntry>();
            foreach (Dictionary<string, object> it in Json.Arr(o, "items"))
                items.Add(new ItemEntry
                {
                    name = Json.Str(it, "name"), type = Json.Str(it, "type"), quote = Json.Str(it, "quote"),
                    quality = Json.Str(it, "quality"), effect = Json.Str(it, "effect"), ids = Json.Ints(it, "ids"),
                });
            file.items = items.ToArray();

            var synergies = new List<SynergyEntry>();
            foreach (Dictionary<string, object> s in Json.Arr(o, "synergies"))
                synergies.Add(new SynergyEntry
                {
                    name = Json.Str(s, "name"), effect = Json.Str(s, "effect"),
                    mandatory = ReadRefs(Json.Arr(s, "mandatory")), optional = ReadRefs(Json.Arr(s, "optional")),
                    optionalNeeded = Json.Int(s, "optionalNeeded"),
                });
            file.synergies = synergies.ToArray();
            return file;
        }

        private static SynergyItemRef[] ReadRefs(List<object> list)
        {
            var refs = new List<SynergyItemRef>();
            foreach (Dictionary<string, object> r in list)
                refs.Add(new SynergyItemRef { name = Json.Str(r, "name"), ids = Json.Ints(r, "ids") });
            return refs.ToArray();
        }

        private void Index()
        {
            foreach (var g in File.guns ?? new GunEntry[0])
            {
                _gunsByName[Norm(g.name)] = g;
                foreach (var id in g.ids ?? new int[0]) _gunsById[id] = g;
            }
            foreach (var it in File.items ?? new ItemEntry[0])
            {
                _itemsByName[Norm(it.name)] = it;
                foreach (var id in it.ids ?? new int[0]) _itemsById[id] = it;
            }
            foreach (var s in File.synergies ?? new SynergyEntry[0])
            {
                var seen = new HashSet<int>();
                foreach (var r in AllRefs(s))
                    foreach (var id in r.ids ?? new int[0])
                    {
                        if (!seen.Add(id)) continue;
                        if (!_synergiesById.TryGetValue(id, out var list)) _synergiesById[id] = list = new List<SynergyEntry>();
                        list.Add(s);
                    }
            }
        }

        public static IEnumerable<SynergyItemRef> AllRefs(SynergyEntry s)
        {
            foreach (var r in s.mandatory ?? new SynergyItemRef[0]) yield return r;
            foreach (var r in s.optional ?? new SynergyItemRef[0]) yield return r;
        }

        /// <summary>Lowercase letters and digits only, so "A.W.P." and "awp" compare equal.</summary>
        public static string Norm(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            foreach (var c in name.ToLowerInvariant())
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }

        public GunEntry GetGun(PickupObject p)
        {
            if (_gunsById.TryGetValue(p.PickupObjectId, out var g)) return g;
            _gunsByName.TryGetValue(Norm(p.EncounterNameOrDisplayName), out g);
            return g;
        }

        public ItemEntry GetItem(PickupObject p)
        {
            if (_itemsById.TryGetValue(p.PickupObjectId, out var it)) return it;
            _itemsByName.TryGetValue(Norm(p.EncounterNameOrDisplayName), out it);
            return it;
        }

        /// <summary>
        /// The ids that count as this pickup when matching synergy requirements: its own id, or every id of
        /// its wiki entry (some entries cover several ids, such as the five Master Rounds).
        /// </summary>
        public int[] SelfIds(PickupObject p)
        {
            if (_synergiesById.ContainsKey(p.PickupObjectId)) return new[] { p.PickupObjectId };
            var g = GetGun(p);
            if (g != null && g.ids != null && g.ids.Length > 0) return g.ids;
            var it = GetItem(p);
            if (it != null && it.ids != null && it.ids.Length > 0) return it.ids;
            return new[] { p.PickupObjectId };
        }

        public List<SynergyEntry> GetSynergies(int[] selfIds)
        {
            var result = new List<SynergyEntry>();
            foreach (var id in selfIds)
                if (_synergiesById.TryGetValue(id, out var list))
                    foreach (var s in list)
                        if (!result.Contains(s)) result.Add(s);
            return result;
        }

        public static string DefaultPath(string pluginDir) => Path.Combine(pluginDir, "eid_data.json");
    }
}
