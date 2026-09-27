namespace EternalLib
{
    public static class EternalExtension
    {
        extension(string text)
        {
            /// <summary>
            /// 以注册名取渐变并着色。渐变不存在时原样返回。
            /// </summary>
            public string ApplyGradient(string key, double? millisecondsPerColor = null, GradientDirection? direction = null)
            {
                key = ModLoader.Mods.FirstOrDefault(mod => mod.Code == Assembly.GetCallingAssembly())?.Name ?? "" + key;
                return ColorGradient.Gradients.TryGetValue(key, out ColorGradient? gradient)
                    ? text.ApplyGradient(gradient, millisecondsPerColor, direction) : text;
            }
            /// <summary>
            /// 直接以 <see cref="ColorGradient"/> 实例着色。方括号字符不参与着色（原样输出），否则会生成残缺的 <c>[c/...]</c> 标签把整行文本的解析弄坏。
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
                string built = BuildGradientText(text, gradient.HexColors, step, reverse);
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
            /// <summary>
            /// 背包（前 50 格）还能装下多少个该物品：先算同类堆叠的剩余空间，再算空格整叠容量。
            /// <para>联机时服务端用它判断“送背包”是否放得下（放不下就不该消耗 / 不该取走）。</para>
            /// </summary>
            /// <param name="item">要放入的物品（按 type / maxStack 判定）。</param>
            /// <returns>能放下的数量（不超过 <paramref name="item"/> 自身的数量）。</returns>
            public int FitAmount(Item item)
            {
                if (item.IsAir || item.stack <= 0)
                {
                    return 0;
                }
                int max = Math.Max(1, item.maxStack);
                int space = 0;
                for (int i = 0; i < 50 && space < item.stack; i++)
                {
                    Item inv = player.inventory[i];
                    if (inv is null)
                    {
                        continue;
                    }
                    if (inv.IsAir)
                    {
                        space += max;
                    }
                    else if (inv.type == item.type && inv.maxStack == max)
                    {
                        space += Math.Max(0, max - inv.stack);
                    }
                }
                return Math.Min(space, item.stack);
            }
            /// <summary>
            /// 将物品移入指定玩家的背包（联机时服务端用，服务端才有权改写玩家背包）。
            /// <para>背包装不下时不丢弃：余量留在 <paramref name="source"/> 里，由调用方决定掉落。</para>
            /// </summary>
            /// <param name="source">要移入的物品；放不下的部分会留在其中。</param>
            public void MoveItemToInventory(Item source)
            {
                if (source.IsAir)
                {
                    return;
                }
                for (int i = 0; i < 50 && !source.IsAir; i++)
                {
                    Item inv = player.inventory[i];
                    if (inv?.IsAir != false || inv.type != source.type || inv.maxStack != source.maxStack)
                    {
                        continue;
                    }
                    int space = inv.maxStack - inv.stack;
                    if (space <= 0)
                    {
                        continue;
                    }
                    if (space >= source.stack)
                    {
                        inv.stack += source.stack;
                        source.TurnToAir();
                        SoundEngine.PlaySound(SoundID.Grab);
                        return;
                    }
                    inv.stack = inv.maxStack;
                    source.stack -= space;
                }
                if (source.IsAir)
                {
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }
                for (int i = 0; i < 50; i++)
                {
                    if (!player.inventory[i].IsAir)
                    {
                        continue;
                    }
                    player.inventory[i] = source.Clone();
                    player.inventory[i].stack = source.stack;
                    source.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }
        private static readonly ConcurrentDictionary<string, Action<Item, Item>> BundleCache = new();
        extension(Item item)
        {
            public Item Clone(params Expression<Func<Item, object>>[] members)
            {
                Item clone = item.Clone();
                if (members.Length == 0)
                {
                    return clone;
                }
                Item.GetOrCompileBundle(members)(item, clone);
                return clone;
            }
            private static Action<Item, Item> GetOrCompileBundle(Expression<Func<Item, object>>[] members)
            {
                string key = Item.BuildKey(members);
                if (BundleCache.TryGetValue(key, out Action<Item, Item>? cached))
                {
                    return cached;
                }
                Action<Item, Item> compiled = Item.BuildBundle(members);
                BundleCache[key] = compiled;
                return compiled;
            }
            private static string BuildKey(Expression<Func<Item, object>>[] members)
            {
                StringBuilder sb = new(members.Length * 32);
                for (int i = 0; i < members.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append('|');
                    }
                    Expression body = members[i].Body.Unwrap();
                    while (body is MemberExpression member)
                    {
                        sb.Append(member.Member.DeclaringType?.Name);
                        sb.Append('.');
                        sb.Append(member.Member.Name);
                        sb.Append('/');
                        body = member.Expression!;
                    }
                }
                return sb.ToString();
            }
            private static Action<Item, Item> BuildBundle(Expression<Func<Item, object>>[] members)
            {
                ParameterExpression sourceParam = Expression.Parameter(typeof(Item), "source");
                ParameterExpression targetParam = Expression.Parameter(typeof(Item), "target");
                Expression[] assignments = new Expression[members.Length];
                for (int i = 0; i < members.Length; i++)
                {
                    Expression body = members[i].Body.Unwrap();
                    if (body is not MemberExpression final)
                    {
                        throw new ArgumentException($"Expression must be a member access: {members[i]}");
                    }
                    bool writable = final.Member switch
                    {
                        FieldInfo { IsInitOnly: false, IsLiteral: false } => true,
                        PropertyInfo { CanWrite: true } p when p.GetIndexParameters().Length == 0 => true,
                        _ => false
                    };
                    if (!writable)
                    {
                        throw new ArgumentException($"Member is not writable: {final.Member.Name}");
                    }
                    Expression sourceAccess = body.RebuildAccess(sourceParam);
                    Expression targetAccess = body.RebuildAccess(targetParam);
                    assignments[i] = Expression.Assign(targetAccess, sourceAccess);
                }
                Expression block = Expression.Block(assignments);
                return Expression.Lambda<Action<Item, Item>>(block, sourceParam, targetParam).Compile();
            }
        }
        extension(Expression expr)
        {
            private Expression Unwrap() => expr is UnaryExpression { NodeType: ExpressionType.Convert } unary ? unary.Operand : expr;
            private Expression RebuildAccess(ParameterExpression root)
            {
                expr = expr.Unwrap();
                List<MemberInfo> chain = [];
                Expression? current = expr;
                while (current is MemberExpression member)
                {
                    chain.Add(member.Member);
                    current = member.Expression;
                }
                if (current is not ParameterExpression)
                {
                    throw new ArgumentException($"Expression must start from a parameter: {expr}");
                }
                chain.Reverse();
                return chain.Aggregate<MemberInfo, Expression>(root, Expression.MakeMemberAccess);
            }
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
                    if (distanceSq > maxDistanceSq || best is not null && !(distanceSq < bestDistanceSq))
                    {
                        continue;
                    }
                    best = candidate;
                    bestDistanceSq = distanceSq;
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
                    if (distanceSq > maxDistanceSq || best is not null && !(distanceSq < bestDistanceSq))
                    {
                        continue;
                    }
                    best = candidate;
                    bestDistanceSq = distanceSq;
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