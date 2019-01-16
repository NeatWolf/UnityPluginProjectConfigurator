using System;
using System.Collections.Generic;
using CommandLine;
using NLog;

namespace ShuHai.UnityPluginProjectConfigurator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineOptions>(args)
#if DEBUG
                .WithParsed(Run)
#else
                .WithParsed(TryRun)
#endif
                .WithNotParsed(HandleErrors);

            Console.Read();
        }

        #region Run

        private static void Run(CommandLineOptions options)
        {
            RunImpl(options);
            VSProject.UnloadAll();
        }

        private static void TryRun(CommandLineOptions options)
        {
            var errorOccus = false;
            try
            {
                RunImpl(options);
            }
            catch (Exception e)
            {
                ConsoleLogger.WriteLine(LogLevel.Error, e);
                errorOccus = true;
            }
            finally
            {
                VSProject.UnloadAll();
            }
            ConsoleLogger.WriteLine(errorOccus ? "Error occurred..." : "Done!");
        }

        private static void RunImpl(CommandLineOptions options)
        {
            //Configs.Config.Save("TemplateConfig.json", Configs.Config.CreateTemplate());
            var config = Configs.Config.Load(options.ConfigPath);

            ConfigureUnityPlugins(config.UnityPlugins);
            ConfigureUnityProjects(config.UnityProjects);

            SaveProjects(VSProject.Instances);
        }

        private static void ConfigureUnityPlugins(Configs.UnityPlugins config)
        {
            foreach (var kvp in config.ManagedProjects)
            {
                var projPath = kvp.Key;
                ConsoleLogger.WriteLine($@"Configure c# project '{projPath}'.");

                var parameter = CSharpProjectConfigurator.ParseUnityPluginParameter(config, kvp.Value);
                CSharpProjectConfigurator.SetupUnityPluginProject(VSProject.GetOrLoad(projPath), parameter);
            }
        }

        private static void ConfigureUnityProjects(IReadOnlyDictionary<string, Configs.UnityProject> configs)
        {
            foreach (var ukvp in configs)
            {
                var uprojPath = ukvp.Key;
                var uprojCfg = ukvp.Value;

                ConsoleLogger.WriteLine($"Configure unity project: '{uprojPath}'.");

                var configurator = new UnityProjectConfigurator(uprojPath);
                foreach (var pkvp in uprojCfg.PluginProjects)
                    configurator.AddCSharpProject(VSProject.GetOrLoad(pkvp.Key), pkvp.Value);
                configurator.SaveSolution();
            }
        }

        private static void SaveProjects(IEnumerable<VSProject> projects)
        {
            foreach (var proj in projects)
            {
                ConsoleLogger.WriteLine($"Save project '{proj.FilePath}'");
                proj.Save();
            }
        }

        #endregion Run

        #region Errors

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            ConsoleLogger.WriteLine("Error occurs when parsing parameters:\n");
            foreach (var err in errors)
                ConsoleLogger.WriteLine(LogLevel.Error, err);
        }

        #endregion Errors
    }
}