using System;
using System.Collections.Generic;
using CommandLine;

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
                LoggedConsole.WriteError(e);
                errorOccus = true;
            }
            finally
            {
                VSProject.UnloadAll();
            }
            LoggedConsole.WriteInfo(errorOccus ? "Error occurred..." : "Done!");
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
                LoggedConsole.WriteInfo($@"Configure c# project '{projPath}'.");

                var vsproj = VSProject.GetOrLoad(projPath);
                if (vsproj == null)
                {
                    LoggedConsole.WriteWarn($"Failed to load project at '{projPath}', skipped.");
                    continue;
                }

                var parameter = CSharpProjectConfigurator.ParseUnityPluginParameter(config, kvp.Value);
                CSharpProjectConfigurator.SetupUnityPluginProject(vsproj, parameter);
            }
        }

        private static void ConfigureUnityProjects(IReadOnlyDictionary<string, Configs.UnityProject> configs)
        {
            foreach (var kvp in configs)
            {
                var path = kvp.Key;
                var config = kvp.Value;

                LoggedConsole.WriteInfo($"Configure unity project: '{path}'.");

                var configurator = new UnityProjectConfigurator(path);
                if (configurator.SolutionFile == null)
                {
                    LoggedConsole.WriteWarn(
                        $@"Solution file of unity project ""{config}"" not found, configure skipped.");
                    continue;
                }

                foreach (var doneKvp in configurator.SetupCSharpProjects(config.PluginProjects))
                {
                    var slnProj = doneKvp.Value;
                    if (slnProj == null)
                        LoggedConsole.WriteWarn($@"Failed to add c# project '{doneKvp.Key}' to Unity solution.");
                }
            }
        }

        private static void SaveProjects(IEnumerable<VSProject> projects)
        {
            foreach (var proj in projects)
            {
                LoggedConsole.WriteInfo($"Save project '{proj.FilePath}'");
                proj.Save();
            }
        }

        #endregion Run

        #region Errors

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            LoggedConsole.WriteInfo("Error occurs when parsing parameters:\n");
            foreach (var err in errors)
                LoggedConsole.WriteError(err);
        }

        #endregion Errors
    }
}