using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using net.r_eg.MvsSln;
using ShuHai.UnityPluginProjectConfigurator.Configs;

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

            ProjectVersion = FindUnityProjectVersion(dir);

            var slnPath = Path.Combine(dir.FullName, ProjectName + ".sln");
            Solution = new Sln(slnPath, SlnItems.All & ~SlnItems.ProjectDependencies);
        }

        public void Configure(Project csharpProject, string dllAssetDirectory)
        {
            if (csharpProject == null)
                throw new ArgumentNullException(nameof(csharpProject));
            if (dllAssetDirectory == null)
                throw new ArgumentNullException(nameof(dllAssetDirectory));
            if (!dllAssetDirectory.StartsWith("Assets"))
                throw new ArgumentException("Unity asset path expected.", nameof(dllAssetDirectory));
        }

        private static ProjectPropertyGroupElement FindOrCreatePostBuildEventProperty(Project csharpProject)
        {
            throw new NotImplementedException();
        }

        #region Find Version

        private static readonly Regex UnityVersionRegex = new Regex(@"m_EditorVersion: (?<ver>\d+\.\d+\.*\d*\w*\d*)\Z");

        private static UnityVersion FindUnityProjectVersion(DirectoryInfo unityProjectDir)
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