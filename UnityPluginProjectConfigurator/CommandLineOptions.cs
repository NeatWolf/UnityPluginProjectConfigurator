using CommandLine;
using ShuHai.UnityPluginProjectConfigurator.Configs;

namespace ShuHai.UnityPluginProjectConfigurator
{
    public class CommandLineOptions
    {
        [Option('c', "config", Default = Config.DefaultFileName, Required = false)]
        public string ConfigPath { get; set; }
    }
}