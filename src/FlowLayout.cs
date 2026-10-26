using System.Collections.Generic;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// Lays out one block of the panel, in screen pixels: word-wrapped colored text, aligned label/value
    /// tables and rows of pickup sprites. <see cref="Overlay"/> turns the result into UI controls.
    /// </summary>
    public class FlowLayout
    {
        /// <summary>
        /// The game's UI font at one size, measured the same way the game's text renderer lays it out
        /// (glyph advance plus kerning, times the scale).
        /// </summary>
        public class TextSize
        {
            public readonly dfFont Font;
            public readonly int Scale;  // screen pixels per font pixel
            public readonly float LineHeight;
            public readonly float SpaceWidth;
            private readonly Dictionary<string, float> _widths = new Dictionary<string, float>();

            public TextSize(dfFont font, int scale)
            {
                Font = font;
                Scale = scale;
                LineHeight = font.LineHeight * scale;
                SpaceWidth = Measure(" ");
                if (SpaceWidth <= 0) SpaceWidth = Mathf.Ceil(LineHeight * 0.35f);
            }

            public float Measure(string s)
            {
                if (_widths.TryGetValue(s, out var w)) return w;
                int units = 0;
                char previous = '\0';
                foreach (var c in s)
                {
                    var glyph = Font.GetGlyph(c);
                    if (glyph == null) continue;
                    units += glyph.xadvance + Font.GetKerning(previous, c);
                    previous = c;
                }
                return _widths[s] = units * Scale;
            }
        }

        public abstract class Op { public Rect Rect; }
        public class TextOp : Op { public string Text; public Color Color; public TextSize Size; }
        public class IconOp : Op { public int PickupId; public float Alpha = 1f; }

        /// <summary>One label/value pair in a <see cref="Grid"/>.</summary>
        public class Cell
        {
            public string Label, Value; public Color ValueColor;
            public Cell(string label, string value, Color valueColor) { Label = label; Value = value; ValueColor = valueColor; }
        }

        /// <summary>A piece of text that <see cref="Run"/> keeps on one line.</summary>
        public struct Seg
        {
            public string Text; public Color Color; public bool Title;
            public Seg(string text, Color color, bool title = false) { Text = text; Color = color; Title = title; }
        }

        public readonly List<Op> Ops = new List<Op>();
        public readonly float MaxWidth;
        public readonly TextSize Body, Title;
        public float LineHeight => Body.LineHeight;
        public float Width { get; private set; }
        public float Height => _y + (_line.Count > 0 ? _lineHeight : 0);

        /// <summary>Opacity of sprites passed as faded.</summary>
        public float FadedAlpha = 0.35f;

        private readonly float _lineGap;
        private readonly List<Op> _line = new List<Op>();
        private float _x, _y, _lineHeight;
        private float _indent; // left edge of text lines inside IconsBesideText, 0 elsewhere

        /// <param name="lineGap">Vertical space between lines, in pixels.</param>
        public FlowLayout(TextSize body, TextSize title, float maxWidth, float lineGap)
        {
            Body = body;
            Title = title;
            MaxWidth = maxWidth;
            _lineGap = lineGap;
            _lineHeight = body.LineHeight;
        }

        public bool AtLineStart => _line.Count == 0 && _x <= _indent;

        /// <summary>Word-wrapped text. '\n' starts a new line.</summary>
        public void Text(string text, Color color, bool title = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var size = title ? Title : Body;
            var paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) NewLine();
                foreach (var word in paragraphs[p].Split(' '))
                    if (word.Length > 0) Place(word, color, size);
            }
        }

        /// <summary>Text kept together on one line; wraps before it if needed, or word by word if it's wider than a line.</summary>
        public void Run(params Seg[] segs)
        {
            float total = 0;
            foreach (var s in segs) total += (s.Title ? Title : Body).Measure(s.Text);
            if (total > MaxWidth - _indent)
            {
                foreach (var s in segs) Text(s.Text, s.Color, s.Title);
                return;
            }
            float lead = AtLineStart ? 0 : Body.SpaceWidth;
            if (_x + lead + total > MaxWidth) { NewLine(); lead = 0; }
            _x += lead;
            foreach (var s in segs)
            {
                var size = s.Title ? Title : Body;
                float w = size.Measure(s.Text);
                AddToLine(new TextOp { Text = s.Text, Color = s.Color, Size = size, Rect = new Rect(_x, _y, w, size.LineHeight) });
                _x += w;
            }
        }

        private void Place(string word, Color color, TextSize size)
        {
            float w = size.Measure(word);
            float lead = AtLineStart ? 0 : size.SpaceWidth;
            if (!AtLineStart && _x + lead + w > MaxWidth) { NewLine(); lead = 0; }
            _x += lead;
            AddToLine(new TextOp { Text = word, Color = color, Size = size, Rect = new Rect(_x, _y, w, size.LineHeight) });
            _x += w;
        }

        /// <summary>Width of a row of pickup sprites, or 0 if none of them have a sprite.</summary>
        public static float IconRowWidth(IList<int> pickupIds, float scale, float gap)
        {
            float width = 0;
            int count = 0;
            foreach (var id in pickupIds)
            {
                var px = SpriteIcons.PixelSize(id);
                if (px.x <= 0) continue;
                width += (count++ > 0 ? gap : 0) + Mathf.Round(px.x * scale);
            }
            return width;
        }

        /// <summary>Height of the tallest of the pickups' sprites, or 0 if none of them have a sprite.</summary>
        public static float IconRowHeight(IList<int> pickupIds, float scale)
        {
            float height = 0;
            foreach (var id in pickupIds)
                height = Mathf.Max(height, Mathf.Round(SpriteIcons.PixelSize(id).y * scale));
            return height;
        }

        /// <summary>
        /// Pickup sprites side by side, bottom-aligned, wrapping to a new row when the width runs out.
        /// </summary>
        /// <param name="scale">Screen pixels per sprite pixel.</param>
        /// <param name="spaceBelow">Space between the last row and whatever follows.</param>
        public void IconRow(IList<int> pickupIds, float scale, float gap, float spaceBelow, ICollection<int> faded = null)
        {
            if (!AtLineStart) NewLine();
            var row = new List<IconOp>();
            float x = 0, rowHeight = 0;
            foreach (var id in pickupIds)
            {
                var px = SpriteIcons.PixelSize(id);
                if (px.x <= 0) continue;
                var size = new Vector2(Mathf.Round(px.x * scale), Mathf.Round(px.y * scale));
                if (row.Count > 0 && x + gap + size.x > MaxWidth)
                {
                    FinishIconRow(row, rowHeight, gap);
                    row.Clear();
                    x = rowHeight = 0;
                }
                if (row.Count > 0) x += gap;
                row.Add(new IconOp { PickupId = id, Rect = new Rect(x, 0, size.x, size.y), Alpha = AlphaFor(id, faded) });
                x += size.x;
                rowHeight = Mathf.Max(rowHeight, size.y);
            }
            if (row.Count > 0) FinishIconRow(row, rowHeight, spaceBelow);
        }

        private void FinishIconRow(List<IconOp> row, float rowHeight, float gapBelow)
        {
            foreach (var op in row)
            {
                op.Rect.y = _y + rowHeight - op.Rect.height;
                Ops.Add(op);
                Width = Mathf.Max(Width, op.Rect.xMax);
            }
            _y += rowHeight + gapBelow;
        }

        private float AlphaFor(int pickupId, ICollection<int> faded) =>
            faded != null && faded.Contains(pickupId) ? FadedAlpha : 1f;

        /// <summary>
        /// A row of sprites centered in a left column of <paramref name="columnWidth"/>, with a block of text
        /// starting at columnWidth + gutter. Sprites and text are vertically centered in a row at least
        /// <paramref name="rowHeight"/> tall. Calls that share a column width and row height line up. A
        /// sprite row wider than the column goes on its own line, with the text below it at the same x.
        /// </summary>
        /// <param name="text">Writes the text block using this layout's Text/Run/NewLine.</param>
        /// <param name="faded">Sprites to draw at <see cref="FadedAlpha"/>.</param>
        public void IconsBesideText(IList<int> pickupIds, float scale, float gap, float columnWidth, float gutter,
            System.Action text, ICollection<int> faded = null, float rowHeight = 0)
        {
            if (!AtLineStart) NewLine();
            var sizes = new List<KeyValuePair<int, Vector2>>();
            float rowWidth = 0;
            foreach (var id in pickupIds)
            {
                var px = SpriteIcons.PixelSize(id);
                if (px.x <= 0) continue;
                var size = new Vector2(Mathf.Round(px.x * scale), Mathf.Round(px.y * scale));
                rowWidth += (sizes.Count > 0 ? gap : 0) + size.x;
                rowHeight = Mathf.Max(rowHeight, size.y);
                sizes.Add(new KeyValuePair<int, Vector2>(id, size));
            }
            float textX = columnWidth > 0 ? columnWidth + gutter : 0;
            if (rowWidth > columnWidth)
            {
                IconRow(pickupIds, scale, gap, _lineGap, faded);
                _indent = textX;
                _x = _indent;
                text();
                if (!AtLineStart) NewLine();
                _indent = 0;
                _x = 0;
                return;
            }

            float top = _y, x = Mathf.Round((columnWidth - rowWidth) / 2);
            foreach (var s in sizes)
            {
                var r = new Rect(x, top + Mathf.Round((rowHeight - s.Value.y) / 2), s.Value.x, s.Value.y);
                Ops.Add(new IconOp { PickupId = s.Key, Rect = r, Alpha = AlphaFor(s.Key, faded) });
                Width = Mathf.Max(Width, r.xMax);
                x += s.Value.x + gap;
            }

            _indent = textX;
            _x = _indent;
            int firstTextOp = Ops.Count;
            text();
            if (!AtLineStart) NewLine();
            float textHeight = _y - _lineGap - top;
            float shift = Mathf.Max(0, Mathf.Round((rowHeight - textHeight) / 2));
            for (int i = firstTextOp; i < Ops.Count; i++) Ops[i].Rect.y += shift;

            _y = top + Mathf.Max(rowHeight, textHeight + shift) + _lineGap;
            _indent = 0;
            _x = 0;
        }

        /// <summary>
        /// Label/value pairs in two aligned columns, or one column if two don't fit. Labels are left-aligned
        /// and values right-aligned, so the digits of numbers line up.
        /// </summary>
        /// <param name="labelGap">Space between a label and its value.</param>
        /// <param name="columnGap">Space between the two columns.</param>
        public void Grid(IList<Cell> cells, Color labelColor, float labelGap, float columnGap)
        {
            if (cells.Count == 0) return;
            if (!AtLineStart) NewLine();

            int cols = 2;
            float[] labelW = null, valueW = null;
            for (; cols >= 1; cols--)
            {
                labelW = new float[cols];
                valueW = new float[cols];
                for (int i = 0; i < cells.Count; i++)
                {
                    labelW[i % cols] = Mathf.Max(labelW[i % cols], Body.Measure(cells[i].Label));
                    valueW[i % cols] = Mathf.Max(valueW[i % cols], Body.Measure(cells[i].Value));
                }
                float total = 0;
                for (int c = 0; c < cols; c++) total += labelW[c] + labelGap + valueW[c] + (c > 0 ? columnGap : 0);
                if (total <= MaxWidth || cols == 1) break;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                int c = i % cols;
                if (c == 0 && i > 0) _y += LineHeight + _lineGap;
                float x = 0;
                for (int k = 0; k < c; k++) x += labelW[k] + labelGap + valueW[k] + columnGap;
                AddAt(cells[i].Label, labelColor, x);
                float valueRight = x + labelW[c] + labelGap + valueW[c];
                AddAt(cells[i].Value, cells[i].ValueColor, valueRight - Body.Measure(cells[i].Value));
            }
            _y += LineHeight + _lineGap;
            _x = 0;
        }

        private void AddAt(string text, Color color, float x)
        {
            float w = Body.Measure(text);
            Ops.Add(new TextOp { Text = text, Color = color, Size = Body, Rect = new Rect(x, _y, w, LineHeight) });
            Width = Mathf.Max(Width, x + w);
        }

        /// <summary>Ends the current line, if any, and adds vertical space.</summary>
        public void Gap(float px)
        {
            if (!AtLineStart) NewLine();
            _y += px;
        }

        /// <summary>Ends the current line. Text of different sizes on one line shares a bottom edge.</summary>
        public void NewLine()
        {
            foreach (var op in _line)
                op.Rect.y = _y + _lineHeight - op.Rect.height;
            MergeLine();
            _y += _lineHeight + _lineGap;
            _line.Clear();
            _lineHeight = LineHeight;
            _x = _indent;
        }

        // Joins neighbouring words of the same color and size into one text op, so fewer labels are
        // created. Relies on the current line's ops being the last entries in Ops.
        private void MergeLine()
        {
            if (_line.Count < 2) return;
            var merged = new List<Op>(_line.Count);
            foreach (var op in _line)
            {
                var prev = merged.Count > 0 ? merged[merged.Count - 1] as TextOp : null;
                var cur = op as TextOp;
                if (prev != null && cur != null && prev.Color == cur.Color && prev.Size == cur.Size)
                {
                    float gap = cur.Rect.x - prev.Rect.xMax;
                    if (Mathf.Abs(gap) < 0.5f || Mathf.Abs(gap - cur.Size.SpaceWidth) < 0.5f)
                    {
                        prev.Text += gap > 0.5f ? " " + cur.Text : cur.Text;
                        prev.Rect.width = cur.Rect.xMax - prev.Rect.x;
                        continue;
                    }
                }
                merged.Add(op);
            }
            Ops.RemoveRange(Ops.Count - _line.Count, _line.Count);
            Ops.AddRange(merged);
        }

        private void AddToLine(Op op)
        {
            Ops.Add(op);
            _line.Add(op);
            _lineHeight = Mathf.Max(_lineHeight, op.Rect.height);
            Width = Mathf.Max(Width, op.Rect.xMax);
        }

        /// <summary>Ends the last line. Call once after adding everything.</summary>
        public void Finish()
        {
            if (_line.Count > 0) NewLine();
        }
    }
}
