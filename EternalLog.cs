namespace EternalLib
{
    /// <summary>
    /// 通用日志工具。自动识别调用方所属模组，并使用该模组的 Logger 输出。
    /// 若无法识别（例如由 EternalLib 自己调用），则回退到 EternalLib 的 Logger。
    /// </summary>
    public static class EternalLog
    {
        /// <summary>
        /// 日志级别。
        /// </summary>
        private enum LogLevel : byte
        {
            Info,
            Warn,
            Error
        }
        /// <summary>
        /// 输出信息级日志。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Info(string message) => Write(message, LogLevel.Info, Assembly.GetCallingAssembly());
        /// <summary>
        /// 输出警告级日志。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Warn(string message) => Write(message, LogLevel.Warn, Assembly.GetCallingAssembly());
        /// <summary>
        /// 输出错误级日志。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Error(string message) => Write(message, LogLevel.Error, Assembly.GetCallingAssembly());
        /// <summary>
        /// 实际写出日志。优先使用调用方模组的 Logger，失败时回退到 EternalLib 或控制台。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="level">日志级别。</param>
        /// <param name="assembly">调用方程序集。</param>
        private static void Write(string message, LogLevel level, Assembly assembly)
        {
            try
            {
                Mod? mod = ModLoader.Mods.FirstOrDefault(mod => mod.Code == assembly) ?? ModContent.GetInstance<EternalLib>();
                if (mod is not null)
                {
                    switch (level)
                    {
                        case LogLevel.Warn:
                            mod.Logger.Warn(message);
                            return;
                        case LogLevel.Error:
                            mod.Logger.Error(message);
                            return;
                        case LogLevel.Info:
                        default:
                            mod.Logger.Info(message);
                            return;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("An error occurred in the log!");
            }
            Console.WriteLine("[Unknown] " + message);
        }
    }
}