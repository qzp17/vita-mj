namespace VitaMj.MatchGame
{
    /// <summary>
    /// 单层错层堆叠中的一枚格子。Layer=0 为最底层（尺寸最大），每往上一层行、列各减 1（矩形 n×m 同理）。
    /// </summary>
    public sealed class LayeredGridCell
    {
        public LayeredGridCell(int id, int layer, int row, int col)
        {
            Id = id;
            Layer = layer;
            Row = row;
            Col = col;
        }

        public int Id { get; }
        /// <summary>0 表示最底层。</summary>
        public int Layer { get; }
        public int Row { get; }
        public int Col { get; }

        /// <summary>配对牌面数值，从 1 开始。</summary>
        public int Value { get; internal set; }

        public bool Eliminated { get; internal set; }

        /// <summary>压住本格的上层格子 Id（位于 Layer+1）。顶层格子集合为空。</summary>
        internal int[] BlockerIds { get; set; } = System.Array.Empty<int>();
    }
}
