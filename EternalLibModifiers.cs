using System.Numerics;

namespace EternalLib
{
    /// <summary>
    /// 描述一份可叠加的数值修饰（modifier），用于把原始数值按"加成 → 倍率 → 固定加值"的顺序变换。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 核心公式为：<c>ApplyTo(x) = (x + Base) * Additive * Multiplicative + Flat</c>。
    /// </para>
    /// <para>
    /// 四个分量的语义：
    /// <list type="bullet">
    ///   <item><description><see cref="Base"/>：先加到原值上的偏移量，与 <see cref="Additive"/> 同层，受乘法影响。</description></item>
    ///   <item><description><see cref="Additive"/>：加法倍率（1 = 不变，2 = 翻倍，4 = ×4）。</description></item>
    ///   <item><description><see cref="Multiplicative"/>：乘法倍率（1 = 不变），与其他修饰符叠加时相乘。</description></item>
    ///   <item><description><see cref="Flat"/>：最后无条件加上的固定值，不受任何倍率影响。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 类型参数 <typeparamref name="T"/> 必须同时是值类型并实现 <see cref="INumber{T}"/>，
    /// 因此可支持 int、long、float、double、decimal、BigInteger 等所有标准数值类型。
    /// </para>
    /// </remarks>
    /// <typeparam name="T">底层数值类型。</typeparam>
    public record struct NumericModifier<T> where T : struct, INumber<T>
    {
        /// <summary>
        /// 不变加成（<c>ApplyTo(x) == x</c>）。
        /// </summary>
        /// <remarks>
        /// 等价于 <c>new NumericModifier&lt;T&gt;()</c>：四个分量分别为 Base=0、Additive=1、Multiplicative=1、Flat=0。
        /// </remarks>
        public static readonly NumericModifier<T> Default = new();
        /// <summary>
        /// 加法项的一部分：先加到原值上，与 <see cref="Additive"/> 同层并按乘法生效。
        /// </summary>
        public T Base { get; set; }
        /// <summary>
        /// 最后的固定加值：在所有倍率计算完成后无条件加上，不受任何倍率影响。
        /// </summary>
        public T Flat { get; set; }
        /// <summary>
        /// 加法倍率（1 = 不变；<c>new NumericModifier&lt;int&gt;(4)</c> 表示 ×4）。
        /// </summary>
        public T Additive { get; }
        /// <summary>
        /// 乘法倍率（1 = 不变）。多个修饰符叠加时通过乘法合并。
        /// </summary>
        public T Multiplicative { get; }
        /// <summary>
        /// 把 <paramref name="modifier"/> 限制到 [<paramref name="minModifier"/>, <paramref name="maxModifier"/>] 区间。
        /// </summary>
        /// <param name="modifier">要被钳制的修饰符。</param>
        /// <param name="minModifier">下界（含）。</param>
        /// <param name="maxModifier">上界（含）。</param>
        /// <returns>钳制后的修饰符。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="minModifier"/> 大于 <paramref name="maxModifier"/> 时抛出。</exception>
        /// <remarks>
        /// 比较方式为把三者都应用到 0 上（<see cref="ApplyTo"/>），因此比较的是"最终效果"而非字段本身。
        /// </remarks>
        public static NumericModifier<T> Clamp(NumericModifier<T> modifier, NumericModifier<T> minModifier, NumericModifier<T> maxModifier)
            => maxModifier < minModifier ? throw new ArgumentException("Min is greater than max!") : modifier < minModifier ? minModifier
            : modifier > maxModifier ? maxModifier : modifier;
        /// <summary>
        /// 在两个修饰符之间按 <paramref name="t"/> 进行线性插值（不对 <paramref name="t"/> 做范围钳制）。
        /// </summary>
        /// <param name="from">t = 0 时返回的修饰符。</param>
        /// <param name="to">t = 1 时返回的修饰符。</param>
        /// <param name="t">插值因子，通常位于 [0, 1] 区间；允许外推。</param>
        /// <returns>插值后的新修饰符。</returns>
        /// <remarks>
        /// 四个分量分别独立做线性插值。注意：对 <see cref="Multiplicative"/> 做线性插值得到的是算术平均而非几何平均，
        /// 大多数场景可接受，但如需"效果上的中点"请另行处理。
        /// </remarks>
        public static NumericModifier<T> Lerp(NumericModifier<T> from, NumericModifier<T> to, T t)
            => new(from.Additive + (to.Additive - from.Additive) * t, from.Multiplicative + (to.Multiplicative - from.Multiplicative) * t
                , from.Base + (to.Base - from.Base) * t, from.Flat + (to.Flat - from.Flat) * t);
        /// <summary>
        /// 使用 SmoothStep 曲线（3t² − 2t³）在两个修饰符之间插值，实现缓入缓出效果。
        /// </summary>
        /// <param name="from">t = 0 时返回的修饰符。</param>
        /// <param name="to">t = 1 时返回的修饰符。</param>
        /// <param name="t">插值因子，会被钳制到 [0, 1]。</param>
        /// <returns>插值后的新修饰符。</returns>
        public static NumericModifier<T> SmoothStep(NumericModifier<T> from, NumericModifier<T> to, T t)
        {
            t = T.Clamp(t, T.Zero, T.One);
            T s = t * t * (T.One + T.One + T.One - (t + t));
            return Lerp(from, to, s);
        }
        /// <summary>
        /// 主构造函数：仅 <paramref name="additive"/> 为必填，其余分量省略时取默认值（Multiplicative=1，Base=0，Flat=0）。
        /// </summary>
        /// <param name="additive">加法倍率。</param>
        /// <param name="multiplicative">乘法倍率；<c>null</c> 表示取 1。</param>
        /// <param name="base">加法项的一部分；<c>null</c> 表示取 0。</param>
        /// <param name="flat">最后的固定加值；<c>null</c> 表示取 0。</param>
        public NumericModifier(T additive, T? multiplicative = null, T? @base = null, T? flat = null)
        {
            Additive = additive;
            Multiplicative = multiplicative ?? T.One;
            Base = @base ?? T.Zero;
            Flat = flat ?? T.Zero;
        }
        /// <summary>
        /// 无参构造：等价于 <see cref="Default"/>（不变加成）。
        /// </summary>
        public NumericModifier() : this(T.One, T.One, T.Zero, T.Zero) { }
        /// <summary>
        /// 合并两份修饰符（加法项相加、乘法项相乘、Base 与 Flat 分别相加）。
        /// </summary>
        public static NumericModifier<T> operator +(NumericModifier<T> modifier1, NumericModifier<T> modifier2) =>
            new(modifier1.Additive + modifier2.Additive, modifier1.Multiplicative * modifier2.Multiplicative
                , modifier1.Base + modifier2.Base, modifier1.Flat + modifier2.Flat);
        /// <summary>
        /// 反向合并两份修饰符，是 <see cref="operator +(NumericModifier{T}, NumericModifier{T})"/> 的逆运算。
        /// </summary>
        public static NumericModifier<T> operator -(NumericModifier<T> modifier1, NumericModifier<T> modifier2) =>
            new(modifier1.Additive - modifier2.Additive, modifier1.Multiplicative / modifier2.Multiplicative
                , modifier1.Base - modifier2.Base, modifier1.Flat - modifier2.Flat);
        /// <summary>
        /// 把标量 <paramref name="add"/> 加到加法倍率上（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator +(NumericModifier<T> modifier, T add) =>
            new(modifier.Additive + add, modifier.Multiplicative, modifier.Base, modifier.Flat);
        /// <summary>
        /// 把标量 <paramref name="add"/> 加到加法倍率上（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator +(T add, NumericModifier<T> modifier) =>
            new(modifier.Additive + add, modifier.Multiplicative, modifier.Base, modifier.Flat);
        /// <summary>
        /// 从加法倍率中减去标量 <paramref name="sub"/>（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator -(NumericModifier<T> modifier, T sub) =>
            new(modifier.Additive - sub, modifier.Multiplicative, modifier.Base, modifier.Flat);
        /// <summary>
        /// 把标量 <paramref name="mul"/> 乘到乘法倍率上（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator *(NumericModifier<T> modifier, T mul) =>
            new(modifier.Additive, modifier.Multiplicative * mul, modifier.Base, modifier.Flat);
        /// <summary>
        /// 把标量 <paramref name="mul"/> 乘到乘法倍率上（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator *(T mul, NumericModifier<T> modifier) =>
            new(modifier.Additive, modifier.Multiplicative * mul, modifier.Base, modifier.Flat);
        /// <summary>
        /// 把乘法倍率除以标量 <paramref name="div"/>（其余分量不变）。
        /// </summary>
        public static NumericModifier<T> operator /(NumericModifier<T> modifier, T div) =>
            new(modifier.Additive, modifier.Multiplicative / div, modifier.Base, modifier.Flat);
        /// <summary>
        /// 比较两份修饰符的"实际效果"是否满足 &lt;。
        /// </summary>
        public static bool operator <(NumericModifier<T> modifier1, NumericModifier<T> modifier2)
            => modifier1.ApplyTo(T.Zero) < modifier2.ApplyTo(T.Zero);
        /// <summary>
        /// 比较两份修饰符的"实际效果"是否满足 &gt;。
        /// </summary>
        public static bool operator >(NumericModifier<T> modifier1, NumericModifier<T> modifier2)
            => modifier1.ApplyTo(T.Zero) > modifier2.ApplyTo(T.Zero);
        /// <summary>
        /// 基于四个分量生成哈希码（与 <c>record struct</c> 自动生成的 <c>Equals</c> 一致）。
        /// </summary>
        public override readonly int GetHashCode() => HashCode.Combine(Base, Additive, Multiplicative, Flat);
        /// <summary>
        /// 把这份修饰符应用到具体数值上。
        /// </summary>
        /// <param name="baseValue">待变换的原始数值。</param>
        /// <returns>变换后的数值，公式为 <c>(baseValue + Base) * Additive * Multiplicative + Flat</c>。</returns>
        public readonly T ApplyTo(T baseValue) => (baseValue + Base) * Additive * Multiplicative + Flat;
        /// <summary>
        /// 把两份修饰符合并为一份，效果等同于"先应用 <paramref name="m"/>，再应用当前修饰符"。
        /// </summary>
        /// <param name="m">要合并进来的修饰符。</param>
        /// <returns>合并后的新修饰符。</returns>
        /// <remarks>
        /// 合并规则为：Additive 相加后再减 1、Multiplicative 相乘、Base 与 Flat 分别相加。
        /// 相比 <see cref="operator +(NumericModifier{T}, NumericModifier{T})"/>，此方法针对"每份修饰符都含有一份 1 的基准倍率"这一情况做了补偿。
        /// </remarks>
        public readonly NumericModifier<T> CombineWith(NumericModifier<T> m)
            => new(Additive + m.Additive - T.One, Multiplicative * m.Multiplicative, Flat + m.Flat, Base + m.Base);
        /// <summary>
        /// 按比例缩放这份修饰符（1 = 不变），通常用于难度、等级等动态系数。
        /// </summary>
        /// <param name="scale">缩放系数。</param>
        /// <returns>缩放后的新修饰符。</returns>
        /// <remarks>
        /// 对 Additive 与 Multiplicative 的缩放是"围绕 1 的线性插值"：
        /// <c>newAdditive = 1 + (Additive - 1) * scale</c>，使 scale = 0 时退化为不变加成。
        /// </remarks>
        public readonly NumericModifier<T> Scale(T scale)
            => new(T.One + (Additive - T.One) * scale, T.One + (Multiplicative - T.One) * scale, Flat * scale, Base * scale);
        /// <summary>
        /// <see cref="ApplyTo"/> 的逆运算：从变换后的值反推出原始值。
        /// </summary>
        /// <param name="currentValue">已经过 <see cref="ApplyTo"/> 变换的值。</param>
        /// <returns>原始数值。</returns>
        /// <remarks>
        /// 公式为 <c>(currentValue - Flat) / (Multiplicative * Additive) - Base</c>。
        /// 仅当 <see cref="Multiplicative"/> 与 <see cref="Additive"/> 均非零时才有意义。
        /// </remarks>
        public readonly T Undo(T currentValue) => (currentValue - Flat) / (Multiplicative * Additive) - Base;
    }
}