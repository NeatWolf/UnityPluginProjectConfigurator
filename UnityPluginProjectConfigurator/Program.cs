using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using ShuHai.UnityPluginProjectConfigurator.Configs;

namespace ShuHai.UnityPluginProjectConfigurator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithParsed(Run)
                .WithNotParsed(HandleErrors);

            Console.Read();
        }

        private static void Run(CommandLineOptions options)
        {
            try
            {
                var config = Config.Load(options.ConfigPath);

                var defaultVersions = config.DefaultVersions.Select(UnityVersion.Parse).ToArray();
                foreach (var kvp in config.CSharpProjects)
                {
                    var proj = kvp.Value;
                    Console.WriteLine($@"Configure c# project: {proj.Path}");
                    ProjectConfigurator.Configure(proj, defaultVersions);
                }

                //foreach (var uproj in config.UnityProjects)
                //{
                //    Console.WriteLine($"Configure unity project: {uproj.Path}");

                //    var configurator = new UnityProjectConfigurator(uproj.Path);
                //    foreach (var csproj in uproj.CSharpProjects) { }
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
#if DEBUG
                throw;
#endif
            }
            Console.WriteLine("Done!");
        }

        private static void HandleErrors(IEnumerable<Error> errors)
        {
            Console.WriteLine("Error Options:\n");
            foreach (var err in errors)
                Console.WriteLine(err);
        }
    }
}