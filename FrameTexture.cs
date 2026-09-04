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
        Vertical,   // 垂直单方向（默认）：TotalFrames 帧沿高度排列
        Grid        // 二维网格：Rows × Columns
    }
    /// <summary>
    /// 纯 Texture2D 动画帧管理类。
    /// 支持垂直单方向或二维网格布局。
    /// </summary>
    public sealed class FrameTexture
    {
        public string? Name { get; }
        public FrameLayout Layout { get; }
        // 垂直布局参数
        public int TotalFrames { get; }
        // 网格布局参数
        public int Rows { get; }
        public int Columns { get; }
        /// <summary>总帧数（统一接口）</summary>
        public int FrameCount => Layout == FrameLayout.Grid ? Rows * Columns : TotalFrames;
        public FrameMode Mode { get; }
        private readonly string? _texturePath;
        private Texture2D? _spriteSheet;
        private GraphicsDevice? _graphicsDevice;
        private Texture2D?[]? _frameCache; // 帧索引 -> 独立纹理（固定长度数组以减少字典开销）
        private int _frameWidth, _frameHeight;
        private bool _isSingleImage;
        private bool _isLoaded;
        // tick->帧索引 映射
        private readonly int[]? _tickToFrameIndex;
        public int? TotalDuration => _tickToFrameIndex?.Length;
        public int CurrentTick { get; set; }
        public bool AutoUpdate { get; set; } = true;
        public FrameTexture(string name, string texturePath)
        {
            _isSingleImage = true;
            Name = name;
            _texturePath = texturePath;
        }
        public FrameTexture(string name, string texturePath, int totalFrames,
            FrameDef[]? frameTimeline = null, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            : this(name, texturePath, FrameLayout.Vertical, totalFrames, 1, 1, frameTimeline, frameDuration, mode)
        {
        }
        public FrameTexture(string name, string texturePath, int rows, int columns,
            FrameDef[]? frameTimeline = null, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
            : this(name, texturePath, FrameLayout.Grid, rows * columns, rows, columns, frameTimeline, frameDuration, mode)
        {
        }
        private FrameTexture(string name, string texturePath, FrameLayout layout, int totalFrames,
            int rows, int columns, FrameDef[]? frameTimeline, int frameDuration, FrameMode mode)
        {
            Name = name;
            _texturePath = texturePath;
            Layout = layout;
            TotalFrames = totalFrames;
            Rows = rows;
            Columns = columns;
            Mode = mode;
            // 计算 tick->帧索引 映射（预分配避免中间集合扩展）
            if (frameTimeline == null || frameTimeline.Length == 0)
            {
                int total = FrameCount * Math.Max(1, frameDuration);
                _tickToFrameIndex = new int[total];
                int pos = 0;
                for (int f = 0; f < FrameCount; f++)
                {
                    for (int d = 0; d < frameDuration; d++)
                    {
                        _tickToFrameIndex[pos++] = f;
                    }
                }
            }
            else
            {
                int total = 0;
                int[] durations = new int[frameTimeline.Length];
                for (int i = 0; i < frameTimeline.Length; i++)
                {
                    int dur = frameTimeline[i].ResolveDuration(frameDuration);
                    durations[i] = dur;
                    total += dur;
                }
                _tickToFrameIndex = new int[total];
                int pos = 0;
                for (int i = 0; i < frameTimeline.Length; i++)
                {
                    int frameIndex = frameTimeline[i].FrameIndex;
                    int dur = durations[i];
                    for (int j = 0; j < dur; j++)
                    {
                        _tickToFrameIndex[pos++] = frameIndex;
                    }
                }
            }
        }
        internal void Load()
        {
            if (_isLoaded)
            {
                return;
            }
            _spriteSheet = ModContent.Request<Texture2D>(_texturePath, AssetRequestMode.ImmediateLoad).Value
                          ?? throw new InvalidOperationException($"Failed to load texture '{_texturePath}'");
            if (_isSingleImage)
            {
                return;
            }
            _graphicsDevice = Main.graphics?.GraphicsDevice
                              ?? throw new InvalidOperationException($"GraphicsDevice not available");
            if (Layout == FrameLayout.Vertical)
            {
                if (_spriteSheet.Height % TotalFrames != 0)
                {
                    throw new ArgumentException($"SpriteSheet height not divisible by TotalFrames.");
                }
                _frameWidth = _spriteSheet.Width;
                _frameHeight = _spriteSheet.Height / TotalFrames;
            }
            else
            {
                if (_spriteSheet.Height % Rows != 0 || _spriteSheet.Width % Columns != 0)
                {
                    throw new ArgumentException($"SpriteSheet size not divisible by grid.");
                }
                _frameWidth = _spriteSheet.Width / Columns;
                _frameHeight = _spriteSheet.Height / Rows;
            }
            // 使用固定数组替代字典以减少查找开销
            _frameCache = new Texture2D[FrameCount];
            _isLoaded = true;
        }
        /// <summary>
        /// 获取指定帧的独立纹理。首次调用时从图集切割并缓存，后续直接返回。
        /// </summary>
        public Texture2D GetFrame(int frameIndex)
        {
            if (!_isLoaded)
            {
                Load();
            }
            if (_isSingleImage)
            {
                return _spriteSheet ?? TextureAssets.MagicPixel.Value;
            }
            frameIndex = Math.Clamp(frameIndex, 0, FrameCount - 1);
            Texture2D? cached = _frameCache?[frameIndex];
            if (cached != null)
            {
                return cached;
            }
            Rectangle sourceRect = GetFrameSourceRect(frameIndex);
            int pixelCount = sourceRect.Width * sourceRect.Height;
            Color[] buffer = ArrayPool<Color>.Shared.Rent(pixelCount);
            try
            {
                _spriteSheet?.GetData(0, sourceRect, buffer, 0, pixelCount);
                Texture2D texture = new(_graphicsDevice, sourceRect.Width, sourceRect.Height);
                texture.SetData(buffer, 0, pixelCount);
                _frameCache?[frameIndex] = texture;
                return texture;
            }
            finally
            {
                ArrayPool<Color>.Shared.Return(buffer);
            }
        }
        /// <summary>
        /// 获取当前帧的独立纹理。
        /// </summary>
        public Texture2D GetCurrentFrame() => GetFrame(CurrentFrameIndex);
        /// <summary>
        /// 获取指定帧在原始图集中的源矩形（无需切割独立纹理时使用）。
        /// </summary>
        public Rectangle GetFrameSourceRect(int frameIndex)
        {
            if (!_isLoaded)
            {
                Load();
            }
            if (_isSingleImage)
            {
                return new Rectangle(0, 0, _spriteSheet?.Width ?? 0, _spriteSheet?.Height ?? 0);
            }
            if (Layout == FrameLayout.Vertical)
            {
                return new Rectangle(0, frameIndex * _frameHeight, _frameWidth, _frameHeight);
            }
            int row = frameIndex / Columns;
            int col = frameIndex % Columns;
            return new Rectangle(col * _frameWidth, row * _frameHeight, _frameWidth, _frameHeight);
        }
        public Rectangle GetFrameRect() => new(0, 0, GetFrameSize().X, GetFrameSize().Y);
        public Point GetFrameSize()
        {
            if (!_isLoaded)
            {
                Load();
            }
            return _isSingleImage ? new Point(_spriteSheet?.Width ?? 0, _spriteSheet?.Height ?? 0) : new Point(_frameWidth, _frameHeight);
        }
        internal void Unload()
        {
            Main.QueueMainThreadAction(() =>
            {
                if (_frameCache != null)
                {
                    for (int i = 0; i < _frameCache.Length; i++)
                    {
                        _frameCache[i]?.Dispose();
                        _frameCache[i] = null;
                    }
                    _frameCache = null;
                }
                _spriteSheet = null;
                _graphicsDevice = null;
                _isLoaded = false;
            });
        }

        /// <summary>
        /// 获取指定帧的源矩形（在原始贴图中的位置）
        /// </summary>
        public Rectangle GetFrameSourceRect(int frameIndex, int frameWidth, int frameHeight)
        {
            if (_isSingleImage)
            {
                return new Rectangle(0, 0, _spriteSheet?.Width ?? 0, _spriteSheet?.Height ?? 0);
            }
            if (Layout == FrameLayout.Vertical)
            {
                return new Rectangle(0, frameIndex * frameHeight, frameWidth, frameHeight);
            }
            int row = frameIndex / Columns;
            int col = frameIndex % Columns;
            return new Rectangle(col * frameWidth, row * frameHeight, frameWidth, frameHeight);
        }
        public int CurrentFrameIndex
        {
            get
            {
                if (TotalDuration is null or 0)
                {
                    return 0;
                }
                int tick = Mode switch
                {
                    FrameMode.Loop => CurrentTick % TotalDuration.Value,
                    FrameMode.PingPong => CalculatePingPongTick(),
                    FrameMode.Once => Math.Min(CurrentTick, TotalDuration.Value - 1),
                    _ => 0
                };
                return _tickToFrameIndex?[tick] ?? 0;
            }
        }
        private int CalculatePingPongTick()
        {
            int? cycle = (TotalDuration - 1) * 2;
            if (cycle is null or <= 0)
            {
                return 0;
            }
            int t = CurrentTick % cycle.Value;
            return t < TotalDuration ? t : cycle.Value - t;
        }
        internal void Update()
        {
            if (!AutoUpdate || _isSingleImage)
            {
                return;
            }
            CurrentTick++;
        }
        public void Reset() => CurrentTick = 0;
    }
    public readonly record struct FrameDef(int FrameIndex, int DurationTicks)
    {
        public static implicit operator FrameDef(int frameIndex) => new(frameIndex, -1);
        public int ResolveDuration(int defaultDuration) => DurationTicks > 0 ? DurationTicks : defaultDuration;
    }
}