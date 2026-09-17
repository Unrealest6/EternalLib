namespace EternalLib
{
    /// <summary>
    /// 序列帧注册与驱动中心。
    /// 所有 <see cref="FrameTexture"/> 都在这里注册、加载、逐 tick 推进与卸载。
    /// </summary>
    public sealed class FrameTextureSystem : ModSystem
    {
        internal static Dictionary<string, FrameTexture> Animations { get; } = [];
        /// <summary>已注册的全部序列帧。</summary>
        public static IReadOnlyCollection<FrameTexture> All => Animations.Values;
        /// <summary>已注册的序列帧数量。</summary>
        public static int Count => Animations.Count;
        /// <summary>
        /// 注册整图序列帧（不切割，等价于单帧）。
        /// </summary>
        public static FrameTexture Register(string name, string texturePath)
            => GetOrAdd(name, texturePath, () => new FrameTexture(name, texturePath));
        /// <summary>
        /// 注册垂直单方向序列帧。
        /// </summary>
        /// <param name="texturePath">纹理路径</param>
        /// <param name="totalFrames">帧数（图集高度必须能被其整除）</param>
        /// <param name="timeline">帧时间轴，为 null 时按 <paramref name="frameDuration"/> 顺序播放</param>
        /// <param name="name">名称</param>
        /// <param name="frameDuration">默认帧数</param>
        /// <param name="mode">模式</param>
        public static FrameTexture Register(string name, string texturePath, int totalFrames,
            FrameDef[]? timeline, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            => GetOrAdd(name, texturePath, () => new FrameTexture(name, texturePath, totalFrames, timeline, frameDuration, mode));
        /// <summary>
        /// 注册垂直单方向序列帧（每帧停留 <paramref name="frameDuration"/> 个 tick）。
        /// </summary>
        public static FrameTexture Register(string name, string texturePath, int totalFrames,
            int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            => GetOrAdd(name, texturePath, () => new FrameTexture(name, texturePath, totalFrames, null, frameDuration, mode));
        /// <summary>
        /// 注册二维网格序列帧。
        /// </summary>
        public static FrameTexture RegisterGrid(string name, string texturePath, int rows, int columns,
            FrameDef[]? timeline = null, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            => GetOrAdd(name, texturePath, () => new FrameTexture(name, texturePath, rows, columns, timeline, frameDuration, mode));
        /// <summary>
        /// 查询已注册的序列帧。
        /// </summary>
        public static bool TryGet(string name, out FrameTexture texture)
        {
            if (Animations.TryGetValue(name, out FrameTexture? found))
            {
                texture = found;
                return true;
            }
            texture = null!;
            return false;
        }
        private static FrameTexture GetOrAdd(string name, string texturePath, Func<FrameTexture> factory)
        {
            if (Animations.TryGetValue(name, out FrameTexture? existing))
            {
                if (existing.TexturePath != texturePath)
                {
                    EternalLog.Warn($"序列帧注册名 '{name}' 已被 '{existing.TexturePath}' 占用，"
                        + $"本次注册的 '{texturePath}' 被忽略。请使用独一无二的注册名以避免跨模组冲突。");
                }
                return existing;
            }
            FrameTexture created = factory();
            Animations[name] = created;
            return created;
        }
        private static void LoadAll()
        {
            foreach (FrameTexture anim in Animations.Values)
            {
                try
                {
                    anim.Load();
                    if (anim.Precache)
                    {
                        anim.PrecacheFrames();
                    }
                }
                catch (Exception ex)
                {
                    anim.MarkBroken();
                    EternalLog.Error($"加载序列帧 '{anim.Name}'（{anim.TexturePath}）失败：{ex.Message}");
                }
            }
        }
        public override void PostSetupContent()
        {
            if (Main.dedServ)
            {
                return;
            }
            // PostSetupContent 本身就在主线程上执行，直接加载即可，
            // 用 Main.QueueMainThreadAction 会把加载推迟到未知时机并造成首帧卡顿。
            LoadAll();
            EternalLog.Info($"已加载 {Animations.Count} 个序列帧。");
        }
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
            {
                return;
            }
            foreach (FrameTexture anim in Animations.Values)
            {
                anim.Update();
            }
        }
        public override void Unload()
        {
            foreach (FrameTexture anim in Animations.Values)
            {
                anim.Unload();
            }
            Animations.Clear();
        }
    }
}