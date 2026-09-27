using System.Numerics;

namespace EternalLib
{
    /// <summary>
    /// 描述一个有界的数值类型：定义其数据类型、最小值和最大值。
    /// </summary>
    /// <typeparam name="T">底层数值类型。</typeparam>
    public interface IBounded<out T> where T : struct, INumber<T>
    {
        /// <summary>该类型允许的最小值（含）。</summary>
        static abstract T MinValue { get; }

        /// <summary>该类型允许的最大值（含）。</summary>
        static abstract T MaxValue { get; }
    }
    /// <summary>
    /// 一个把底层数值钳制到 [<typeparamref name="TBounds"/>.MinValue, MaxValue] 的包装器。
    /// </summary>
    /// <typeparam name="T">底层数值类型。</typeparam>
    /// <typeparam name="TBounds">提供边界的类型。</typeparam>
    public readonly record struct Bounded<T, TBounds> where T : struct, INumber<T> where TBounds : struct, IBounded<T>
    {
        /// <summary>构造时暂不钳制，读取时实时钳制。</summary>
        public Bounded(T value) => Value = value;
        /// <summary>经钳制后的有效值。</summary>
        public T Value => T.Clamp(field, TBounds.MinValue, TBounds.MaxValue);
        /// <summary>从底层数值隐式构造。</summary>
        public static implicit operator Bounded<T, TBounds>(T value) => new(value);
        /// <summary>隐式转换回底层数值（自动钳制）。</summary>
        public static implicit operator T(Bounded<T, TBounds> bounded) => bounded.Value;
    }
}