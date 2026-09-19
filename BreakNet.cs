namespace EternalLib
{
    /// <summary>
    /// 物块破坏相关的联机消息（客户端 → 服务端的破坏请求 / 结算请求，以及服务端的广播）。
    /// <para>全部走库自己的包通道；依赖方无需关心包格式。</para>
    /// </summary>
    internal static class BreakNet
    {
        /// <summary>单个批量破坏包最多携带的坐标数（400 × 4 字节 ≈ 1.6KB，远低于包长度上限）。</summary>
        private const int MaxBatchKillTiles = 400;
        /// <summary>单次“抹墙”请求允许的最大矩形边长（范围挖掘是 28×28，留足余量即可拦住伪造包）。</summary>
        private const int MaxWallAreaSize = 128;
        /// <summary>服务端接受破坏请求的最大距离（像素，≈100 格）。</summary>
        private const float MaxKillReach = 1600f;
        /// <summary>
        /// 请求服务端破坏指定物块。<paramref name="noItem"/> 表示掉落由谁负责：调用方自己生成并同步时传
        /// <c>true</c>，希望服务端按原版掉落表生成时传 <c>false</c>。
        /// </summary>
        internal static void RequestServerKillTile(int x, int y, bool noItem)
        {
            if (Main.netMode == NetmodeID.SinglePlayer || !EternalNet.InTileBounds(x, y))
            {
                return;
            }
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.ServerKillTile);
            packet.Write(x);
            packet.Write(y);
            packet.Write(noItem);
            packet.Send();
        }
        /// <summary>
        /// 批量请求服务端破坏物块（范围挖掘使用）。
        /// <para>一次挥动可能命中上千格，逐格发单格包会浪费带宽；这里按 <see cref="MaxBatchKillTiles"/> 分片。</para>
        /// </summary>
        internal static void RequestServerKillTiles(IReadOnlyList<Point16> tiles, bool noItem = true)
        {
            if (Main.netMode == NetmodeID.SinglePlayer || tiles.Count <= 0)
            {
                return;
            }
            for (int start = 0; start < tiles.Count; start += MaxBatchKillTiles)
            {
                int count = Math.Min(MaxBatchKillTiles, tiles.Count - start);
                ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.ServerKillTiles);
                packet.Write(count);
                packet.Write(noItem);
                for (int i = 0; i < count; i++)
                {
                    EternalNet.WriteTileCoords(packet, tiles[start + i].X, tiles[start + i].Y);
                }
                packet.Send();
            }
        }
        /// <summary>
        /// 请求服务端抹掉一片墙体（范围挖掘用，坐标为 tile 坐标的矩形）。
        /// <para>只发一个矩形而不是上千个坐标；<paramref name="clusterPosition"/> 是掉落要交付到的位置。</para>
        /// </summary>
        internal static void RequestServerKillWalls(Rectangle area, Vector2 clusterPosition)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || area.Width <= 0 || area.Height <= 0)
            {
                return;
            }
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.ServerKillWalls);
            EternalNet.WriteRect(packet, area);
            packet.Write((int)clusterPosition.X);
            packet.Write((int)clusterPosition.Y);
            packet.Send();
        }
        /// <summary>
        /// 请求服务端结算一批方块的掉落并破坏它们（范围挖掘用，坐标为多格物块的左上角）。
        /// <para><b>模组方块</b>（带内容物的机器 / 家具）的掉落必须由服务端来算：客户端手上的实体数据
        /// 不一定完整，这与箱子是同一套“服务端权威”思路。服务端算完会把掉落交付到
        /// <paramref name="clusterPosition"/> 处。</para>
        /// </summary>
        internal static void RequestLootTiles(IReadOnlyList<Point16> origins, Vector2 clusterPosition)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || origins.Count <= 0)
            {
                return;
            }
            for (int start = 0; start < origins.Count; start += MaxBatchKillTiles)
            {
                int count = Math.Min(MaxBatchKillTiles, origins.Count - start);
                ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.RequestLootTiles);
                packet.Write(count);
                packet.Write((int)clusterPosition.X);
                packet.Write((int)clusterPosition.Y);
                for (int i = 0; i < count; i++)
                {
                    EternalNet.WriteTileCoords(packet, origins[start + i].X, origins[start + i].Y);
                }
                packet.Send();
            }
        }
        /// <summary>
        /// 请求服务端清空并破坏箱子。
        /// <para>箱子内容物由服务端取（客户端可能从未打开过该箱子，本地副本是空的），
        /// 服务端取完会交付掉落并广播给其它客户端做本地清理。</para>
        /// </summary>
        internal static void RequestChestBreak(int x, int y)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || !EternalNet.InTileBounds(x, y))
            {
                return;
            }
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.RequestChestBreak);
            packet.Write(x);
            packet.Write(y);
            packet.Send();
        }
        internal static void HandleOnServer(EternalNet.MessageType type, BinaryReader reader, int whoAmI)
        {
            switch (type)
            {
                case EternalNet.MessageType.ServerKillTile:
                    HandleServerKillTile(reader, whoAmI);
                    break;
                case EternalNet.MessageType.ServerKillTiles:
                    HandleServerKillTiles(reader, whoAmI);
                    break;
                case EternalNet.MessageType.ServerKillWalls:
                    HandleServerKillWalls(reader, whoAmI);
                    break;
                case EternalNet.MessageType.RequestLootTiles:
                    HandleRequestLootTiles(reader, whoAmI);
                    break;
                case EternalNet.MessageType.RequestChestBreak:
                    HandleRequestChestBreak(reader, whoAmI);
                    break;
            }
        }
        internal static void HandleOnClient(EternalNet.MessageType type, BinaryReader reader)
        {
            if (type == EternalNet.MessageType.RequestChestBreak)
            {
                HandleChestBreakBroadcast(reader);
            }
        }
        /// <summary>服务端：箱子内容物只有服务端拥有权威副本，取内容 → 交付掉落 → 破坏箱子，再转发给其它客户端。</summary>
        private static void HandleRequestChestBreak(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (!EternalNet.InTileBounds(x, y))
            {
                return;
            }
            BreakHelper.LootAndBreakChestOnServer(x, y);
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.RequestChestBreak);
            packet.Write(x);
            packet.Write(y);
            packet.Send(ignoreClient: whoAmI);
            NetMessage.SendTileSquare(-1, x, y, 2, 2);
        }
        /// <summary>客户端：服务端已经取走内容物并交付掉落，这里只做本地清理（清空箱子副本 + 破坏方块）。</summary>
        private static void HandleChestBreakBroadcast(BinaryReader reader)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (!EternalNet.InTileBounds(x, y))
            {
                return;
            }
            BreakHelper.ClearAndBreakChestAt(x, y);
        }
        private static void HandleServerKillTile(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            bool noItem = reader.ReadBoolean();
            if (!EternalNet.InTileBounds(x, y))
            {
                return;
            }
            //只接受来自有效玩家、且目标在其附近（1600 像素≈100 格）的请求；工具本身的判定在客户端完成，
            //这里只是拦住明显的伪造包。
            Player? player = GetRequestPlayer(whoAmI);
            if (whoAmI >= 0 && (player is null || !WithinReach(player, x, y)))
            {
                return;
            }
            List<Item> wallDrops = [];
            BreakHelper.KillWallWithDrops(x, y, wallDrops);
            if (noItem)
            {
                //走助手以屏蔽家具类方块“无视 noItem”的自动掉落（tML #1269）；
                //服务端调用时它不会发包，只做本地破坏。
                BreakHelper.BreakTile(x, y);
            }
            else
            {
                WorldGen.KillTile(x, y);
            }
            BreakHelper.DeliverExternalDrops(wallDrops, x, y, player, BreakHelper.GetHeldAoeTool(player));
        }
        /// <summary>
        /// 批量破坏：语义与 <see cref="HandleServerKillTile"/> 相同，只是一次处理多格坐标。
        /// <para>越界坐标或超出玩家可达范围的格直接跳过，不影响同一包里其余坐标。</para>
        /// </summary>
        private static void HandleServerKillTiles(BinaryReader reader, int whoAmI)
        {
            int count = reader.ReadInt32();
            bool noItem = reader.ReadBoolean();
            if (count <= 0 || count > MaxBatchKillTiles)
            {
                return;
            }
            Player? player = GetRequestPlayer(whoAmI);
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            //抹墙时“非环境墙”会掉一份墙体物品：收起来，循环结束后一起交付，
            //免得它们散落在世界上。
            List<Item> wallDrops = [];
            for (int i = 0; i < count; i++)
            {
                //坐标是 short（见 WriteTileCoords），必须按 short 读
                Point16 coords = EternalNet.ReadTileCoords(reader);
                int x = coords.X;
                int y = coords.Y;
                if (!EternalNet.InTileBounds(x, y) || player is not null && !WithinReach(player, x, y))
                {
                    continue;
                }
                List<Item> perTileWallDrops = [];
                BreakHelper.KillWallWithDrops(x, y, perTileWallDrops);
                if (noItem)
                {
                    BreakHelper.BreakTile(x, y);
                }
                else
                {
                    WorldGen.KillTile(x, y);
                }
                wallDrops.AddRange(perTileWallDrops);
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
            //服务端破坏方块不会自动同步地形，必须主动发一次（按破坏范围的包围盒，只发一个区域）
            if (Main.netMode == NetmodeID.Server && maxX >= minX)
            {
                NetMessage.SendTileSquare(-1, minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            if (wallDrops.Count > 0 && maxX >= minX)
            {
                BreakHelper.DeliverExternalDrops(wallDrops, (minX + maxX) / 2, (minY + maxY) / 2,
                    player, BreakHelper.GetHeldAoeTool(player));
            }
        }
        /// <summary>
        /// 服务端：抹掉客户端上报的矩形范围内的墙体（范围挖掘用），并把结果同步回去。
        /// <para>客户端只负责上报范围，抹除与掉落都由服务端完成（服务端权威）：
        /// 少了这一步，服务端随后发出的地形同步就会把客户端刚挖掉的墙“长回来”。</para>
        /// <para>非环境墙（玩家放置的墙）被抹掉时会掉一份墙体物品，这里收住并交付到客户端指定的位置。</para>
        /// </summary>
        private static void HandleServerKillWalls(BinaryReader reader, int whoAmI)
        {
            Rectangle area = EternalNet.ReadRect(reader);
            Vector2 clusterPosition = new(reader.ReadInt32(), reader.ReadInt32());
            //矩形不能过大（拦住伪造包一次性抹掉整张地图的墙），也不能越界
            if (area.Width <= 0 || area.Height <= 0 || area.Width > MaxWallAreaSize || area.Height > MaxWallAreaSize)
            {
                return;
            }
            int minX = Math.Max(0, area.X);
            int minY = Math.Max(0, area.Y);
            int maxX = Math.Min(Main.maxTilesX - 1, area.X + area.Width - 1);
            int maxY = Math.Min(Main.maxTilesY - 1, area.Y + area.Height - 1);
            if (maxX < minX || maxY < minY)
            {
                return;
            }
            Player? player = GetRequestPlayer(whoAmI);
            List<Item> wallDrops = [];
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (player is not null && !WithinReach(player, x, y))
                    {
                        continue;
                    }
                    if (Framing.GetTileSafely(x, y).WallType == WallID.None)
                    {
                        continue;
                    }

                    BreakHelper.KillWallWithDrops(x, y, wallDrops);
                }
            }
            BreakHelper.DeliverExternalDrops(wallDrops, (int)(clusterPosition.X / 16f), (int)(clusterPosition.Y / 16f),
                player, BreakHelper.GetHeldAoeTool(player));
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendTileSquare(-1, minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
        }
        /// <summary>
        /// 服务端结算范围挖掘的一批方块：由服务端调用模组的 <c>GetItemDrops</c> / 箱子内容物取出，
        /// 再把结果交付到客户端指定的位置。
        /// </summary>
        private static void HandleRequestLootTiles(BinaryReader reader, int whoAmI)
        {
            int count = reader.ReadInt32();
            int clusterX = reader.ReadInt32();
            int clusterY = reader.ReadInt32();
            if (count is <= 0 or > MaxBatchKillTiles)
            {
                return;
            }
            Player? player = GetRequestPlayer(whoAmI);
            //发包玩家手上的工具就是这次结算的责任人（附加掉落与交付策略都从它身上取）
            BreakHelper.AoeSwing swing = new(player);
            for (int i = 0; i < count; i++)
            {
                //坐标是 short（见 WriteTileCoords），必须按 short 读
                Point16 coords = EternalNet.ReadTileCoords(reader);
                int x = coords.X;
                int y = coords.Y;
                if (!EternalNet.InTileBounds(x, y) || player is not null && !WithinReach(player, x, y))
                {
                    continue;
                }
                BreakHelper.ProcessAoeTile(Framing.GetTileSafely(x, y), x, y, swing);
            }
            BreakHelper.FinishAoeSwing(swing, new Vector2(clusterX, clusterY));
        }
        /// <summary>取发包玩家（whoAmI &lt; 0 表示非玩家来源，返回 null）。</summary>
        private static Player? GetRequestPlayer(int whoAmI)
            => whoAmI >= 0 && EternalNet.ValidPlayerIndex(whoAmI) && Main.player[whoAmI] is { active: true } player
                ? player
                : null;
        /// <summary>目标物块是否在玩家可达范围内（用于拦截明显的伪造包）。</summary>
        private static bool WithinReach(Player player, int x, int y)
        {
            float dx = Math.Abs(player.Center.X - (x * 16f + 8f));
            float dy = Math.Abs(player.Center.Y - (y * 16f + 8f));
            return dx <= MaxKillReach && dy <= MaxKillReach;
        }
    }
}
