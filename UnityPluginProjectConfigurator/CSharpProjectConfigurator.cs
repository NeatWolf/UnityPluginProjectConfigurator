using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public static class CSharpProjectConfigurator
    {
        public static void ConfigureVersions(CSharpProject project, bool isEditor, IEnumerable<UnityVersion> versions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (!versions.Any())
                return;

            var xml = project.Xml;

            // Remove existed configuration property groups.
            var configurationGroups = project.FindPropertyGroups(
                g => g.Condition.Contains("$(Configuration)|$(Platform)"));
            foreach (var group in configurationGroups)
                xml.RemoveChild(group);

            // Add new configuration property group for each version.
            ProjectPropertyGroupElement addAfterMe = project.DefaultPropertyGroup;
            foreach (var ver in versions)
            {
                ConfigureGroup(addAfterMe = project.CreatePropertyGroupAfter(addAfterMe), true, isEditor, ver);
                ConfigureGroup(addAfterMe = project.CreatePropertyGroupAfter(addAfterMe), false, isEditor, ver);
            }
        }

        private static void ConfigureGroup(
            ProjectPropertyGroupElement group, bool isDebug, bool isEditor, UnityVersion version)
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