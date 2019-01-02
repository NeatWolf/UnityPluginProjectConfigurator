using System;

namespace ShuHai.UnityPluginProjectConfigurator.Configs
{
    public class ConfigParseException : Exception
    {
        public ConfigParseException() { }
        public ConfigParseException(string message) : base(message) { }
    }
}