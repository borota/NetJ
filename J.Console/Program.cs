using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using J.SessionManager;

namespace J.Console
{
    class Program
    {
        private static JSession jSession = null;
        private static string input = null;

        private static int Main(string[] args)
        {
            try
            {
                using (jSession = new JSession())
                {
                    System.Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        jSession.IncAdBreak();
                    };
                    jSession.SetOutput((jt, tp, s) =>
                    {
                        if (JSession.MTYOEXIT == tp) Environment.Exit(tp);
                        System.Console.Out.Write(s); System.Console.Out.Flush();
                    });
                    jSession.SetInput((jt, prompt) => JInput(jt, prompt));
                    jSession.SetType(JSession.SMCON);
                    jSession.ApplyCallbacks();
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
                        jSession.Do(JInput(IntPtr.Zero, "   "));
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
                jSession.IncAdBreak();
            }
            else
            {
                input = line;
            }
            return input;
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
            input = sb.ToString();
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
            init.Append(input);
            init.Append("[BINPATH_z_=:'");
            init.Append(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace('\\', '/'));
            init.Append('\'');
            return jSession.Do(init.ToString());
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
    }
}