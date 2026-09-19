namespace EternalLib
{
    /// <summary>
    /// 序列帧物品“形态”的联机同步。
    /// <para>形态存在物品实例上，本地读实例即可；远程玩家则读 <see cref="FramePlayer.HeldItemMode"/>
    /// （由这里同步：切换形态或换到形态不同的同款物品时补发一次，不做每帧发包）。</para>
    /// </summary>
    internal static class FrameNet
    {
        /// <summary>把手持物品的形态推给服务端（服务端再转发给其它客户端）。</summary>
        internal static void SendHeldItemMode(byte mode)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                return;
            }
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.SyncItemMode);
            packet.Write(mode);
            packet.Send();
        }
        /// <summary>请求服务端回传所有玩家的手持物形态（进入世界时使用）。</summary>
        internal static void RequestItemModes()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                return;
            }
            EternalNet.NewPacket(EternalNet.MessageType.RequestItemModes).Send();
        }
        internal static void HandleOnServer(EternalNet.MessageType type, BinaryReader reader, int whoAmI)
        {
            switch (type)
            {
                case EternalNet.MessageType.SyncItemMode:
                    HandleItemModeRequest(reader, whoAmI);
                    break;
                case EternalNet.MessageType.RequestItemModes:
                    HandleRequestItemModes(whoAmI);
                    break;
            }
        }
        internal static void HandleOnClient(EternalNet.MessageType type, BinaryReader reader, int whoAmI)
        {
            switch (type)
            {
                case EternalNet.MessageType.SyncItemMode:
                    HandleItemModeBroadcast(reader);
                    break;
                case EternalNet.MessageType.BroadcastItemModes:
                    HandleItemModesSnapshot(reader);
                    break;
            }
        }
        /// <summary>服务端：记下这个玩家的手持形态，并转发给其它客户端。</summary>
        private static void HandleItemModeRequest(BinaryReader reader, int whoAmI)
        {
            byte mode = reader.ReadByte();
            if (!EternalNet.ValidPlayerIndex(whoAmI) || !Main.player[whoAmI].TryGetModPlayer(out FramePlayer framePlayer))
            {
                return;
            }
            framePlayer.HeldItemMode = mode;
            ModPacket packet = EternalNet.NewPacket(EternalNet.MessageType.SyncItemMode);
            packet.Write((byte)whoAmI);
            packet.Write(mode);
            //请求方本地已经是这个形态，排除它可避免重复结算
            packet.Send(ignoreClient: whoAmI);
        }
        /// <summary>服务端：把指定玩家的手持形态回传给请求的客户端（含所有玩家）。</summary>
        private static void HandleRequestItemModes(int whoAmI)
        {
            if (!EternalNet.ValidPlayerIndex(whoAmI))
            {
                return;
            }
            ModPacket response = EternalNet.NewPacket(EternalNet.MessageType.BroadcastItemModes);
            response.Write((byte)Main.maxPlayers);
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                byte mode = player is { active: true } && player.TryGetModPlayer(out FramePlayer framePlayer)
                    ? framePlayer.HeldItemMode
                    : (byte)0;
                response.Write((byte)i);
                response.Write(mode);
            }
            response.Send(whoAmI);
        }
        /// <summary>客户端：收到某个玩家的手持形态。</summary>
        private static void HandleItemModeBroadcast(BinaryReader reader)
        {
            int playerIndex = reader.ReadByte();
            byte mode = reader.ReadByte();
            if (!EternalNet.ValidPlayerIndex(playerIndex) || !Main.player[playerIndex].TryGetModPlayer(out FramePlayer framePlayer))
            {
                return;
            }
            framePlayer.HeldItemMode = mode;
        }
        /// <summary>客户端：收到所有玩家的手持形态快照。</summary>
        private static void HandleItemModesSnapshot(BinaryReader reader)
        {
            int count = reader.ReadByte();
            //上界校验：伪造包里的数量会让读取越界
            if (count is <= 0 or > Main.maxPlayers)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                int playerIndex = reader.ReadByte();
                byte mode = reader.ReadByte();
                if (!EternalNet.ValidPlayerIndex(playerIndex) || !Main.player[playerIndex].TryGetModPlayer(out FramePlayer framePlayer))
                {
                    continue;
                }
                framePlayer.HeldItemMode = mode;
            }
        }
    }
}
