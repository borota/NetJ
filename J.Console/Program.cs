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
        private static Options _options;

        private static int Main(string[] argv)
        {
            try
            {
                _programName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                _options = new Options();

                bool showHelp = false;
                var optionSet = new OptionSet()
                {
                    { "p|port=", "{PORT} on which J server listens.", (ushort v) => _options.Ports.Add(v) },
					{ "n|non-interactive", "Start non-interactive server session.", v => _options.NonInteractive = v != null },
                    { "h|help",  "Show this message and exit.", v => showHelp = v != null }
                };

                string[] args = null;
                try
                {
                    args = optionSet.Parse(argv).ToArray();
                    if (showHelp)
                    {
                        ShowHelp(optionSet);
                        return 0;
                    }
                    if (_options.NonInteractive && _options.Ports.Count < 1)
                    {
                        throw new Exception("Missing port for non-interactive server session.");
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
                    _jSession.SetOutput((jt, tp, s) =>
                    {
                        if (JSession.MTYOEXIT == tp) Environment.Exit(tp);
                        System.Console.Out.Write(s); System.Console.Out.Flush();
                    });
                    _jSession.SetInput((jt, prompt) => JInput(jt, prompt));
                    _jSession.SetType(JSession.SMCON);
                    _jSession.ApplyCallbacks();
                    int type;
                    if (args.Length == 1 && args[0] == "-jprofile")
                        type = 3;
                    else if (args.Length > 1 && args[0] == "-jprofile")
                        type = 1;
                    else
                        type = 0;
                    AddArgs(args);
                    JeFirst(type);
                    while (true)
                    {
                        _jSession.Do(JInput(IntPtr.Zero, "   "));
                    }
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

        private static string JInput(IntPtr jt, string prompt)
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

        private static void AddArgs(string[] args)
        {
            var sb = new StringBuilder();
            if (0 == args.Length)
            {
                sb.Append(",<");
            }
            sb.Append('\'');
            sb.Append(Assembly.GetExecutingAssembly().Location.Replace('\\', '/'));
            sb.Append('\'');
            foreach (var arg in args)
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
            System.Console.WriteLine("Usual jconsole options (-jprofile, -js, etc.) are supported too.");
        }

        private class Options
        {
            internal readonly List<ushort> Ports = new List<ushort>();
            internal bool NonInteractive = false;
        }
    }
}