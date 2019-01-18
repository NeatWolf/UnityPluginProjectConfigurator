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
    using XmlItemGroup = ProjectItemGroupElement;
    using ProjectConfigurationTypeTraits = EnumTraits<ProjectConfigurationType>;
    using SLNToolsProject = Project;

    public class UnityProjectConfigurator
    {
        public readonly string ProjectName;
        public readonly DirectoryInfo ProjectDirectory;
        public readonly UnityVersion ProjectVersion;

        public readonly SolutionFile SolutionFile;

        public UnityProjectConfigurator(string directory)
        {
            ProjectDirectory = new DirectoryInfo(directory);
            ProjectName = ProjectDirectory.Name;
            ProjectVersion = FindProjectVersion(ProjectDirectory);

            var slnPath = Path.Combine(ProjectDirectory.FullName, ProjectName + ".sln");
            if (File.Exists(slnPath))
                SolutionFile = SolutionFile.FromFile(slnPath);
        }

        public void SaveSolution() { SolutionFile.Save(); }

        #region Configure

        public void SetupCSharpProjects(IReadOnlyDictionary<string, Configs.UnityProject.PluginProject> configs)
        {
            foreach (var kvp in configs)
                SetupCSharpProject(VSProject.GetOrLoad(kvp.Key), kvp.Value);
            UpdateProjectReferences();

            SaveSolution();
        }

        public void SetupCSharpProject(VSProject project, Configs.UnityProject.PluginProject config)
        {
            if (SolutionFile == null)
                throw new InvalidOperationException("Solution file not found.");
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Clone a new project for modification for solution.
            var newPath = Path.Combine(ProjectDirectory.FullName, Path.GetFileName(project.FilePath));
            project = VSProject.Clone(project, newPath, true);

            // Select configuration groups of the project for solution.
            var targetConfigurations = SelectConfigurationsForSolution(project, config.Configurations);
            SelectConfigurationPropertyGroups(project, targetConfigurations,
                out var targetPropertyGroups, out var redundantPropertyGroups);
            SelectConfigurationItemGroups(project, targetConfigurations, out _, out var redundantItemGroups);

            // Remove redundant configuration groups.
            project.RemovePropertyGroups(redundantPropertyGroups);
            project.RemoveItemGroups(redundantItemGroups);

            // Setup configuration groups.
            SetupOutputPath(project, targetPropertyGroups);

            // Setup build events.
            SetupPostBuildEvent(project, config);

            project.Save();

            RemoveCSharpProject(newPath);
            var slnProj = AddProjectToSolutionFile(project, targetPropertyGroups);
            if (slnProj == null)
                ConsoleLogger.WriteLine(LogLevel.Warn, $@"Project ""{project.FilePath}"" skipped.");
        }

        public void UpdateProjectReferences()
        {
            var projects = SolutionFile.Projects;

            var originalToCloned = projects
                .Select(p => VSProject.GetOrLoad(p.FullPath))
                .Where(p => p.IsCloned)
                .ToDictionary(p => VSProject.GetOrLoad(p.CloneSourcePath));

            foreach (var slnProj in projects)
            {
                var vsProj = VSProject.Get(slnProj.FullPath);
                foreach (var projRefItem in vsProj.FindProjectReferences(null))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(vsProj.DirectoryPath, projRefItem.Include));
                    var refProj = VSProject.GetOrLoad(fullPath);
                    if (originalToCloned.TryGetValue(refProj, out var clonedProj))
                    {
                        projRefItem.Include = PathEx.MakeRelative(
                            ProjectDirectory.FullName + '\\', clonedProj.FilePath);
                        projRefItem.Metadata.First(m => m.ElementName == "Project").Value = clonedProj.GuidText;
                    }
                }
            }
        }

        public void RemoveCSharpProject(string path)
        {
            var projects = SolutionFile.Projects;
            var toRemove = projects.FirstOrDefault(p => PathEx.AreEqual(p.FullPath, path));
            if (toRemove != null)
                projects.Remove(toRemove);
        }

        #region Configuration Groups Selection

        private string[] SelectConfigurationsForSolution(VSProject project, IReadOnlyDictionary<string, string> config)
        {
            var configurations = project.MSBuildProject.ConditionedProperties[VSProject.ConditionNames.Configuration];
            return new[]
            {
                FindConfigurationForSolution(configurations, ProjectConfigurationType.Debug, config),
                FindConfigurationForSolution(configurations, ProjectConfigurationType.Release, config)
            };
        }

        private string FindConfigurationForSolution(
            IEnumerable<string> configurations, ProjectConfigurationType type,
            IReadOnlyDictionary<string, string> config)
        {
            var typeStr = type.ToString();

            // Find in config.
            if (config != null && config.TryGetValue(typeStr, out var targetConfiguration))
            {
                if (configurations.Contains(targetConfiguration))
                    return targetConfiguration;
            }

            // Find according to default rule.
            configurations = configurations.Where(c => c.StartsWith(typeStr));
            foreach (var configuration in configurations)
            {
                var version = VersionOfConfiguration(configuration);
                if (version != null && version.MajorEquals(ProjectVersion))
                    return configuration;
            }
            return configurations.FirstOrDefault();
        }

        private static void SelectConfigurationPropertyGroups(
            VSProject project, string[] selectedConfigurations,
            out XmlPropertyGroup[] selectedGroups, out XmlPropertyGroup[] redundantGroups)
        {
            SelectConfigurationElements(
                project.ParseConditionalConfigurationPropertyGroups((Func<string, bool>)null),
                selectedConfigurations, out selectedGroups, out redundantGroups);
        }

        public static void SelectConfigurationItemGroups(
            VSProject project, string[] selectedConfigurations,
            out XmlItemGroup[] selectedGroups, out XmlItemGroup[] redundantGroups)
        {
            SelectConfigurationElements(
                project.ParseConditionalConfigurationItemGroups((Func<string, bool>)null),
                selectedConfigurations, out selectedGroups, out redundantGroups);
        }

        private static void SelectConfigurationElements<T>(
            IEnumerable<KeyValuePair<string, T>> allElements, string[] selectedConfigurations,
            out T[] selectedElements, out T[] redundantElements)
            where T : ProjectElement
        {
            selectedElements = new T[selectedConfigurations.Length];
            var redundantGroupList = new List<T>();
            foreach (var kvp in allElements)
            {
                var group = kvp.Value;
                var configurationIndex = Array.IndexOf(selectedConfigurations, kvp.Key);
                if (configurationIndex >= 0)
                    selectedElements[configurationIndex] = group;
                else
                    redundantGroupList.Add(group);
            }
            redundantElements = redundantGroupList.ToArray();
        }

        #endregion Configuration Groups Selection

        #region Configuration Groups Setup

        #region Output Path

        private static void SetupOutputPath(VSProject project, XmlPropertyGroup[] targetPropertyGroups)
        {
            foreach (var type in ProjectConfigurationTypeTraits.Values)
            {
                var group = targetPropertyGroups[(int)type];
                group.SetProperty("IntermediateOutputPath", GetIntermediateOutputPath(project, type));
                group.SetProperty("OutputPath", GetOutputPath(project, type));
            }
        }

        private static string GetIntermediateOutputPath(VSProject project, ProjectConfigurationType type)
            => $@"Temp\{project.Name}_obj\{type}";

        private static string GetOutputPath(VSProject project, ProjectConfigurationType type)
            => $@"Temp\{project.Name}_bin\{type}";

        #endregion Output Path

        #endregion Configuration Groups Setup

        #region Build Event Setup

        public const string DefaultDllAssetDirectory = @"Assets\Assemblies";

        private void SetupPostBuildEvent(VSProject project, Configs.UnityProject.PluginProject config)
        {
            var dllAssetDirectory = config.DllAssetDirectory ?? DefaultDllAssetDirectory;
            if (!dllAssetDirectory.StartsWith("Assets"))
                throw new ArgumentException("Unity asset path expected.", nameof(config.DllAssetDirectory));

            var assetDir = Path.Combine(ProjectDirectory.FullName, dllAssetDirectory);
            assetDir = assetDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            var copyCmd = $@"xcopy ""$(TargetDir)$(TargetName).*"" ""{assetDir}"" /i /y";
            //if (!config.CreateDllAssetDirectoryIfNecessary)
            //    copyCmd = $@"if exist ""{assetDir}"" ({copyCmd})";

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

        #endregion Build Event Setup

        #region Solution

        public SLNToolsProject AddProjectToSolutionFile(VSProject project, XmlPropertyGroup[] targetPropertyGroups)
        {
            if (SolutionFile.Projects.Any(p => p.ProjectGuid == project.GuidText))
                throw new ArgumentException("Project already exists in solution.", nameof(project));

            var existedProj = SolutionFile.Projects.FirstOrDefault(
                p => p.ProjectGuid.Equals(project.GuidText, StringComparison.OrdinalIgnoreCase));
            if (existedProj != null)
                return existedProj;

            var projectConfigurationConditions = targetPropertyGroups
                .Select(g => VSProject.Conditions.Parse(g.Condition)).ToArray();
            var solutionConfigurations = ParseSolutionConfigurations();

            var projectConfigurationsLines =
                CreateProjectConfigurationPropertyLines(solutionConfigurations, projectConfigurationConditions);

            var slnProj = new SLNToolsProject(SolutionFile,
                project.GuidText,
                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", // C# project type GUID.
                project.Name,
                PathEx.MakeRelative(SolutionFile.SolutionFullPath, project.FilePath),
                null, null, null, projectConfigurationsLines);

            SolutionFile.Projects.Add(slnProj);
            return slnProj;
        }

        private string[] ParseSolutionConfigurations()
        {
            var configurationLines = SolutionFile.GlobalSections
                .First(s => s.Name == "SolutionConfigurationPlatforms").PropertyLines;

            var configurations = new string[ProjectConfigurationTypeTraits.ValueCount];
            for (int i = 0; i < ProjectConfigurationTypeTraits.ValueCount; ++i)
            {
                var typeName = ProjectConfigurationTypeTraits.GetName(i);
                configurations[i] = configurationLines.First(l => l.Name.Contains(typeName)).Value;
            }
            return configurations;
        }

        private List<PropertyLine> CreateProjectConfigurationPropertyLines(
            string[] solutionConfigurations, VSProject.Conditions[] projectConfigurationConditions)
        {
            var lines = new List<PropertyLine>();
            for (int i = 0; i < ProjectConfigurationTypeTraits.ValueCount; ++i)
            {
                var projectConfigurationCondition = projectConfigurationConditions[i];
                var solutionConfiguration = solutionConfigurations[i];

                var valuesText = projectConfigurationCondition.ValuesString;
                valuesText = valuesText.Replace("AnyCPU", "Any CPU");

                lines.Add(new PropertyLine($"{solutionConfiguration}.ActiveCfg", valuesText));
                lines.Add(new PropertyLine($"{solutionConfiguration}.Build.0", valuesText));
            }
            return lines;
        }

        #endregion Solution

        #endregion Configure

        #region Utilities

        private static readonly Regex configurationRegex = new Regex(@"(?<cfg>\w+)-(?<ver>.+)");

        private static UnityVersion VersionOfConfiguration(string configuration)
        {
            var match = configurationRegex.Match(configuration);
            return !match.Success ? null : UnityVersion.Parse(match.Groups["ver"].Value);
        }

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

        #endregion Utilities
    }
}