namespace EternalLib
{
    /// <summary>
    /// 序列帧物品基类：主贴图按 <see cref="FrameCount"/> 垂直分割播放，<see cref="FrameTextures"/> 提供其它形态。
    /// <para><see cref="Mode"/> 是物品实例上的状态（<see cref="CloneNewInstances"/> + <see cref="Clone"/>
    /// 保证掉落、容器取放后不丢失），存档与联机同步也由本类一并处理。</para>
    /// </summary>
    public abstract class FrameItem : ModItem
    {
        /// <summary>主贴图对应的序列帧（以 <see cref="ModItem.FullName"/> 注册）。</summary>
        public FrameTexture? FrameTexture => ModContent.GetFrameTexture(FullName);

        /// <summary>当前形态。形态 0 使用主贴图，其它值查 <see cref="FrameTextures"/>。</summary>
        public byte Mode { get; protected set; }

        /// <summary>形态数量；只有 1 种形态的物品无需处理切换。</summary>
        public virtual byte ModeCount => 1;

        /// <summary>切换形态的按键组合（默认：Shift + 右键点按）。</summary>
        protected virtual bool ToggleModePressed
            => Main.keyState.IsKeyDown(Keys.LeftShift) && Main.mouseRight && Main.mouseRightRelease;

        /// <summary>存档里形态使用的键名。</summary>
        private const string ModeTagKey = "FrameItemMode";

        /// <summary>早期版本使用的形态存档键，读取时一并兼容；使用方按需覆写。</summary>
        protected virtual string[] LegacyModeTagKeys => [];

        /// <summary>主贴图的帧数。</summary>
        protected abstract int FrameCount { get; }

        /// <summary>主贴图的帧时间轴。</summary>
        protected abstract FrameDef[] FrameTimeline { get; }

        /// <summary>形态 → 贴图的映射。形态 0 恒为主贴图，无需列出。</summary>
        public virtual Dictionary<byte, FrameTexture?> FrameTextures => [];

        /// <summary>主贴图的默认帧时长（tick）。</summary>
        protected virtual int FrameDuration => 1;

        /// <summary>是否需要注册原版的竖直帧动画（单帧物品无需注册）。</summary>
        protected virtual bool Animated => FrameCount > 1;

        /// <summary>形态映射的按实例弱缓存；避免 <see cref="FrameTextures"/> 的默认实现每次访问都新建字典。</summary>
        private static ConditionalWeakTable<FrameItem, Dictionary<byte, FrameTexture?>> ModeTextureCache { get; } = [];

        /// <summary>形态映射的缓存结果。</summary>
        protected Dictionary<byte, FrameTexture?> ModeTextures
        {
            get
            {
                if (ModeTextureCache.TryGetValue(this, out Dictionary<byte, FrameTexture?>? cached))
                {
                    return cached;
                }
                cached = FrameTextures;
                ModeTextureCache.Add(this, cached);
                return cached;
            }
        }

        /// <summary>清理形态映射缓存（模组卸载时调用）。</summary>
        internal static void ClearTextureCache() => ModeTextureCache.Clear();

        /// <summary>让每个物品实例持有独立的 ModItem，以便各自维护形态。</summary>
        protected override bool CloneNewInstances => true;

        /// <summary>克隆物品时一并复制形态。</summary>
        public override ModItem Clone(Item newEntity)
        {
            FrameItem clone = (FrameItem)base.Clone(newEntity);
            clone.Mode = Mode;
            return clone;
        }

        public override void SetStaticDefaults()
        {
            FrameTextureSystem.Register(FullName, Texture, FrameCount, FrameTimeline, FrameDuration);
            if (Animated)
            {
                // 原版用单帧尺寸计算持握原点，必须与实际单帧尺寸一致。
                Main.RegisterItemAnimation(Type, new DrawAnimationVertical(FrameDuration, FrameCount));
            }
        }

        /// <summary>
        /// 取某个物品实例应当使用的形态。
        /// <para>本地玩家/单人直接读实例；远程玩家退回按玩家同步的 <see cref="FramePlayer.HeldItemMode"/>。</para>
        /// </summary>
        protected virtual byte GetMode(Player drawPlayer, Item? item) => GetModeFor(drawPlayer, item);

        /// <summary>
        /// 取“这名玩家手上这件物品”的形态（渲染与库内判定共用同一套口径）：
        /// 本地玩家 / 单人读物品实例，远程玩家读按玩家同步的 <see cref="FramePlayer.HeldItemMode"/>。
        /// </summary>
        public static byte GetModeFor(Player player, Item? item)
        {
            byte instanceMode = GetItemMode(item);
            if (player.whoAmI == Main.myPlayer || Main.netMode == NetmodeID.SinglePlayer || instanceMode != 0)
            {
                return instanceMode;
            }
            return player.TryGetModPlayer(out FramePlayer framePlayer) ? framePlayer.HeldItemMode : (byte)0;
        }

        /// <summary>取某个物品实例的形态（不是序列帧物品时返回 0）。</summary>
        public static byte GetItemMode(Item? item) => item?.ModItem is FrameItem frameItem ? frameItem.Mode : (byte)0;

        /// <summary>写入某个物品实例的形态（越界值会被夹回合法范围）。</summary>
        public static void SetItemMode(Item? item, byte mode)
        {
            if (item?.ModItem is FrameItem frameItem)
            {
                frameItem.Mode = frameItem.ClampMode(mode);
            }
        }

        /// <summary>把形态夹到 <c>[0, ModeCount-1]</c>。</summary>
        public byte ClampMode(byte mode) => ModeCount <= 1 ? (byte)0 : (byte)Math.Min(mode, ModeCount - 1);

        /// <summary>取本地玩家手持实例的形态。</summary>
        protected static byte GetHeldMode(Player player) => GetItemMode(player.HeldItem);

        /// <summary>把形态对应的属性写到实际手持的物品实例上（每 tick 调用一次）。</summary>
        protected virtual void ApplyModeStats(Item heldItem, byte mode) { }

        public override void SaveData(TagCompound tag)
        {
            if (Mode != 0)
            {
                tag[ModeTagKey] = Mode;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey(ModeTagKey))
            {
                Mode = ClampMode(tag.GetByte(ModeTagKey));
                return;
            }
            foreach (string legacyKey in LegacyModeTagKeys)
            {
                if (!tag.ContainsKey(legacyKey))
                {
                    continue;
                }
                Mode = ClampMode(tag.GetByte(legacyKey));
                return;
            }
        }

        public override void NetSend(BinaryWriter writer) => writer.Write(Mode);

        public override void NetReceive(BinaryReader reader) => Mode = ClampMode(reader.ReadByte());

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
            {
                return;
            }
            Item heldItem = player.HeldItem;
            if (heldItem is null || heldItem.IsAir || heldItem.type != Type || heldItem.ModItem is not FrameItem held)
            {
                return;
            }
            byte mode = held.Mode;
            if (ModeCount > 1 && ToggleModePressed)
            {
                mode = (byte)((mode + 1) % ModeCount);
                held.Mode = mode;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            if (player.TryGetModPlayer(out FramePlayer framePlayer))
            {
                framePlayer.SyncHeldMode(heldItem, mode);
            }
            ApplyModeStats(heldItem, mode);
        }

        /// <summary>取指定形态对应的序列帧；形态 0 或映射缺失时回落到主贴图。</summary>
        protected FrameTexture? GetFrameTextureForMode(byte mode)
            => mode != 0 && ModeTextures.TryGetValue(mode, out FrameTexture? texture) && texture is not null
                ? texture
                : FrameTexture;

        /// <summary>取当前形态对应的序列帧。</summary>
        protected FrameTexture? GetCurrentFrameTexture() => GetFrameTextureForMode(Mode);

        /// <summary>切换形态并返回切换后的形态。</summary>
        protected byte ToggleMode(byte count = 2) => Mode = (byte)((Mode + 1) % Math.Max(1, (int)count));

        public override bool ModifyItemDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData,
            ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData)
        {
            // 按“正在被绘制的那名玩家的手持实例”取形态，否则多人下其它玩家只能看到默认形态。
            FrameTexture? texture = GetFrameTextureForMode(GetMode(drawInfo.drawPlayer, drawInfo.drawPlayer.HeldItem));
            if (texture is null)
            {
                return true;
            }
            Texture2D frame = texture.GetCurrentFrame();
            // 形态贴图单帧尺寸与主贴图不同时，把差值补回 origin。
            Rectangle vanillaFrame = drawInfo.drawPlayer.GetItemDrawFrame(Type);
            Vector2 vanillaOrigin = new(vanillaFrame.Width / 2f, vanillaFrame.Height / 2f);
            Vector2 extraOffset = drawData.origin - vanillaOrigin;
            drawData.texture = frame;
            drawData.sourceRect = texture.GetFrameRect();
            drawData.origin = texture.GetFrameSize().ToVector2() / 2f + extraOffset;
            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // <see cref="ModItem.Item"/> 指向当前正在绘制的物品实例，直接按它的形态绘制。
            FrameTexture? texture = GetFrameTextureForMode(GetMode(Main.LocalPlayer, Item));
            if (texture is null)
            {
                // 没有注册序列帧时交回原版绘制；返回 false 会让物品在背包里彻底不可见。
                return true;
            }
            Texture2D currentFrame = texture.GetCurrentFrame();
            spriteBatch.Draw(currentFrame, position, null, drawColor, 0f, currentFrame.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // 世界中掉落的是独立实例，用 whoAmI 取回它。
            Item worldItem = whoAmI >= 0 && whoAmI < Main.item.Length ? Main.item[whoAmI] : Item;
            FrameTexture? texture = GetFrameTextureForMode(GetMode(Main.LocalPlayer, worldItem));
            if (texture is null)
            {
                return true;
            }
            Texture2D currentFrame = texture.GetCurrentFrame();
            Vector2 origin = currentFrame.Size() / 2f;
            // 与原版一致：水平居中、底边贴合碰撞箱底部。
            Vector2 position = worldItem.Bottom - Main.screenPosition - new Vector2(0f, origin.Y);
            spriteBatch.Draw(currentFrame, position, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}