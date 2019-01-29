using NLog;

namespace ShuHai.UnityPluginProjectConfigurator
{
    internal static class LoggedConsole
    {
        public static readonly Logger Logger;


        public static void WriteInfo(string value) { Logger.Log(LogLevel.Info, value); }
        public static void WriteInfo(object value) { Logger.Log(LogLevel.Info, value); }

        public static void WriteWarn(string value) { Logger.Log(LogLevel.Warn, value); }
        public static void WriteWarn(object value) { Logger.Log(LogLevel.Warn, value); }

        public static void WriteError(string value) { Logger.Log(LogLevel.Error, value); }
        public static void WriteError(object value) { Logger.Log(LogLevel.Error, value); }

        static LoggedConsole()
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