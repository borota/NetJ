using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace J.SessionManager
{
    public class JSession : IDisposable
    {
        #region - Field -

        public static readonly string ProgramName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        public static readonly string ProgramPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

        private readonly int _sid;
        private readonly IntPtr[] _callbacks;
        private readonly string[] _jOptions;
        private bool _disposed;
        private const int _maxInput = 30000;
        private byte[] _byteInput;
        private IntPtr _ptrInput;

        // smoptions
        public const int SMWIN = 0;  /* j.exe    Jwdw (Windows) front end */
        public const int SMJAVA = 2;  /* j.jar    Jwdp (Java) front end */
        public const int SMCON = 3;  /* jconsole */

        // output type
        public const int MTYOFM = 1;	/* formatted result array output */
        public const int MTYOER = 2;	/* error output */
        public const int MTYOLOG = 3;	/* output log */
        public const int MTYOSYS = 4;	/* system assertion failure */
        public const int MTYOEXIT = 5;	/* exit */
        public const int MTYOFILE = 6;	/* output 1!:2[2 */

        #endregion

        #region - Constructor -

        public JSession() : this(new string[0])
        {
        }

        public JSession(string[] jOptions)
        {
            this._jOptions = jOptions;
            this._sid = -1;
            this._callbacks = new IntPtr[5];
            this._callbacks[0] = this._callbacks[1] = this._callbacks[2] = this._callbacks[3] = this._callbacks[4] = IntPtr.Zero;
            this._disposed = false;
            this._sid = JInit(); // might throw exception
            this.JeFirst();
        }

        #endregion

        #region - Property -

        public string LastSentence { get; private set; }

        #endregion

        #region - Method -

        public void SetStringOutput(StringOutputType outputType)
        {
            this._callbacks[0] = Marshal.GetFunctionPointerForDelegate(((InteropOutputType)((jt, type, output) => {
                string o = null;
                if (null != output && IntPtr.Zero != output)
                {
                    byte[] bo = this.BytesFromPtr(output);
                    o = Encoding.UTF8.GetString(bo);
                }
                outputType(type, o);
            })));
        }

        public void SetByteOutput(ByteOutputType outputType)
        {
            this._callbacks[0] = Marshal.GetFunctionPointerForDelegate(((InteropOutputType)((jt, type, output) =>
            {
                byte[] o = null;
                if (null != output && IntPtr.Zero != output)
                {
                    o = this.BytesFromPtr(output);
                }
                outputType(type, o);
            })));
        }

        private byte[] BytesFromPtr(IntPtr ptr)
        {
            var data = new List<byte>();
            var off = 0; byte ch;
            while (0 != (ch = Marshal.ReadByte(ptr, off++)))
            {
                data.Add(ch);
            }
            return data.ToArray();
        }

        public void SetDoWd(DoWdType doWdType)
        {
            this._callbacks[1] = Marshal.GetFunctionPointerForDelegate((InteropDoWdType)((IntPtr jt, int t, IntPtr w, ref IntPtr z) => doWdType(t, w, ref z)));
        }

        /// <summary>
        /// inputType callback is called after an Explicit Definition :
        /// </summary>
        public void SetInput(InputType inputType)
        {
            this._callbacks[2] = Marshal.GetFunctionPointerForDelegate((InteropInputType)((jt, prompt) =>
                {
                    if (null == this._ptrInput || IntPtr.Zero == this._ptrInput)
                    {
                        this._byteInput = new byte[_maxInput];
                        this._ptrInput = Marshal.AllocHGlobal(_maxInput + 1);
                    }
                    string inp = inputType(prompt);
                    int byteCnt = Encoding.UTF8.GetBytes(inp, 0, inp.Length, this._byteInput, 0);
                    Marshal.Copy(this._byteInput, 0, this._ptrInput, byteCnt);
                    Marshal.WriteByte(this._ptrInput, byteCnt, 0); // null terminated
                    return this._ptrInput;
                }));
        }

        public void SetType(int type)
        {
            this._callbacks[4] = new IntPtr(type); // TODO come back and verify if this is kosher
        }

        public int ApplyCallbacks()
        {
            if (this._sid > -1)
            {
                return JSession.JSM(this._sid, this._callbacks);
            }
            return this._sid;
        }

        public int Do(string sentence)
        {
            this.LastSentence = sentence;
            if (this._sid > -1)
            {
                if (null == sentence) return 0;
                return JSession.JDo(this._sid, Encoding.UTF8.GetBytes(sentence));
            }
            return this._sid;
        }

        public int Free()
        {
            if (this._sid > -1)
            {
                return JSession.JFree(this._sid);
            }
            return this._sid;
        }

        public byte IncAdBreak()
        {
            if (this._sid > -1)
            {
                return JSession.JIncAdBreak(this._sid);
            }
            return 0;
        }

        private string AddArgs()
        {
            var sb = new StringBuilder();
            if (0 == this._jOptions.Length)
            {
                sb.Append(",<");
            }
            sb.Append('\'');
            sb.Append(JSession.ProgramPath.Replace('\\', '/'));
            sb.Append('\'');
            foreach (var arg in this._jOptions)
            {
                sb.Append(";'");
                sb.Append(arg.Replace("'", "''"));
                sb.Append('\'');
            }
            return sb.ToString();
        }

        private int JeFirst()
        {
            var init = new StringBuilder();
            if (this._jOptions.Length == 1 && this._jOptions[0] == "-jprofile") 
            {
                init.Append("i.0 0");
            }
            else if (this._jOptions.Length > 1 && this._jOptions[0] == "-jprofile") 
            {
                init.Append("(3 : '0!:0 y')2{ARGV");
            }
            else
            {
                init.Append("(3 : '0!:0 y')<BINPATH,'/profile.ijs'");
            }
                
            init.Append("[ARGV_z_=:");
            init.Append(AddArgs());
            init.Append("[BINPATH_z_=:'");
            string binPath = null;
            if (null == (binPath = Environment.GetEnvironmentVariable("JPATH")))
            {
                binPath = Assembly.GetExecutingAssembly().Location;
            }
            init.Append(Path.GetDirectoryName(binPath).Replace('\\', '/'));
            init.Append('\'');
            return this.Do(init.ToString());
        }

        #region - IDispose -

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources.
                    Marshal.FreeHGlobal(this._ptrInput);
                }
                this.Free();
                this._disposed = true;
            }
        }

        ~JSession()
        {
            Dispose(false);
        }

        #endregion

        #endregion

        // TODO PInvokes need some more tweaking

        #region - DllImport -

        private const string _dllName = "jsm.dll";

        /// Return Type: int
        [DllImport(_dllName, EntryPoint = "JInit", CallingConvention = CallingConvention.StdCall)]
        public static extern int JInit();

        /// Return Type: int
        /// sid: int
        /// callback: void**
        [DllImport(_dllName, EntryPoint = "JSM", CallingConvention = CallingConvention.StdCall)]
        public static extern int JSM([In]int sid, [In][MarshalAs(UnmanagedType.LPArray)]IntPtr[] callback);

        /// Return Type: int
        /// sid: int
        /// sentence: C*
        [DllImport(_dllName, EntryPoint = "JDo", CallingConvention = CallingConvention.StdCall)]
        public static extern int JDo([In]int sid, [In]byte[] sentence);

        [DllImport(_dllName)]
        private static extern string JGetLocale(int sid);

        [DllImport(_dllName)]
        private static extern System.IntPtr JGetA(int sid, int n, [MarshalAs(UnmanagedType.LPStr)]string name);

        [DllImport(_dllName)]
        private static extern int JGetM(int sid, [MarshalAs(UnmanagedType.LPStr)]string name, ref int jtype, ref int jrank, ref int jshape, ref int jdata);

        [DllImport(_dllName)]
        private static extern int JSetA(int sid, int n, [MarshalAs(UnmanagedType.LPStr)]string name, int dlen, StringBuilder d);

        [DllImport(_dllName)]
        private static extern int JSetM(int sid, [MarshalAs(UnmanagedType.LPStr)]string name, ref int jtype, ref int jrank, ref int jshape, ref int jdata);

        [DllImport(_dllName)]
        private static extern System.IntPtr Jga(int sid, int t, int n, int r, ref int s);

        [DllImport(_dllName)]
        private static extern int JErrorTextM(int sid, int ec, ref int p);

        [DllImport(_dllName)]
        private static extern int JFree(int sid);

        [DllImport(_dllName)]
        private static extern System.IntPtr JGetJt(int sid);

        [DllImport(_dllName)]
        private static extern byte JIncAdBreak(int idx);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void InteropOutputType([In]IntPtr jt, [In]int type, [In]IntPtr output);
        
        public delegate void StringOutputType(int type, string output);

        public delegate void ByteOutputType(int type, byte[] output);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int InteropDoWdType([In]IntPtr jt, [In]int t, IntPtr w, ref IntPtr z);

        public delegate int DoWdType(int x, IntPtr w, ref IntPtr press);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr InteropInputType([In]IntPtr jt, [In][MarshalAs(UnmanagedType.LPStr)]string prompt);

        public delegate string InputType(string prompt);
        #endregion
    }

#if WIN64
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct A
    {
        public long k;
        public long flag;
        public long m;
        public long t;
        public long c;
        public long n;
        public long r;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.I8)]
        public long[] s;
    }
#else
        [StructLayout(LayoutKind.Sequential)]
        public struct A
        {
            public int k;
            public int flag;
            public int m;
            public int t;
            public int c;
            public int n;
            public int r;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.I4)]
            public int[] s;
        }
#endif
}