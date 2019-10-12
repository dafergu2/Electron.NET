using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ElectronNET.CLI.Commands.Actions;

namespace ElectronNET.CLI.Commands
{
    public class StartElectronCommand : ICommand
    {
        public const string COMMAND_NAME = "start";
        public const string COMMAND_DESCRIPTION = "Start your ASP.NET Core Application with Electron, without package it as a single exe. Faster for development.";
        public static string COMMAND_ARGUMENTS = "<Path> from ASP.NET Core Project." + Environment.NewLine +
                                                 "Optional: '/app-arguments' with n number arguments to pass to the app";
        public static IList<CommandOption> CommandOptions { get; set; } = new List<CommandOption>();

        private string[] _args;

        public StartElectronCommand(string[] args)
        {
            _args = args;
        }

        private string _paramAppArguments = "app-arguments";

        public Task<bool> ExecuteAsync()
        {
            return Task.Run(() =>
            {
                Console.WriteLine("Start Electron Desktop Application...");

                string aspCoreProjectPath = "";
                var appParams = "";

                if (_args.Length > 0)
                {
                    if (!_args[0].StartsWith("/") && Directory.Exists(_args[0]))
                    {
                        aspCoreProjectPath = _args[0];
                    }

                    var parser = new SimpleCommandLineParser();
                    parser.Parse(_args);

                    if (parser.Arguments.ContainsKey(_paramAppArguments))
                    {
                        appParams = string.Join(" ", parser.Arguments[_paramAppArguments].Select(a => $"\"{a}\""));
                    }
                }
                else
                {
                    aspCoreProjectPath = Directory.GetCurrentDirectory();
                }

                string tempPath = Path.Combine(aspCoreProjectPath, "obj", "Host");
                if (Directory.Exists(tempPath) == false)
                {
                    Directory.CreateDirectory(tempPath);
                }

                var platformInfo = GetTargetPlatformInformation.Do(string.Empty, string.Empty);

                string tempBinPath = Path.Combine(tempPath, "bin");
                var resultCode = ProcessHelper.CmdExecute($"dotnet publish -r {platformInfo.NetCorePublishRid} --output \"{tempBinPath}\"", aspCoreProjectPath);

                if (resultCode != 0)
                {
                    Console.WriteLine("Error occurred during dotnet publish: " + resultCode);
                    return false;
                }

                DeployEmbeddedElectronFiles.Do(tempPath);

                var nodeModulesDirPath = Path.Combine(tempPath, "node_modules");

                Console.WriteLine("node_modules missing in: " + nodeModulesDirPath);

                Console.WriteLine("Start npm install...");
                ProcessHelper.CmdExecute("npm install", tempPath);

                Console.WriteLine("ElectronHostHook handling started...");

                string electronhosthookDir = Path.Combine(Directory.GetCurrentDirectory(), "ElectronHostHook");

                if (Directory.Exists(electronhosthookDir))
                {
                    string hosthookDir = Path.Combine(tempPath, "ElectronHostHook");
                    DirectoryCopy.Do(electronhosthookDir, hosthookDir, true, new List<string>() { "node_modules" });

                    Console.WriteLine("Start npm install for hosthooks...");
                    ProcessHelper.CmdExecute("npm install", hosthookDir);

                    string tscPath = Path.Combine(tempPath, "node_modules", ".bin");
                    // ToDo: Not sure if this runs under linux/macos
                    ProcessHelper.CmdExecute(@"tsc -p ../../ElectronHostHook", tscPath);
                }

                var path = Path.Combine(tempPath, "node_modules", ".bin");
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var electron = isWindows ? @"electron.cmd" : $"./electron";
                var script = $"\"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}main.js\"";
                if (!string.IsNullOrWhiteSpace(appParams))
                {
                    script = $"{script} {appParams}";
                }
                var cmd = $"{electron} {script}";
                Console.WriteLine($"Invoke '{cmd}' - in dir: " + path);
                ProcessHelper.CmdExecute(cmd, path);
                return true;
            });
        }
    }
}
