namespace EternalLib
{
    public static class TileHelper
    {
        public static Item[]? ProcessChestMining(Tile tile, int x, int y, bool drop = true)
        {
            if (tile.TileFrameX % 36 != 0)
            {
                x--;
            }
            if (tile.TileFrameY % 36 != 0)
            {
                y--;
            }
            int chestIndex = Chest.FindChest(x, y);
            if (Main.netMode == NetmodeID.MultiplayerClient && chestIndex != -1)
            {
                NetMessage.SendData(MessageID.RequestChestOpen, number: x, number2: y);
            }
            Item[]? savedItems = null;
            if (chestIndex != -1)
            {
                savedItems = SaveChestContents(chestIndex);
            }
            if (savedItems == null)
            {
                return null;
            }
            bool success = ForceBreakChestTile(x, y);
            if (!success)
            {
                return null;
            }
            if (drop)
            {
                DropChestContents(savedItems, x, y);
            }
            return savedItems;

        }
        private static bool ForceBreakChestTile(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
            {
                return false;
            }
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null,
                    0, x, y);
            }
            int chestIndex = Chest.FindChest(x, y);
            Main.chest[chestIndex].item = new Item[Main.chest[chestIndex].item.Length];
            if (chestIndex != -1)
            {
                Main.chest[chestIndex] = null;
            }
            WorldGen.KillTile(x, y);
            return true;
        }
        private static Item[]? SaveChestContents(int chestIndex)
        {
            if (chestIndex < 0 || chestIndex >= Main.chest.Length || Main.chest[chestIndex] is not { } chest)
            {
                return null;
            }
            Item[] saved = new Item[chest.item.Length];
            bool hasItems = false;
            for (int i = 0; i < chest.item.Length; i++)
            {
                if (chest.item[i] == null)
                {
                    continue;
                }
                saved[i] = chest.item[i].Clone();
                hasItems = true;
            }
            return hasItems ? saved : null;
        }
        private static void DropChestContents(Item[] items, int x, int y)
        {
            Vector2 center = new(x * 16f, y * 16f);
            foreach (Item item in items)
            {
                if (item is not { stack: > 0 })
                {
                    continue;
                }
                float angle = Main.rand.NextFloat(0, MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(0, 16);
                Vector2 dropPos = center + new Vector2((float)Math.Cos(angle) * distance, (float)Math.Sin(angle) * distance);
                Item.NewItem(WorldGen.GetItemSource_FromTileBreak(x, y), (int)dropPos.X, (int)dropPos.Y, 16, 16,
                    item.type, item.stack, false, item.prefix);
            }
        }
    }
}