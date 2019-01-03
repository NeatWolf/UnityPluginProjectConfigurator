using NLog;

namespace ShuHai.UnityPluginProjectConfigurator
{
    internal static class ConsoleLogger
    {
        public static readonly Logger Logger;

        public static LogLevel LogLevel = LogLevel.Info;

        public static void WriteLine(string value) { Logger.Log(LogLevel, value); }
        public static void WriteLine(LogLevel logLevel, string value) { Logger.Log(logLevel, value); }

        public static void WriteLine(object value) { Logger.Log(LogLevel, value); }
        public static void WriteLine(LogLevel logLevel, object value) { Logger.Log(logLevel, value); }

        static ConsoleLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var layout = NLog.Layouts.Layout.FromString("${level}|${message}");
            var logfile = new NLog.Targets.FileTarget
            {
                FileName = "console.log",
                Layout = layout,
                DeleteOldFileOnStartup = true
            };
            var logconsole = new NLog.Targets.ConsoleTarget { Layout = layout };

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            LogManager.Configuration = config;

            Logger = LogManager.GetCurrentClassLogger();
        }
    }
}