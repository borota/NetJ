using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using J.SessionManager;
using NDesk.Options;

namespace J.Console
{
    class Program
    {
        private static JSession _jSession = null;
        private static string _input = null;
        private static string _programName;
        private static CmdLineOptions _options;

            private static int Main(string[] argv)
        {
            try
            {
                _programName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                _options = new CmdLineOptions();

                bool showHelp = false;
                var optionSet = new OptionSet()
                {
                    { "p|port=", "{PORT} to listen/connect to.", (ushort v) => _options.Ports.Add(v) },
					{ "i|interactive", "Start interactive session. Use -i- to turn off.", v => _options.Interactive = v != null },
                    { "l|loopback", "Listen on loopback. Use -l- to use network.\n", v => _options.Loopback = v != null },
                    { "r|repl", "Start as repl client. Following options apply.", v => { _options.ReplMode = v != null;  _options.Interactive = false; } },
                    { "f|launch_file", "The script file to run on startup.", v => { _options.LaunchFile = v; _options.ReplMode = true; _options.Interactive = false; } },
                    { "m|execution_mode", "The backend to use.", v => { _options.Backend = v; _options.ReplMode = true; _options.Interactive = false; } },
                    { "a|enable-attach", "Enable attaching the debugger via )attach.", v => { _options.EnableAttach = v != null; _options.ReplMode = true; _options.Interactive = false; } },
                    { "s|server=", "Repl {SERVER} to connect to. Default is 127.0.0.1.\n", v => { _options.Server = v; _options.ReplMode = true; _options.Interactive = false; } },
                    { "h|help",  "Show this message and exit.", v => showHelp = v != null }
                };

                try
                {
                    _options.JOptions = optionSet.Parse(argv).ToArray();
                    if (showHelp)
                    {
                        ShowHelp(optionSet);
                        return 0;
                    }
                    if (!_options.Interactive && _options.Ports.Count < 1)
                    {
                        throw new Exception(string.Format("Missing PORT for {0} session.", (_options.ReplMode ? "repl processor" : "non-interactive server")));
                    }
                    if (_options.ReplMode && _options.Ports.Count < 1)
                    {
                        throw new Exception("Missing PORT for client mode session.");
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine();
                    System.Console.Write(string.Format("{0}: ", _programName));
                    System.Console.WriteLine(e.Message);
                    System.Console.WriteLine(string.Format("Try '{0} --help' for more information.", _programName));
                    return 1;
                }
                using (_jSession = new JSession())
                {
                    System.Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        _jSession.IncAdBreak();
                    };
                    if (_options.Interactive)
                    {
                        _jSession.SetStringOutput((tp, s) =>
                        {
                            if (JSession.MTYOEXIT == tp) Environment.Exit(tp);
                            System.Console.Out.Write(s); System.Console.Out.Flush();
                        });
                        _jSession.SetInput(JInput);
                        _jSession.SetType(JSession.SMCON);

                        _jSession.ApplyCallbacks();
                    }
                    int type;
                    if (_options.JOptions.Length == 1 && _options.JOptions[0] == "-jprofile")
                        type = 3;
                    else if (_options.JOptions.Length > 1 && _options.JOptions[0] == "-jprofile")
                        type = 1;
                    else
                        type = 0;
                    AddArgs();
                    JeFirst(type);
                    if (0 < _options.Ports.Count)
                    {
                        var threadCount = _options.Interactive ? _options.Ports.Count : _options.Ports.Count - 1;
                        for (int i = 0; i < threadCount; i++)
                        {
                            // create listening threads.
                        }
                        if (!_options.Interactive) // use main thread
                        {
                            if (_options.ReplMode)
                            {
                                ClientProc(_options.Ports[threadCount]);
                            }
                            else
                            {
                                ServerProc(_options.Ports[threadCount]);
                            }
                        }
                    }
                    while (_options.Interactive)
                    {
                        _jSession.Do(JInput("   "));
                    }
                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.Message);
                System.Console.Write("Press any key to continue... ");
                System.Console.ReadKey();
                return 1;
            }
        }

        private static string JInput(string prompt)
        {
            System.Console.Out.Write(prompt); System.Console.Out.Flush();
            var line = System.Console.In.ReadLine();
            if (null == line)
            {
                if (IsConsoleInputRedirected())
                {
                    return "2!:55''";
                }
                System.Console.Out.WriteLine(); System.Console.Out.Flush();
                _jSession.IncAdBreak();
            }
            else
            {
                _input = line;
            }
            return _input;
        }

        private static void AddArgs()
        {
            var sb = new StringBuilder();
            if (0 == _options.JOptions.Length)
            {
                sb.Append(",<");
            }
            sb.Append('\'');
            sb.Append(Assembly.GetExecutingAssembly().Location.Replace('\\', '/'));
            sb.Append('\'');
            foreach (var arg in _options.JOptions)
            {
                sb.Append(";'");
                sb.Append(arg.Replace("'", "''"));
                sb.Append('\'');
            }
            _input = sb.ToString();
        }

        private static int JeFirst(int type)
        {
            var init = new StringBuilder();
            if (0 == type)
            {
                init.Append("(3 : '0!:0 y')<BINPATH,'/profile.ijs'");
            }
            else if (1 == type)
                init.Append("(3 : '0!:0 y')2{ARGV");
            else if (2 == type)
                init.Append("11!:0'pc ijx closeok;xywh 0 0 300 200;cc e editijx rightmove bottommove ws_vscroll ws_hscroll;setfont e \"Courier New\" 12;setfocus e;pas 0 0;pgroup jijx;pshow;'[18!:4<'base'");
            else
                init.Append("i.0 0");
            init.Append("[ARGV_z_=:");
            init.Append(_input);
            init.Append("[BINPATH_z_=:'");
            string binPath = null;
            if (null == (binPath = Environment.GetEnvironmentVariable("JPATH")))
            {
                binPath = Assembly.GetExecutingAssembly().Location;
            }
            init.Append(Path.GetDirectoryName(binPath).Replace('\\', '/'));
            init.Append('\'');
            return _jSession.Do(init.ToString());
        }

        private static bool IsConsoleInputRedirected()
        {
#if NET40
            return FileType.Char != GetFileType(GetStdHandle(StdHandle.Stdin));
#else
            return System.Console.IsInputRedirected;
#endif
        }

#if NET40
        // P/Invoke:
        private enum FileType { Unknown, Disk, Char, Pipe };
        private enum StdHandle { Stdin = -10, Stdout = -11, Stderr = -12 };
        [DllImport("kernel32.dll")]
        private static extern FileType GetFileType(IntPtr hdl);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(StdHandle std);
#endif

        private static void ShowHelp(OptionSet optionSet)
        {
            System.Console.WriteLine(string.Format("Usage: {0} [OPTIONS]", _programName));
            System.Console.WriteLine(".NET based console for J language engine.");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(System.Console.Out);
            System.Console.WriteLine();
            System.Console.WriteLine("Usual jconsole options (-jprofile, -js, etc.) are supported unchanged.");
        }

        private static void ClientProc(ushort port)
        {
        }

        private static void ServerProc(ushort port)
        {
        }
    }

    internal class CmdLineOptions
    {
        internal readonly List<ushort> Ports = new List<ushort>();
        internal bool Interactive = true;
        internal bool Loopback = true;

        internal bool ReplMode = false;
        internal string LaunchFile = null;
        internal string Backend = null;
        internal bool EnableAttach = false;
        internal string Server = "127.0.0.1";

        internal string[] JOptions = null;
    }
}