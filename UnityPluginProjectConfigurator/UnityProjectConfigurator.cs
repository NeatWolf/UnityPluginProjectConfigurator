using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CWDev.SLNTools.Core;
using Microsoft.Build.Construction;
using NLog;
using SolutionFile = CWDev.SLNTools.Core.SolutionFile;

namespace ShuHai.UnityPluginProjectConfigurator
{
    using XmlPropertyGroup = ProjectPropertyGroupElement;
    using ProjectConfigurationTypeTraits = EnumTraits<ProjectConfigurationType>;
    using SLNToolsProject = Project;
    using MSBuildProject = Microsoft.Build.Evaluation.Project;

    public class UnityProjectConfigurator
    {
        public readonly string ProjectName;
        public readonly DirectoryInfo ProjectDirectory;
        public UnityVersion ProjectVersion;

        public readonly SolutionFile SolutionFile;

        public UnityProjectConfigurator(string directory)
        {
            ProjectDirectory = new DirectoryInfo(directory);
            ProjectName = ProjectDirectory.Name;
            ProjectVersion = FindProjectVersion(ProjectDirectory);

            var slnPath = Path.Combine(ProjectDirectory.FullName, ProjectName + ".sln");
            SolutionFile = SolutionFile.FromFile(slnPath);
        }

        public void SaveSolution() { SolutionFile.Save(); }

        #region Configure

        public void AddCSharpProject(VSProject project, Configs.UnityProject.PluginProject config)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var newPath = Path.Combine(ProjectDirectory.FullName, Path.GetFileName(project.FilePath));
            project = VSProject.Clone(project, newPath, true);
            project.Save();

            if (config.AddToSolution)
            {
                var slnProj = AddProjectToSolutionFile(project);
                if (slnProj == null)
                {
                    ConsoleLogger.WriteLine(LogLevel.Warn, $@"Project ""{project.FilePath}"" skipped.");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(config.DllAssetDirectory))
                ConfigurePostBuildEvent(project, config);
        }

        #region Solution

        public SLNToolsProject AddProjectToSolutionFile(VSProject project)
        {
            var projectGuid = $"{{{project.Guid.ToString().ToUpper()}}}";
            var existedProj = SolutionFile.Projects.FirstOrDefault(
                p => p.ProjectGuid.Equals(projectGuid, StringComparison.OrdinalIgnoreCase));
            if (existedProj != null)
                return existedProj;

            var projectConfigurationGroups = SelectPropertyGroupsForSolution(project);

            var solutionConfigurations = SolutionFile.GlobalSections
                .First(s => s.Name == "SolutionConfigurationPlatforms").PropertyLines;
            string slnDebugCfg = solutionConfigurations.First(p => p.Name.Contains("Debug")).Value;
            string slnReleaseCfg = solutionConfigurations.First(p => p.Name.Contains("Release")).Value;

            var projectConfigurations = new List<PropertyLine>();
            foreach (var p in projectConfigurationGroups)
            {
                var conditions = p.Key;

                var configuration = conditions[VSProject.ConditionNames.Configuration];
                string slnCfg = null;
                if (configuration.Contains("Debug"))
                    slnCfg = slnDebugCfg;
                else if (configuration.Contains("Release"))
                    slnCfg = slnReleaseCfg;
                if (slnCfg == null)
                    throw new ArgumentException("Appropriate project configuration for solution not found.");

                var valuesText = conditions.ValuesString;
                valuesText = valuesText.Replace("AnyCPU", "Any CPU");
                projectConfigurations.Add(new PropertyLine(
                    $@"{slnCfg}.ActiveCfg", valuesText));
                projectConfigurations.Add(new PropertyLine(
                    $@"{slnCfg}.Build.0", valuesText));
            }

            var slnProj = new SLNToolsProject(SolutionFile,
                projectGuid,
                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", // C# project type GUID.
                project.Name,
                PathEx.MakeRelativePath(SolutionFile.SolutionFullPath, project.FilePath),
                null, null, null, projectConfigurations);

            SolutionFile.Projects.Add(slnProj);
            return slnProj;
        }

        private IEnumerable<KeyValuePair<VSProject.Conditions, XmlPropertyGroup>>
            SelectPropertyGroupsForSolution(VSProject project)
        {
            return project.ParseConditionalPropertyGroups(
                c =>
                {
                    var configurationValue = c[VSProject.ConditionNames.Configuration];
                    var version = VersionOfConfiguration(configurationValue);
                    return version != null
                        ? version.MajorEquals(ProjectVersion)
                        : ProjectConfigurationTypeTraits.Names.Contains(configurationValue);
                });
        }

        private static readonly Regex configurationRegex = new Regex(@"(?<cfg>\w+)-(?<ver>.+)");

        private static UnityVersion VersionOfConfiguration(string configuration)
        {
            var match = configurationRegex.Match(configuration);
            return !match.Success ? null : UnityVersion.Parse(match.Groups["ver"].Value);
        }

        #endregion Solution

        #region Build Event

        private void ConfigurePostBuildEvent(VSProject project, Configs.UnityProject.PluginProject config)
        {
            var dllAssetDirectory = config.DllAssetDirectory;
            if (string.IsNullOrEmpty(dllAssetDirectory))
                return;

            if (!dllAssetDirectory.StartsWith("Assets"))
                throw new ArgumentException("Unity asset path expected.", nameof(dllAssetDirectory));

            var assetDir = Path.Combine(ProjectDirectory.FullName, dllAssetDirectory);
            assetDir = assetDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            var copyCmd = $@"xcopy ""$(TargetDir)$(TargetName).*"" ""{assetDir}"" /i /y";
            if (!config.CreateDllAssetDirectoryIfNecessary)
                copyCmd = $@"if exist ""{assetDir}"" ({copyCmd})";

            // The approach below doesn't work since it add the property to existing PropertyGroup such as the
            // PropertyGroup that contains ProjectGuid and AssemblyName, this is usually the first PropertyGroup in
            // the .csproj file, and MSBuild evaluate $(TargetDir) and $(TargetName) as empty string. To ensure the
            // macros available, we need to add the PostBuildEvent property after
            // <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />.
            //project.MSBuildProject.SetProperty("PostBuildEvent", copyCmd);

            SetBuildEvent(project, "PostBuildEvent", copyCmd);
        }

        private static void SetBuildEvent(VSProject project, string name, string value)
        {
            // Remove old build event property if existed.
            if (project.FindProperty(name, out var group, out var property))
            {
                group.RemoveChild(property);
                if (group.Count == 0)
                    project.Xml.RemoveChild(group);
            }

            // Add new build event property.
            var buildEventGroup = project.CreatePropertyGroupAfter(project.MSBuildToolsImport);
            buildEventGroup.AddProperty(name, value);
        }

        #endregion Build Event

        #endregion Configure

        #region Find Version

        private static readonly Regex UnityVersionRegex = new Regex(@"m_EditorVersion: (?<ver>\d+\.\d+\.*\d*\w*\d*)\Z");

        private static UnityVersion FindProjectVersion(DirectoryInfo unityProjectDir)
        {
            var versionPath = Path.Combine(unityProjectDir.FullName, "ProjectSettings", "ProjectVersion.txt");
            var versionText = File.ReadAllText(versionPath);

            var match = UnityVersionRegex.Match(versionText);
            if (!match.Success)
                throw new ConfigParseException("Failed to parse unity version from ProjectVersion.txt.");

            var ver = match.Groups["ver"].Value;
            return UnityVersion.Parse(ver);
        }

        #endregion Find Version
    }
}