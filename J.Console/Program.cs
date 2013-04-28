using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using J.SessionManager;
using J.Wd;
using NDesk.Options;

namespace J.Console
{
    class Program
    {
        private static JSession _jSession = null;
        private static string _input = string.Empty;
        private static string _programName;
        private static CmdLineOptions _options;

        [STAThread]
        private static int Main(string[] argv)
        {
            try
            {
                System.Console.InputEncoding = (5 == Environment.OSVersion.Version.Major) ? Encoding.UTF8 : Encoding.Unicode;
                System.Console.OutputEncoding = Encoding.UTF8;
                _programName = JSession.ProgramName;
                _options = new CmdLineOptions();

                bool showHelp = false;
                bool isServer = false;
                var optionSet = new OptionSet()
                {
                    { "p|port:", "{PORT} to listen to. Multiple ports are supported.", (ushort v) => { _options.Ports.Add(v); isServer = true; } },
					{ "i|interactive", "Interactive session. Use -i- to turn off.", v => _options.Interactive = v != null },
                    { "l|loopback", "Listen on loopback. Use -l- to use network.\n", v => _options.Loopback = v != null },
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
                    if ((!_options.Interactive || !_options.Loopback) && _options.Ports.Count < 1)
                    {
                        throw new Exception(string.Format("Missing PORT for server session."));
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
                using (_jSession = new JSession(_options.JOptions))
                {
                    System.Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        _jSession.IncAdBreak();
                    };
                    _jSession.SetType(JSession.SMCON);
                    if (!isServer) // normal interactive console read/write to standard streams.
                    {
                        _jSession.SetStringOutput((tp, s) =>
                        {
                            if (JSession.MTYOEXIT == tp) Environment.Exit(tp);
                            System.Console.Out.Write(s); System.Console.Out.Flush();
                        });
                        _jSession.SetDoWd(Parser.Parse);
                        _jSession.SetInput(JInput);
                        _jSession.ApplyCallbacks();
                    }
                    if (isServer)
                    {
                        var threadCount = _options.Interactive ? _options.Ports.Count : _options.Ports.Count - 1;
                        for (int i = 0; i < threadCount; i++)
                        {
                            ServerProc(_options.Ports[i]);
                        }
                        if (!_options.Interactive) // use main thread
                        {
                            ServerProc(_options.Ports[threadCount]);
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            _jSession.Do(JInput("   "));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine();
                System.Console.Error.Write(string.Format("{0}: ", _programName));
                System.Console.Error.WriteLine(ex.ToString());
                if (!IsConsoleInputRedirected())
                {
                    System.Console.Error.Write("Press any key to continue... ");
                    System.Console.ReadKey();
                }
                return 1;
            }
            return 0;
        }

        private static string JInput(string prompt)
        {
            System.Console.Out.Write(prompt); System.Console.Out.Flush();
            var line = System.Console.ReadLine();
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

        private static void ServerProc(ushort port)
        {
            throw new NotImplementedException();
        }
    }

    internal class CmdLineOptions
    {
        internal readonly List<ushort> Ports = new List<ushort>();
        internal bool Interactive = true;
        internal bool Loopback = true;

        internal string[] JOptions = null;
    }
}