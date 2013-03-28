using Nj;
using System;
using System.Text;

namespace NjConsole
{
    class Program
    {
        static JSession jsm = null;
        static StringBuilder input = null;

        static int Main(string[] args)
        {
            try
            {
                input = new StringBuilder();
                using (jsm = new JSession())
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        jsm.IncAdBreak();
                    };
                    jsm.SetOutput((jt, tp, s) =>
                    {
                        if (JSession.MTYOEXIT == tp) Environment.Exit(tp);
                        Console.Out.Write(s); Console.Out.Flush();
                    });
                    jsm.SetInput((jt, prompt) => Jinput(jt, prompt));
                    jsm.SetType(JSession.SMCON);
                    jsm.ApplyCallbacks();
                    int type;
                    if (args.Length == 1 && args[0] == "-jprofile")
                        type = 3;
                    else if (args.Length > 1 && args[0] == "-jprofile")
                        type = 1;
                    else
                        type = 0;
                    addargv(args);
                    jefirst(type);
                    while (true)
                    {
                        jsm.Do(Jinput(IntPtr.Zero, "   "));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.Write("Press any key to continue... ");
                Console.ReadKey();
                return 1;
            }
        }

        static string Jinput(IntPtr jt, string prompt)
        {
            Console.Out.Write(prompt); Console.Out.Flush();
            var line = Console.In.ReadLine();
            if (null == line)
            {
                if (Console.IsInputRedirected)
                {
                    return "2!:55''";
                }
                Console.Out.WriteLine(); Console.Out.Flush();
                jsm.IncAdBreak();
            }
            else {
                input = new StringBuilder(line);
            }
            return input.ToString();
        }

        static void addargv(string[] args)
        {
            input.Append(",<'");
            input.Append(System.Reflection.Assembly.GetExecutingAssembly().Location.Replace('\\', '/'));
            input.Append("'");
            bool firstArg = true;
            foreach (var arg in args)
            {
                if (firstArg)
                {
                    input.Append(",<");
                    firstArg = false;
                }
                input.Append(";'");
                input.Append(arg.Replace("'", "''"));
                input.Append('\'');
            }
        }

        static int jefirst(int type)
        {
            const string ijx = "11!:0'pc ijx closeok;xywh 0 0 300 200;cc e editijx rightmove bottommove ws_vscroll ws_hscroll;setfont e \"Courier New\" 12;setfocus e;pas 0 0;pgroup jijx;pshow;'[18!:4<'base'";
            var init = new StringBuilder();
            if (0 == type)
            {
                init.Append("(3 : '0!:0 y')<BINPATH,'");
                init.Append("\\");
                init.Append("profile.ijs'");
            }
            else if (1 == type)
                init.Append("(3 : '0!:0 y')2{ARGV");
            else if (2 == type)
                init.Append(ijx);
            else
                init.Append("i.0 0");
            init.Append("[ARGV_z_=:");
            init.Append(input);
            init.Append("[BINPATH_z_=:'");
            init.Append(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location).Replace('\\', '/'));
            init.Append("'");
            return jsm.Do(init.ToString());
        }
    }
}
