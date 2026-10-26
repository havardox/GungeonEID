using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// Every size, gap and color the panel uses, in screen pixels. Sizes are whole multiples of one panel
    /// pixel, so text, sprites and gaps all sit on the same pixel grid and the pixel font is never scaled
    /// unevenly.
    /// </summary>
    public class Design
    {
        /// <summary>Screen pixels per panel pixel.</summary>
        public readonly int Pixel;

        // Spacing, from closest to loosest relationship.
        public float Tight => 2 * Pixel;      // between sprites in a row
        public float Related => 4 * Pixel;    // a sprite and its text; a stat's label and value; neighbouring synergies
        public float Section => 8 * Pixel;    // between sections; the stat table's two columns
        public float Margin => 24 * Pixel;    // the panel and the HUD around it
        public float DownShift => 16 * Pixel; // added to the panel's top and bottom limits
        public float LineGap => Pixel;        // between lines of text
        public float OutlineWidth => Pixel;   // dark outline around text and sprites

        // Sizes. The title and sprites are about 1.5x the body text, rounded down to whole pixels.
        public readonly FlowLayout.TextSize Body;   // all text except the title
        public readonly FlowLayout.TextSize Title;  // item name and quality
        public int TitleScale => OneAndAHalf;
        public float SpriteScale => OneAndAHalf;    // the item's sprite and synergy sprites
        private int OneAndAHalf => Pixel + Pixel / 2;

        // Colors.
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color Paragraph = new Color(0.86f, 0.86f, 0.86f);
        public static readonly Color Dim = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color Quote = new Color(0.93f, 0.86f, 0.62f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.32f);
        public static readonly Color Dps = new Color(1f, 0.62f, 0.25f);
        public static readonly Color Outline = new Color(0.04f, 0.03f, 0.06f, 0.9f);

        public static Color QualityColor(string quality)
        {
            switch (string.IsNullOrEmpty(quality) ? 'N' : quality[0])
            {
                case 'D': return new Color(0.78f, 0.62f, 0.45f);
                case 'C': return new Color(0.85f, 0.85f, 0.9f);
                case 'B': return new Color(0.45f, 0.9f, 0.45f);
                case 'A': return new Color(1f, 0.42f, 0.38f);
                case 'S': return new Color(1f, 0.85f, 0.2f);
                default: return Dim;
            }
        }

        /// <param name="font">The game's UI font (see <see cref="GameFont"/>).</param>
        public Design(int pixel, dfFont font)
        {
            Pixel = Mathf.Max(1, pixel);
            Body = new FlowLayout.TextSize(font, Pixel);
            Title = TitleScale == Pixel ? Body : new FlowLayout.TextSize(font, TitleScale);
        }

        /// <summary>The font the game's HUD uses, or null before the HUD exists.</summary>
        public static dfFont GameFont()
        {
            if (!GameUIRoot.HasInstance || GameUIRoot.Instance.Manager == null) return null;
            return GameUIRoot.Instance.Manager.DefaultFont as dfFont;
        }
    }
}
