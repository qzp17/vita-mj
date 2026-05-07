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

        /// <summary>
        /// 收纳栏最近一次抵消所移除的格子 Id（可由 <see cref="TryClick"/>+<see cref="CompleteDeferredMatchBarMerges"/> 提供；连环合并可能多于 2 个）。
        /// </summary>
        IReadOnlyList<int> LastMatchBarMergedAwayCellIds { get; }

        /// <summary>
        /// 上一次收纳栏点击已入栏，但尚在等待「飞入栏位落定」之后再执行抵消时（见 <see cref="CompleteDeferredMatchBarMerges"/>）为 true。
        /// </summary>
        bool PendingMatchBarMergeAfterFly { get; }

        LayeredGridCell GetCell(int id);
        bool CanClick(int cellId);
        LayeredMatchClickResult TryClick(int cellId);

        /// <summary>
        /// 在入栏动画结束后调用：若有挂起的抵消则执行并从栏移除，返回是否为抵消；否则保持原状返回 <see cref="LayeredMatchClickResult.MatchBarEnqueued"/>。
        /// </summary>
        LayeredMatchClickResult CompleteDeferredMatchBarMerges();

        /// <summary>收纳栏模式：撤回队尾最后一张入场牌放回棋盘。</summary>
        bool TryRevertLastMatchBarEntry();
    }
}
