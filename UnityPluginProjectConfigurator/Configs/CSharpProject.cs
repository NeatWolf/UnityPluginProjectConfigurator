namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    /// <summary>
    ///     Provides settings for configure a certain c# project.
    /// </summary>
    public class CSharpProject
    {
        /// <summary>
        ///     Full path of the c# project file(.csproj) to be configure.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        ///     Indicates whether the project is going to configure as an unity editor project.
        /// </summary>
        public bool IsEditor { get; set; }

        /// <summary>
        ///     List of supported unity versions of the project to configure. <see cref="Config.DefaultVersions" /> is used if it
        ///     is <see langword="null" /> or empty.
        /// </summary>
        public string[] Versions { get; set; }
    }
}