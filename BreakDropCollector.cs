namespace EternalLib
{
    /// <summary>
    /// 把“破坏方块时由游戏生成的掉落”搬进当前收集窗口，避免家具 / 箱子的掉落留在世界上。
    /// </summary>
    /// <remarks>
    /// 家具与箱子的掉落只存在于原版自己的破坏路径里，而本版 tModLoader 对这类方块会忽略
    /// <c>noItem</c>，因此在物品生成的瞬间拦截是唯一可靠的做法。
    /// <para>同时也是“漏网掉落”的观察点：在我们的范围挖掘上下文里、却没有窗口接住的掉落，
    /// 一定是漏的，按警告写日志。</para>
    /// </remarks>
    public sealed class BreakDropCollector : GlobalItem
    {
        /// <summary>漏网日志的条数上限，避免高频路径刷爆日志。</summary>
        private static int _leakLogCount;
        /// <summary>清空漏网日志的计数（库加载 / 卸载时调用）。</summary>
        internal static void ResetLeakLog() => _leakLogCount = 0;
        public override void OnSpawn(Item item, IEntitySource source)
        {
            if (item is not { active: true } || item.IsAir || item.stack <= 0)
            {
                return;
            }
            if (BreakHelper.ActiveDropSink is not { } sink)
            {
                LogLeak(item);
                return;
            }
            int slot = item.whoAmI;
            if (slot < 0 || slot >= Main.item.Length || !ReferenceEquals(Main.item[slot], item))
            {
                //whoAmI 可能还没写好，按引用再找一次
                slot = Array.IndexOf(Main.item, item);
            }
            //原样收走这个实例（maxStack 与 ModItem 数据都在它身上），世界里的槽位换成新物品。
            //绝不能对它调用 TurnToAir()：那样收到的是一个空物品，掉落会“哪里都没有”。
            sink.Add(item);
            item.active = false;
            if (slot < 0 || slot >= Main.item.Length)
            {
                return;
            }
            Main.item[slot] = new Item();
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, slot, 1f);
            }
        }
        /// <summary>报告“没有窗口接住、但位置落在我们范围挖掘上下文里”的掉落（判定见
        /// <see cref="BreakHelper.IsInAoeMiningContext"/>）。</summary>
        private static void LogLeak(Item item)
        {
            //交付过程中生成的物品（使用方自己造的收纳容器）是正常产物
            if (BreakHelper.Delivering || _leakLogCount >= 200)
            {
                return;
            }
            int tileX = (int)(item.position.X / 16f);
            int tileY = (int)(item.position.Y / 16f);
            if (!BreakHelper.IsInAoeMiningContext(tileX, tileY))
            {
                return;
            }
            _leakLogCount++;
            //调用栈回溯开销大，只在打开诊断开关时附带
            string caller = BreakHelper.BreakDiagnostics ? $" callStack={BreakHelper.DescribeCaller()}" : string.Empty;
            Warn($"Leaked drop (no drop sink was open): type={item.type}({Lang.GetItemNameValue(item.type)}) "
                + $"stack={item.stack} at=({tileX},{tileY}) netMode={Main.netMode} "
                + $"ticksSinceLastAoeSwing={BreakHelper.TicksSinceLastAoeSwing} potsNearby={BreakHelper.DescribeNearbyPots(item.position)}"
                + caller);
        }
    }
}
