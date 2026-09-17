namespace EternalLib
{
    /// <summary>
    /// 序列帧物品基类。
    /// <para>主贴图（<see cref="ModItem.Texture"/>）按 <see cref="FrameCount"/> 垂直分割，
    /// 通过 <see cref="FrameTextureSystem"/> 播放；<see cref="FrameTextures"/> 可提供其它形态的贴图。</para>
    /// </summary>
    public abstract class FrameItem : ModItem
    {
        /// <summary>主贴图对应的序列帧（以 <see cref="ModItem.FullName"/> 注册）。</summary>
        public FrameTexture? FrameTexture => ModContent.GetFrameTexture(FullName);
        /// <summary>当前形态。形态 0 使用主贴图，其它值查 <see cref="FrameTextures"/>。</summary>
        public byte Mode { get; protected set; }
        /// <summary>主贴图的帧数。</summary>
        protected abstract int FrameCount { get; }
        /// <summary>主贴图的帧时间轴。</summary>
        protected abstract FrameDef[] FrameTimeline { get; }
        /// <summary>
        /// 形态 → 贴图 的映射。形态 0 恒为主贴图，无需在此列出。
        /// <para>注意：该属性可能在绘制时被反复访问，派生类应返回稳定实例（基类会缓存首次结果）。</para>
        /// </summary>
        public virtual Dictionary<byte, FrameTexture?> FrameTextures => [];
        /// <summary>主贴图的默认帧时长（tick）。</summary>
        protected virtual int FrameDuration => 1;
        /// <summary>是否需要注册原版的竖直帧动画（单帧物品无需注册）。</summary>
        protected virtual bool Animated => FrameCount > 1;
        /// <summary>
        /// 形态映射的缓存。
        /// <para>用静态表按实例缓存：ModItem 是每个类型一个实例，
        /// 在类里放可变的引用字段会被 tModLoader 判定为“克隆时不安全”并在加载期打警告。</para>
        /// </summary>
        private static readonly Dictionary<FrameItem, Dictionary<byte, FrameTexture?>> ModeTextureCache = [];
        protected Dictionary<byte, FrameTexture?> ModeTextures
        {
            get
            {
                if (ModeTextureCache.TryGetValue(this, out Dictionary<byte, FrameTexture?>? cached))
                {
                    return cached;
                }
                cached = FrameTextures;
                ModeTextureCache[this] = cached;
                return cached;
            }
        }
        /// <summary>清理缓存（模组卸载时调用，避免残留旧实例）。</summary>
        internal static void ClearTextureCache() => ModeTextureCache.Clear();
        public override void SetStaticDefaults()
        {
            FrameTextureSystem.Register(FullName, Texture, FrameCount, FrameTimeline, FrameDuration);
            if (Animated)
            {
                // 原版动画帧的尺寸会被用来计算持握物品的原点，必须与实际单帧尺寸一致。
                Main.RegisterItemAnimation(Type, new DrawAnimationVertical(FrameDuration, FrameCount));
            }
        }
        /// <summary>
        /// 取某个物品实例应当使用的形态。默认返回全局 <see cref="Mode"/>。
        /// <para><paramref name="drawPlayer"/> 是这个实例正在被绘制给谁看（背包/世界绘制传 <c>Main.LocalPlayer</c>），
        /// <paramref name="item"/> 是<b>正在被绘制的那个物品实例</b>（可能为 null）。
        /// 需要“形态跟随物品实例”的物品应重写本方法读取 <paramref name="item"/> 上的状态。</para>
        /// </summary>
        protected virtual byte GetMode(Player drawPlayer, Item? item) => Mode;
        /// <summary>取指定形态对应的序列帧；形态 0 或映射缺失时回落到主贴图。</summary>
        protected FrameTexture? GetFrameTextureForMode(byte mode)
            => mode != 0 && ModeTextures.TryGetValue(mode, out FrameTexture? texture) && texture is not null
                ? texture
                : FrameTexture;
        /// <summary>取当前（本地）形态对应的序列帧。</summary>
        protected FrameTexture? GetCurrentFrameTexture() => GetFrameTextureForMode(Mode);
        /// <summary>切换形态并返回切换后的形态。</summary>
        protected byte ToggleMode(byte count = 2) => Mode = (byte)((Mode + 1) % Math.Max(1, (int)count));
        public override bool ModifyItemDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData,
            ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData)
        {
            // 手持绘制发生在每个客户端上，形态必须按“正在被绘制的那名玩家的手持实例”取值，
            // 否则多人游戏下其它玩家只能看到默认形态。
            FrameTexture? texture = GetFrameTextureForMode(GetMode(drawInfo.drawPlayer, drawInfo.drawPlayer.HeldItem));
            if (texture is null)
            {
                return true;
            }
            Texture2D frame = texture.GetCurrentFrame();
            // 原版用 GetItemDrawFrame 得到的帧尺寸计算 origin；
            // 当形态贴图的单帧尺寸与主贴图不同时，需要把这一半尺寸差补回来，
            // 同时保留 HoldoutOrigin 等钩子附加的偏移。
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
            //背包里画的是“某个格子里的实例”，由 FrameItemDrawContext 提供该实例，
            //这样每个实例都能画出自己形态的贴图。
            FrameTexture? texture = GetFrameTextureForMode(GetMode(Main.LocalPlayer, FrameItemDrawContext.CurrentInventoryItem));
            if (texture is null)
            {
                // 没有注册序列帧时交回原版绘制，返回 false 会让物品在背包里彻底不可见。
                return true;
            }
            Texture2D currentFrame = texture.GetCurrentFrame();
            spriteBatch.Draw(currentFrame, position, null, drawColor, 0f, currentFrame.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            //世界中掉落的是独立实例，直接用 whoAmI 取回它（ModItem.Item 不保证指向正在绘制的实例）。
            Item worldItem = whoAmI >= 0 && whoAmI < Main.item.Length ? Main.item[whoAmI] : Item;
            FrameTexture? texture = GetFrameTextureForMode(GetMode(Main.LocalPlayer, worldItem));
            if (texture is null)
            {
                return true;
            }
            Texture2D currentFrame = texture.GetCurrentFrame();
            Vector2 origin = currentFrame.Size() / 2f;
            // 与原版一致：世界中掉落的物品水平居中、底边贴合碰撞箱底部。
            Vector2 position = worldItem.Bottom - Main.screenPosition - new Vector2(0f, origin.Y);
            spriteBatch.Draw(currentFrame, position, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}