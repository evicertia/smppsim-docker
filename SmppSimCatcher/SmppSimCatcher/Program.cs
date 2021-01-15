using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;


namespace SmppSimCatcher
{
    public class Program
    {
        private static global::NLog.Logger _Log = null;
        public static string ProgramName { get { return "SmppSimCatcher"; } }
        public static Version ProgramVersion { get { return typeof(Program).Assembly.GetName().Version; } }
        public static string InformationalVersion { get { return typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion; } }
        private static void SetupLogging()
        {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "nlog.config"
                )
            );

            Common.Logging.LogManager.Adapter =
                new Common.Logging.NLog.NLogLoggerFactoryAdapter(
                    new Common.Logging.Configuration.NameValueCollection()
                    {
                        { "configType", "EXTERNAL" }
                    });

            _Log = global::NLog.LogManager.GetCurrentClassLogger();

            _Log.Info("{0} v{1} - {2} starting.", ProgramName, ProgramVersion, InformationalVersion);
            _Log.Info("CommandLine: {0}", Environment.CommandLine);
            _Log.Info("CommandLineArgs: {0}", Environment.GetCommandLineArgs().Aggregate((cur, next) => cur + "," + next));
            _Log.Info("CurrentBaseDirectory: {0}", AppDomain.CurrentDomain.BaseDirectory);
        }

        private static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                 .ConfigureAppConfiguration((hostingContext, config) =>
                 {
                     config.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                     config.AddCommandLine(args);
                 })
                .UseStartup<Startup>();

        public static void Main(string[] args)
        {
            try
            {
                SetupLogging();
                CreateWebHostBuilder(args).Build().Run();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Console.Error.WriteLine(e.StackTrace);
                _Log?.Fatal(e, "Unhandled exception!");

                Environment.Exit(254);
            }

            Environment.Exit(0);
        }
    }
}
