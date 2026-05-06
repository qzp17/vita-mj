namespace VitaMj.MatchGame
{
    /// <summary>配对对局开局玩法（与关卡布局数据无关）。</summary>
    public enum MatchPlayStyle
    {
        /// <summary>依次点两张相同数字消除（原版）。</summary>
        ClassicPairClick,

        /// <summary>点击下方牌移入收纳栏；栏尾相邻相同则抵消，栏满再点失败。</summary>
        MatchBarQueue,
    }
}
