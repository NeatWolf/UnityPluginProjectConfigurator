using System;
using System.Linq;

namespace ShuHai.UnityPluginProjectConfigurator
{
    using UnityEnvironmentTypesTraits = EnumTraits<UnityEnvironmentTypes>;

    [Flags]
    public enum UnityEnvironmentTypes
    {
        Runtime = 0x1,
        Editor = 0x2,
        All = Runtime | Editor
    }

    public static class UnityEnvironmentTypesConverter
    {
        public static string[] ToStrings(UnityEnvironmentTypes types)
        {
            return UnityEnvironmentTypesTraits.Values
                .Where(v => v != UnityEnvironmentTypes.All && (types & v) == v)
                .Select(t => t.ToString())
                .ToArray();
        }

        public static UnityEnvironmentTypes FromStrings(string[] strings)
        {
            return strings
                .Select(UnityEnvironmentTypesTraits.GetValue)
                .Aggregate((types, type) => types | type);
        }
    }
}