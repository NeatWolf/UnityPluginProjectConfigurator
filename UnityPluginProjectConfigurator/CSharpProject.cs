using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public sealed class CSharpProject
    {
        public readonly Project Project;

        public readonly bool IsEditor;
        public readonly IReadOnlyList<UnityVersion> Versions;

        public CSharpProject(Project project, Configs.CSharpProject config, IEnumerable<string> versions)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));

            IsEditor = config.IsEditor;

            Versions = versions != null
                ? versions.Select(UnityVersion.Parse).ToArray()
                : Array.Empty<UnityVersion>();

            DefaultPropertyGroup = FindPropertyGroup(g => g.Properties.Any(p => p.Name == "ProjectGuid"));
            MSBuildToolsImport = FindImport(@"$(MSBuildToolsPath)\Microsoft.CSharp.targets");
        }

        #region Xml

        public ProjectRootElement Xml => Project.Xml;

        #region Properties

        public bool FindProperty(Func<ProjectPropertyElement, bool> predicate,
            out ProjectPropertyGroupElement propertyGroup, out ProjectPropertyElement property)
        {
            ProjectPropertyElement propertyForSearch = null;
            propertyGroup = Xml.PropertyGroups.FirstOrDefault(g =>
            {
                propertyForSearch = g.Properties.FirstOrDefault(predicate);
                return propertyForSearch != null;
            });
            property = propertyForSearch;

            return property != null;
        }

        public bool FindProperty(string name,
            out ProjectPropertyGroupElement propertyGroup, out ProjectPropertyElement property)
        {
            return FindProperty(p => p.Name == name, out propertyGroup, out property);
        }

        #region Group

        /// <summary>
        ///     The property group that contains &lt;ProjectGuid&gt;, &lt;OutputType&gt;, &lt;RootNamespace&gt;, etc.
        /// </summary>
        public readonly ProjectPropertyGroupElement DefaultPropertyGroup;

        public ProjectPropertyGroupElement FindPropertyGroup(Func<ProjectPropertyGroupElement, bool> predicate)
        {
            return Xml.PropertyGroups.FirstOrDefault(predicate);
        }

        public IEnumerable<ProjectPropertyGroupElement>
            FindPropertyGroups(Func<ProjectPropertyGroupElement, bool> predicate)
        {
            return Xml.PropertyGroups.Where(predicate);
        }

        public ProjectPropertyGroupElement CreatePropertyGroupAfter(ProjectElement afterMe)
        {
            InsertPropertyGroupArgumentCheck(afterMe, nameof(afterMe));

            var group = Xml.CreatePropertyGroupElement();
            Xml.InsertAfterChild(group, afterMe);
            return group;
        }

        public ProjectPropertyGroupElement CreatePropertyGroupBefore(ProjectElement beforeMe)
        {
            InsertPropertyGroupArgumentCheck(beforeMe, nameof(beforeMe));

            var group = Xml.CreatePropertyGroupElement();
            Xml.InsertBeforeChild(group, beforeMe);
            return group;
        }

        private void InsertPropertyGroupArgumentCheck(ProjectElement arg, string name)
        {
            if (arg == null)
                throw new ArgumentNullException(name);
            if (arg.ContainingProject != Xml)
                throw new ArgumentException("Child of current project required.", name);
        }

        #endregion Group

        #endregion Properties

        #region Imports

        public readonly ProjectImportElement MSBuildToolsImport;

        public ProjectImportElement FindImport(string project) { return FindImport(i => i.Project == project); }

        public ProjectImportElement FindImport(Func<ProjectImportElement, bool> predicate)
        {
            return Xml.Imports.FirstOrDefault(predicate);
        }

        #endregion Imports

        #endregion Xml
    }
}