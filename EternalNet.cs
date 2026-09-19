namespace EternalLib
{
    /// <summary>
    /// EternalLib 的网络层：库内消息的分发与字段读写。
    /// <para><b>顺序即协议</b>：新增消息类型只能追加到枚举末尾，否则新旧版本之间的包会错位。
    /// 库走自己的包通道，与使用方的包互不干扰。</para>
    /// </summary>
    public static class EternalNet
    {
        /// <summary>
        /// 库内协议版本：随每次 <see cref="NewPacket"/> 写入，接收时校验，不一致的包直接丢弃。
        /// <para>改包格式（增删字段、改变字段宽度）时必须递增，否则新旧版本混用会静默错位。</para>
        /// </summary>
        public const byte ProtocolVersion = 1;
        /// <summary>
        /// 库内消息类型。<b>数值即协议</b>：每个成员都写死数值，不要复用或重排既有数值
        /// （新增消息请追加新数值）。
        /// </summary>
        public enum MessageType : byte
        {
            /// <summary>手持物形态：客户端 → 服务端（mode）／服务端 → 其它客户端（whoAmI, mode）</summary>
            SyncItemMode = 0,
            /// <summary>客户端请求服务端回传所有玩家的手持物形态</summary>
            RequestItemModes = 1,
            /// <summary>服务端回传所有玩家的手持物形态快照</summary>
            BroadcastItemModes = 2,
            /// <summary>客户端请求服务端破坏一格物块（x, y, noItem）</summary>
            ServerKillTile = 3,
            /// <summary>客户端批量请求服务端破坏物块（count, noItem, 坐标…）</summary>
            ServerKillTiles = 4,
            /// <summary>客户端请求服务端抹掉一片墙体（矩形 + 掉落交付位置）</summary>
            ServerKillWalls = 5,
            /// <summary>客户端请求服务端结算一批方块的掉落（count, 交付位置, 坐标…）</summary>
            RequestLootTiles = 6,
            /// <summary>客户端请求服务端清空并破坏箱子（x, y）</summary>
            RequestChestBreak = 7
        }
        /// <summary>构造一个已写好消息类型与协议版本的包（必须在库已加载时调用）。</summary>
        internal static ModPacket NewPacket(MessageType type)
        {
            ModPacket packet = ModContent.GetInstance<EternalLib>().GetPacket();
            packet.Write((byte)type);
            packet.Write(ProtocolVersion);
            return packet;
        }
        /// <summary>
        /// 写入一格坐标。<b>必须与 <see cref="ReadTileCoords"/> 成对使用</b>：统一写成 <c>short</c>（2 字节）。
        /// 一边写 short、一边按 int 读会让对端 <c>Read underflow</c> 并让整包作废。
        /// </summary>
        internal static void WriteTileCoords(ModPacket packet, int x, int y)
        {
            packet.Write((short)x);
            packet.Write((short)y);
        }
        /// <summary>读取一格坐标（与 <see cref="WriteTileCoords"/> 成对）。</summary>
        internal static Point16 ReadTileCoords(BinaryReader reader) => new(reader.ReadInt16(), reader.ReadInt16());
        /// <summary>写入一片矩形区域（tile 坐标，int）。<b>必须与 <see cref="ReadRect"/> 成对使用</b>。</summary>
        internal static void WriteRect(ModPacket packet, Rectangle area)
        {
            packet.Write(area.X);
            packet.Write(area.Y);
            packet.Write(area.Width);
            packet.Write(area.Height);
        }
        /// <summary>读取一片矩形区域（与 <see cref="WriteRect"/> 成对）。</summary>
        internal static Rectangle ReadRect(BinaryReader reader)
            => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        /// <summary>玩家下标是否合法。</summary>
        internal static bool ValidPlayerIndex(int index) => index is >= 0 and < Main.maxPlayers;
        /// <summary>物块坐标是否在世界内。</summary>
        internal static bool InTileBounds(int x, int y) => x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY;
        /// <summary>依赖方调用：把库的包分发到对应处理器。</summary>
        internal static void Handle(BinaryReader reader, int whoAmI)
        {
            MessageType type = (MessageType)reader.ReadByte();
            byte version = reader.ReadByte();
            if (version != ProtocolVersion)
            {
                EternalLog.Warn($"Dropped a packet with protocol version {version} (this build uses {ProtocolVersion}); "
                    + "the other side is running a different EternalLib build.");
                return;
            }
            if (Main.netMode == NetmodeID.Server)
            {
                FrameNet.HandleOnServer(type, reader, whoAmI);
                BreakNet.HandleOnServer(type, reader, whoAmI);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                FrameNet.HandleOnClient(type, reader, whoAmI);
                BreakNet.HandleOnClient(type, reader);
            }
        }
    }
}
