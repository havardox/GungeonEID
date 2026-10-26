using BepInEx.Configuration;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// The mod's settings, in four groups:
    ///   General    - on/off and the toggle key
    ///   Layout     - how big the panel is
    ///   Appearance - colors and effects
    ///   Content    - which sections the panel shows
    /// </summary>
    public class EidConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<KeyboardShortcut> ToggleKey;

        public readonly ConfigEntry<int> Scale;
        public readonly ConfigEntry<int> MaxWidth;

        public readonly ConfigEntry<string> OwnedColorHex;
        public readonly ConfigEntry<float> UnownedSpriteOpacity;
        public readonly ConfigEntry<bool> Outline;

        public readonly ConfigEntry<bool> ShowQuote;
        public readonly ConfigEntry<bool> ShowGunStats;
        public readonly ConfigEntry<bool> ShowDescription;
        public readonly ConfigEntry<bool> ShowSynergies;
        public readonly ConfigEntry<bool> ShowSynergySprites;

        public Color OwnedColor { get; private set; }

        public EidConfig(ConfigFile file)
        {
            Enabled = file.Bind("General", "Enabled", true,
                "Show a description when standing next to a gun or item.");
            ToggleKey = file.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F4),
                "Key that shows or hides the description.");

            Scale = file.Bind("Layout", "Scale", 0,
                new ConfigDescription("Size of the text and sprites. 0 picks a size for your resolution (1 at 1080p, 2 at 1440p, 3 at 4K).",
                    new AcceptableValueRange<int>(0, 8)));
            MaxWidth = file.Bind("Layout", "MaxWidth", 50,
                new ConfigDescription("Width at which text wraps, in characters.", new AcceptableValueRange<int>(15, 120)));

            OwnedColorHex = file.Bind("Appearance", "OwnedColor", "#4DA6FF",
                "Color for the names of guns and items you already have (hex RGB).");
            UnownedSpriteOpacity = file.Bind("Appearance", "UnownedSpriteOpacity", 0.35f,
                new ConfigDescription("Opacity of synergy partner sprites you don't have (1 = no fading).",
                    new AcceptableValueRange<float>(0f, 1f)));
            Outline = file.Bind("Appearance", "Outline", true,
                "Draw a dark outline around text and sprites.");

            ShowQuote = file.Bind("Content", "ShowQuote", true, "Show the item's pickup quote.");
            ShowGunStats = file.Bind("Content", "ShowGunStats", true, "Show DPS and the stat table for guns.");
            ShowDescription = file.Bind("Content", "ShowDescription", true, "Show what the gun or item does.");
            ShowSynergies = file.Bind("Content", "ShowSynergies", true, "List the synergies this gun or item is part of.");
            ShowSynergySprites = file.Bind("Content", "ShowSynergySprites", true, "Show partner sprites next to each synergy.");

            UpdateColor();
            OwnedColorHex.SettingChanged += (s, e) => UpdateColor();
        }

        /// <summary>A string that changes whenever a setting that affects the panel's layout changes.</summary>
        public string LayoutKey() =>
            $"{Scale.Value}|{MaxWidth.Value}|{OwnedColorHex.Value}|{UnownedSpriteOpacity.Value}|" +
            $"{ShowQuote.Value}{ShowGunStats.Value}{ShowDescription.Value}{ShowSynergies.Value}{ShowSynergySprites.Value}";

        private void UpdateColor()
        {
            OwnedColor = TryParseHex(OwnedColorHex.Value, out var c) ? c : new Color(0.3f, 0.65f, 1f);
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(hex)) return false;
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6) return false;
            try
            {
                int v = System.Convert.ToInt32(hex, 16);
                color = new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
                return true;
            }
            catch (System.FormatException)
            {
                return false;
            }
        }
    }
}
