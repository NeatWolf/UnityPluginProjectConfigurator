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
                .WithParsed(Run)
                .WithNotParsed(HandleErrors);

            Console.Read();
        }

        #region Run

        private static void Run(CommandLineOptions options)
        {
#if DEBUG
            RunImpl(options);
#else
            TryRun(options);
#endif
        }

        private static void TryRun(CommandLineOptions options)
        {
            try
            {
                RunImpl(options);
            }
            catch (Exception e)
            {
                ConsoleLogger.WriteLine(LogLevel.Error, e);
            }
            finally
            {
                CSharpProject.UnloadAll();
            }
            ConsoleLogger.WriteLine("Done!");
        }

        private static void RunImpl(CommandLineOptions options)
        {
            //Configs.Config.Save("TemplateConfig.json", Configs.Config.CreateTemplate());
            var config = Configs.Config.Load(options.ConfigPath);

            ConfigureUnityPlugins(config.UnityPlugins);
            ConfigureUnityProjects(config.UnityProjects);

            SaveProjects(CSharpProject.Instances);
        }

        private static void ConfigureUnityPlugins(Configs.UnityPlugins config)
        {
            foreach (var kvp in config.ManagedProjects)
            {
                var projPath = kvp.Key;
                ConsoleLogger.WriteLine($@"Configure c# project '{projPath}'.");

                var parameter = CSharpProjectConfigurator.ParseUnityPluginParameter(config, kvp.Value);
                CSharpProjectConfigurator.SetupUnityPluginProject(CSharpProject.GetOrLoad(projPath), parameter);
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
                    configurator.AddCSharpProject(CSharpProject.GetOrLoad(pkvp.Key), pkvp.Value);
                configurator.SaveSolution();
            }
        }

        private static void SaveProjects(IEnumerable<CSharpProject> projects)
        {
            foreach (var proj in projects)
            {
                ConsoleLogger.WriteLine($"Save project '{proj.Path}'");
                proj.Save();
            }
        }

        #endregion Run

        #region Errors

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            ConsoleLogger.WriteLine("Command Line Errors:\n");
            foreach (var err in errors)
                ConsoleLogger.WriteLine(LogLevel.Error, err);
        }

        #endregion Errors
    }
}