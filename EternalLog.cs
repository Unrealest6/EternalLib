namespace EternalLib
{
    /// <summary>
    /// EternalLib 内部日志入口。模组未加载时退化为控制台输出，避免日志本身抛异常。
    /// </summary>
    internal static class EternalLog
    {
        private const string Prefix = "[EternalLib] ";
        internal static void Info(string message) => Write(message, 0);
        internal static void Warn(string message) => Write(message, 1);
        internal static void Error(string message) => Write(message, 2);
        private static void Write(string message, int level)
        {
            try
            {
                Mod? mod = ModContent.GetInstance<EternalLib>();
                if (mod is not null)
                {
                    switch (level)
                    {
                        case 1:
                            mod.Logger.Warn(message);
                            return;
                        case 2:
                            mod.Logger.Error(message);
                            return;
                        default:
                            mod.Logger.Info(message);
                            return;
                    }
                }
            }
            catch (Exception)
            {
                // 模组未加载 / 已卸载：落到控制台。
            }
            Console.WriteLine(Prefix + message);
        }
    }
}