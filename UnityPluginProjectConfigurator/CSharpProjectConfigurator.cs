using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace ShuHai.UnityPluginProjectConfigurator
{
    using XmlPropertyGroup = ProjectPropertyGroupElement;

    public static class CSharpProjectConfigurator
    {
        public static void SetupUnityPluginProject(CSharpProject project,
            Configs.UnityManagedPluginProject config, IEnumerable<string> fallbackVersions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            IEnumerable<string> versions = config.Versions;
            if (CollectionUtil.IsNullOrEmpty(versions) && config.UseFallbackVersionsIfNecessary)
                versions = fallbackVersions;
            if (CollectionUtil.IsNullOrEmpty(versions))
                throw new ArgumentException("Versions of target project is not determined.");

            var configurationGroups = project.ParseConfigurationPropertyGroups(null);
            if (config.RemoveExistedConfigurations)
            {
                foreach (var group in configurationGroups.Select(p => p.Value))
                    project.Xml.RemoveChild(group);
            }

            var addAfterMe = project.DefaultPropertyGroup;
            foreach (var ver in versions.Select(UnityVersion.Parse))
            {
                foreach (var type in EnumTraits<ProjectConfigurationType>.Values)
                {
                    SetupConfigurationGroupForUnity(
                        addAfterMe = project.CreatePropertyGroupAfter(addAfterMe), type, config.ForUnityEditor, ver);
                }
            }
        }

        private static void SetupConfigurationGroupForUnity(
            XmlPropertyGroup group, ProjectConfigurationType type, bool forUnityEditor, UnityVersion version)
        {
            string name = type.ToString();
            string configurationString = version != null ? $"{name}-{version.ToString(true)}" : name;
            group.Condition = $" '$(Configuration)|$(Platform)' == '{configurationString}|AnyCPU' ";

            bool isDebug = type == ProjectConfigurationType.Debug;
            if (isDebug)
                group.SetProperty("DebugSymbols", "true");

            group.SetProperty("DebugType", isDebug ? "full" : "pdbonly");
            group.SetProperty("Optimize", (!isDebug).ToString().ToLower());
            group.SetProperty("OutputPath", $@"bin\{configurationString}");
            group.SetProperty("DefineConstants", GetDefineConstantsForUnity(isDebug, forUnityEditor, version));
            group.SetProperty("ErrorReport", "prompt");
            group.SetProperty("WarningLevel", "4");
        }

        private static string GetDefineConstantsForUnity(bool isDebug, bool forUnityEditor, UnityVersion version)
        {
            var directives = new List<string> { "TRACE" };
            if (isDebug)
                directives.Add("DEBUG");
            if (forUnityEditor)
                directives.Add("UNITY_EDITOR");

            if (version != null)
            {
                if (version.Major == null)
                    throw new ArgumentException("Major version number is required.", nameof(version));

                // Unity-5.3 is the minimum unity version that defines X_X_OR_NEWER constants.
                var directiveVer = HistoricalUnityVersions.Unity_5_3;
                while (directiveVer != null && directiveVer <= version)
                {
                    directives.Add($"UNITY_{directiveVer.Cycle}_{directiveVer.Major.Value}_OR_NEWER");
                    if (!HistoricalUnityVersions.NextMajorVersion(directiveVer, out directiveVer))
                        break;
                }
                directives.Add($"UNITY_{version.Cycle}_{version.Major}");
                directives.Add($"UNITY_{version.Cycle}");
            }

            return string.Join(";", directives);
        }
    }
}