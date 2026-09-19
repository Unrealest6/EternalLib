namespace EternalLib
{
    public enum GradientDirection
    {
        Left,
        Right
    }
    /// <summary>
    /// 文本渐变色定义：按 <see cref="MillisecondsPerColor"/> 在 <see cref="Colors"/> 之间循环偏移，
    /// 供 <c>string.ApplyGradient</c> 生成 <c>[c/HEX:字符]</c> 标签序列。
    /// </summary>
    public sealed class ColorGradient
    {
        /// <summary>默认每种颜色的停留时间（毫秒，约等于 60 FPS 的一帧）。</summary>
        public const double DefaultMillisecondsPerColor = 1000.0 / 60.0;
        private const int MaxCacheEntries = 256;
        private static readonly Dictionary<string, ColorGradient> Registry = [];
        private static readonly Dictionary<GradientCacheKey, string> TextCache = [];
        /// <summary>已注册的渐变（只读视图，写入请使用 <see cref="Register"/> / <see cref="RegisterOrReplace"/>）。</summary>
        public static IReadOnlyDictionary<string, ColorGradient> Gradients => Registry;
        public Color[] Colors { get; }
        public string[] HexColors { get; }
        public double MillisecondsPerColor { get; }
        public GradientDirection Direction { get; }
        /// <summary>
        /// 注册一个渐变；键已存在时不做任何修改并返回 <c>false</c>。
        /// </summary>
        public static bool Register(string key, Color[]? colors,
            double millisecondsPerColor = DefaultMillisecondsPerColor,
            GradientDirection direction = GradientDirection.Left)
        {
            if (string.IsNullOrEmpty(key) || Registry.ContainsKey(key))
            {
                return false;
            }
            Registry[key] = new ColorGradient(colors, millisecondsPerColor, direction);
            return true;
        }
        /// <summary>
        /// 注册或覆盖一个渐变（用于模组配置改变后热更新）。
        /// </summary>
        public static bool RegisterOrReplace(string key, Color[]? colors,
            double millisecondsPerColor = DefaultMillisecondsPerColor,
            GradientDirection direction = GradientDirection.Left)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            Registry[key] = new ColorGradient(colors, millisecondsPerColor, direction);
            TextCache.Clear();
            return true;
        }
        /// <summary>移除一个渐变。</summary>
        public static bool Unregister(string key)
        {
            bool removed = Registry.Remove(key);
            if (removed)
            {
                TextCache.Clear();
            }
            return removed;
        }
        public static bool TryGet(string key, out ColorGradient gradient) => Registry.TryGetValue(key, out gradient!);
        private ColorGradient(Color[]? colors,
            double millisecondsPerColor = DefaultMillisecondsPerColor,
            GradientDirection direction = GradientDirection.Left)
        {
            Colors = colors is { Length: > 0 } ? colors : [Color.White];
            MillisecondsPerColor = Math.Max(0.001, millisecondsPerColor);
            Direction = direction;
            HexColors = new string[Colors.Length];
            for (int i = 0; i < Colors.Length; i++)
            {
                Color color = Colors[i];
                HexColors[i] = color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            }
        }
        /// <summary>当前时间对应的起始色序号。</summary>
        public int GetStepIndex(double? millisecondsPerColor = null)
        {
            double interval = Math.Max(0.001, millisecondsPerColor ?? MillisecondsPerColor);
            // Main.GlobalTimeWrappedHourly 的单位是秒（3600 秒一循环），乘 1000 转成毫秒。
            double elapsedMilliseconds = Main.GlobalTimeWrappedHourly * 1000.0;
            int step = (int)(elapsedMilliseconds / interval % Colors.Length);
            return step < 0 ? step + Colors.Length : step;
        }
        /// <summary>带缓存的文本着色结果，避免同一个 tooltip 每帧重复拼接字符串。</summary>
        internal static bool TryGetCached(in GradientCacheKey key, out string? value) => TextCache.TryGetValue(key, out value);
        internal static void StoreCached(in GradientCacheKey key, string value)
        {
            if (TextCache.Count >= MaxCacheEntries)
            {
                TextCache.Clear();
            }
            TextCache[key] = value;
        }
        internal static void ClearAll()
        {
            Registry.Clear();
            TextCache.Clear();
        }
        /// <summary>按文本 + 渐变 + 色序号缓存，色序号变化时会自然失效。</summary>
        internal readonly record struct GradientCacheKey(string Text, ColorGradient Gradient, int Step, bool Reverse);
    }
}
