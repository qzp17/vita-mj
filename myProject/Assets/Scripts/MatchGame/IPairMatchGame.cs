using System.Collections.Generic;

namespace VitaMj.MatchGame
{
    /// <summary>分层配对玩法运行时模型（程序化棋盘或关卡表共用）。</summary>
    public interface IPairMatchGame
    {
        IReadOnlyList<LayeredGridCell> Cells { get; }
        int? SelectedCellId { get; }
        bool IsComplete { get; }

        LayeredGridCell GetCell(int id);
        bool CanClick(int cellId);
        LayeredMatchClickResult TryClick(int cellId);
    }
}
