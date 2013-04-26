using System;
using J.SessionManager;
using NDesk.Options;

namespace J.VS.Client
{
    class Program
    {
        private static JSession _jSession = null;
        private static string _programName;
        private static CmdLineOptions _options;
        
        static int Main(string[] args)
        {
            _programName = JSession.ProgramName;
            _options = new CmdLineOptions();

            bool showHelp = false;
            var optionSet = new OptionSet()
            {
                { "p|port=", "{PORT} to connect to.", (ushort v) => _options.Port = v },
                { "s|server:", "Repl {SERVER} to connect to. Default is 127.0.0.1.", v => _options.Server = v },
                { "f|launch_file:", "{SCRIPT} file to run on startup.", v => _options.LaunchFile = v },
                { "m|mode:", "Interactive {MODE} to use. Defaults to Standard.", v => _options.Backend = v },
                { "a|enable-attach", "Enable attaching the debugger via )attach.\n", v => _options.EnableAttach = v != null },
                { "h|help",  "Show this message and exit.", v => showHelp = v != null }
            };

            try
            {
                _options.JOptions = optionSet.Parse(args).ToArray();
                if (showHelp)
                {
                    ShowHelp(optionSet);
                    return 0;
                }
                if (_options.Port < 1)
                {
                    throw new Exception(string.Format("Missing PORT for repl processor client session."));
                }
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine();
                System.Console.Error.Write(string.Format("{0}: ", _programName));
                System.Console.Error.WriteLine(ex.Message);
                System.Console.Error.WriteLine(string.Format("Try '{0} --help' for more information.", _programName));
                return 1;
            }

            try
            {
                using (_jSession = new JSession(_options.JOptions))
                {
                    System.Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        _jSession.IncAdBreak();
                    };
                    _jSession.SetType(JSession.SMCON);
                    throw new NotImplementedException();
                    ReplProcessor.Run(_options, _jSession);
                }
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine();
                System.Console.Error.Write(string.Format("{0}: ", _programName));
                System.Console.Error.WriteLine(ex.ToString());
                return 1;
            } 
            return 0;
        }

        private static void ShowHelp(OptionSet optionSet)
        {
            System.Console.WriteLine(string.Format("Usage: {0} [OPTIONS]", _programName));
            System.Console.WriteLine("J Repl client for Visual Studio integration.");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(System.Console.Out);
            System.Console.WriteLine();
            System.Console.WriteLine("Usual jconsole options (-jprofile, -js, etc.) are supported unchanged.");
        }
    }
    internal class CmdLineOptions
    {
        internal ushort Port = 0;
        internal string LaunchFile = null;
        internal string Backend = "Standard";
        internal bool EnableAttach = false;
        internal string Server = "127.0.0.1";

        internal string[] JOptions = null;
    }
}