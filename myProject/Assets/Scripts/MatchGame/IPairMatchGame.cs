using System.Collections.Generic;

namespace VitaMj.MatchGame
{
    /// <summary>分层配对玩法运行时模型（程序化棋盘或关卡表共用）。</summary>
    public interface IPairMatchGame
    {
        IReadOnlyList<LayeredGridCell> Cells { get; }
        int? SelectedCellId { get; }
        bool IsComplete { get; }

        /// <summary>收纳栏容量；0 表示传统双点配对（无收纳栏）。</summary>
        int MatchBarCapacity { get; }

        /// <summary>当前收纳栏中的格子 Id（从左到右）。</summary>
        IReadOnlyList<int> MatchBarCellIds { get; }

        LayeredGridCell GetCell(int id);
        bool CanClick(int cellId);
        LayeredMatchClickResult TryClick(int cellId);

        /// <summary>收纳栏模式：撤回队尾最后一张入场牌放回棋盘。</summary>
        bool TryRevertLastMatchBarEntry();
    }
}
