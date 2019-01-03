using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using Microsoft.Build.Evaluation;

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
            var projects = new ProjectCollection();
            try
            {
                var config = Configs.Config.Load(options.ConfigPath);
                var projectDict = CreateCSharpProjectDict(projects, config.CSharpProjects, config.DefaultVersions);

                ConfigureCSharpProjects(projectDict);
                ConfigureUnityProjects(config.UnityProjects, projectDict);

                SaveProjects(projectDict.Values.Select(i => i.Project));
            }
            catch (Exception e)
            {
                ConsoleLogger.WriteLine(e);
#if DEBUG
                throw;
#endif
            }
            finally
            {
                projects.Dispose();
            }
            ConsoleLogger.WriteLine("Done!");
        }

        private static void ConfigureCSharpProjects(IReadOnlyDictionary<string, CSharpProject> projectDict)
        {
            foreach (var kvp in projectDict)
            {
                var proj = kvp.Value;
                ConsoleLogger.WriteLine($@"Configure c# project '{proj.Project.FullPath}'.");
                CSharpProjectConfigurator.ConfigureVersions(proj, proj.IsEditor, proj.Versions);
            }
        }

        private static void ConfigureUnityProjects(
            IEnumerable<Configs.UnityProject> unityProjectConfigs,
            IReadOnlyDictionary<string, CSharpProject> candidateCSharpProjectDict)
        {
            foreach (var uproj in unityProjectConfigs)
            {
                ConsoleLogger.WriteLine($"Configure unity project: '{uproj.Path}'.");

                using (var configurator = new UnityProjectConfigurator(uproj.Path))
                {
                    foreach (var csProjCfg in uproj.CSharpProjects)
                    {
                        var projKey = csProjCfg.Key;
                        if (!candidateCSharpProjectDict.TryGetValue(projKey, out var proj))
                        {
                            ConsoleLogger.WriteLine($"Error: Unable to find c# project with key '{projKey}'.");
                            continue;
                        }
                        configurator.AddCSharpProject(proj, csProjCfg);
                    }
                }
            }
        }

        private static void SaveProjects(IEnumerable<Project> projects)
        {
            foreach (var proj in projects)
            {
                ConsoleLogger.WriteLine($"Save project '{proj.FullPath}'");
                proj.ReevaluateIfNecessary();
                proj.Save();
            }
        }

        #region Project

        private static IReadOnlyDictionary<string, CSharpProject> CreateCSharpProjectDict(
            ProjectCollection projectCollection,
            IReadOnlyDictionary<string, Configs.CSharpProject> projectConfigs, IEnumerable<string> fallbackVersions)
        {
            var dict = new Dictionary<string, CSharpProject>();
            foreach (var kvp in projectConfigs)
            {
                var projCfg = kvp.Value;
                var projPath = projCfg.Path;

                ConsoleLogger.WriteLine($"Load project '{projPath}'");
                var proj = projectCollection.LoadProject(projPath);

                IEnumerable<string> versions = Array.Empty<string>();
                var cfgVers = projCfg.Versions;
                if ((cfgVers == null || cfgVers.Length == 0) && projCfg.DefaultVersionsAsFallback)
                    versions = fallbackVersions;

                dict.Add(kvp.Key, new CSharpProject(proj, projCfg, versions));
            }
            return dict;
        }

        #endregion Project

        #endregion Run

        #region Errors

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            ConsoleLogger.WriteLine("Command Line Errors:\n");
            foreach (var err in errors)
                ConsoleLogger.WriteLine(err);
        }

        #endregion Errors
    }
}