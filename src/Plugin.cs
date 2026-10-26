using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// Shows a description panel for the gun or item a player is next to.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gungeon.eid.externalitemdescriptions";
        public const string Name = "External Item Descriptions";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private static readonly FieldInfo LastInteractionTarget =
            typeof(PlayerController).GetField("m_lastInteractionTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private EidConfig _cfg;
        private EidDatabase _db;
        private PanelBuilder _builder;
        private bool _visible = true;

        private PickupObject _target;
        private readonly HashSet<int> _owned = new HashSet<int>();
        private int _ownedHash;

        private Design _design;
        private Panel _panel;
        private string _panelKey;  // inputs the current panel was built from
        private readonly Overlay _overlay = new Overlay();
        private string _lastError;

        private void Awake()
        {
            Log = Logger;
            _cfg = new EidConfig(Config);
            var path = EidDatabase.DefaultPath(Path.GetDirectoryName(Info.Location));
            try
            {
                _db = EidDatabase.Load(path);
                _builder = new PanelBuilder(_db, _cfg);
                Log.LogInfo($"Loaded wiki data ({_db.File.fetched}): {_db.File.guns.Length} guns, {_db.File.items.Length} items, {_db.File.synergies.Length} synergies");
            }
            catch (Exception e)
            {
                Log.LogError($"Could not load {path}: {e}");
            }
            if (LastInteractionTarget == null)
                Log.LogError("PlayerController.m_lastInteractionTarget not found; descriptions will not show.");
        }

        private void Update()
        {
            if (_cfg.ToggleKey.Value.IsDown())
                _visible = !_visible;

            _target = null;
            if (_db == null || !_cfg.Enabled.Value || !_visible) return;
            try
            {
                _target = FindTarget();
                if (_target != null) RefreshOwned();
            }
            catch (Exception e)
            {
                _target = null;
                Log.LogDebug($"Update: {e}");
            }
        }

        /// <summary>The pickup any player is currently in interaction range of, or null.</summary>
        private PickupObject FindTarget()
        {
            if (!GameManager.HasInstance || LastInteractionTarget == null) return null;
            var gm = GameManager.Instance;
            if (gm.IsPaused || gm.IsLoadingLevel) return null;
            if (AmmonomiconController.HasInstance && AmmonomiconController.Instance.IsOpen) return null;
            if (Minimap.HasInstance && Minimap.Instance.IsFullscreen) return null;

            var players = gm.AllPlayers;
            if (players == null) return null;
            foreach (var player in players)
            {
                if (player == null) continue;
                var pickup = Resolve(LastInteractionTarget.GetValue(player) as IPlayerInteractable);
                if (pickup != null && IsDescribed(pickup)) return pickup;
            }
            return null;
        }

        /// <summary>
        /// Guns and items get a description. Pickups (hearts, armor, ammo, keys, money) and blanks don't.
        /// </summary>
        private static bool IsDescribed(PickupObject pickup) =>
            (pickup is Gun || pickup is PassiveItem || pickup is PlayerItem) && !(pickup is SilencerItem);

        private static PickupObject Resolve(IPlayerInteractable target)
        {
            var behaviour = target as MonoBehaviour;
            if (behaviour == null) return null; // Unity's null check also catches destroyed objects
            switch (target)
            {
                case ShopItemController shop:
                    if (shop.item == null) return null;
                    if (shop.CurrencyType == ShopItemController.ShopCurrencyType.META_CURRENCY)
                    {
                        // Breach shop items are placeholder pickups; look up the real one by name.
                        var real = PickupObjectDatabase.GetByEncounterName(shop.item.EncounterNameOrDisplayName);
                        return real != null ? real : shop.item;
                    }
                    return shop.item;
                case RewardPedestal pedestal:
                    return pedestal.contents != null ? pedestal.contents : null;
                case PickupObject pickup:
                    return pickup;
                default:
                    return null;
            }
        }

        /// <summary>Collects the ids of every gun and item held by any player.</summary>
        private void RefreshOwned()
        {
            _owned.Clear();
            foreach (var player in GameManager.Instance.AllPlayers)
            {
                if (player == null) continue;
                if (player.inventory != null && player.inventory.AllGuns != null)
                    foreach (var g in player.inventory.AllGuns)
                        if (g != null) _owned.Add(g.PickupObjectId);
                foreach (var p in player.passiveItems)
                    if (p != null) _owned.Add(p.PickupObjectId);
                foreach (var a in player.activeItems)
                    if (a != null) _owned.Add(a.PickupObjectId);
            }
            unchecked
            {
                int h = _owned.Count;
                foreach (var id in _owned) h += id * 397 ^ (id << 7);
                _ownedHash = h;
            }
        }

        /// <summary>Screen pixels per panel pixel: the Scale setting, or 1 at 1080p, 2 at 1440p, 3 at 4K.</summary>
        private int PixelSize()
        {
            int s = _cfg.Scale.Value;
            return s > 0 ? s : Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f) - 1);
        }

        /// <returns>False if the game's UI font isn't available yet.</returns>
        private bool EnsureDesign()
        {
            int pixel = PixelSize();
            if (_design != null && _design.Pixel == pixel && _design.Body.Font != null) return true;
            var font = Design.GameFont();
            if (font == null) return false;
            _design = new Design(pixel, font);
            _panelKey = null;
            return true;
        }

        /// <summary>Screen x of the HUD's left edge (hearts and active item box), or null if it isn't shown.</summary>
        private static float? HudLeft()
        {
            try
            {
                if (!GameUIRoot.HasInstance) return null;
                var ui = GameUIRoot.Instance;
                float left = float.MaxValue;
                if (ui.heartControllers != null && ui.heartControllers.Count > 0 && ui.heartControllers[0] != null)
                {
                    var panel = ui.heartControllers[0].Panel;
                    if (panel != null && panel.IsVisible) left = Mathf.Min(left, panel.GetScreenRect().x);
                }
                if (ui.itemControllers != null && ui.itemControllers.Count > 0 && ui.itemControllers[0] != null)
                {
                    var box = ui.itemControllers[0].ItemBoxSprite;
                    if (box != null && box.IsVisible) left = Mathf.Min(left, box.GetScreenRect().x);
                }
                return left < float.MaxValue && left >= 0 ? left : (float?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Screen y range between the top-left HUD (hearts, blanks, keys, money) and the active item box.
        /// An edge is NaN when that part of the HUD isn't shown.
        /// </summary>
        private static void HudGap(out float top, out float bottom)
        {
            top = bottom = float.NaN;
            try
            {
                if (!GameUIRoot.HasInstance) return;
                var ui = GameUIRoot.Instance;
                float topHudBottom = float.NaN;
                Action<dfControl> below = c =>
                {
                    if (c == null || !c.IsVisible) return;
                    float yMax = c.GetScreenRect().yMax;
                    if (float.IsNaN(topHudBottom) || yMax > topHudBottom) topHudBottom = yMax;
                };
                if (ui.heartControllers != null && ui.heartControllers.Count > 0 && ui.heartControllers[0] != null)
                    below(ui.heartControllers[0].Panel);
                if (ui.blankControllers != null && ui.blankControllers.Count > 0 && ui.blankControllers[0] != null)
                    below(ui.blankControllers[0].Panel);
                below(ui.p_playerKeyLabel);
                below(ui.p_playerCoinLabel);
                below(ui.p_specialKeySprite);
                top = topHudBottom;

                if (ui.itemControllers != null && ui.itemControllers.Count > 0 && ui.itemControllers[0] != null)
                {
                    var box = ui.itemControllers[0].ItemBoxSprite;
                    if (box != null && box.IsVisible) bottom = box.GetScreenRect().y;
                }
            }
            catch (Exception)
            {
                top = bottom = float.NaN;
            }
        }

        // Runs after Update, so the panel follows this frame's target and HUD layout.
        private void LateUpdate()
        {
            if (_target == null || _builder == null || !EnsureDesign())
            {
                _overlay.Hide();
                return;
            }
            try
            {
                var d = _design;
                float charWidth = d.Body.Measure("0000000000") / 10f;
                float width = Mathf.Round(charWidth * _cfg.MaxWidth.Value);

                // The panel fills the space between the top-left HUD and the active item box.
                // Without a HUD, it uses the middle 70% of the screen.
                HudGap(out float hudTop, out float hudBottom);
                float top = Mathf.Round(float.IsNaN(hudTop) ? Screen.height * 0.15f : hudTop + d.Margin) + d.DownShift;
                float bottom = Mathf.Round(float.IsNaN(hudBottom) ? Screen.height * 0.85f : hudBottom - d.Margin) + d.DownShift;
                float maxHeight = Mathf.Max(d.Body.LineHeight * 4, bottom - top);

                string key = $"{_target.PickupObjectId}|{_ownedHash}|{Screen.width}x{Screen.height}|{d.Pixel}|{maxHeight}|{_cfg.LayoutKey()}";
                if (key != _panelKey)
                {
                    _panel = _builder.Build(_target, _owned, d, width, maxHeight);
                    _panelKey = key;
                }

                // Left edge lines up with the hearts and active item box; without a HUD, close to the screen edge.
                float x = Mathf.Round(HudLeft() ?? Screen.width * 0.012f);
                _overlay.Show(_panel, new Vector2(x, top), _cfg.Outline.Value, d.OutlineWidth);
            }
            catch (Exception e)
            {
                // This runs every frame, so log each distinct error only once.
                var msg = e.ToString();
                if (msg != _lastError) Log.LogError($"Showing the panel: {msg}");
                _lastError = msg;
                _panel = null;
                _panelKey = null;
                _overlay.Hide();
            }
        }
    }
}
