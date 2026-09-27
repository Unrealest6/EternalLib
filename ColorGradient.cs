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
        private static readonly Dictionary<string, ColorGradient> Registry = [];
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
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            key = ModLoader.Mods.FirstOrDefault(mod => mod.Code == Assembly.GetCallingAssembly())?.Name ?? "" + key;
            if (Registry.ContainsKey(key))
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
            key = ModLoader.Mods.FirstOrDefault(mod => mod.Code == Assembly.GetCallingAssembly())?.Name ?? "" + key;
            Registry[key] = new ColorGradient(colors, millisecondsPerColor, direction);
            return true;
        }
        /// <summary>移除一个渐变。</summary>
        public static bool Unregister(string key)
        {
            key = ModLoader.Mods.FirstOrDefault(mod => mod.Code == Assembly.GetCallingAssembly())?.Name ?? "" + key;
            bool removed = Registry.Remove(key);
            return removed;
        }
        public static bool TryGet(string key, out ColorGradient gradient)
        {
            key = ModLoader.Mods.FirstOrDefault(mod => mod.Code == Assembly.GetCallingAssembly())?.Name ?? "" + key;
            return Registry.TryGetValue(key, out gradient!);
        }
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
        internal static void ClearAll()
        {
            Registry.Clear();
        }
    }
}
