/*
 * Based on code from PTVS licensed by Microsoft under Apache License, Version 2.0.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using J.SessionManager;

namespace J.Console
{
    internal class ReplProcessor
    {
        private const string _debugReplEnv = "DEBUG_REPL";
        private static bool _isDebug = Environment.GetEnvironmentVariable(_debugReplEnv) != null;
        private CmdLineOptions _cmdLineOptions;
        private Thread _replThread;

        private ReplProcessor(CmdLineOptions cmdLineOptions)
        {
            this._cmdLineOptions = cmdLineOptions;
            this._replThread = null;
        }

        private static void DebugWrite(string msg)
        {
            if (_isDebug)
            {
                System.Console.Write(msg);
                System.Console.Out.Flush();
            }
        }

        private static byte[] Cmd(string cmd)
        {
            return Encoding.ASCII.GetBytes(cmd);
        }

        private static string Cmd(byte[] cmd)
        {
            return Encoding.ASCII.GetString(cmd);
        }

        private static string Cmd(byte[] cmd, int count)
        {
            return Encoding.ASCII.GetString(cmd, 0, count);
        }

        private static bool IsUnicode()
        {
            return false;
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
        public class ReplBackend
        {
            protected static readonly byte[] _MRES;
            protected static readonly byte[] _SRES;
            protected static readonly byte[] _MODS;
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
                _MODS = ReplProcessor.Cmd("MODS");
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
            private TcpClient _conn;
            /// <summary>
            /// TODO: Make sure stream is closed.
            /// </summary>
            protected NetworkStream _stream;
            protected JSession _jSession;
            private string _inputString;
            private bool _exitRequested;
            private readonly Dictionary<string, Action> _COMMANDS;
            private readonly object _socketLock = new object();
#if DEBUG
            private Thread _socketLockedThread;
#endif

            public ReplBackend(ReplProcessor replProcessor)
            {
                this._replProc = replProcessor;

                this._COMMANDS = new Dictionary<string, Action>()
                {
                    { "run ", this.CmdRun },
                    { "abrt", this.CmdAbrt },
                    { "exit", this.CmdExit },
                    { "mems", this.CmdMems },
                    { "sigs", this.CmdSigs },
                    { "mods", this.CmdMods },
                    { "setm", this.CmdSetm },
                    { "sett", this.CmdSett },
                    { "inpl", this.CmdInpl },
                    { "excf", this.CmdExcf },
                    { "dbga", this.CmdDebugAttach }
                };

                this._conn = null;
                this._stream = null;
                this._inputString = null;
                this._exitRequested = false;
            }

            internal void Connect()
            {
                this._conn = new TcpClient(this._replProc._cmdLineOptions.Server, this._replProc._cmdLineOptions.Ports[0]);
                this._stream = this._conn.GetStream();
                this._replProc._replThread = new Thread(this.ReplLoop); // start a new thread for communicating w/ the remote process
            }

            /// <summary>
            /// TODO: revisit this method to see if you have to close previous stream, etc.
            /// </summary>
            protected void Connect(TcpClient socket)
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
                        this._conn.ReceiveTimeout = 10000; // 10 sec
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
                        this._conn.ReceiveTimeout = 0;
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
                using (new SocketLock(this))
                {
                    foreach (var d in data)
                    {
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
                using (new SocketLock(this))
                {
                    this._stream.Write(data);
                }
            }

            /// <summary>
            /// reads length of text to read, and then the text encoded in UTF-8, and returns the string
            /// </summary>
            private string ReadString()
            {
                byte[] r = new byte[this._stream.ReadInt32()];
                this._stream.Read(r);
                return Encoding.ASCII.GetString(r);
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
                using(new SocketLock())
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
                throw new NotImplementedException();
            }

            /// <summary>
            /// sets the current module which code will execute against
            /// </summary>
            private void CmdSetm()
            {
                string modName = this.ReadString();
                this.SetCurrentModule(modName);
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
            /// gets the list of available modules
            /// </summary>
            private void CmdMods()
            {
                List<ModuleTuple> mods = null;
                try
                {
                    mods = this.GetModuleNames();
                    mods.Sort(new Comparison<ModuleTuple>((t1, t2) => t1.Name.CompareTo(t2.Name)));
                }
                catch
                {
                    mods = new List<ModuleTuple>();
                }
                using (new SocketLock())
                {
                    this._stream.Write(ReplBackend._MODS);
                    this._stream.WriteInt64(mods.Count);
                    foreach (var mod in mods)
                    {
                        this.WriteString(mod.Name);
                        this.WriteString(mod.FileName);
                    }
                }
            }

            /// <summary>
            /// handles the input command which returns a string of input
            /// </summary>
            private void CmdInpl()
            {
                this._inputString = this.ReadString();
                //self.input_event.release()
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
                throw new NotImplementedException();
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
            /// executes the given filename as the main module
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
            /// sets the module which code executes against
            /// </summary>
            protected virtual void SetCurrentModule(string moduleName)
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
            /// returns a list of module names
            /// </summary>
            protected virtual List<ModuleTuple> GetModuleNames()
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
            protected virtual void AttachProcess(ushort port, int debuggerId)
            {
                throw new NotImplementedException();
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
                if (ReplProcessor.IsUnicode())
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

            /// <summary>
            /// Helper struct for locking and tracking the current holding thread.  This allows
            /// us to assert that our socket is always accessed while the lock is held.  The lock
            /// needs to be held so that requests from the UI (switching scopes, getting module lists,
            /// executing text, etc...) won't become interleaved with interactions from the repl process 
            /// (output, execution completing, etc...).
            /// </summary>
            protected struct SocketLock : IDisposable
            {
                private readonly ReplBackend _evaluator;

                public SocketLock(ReplBackend evaluator)
                {
                    Monitor.Enter(evaluator._socketLock);
#if DEBUG
                    Debug.Assert(evaluator._socketLockedThread == null);
                    evaluator._socketLockedThread = Thread.CurrentThread;
#endif
                    _evaluator = evaluator;
                }

                public void Dispose()
                {
#if DEBUG
                    _evaluator._socketLockedThread = null;
#endif
                    Monitor.Exit(_evaluator._socketLock);
                }
            }

            /// <summary>
            /// Releases the socket lock and re-acquires it when finished.  This enables
            /// calling back into the repl window which could potentially call back to do
            /// work w/ the evaluator that we don't want to deadlock.
            /// </summary>
            protected struct SocketUnlock : IDisposable
            {
                private readonly ReplBackend _evaluator;

                public SocketUnlock(ReplBackend evaluator)
                {
#if DEBUG
                    Debug.Assert(evaluator._socketLockedThread == Thread.CurrentThread);
                    evaluator._socketLockedThread = null;
#endif
                    _evaluator = evaluator;
                    Monitor.Exit(evaluator._socketLock);
                }

                public void Dispose()
                {
                    Monitor.Enter(_evaluator._socketLock);
#if DEBUG
                    _evaluator._socketLockedThread = Thread.CurrentThread;
#endif
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

            protected struct ModuleTuple
            {
                internal string Name;
                internal string FileName;

                internal ModuleTuple(string name, string fileName)
                {
                    this.Name = name;
                    this.FileName = fileName;
                }
            }
        }

        class BasicReplBackend : ReplBackend
        {
            internal BasicReplBackend(ReplProcessor replProcessor)
                : base(replProcessor)
            {
            }

            /// <summary>
            /// loop on the main thread which is responsible for executing code
            /// </summary>
            internal override void ExecutionLoop()
            {
                this.SendPrompt("    ", "");
                using (_jSession = new JSession())
                {
                    if (null != this._replProc._cmdLineOptions.LaunchFile)
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
            }

            /// <summary>
            /// sends the current prompt to the interactive window
            /// </summary>
            internal void SendPrompt(string ps1, string ps2, bool updateAll = true)
            {
                using (new SocketLock())
                {
                    this._stream.Write(ReplBackend._PRPC);
                    this.WriteString(ps1);
                    this.WriteString(ps2);
                    this._stream.WriteInt64(updateAll ? 1 : 0);
                }
            }

            private void runFileAsBase()
            {
                string fileContent = System.IO.File.ReadAllText(this._replProc._cmdLineOptions.LaunchFile).Replace("\r\n", "\n");
                _jSession.Do(fileContent);
            }

            internal void InitDebugger()
            {
            }
        }

        private void RunRepl()
        {
            BasicReplBackend backendType = null;
            string backendError = null;
            if (null != this._cmdLineOptions.Backend && !this._cmdLineOptions.Backend.Equals("standard", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    backendType = (BasicReplBackend)Activator.CreateInstance(Type.GetType(this._cmdLineOptions.Backend), this);
                }
                catch (UnsupportedReplException ex)
                {
                    backendError = ex.Reason;
                }
                catch (Exception ex)
                {
                    backendError = ex.ToString();
                }
            }
            if (null == backendType)
            {
                backendType = new BasicReplBackend(this);
            }
            backendType.Connect();
            if (this._cmdLineOptions.EnableAttach)
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

        internal static void Run(CmdLineOptions cmdLineOptions)
        {
            try
            {
                var proc = new ReplProcessor(cmdLineOptions);
                proc.RunRepl();
            }
            catch (Exception ex)
            {
                if (ReplProcessor._isDebug)
                {
                    System.Console.WriteLine(ex.ToString());
                    System.Console.Write("Press a key to exit...");
                    System.Console.ReadKey(true);
                }
                throw;
            }
        }
    }
}
