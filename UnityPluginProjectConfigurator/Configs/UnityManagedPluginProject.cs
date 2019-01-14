namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    /// <summary>
    ///     Provides settings for configure a certain c# project.
    /// </summary>
    public class UnityManagedPluginProject
    {
        /// <summary>
        ///     Indicates whether the project is going to configure as an unity editor project.
        /// </summary>
        public bool ForUnityEditor;

        /// <summary>
        ///     Indicates whether to remove existed configurations of the configuring c# project.
        /// </summary>
        public bool RemoveExistedConfigurations = true;

        /// <summary>
        ///     List of supported unity versions of the project to configure.
        /// </summary>
        public string[] Versions;

        /// <summary>
        ///     Indicates whether to use <see cref="UnityPlugins.FallbackVersions" /> as fallback when <see cref="Versions" /> is
        ///     <see langword="null" /> or empty.
        /// </summary>
        public bool UseFallbackVersionsIfNecessary = true;
    }
}