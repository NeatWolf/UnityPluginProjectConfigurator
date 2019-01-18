using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace ShuHai.UnityPluginProjectConfigurator
{
    using XmlElement = ProjectElement;
    using XmlPropertyGroup = ProjectPropertyGroupElement;
    using XmlProperty = ProjectPropertyElement;
    using XmlItemGroup = ProjectItemGroupElement;
    using XmlItem = ProjectItemElement;
    using XmlImport = ProjectImportElement;
    using StringPair = KeyValuePair<string, string>;

    public sealed class VSProject : IDisposable
    {
        public readonly Project MSBuildProject;

        public string FilePath => MSBuildProject.FullPath;
        public string DirectoryPath => MSBuildProject.DirectoryPath;

        public Guid Guid { get; private set; }
        public string GuidText { get; private set; }
        public string Name { get; private set; }

        public override string ToString() => FilePath;

        #region Initialization

        private VSProject(string path)
            : this(ProjectCollection.GlobalProjectCollection, path) { }

        private VSProject(ProjectCollection msBuildProjectCollection, string path)
        {
            MSBuildProject = msBuildProjectCollection.LoadProject(path);
            Initialize();
        }

        private VSProject(Project msBuildProject)
        {
            MSBuildProject = msBuildProject ?? throw new ArgumentNullException(nameof(msBuildProject));
            Initialize();
        }

        private void Initialize()
        {
            GuidText = MSBuildProject.GetProperty("ProjectGuid").EvaluatedValue;
            Guid = new Guid(GuidText);
            Name = Path.GetFileNameWithoutExtension(MSBuildProject.FullPath);

            InitializePropertyGroups();
            InitializeItemGroups();
            InitializeImports();
        }

        #endregion Initialization

        #region Deinitialization

        public void Dispose()
        {
            if (disposed)
                return;

            Deinitialize();
            MSBuildProject.ProjectCollection.UnloadProject(MSBuildProject);

            disposed = true;
        }

        private bool disposed;

        private void Deinitialize()
        {
            MSBuildToolsImport = null;
            DefaultPropertyGroup = null;
            Name = null;
            Guid = default(Guid);
        }

        #endregion Deinitialization

        #region Xml

        public ProjectRootElement Xml => MSBuildProject.Xml;

        #region Properties

        public bool FindProperty(Func<XmlProperty, bool> predicate,
            out XmlPropertyGroup propertyGroup, out XmlProperty property)
        {
            XmlProperty propertyForSearch = null;
            propertyGroup = Xml.PropertyGroups.FirstOrDefault(g =>
            {
                propertyForSearch = g.Properties.FirstOrDefault(predicate);
                return propertyForSearch != null;
            });
            property = propertyForSearch;

            return property != null;
        }

        public bool FindProperty(string name, out XmlPropertyGroup propertyGroup, out XmlProperty property)
        {
            return FindProperty(p => p.Name == name, out propertyGroup, out property);
        }

        #endregion Properties

        #region Property Groups

        /// <summary>
        ///     The property group that contains &lt;ProjectGuid&gt;, &lt;OutputType&gt;, &lt;RootNamespace&gt;, etc.
        /// </summary>
        public XmlPropertyGroup DefaultPropertyGroup { get; private set; }

        public XmlPropertyGroup FindPropertyGroup(Func<XmlPropertyGroup, bool> predicate)
            => Xml.PropertyGroups.FirstOrDefault(predicate);

        public IEnumerable<XmlPropertyGroup> FindPropertyGroups(Func<XmlPropertyGroup, bool> predicate)
            => Xml.PropertyGroups.Where(predicate);

        public XmlPropertyGroup CreatePropertyGroupAfter(XmlElement afterMe)
            => CreateAndInsertXmlElement(afterMe, Xml.CreatePropertyGroupElement, Xml.InsertAfterChild);

        public XmlPropertyGroup CreatePropertyGroupBefore(XmlElement beforeMe)
            => CreateAndInsertXmlElement(beforeMe, Xml.CreatePropertyGroupElement, Xml.InsertBeforeChild);

        public void RemovePropertyGroup(XmlPropertyGroup group) => Xml.RemoveChild(group);

        public void RemovePropertyGroups(IEnumerable<XmlPropertyGroup> groups)
        {
            foreach (var group in groups.ToArray())
                RemovePropertyGroup(group);
        }

        private void InitializePropertyGroups()
        {
            DefaultPropertyGroup = FindPropertyGroup(g => g.Properties.Any(p => p.Name == "ProjectGuid"));
        }

        #region Conditional

        public IEnumerable<XmlPropertyGroup> ParseConditionalConfigurationPropertyGroups(string configurationValue)
            => ParseConditionalConfigurationElements(Xml.PropertyGroups, configurationValue);

        /// <summary>
        ///     Parse and enumerate conditional property groups which contains condition named
        ///     <see cref="ConditionNames.Configuration" />.
        /// </summary>
        /// <param name="configurationValuePredicate">
        ///     Predicate with value of configurations that determines which property groups should be results.
        /// </param>
        /// <returns>
        ///     An enumerable collection that contains <see cref="KeyValuePair{TKey, TValue}" />s mapping from configuration value
        ///     to its corresponding property group pair.
        /// </returns>
        public IEnumerable<KeyValuePair<string, XmlPropertyGroup>>
            ParseConditionalConfigurationPropertyGroups(Func<string, bool> configurationValuePredicate)
            => ParseConditionalConfigurationElements(Xml.PropertyGroups, configurationValuePredicate);

        public IEnumerable<KeyValuePair<Conditions, XmlPropertyGroup>>
            ParseConditionalPropertyGroups(Func<Conditions, bool> predicate)
            => ParseConditionalElements(Xml.PropertyGroups, predicate);

        public static class ConditionNames
        {
            public const string Configuration = "Configuration";
            public const string Platform = "Platform";
        }

        public struct Condition : IEquatable<Condition>
        {
            public readonly string Name;
            public readonly string Value;

            #region Constructors

            public Condition(string name, string value)
            {
                Name = name;
                Value = value;
                hashCode = HashCode.Get(Name, Value);
            }

            public Condition(StringPair condition)
            {
                Name = condition.Key;
                Value = condition.Value;
                hashCode = HashCode.Get(Name, Value);
            }

            #endregion Constructors

            #region Equality

            public static bool operator ==(Condition l, Condition r)
                => EqualityComparer<Condition>.Default.Equals(l, r);

            public static bool operator !=(Condition l, Condition r)
                => !EqualityComparer<Condition>.Default.Equals(l, r);

            public bool Equals(Condition other) => string.Equals(Name, other.Name) && string.Equals(Value, other.Value);

            public override bool Equals(object obj) => obj is Condition condition && Equals(condition);

            public override int GetHashCode() => hashCode;

            [NonSerialized] private readonly int hashCode;

            #endregion Equality
        }

        public sealed class Conditions
            : IReadOnlyDictionary<string, string>, IReadOnlyList<Condition>, IEquatable<Conditions>
        {
            public int Count => list.Count;

            public Condition this[int index] => list[index];

            public string this[string key] => dict[key];

            public IEnumerable<string> Keys => dict.Keys;
            public IEnumerable<string> Values => dict.Values;

            #region Constructors

            public Conditions() : this((IEnumerable<Condition>)null) { }

            public Conditions(IEnumerable<StringPair> conditions)
                : this(conditions.Select(p => new Condition(p.Key, p.Value))) { }

            public Conditions(IEnumerable<Condition> conditions)
            {
                var dict = new Dictionary<string, string>();
                var list = new List<Condition>();
                if (conditions != null)
                {
                    foreach (var condition in conditions)
                    {
                        list.Add(condition);
                        dict.Add(condition.Name, condition.Value);
                    }
                }
                this.list = list;
                this.dict = dict;

                namesString = new Lazy<string>(AppendNames(new StringBuilder(), false).ToString);
                valuesString = new Lazy<string>(AppendValues(new StringBuilder(), false).ToString);
                str = new Lazy<string>(BuildString);
                hashCode = HashCode.Get(list);
            }

            #endregion Constructors

            public bool ContainsKey(string key) { return dict.ContainsKey(key); }

            public bool TryGetValue(string key, out string value) { return dict.TryGetValue(key, out value); }

            public IEnumerator<Condition> GetEnumerator() { return list.GetEnumerator(); }

            private readonly IReadOnlyDictionary<string, string> dict;
            private readonly IReadOnlyList<Condition> list;

            #region Parse

            public static Conditions Parse(string text) { return new Conditions(ParseAndEnumerate(text)); }

            public static IEnumerable<Condition> ParseAndEnumerate(string text)
            {
                var match = conditionRegex.Match(text);
                if (!match.Success)
                    throw new ArgumentException("Invalid format of condition text.", nameof(text));

                var names = match.Groups["Names"].Value.Split('|');
                var values = match.Groups["Values"].Value.Split('|');
                int count = names.Length;
                if (count != values.Length)
                    throw new ArgumentException("Number of configuration and its value does not match.", nameof(text));

                for (int i = 0; i < count; ++i)
                {
                    var nameMatch = configurationNameRegex.Match(names[i]);
                    if (!nameMatch.Success)
                        throw new ArgumentException("Invalid format of configuration name.", nameof(text));
                    yield return new Condition(nameMatch.Groups["Name"].Value, values[i]);
                }
            }

            private static readonly Regex conditionRegex = new Regex(@"\'(?<Names>.+)\'\s*==\s*\'(?<Values>.*)\'");
            private static readonly Regex configurationNameRegex = new Regex(@"\$\((?<Name>\w+)\)");

            #endregion Parse

            #region Strings

            public string NamesString => namesString.Value;
            public string ValuesString => valuesString.Value;

            public override string ToString() => str.Value;

            [NonSerialized] private readonly Lazy<string> namesString;
            [NonSerialized] private readonly Lazy<string> valuesString;
            [NonSerialized] private readonly Lazy<string> str;

            private string BuildString()
            {
                var builder = new StringBuilder();

                builder.Append(' ');

                AppendNames(builder, true);
                builder.Append(" == ");
                AppendValues(builder, true);

                builder.Append(' ');

                return builder.ToString();
            }

            private StringBuilder AppendNames(StringBuilder builder, bool quote)
            {
                if (quote)
                    builder.Append('\'');

                foreach (var condition in list)
                    builder.Append($@"$({condition.Name})").Append('|');
                builder.RemoveTail(1);

                if (quote)
                    builder.Append('\'');

                return builder;
            }

            private StringBuilder AppendValues(StringBuilder builder, bool quote)
            {
                if (quote)
                    builder.Append('\'');

                foreach (var condition in list)
                    builder.Append(condition.Value).Append('|');
                builder.RemoveTail(1);

                if (quote)
                    builder.Append('\'');

                return builder;
            }

            #endregion Strings

            #region Equality

            public static bool operator ==(Conditions l, Conditions r)
                => EqualityComparer<Conditions>.Default.Equals(l, r);

            public static bool operator !=(Conditions l, Conditions r)
                => !EqualityComparer<Conditions>.Default.Equals(l, r);

            public bool Equals(Conditions other) { return list.SequenceEqual(other.list); }

            public override bool Equals(object obj) { return obj is Conditions conditions && Equals(conditions); }

            public override int GetHashCode() => hashCode;

            [NonSerialized] private readonly int hashCode;

            #endregion Equality

            #region Explicit Implementations

            IEnumerator<StringPair> IEnumerable<StringPair>.GetEnumerator() { return dict.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }

            #endregion Explicit Implementations
        }

        #endregion Conditional

        #endregion Property Groups

        #region Items

        public XmlItem FindItem(Func<XmlItem, bool> predicate) => Xml.Items.FirstOrDefault(predicate);

        public IEnumerable<XmlItem> FindItems(string name)
            => Xml.Items.Where(i => name == null || name == i.ElementName);

        public IEnumerable<XmlItem> FindItems(Func<XmlItem, bool> predicate) => Xml.Items.Where(predicate);

        public IEnumerable<XmlItem> FindCompiles(Func<XmlItem, bool> predicate)
            => FindItems(i => i.ElementName == "Compile" && (predicate == null || predicate(i)));

        public IEnumerable<XmlItem> FindProjectReferences(Func<XmlItem, bool> predicate)
            => FindItems(i => i.ElementName == "ProjectReference" && (predicate == null || predicate(i)));

        #endregion Items

        #region Item Groups

        public XmlItemGroup DefaultReferenceGroup { get; private set; }

        public XmlItemGroup FindItemGroup(Func<XmlItemGroup, bool> predicate)
            => Xml.ItemGroups.FirstOrDefault(predicate);

        public IEnumerable<XmlItemGroup> FindItemGroups(Func<XmlItemGroup, bool> predicate)
            => Xml.ItemGroups.Where(predicate);

        public IEnumerable<XmlItemGroup> ParseConditionalConfigurationItemGroups(string configurationValue)
            => ParseConditionalConfigurationElements(Xml.ItemGroups, configurationValue);

        public IEnumerable<KeyValuePair<string, XmlItemGroup>>
            ParseConditionalConfigurationItemGroups(Func<string, bool> configurationValuePredicate)
            => ParseConditionalConfigurationElements(Xml.ItemGroups, configurationValuePredicate);

        public IEnumerable<KeyValuePair<Conditions, XmlItemGroup>>
            ParseConditionalItemGroups(Func<Conditions, bool> predicate)
            => ParseConditionalElements(Xml.ItemGroups, predicate);

        public XmlItemGroup CreateItemGroupAfter(XmlElement afterMe)
            => CreateAndInsertXmlElement(afterMe, Xml.CreateItemGroupElement, Xml.InsertAfterChild);

        public XmlItemGroup CreateItemGroupBefore(XmlElement beforeMe)
            => CreateAndInsertXmlElement(beforeMe, Xml.CreateItemGroupElement, Xml.InsertBeforeChild);

        public void RemoveItemGroup(XmlItemGroup group) => Xml.RemoveChild(group);

        public void RemoveItemGroups(IEnumerable<XmlItemGroup> groups)
        {
            foreach (var group in groups.ToArray())
                Xml.RemoveChild(group);
        }

        private void InitializeItemGroups()
        {
            DefaultReferenceGroup = FindItemGroup(g => g.Items.Any(i => i.Include == "System"));
        }

        #endregion Item Groups

        #region Imports

        public XmlImport MSBuildToolsImport;

        public XmlImport FindImport(string project) { return FindImport(i => i.Project == project); }

        public XmlImport FindImport(Func<XmlImport, bool> predicate) { return Xml.Imports.FirstOrDefault(predicate); }

        private void InitializeImports()
        {
            MSBuildToolsImport = FindImport(@"$(MSBuildToolsPath)\Microsoft.CSharp.targets");
        }

        #endregion Imports

        #region Utilities

        public static IEnumerable<T>
            ParseConditionalConfigurationElements<T>(IEnumerable<T> elements, string configuraionValue)
            where T : XmlElement
        {
            return ParseConditionalConfigurationElements(elements,
                    configuraionValue == null ? (Func<string, bool>)null : c => c == configuraionValue)
                .Select(kvp => kvp.Value);
        }

        public static IEnumerable<KeyValuePair<string, T>> ParseConditionalConfigurationElements<T>(
            IEnumerable<T> elements, Func<string, bool> configuraionValuePredicate)
            where T : XmlElement
        {
            return from kvp in ParseConditionalElements(elements, null)
                let configurationValue = kvp.Key[ConditionNames.Configuration]
                where configuraionValuePredicate == null || configuraionValuePredicate(configurationValue)
                select new KeyValuePair<string, T>(configurationValue, kvp.Value);
        }

        public static IEnumerable<KeyValuePair<Conditions, T>>
            ParseConditionalElements<T>(IEnumerable<T> elements, Func<Conditions, bool> predicate)
            where T : XmlElement
        {
            return from e in elements
                let conditionsText = e.Condition
                where !string.IsNullOrEmpty(conditionsText)
                let conditions = new Conditions(Conditions.ParseAndEnumerate(conditionsText))
                where predicate == null || predicate(conditions)
                select new KeyValuePair<Conditions, T>(conditions, e);
        }

        private T CreateAndInsertXmlElement<T>(XmlElement insertAnchor,
            Func<T> createMethod, Action<XmlElement, XmlElement> insertMethod)
            where T : XmlElement
        {
            Ensure.Argument.NotNull(insertAnchor, nameof(insertAnchor));
            if (insertAnchor.ContainingProject != Xml)
                throw new ArgumentException("Child of current project required.", nameof(insertAnchor));

            var group = createMethod();
            insertMethod(group, insertAnchor);
            return group;
        }

        #endregion Utilities

        #endregion Xml

        #region Persistency

        public void Save()
        {
            PrepareSave();
            MSBuildProject.Save();
        }

        public void Save(string path)
        {
            PrepareSave();
            MSBuildProject.Save(path);
        }

        private void PrepareSave() => MSBuildProject.ReevaluateIfNecessary();

        public static void SaveAll()
        {
            foreach (var inst in Instances)
                inst.Save();
        }

        #endregion Persistency

        #region Clone

        public bool IsCloned => CloneSourcePath != null;

        /// <summary>
        ///     Indicates from which project the current instance is cloned from if not <see langword="null" />, or the current
        ///     instance isn't cloned from any project.
        /// </summary>
        public string CloneSourcePath { get; private set; }

        public static VSProject Clone(VSProject project, string newPath, bool overwrite)
        {
            Ensure.Argument.NotNull(project, nameof(project));
            Ensure.Argument.NotNullOrEmpty(newPath, nameof(newPath));

            if (overwrite)
            {
                Unload(newPath);
            }
            else
            {
                if (instances.ContainsKey(newPath))
                    throw new InvalidOperationException("Project at specified path already loaded.");
                if (File.Exists(newPath))
                    throw new InvalidOperationException("Project at specified path already existed.");
            }

            // Create new project instance.
            var newGuidText = $@"{{{Guid.NewGuid().ToString().ToUpper()}}}";
            var newXml = project.Xml.DeepClone();
            newXml.FullPath = newPath;
            newXml.Properties.First(p => p.ElementName == "ProjectGuid").Value = newGuidText;
            var newProj = new VSProject(new Project(newXml));

            // Prepare data for following process.
            var oldProjDir = project.DirectoryPath + Path.DirectorySeparatorChar;
            var newProjDir = newProj.DirectoryPath + Path.DirectorySeparatorChar;

            // Convert path of sources.
            var compileItems = newProj.FindCompiles(null);
            foreach (var item in compileItems)
            {
                var fullPath = Path.GetFullPath(Path.Combine(oldProjDir, item.Include));
                item.Include = PathEx.MakeRelative(newProjDir, fullPath);

                var link = item.Metadata.FirstOrDefault(m => m.ElementName == "Link");
                if (link == null)
                    item.AddMetadata("Link", Path.GetFileName(item.Include));
            }

            // Convert path of project references.
            var projRefItems = newProj.FindProjectReferences(null);
            foreach (var item in projRefItems)
            {
                var fullPath = Path.GetFullPath(Path.Combine(oldProjDir, item.Include));
                item.Include = PathEx.MakeRelative(newProjDir, fullPath);
            }

            newProj.CloneSourcePath = project.FilePath;
            return AddInstance(newProj);
        }

        #endregion Clone

        #region Instances

        public static IReadOnlyCollection<VSProject> Instances => instances.Values;

        /// <summary>
        ///     Loads a project file at specifiled path anyway. If the project at specified path is already loaded, reloaed it.
        /// </summary>
        /// <param name="path">Path of the project file.</param>
        /// <returns>A <see cref="VSProject" /> instance that represents the loaded project.</returns>
        public static VSProject Load(string path)
        {
            if (instances.TryGetValue(path, out var instance))
                UnloadImpl(path, instance);
            return LoadImpl(path);
        }

        public static VSProject Get(string path)
        {
            return instances.TryGetValue(path, out var instance) ? instance : null;
        }

        public static VSProject GetOrLoad(string path)
        {
            return instances.TryGetValue(path, out var instance) ? instance : LoadImpl(path);
        }

        public static bool Unload(string path)
        {
            if (!instances.TryGetValue(path, out var instance))
                return false;
            UnloadImpl(path, instance);
            return true;
        }

        public static void UnloadAll()
        {
            foreach (var kvp in new Dictionary<string, VSProject>(instances))
                UnloadImpl(kvp.Key, kvp.Value);
        }

        private static readonly Dictionary<string, VSProject> instances = new Dictionary<string, VSProject>();

        private static VSProject LoadImpl(string path) => AddInstance(new VSProject(path));

        private static VSProject LoadImpl(ProjectRootElement xml) => AddInstance(new VSProject(new Project(xml)));

        private static VSProject AddInstance(VSProject instance)
        {
            instances.Add(instance.FilePath, instance);
            return instance;
        }

        private static void UnloadImpl(string path, VSProject instance)
        {
            instances.Remove(path);
            instance.Dispose();
        }

        #endregion Instances
    }
}