namespace VitaMj.MatchGame
{
    /// <summary>
    /// 分层配对中的一枚格子。可由程序化棋盘或关卡表生成。
    /// </summary>
    public sealed class LayeredGridCell
    {
        public LayeredGridCell(int id, int layer, int row, int col, string designerId = null)
        {
            Id = id;
            Layer = layer;
            Row = row;
            Col = col;
            DesignerId = designerId ?? string.Empty;
        }

        public int Id { get; }
        /// <summary>0 表示最底层。</summary>
        public int Layer { get; }
        /// <summary>逻辑格点行，用于自动推算遮挡（上层 2×2 压住下层）。</summary>
        public int Row { get; }
        /// <summary>逻辑格点列。</summary>
        public int Col { get; }

        /// <summary>策划自定义 id，便于表引用 blockedBy。</summary>
        public string DesignerId { get; }

        /// <summary>为 true 时在 UI 上使用 <see cref="DisplayX"/>/<see cref="DisplayY"/>，否则由 Binder 按网格排版。</summary>
        public bool UseDesignerCoordinates { get; internal set; }

        /// <summary>FairyGUI 父容器内坐标。</summary>
        public float DisplayX { get; internal set; }
        public float DisplayY { get; internal set; }

        /// <summary>配对牌面数值，从 1 开始。</summary>
        public int Value { get; internal set; }

        public bool Eliminated { get; internal set; }

        /// <summary>压住本格的上层格子 Id（位于 Layer+1）。顶层格子集合为空。</summary>
        internal int[] BlockerIds { get; set; } = System.Array.Empty<int>();
    }
}
