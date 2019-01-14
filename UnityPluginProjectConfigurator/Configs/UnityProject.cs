using System.Collections.Generic;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    using PluginProjectDict = Dictionary<string, UnityProject.PluginProject>;

    /// <summary>
    ///     Provides settings for configure a certain Unity project.
    /// </summary>
    public class UnityProject
    {
        /// <summary>
        ///     Mapping from c# project file path to its configs as an unity plugin project.
        /// </summary>
        public PluginProjectDict PluginProjects;

        public class PluginProject
        {
            public bool AddToSolution = true;

            public bool CopyDllAsset = true;

            public string DllAssetDirectory;

            public bool CreateDllAssetDirectoryIfNecessary = true;
        }
    }
}