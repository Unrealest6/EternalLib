namespace EternalLib
{
    /// <summary>
    /// 物块相关操作的安全封装。
    /// <para>[已过时] 箱子开采请改用 <see cref="BreakHelper.TakeChestLoot"/>（单人 / 服务端）与
    /// <see cref="BreakHelper.LootAndBreakChestOnServer"/>（服务端权威）：它们会把内容物交给掉落收集窗口，
    /// 而这里会直接把内容物掉在世界上。</para>
    /// </summary>
    [Obsolete("Use BreakHelper.TakeChestLoot / LootAndBreakChestOnServer instead.")]
    public static class TileHelper
    {
        /// <summary>
        /// 开采包含收纳物（箱子）的物块，并返回箱内物品。
        /// <para>返回 <c>null</c> 表示该位置没有箱子或操作失败；箱子存在但为空时返回空数组。</para>
        /// </summary>
        /// <param name="x">物块 x 坐标（任意一格即可）</param>
        /// <param name="y">物块 y 坐标（任意一格即可）</param>
        /// <param name="drop">是否把箱内物品掉落到世界中</param>
        public static Item[]? ProcessChestMining(int x, int y, bool drop = true)
            => TryProcessChestMining(x, y, drop, out Item[] items) ? items : null;
        /// <summary>
        /// 开采包含收纳物（箱子）的物块。
        /// </summary>
        /// <param name="x">物块 x 坐标（箱子所占任意一格即可）</param>
        /// <param name="y">物块 y 坐标（箱子所占任意一格即可）</param>
        /// <param name="drop">是否把箱内物品掉落到世界中</param>
        /// <param name="items">箱内物品快照；箱子存在时一定非 null</param>
        /// <returns>是否成功找到并破坏了箱子</returns>
        public static bool TryProcessChestMining(int x, int y, bool drop, out Item[] items)
        {
            items = [];
            // FindChestByGuessing：传入箱子范围内任意一格都能命中，不像按 TileFrameX/Y % 36 猜左上角那样会因坐标修正整体偏移一格
            int chestIndex = Chest.FindChestByGuessing(x, y);
            if (chestIndex < 0 || chestIndex >= Main.chest.Length || Main.chest[chestIndex] is not { } chest)
            {
                return false;
            }
            int chestX = chest.x;
            int chestY = chest.y;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.RequestChestOpen, number: chestX, number2: chestY);
            }
            items = SaveChestContents(chest);
            // 必须先清空箱子数据再破坏物块，否则原版 KillTile 会把箱内物品再掉一次；DestroyChest 同时负责多人同步
            Chest.DestroyChest(chestX, chestY);
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, chestX, chestY);
            }
            WorldGen.KillTile(chestX, chestY);
            if (drop)
            {
                DropChestContents(items, chestX, chestY);
            }
            return true;
        }
        /// <summary>把箱子内容复制成独立数组（包含空槽位，索引与原箱子一致）。</summary>
        public static Item[] SaveChestContents(Chest chest)
        {
            Item[] saved = new Item[chest.item.Length];
            for (int i = 0; i < chest.item.Length; i++)
            {
                Item item = chest.item[i];
                if (item is null || item.IsAir || item.stack <= 0)
                {
                    continue;
                }
                saved[i] = item.Clone();
            }
            return saved;
        }
        /// <summary>把箱内物品掉落到指定物块位置附近。</summary>
        public static void DropChestContents(Item[] items, int x, int y)
        {
            Vector2 center = new(x * 16f, y * 16f);
            foreach (Item item in items)
            {
                if (item is not { stack: > 0 } || item.IsAir)
                {
                    continue;
                }
                float angle = Main.rand.NextFloat(0, MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(0, 16);
                Vector2 dropPosition = center + new Vector2((float)Math.Cos(angle) * distance, (float)Math.Sin(angle) * distance);
                Item.NewItem(WorldGen.GetItemSource_FromTileBreak(x, y), (int)dropPosition.X, (int)dropPosition.Y, 16, 16,
                    item.type, item.stack, false, item.prefix);
            }
        }
    }
}
