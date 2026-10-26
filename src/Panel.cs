using System.Collections.Generic;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>
    /// The finished description: a main section of blocks stacked top to bottom, optionally followed by a
    /// region of side-by-side columns of equal width (used for synergies). Columns all start at the top of
    /// that region.
    /// </summary>
    public class Panel
    {
        private struct Part
        {
            public FlowLayout Layout;
            public Vector2 Offset;
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly float _maxHeight, _columnGap;
        private float _y;            // bottom of the main section
        private float _regionTop;    // top of the column region
        private float _columnX;      // left edge of the next column

        public float Width { get; private set; }
        public float Height { get; private set; }

        /// <param name="maxHeight">Height limit for the whole panel; 0 or less means unlimited.</param>
        public Panel(float maxHeight, float columnGap)
        {
            _maxHeight = maxHeight > 0 ? maxHeight : float.MaxValue;
            _columnGap = columnGap;
        }

        /// <summary>Adds a block to the main section, below the previous one.</summary>
        public void Add(FlowLayout block, float gapAbove)
        {
            if (_parts.Count > 0) _y += gapAbove;
            Place(block, 0, _y);
            _y += block.Height;
        }

        /// <summary>Starts the column region below the main section.</summary>
        public void BeginColumns(float gapAbove)
        {
            _regionTop = _y + gapAbove;
            _columnX = 0;
        }

        /// <summary>Height available to each column in the region.</summary>
        public float ColumnHeight => _maxHeight == float.MaxValue ? float.MaxValue : _maxHeight - _regionTop;

        /// <summary>
        /// Splits blocks of the given heights, in order, into the fewest columns that fit
        /// <see cref="ColumnHeight"/>, with the blocks spread as evenly as possible (14 blocks in three
        /// columns become 5, 5 and 4). A block taller than a column still gets a column of its own.
        /// </summary>
        public List<List<int>> SplitIntoColumns(IList<float> heights, float gap)
        {
            int count = heights.Count;
            for (int columns = Mathf.Max(1, FewestColumns(heights, gap)); columns < count; columns++)
            {
                var split = EvenSplit(count, columns);
                if (split.TrueForAll(c => ColumnFits(heights, c, gap))) return split;
            }
            return EvenSplit(count, Mathf.Max(1, count));
        }

        // Lower bound: the number of columns a greedy top-to-bottom fill needs.
        private int FewestColumns(IList<float> heights, float gap)
        {
            int columns = 0;
            float y = 0;
            bool empty = true;
            foreach (var h in heights)
            {
                if (empty || y + gap + h > ColumnHeight)
                {
                    columns++;
                    y = h;
                }
                else
                {
                    y += gap + h;
                }
                empty = false;
            }
            return columns;
        }

        // Consecutive runs of indices, the first (count % columns) of them one longer than the rest.
        private static List<List<int>> EvenSplit(int count, int columns)
        {
            var split = new List<List<int>>();
            int index = 0;
            for (int c = 0; c < columns && index < count; c++)
            {
                int size = count / columns + (c < count % columns ? 1 : 0);
                var column = new List<int>();
                for (int k = 0; k < size; k++) column.Add(index++);
                split.Add(column);
            }
            return split;
        }

        private bool ColumnFits(IList<float> heights, List<int> column, float gap)
        {
            if (column.Count <= 1) return true;
            float y = 0;
            for (int k = 0; k < column.Count; k++) y += (k > 0 ? gap : 0) + heights[column[k]];
            return y <= ColumnHeight;
        }

        /// <summary>Places blocks as one column at the top of the region, right of the previous column.</summary>
        /// <param name="columnWidth">Width of the column; the next column starts this far to the right plus the column gap.</param>
        public void AddColumn(IList<FlowLayout> blocks, float gap, float columnWidth)
        {
            float y = _regionTop;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0) y += gap;
                Place(blocks[i], _columnX, y);
                y += blocks[i].Height;
            }
            _columnX += columnWidth + _columnGap;
        }

        private void Place(FlowLayout block, float x, float y)
        {
            _parts.Add(new Part { Layout = block, Offset = new Vector2(x, y) });
            Width = Mathf.Max(Width, x + block.Width);
            Height = Mathf.Max(Height, y + block.Height);
        }

        /// <summary>Calls <paramref name="visit"/> for every op, with the offset of the block it belongs to.</summary>
        public void Visit(System.Action<FlowLayout.Op, Vector2> visit)
        {
            foreach (var part in _parts)
                foreach (var op in part.Layout.Ops)
                    visit(op, part.Offset);
        }
    }
}
