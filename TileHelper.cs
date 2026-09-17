namespace EternalLib
{
    /// <summary>
    /// 物块相关操作的安全封装。
    /// </summary>
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
            // 旧实现用 TileFrameX/Y % 36 猜测箱子左上角，调用方若是先做过一次同样的修正
            // 就会整体偏移一格；FindChestByGuessing 允许传入箱子范围内任意一格，稳健得多。
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
            // 必须先清空箱子数据再破坏物块，否则原版会在 KillTile 里把箱内物品再掉一次；
            // 旧实现直接给 Main.chest[index] 赋 null 并手动清空 item 数组，
            // 既跳过了原版的同步逻辑，也会在 index 为 -1 时抛 IndexOutOfRangeException。
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