using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    using UnityProjectDict = Dictionary<string, UnityProject>;

    public class Config
    {
        /// <summary>
        ///     Settings for c# projects that need to configure as unity plugin projects.
        /// </summary>
        public UnityPlugins UnityPlugins = new UnityPlugins();

        /// <summary>
        ///     Unity projects that need to configure.
        ///     The configuration is only applied if there is any solution file(.sln) in the directory specified by "Path"
        ///     property.
        ///     Projects listed in this config file will be added to the unity generated c# solution.
        /// </summary>
        public UnityProjectDict UnityProjects;

        #region Template

        public static Config CreateTemplate()
        {
            var unityProjects = new UnityProjectDict
            {
                ["Full path to root directory of unity project"] = new UnityProject
                {
                    PluginProjects = new Dictionary<string, UnityProject.PluginProject>
                    {
                        ["Full path to plugin project."] = new UnityProject.PluginProject
                        {
                            Configurations = new Dictionary<string, string>
                            {
                                ["Debug"] = "Debug-2017.4",
                                ["Release"] = "Release-2017.4"
                            },
                            DllAssetDirectory = @"Assets/Example/Assemblies"
                        },
                        ["Full path to c# project."] = new UnityProject.PluginProject
                        {
                            Configurations = new Dictionary<string, string>
                            {
                                ["Debug"] = "Debug",
                                ["Release"] = "Release"
                            },
                            DllAssetDirectory = @"Assets/Example/Assemblies"
                        }
                    }
                },
                ["Full path to root directory of another unity project"] = new UnityProject
                {
                    PluginProjects = new Dictionary<string, UnityProject.PluginProject>
                    {
                        ["Full path to plugin project."] = new UnityProject.PluginProject
                        {
                            DllAssetDirectory = @"Assets/Example/Assemblies/Editor",
                        }
                    }
                }
            };

            return new Config
            {
                UnityPlugins = UnityPlugins.CreateTemplate(),
                UnityProjects = unityProjects
            };
        }

        #endregion Template

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