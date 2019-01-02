using System;
using System.Collections.Generic;
using System.IO;
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
                var projectInfoDict = CreateProjectInfoDict(projects, config.CSharpProjects, config.DefaultVersions);
                ConfigureCSharpProjects(projectInfoDict);
                ConfigureUnityProjects(config.UnityProjects, projectInfoDict);

                foreach (var proj in projectInfoDict.Values.Select(i => i.Project))
                {
                    Console.WriteLine($"Save project '{proj.FullPath}'");
                    proj.ReevaluateIfNecessary();
                    proj.Save();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
#if DEBUG
                throw;
#endif
            }
            finally
            {
                projects.Dispose();
            }
            Console.WriteLine("Done!");
        }

        private static void ConfigureCSharpProjects(IReadOnlyDictionary<string, ProjectInfo> projectInfoDict)
        {
            foreach (var kvp in projectInfoDict)
            {
                var info = kvp.Value;
                Console.WriteLine($@"Configure c# project '{info.Project.FullPath}'.");
                ProjectConfigurator.Configure(info.Project, info.IsEditor, info.Versions);
            }
        }

        private static void ConfigureUnityProjects(
            IEnumerable<Configs.UnityProject> unityProjectConfigs,
            IReadOnlyDictionary<string, ProjectInfo> candidateCSharpProjectDict)
        {
            foreach (var uproj in unityProjectConfigs)
            {
                Console.WriteLine($"Configure unity project: '{uproj.Path}'.");

                using (var configurator = new UnityProjectConfigurator(uproj.Path))
                {
                    foreach (var csProjCfg in uproj.CSharpProjects)
                    {
                        var projKey = csProjCfg.Key;
                        if (!candidateCSharpProjectDict.TryGetValue(projKey, out var info))
                        {
                            Console.WriteLine($"Error: Unable to find c# project with key '{projKey}'.");
                            continue;
                        }
                        configurator.Configure(info.Project, info.IsEditor, csProjCfg.DllAssetDirectory);
                    }
                }
            }
        }

        #region ProjectInfo

        private static IReadOnlyDictionary<string, ProjectInfo> CreateProjectInfoDict(
            ProjectCollection projectCollection,
            IReadOnlyDictionary<string, Configs.CSharpProject> projectConfigs, IEnumerable<string> fallbackVersions)
        {
            var dict = new Dictionary<string, ProjectInfo>();
            foreach (var kvp in projectConfigs)
            {
                var projCfg = kvp.Value;
                var projPath = projCfg.Path;

                Console.WriteLine($"Load project '{projPath}'");
                var proj = projectCollection.LoadProject(projPath);

                var cfgVers = projCfg.Versions;
                var versions = cfgVers == null || cfgVers.Length == 0 ? fallbackVersions : cfgVers;

                dict.Add(kvp.Key, new ProjectInfo(proj, projCfg, versions));
            }
            return dict;
        }

        private sealed class ProjectInfo
        {
            public readonly Project Project;

            public readonly bool IsEditor;
            public readonly IReadOnlyList<UnityVersion> Versions;

            public ProjectInfo(Project project, Configs.CSharpProject config, IEnumerable<string> versions)
            {
                Project = project ?? throw new ArgumentNullException(nameof(project));
                IsEditor = config.IsEditor;
                Versions = versions.Select(UnityVersion.Parse).ToArray();
            }

            public void WritePropertiesToFile(string path)
            {
                using (var fs = new FileStream(path, FileMode.Create))
                using (var writer = new StreamWriter(fs))
                {
                    foreach (var prop in Project.Properties)
                        writer.WriteLine($"{prop.Name}: {prop.EvaluatedValue}");
                }
            }
        }

        #endregion ProjectInfo

        #endregion Run

        #region Errors

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            Console.WriteLine("Command Line Errors:\n");
            foreach (var err in errors)
                Console.WriteLine(err);
        }

        #endregion Errors
    }
}