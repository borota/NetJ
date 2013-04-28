using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace J.SessionManager
{
    public class JSession : IDisposable
    {
        #region - Field -

        public static readonly string ProgramName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        public static readonly string ProgramPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        public static string JeFullName { get; private set; }
        public const string JeName = "j.dll";
        /// <summary>
        /// Warning: Usually it's very dangerous to unload dlls in code. No need to worry about unloading j.dll, OS should take care of it.
        /// </summary>
        public static readonly UnmanagedLibrary JeLibrary;

        private readonly JtHandle _jt;
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

        static JSession() // static constructor is always safe thread-wise
        {
            if (JSession.IsJeLoaded()) return;
            if (null == (JSession.JeFullName = Environment.GetEnvironmentVariable("JEPATH")))
            {
                JSession.JeFullName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), JSession.JeName);
            }
            JSession.JeLibrary = new UnmanagedLibrary(JSession.JeFullName);
            JSession.JInit = JSession.JeLibrary.GetUnmanagedFunction<JInitType>("JInit");
            JSession.JSM = JSession.JeLibrary.GetUnmanagedFunction<JSMType>("JSM");
            JSession.JDo = JSession.JeLibrary.GetUnmanagedFunction<JDoType>("JDo");
            JSession.JFree = JSession.JeLibrary.GetUnmanagedFunction<JFreeType>("JFree");
        }

        public JSession() : this(new string[0])
        {
        }

        public JSession(string[] jOptions)
        {
            this._disposed = false;
            this._jOptions = jOptions;
            this._callbacks = new IntPtr[5];
            this._callbacks[0] = this._callbacks[1] = this._callbacks[2] = this._callbacks[3] = this._callbacks[4] = IntPtr.Zero;
            this._jt = JInit(); // might throw exception
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
            this._callbacks[1] = Marshal.GetFunctionPointerForDelegate((InteropDoWdType)(
                (IntPtr jt, int t, IntPtr w, ref IntPtr z) => doWdType(t, w, ref z)));
        }

        /// <summary>
        /// inputType callback is called after an Explicit Definition :
        /// </summary>
        public void SetInput(InputType inputType)
        {
            this._callbacks[2] = Marshal.GetFunctionPointerForDelegate((InteropInputType)((jt, prompt) =>
                {
                    if (IntPtr.Zero == this._ptrInput)
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

        public void ApplyCallbacks()
        {
            JSession.JSM(this._jt, this._callbacks);
        }

        public int Do(string sentence)
        {
            this.LastSentence = sentence;
            return (null == sentence ? 0 : JSession.JDo(this._jt, Encoding.UTF8.GetBytes(sentence)));
        }

        public byte IncAdBreak()
        {
            IntPtr jt = this._jt.DangerousGetHandle();
            IntPtr adadbreak = Marshal.ReadIntPtr(jt); // first address in jt is address of breakdata;
            byte adadbreakValue = Marshal.ReadByte(adadbreak);
            Marshal.WriteByte(adadbreak, ++adadbreakValue);
            return adadbreakValue;
            /*
             * char** adadbreak;
             * adadbreak=(char**)jt; // first address in jt is address of breakdata
             * **adadbreak+=1
             */
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
            init.Append(Path.GetDirectoryName(JSession.JeFullName));
            init.Append('\'');
            return this.Do(init.ToString());
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static int Free(IntPtr jt) 
        {
            return (null == JSession.JFree) ? 0 : JSession.JFree(jt);
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
                if (!this._jt.IsClosed)
                {
                    this._jt.Close();
                }
                this._disposed = true;
            }
        }

        ~JSession()
        {
            Dispose(false);
        }

        #endregion

        #endregion

        #region - Private -

        private static bool IsJeLoaded()
        {
            // Get the module in the process according to the module name.
            IntPtr hMod = GetModuleHandle(JSession.JeName);
            return (hMod != IntPtr.Zero);
        }

        #endregion

        #region - PInvoke -

        /// Return Type: void*
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate JtHandle JInitType();
        private static JInitType JInit;

        /// Return Type: void
        ///jt: void*
        ///callback: void**
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void JSMType([In]JtHandle jt, [In][MarshalAs(UnmanagedType.LPArray)]IntPtr[] callback);
        private static JSMType JSM;


        /// Return Type: int
        ///jt: void*
        ///sentence: C*
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int JDoType([In]JtHandle jt,  [In]byte[] sentence);
        private static JDoType JDo;

        /// Return Type: int
        ///jt: void*
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int JFreeType([In]IntPtr jt);        
        private static JFreeType JFree;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void InteropOutputType([In]IntPtr jt, [In]int type, [In]IntPtr output);
        public delegate void StringOutputType(int type, string output);
        public delegate void ByteOutputType(int type, byte[] output);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int InteropDoWdType([In]IntPtr jt, [In]int t, IntPtr w, ref IntPtr z);
        public delegate int DoWdType(int x, IntPtr w, ref IntPtr press);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr InteropInputType([In]IntPtr jt, [In][MarshalAs(UnmanagedType.LPStr)]string prompt);
        public delegate string InputType(string prompt);

        /*

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
private static extern System.IntPtr JGetJt(int sid);*/

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string moduleName);

        #endregion

        #region - SafeJtHandle -

        /// <summary>
        /// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ 
        /// for more about safe handles.
        /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        sealed class JtHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            /// <summary>
            /// Create safe library handle
            /// </summary>
            private JtHandle() : base(true) { }

            /// <summary>
            /// Release handle
            /// </summary>
            protected override bool ReleaseHandle()
            {
                return 0 == JSession.Free(handle);
            }
        }

        /// <summary>
        /// Native methods
        /// </summary>
        static class NativeMethod
        {
            [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern JtHandle LoadLibrary(string fileName);

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32", SetLastError = true, EntryPoint = "GetProcAddress")]
            public static extern IntPtr GetProcAddress(JtHandle hModule,
                String procname);
        }

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

/******************************** Module Header ********************************\
* Module Name:  UnmanagedLibrary.cs
* Project:      CSLoadLibrary
* Copyright (c) Microsoft Corporation.
* 
* The source code of UnmanagedLibrary is quoted from Mike Stall's article:
* 
* Type-safe Managed wrappers for kernel32!GetProcAddress
* http://blogs.msdn.com/jmstall/archive/2007/01/06/Typesafe-GetProcAddress.aspx
* http://blogs.msdn.com/b/jonathanswift/archive/2006/10/03/dynamically-calling-an-unmanaged-dll-from-.net-_2800_c_23002900_.aspx
* 
* This source is subject to the Microsoft Public License.
* See http://www.microsoft.com/en-us/openness/resources/licenses.aspx#MPL.
* All other rights reserved.
* 
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
* EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
* WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\*******************************************************************************/

    /// <summary>
    /// Utility class to wrap an unmanaged DLL and be responsible for freeing it.
    /// </summary>
    /// <remarks>
    /// This is a managed wrapper over the native LoadLibrary, GetProcAddress, 
    /// and FreeLibrary calls.
    /// </example>
    /// <see cref=
    /// "http://blogs.msdn.com/jmstall/archive/2007/01/06/Typesafe-GetProcAddress.aspx"
    /// />
    public sealed class UnmanagedLibrary : IDisposable
    {
        #region Safe handles and Native imports

        /// <summary>
        /// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ 
        /// for more about safe handles.
        /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            /// <summary>
            /// Create safe library handle
            /// </summary>
            private SafeLibraryHandle() : base(true) { }

            /// <summary>
            /// Release handle
            /// </summary>
            protected override bool ReleaseHandle()
            {
                return NativeMethod.FreeLibrary(handle);
            }
        }

        /// <summary>
        /// Native methods
        /// </summary>
        static class NativeMethod
        {
            [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafeLibraryHandle LoadLibrary(string fileName);

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32", SetLastError = true, EntryPoint = "GetProcAddress")]
            public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule,
                String procname);
        }

        #endregion


        /// <summary>
        /// Constructor to load a dll and be responsible for freeing it.
        /// </summary>
        /// <param name="fileName">full path name of dll to load</param>
        /// <exception cref="System.IO.FileNotFoundException">
        /// If fileName can't be found
        /// </exception>
        /// <remarks>
        /// Throws exceptions on failure. Most common failure would be 
        /// file-not-found, or that the file is not a loadable image.
        /// </remarks>
        public UnmanagedLibrary(string fileName)
        {
            m_hLibrary = NativeMethod.LoadLibrary(fileName);
            if (m_hLibrary.IsInvalid)
            {
                int hr = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <summary>
        /// Dynamically lookup a function in the dll via kernel32!GetProcAddress.
        /// </summary>
        /// <param name="functionName">
        /// raw name of the function in the export table.
        /// </param>
        /// <returns>
        /// null if function is not found. Else a delegate to the unmanaged 
        /// function.
        /// </returns>
        /// <remarks>
        /// GetProcAddress results are valid as long as the dll is not yet 
        /// unloaded. This is very very dangerous to use since you need to 
        /// ensure that the dll is not unloaded until after you're done with any 
        /// objects implemented by the dll. For example, if you get a delegate 
        /// that then gets an IUnknown implemented by this dll, you can not 
        /// dispose this library until that IUnknown is collected. Else, you may 
        /// free the library and then the CLR may call release on that IUnknown 
        /// and it will crash.
        /// </remarks>
        public TDelegate GetUnmanagedFunction<TDelegate>(string functionName)
            where TDelegate : class
        {
            IntPtr p = NativeMethod.GetProcAddress(m_hLibrary, functionName);

            // Failure is a common case, especially for adaptive code.
            if (p == IntPtr.Zero)
            {
                return null;
            }

            Delegate function = Marshal.GetDelegateForFunctionPointer(
                p, typeof(TDelegate));

            // Ideally, we'd just make the constraint on TDelegate be
            // System.Delegate, but compiler error CS0702 
            // (constrained can't be System.Delegate)
            // prevents that. So we make the constraint system.object and do the
            // cast from object-->TDelegate.
            object o = function;

            return (TDelegate)o;
        }


        #region IDisposable Members

        /// <summary>
        /// Call FreeLibrary on the unmanaged dll. All function pointers handed 
        /// out from this class become invalid after this.
        /// </summary>
        /// <remarks>
        /// This is very dangerous because it suddenly invalidate everything
        /// retrieved from this dll. This includes any functions handed out via 
        /// GetProcAddress, and potentially any objects returned from those 
        /// functions (which may have an implemention in the dll).
        /// </remarks>
        public void Dispose()
        {
            if (!m_hLibrary.IsClosed)
            {
                m_hLibrary.Close();
            }
        }

        // Unmanaged resource. CLR will ensure SafeHandles get freed, without 
        // requiring a finalizer on this class.
        SafeLibraryHandle m_hLibrary;

        #endregion
    }
}