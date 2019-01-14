using System.Collections.Generic;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    using ProjectDict = Dictionary<string, UnityManagedPluginProject>;

    public sealed class UnityPlugins
    {
        /// <summary>
        ///     List of projects that need to configure as plugin project for unity.
        /// </summary>
        public ProjectDict ManagedProjects = new ProjectDict
        {
            ["Full path to your .csproj file."] = new UnityManagedPluginProject
            {
                Versions = new[] { "5.6", "2017.1", "2017.2", "2017.3", "2017.4", "2018.1", "2018.2", "2018.3" }
            },
            ["Full path to your another .csproj file."] = new UnityManagedPluginProject
            {
                ForUnityEditor = true
            }
        };

        /// <summary>
        ///     Fallbcak versions for <see cref="ManagedProjects" />.
        ///     Only applied when <see cref="UnityManagedPluginProject.Versions" /> field of
        ///     <see cref="UnityManagedPluginProject" /> is <see langword="null" /> or empty.
        /// </summary>
        public string[] FallbackVersions =
        {
            "5.3", "5.4", "5.5", "5.6",
            "2017.1", "2017.2", "2017.3", "2017.4",
            "2018.1", "2018.2", "2018.3"
        };
    }
}