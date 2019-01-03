using System;
using System.IO;
using System.Text.RegularExpressions;
using net.r_eg.MvsSln;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public class UnityProjectConfigurator : IDisposable
    {
        public readonly string ProjectName;
        public UnityVersion ProjectVersion;

        public readonly Sln Solution;

        public UnityProjectConfigurator(string directory)
        {
            var dir = new DirectoryInfo(directory);
            ProjectName = dir.Name;

            ProjectVersion = FindProjectVersion(dir);

            var slnPath = Path.Combine(dir.FullName, ProjectName + ".sln");
            Solution = new Sln(slnPath, SlnItems.All & ~SlnItems.ProjectDependencies);
        }

        public void AddCSharpProject(CSharpProject project, Configs.UnityProject.CSharpProject config)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var dllAssetDirectory = config.DllAssetDirectory;
            if (!string.IsNullOrEmpty(dllAssetDirectory))
            {
                if (!dllAssetDirectory.StartsWith("Assets"))
                    throw new ArgumentException("Unity asset path expected.", nameof(dllAssetDirectory));

                var assetDir = Path.Combine(Solution.Result.SolutionDir, dllAssetDirectory);
                assetDir = assetDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                var copyCmd = $@"xcopy ""$(TargetDir)$(TargetName).*"" ""{assetDir}"" /i /y";
                if (!config.CreateDllAssetDirectoryIfNecessary)
                    copyCmd = $@"if exist ""{assetDir}"" ({copyCmd})";

                // The approach below doesn't work since it add the property to existing PropertyGroup such as the
                // PropertyGroup that contains ProjectGuid and AssemblyName, this is usually the first PropertyGroup in
                // the .csproj file, and MSBuild evaluate $(TargetDir) and $(TargetName) as empty string. To ensure the
                // macros available, we need to add the PostBuildEvent property after
                // <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />.
                //csharpProject.SetProperty("PostBuildEvent", copyDllCmd);

                SetBuildEvent(project, "PostBuildEvent", copyCmd);
            }
        }

        private static void SetBuildEvent(CSharpProject project, string name, string value)
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

        #region Dispose

        public void Dispose()
        {
            if (disposed)
                return;

            Solution.Dispose();

            disposed = true;
        }

        private bool disposed;

        #endregion Dispose
    }
}