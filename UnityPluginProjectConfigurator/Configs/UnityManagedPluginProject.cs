using System.Collections.Generic;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    using VersionInfoDict = Dictionary<string, UnityManagedPluginProject.VersionInfo>;

    /// <summary>
    ///     Provides settings for configure a certain c# project.
    /// </summary>
    public class UnityManagedPluginProject
    {
        /// <summary>
        ///     Indicates whether the project is going to configure as an unity editor project.
        /// </summary>
        public bool ForEditor;

        /// <summary>
        ///     Indicates whether to remove existed configurations of the configuring c# project.
        /// </summary>
        public bool RemoveExistedConfigurations = true;

        /// <summary>
        ///     Collection of supported unity versions and corresponding information of the project to configure.
        ///     Version information in <see cref="UnityPlugins.DefaultVersions" /> is used if any version is missing in the
        ///     collection. Set the version information value to <see langword="null" /> to skip certain version.
        /// </summary>
        public VersionInfoDict Versions;

        public class VersionInfo
        {
            public AssemblyReference[] AssemblyReferences;
        }

        public class AssemblyReference
        {
            public string Name;

            public string Path;

            public string[] Environments = { "Runtime", "Editor" };
        }
    }
}