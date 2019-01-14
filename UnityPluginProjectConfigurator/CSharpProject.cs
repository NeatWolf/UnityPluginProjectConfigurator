using System;
using System.Collections;
using System.Collections.Generic;
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
    using XmlImport = ProjectImportElement;
    using StringPair = KeyValuePair<string, string>;

    public sealed class CSharpProject : IDisposable
    {
        public readonly Project MSBuildProject;

        public string Path => MSBuildProject.FullPath;

        public Guid Guid { get; private set; }
        public string Name { get; private set; }

        #region Initialization

        private CSharpProject(string path)
            : this(ProjectCollection.GlobalProjectCollection, path) { }

        private CSharpProject(ProjectCollection msBuildProjectCollection, string path)
        {
            MSBuildProject = msBuildProjectCollection.LoadProject(path);
            Initialize();
        }

        private CSharpProject(Project msBuildProject)
        {
            MSBuildProject = msBuildProject ?? throw new ArgumentNullException(nameof(msBuildProject));
            Initialize();
        }

        private void Initialize()
        {
            Guid = new Guid(MSBuildProject.GetProperty("ProjectGuid").EvaluatedValue);
            Name = System.IO.Path.GetFileNameWithoutExtension(MSBuildProject.FullPath);

            InitializePropertyGroups();
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
        {
            return Xml.PropertyGroups.FirstOrDefault(predicate);
        }

        public IEnumerable<XmlPropertyGroup> FindPropertyGroups(Func<XmlPropertyGroup, bool> predicate)
        {
            return Xml.PropertyGroups.Where(predicate);
        }

        public XmlPropertyGroup CreatePropertyGroupAfter(XmlElement afterMe)
        {
            InsertPropertyGroupArgumentCheck(afterMe, nameof(afterMe));

            var group = Xml.CreatePropertyGroupElement();
            Xml.InsertAfterChild(group, afterMe);
            return group;
        }

        public XmlPropertyGroup CreatePropertyGroupBefore(XmlElement beforeMe)
        {
            InsertPropertyGroupArgumentCheck(beforeMe, nameof(beforeMe));

            var group = Xml.CreatePropertyGroupElement();
            Xml.InsertBeforeChild(group, beforeMe);
            return group;
        }

        private void InsertPropertyGroupArgumentCheck(XmlElement arg, string name)
        {
            if (arg == null)
                throw new ArgumentNullException(name);
            if (arg.ContainingProject != Xml)
                throw new ArgumentException("Child of current project required.", name);
        }

        private void InitializePropertyGroups()
        {
            DefaultPropertyGroup = FindPropertyGroup(g => g.Properties.Any(p => p.Name == "ProjectGuid"));
        }

        #region Conditional

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
            ParseConfigurationPropertyGroups(Func<string, bool> configurationValuePredicate)
        {
            return ParseConditionalPropertyGroups(c => c.ContainsKey(ConditionNames.Configuration))
                .Select(p => new KeyValuePair<string, XmlPropertyGroup>(p.Key[ConditionNames.Configuration], p.Value))
                .Where(p => configurationValuePredicate == null || configurationValuePredicate(p.Key));
        }

        /// <summary>
        ///     Enumerate conditional property groups that matching specified predicate, or enumerate all conditional property
        ///     groups if specified predicate is <see langword="null" />.
        /// </summary>
        public IEnumerable<XmlPropertyGroup> FindConditionalPropertyGroups(Func<string, bool> predicate)
        {
            return Xml.PropertyGroups
                .Where(g => !string.IsNullOrEmpty(g.Condition) && (predicate == null || predicate(g.Condition)));
        }

        public IEnumerable<KeyValuePair<Conditions, XmlPropertyGroup>>
            ParseConditionalPropertyGroups(Func<Conditions, bool> predicate)
        {
            foreach (var group in Xml.PropertyGroups)
            {
                var conditionText = group.Condition;
                if (string.IsNullOrEmpty(conditionText))
                    continue;

                var condition = new Conditions(ParseConditions(conditionText));
                if (predicate == null || predicate(condition))
                    yield return new KeyValuePair<Conditions, XmlPropertyGroup>(condition, group);
            }
        }

        #region Parse

        /// <summary>
        ///     Parse the specified condition text and get condition value with specified name.
        /// </summary>
        /// <param name="text">Condition text to parse.</param>
        /// <param name="name">Name of the condition.</param>
        /// <returns>
        ///     Value of the condition with specified <paramref name="name" />, or <see langword="null" /> if condition with
        ///     specified <paramref name="name" /> doesn't exist.
        /// </returns>
        public static string ParseCondition(string text, string name)
        {
            return ParseConditions(text).FirstOrDefault(c => c.Name == name).Value;
        }

        public static IEnumerable<Condition> ParseConditions(string text)
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

        public static class ConditionNames
        {
            public const string Configuration = "Configuration";
            public const string Platform = "Platform";
        }

        public struct Condition : IEquatable<Condition>
        {
            public readonly string Name;
            public readonly string Value;

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

                hashCode = HashCode.Get(list);
            }

            public bool ContainsKey(string key) { return dict.ContainsKey(key); }

            public bool TryGetValue(string key, out string value) { return dict.TryGetValue(key, out value); }

            public IEnumerator<Condition> GetEnumerator() { return list.GetEnumerator(); }

            private readonly IReadOnlyDictionary<string, string> dict;
            private readonly IReadOnlyList<Condition> list;

            #region To String

            public string NamesToString() => AppendNames(new StringBuilder(), false).ToString();

            public string ValuesToString() => AppendValues(new StringBuilder(), false).ToString();

            public override string ToString()
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

            #endregion To String

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

        #region Imports

        public XmlImport MSBuildToolsImport;

        public XmlImport FindImport(string project) { return FindImport(i => i.Project == project); }

        public XmlImport FindImport(Func<XmlImport, bool> predicate) { return Xml.Imports.FirstOrDefault(predicate); }

        private void InitializeImports()
        {
            MSBuildToolsImport = FindImport(@"$(MSBuildToolsPath)\Microsoft.CSharp.targets");
        }

        #endregion Imports

        #region Persistency

        public void Save()
        {
            MSBuildProject.ReevaluateIfNecessary();
            MSBuildProject.Save();
        }

        public static void SaveAll()
        {
            foreach (var inst in Instances)
                inst.Save();
        }

        #endregion Persistency

        #endregion Xml

        #region Instances

        public static IReadOnlyCollection<CSharpProject> Instances => instances.Values;

        /// <summary>
        ///     Loads a project file at specifiled path anyway. If the project at specified path is already loaded, reloaed it.
        /// </summary>
        /// <param name="path">Path of the project file.</param>
        /// <returns>A <see cref="CSharpProject" /> instance that represents the loaded project.</returns>
        public static CSharpProject Load(string path)
        {
            if (instances.TryGetValue(path, out var instance))
                UnloadImpl(path, instance);
            return LoadImpl(path);
        }

        public static CSharpProject Get(string path)
        {
            return instances.TryGetValue(path, out var instance) ? instance : null;
        }

        public static CSharpProject GetOrLoad(string path)
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
            foreach (var kvp in new Dictionary<string, CSharpProject>(instances))
                UnloadImpl(kvp.Key, kvp.Value);
        }

        private static readonly Dictionary<string, CSharpProject> instances = new Dictionary<string, CSharpProject>();

        private static CSharpProject LoadImpl(string path)
        {
            var instance = new CSharpProject(path);
            instances.Add(path, instance);
            return instance;
        }

        private static void UnloadImpl(string path, CSharpProject instance)
        {
            instances.Remove(path);
            instance.Dispose();
        }

        #endregion Instances
    }
}