namespace EternalLib
{
    /// <summary>
    /// 序列帧注册与驱动中心：所有 <see cref="FrameTexture"/> 在此注册、加载、逐 tick 推进与卸载。
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
        /// <param name="name">注册名，需全局唯一</param>
        /// <param name="texturePath">纹理路径</param>
        /// <param name="totalFrames">帧数（图集高度必须能被其整除）</param>
        /// <param name="timeline">帧时间轴，为 null 时按 <paramref name="frameDuration"/> 顺序播放</param>
        /// <param name="frameDuration">每帧默认停留的 tick 数</param>
        /// <param name="mode">播放模式</param>
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
                    EternalLog.Warn($"Frame animation name '{name}' is already taken by '{existing.TexturePath}'; "
                        + $"the new registration for '{texturePath}' was ignored. Use unique names to avoid cross-mod conflicts.");
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
                    EternalLog.Error($"Failed to load frame animation '{anim.Name}' ({anim.TexturePath}): {ex.Message}");
                }
            }
        }
        public override void PostSetupContent()
        {
            if (Main.dedServ)
            {
                return;
            }
            // PostSetupContent 在主线程执行，直接加载即可；改用 Main.QueueMainThreadAction 会把加载推迟到未知时机并造成首帧卡顿。
            LoadAll();
            EternalLog.Info($"Loaded {Animations.Count} frame animations.");
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