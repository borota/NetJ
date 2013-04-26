/*
 * Used ideas from PTVS code licensed by Microsoft under Apache License, Version 2.0.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using J.SessionManager;

namespace J.VS.Client
{
    internal class ReplProcessor
    {
        private const string _debugReplEnv = "DEBUG_REPL";
        private static bool _isDebug = Environment.GetEnvironmentVariable(_debugReplEnv) != null;
        private CmdLineOptions _cmdLine;
        private Thread _replThread;

        private ReplProcessor(CmdLineOptions cmdLineOptions)
        {
            this._cmdLine = cmdLineOptions;
            this._replThread = null;
        }

        private static void DebugWrite(string msg)
        {
            if (_isDebug) { System.Console.Write(msg); System.Console.Out.Flush(); }
        }

        /// <summary>
        /// creates command bytes for sending out via sockets
        /// </summary>
        private static byte[] Cmd(string cmd)
        {
            return ReplProcessor.Utf8Enabled ? Encoding.UTF8.GetBytes(cmd) : Encoding.ASCII.GetBytes(cmd);
        }

        /// <summary>
        /// creates command string from bytes
        /// </summary>
        private static string Cmd(byte[] cmd)
        {
            return ReplProcessor.Utf8Enabled ? Encoding.UTF8.GetString(cmd) : Encoding.ASCII.GetString(cmd);
        }

        /// <summary>
        /// creates command string from count number of bytes
        /// </summary>
        private static string Cmd(byte[] cmd, int count)
        {
            return ReplProcessor.Utf8Enabled ? Encoding.UTF8.GetString(cmd, 0, count) : Encoding.ASCII.GetString(cmd, 0, count);
        }

        private static bool Utf8Enabled
        {
            get
            {
                return true;
            }
        }

        private static void ExitWorkItem()
        {
            Environment.Exit(0);
        }

        public class UnsupportedReplException : Exception
        {
            UnsupportedReplException(string reason)
            {
                this.Reason = reason;
            }

            public string Reason { get; private set; }
        }

        /// <summary>
        /// back end for executing REPL code.  This base class handles all of the 
        /// communication with the remote process while derived classes implement the 
        /// actual inspection and introspection.
        /// </summary>
        internal class ReplBackend : IDisposable
        {
            protected static readonly byte[] _MRES;
            protected static readonly byte[] _SRES;
            protected static readonly byte[] _LOCS;
            protected static readonly byte[] _IMGD;
            protected static readonly byte[] _PRPC;
            protected static readonly byte[] _RDLN;
            protected static readonly byte[] _STDO;
            protected static readonly byte[] _STDE;
            protected static readonly byte[] _DBGA;
            protected static readonly byte[] _DETC;
            protected static readonly byte[] _DPNG;
            protected static readonly byte[] _UNICODE_PREFIX;
            protected static readonly byte[] _ASCII_PREFIX;

            static ReplBackend()
            {
                _MRES = ReplProcessor.Cmd("MRES");
                _SRES = ReplProcessor.Cmd("SRES");
                _LOCS = ReplProcessor.Cmd("LOCS");
                _IMGD = ReplProcessor.Cmd("IMGD");
                _PRPC = ReplProcessor.Cmd("PRPC");
                _RDLN = ReplProcessor.Cmd("RDLN");
                _STDO = ReplProcessor.Cmd("STDO");
                _STDE = ReplProcessor.Cmd("STDE");
                _DBGA = ReplProcessor.Cmd("DBGA");
                _DETC = ReplProcessor.Cmd("DETC");
                _DPNG = ReplProcessor.Cmd("DPNG");
                _UNICODE_PREFIX = ReplProcessor.Cmd("U");
                _ASCII_PREFIX = ReplProcessor.Cmd("A");
            }

            protected readonly ReplProcessor _replProc;
            private readonly Dictionary<string, Action> _COMMANDS;
            protected readonly JSession _jSession;
            protected bool _disposed;

            private TcpClient _conn;
            /// <summary>
            /// TODO: Make sure stream is closed.
            /// </summary>
            protected NetworkStream _stream;
            private readonly object _sendLocker;
            private readonly object _inputLocker;
            private InputLock _inputEvent;
            private string _inputString;
            private bool _exitRequested;

#if DEBUG
            private Thread _sendLockedThread;
            private Thread _inputLockedThread;
#endif

            public ReplBackend(ReplProcessor replProcessor, JSession jSession)
            {
                this._replProc = replProcessor;
                this._COMMANDS = new Dictionary<string, Action>()
                {
                    { "run ", this.CmdRun },
                    { "abrt", this.CmdAbrt },
                    { "exit", this.CmdExit },
                    { "mems", this.CmdMems },
                    { "sigs", this.CmdSigs },
                    { "locs", this.CmdLocs },
                    { "setl", this.CmdSetl },
                    { "sett", this.CmdSett },
                    { "inpl", this.CmdInpl },
                    { "excf", this.CmdExcf },
                    { "dbga", this.CmdDebugAttach }
                };
                this._jSession = jSession;
                this._disposed = false;

                this._conn = null;
                this._stream = null;
                this._sendLocker = new object();
                this._inputLocker = new object();
                this._inputEvent = new InputLock(this); // lock starts acquired (we use it like a manual reset event)

                this._inputString = null;
                this._exitRequested = false;
            }

            ~ReplBackend()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!this._disposed)
                {
                    // If disposing equals true, dispose all managed and unmanaged resources. 
                    if (disposing)
                    {
                        // Dispose managed resources.
                        if (null != this._stream)
                        {
                            this._stream.Close();
                            this._stream = null;
                        }
                        if (null != this._conn)
                        {
                            this._conn.Close();
                            this._conn = null;
                        }
                    }
                    // Call the appropriate methods to clean up unmanaged resources here. 
                    // If disposing is false, only the following code is executed.

                    // Note disposing has been done.
                    this._disposed = true;
                }
            }

            internal void Connect()
            {
                this._conn = new TcpClient(this._replProc._cmdLine.Server, this._replProc._cmdLine.Port);
                this._stream = this._conn.GetStream();
                this._replProc._replThread = new Thread(this.ReplLoop); // start a new thread for communicating w/ the remote process
            }

            /// <summary>
            /// TODO: revisit this method to see if you have to close previous stream, etc.
            /// </summary>
            internal void Connect(TcpClient socket)
            {
                this._conn = socket;
                this._stream = this._conn.GetStream();
                this._replProc._replThread = new Thread(this.ReplLoop); // start a new thread for communicating w/ the remote process
            }

            /// <summary>
            /// loop on created thread which processes communicates with the REPL window
            /// </summary>
            private void ReplLoop()
            {
                try
                {
                    byte[] inp = new byte[4];
                    int size = 0;
                    while (true)
                    {
                        if (this.CheckForExitReplLoop())
                        {
                            break;
                        }
                        /* we receive a series of 4 byte commands. Each command then has it's
                           own format which we must parse before continuing to the next command. */
                        this.Flush();
                        this._stream.ReadTimeout = 10000; // 10 sec
                        try
                        {
                            size = this._stream.Read(inp, 0, 4);
                        }
                        catch (IOException ex)
                        {
                            SocketException se = null;
                            if (null != ex.InnerException && null != (se = ex.InnerException as SocketException) && SocketError.TimedOut == se.SocketErrorCode)
                            {
#if DEBUG
                                ReplProcessor.DebugWrite("Time out reading from server.");
#endif
                                continue;
                            }
                            throw;
                        }
                        this._stream.ReadTimeout = Timeout.Infinite;

                        if (size < 1)
                        {
                            break;
                        }
                        this.Flush();
                        Action cmd = null;
                        if (this._COMMANDS.TryGetValue(ReplProcessor.Cmd(inp), out cmd))
                        {
                            cmd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReplProcessor.DebugWrite("Error in ReplLoop");
                    ReplProcessor.DebugWrite(ex.ToString());
                    this.ExitProcess();
                    Thread.Sleep(2000); // try and exit gracefully, then interrupt main if necessary
                    Environment.Exit(1);
                    this.InterruptMain(); // will never happen
                }

            }

            protected virtual bool CheckForExitReplLoop()
            {
                return false;
            }

            private void Send(params string[] data)
            {
                if (null == data || 0 == data.Length) 
                {
                    return;
                }
                using (new SendLock(this))
                {
                    foreach (var d in data)
                    {
                        ReplProcessor.DebugWrite(d + '\n');
                        this._stream.Write(ReplProcessor.Cmd(d));
                    }
                }
            }

            private void Send(byte[] data)
            {
                if (null == data || 0 == data.Length)
                {
                    return;
                }
                using (new SendLock(this))
                {
                    this._stream.Write(data);
                }
            }

            /// <summary>
            /// reads length of text to read, and then the text encoded in UTF-8, and returns the string
            /// </summary>
            private string ReadString()
            {
                int strlen = this._stream.ReadInt32();
                if (strlen < 1 ) {
                    return string.Empty;
                }
                byte[] r = new byte[strlen];
                this._stream.Read(r, 0, strlen);
                return ReplProcessor.Utf8Enabled ? Encoding.UTF8.GetString(r) : Encoding.ASCII.GetString(r);
            }

            /// <summary>
            /// runs the received snippet of code
            /// </summary>
            private void CmdRun()
            {
                this.RunCommand(this.ReadString());
            }

            /// <summary>
            /// aborts the current running command
            /// </summary>
            private void CmdAbrt()
            {
                // abort command, interrupts execution of the main thread.
                this.InterruptMain();
            }

            /// <summary>
            /// exits the interactive process
            /// </summary>
            private void CmdExit()
            {
                this._exitRequested = true;
                this.ExitProcess();
            }

            /// <summary>
            /// gets the list of members available for the given expression
            /// </summary>
            private void CmdMems()
            {
                string expression = this.ReadString();
                MemberTuple memberTuple;
                try
                {
                    memberTuple = this.GetMembers(expression);
                }
                catch (Exception ex)
                {
                    this.Send("MERR");
                    ReplProcessor.DebugWrite("error in eval");
                    ReplProcessor.DebugWrite(ex.ToString());
                    return;
                }
                using(new SendLock(this))
                {
                    this._stream.Write(ReplBackend._MRES);
                    this.WriteString(memberTuple.Name);
                    this.WriteMemberDict(memberTuple.InstMembers);
                    this.WriteMemberDict(memberTuple.TypeMembers);
                }
            }

            /// <summary>
            /// gets the signatures for the given expression
            /// </summary>
            private void CmdSigs()
            {
                string expression = this.ReadString();
                object sigs = null;
                try
                {
                    // TODO
                    sigs = this.GetSignatures();
                }
                catch (Exception ex)
                {
                    this.Send("SERR");
                    ReplProcessor.DebugWrite("error in eval");
                    ReplProcessor.DebugWrite(ex.ToString());
                    return;
                }
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._SRES);
                    // TODO
                }
                
            }

            /// <summary>
            /// sets the current j locale which code will execute against
            /// </summary>
            private void CmdSetl()
            {
                string jLocale = this.ReadString();
                this.SetCurrentLocale(jLocale);
            }

            /// <summary>
            /// sets the current thread and frame which code will execute against
            /// </summary>
            private void CmdSett()
            {
                long threadId = this._stream.ReadInt64();
                long frameId = this._stream.ReadInt64();
                long frameKind = this._stream.ReadInt64();
                this.SetCurrentThreadAndFrame(threadId, frameId, frameKind);
            }

            /// <summary>
            /// gets the list of available locales
            /// </summary>
            private void CmdLocs()
            {
                List<LocaleTuple> locs = null;
                try
                {
                    locs = this.GetLocaleNames();
                    locs.Sort(new Comparison<LocaleTuple>((t1, t2) => t1.Name.CompareTo(t2.Name)));
                }
                catch
                {
                    locs = new List<LocaleTuple>();
                }
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._LOCS);
                    this._stream.WriteInt64(locs.Count);
                    foreach (var loc in locs)
                    {
                        this.WriteString(loc.Name);
                        this.WriteString(loc.FileName);
                    }
                }
            }

            /// <summary>
            /// handles the input command which returns a string of input
            /// </summary>
            private void CmdInpl()
            {
                this._inputString = this.ReadString();
                this._inputEvent.Dispose();
            }

            /// <summary>
            /// handles executing a single file
            /// </summary>
            private void CmdExcf()
            {
                string filename = this.ReadString();
                string args = this.ReadString();
                this.ExecuteFile(filename, args);
            }

            /// <summary>
            /// runs attach to process command
            /// </summary>
            private void CmdDebugAttach()
            {
                int port = this._stream.ReadInt32();
                string id = this.ReadString();
                this.AttachProcess(port, id);
            }

            private void WriteMemberDict(Dictionary<string, TypeTuple> memDict)
            {
                this._stream.WriteInt64(memDict.Count);
                foreach (TypeTuple mt in memDict.Values)
                {
                    this.WriteString(mt.Name);
                    this.WriteString(mt.TypeName);
                }
            }

            protected void WriteString(string str)
            {
                if (ReplProcessor.Utf8Enabled)
                {
                    this._stream.Write(ReplBackend._UNICODE_PREFIX);
                    this._stream.WriteUtf8String(str);
                }
                else
                {
                    this._stream.Write(ReplBackend._ASCII_PREFIX);
                    this._stream.WriteAsciiString(str);
                }
            }

            protected void OnDebuggerDetach()
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._DETC);
                }
            }

            protected void InitDebugger()
            {
                throw new NotImplementedException();
            }

            protected void SendImage(string filename)
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._IMGD);
                    this.WriteString(filename);
                }
            }

            protected void WritePng(byte[] imageBytes)
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._DPNG);
                    this._stream.WriteInt32(imageBytes.Length);
                    this._stream.Write(imageBytes);
                }
            }

            /// <summary>
            /// sends the current prompt to the interactive window
            /// </summary>
            protected void SendPrompt(string ps1, string ps2, bool updateAll = true)
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._PRPC);
                    this.WriteString(ps1);
                    this.WriteString(ps2);
                    this._stream.WriteInt32(updateAll ? 1 : 0);
                }
            }

            /// <summary>
            /// reports that an error occured to the interactive window
            /// </summary>
            protected void SendError()
            {
                this.Send("ERRE");
            }

            /// <summary>
            /// reports the that the REPL process has exited to the interactive window
            /// </summary>
            protected void SendExit()
            {
                this.Send("EXIT");
            }

            protected void SendCommandExecuted()
            {
                this.Send("DONE");
            }

            protected void SendLocalesChanged()
            {
                this.Send("LOCC");
            }

            /// <summary>
            /// reads a line of input from standard input
            /// </summary>
            protected string ReadLine()
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._RDLN);
                }
                this._inputEvent = new InputLock(this);
                return this._inputString;
            }

            /// <summary>
            /// writes a string to standard output in the remote console
            /// </summary>
            protected void WriteStdout(string value)
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._STDO);
                    this.WriteString(value);
                }
            }

            /// <summary>
            /// writes a string to standard error in the remote console
            /// </summary>
            protected void WriteSterr(string value)
            {
                using (new SendLock(this))
                {
                    this._stream.Write(ReplBackend._STDE);
                    this.WriteString(value);
                }
            }

            /// <summary>
            /// starts processing execution requests
            /// </summary>
            internal virtual void ExecutionLoop()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// runs the specified command which is a string containing code
            /// </summary>
            protected virtual void RunCommand(string command)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// executes the given filename in the base j locale
            /// </summary>
            protected virtual void ExecuteFile(string fileName, string args)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// aborts the current running command
            /// </summary>
            protected virtual void InterruptMain()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// exits the REPL process
            /// </summary>
            protected virtual void ExitProcess()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// returns a tuple of the type name, instance members, and type members
            /// </summary>
            protected virtual MemberTuple GetMembers(string expression)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// returns doc, args, vargs, varkw, defaults.
            /// </summary>
            protected virtual object GetSignatures()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// sets the j locale which code executes against
            /// </summary>
            protected virtual void SetCurrentLocale(string jLocale)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// sets the current thread and frame which code will execute against
            /// </summary>
            protected virtual void SetCurrentThreadAndFrame(long threadId, long frameId, long frameKind)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// returns a list of locale names
            /// </summary>
            protected virtual List<LocaleTuple> GetLocaleNames()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// flushes the stdout/stderr buffers
            /// </summary>
            protected virtual void Flush()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// starts processing execution requests
            /// </summary>
            protected virtual void AttachProcess(int port, string debuggerId)
            {
                throw new NotImplementedException();
            }

            protected class SendLock : IDisposable
            {
                private readonly ReplBackend _evaluator;

                public SendLock(ReplBackend evaluator)
                {
                    Monitor.Enter(evaluator._sendLocker);
#if DEBUG
                    Debug.Assert(evaluator._sendLockedThread == null);
                    evaluator._sendLockedThread = Thread.CurrentThread;
#endif
                    _evaluator = evaluator;
                }

                public void Dispose()
                {
#if DEBUG
                    _evaluator._sendLockedThread = null;
#endif
                    Monitor.Exit(_evaluator._sendLocker);
                }
            }

            protected class InputLock : IDisposable
            {
                private readonly ReplBackend _evaluator;

                public InputLock(ReplBackend evaluator)
                {
                    Monitor.Enter(evaluator._inputLocker);
#if DEBUG
                    Debug.Assert(evaluator._inputLockedThread == null);
                    evaluator._inputLockedThread = Thread.CurrentThread;
#endif
                    _evaluator = evaluator;
                }

                public void Dispose()
                {
#if DEBUG
                    _evaluator._inputLockedThread = null;
#endif
                    Monitor.Exit(_evaluator._inputLocker);
                }
            }

            protected struct MemberTuple
            {
                internal string Name;
                internal Dictionary<string, TypeTuple> InstMembers;
                internal Dictionary<string, TypeTuple> TypeMembers;

                internal MemberTuple(string name, Dictionary<string, TypeTuple> instMembers, Dictionary<string, TypeTuple> typeMember)
                {
                    this.Name = name;
                    this.InstMembers = instMembers;
                    this.TypeMembers = typeMember;
                }
            }

            protected struct TypeTuple
            {
                internal string Name;
                internal string TypeName;

                internal TypeTuple(string name, string typeName)
                {
                    this.Name = name;
                    this.TypeName = typeName;
                }
            }

            protected struct LocaleTuple
            {
                internal string Name;
                internal string FileName;

                internal LocaleTuple(string name, string fileName)
                {
                    this.Name = name;
                    this.FileName = fileName;
                }
            }
        }

        internal class BasicReplBackend : ReplBackend
        {
            private readonly object _executeItemLocker;
            private ExecuteItemLock _executeItemLock;
#if DEBUG
            private Thread _executeItemLockedThread;
#endif

            internal BasicReplBackend(ReplProcessor replProcessor, JSession jSession)
                : this(replProcessor, jSession, null)
            {
            }

            internal BasicReplBackend(ReplProcessor replProcessor, JSession jSession, string locale)
                : base(replProcessor, jSession)
            {
                this._executeItemLocker = new object();

                if (null != locale && string.Empty != locale.Trim())
                {
                    this._jSession.Do(string.Format("18!:4 <'{0}'", locale));
                }
                this._executeItemLock = new ExecuteItemLock(this); // lock starts acquired (we use it like manual reset event)
            }

            internal void InitConnection()
            {

            }

            /// <summary>
            /// loop on the main thread which is responsible for executing code
            /// </summary>
            internal override void ExecutionLoop()
            {
                this.SendPrompt("    ", "");
                if (null != this._replProc._cmdLine.LaunchFile)
                {
                    try
                    {
                        runFileAsBase();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("error in launching startup script:");
                        System.Console.WriteLine(ex.ToString());
                    }
                }
                while (true)
                {
                }
            }

            private void runFileAsBase()
            {
                string fileContent = System.IO.File.ReadAllText(this._replProc._cmdLine.LaunchFile).Replace("\r\n", "\n");
                _jSession.Do(fileContent);
            }

            internal void InitDebugger()
            {
            }

            protected class ExecuteItemLock : IDisposable
            {
                private readonly BasicReplBackend _evaluator;

                public ExecuteItemLock(BasicReplBackend evaluator)
                {
                    Monitor.Enter(evaluator._executeItemLocker);
#if DEBUG
                    Debug.Assert(evaluator._executeItemLockedThread == null);
                    evaluator._executeItemLockedThread = Thread.CurrentThread;
#endif
                    _evaluator = evaluator;
                }

                public void Dispose()
                {
#if DEBUG
                    _evaluator._executeItemLockedThread = null;
#endif
                    Monitor.Exit(_evaluator._executeItemLocker);
                }
            }
        }

        private void RunRepl(JSession jSession)
        {
            BasicReplBackend backendType = null;
            string backendError = null;
            if (null != this._cmdLine.Backend && !this._cmdLine.Backend.Equals("standard", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    backendType = (BasicReplBackend)Activator.CreateInstance(Type.GetType(this._cmdLine.Backend), this, jSession);
                }
                catch (UnsupportedReplException ex)
                {
                    backendError = ex.Reason;
                }
                catch (Exception ex)
                {
                    backendError = ex.Message;
                }
            }
            if (null == backendType)
            {
                backendType = new BasicReplBackend(this, jSession);
            }
            using (backendType)
            {
                backendType.Connect();
                if (this._cmdLine.EnableAttach)
                {
                    backendType.InitDebugger();
                }

                if (null != backendError)
                {
                    System.Console.Error.WriteLine("Error using selected REPL back-end:");
                    System.Console.Error.WriteLine(backendError);
                    System.Console.Error.WriteLine("Using standard backend instead.");
                }
                backendType.ExecutionLoop();                
            }
        }

        internal static void Run(CmdLineOptions cmdLineOptions, JSession jSession)
        {
            try
            {
                new ReplProcessor(cmdLineOptions).RunRepl(jSession);
            }
            catch (Exception ex)
            {
                if (ReplProcessor._isDebug)
                {
                    System.Console.WriteLine(ex.ToString());
                }
                throw;
            }
        }
    }
}
