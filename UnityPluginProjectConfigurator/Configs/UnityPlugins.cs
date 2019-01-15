using System.Collections.Generic;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    using ProjectDict = Dictionary<string, UnityManagedPluginProject>;
    using VersionInfoDict = Dictionary<string, UnityManagedPluginProject.VersionInfo>;
    using AssemblyReference = UnityManagedPluginProject.AssemblyReference;
    using VersionInfo = UnityManagedPluginProject.VersionInfo;

    public sealed class UnityPlugins
    {
        /// <summary>
        ///     List of projects that need to configure as plugin project for unity.
        /// </summary>
        public ProjectDict ManagedProjects;

        /// <summary>
        ///     Default versions information for <see cref="ManagedProjects" />.
        ///     Missing version infomation in <see cref="UnityManagedPluginProject.Versions" /> is going to be replaced by
        ///     corresponding version information in this collection.
        /// </summary>
        public VersionInfoDict DefaultVersions;

        #region Template

        public static UnityPlugins CreateTemplate()
        {
            var assemblyReferences = CreateAssemblyReferencesTemplate();

            var managedProjects = new ProjectDict
            {
                ["Full path to your .csproj file."] = new UnityManagedPluginProject
                {
                    Versions = new VersionInfoDict
                    {
                        ["5.6"] = new VersionInfo { AssemblyReferences = assemblyReferences },
                        ["2017.1"] = new VersionInfo { AssemblyReferences = assemblyReferences },
                        ["2017.4"] = new VersionInfo { AssemblyReferences = assemblyReferences }
                    }
                },
                ["Full path to your another .csproj file."] = new UnityManagedPluginProject
                {
                    ForEditor = true
                }
            };

            var fallbackVersions = new VersionInfoDict();
            var ver = HistoricalUnityVersions.Unity_5_3;
            do
            {
                fallbackVersions.Add(ver.ToString(true), new VersionInfo { AssemblyReferences = assemblyReferences });
            } while (HistoricalUnityVersions.NextMajorVersion(ver, out ver));

            return new UnityPlugins
            {
                ManagedProjects = managedProjects,
                DefaultVersions = fallbackVersions
            };
        }

        private static AssemblyReference[] CreateAssemblyReferencesTemplate()
        {
            return new[]
            {
                new AssemblyReference
                {
                    Path = "Unity Install Directory\\Editor\\Data\\Managed\\UnityEngine.dll",
                    Environments = UnityEnvironmentTypesConverter.ToStrings(UnityEnvironmentTypes.All)
                },
                new AssemblyReference
                {
                    Path = "Unity Install Directory\\Editor\\Data\\Managed\\UnityEditor.dll",
                    Environments = UnityEnvironmentTypesConverter.ToStrings(UnityEnvironmentTypes.Editor)
                }
            };
        }

        #endregion Template
    }
}