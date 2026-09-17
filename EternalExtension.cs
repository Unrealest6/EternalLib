namespace EternalLib
{
    public static class EternalExtension
    {
        extension(string text)
        {
            /// <summary>
            /// 以注册名取渐变并着色。渐变不存在时原样返回。
            /// </summary>
            public string ApplyGradient(string gradientKey, double? millisecondsPerColor = null, GradientDirection? direction = null)
                => ColorGradient.Gradients.TryGetValue(gradientKey, out ColorGradient? gradient)
                    ? text.ApplyGradient(gradient, millisecondsPerColor, direction)
                    : text;
            /// <summary>
            /// 直接以 <see cref="ColorGradient"/> 实例着色。
            /// <para>方括号字符不参与着色（原样输出），否则会生成残缺的 <c>[c/...]</c> 标签把整行文本的解析弄坏。</para>
            /// </summary>
            public string ApplyGradient(ColorGradient? gradient, double? millisecondsPerColor = null, GradientDirection? direction = null)
            {
                if (gradient is null || string.IsNullOrEmpty(text) || gradient.Colors.Length == 0)
                {
                    return text;
                }
                double interval = millisecondsPerColor ?? gradient.MillisecondsPerColor;
                if (interval < 0.001)
                {
                    return text;
                }
                bool reverse = (direction ?? gradient.Direction) == GradientDirection.Right;
                int step = gradient.GetStepIndex(interval);
                ColorGradient.GradientCacheKey cacheKey = new(text, gradient, step, reverse);
                if (ColorGradient.TryGetCached(cacheKey, out string? cached))
                {
                    return cached ?? text;
                }
                string built = BuildGradientText(text, gradient.HexColors, step, reverse);
                ColorGradient.StoreCached(cacheKey, built);
                return built;
            }
        }
        /// <summary>逐字符拼接颜色标签。抽成独立方法以避免每次调用都分配闭包。</summary>
        private static string BuildGradientText(string text, string[] hexColors, int step, bool reverse)
        {
            int colorCount = hexColors.Length;
            StringBuilder builder = new(text.Length * 10);
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character is '[' or ']')
                {
                    builder.Append(character);
                    continue;
                }
                int index = reverse ? step - i : step + i;
                index %= colorCount;
                if (index < 0)
                {
                    index += colorCount;
                }
                builder.Append("[c/").Append(hexColors[index]).Append(':').Append(character).Append(']');
            }
            return builder.ToString();
        }
        extension(Color)
        {
            /// <summary>整体加减亮度（保持 Alpha）。</summary>
            public static Color operator +(Color color, short amount) =>
                new((byte)Math.Clamp(color.R + amount, byte.MinValue, byte.MaxValue)
                , (byte)Math.Clamp(color.G + amount, byte.MinValue, byte.MaxValue)
                , (byte)Math.Clamp(color.B + amount, byte.MinValue, byte.MaxValue), color.A);
            public static Color operator -(Color color, short amount) =>
                new((byte)Math.Clamp(color.R - amount, byte.MinValue, byte.MaxValue)
                , (byte)Math.Clamp(color.G - amount, byte.MinValue, byte.MaxValue)
                , (byte)Math.Clamp(color.B - amount, byte.MinValue, byte.MaxValue), color.A);
        }
        extension(Player player)
        {
            /// <summary>是否允许自由飞行，由 <see cref="EternalPlayer"/> 读取。</summary>
            public bool CanFly
            {
                get => player.GetModPlayer<EternalPlayer>().CanFly;
                set => player.GetModPlayer<EternalPlayer>().CanFly = value;
            }
            /// <summary>飞行状态下的水平最大速度。</summary>
            public float FlySpeedX => player.GetModPlayer<EternalPlayer>().FlySpeedX;
            /// <summary>飞行状态下的垂直最大速度。</summary>
            public float FlySpeedY => player.GetModPlayer<EternalPlayer>().FlySpeedY;
            /// <summary>当前是否处于飞行状态。</summary>
            public bool IsFlying => player.GetModPlayer<EternalPlayer>().IsFlying;
            /// <summary>就地按最大速度分量钳制玩家速度。</summary>
            public void ClampVelocity(Vector2 maxVelocity) => player.velocity = new Vector2(
                Math.Clamp(player.velocity.X, -Math.Abs(maxVelocity.X), Math.Abs(maxVelocity.X)),
                Math.Clamp(player.velocity.Y, -Math.Abs(maxVelocity.Y), Math.Abs(maxVelocity.Y)));
        }
        extension<T>(T[] entities) where T : Entity
        {
            /// <summary>取离指定实体最近、活跃的实体。</summary>
            public T? GetRecent(Entity entity, float? radius = null) => entities.GetNearest(entity.Center, null, radius);
            public T? GetRecent(Vector2 center, float? radius = null) => entities.GetNearest(center, null, radius);
            /// <summary>
            /// 取距离中心最近且满足条件的活跃实体。
            /// </summary>
            public T? GetNearest(Vector2 center, Func<T, bool>? predicate = null, float? radius = null)
            {
                float maxDistanceSq = radius.HasValue ? radius.Value * radius.Value : float.MaxValue;
                T? best = null;
                float bestDistanceSq = 0f;
                foreach (T candidate in entities)
                {
                    if (!candidate.active || (predicate is not null && !predicate(candidate)))
                    {
                        continue;
                    }
                    float distanceSq = candidate.DistanceSQ(center);
                    if (distanceSq > maxDistanceSq)
                    {
                        continue;
                    }
                    if (best is null || distanceSq < bestDistanceSq)
                    {
                        best = candidate;
                        bestDistanceSq = distanceSq;
                    }
                }
                return best;
            }
        }
        extension<T>(IEnumerable<T> entities) where T : Entity
        {
            public T? GetRecent(Entity entity, float? radius = null) => entities.GetNearest(entity.Center, null, radius);
            public T? GetRecent(Vector2 center, float? radius = null) => entities.GetNearest(center, null, radius);
            public T? GetNearest(Vector2 center, Func<T, bool>? predicate = null, float? radius = null)
            {
                if (entities is T[] array)
                {
                    return array.GetNearest(center, predicate, radius);
                }
                float maxDistanceSq = radius.HasValue ? radius.Value * radius.Value : float.MaxValue;
                T? best = null;
                float bestDistanceSq = 0f;
                foreach (T candidate in entities)
                {
                    if (!candidate.active || (predicate is not null && !predicate(candidate)))
                    {
                        continue;
                    }
                    float distanceSq = candidate.DistanceSQ(center);
                    if (distanceSq > maxDistanceSq)
                    {
                        continue;
                    }
                    if (best is null || distanceSq < bestDistanceSq)
                    {
                        best = candidate;
                        bestDistanceSq = distanceSq;
                    }
                }
                return best;
            }
        }
        extension(ModContent)
        {
            public static FrameTexture? GetFrameTexture(string name) => FrameTextureSystem.Animations.GetValueOrDefault(name);
            public static bool TryGetFrameTexture(string name, out FrameTexture texture) => FrameTextureSystem.TryGet(name, out texture);
        }
        extension(uint num)
        {
            /// <summary>三角波（0 → amplitude → 0）。</summary>
            public uint TriangleWave(uint amplitude)
            {
                if (amplitude == 0)
                {
                    return 0;
                }
                return (uint)(amplitude - Math.Abs(num % (2L * amplitude) - amplitude));
            }
        }
        extension(float num)
        {
            public bool IsWithinTolerance(float target, float tolerance)
            {
                if (tolerance < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance can't be negative");
                }
                if (float.IsNaN(num) || float.IsNaN(target) || float.IsInfinity(num) || float.IsInfinity(target))
                {
                    return false;
                }
                return Math.Abs(num - target) <= tolerance;
            }
        }
        extension(double num)
        {
            public bool IsWithinTolerance(double target, double tolerance)
            {
                if (tolerance < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative");
                }
                if (double.IsNaN(num) || double.IsNaN(target) || double.IsInfinity(num) || double.IsInfinity(target))
                {
                    return false;
                }
                return Math.Abs(num - target) <= tolerance;
            }
        }
    }
}