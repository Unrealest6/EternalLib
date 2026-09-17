namespace EternalLib
{
    public enum FrameMode
    {
        Loop,
        PingPong,
        Once
    }
    /// <summary>
    /// 帧图布局类型
    /// </summary>
    public enum FrameLayout
    {
        /// <summary>整张纹理即一帧，不做切割（静态图）</summary>
        Single,
        /// <summary>垂直单方向（默认）：TotalFrames 帧沿高度排列</summary>
        Vertical,
        /// <summary>二维网格：Rows × Columns</summary>
        Grid
    }
    /// <summary>
    /// 纯 Texture2D 动画帧管理类。
    /// 支持整图、垂直单方向或二维网格布局。
    /// tick → 帧索引 的映射在构造时预计算，切割出的帧纹理按需生成并缓存。
    /// </summary>
    public sealed class FrameTexture
    {
        /// <summary>注册名（通常为物品 FullName 或自定义键）</summary>
        public string Name { get; }
        /// <summary>原始图集路径</summary>
        public string TexturePath { get; }
        /// <summary>帧图布局</summary>
        public FrameLayout Layout { get; }
        /// <summary>垂直布局参数：总帧数</summary>
        public int TotalFrames { get; }
        /// <summary>网格布局参数：行数</summary>
        public int Rows { get; }
        /// <summary>网格布局参数：列数</summary>
        public int Columns { get; }
        /// <summary>总帧数（统一接口）</summary>
        public int FrameCount => Layout == FrameLayout.Grid ? Rows * Columns : TotalFrames;
        /// <summary>播放模式</summary>
        public FrameMode Mode { get; }
        /// <summary>一个完整播放周期的 tick 数（0 表示没有可播放的帧）</summary>
        public int TotalDuration => _tickToFrameIndex.Length;
        /// <summary>当前 tick（每帧 +1，由 <see cref="FrameTextureSystem"/> 驱动）</summary>
        public int CurrentTick { get; set; }
        /// <summary>是否由系统自动推进 tick</summary>
        public bool AutoUpdate { get; set; } = true;
        /// <summary>加载后是否立即切割所有帧（会增加启动耗时与显存占用，换取零绘制期卡顿）</summary>
        public bool Precache { get; set; }
        /// <summary>加载是否失败（失败后不再重复抛出，绘制时退化为 1x1 白点）</summary>
        public bool IsBroken { get; private set; }

        private readonly bool _isSingleImage;
        private readonly int[] _tickToFrameIndex;
        private Texture2D? _spriteSheet;
        private GraphicsDevice? _graphicsDevice;
        private Texture2D?[]? _frameCache;
        private int _frameWidth;
        private int _frameHeight;
        private bool _isLoaded;

        /// <summary>标记为加载失败，之后不再重复尝试加载与抛出异常。</summary>
        internal void MarkBroken() => IsBroken = true;
        /// <summary>整张纹理作为单帧使用。</summary>
        public FrameTexture(string name, string texturePath)
            : this(name, texturePath, FrameLayout.Single, 1, 1, 1, null, 1, FrameMode.Loop)
        {
        }
        /// <summary>垂直单方向布局：TotalFrames 帧沿高度排列。</summary>
        public FrameTexture(string name, string texturePath, int totalFrames,
            FrameDef[]? frameTimeline = null, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            : this(name, texturePath, FrameLayout.Vertical, totalFrames, 1, 1, frameTimeline, frameDuration, mode)
        {
        }
        /// <summary>二维网格布局：Rows × Columns。</summary>
        public FrameTexture(string name, string texturePath, int rows, int columns,
            FrameDef[]? frameTimeline = null, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            : this(name, texturePath, FrameLayout.Grid, rows * columns, rows, columns, frameTimeline, frameDuration, mode)
        {
        }
        private FrameTexture(string name, string texturePath, FrameLayout layout, int totalFrames,
            int rows, int columns, FrameDef[]? frameTimeline, int frameDuration, FrameMode mode)
        {
            ArgumentException.ThrowIfNullOrEmpty(texturePath);
            Name = name;
            TexturePath = texturePath;
            Layout = layout;
            _isSingleImage = layout == FrameLayout.Single;
            Mode = mode;
            switch (layout)
            {
                case FrameLayout.Single:
                    // 整图布局：恒为 1 帧，避免 FrameCount 为 0 导致各处需要额外判空。
                    TotalFrames = 1;
                    Rows = 1;
                    Columns = 1;
                    _tickToFrameIndex = [0];
                    return;
                case FrameLayout.Grid:
                    ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
                    ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
                    break;
                case FrameLayout.Vertical:
                default:
                    ArgumentOutOfRangeException.ThrowIfLessThan(totalFrames, 1);
                    break;
            }
            TotalFrames = totalFrames;
            Rows = rows;
            Columns = columns;
            int frameCount = FrameCount;
            frameDuration = Math.Max(1, frameDuration);
            // 预计算 tick → 帧索引 映射，避免播放期做除法/取模。
            if (frameTimeline is null || frameTimeline.Length == 0)
            {
                _tickToFrameIndex = new int[frameCount * frameDuration];
                int pos = 0;
                for (int f = 0; f < frameCount; f++)
                {
                    for (int d = 0; d < frameDuration; d++)
                    {
                        _tickToFrameIndex[pos++] = f;
                    }
                }
                return;
            }
            int[] durations = new int[frameTimeline.Length];
            int total = 0;
            for (int i = 0; i < frameTimeline.Length; i++)
            {
                FrameDef def = frameTimeline[i];
                if ((uint)def.FrameIndex >= (uint)frameCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(frameTimeline),
                        $"'{name}' 的帧时间轴包含越界帧索引 {def.FrameIndex}（合法范围 0..{frameCount - 1}）。");
                }
                int duration = def.ResolveDuration(frameDuration);
                durations[i] = duration;
                total += duration;
            }
            _tickToFrameIndex = new int[total];
            int index = 0;
            for (int i = 0; i < frameTimeline.Length; i++)
            {
                int frameIndex = frameTimeline[i].FrameIndex;
                int duration = durations[i];
                for (int j = 0; j < duration; j++)
                {
                    _tickToFrameIndex[index++] = frameIndex;
                }
            }
        }
        /// <summary>原始图集（未切割）。首次访问时会尝试加载。</summary>
        public Texture2D? SourceTexture
        {
            get
            {
                EnsureLoaded();
                return _spriteSheet;
            }
        }
        internal void Load()
        {
            if (_isLoaded || Main.dedServ)
            {
                return;
            }
            Texture2D sheet = ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad).Value
                              ?? throw new InvalidOperationException($"贴图 '{TexturePath}' 加载失败。");
            if (_isSingleImage)
            {
                _spriteSheet = sheet;
                _frameWidth = sheet.Width;
                _frameHeight = sheet.Height;
                _isLoaded = true;
                return;
            }
            GraphicsDevice? device = Main.graphics?.GraphicsDevice;
            if (device is null || device.IsDisposed)
            {
                // 图形设备尚未就绪（或无客户端）：保持未加载状态，稍后重试。
                return;
            }
            if (Layout == FrameLayout.Vertical)
            {
                if (sheet.Height % TotalFrames != 0)
                {
                    throw new ArgumentException(
                        $"'{Name}' 的图集高度 {sheet.Height} 无法被帧数 {TotalFrames} 整除。");
                }
                _frameWidth = sheet.Width;
                _frameHeight = sheet.Height / TotalFrames;
            }
            else
            {
                if (sheet.Height % Rows != 0 || sheet.Width % Columns != 0)
                {
                    throw new ArgumentException(
                        $"'{Name}' 的图集尺寸 {sheet.Width}x{sheet.Height} 无法被网格 {Rows}x{Columns} 整除。");
                }
                _frameWidth = sheet.Width / Columns;
                _frameHeight = sheet.Height / Rows;
            }
            _spriteSheet = sheet;
            _graphicsDevice = device;
            _frameCache = new Texture2D[FrameCount];
            _isLoaded = true;
            if (Precache)
            {
                PrecacheFrames();
            }
        }
        private void EnsureLoaded()
        {
            if (_isLoaded || IsBroken || Main.dedServ)
            {
                return;
            }
            try
            {
                Load();
            }
            catch (Exception ex)
            {
                // 加载失败只在首次记录一次，避免在绘制循环里反复抛异常刷屏。
                IsBroken = true;
                EternalLog.Error($"加载序列帧 '{Name}'（{TexturePath}）失败：{ex.Message}");
            }
        }
        /// <summary>
        /// 预先切割并缓存所有帧。适合在加载阶段调用，以避免首次绘制时的显存分配卡顿。
        /// </summary>
        public void PrecacheFrames()
        {
            EnsureLoaded();
            if (!_isLoaded || _isSingleImage)
            {
                return;
            }
            for (int i = 0; i < FrameCount; i++)
            {
                GetFrame(i);
            }
        }
        /// <summary>
        /// 获取指定帧的独立纹理。首次调用时从图集切割并缓存，后续直接返回。
        /// </summary>
        public Texture2D GetFrame(int frameIndex)
        {
            if (_isSingleImage)
            {
                EnsureLoaded();
                return _spriteSheet ?? TextureAssets.MagicPixel.Value;
            }
            EnsureLoaded();
            if (!_isLoaded || _spriteSheet is null || _graphicsDevice is null)
            {
                return TextureAssets.MagicPixel.Value;
            }
            frameIndex = Math.Clamp(frameIndex, 0, FrameCount - 1);
            _frameCache ??= new Texture2D[FrameCount];
            Texture2D? cached = _frameCache[frameIndex];
            if (cached is not null && !cached.IsDisposed)
            {
                return cached;
            }
            Rectangle sourceRect = GetFrameSourceRect(frameIndex);
            int pixelCount = sourceRect.Width * sourceRect.Height;
            Color[] buffer = ArrayPool<Color>.Shared.Rent(pixelCount);
            try
            {
                _spriteSheet.GetData(0, sourceRect, buffer, 0, pixelCount);
                Texture2D texture = new(_graphicsDevice, sourceRect.Width, sourceRect.Height);
                texture.SetData(buffer, 0, pixelCount);
                _frameCache[frameIndex] = texture;
                return texture;
            }
            finally
            {
                ArrayPool<Color>.Shared.Return(buffer);
            }
        }
        /// <summary>获取当前帧的独立纹理。</summary>
        public Texture2D GetCurrentFrame() => GetFrame(CurrentFrameIndex);
        /// <summary>获取指定帧在原始图集中的源矩形（无需切割独立纹理时使用）。</summary>
        public Rectangle GetFrameSourceRect(int frameIndex)
        {
            EnsureLoaded();
            frameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, FrameCount - 1));
            if (_isSingleImage || Layout == FrameLayout.Vertical)
            {
                return new Rectangle(0, frameIndex * _frameHeight, _frameWidth, _frameHeight);
            }
            int row = frameIndex / Columns;
            int col = frameIndex % Columns;
            return new Rectangle(col * _frameWidth, row * _frameHeight, _frameWidth, _frameHeight);
        }
        /// <summary>当前帧在原始图集中的源矩形。</summary>
        public Rectangle GetCurrentSourceRect() => GetFrameSourceRect(CurrentFrameIndex);
        /// <summary>当前帧尺寸（切割后单帧纹理的尺寸）。</summary>
        public Point GetFrameSize()
        {
            EnsureLoaded();
            return new Point(_frameWidth, _frameHeight);
        }
        /// <summary>帧纹理的完整源矩形（配合 <see cref="GetCurrentFrame"/> 使用）。</summary>
        public Rectangle GetFrameRect() => new(0, 0, GetFrameSize().X, GetFrameSize().Y);
        /// <summary>释放已切割的帧纹理，保留图集引用；下次访问时按需重新切割。</summary>
        public void ReleaseFrames()
        {
            if (_frameCache is null)
            {
                return;
            }
            Main.QueueMainThreadAction(() =>
            {
                for (int i = 0; i < _frameCache.Length; i++)
                {
                    Texture2D? frame = _frameCache[i];
                    if (frame is not null && !frame.IsDisposed)
                    {
                        frame.Dispose();
                    }
                    _frameCache[i] = null;
                }
                _frameCache = null;
            });
        }
        internal void Unload()
        {
            // ModSystem.Unload 执行在主线程上，直接释放即可；
            // 不要使用 Main.QueueMainThreadAction，卸载后该回调可能不再执行而泄漏显存。
            ReleaseFrames();
            _spriteSheet = null;
            _graphicsDevice = null;
            _isLoaded = false;
            IsBroken = false;
            CurrentTick = 0;
        }
        /// <summary>当前帧索引。</summary>
        public int CurrentFrameIndex
        {
            get
            {
                int duration = _tickToFrameIndex.Length;
                if (duration == 0)
                {
                    return 0;
                }
                int tick = Mode switch
                {
                    FrameMode.Loop => Wrap(CurrentTick, duration),
                    FrameMode.PingPong => CalculatePingPongTick(duration),
                    FrameMode.Once => Math.Clamp(CurrentTick, 0, duration - 1),
                    _ => 0
                };
                return _tickToFrameIndex[tick];
            }
        }
        /// <summary>Once 模式下是否已播放完毕。</summary>
        public bool IsFinished => Mode == FrameMode.Once && CurrentTick >= TotalDuration - 1;
        /// <summary>设置为非负余数，避免负数取模。</summary>
        private static int Wrap(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
        private int CalculatePingPongTick(int duration)
        {
            int cycle = (duration - 1) * 2;
            if (cycle <= 0)
            {
                return 0;
            }
            int tick = Wrap(CurrentTick, cycle);
            return tick < duration ? tick : cycle - tick;
        }
        internal void Update()
        {
            if (!AutoUpdate || _isSingleImage || _tickToFrameIndex.Length <= 1)
            {
                return;
            }
            // 回绕到下界而不是让 int 溢出，保证 PingPong 的取模运算连续。
            CurrentTick = CurrentTick >= int.MaxValue - 1 ? 0 : CurrentTick + 1;
        }
        /// <summary>跳转到指定帧的起始 tick。</summary>
        public void SetFrame(int frameIndex)
        {
            frameIndex = Math.Clamp(frameIndex, 0, FrameCount - 1);
            for (int tick = 0; tick < _tickToFrameIndex.Length; tick++)
            {
                if (_tickToFrameIndex[tick] != frameIndex)
                {
                    continue;
                }
                CurrentTick = tick;
                return;
            }
        }
        /// <summary>重置到第 0 帧。</summary>
        public void Reset() => CurrentTick = 0;
        /// <summary>从头开始播放。</summary>
        public void Restart()
        {
            CurrentTick = 0;
            AutoUpdate = true;
        }
        /// <summary>暂停播放（保持当前帧）。</summary>
        public void Pause() => AutoUpdate = false;
        /// <summary>继续播放。</summary>
        public void Play() => AutoUpdate = true;
    }
    /// <summary>
    /// 帧时间轴上的一个条目：在第 <paramref name="FrameIndex"/> 帧停留 <paramref name="DurationTicks"/> 个 tick。
    /// <paramref name="DurationTicks"/> 小于等于 0 时使用默认帧时长。
    /// </summary>
    public readonly record struct FrameDef(int FrameIndex, int DurationTicks)
    {
        public static implicit operator FrameDef(int frameIndex) => new(frameIndex, -1);
        public int ResolveDuration(int defaultDuration) => DurationTicks > 0 ? DurationTicks : defaultDuration;
    }
}
