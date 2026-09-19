namespace EternalLib
{
    /// <summary>
    /// 序列帧物品“形态”的按玩家联机状态。
    /// <para>本地玩家直接读物品实例；远程玩家的实例数据不一定同步过来，因此这里缓存一份最近同步值，
    /// 并负责把手持形态推给服务端、拉取其它玩家的形态。</para>
    /// </summary>
    public sealed class FramePlayer : ModPlayer
    {
        /// <summary>该玩家手持物品的形态（用于绘制远程玩家的手持物）。</summary>
        public byte HeldItemMode { get; internal set; }
        /// <summary>
        /// 最近一次同步出去的手持实例。用于“玩家换到另一把同款物品”时补发形态——
        /// 只在切换形态时发包的话，其它客户端会把上一个实例的形态一直沿用下去。
        /// </summary>
        internal Item? LastSyncedHeldItem { get; set; }
        public override void OnEnterWorld()
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                return;
            }
            //把手持物品的形态推给服务端（供其它客户端绘制），并拉取其它玩家的形态
            FrameNet.SendHeldItemMode(FrameItem.GetItemMode(Player.HeldItem));
            FrameNet.RequestItemModes();
        }
        /// <summary>
        /// 手持实例或其形态发生变化时补发一次同步（不是每 tick 发包）：
        /// 玩家在“形态不同的两件同款物品”之间切换时，其它客户端也要跟着换贴图。
        /// </summary>
        internal void SyncHeldMode(Item heldItem, byte mode)
        {
            if (ReferenceEquals(LastSyncedHeldItem, heldItem) && HeldItemMode == mode)
            {
                return;
            }
            LastSyncedHeldItem = heldItem;
            HeldItemMode = mode;
            FrameNet.SendHeldItemMode(mode);
        }
    }
}
