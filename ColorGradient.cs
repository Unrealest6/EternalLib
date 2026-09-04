namespace EternalLib
{
    public enum GradientDirection
    {
        Left,
        Right
    }
    public sealed class ColorGradient
    {
        public static Dictionary<string, ColorGradient> Gradients { get; } = [];
        public Color[] Colors { get; }
        public string[] HexColors { get; }
        public double MillisecondsPerColor { get; }
        public GradientDirection Direction { get; }
        public static bool Register(string key, Color[]? colors, double millisecondsPerColor = 1000.0 / 60.0, GradientDirection direction = GradientDirection.Left)
        {
            if (Gradients.ContainsKey(key))
            {
                return false;
            }
            Gradients[key] = new ColorGradient(colors, millisecondsPerColor, direction);
            return true;
        }
        private ColorGradient(Color[]? colors, double millisecondsPerColor = 1000.0 / 60.0, GradientDirection direction = GradientDirection.Left)
        {
            Colors = colors ?? [Color.White];
            MillisecondsPerColor = Math.Max(0.001, millisecondsPerColor);
            Direction = direction;
            HexColors = new string[Colors.Length];
            for (int i = 0; i < Colors.Length; i++)
            {
                Color c = Colors[i];
                HexColors[i] = c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            }
        }
    }
}