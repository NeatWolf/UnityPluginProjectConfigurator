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
        ///     Indicates whether to configure the plugin project for targeting multiple unity versions. Multiple configurations
        ///     for the plugin project will be setup base on <see cref="Versions" />  or
        ///     <see cref="UnityPlugins.DefaultVersions" /> and existed configurations of the plugin project will be removed if the
        ///     value is set to <see langword="true" />; otherwise, configurations of the plugin project remains if the value is
        ///     set to <see langword="false" />.
        /// </summary>
        public bool ForMultipleVersions = true;

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