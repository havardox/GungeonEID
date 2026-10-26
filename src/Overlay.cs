using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// Shows a <see cref="Panel"/> with the game's own UI system (Daikon Forge GUI), under the same UI
    /// manager as the HUD: one dfLabel per piece of text and one <see cref="AtlasSprite"/> per pickup sprite,
    /// inside a container panel. The controls are rebuilt only when the panel changes.
    /// </summary>
    public class Overlay
    {
        private static readonly Vector2[] OutlineDirections = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        private dfPanel _root;
        private Panel _shown;
        private bool _shownWithOutline;
        private Vector2 _shownAt;
        private float _pixelsPerUnit = 1f; // screen pixels per UI unit

        /// <param name="topLeft">Screen position of the panel's top-left corner, in pixels from the top-left.</param>
        /// <param name="outline">Whether to outline text and sprites.</param>
        public void Show(Panel panel, Vector2 topLeft, bool outline, float outlineWidth)
        {
            if (!GameUIRoot.HasInstance || GameUIRoot.Instance.Manager == null) return;
            bool rebuilt = false;
            if (_root == null || panel != _shown || outline != _shownWithOutline)
            {
                Build(GameUIRoot.Instance.Manager, panel, outline, outlineWidth);
                rebuilt = true;
            }
            if (rebuilt || topLeft != _shownAt) MoveTo(topLeft);
            if (!_root.IsVisible) _root.IsVisible = true;
        }

        // The UI's origin isn't necessarily the screen's top-left corner (the game view can be letterboxed),
        // so measure where position (0, 0) lands on screen and place the panel relative to that.
        private void MoveTo(Vector2 topLeft)
        {
            _root.RelativePosition = Vector3.zero;
            Vector2 origin = _root.GetScreenRect().position;
            _root.RelativePosition = new Vector3(
                Mathf.Round((topLeft.x - origin.x) / _pixelsPerUnit),
                Mathf.Round((topLeft.y - origin.y) / _pixelsPerUnit));
            _shownAt = topLeft;
        }

        public void Hide()
        {
            if (_root != null && _root.IsVisible) _root.IsVisible = false;
        }

        private void Build(dfGUIManager manager, Panel panel, bool outline, float outlineWidth)
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = manager.AddControl<dfPanel>();
            _root.gameObject.name = "ExternalItemDescriptions";
            _root.IsInteractive = false;

            // Measure how many screen pixels one UI unit covers, so the layout (in screen pixels) can be
            // converted to UI units.
            _root.Size = new Vector2(100, 100);
            float measured = _root.GetScreenRect().height / 100f;
            _pixelsPerUnit = measured > 0.01f ? measured : 1f;
            _root.Size = new Vector2(Mathf.Max(1, panel.Width), Mathf.Max(1, panel.Height)) / _pixelsPerUnit;

            panel.Visit((op, offset) =>
            {
                var rect = new Rect(op.Rect.position + offset, op.Rect.size);
                switch (op)
                {
                    case FlowLayout.TextOp t:
                        AddLabel(t, rect.position, outline);
                        break;
                    case FlowLayout.IconOp i:
                        var icon = SpriteIcons.Get(i.PickupId);
                        if (icon == null) break;
                        if (outline)
                        {
                            var shade = new Color(Design.Outline.r, Design.Outline.g, Design.Outline.b, Design.Outline.a * i.Alpha);
                            foreach (var dir in OutlineDirections)
                                AddSprite(icon, new Rect(rect.position + dir * outlineWidth, rect.size), shade);
                        }
                        AddSprite(icon, rect, new Color(1f, 1f, 1f, i.Alpha));
                        break;
                }
            });

            _shown = panel;
            _shownWithOutline = outline;
        }

        private void AddLabel(FlowLayout.TextOp t, Vector2 position, bool outline)
        {
            var label = _root.AddControl<dfLabel>();
            label.PreventFontChanges = true; // keep this font and scale in every game language
            label.IsInteractive = false;
            label.Font = t.Size.Font;
            label.Atlas = t.Size.Font.Atlas;
            label.TextScale = t.Size.Scale / _pixelsPerUnit;
            label.Padding = new RectOffset(0, 0, 0, 0);
            label.ProcessMarkup = false;
            label.WordWrap = false;
            label.AutoSize = true;
            label.Color = t.Color;
            label.Outline = outline;
            label.OutlineSize = 1; // in font pixels
            label.OutlineColor = Design.Outline;
            label.Text = t.Text;
            label.RelativePosition = ToUnits(position);
        }

        private void AddSprite(SpriteIcons.Icon icon, Rect rect, Color color)
        {
            var sprite = _root.AddControl<AtlasSprite>();
            sprite.IsInteractive = false;
            // The sprite renders with a copy of the HUD atlas's material, textured with the pickup's atlas.
            var hudAtlas = _root.GetManager().DefaultAtlas;
            if (hudAtlas != null && hudAtlas.Material != null) sprite.Material = hudAtlas.Material;
            sprite.Uvs = icon.Uvs;
            sprite.Texture = icon.Texture;
            sprite.Size = rect.size / _pixelsPerUnit;
            sprite.RelativePosition = ToUnits(rect.position);
            sprite.Color = color;
            sprite.Invalidate();
        }

        private Vector3 ToUnits(Vector2 pixels) => new Vector3(pixels.x / _pixelsPerUnit, pixels.y / _pixelsPerUnit);
    }

    /// <summary>
    /// A texture sprite that shows one sprite from a tk2d atlas, using the sprite's own corner uvs, so
    /// sprites the atlas stores rotated or mirrored are drawn the right way up.
    /// </summary>
    public class AtlasSprite : dfTextureSprite
    {
        /// <summary>Corner uvs in tk2d order: bottom-left, bottom-right, top-left, top-right.</summary>
        public Vector2[] Uvs;

        protected override void rebuildUV(dfRenderData renderBuffer)
        {
            if (Uvs == null || Uvs.Length < 4)
            {
                base.rebuildUV(renderBuffer);
                return;
            }
            // The quad's vertices run top-left, top-right, bottom-right, bottom-left.
            renderBuffer.UV.Add(Uvs[2]);
            renderBuffer.UV.Add(Uvs[3]);
            renderBuffer.UV.Add(Uvs[1]);
            renderBuffer.UV.Add(Uvs[0]);
        }
    }
}
