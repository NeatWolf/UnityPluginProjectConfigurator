using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    public class Config
    {
        /// <summary>
        ///     List of projects that need to configure.
        /// </summary>
        public Dictionary<string, CSharpProject> CSharpProjects { get; set; } = new Dictionary<string, CSharpProject>
        {
            ["RuntimeProject"] = new CSharpProject
            {
                Path = "Full path to your .csproj file.",
                Versions = new[] { "5.6", "2017.1", "2017.2", "2017.3", "2017.4", "2018.1", "2018.2", "2018.3" }
            },
            ["EditorProject"] = new CSharpProject
            {
                Path = "Full path to your .csproj file."
            }
        };

        /// <summary>
        ///     Unity projects that need to configure.
        ///     The configuration is only applied if there is any solution file(.sln) in the directory specified by "Path"
        ///     property.
        ///     Projects listed in this config file will be added to the unity generated c# solution.
        /// </summary>
        public UnityProject[] UnityProjects { get; set; } =
        {
            new UnityProject
            {
                Path = "Full path to root directory of unity project",
                CSharpProjects = new[]
                {
                    new UnityProject.CSharpProject
                    {
                        Key = "RuntimeProject",
                        DllAssetDirectory = @"Assets/Example/Assemblies"
                    }
                }
            },
            new UnityProject
            {
                Path = "Full path to root directory of another unity project",
                CSharpProjects = new[]
                {
                    new UnityProject.CSharpProject
                    {
                        Key = "EditorProject",
                        DllAssetDirectory = @"Assets/Example/Assemblies/Editor"
                    }
                }
            }
        };

        /// <summary>
        ///     Supported versions to configure on target projects.
        ///     More specifically, project configuration "Debug-[version]" and "Release-[version]" of each version will be added to
        ///     target projects if it dosen't exist or overwritten if already existed.
        ///     This configuration is applied if Versions property of [CSharpProjects.[ProjectKey]] is not set.
        /// </summary>
        public string[] DefaultVersions { get; set; } =
        {
            "5.3", "5.4", "5.5", "5.6",
            "2017.1", "2017.2", "2017.3", "2017.4",
            "2018.1", "2018.2", "2018.3"
        };

        #region Persistency

        public const string DefaultFileName = "config.json";

        public static Config Load(string path = DefaultFileName)
        {
            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Config>(text, loadSettings);
        }

        private static readonly JsonSerializerSettings loadSettings =
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        public static void Save(Config config) { Save(DefaultFileName, config); }

        public static void Save(string path, Config config)
        {
            var text = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, text);
        }

        #endregion Persistency
    }
}