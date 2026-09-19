namespace EternalLib
{
    /// <summary>
    /// 整数（例如掉落数量）的通用加成：
    /// <c>(原值 + <see cref="Base"/>) * <see cref="Additive"/> * <see cref="Multiplicative"/> + <see cref="Flat"/></c>。
    /// <para>默认（<see cref="Default"/>）是不变的：<c>Additive</c> 与 <c>Multiplicative</c> 都为 1。</para>
    /// </summary>
    public struct Int32Modifier : IEquatable<Int32Modifier>
    {
        /// <summary>不变加成（<c>ApplyTo(x) == x</c>）。</summary>
        public static readonly Int32Modifier Default = new();
        /// <summary>加法项的一部分：先加到原值上（与 <see cref="Additive"/> 同层，按乘法生效）。</summary>
        public int Base = 0;
        /// <summary>最后的固定加值。</summary>
        public int Flat = 0;
        /// <summary>加法倍率（1 = 不变；<c>new Int32Modifier(4)</c> 即“×4”）。</summary>
        public int Additive { get; } = 1;
        /// <summary>乘法倍率（1 = 不变）。</summary>
        public int Multiplicative { get; } = 1;
        public Int32Modifier()
        {
            Additive = 1;
            Multiplicative = 1;
        }
        /// <param name="additive">加法倍率（1 = 不变）</param>
        /// <param name="multiplicative">乘法倍率（1 = 不变）</param>
        /// <param name="baseValue">先加到原值上的部分（默认 0）</param>
        /// <param name="flat">最后的固定加值（默认 0）</param>
        public Int32Modifier(int additive, int multiplicative = 1, int baseValue = 0, int flat = 0)
        {
            Base = baseValue;
            Flat = flat;
            Additive = additive;
            Multiplicative = multiplicative;
        }
        public static bool operator ==(Int32Modifier m1, Int32Modifier m2)
            => m1.Additive == m2.Additive && m1.Multiplicative == m2.Multiplicative && m1.Flat == m2.Flat && m1.Base == m2.Base;
        public static bool operator !=(Int32Modifier m1, Int32Modifier m2)
            => m1.Additive != m2.Additive || m1.Multiplicative != m2.Multiplicative || m1.Flat != m2.Flat || m1.Base != m2.Base;
        public static Int32Modifier operator +(Int32Modifier modifiers1, Int32Modifier modifiers2) =>
            new(modifiers1.Additive + modifiers2.Additive, modifiers1.Multiplicative * modifiers2.Multiplicative
                , modifiers1.Base + modifiers2.Base, modifiers1.Flat + modifiers2.Flat);
        public static Int32Modifier operator -(Int32Modifier modifiers1, Int32Modifier modifiers2) =>
            new(modifiers1.Additive - modifiers2.Additive, modifiers1.Multiplicative / modifiers2.Multiplicative
                , modifiers1.Base - modifiers2.Base, modifiers1.Flat - modifiers2.Flat);
        public static Int32Modifier operator +(Int32Modifier modifiers, int add) =>
            new(modifiers.Additive + add, modifiers.Multiplicative, modifiers.Base, modifiers.Flat);
        public static Int32Modifier operator -(Int32Modifier modifiers, int sub) =>
            new(modifiers.Additive - sub, modifiers.Multiplicative, modifiers.Base, modifiers.Flat);
        public static Int32Modifier operator *(Int32Modifier modifiers, int mul) =>
            new(modifiers.Additive, modifiers.Multiplicative * mul, modifiers.Base, modifiers.Flat);
        public static Int32Modifier operator /(Int32Modifier modifiers, int div) =>
            new(modifiers.Additive, modifiers.Multiplicative / div, modifiers.Base, modifiers.Flat);
        public readonly bool Equals(Int32Modifier other) => this == other;
        public override readonly bool Equals(object? obj) => obj is Int32Modifier m && this == m;
        public override readonly int GetHashCode() => HashCode.Combine(Base, Additive, Multiplicative, Flat);
        /// <summary>把加成套用到具体数值上。</summary>
        public readonly int ApplyTo(int baseValue) => (baseValue + Base) * Additive * Multiplicative + Flat;
        /// <summary>把两份加成合成一份。</summary>
        public readonly Int32Modifier CombineWith(Int32Modifier m)
            => new(Additive + m.Additive - 1, Multiplicative * m.Multiplicative, Flat + m.Flat, Base + m.Base);
        /// <summary>按比例缩放这份加成（1 = 不变）。</summary>
        public readonly Int32Modifier Scale(int scale)
            => new(1 + (Additive - 1) * scale, 1 + (Multiplicative - 1) * scale, Flat * scale, Base * scale);
        /// <summary><see cref="ApplyTo"/> 的逆运算：从加成后的值反推原值。</summary>
        public readonly int Undo(int currentValue) => (currentValue - Flat) / (Multiplicative * Additive) - Base;
    }
}
