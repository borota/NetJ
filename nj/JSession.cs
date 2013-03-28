using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Nj
{
    public class JSession : IDisposable
    {
        #region - Field -

        private readonly int _sid;
        private readonly IntPtr[] _callbacks;
        private bool _disposed;

        // smoptions
        public const int SMWIN  = 0;  /* j.exe    Jwdw (Windows) front end */
        public const int SMJAVA = 2;  /* j.jar    Jwdp (Java) front end */
        public const int SMCON  = 3;  /* jconsole */

        // output type
        public const int MTYOFM   = 1;	/* formatted result array output */
        public const int MTYOER   = 2;	/* error output */
        public const int MTYOLOG  = 3;	/* output log */
        public const int MTYOSYS  = 4;	/* system assertion failure */
        public const int MTYOEXIT = 5;	/* exit */
        public const int MTYOFILE = 6;	/* output 1!:2[2 */

        #endregion

        #region - Constructor -

        public JSession()
        {
            this._sid = -1;
            this._callbacks = new IntPtr[5];
            this._callbacks[0] = this._callbacks[1] = this._callbacks[2] = this._callbacks[3] = this._callbacks[4] = IntPtr.Zero;
            IntPtr.Subtract(new IntPtr(), 2);
            this._disposed = false;
            this._sid = JInit(); // might throw exception
        }

        #endregion

        #region - Method -

        public void SetOutput(OutputType outputType)
        {
            this._callbacks[0] = Marshal.GetFunctionPointerForDelegate(outputType);
        }

        public void SetDoWd(DoWdType doWdType)
        {
            this._callbacks[1] = Marshal.GetFunctionPointerForDelegate(doWdType);
        }

        public void SetInput(InputType inputType)
        {
            this._callbacks[2] = Marshal.GetFunctionPointerForDelegate(inputType);
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
            if (this._sid > -1)
            {
                return JSession.JDo(this._sid, sentence);
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

        [DllImport(_dllName)]
        private static extern int JInit();

        [DllImport(_dllName)]
        private static extern int JSM(int sid, [In][MarshalAs(UnmanagedType.LPArray)] IntPtr[] callbacks);

        [DllImport(_dllName)]
        private static extern int JDo(int sid, [MarshalAs(UnmanagedType.LPStr)]string sentence);

        [DllImport(_dllName)]
        private static extern StringBuilder JGetLocale(int sid);

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
        public delegate void OutputType(IntPtr jt, int type, [MarshalAs(UnmanagedType.LPStr)]string s);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int DoWdType(IntPtr jt, int x, ref A parg, ref IntPtr press);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate string InputType(IntPtr jt, [MarshalAs(UnmanagedType.LPStr)]string prompt);

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

        #endregion
    }
}