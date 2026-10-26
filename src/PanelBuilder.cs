using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>Builds the description panel for a pickup from the wiki data and the players' inventories.</summary>
    public class PanelBuilder
    {
        private readonly EidDatabase _db;
        private readonly EidConfig _cfg;

        public PanelBuilder(EidDatabase db, EidConfig cfg)
        {
            _db = db;
            _cfg = cfg;
        }

        /// <summary>A synergy as seen from one pickup: the other items it needs.</summary>
        private class SynergyView
        {
            public SynergyEntry Entry;
            public List<SynergyItemRef> Mandatory = new List<SynergyItemRef>();
            public List<SynergyItemRef> Optional = new List<SynergyItemRef>();
            public int OptionalNeeded;
        }

        /// <summary>
        /// Layout, top to bottom:
        /// <code>
        /// [sprite]  Name - Quality          (title size, centered on the sprite)
        /// Type (class)
        /// "Quote"
        /// Stat table (guns)
        /// Description
        /// Synergies:
        /// [sprites] + Partner / Partner     one block per synergy, in columns
        ///           Synergy name
        /// </code>
        /// </summary>
        /// <param name="maxWidth">Width at which text wraps.</param>
        /// <param name="maxHeight">Height of the panel; synergies that don't fit continue in another column.</param>
        public Panel Build(PickupObject pickup, HashSet<int> owned, Design d, float maxWidth, float maxHeight)
        {
            var gun = pickup is Gun ? _db.GetGun(pickup) : null;
            var item = gun == null ? _db.GetItem(pickup) : null;
            var panel = new Panel(maxHeight, d.Related);

            var main = NewBlock(d, maxWidth);
            AddHeader(main, d, pickup, gun, item, owned);
            if (gun != null && _cfg.ShowGunStats.Value)
            {
                main.Gap(d.Section);
                AddStats(main, d, gun);
            }
            if (_cfg.ShowDescription.Value)
            {
                string desc = gun != null ? gun.notes : item?.effect;
                if (!string.IsNullOrEmpty(desc))
                {
                    main.Gap(d.Section);
                    main.Text(desc, Design.Paragraph);
                }
            }
            main.Finish();
            panel.Add(main, 0);

            if (_cfg.ShowSynergies.Value)
                AddSynergies(panel, d, maxWidth, pickup, owned);
            return panel;
        }

        private FlowLayout NewBlock(Design d, float maxWidth) =>
            new FlowLayout(d.Body, d.Title, maxWidth, d.LineGap) { FadedAlpha = _cfg.UnownedSpriteOpacity.Value };

        private void AddHeader(FlowLayout L, Design d, PickupObject pickup, GunEntry gun, ItemEntry item, HashSet<int> owned)
        {
            string name = gun?.name ?? item?.name ?? pickup.EncounterNameOrDisplayName;
            string quality = gun?.quality ?? item?.quality ?? QualityLetter(pickup.quality);
            string quote = gun?.quote ?? item?.quote;
            bool ownedSelf = owned.Contains(pickup.PickupObjectId);
            var self = new[] { pickup.PickupObjectId };

            // Only the name line sits beside the sprite; the type and quote go below it.
            L.IconsBesideText(self, d.SpriteScale, d.Tight, FlowLayout.IconRowWidth(self, d.SpriteScale, d.Tight), d.Related, () =>
            {
                L.Run(new FlowLayout.Seg(name, ownedSelf ? _cfg.OwnedColor : Design.Accent, true));
                L.Text("-", Design.Dim, true);
                L.Text(quality, Design.QualityColor(quality), true);
                if (ownedSelf) L.Text("(owned)", _cfg.OwnedColor);
            });
            L.Gap(d.Tight);
            L.Text(TypeLine(pickup, gun, item), Design.Dim);
            if (gun != null && !string.IsNullOrEmpty(gun.gunClass)) L.Text("(" + gun.gunClass + ")", Design.Dim);
            if (_cfg.ShowQuote.Value && !string.IsNullOrEmpty(quote))
            {
                L.NewLine();
                L.Text("\"" + quote + "\"", Design.Quote);
            }
        }

        private static void AddStats(FlowLayout L, Design d, GunEntry gun)
        {
            var cells = new List<FlowLayout.Cell>();
            AddStat(cells, "DPS", gun.dps, Design.Dps);
            AddStat(cells, "Damage", gun.damage, Design.Text);
            AddStat(cells, "Fire delay", WithUnit(gun.fireRate, "s"), Design.Text);
            AddStat(cells, "Reload", WithUnit(gun.reload, "s"), Design.Text);
            AddStat(cells, "Magazine", gun.magazine, Design.Text);
            AddStat(cells, "Max ammo", gun.ammo, Design.Text);
            AddStat(cells, "Shot speed", gun.shotSpeed, Design.Text);
            AddStat(cells, "Range", gun.range, Design.Text);
            AddStat(cells, "Force", gun.force, Design.Text);
            AddStat(cells, "Spread", gun.spread, Design.Text);
            L.Grid(cells, Design.Dim, d.Related, d.Section);
        }

        /// <summary>
        /// Adds the "Synergies:" heading and one block per synergy below it, in as few columns as fit above
        /// the panel's height limit, spread evenly. All synergies share one grid: every row is as tall as
        /// the tallest sprite, every column as wide as the widest block, and all synergy text starts at the
        /// same x (the widest sprite row, at most a third of the panel width, plus a gap). Sprites are
        /// centered in their part of the row.
        /// </summary>
        private void AddSynergies(Panel panel, Design d, float maxWidth, PickupObject pickup, HashSet<int> owned)
        {
            var views = Synergies(pickup, owned);
            if (views.Count == 0) return;

            var heading = NewBlock(d, maxWidth);
            heading.Text("Synergies:", Design.Accent);
            heading.Finish();
            panel.Add(heading, d.Section);
            panel.BeginColumns(d.Related);

            var sprites = views.Select(v => _cfg.ShowSynergySprites.Value ? PartnerSprites(v) : new List<int>()).ToList();
            float spriteColumn = Mathf.Min(Mathf.Round(maxWidth / 3),
                sprites.Select(s => FlowLayout.IconRowWidth(s, d.SpriteScale, d.Tight)).DefaultIfEmpty(0).Max());
            float rowHeight = sprites.Select(s => FlowLayout.IconRowHeight(s, d.SpriteScale)).DefaultIfEmpty(0).Max();
            var blocks = views.Select((v, i) => SynergyBlock(d, maxWidth, v, sprites[i], spriteColumn, rowHeight, owned)).ToList();
            float columnWidth = blocks.Max(b => b.Width);

            foreach (var indices in panel.SplitIntoColumns(blocks.Select(b => b.Height).ToList(), d.Related))
                panel.AddColumn(indices.Select(i => blocks[i]).ToList(), d.Related, columnWidth);
        }

        // [sprites]  + Partner / Partner
        //            Synergy name
        // Partners the players own get the owned color and a full-strength sprite; others are faded.
        private FlowLayout SynergyBlock(Design d, float maxWidth, SynergyView v, List<int> sprites, float spriteColumn, float rowHeight,
            HashSet<int> owned)
        {
            var B = NewBlock(d, maxWidth);
            var faded = new HashSet<int>(v.Mandatory.Concat(v.Optional)
                .Where(part => part.ids != null && part.ids.Length > 0 && !Owns(part, owned))
                .Select(part => part.ids[0]));
            B.IconsBesideText(sprites, d.SpriteScale, d.Tight, spriteColumn, d.Related, () =>
            {
                B.Text("+", Design.Dim);
                for (int i = 0; i < v.Mandatory.Count; i++)
                {
                    if (i > 0) B.Text("+", Design.Dim);
                    Partner(B, v.Mandatory[i], owned);
                }
                if (v.OptionalNeeded > 0 && v.Optional.Count > 0)
                {
                    if (v.Mandatory.Count > 0) B.Text("+", Design.Dim);
                    B.Text(v.OptionalNeeded == 1 ? "one of" : $"{NumberWord(v.OptionalNeeded)} of", Design.Dim);
                    for (int i = 0; i < v.Optional.Count; i++)
                    {
                        if (i > 0) B.Text("/", Design.Dim);
                        Partner(B, v.Optional[i], owned);
                    }
                }
                B.NewLine();
                B.Text(v.Entry.name, Design.Accent);
            }, faded, rowHeight);
            B.Finish();
            return B;
        }

        /// <summary>The sprite drawn for each partner: the first id of each one that has a sprite.</summary>
        private static List<int> PartnerSprites(SynergyView v)
        {
            var ids = new List<int>();
            foreach (var part in v.Mandatory.Concat(v.Optional))
                if (part.ids != null && part.ids.Length > 0 && SpriteIcons.PixelSize(part.ids[0]).x > 0)
                    ids.Add(part.ids[0]);
            return ids;
        }

        private void Partner(FlowLayout L, SynergyItemRef part, HashSet<int> owned) =>
            L.Run(new FlowLayout.Seg(part.name, Owns(part, owned) ? _cfg.OwnedColor : Design.Text));

        /// <summary>
        /// The pickup's synergies, each reduced to the other items it needs. If the pickup is one of the
        /// "one of" options, that requirement counts as met.
        /// </summary>
        private List<SynergyView> Synergies(PickupObject pickup, HashSet<int> owned)
        {
            var self = new HashSet<int>(_db.SelfIds(pickup));
            var result = new List<SynergyView>();
            foreach (var s in _db.GetSynergies(self.ToArray()))
            {
                var v = new SynergyView { Entry = s, OptionalNeeded = s.optionalNeeded };
                foreach (var r in s.mandatory ?? new SynergyItemRef[0])
                    if (!Contains(r, self)) v.Mandatory.Add(r);
                foreach (var r in s.optional ?? new SynergyItemRef[0])
                {
                    if (Contains(r, self)) v.OptionalNeeded = Mathf.Max(0, v.OptionalNeeded - 1);
                    else v.Optional.Add(r);
                }
                if (v.OptionalNeeded == 0) v.Optional.Clear();
                result.Add(v);
            }
            return result;
        }

        private static bool Contains(SynergyItemRef r, HashSet<int> ids) => r.ids != null && r.ids.Any(ids.Contains);
        private static bool Owns(SynergyItemRef r, HashSet<int> owned) => Contains(r, owned);

        private static void AddStat(List<FlowLayout.Cell> cells, string label, string value, Color valueColor)
        {
            if (!string.IsNullOrEmpty(value)) cells.Add(new FlowLayout.Cell(label, value, valueColor));
        }

        /// <summary>Appends a unit to plain numbers; leaves values like "0.1 (Manual)" alone.</summary>
        private static string WithUnit(string value, string unit)
        {
            if (string.IsNullOrEmpty(value)) return value;
            float f;
            return float.TryParse(value, out f) ? value + unit : value;
        }

        private static string TypeLine(PickupObject p, GunEntry gun, ItemEntry item)
        {
            if (gun != null) return string.IsNullOrEmpty(gun.type) ? "Gun" : "Gun, " + gun.type;
            if (item != null && !string.IsNullOrEmpty(item.type)) return item.type + " item";
            if (p is Gun) return "Gun";
            if (p is PlayerItem) return "Active item";
            if (p is PassiveItem) return "Passive item";
            return "Pickup";
        }


        private static string QualityLetter(PickupObject.ItemQuality q)
        {
            switch (q)
            {
                case PickupObject.ItemQuality.D: return "D";
                case PickupObject.ItemQuality.C: return "C";
                case PickupObject.ItemQuality.B: return "B";
                case PickupObject.ItemQuality.A: return "A";
                case PickupObject.ItemQuality.S: return "S";
                default: return "N";
            }
        }

        private static string NumberWord(int n)
        {
            switch (n)
            {
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                default: return n.ToString();
            }
        }
    }
}
