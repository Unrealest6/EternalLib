namespace EternalLib
{
    /// <summary>
    /// 范围挖掘工具的契约：<b>由使用库的模组在自己的工具物品（<see cref="ModItem"/>）上实现</b>。
    /// <para>哪些物块要额外产出、收拢到的掉落交给谁，都由实现它的物品自己回答，库不保存使用方的数值。</para>
    /// <para>用法：实现本接口后，在自己的使用逻辑里调用 <see cref="BreakHelper.AoeSwing"/>、
    /// <see cref="BreakHelper.ProcessAoeTile"/> 与 <see cref="BreakHelper.FinishAoeSwing"/>
    /// （破坏、掉落、抹墙与多人服务端权威结算都在其中）。</para>
    /// </summary>
    public interface IAoeMiningTool
    {
        /// <summary>取一件物品上的范围挖掘工具（不是这种工具时返回 null）；库用它解析责任人。</summary>
        // ReSharper disable once SuspiciousTypeConversion.Global
        public static IAoeMiningTool? GetIAoeMiningTool(Item item) => item.ModItem as IAoeMiningTool;

        /// <summary>
        /// 额外掉落表：键是“按物块类型索引的开关表”（下标即 <c>Tile.Type</c>，可直接传
        /// <c>TileID.Sets.Ore</c> 这类 <c>bool[]</c>），值是套用到这一格掉落上的
        /// <see cref="Int32Modifier"/>（同一格命中多项时依次叠加）。
        /// <para>每次读取都会重新求值，所以“每格现掷随机倍率”写在 getter 里即可；默认 <c>null</c> 表示不加成。</para>
        /// </summary>
        IReadOnlyDictionary<bool[], Int32Modifier>? ExtraDropModifier => null;

        /// <summary>
        /// 把一次挖掘收拢到的掉落交付出去（例如合并成自己的收纳物品）。
        /// <para>默认散落到世界，保证东西不会凭空消失。库调用后<b>会清空</b> <paramref name="drops"/>，
        /// 实现方不需要自己清。</para>
        /// </summary>
        void DeliverDrops(Player? player, List<Item> drops, Vector2 position) => BreakHelper.ScatterDrops(drops, position);
    }
}
