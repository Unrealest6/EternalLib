namespace EternalLib
{
    public static class EternalExtension
    {
        extension(string text)
        {
            public string ApplyGradient(string gradientKey, double? millisecondsPerColor = null, GradientDirection? direction = null)
            {
                if (!ColorGradient.Gradients.TryGetValue(gradientKey, out ColorGradient? gradient))
                {
                    return text;
                }
                millisecondsPerColor ??= gradient.MillisecondsPerColor;
                direction ??= gradient.Direction;
                int colorCount = gradient.Colors.Length;
                if (colorCount == 0 || string.IsNullOrEmpty(text) || millisecondsPerColor < 0.001)
                {
                    return text;
                }
                double currentTimeSeconds = Main.GlobalTimeWrappedHourly;
                double currentTimeMs = currentTimeSeconds * 1000000.0;
                int step = (int)(currentTimeMs / (millisecondsPerColor * 1000) % colorCount);
                if (step < 0)
                {
                    step += colorCount;
                }
                StringBuilder sb = new(text.Length * 10);
                string[] hexColors = gradient.HexColors;
                for (int i = 0; i < text.Length; i++)
                {
                    int idx = direction == GradientDirection.Right ? step - i : step + i;
                    idx %= colorCount;
                    if (idx < 0)
                    {
                        idx += colorCount;
                    }
                    string hex = hexColors[idx];
                    sb.Append("[c/");
                    sb.Append(hex);
                    sb.Append(':');
                    sb.Append(text[i]);
                    sb.Append(']');
                }
                return sb.ToString();
            }
        }
        extension(Color)
        {
            public static Color operator +(Color color, short amount) =>
                new((byte)Math.Clamp(byte.MinValue, color.R + amount, byte.MaxValue)
                , (byte)Math.Clamp(byte.MinValue, color.G + amount, byte.MaxValue)
                , (byte)Math.Clamp(byte.MinValue, color.B + amount, byte.MaxValue), color.A);
            public static Color operator -(Color color, short amount) =>
                new((byte)Math.Clamp(byte.MinValue, color.R - amount, byte.MaxValue)
                , (byte)Math.Clamp(byte.MinValue, color.G - amount, byte.MaxValue)
                , (byte)Math.Clamp(byte.MinValue, color.B - amount, byte.MaxValue), color.A);
        }
        extension(Player player)
        {
            public bool CanFly
            {
                get => player.GetModPlayer<EternalPlayer>()?.CanFly ?? false;
                set => player.GetModPlayer<EternalPlayer>()?.CanFly = value;
            }
            public float FlySpeedX => player.GetModPlayer<EternalPlayer>()?.FlySpeedX ?? float.NaN;
            public float FlySpeedY => player.GetModPlayer<EternalPlayer>()?.FlySpeedY ?? float.NaN;
            public void ResetVelocity(Vector2 maxVelocity) => player.velocity = new Vector2(Math.Clamp(player.velocity.X, -maxVelocity.X, maxVelocity.X)
                , Math.Clamp(player.velocity.Y, -maxVelocity.Y, maxVelocity.Y));
        }
        extension<T>(T[] entities) where T : Entity
        {
            public T? GetRecent(Entity entity, float? radius = null) => entities.GetRecent(entity.Center, radius);
            public T? GetRecent(Vector2 center, float? radius = null)
            {
                int? index = null;
                float? minDistanceSq = null;
                for (int i = 0; i < entities.Length; i++)
                {
                    float distanceSq = entities[i].DistanceSQ(center);
                    if (distanceSq > (radius * radius ?? float.MaxValue) || minDistanceSq != null && !(distanceSq < minDistanceSq))
                    {
                        continue;
                    }
                    minDistanceSq = distanceSq;
                    index = i;
                }
                return index is null ? null : entities[index.Value];
            }
        }
        extension<T>(IEnumerable<T> entities) where T : Entity
        {
            public T? GetRecent(Entity entity, float? radius = null) => entities.GetRecent(entity.Center, radius);
            public T? GetRecent(Vector2 center, float? radius = null)
            {
                if (entities is T[] array)
                {
                    return array.GetRecent(center, radius);
                }
                T? entity = null;
                float? minDistanceSq = null;
                foreach (T entity1 in entities)
                {
                    float distanceSq = entity1.DistanceSQ(center);
                    if (distanceSq > (radius * radius ?? float.MaxValue) || minDistanceSq != null && !(distanceSq < minDistanceSq))
                    {
                        continue;
                    }
                    minDistanceSq = distanceSq;
                    entity = entity1;
                }
                return entity;
            }
        }
        extension(ModContent)
        {
            public static FrameTexture? GetFrameTexture(string name) => FrameTextureSystem.Animations.GetValueOrDefault(name);
            public static bool TryGetFrameTexture(string name, out FrameTexture texture)
            {
                FrameTexture? texture0 = FrameTextureSystem.Animations.GetValueOrDefault(name);
                if (texture0 == null)
                {
                    texture = null!;
                    return false;
                }
                texture = texture0;
                return true;
            }
        }
        extension(uint num)
        {
            public uint TriangleWave(uint amplitude) => (uint)(amplitude - Math.Abs(num % (2L * amplitude) - amplitude));
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