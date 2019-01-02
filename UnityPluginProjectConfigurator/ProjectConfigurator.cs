using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using ShuHai.UnityPluginProjectConfigurator.Configs;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public class ProjectConfigurator
    {
        public static void Configure(CSharpProject config, IEnumerable<UnityVersion> fallbackVersions)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var cfgVers = config.Versions;
            var versions = cfgVers == null || cfgVers.Length == 0
                ? fallbackVersions
                : cfgVers.Select(UnityVersion.Parse);
            Configure(config.Path, config.IsEditor, versions);
        }

        public static void Configure(string projectPath, bool isEditor, IEnumerable<UnityVersion> versions)
        {
            if (projectPath == null)
                throw new ArgumentNullException(nameof(projectPath));

            var project = new Project(projectPath);
            Configure(project, isEditor, versions);
            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
        }

        public static void Configure(Project project, bool isEditor, IEnumerable<UnityVersion> versions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            var xml = project.Xml;

            // Remove existed configuration property groups.
            var configurationGroups = xml.PropertyGroups.Where(g => !string.IsNullOrEmpty(g.Condition)).ToArray();
            foreach (var group in configurationGroups)
                xml.RemoveChild(group);

            foreach (var ver in versions)
            {
                ConfigureGroup(xml.AddPropertyGroup(), true, isEditor, ver);
                ConfigureGroup(xml.AddPropertyGroup(), false, isEditor, ver);
            }

            project.ReevaluateIfNecessary();
            project.Save();
        }

        private static void ConfigureGroup(ProjectPropertyGroupElement group,
            bool isDebug, bool isEditor, UnityVersion version)
        {
            string conf = isDebug ? "Debug" : "Release";
            string configurationString = version != null ? $"{conf}-{version.ToString(true)}" : conf;
            group.Condition = $" '$(Configuration)|$(Platform)' == '{configurationString}|AnyCPU' ";

            if (isDebug)
                group.SetProperty("DebugSymbols", "true");

            group.SetProperty("DebugType", isDebug ? "full" : "pdbonly");
            group.SetProperty("Optimize", (!isDebug).ToString().ToLower());
            group.SetProperty("OutputPath", $@"bin\{configurationString}");
            group.SetProperty("DefineConstants", GetDefineConstants(isDebug, isEditor, version));
            group.SetProperty("ErrorReport", "prompt");
            group.SetProperty("WarningLevel", "4");
        }

        private static string GetDefineConstants(bool isDebug, bool isEditor, UnityVersion version)
        {
            var directives = new List<string> { "TRACE" };
            if (isDebug)
                directives.Add("DEBUG");
            if (isEditor)
                directives.Add("UNITY_EDITOR");

            if (version != null)
            {
                if (version.Major == null)
                    throw new ArgumentException("Major version number is required.", nameof(version));

                var directiveVer = UnityVersion.Unity_5_3;
                while (directiveVer != null && directiveVer <= version)
                {
                    directives.Add($"UNITY_{directiveVer.Cycle}_{directiveVer.Major.Value}_OR_NEWER");
                    directiveVer.NextMajorVersion(out directiveVer);
                }
                directives.Add($"UNITY_{version.Cycle}_{version.Major}");
                directives.Add($"UNITY_{version.Cycle}");
            }

            return string.Join(";", directives);
        }
    }
}