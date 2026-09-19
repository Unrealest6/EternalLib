namespace EternalLib
{
    /// <summary>
    /// 物块破坏相关的全局钩子。
    /// <para><see cref="CanDrop"/>：<see cref="BreakHelper"/> 主动破坏方块时屏蔽自动掉落。本版 tModLoader
    /// 对家具类方块会忽略 <c>noItem</c>，箱子 / 工作台 / 收集器都会额外掉一份；但“破坏时现算随机奖励”
    /// 的方块（罐子等）一旦被屏蔽奖励就凭空消失，所以它们永不放行。</para>
    /// <para><see cref="KillTile"/> / <see cref="TileFrame"/>：没有收集窗口时临时开窗口，
    /// 接住这类方块被框架级联破坏时产生的奖励；奖励的唯一生成点另有
    /// <see cref="EternalLib.SpawnThingsFromPotHook"/>。</para>
    /// </summary>
    public sealed class BreakGlobalTile : GlobalTile
    {
        public override bool CanDrop(int i, int j, int type)
            => !BreakHelper.SuppressTileDrops || BreakHelper.IsRandomLootTile(type);
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!BreakHelper.IsRandomLootTile(type) || BreakHelper.ActiveDropSink is not null)
            {
                base.KillTile(i, j, type, ref fail, ref effectOnly, ref noItem);
                return;
            }
            //没有窗口：临时接住这次破坏产生的掉落（罐子奖励可能就在这里现算生成）
            List<Item> captured = [];
            List<Item>? previousSink = BreakHelper.BeginExternalCapture(captured);
            try
            {
                base.KillTile(i, j, type, ref fail, ref effectOnly, ref noItem);
            }
            finally
            {
                BreakHelper.EndExternalCapture(previousSink);
            }
            BreakHelper.DeliverExternalDrops(captured, i, j);
        }
        /// <summary>
        /// 这类方块还有一条独立的奖励生成路径：<c>WorldGen.TileFrame</c> → <c>WorldGen.CheckPot</c>
        /// （支撑被拆、框架刷新、收到地形同步后的重新判帧都会走）。
        /// <para>本钩子在原版 <c>TileFrame</c> 函数体之前执行，开在这里的窗口盖不住 <c>CheckPot</c> 做事的那一刻，
        /// 真正的兜底是 <c>WorldGen.SpawnThingsFromPot</c> 上的钩子；这里只覆盖少数其它分支。</para>
        /// </summary>
        public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak)
        {
            if (!BreakHelper.IsRandomLootTile(type)
                || BreakHelper.ActiveDropSink is not null
                || !BreakHelper.InBounds(i, j)
                || !Framing.GetTileSafely(i, j).HasTile)
            {
                return base.TileFrame(i, j, type, ref resetFrame, ref noBreak);
            }
            List<Item> captured = [];
            List<Item>? previousSink = BreakHelper.BeginExternalCapture(captured);
            bool result;
            try
            {
                result = base.TileFrame(i, j, type, ref resetFrame, ref noBreak);
            }
            finally
            {
                BreakHelper.EndExternalCapture(previousSink);
            }
            BreakHelper.DeliverExternalDrops(captured, i, j);
            return result;
        }
    }
}
