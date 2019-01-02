using System;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public class ConfigParseException : Exception
    {
        public ConfigParseException() { }
        public ConfigParseException(string message) : base(message) { }
    }
}