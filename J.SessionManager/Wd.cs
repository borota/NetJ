using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace J.SessionManager
{
    internal static class Wd
    {
        public const int EVDOMAIN = 3;

        public const int LIT = 2;
#if WIN64
        private const int _iSz = sizeof(long);
        //#define AK(x)           ((x)->k)        /* offset of ravel wrt x           */
        private static long AK(IntPtr a)
        {
            return Marshal.ReadInt64(a, 0 * _iSz);
        }
        //#define AFLAG(x)        ((x)->flag)     /* flag                            */
        private static long AFLAG(IntPtr a)
        {
            return Marshal.ReadInt64(a, 1 * _iSz);
        }
        //#define AM(x)           ((x)->m)        /* Max # bytes in ravel            */
        private static long AM(IntPtr a)
        {
            return Marshal.ReadInt64(a, 2 * _iSz);
        }
        //#define AT(x)           ((x)->t)        /* Type;                           */
        private static long AT(IntPtr a)
        {
            return Marshal.ReadInt64(a, 3 * _iSz);
        }
        //#define AC(x)           ((x)->c)        /* Reference count.                */
        private static long AC(IntPtr a)
        {
            return Marshal.ReadInt64(a, 4 * _iSz);
        }
        //#define AN(x)           ((x)->n)        /* # elements in ravel             */
        private static long AN(IntPtr a)
        {
            return Marshal.ReadInt64(a, 5 * _iSz);
        }
        //#define AR(x)           ((x)->r)        /* Rank                            */
        private static long AR(IntPtr a)
        {
            return Marshal.ReadInt64(a, 6 * _iSz);
        }
#else
        private const int _iSz = sizeof(int);
        private static int AK(IntPtr a)
        {
            return Marshal.ReadInt32(a, 0 * _iSz);
        }
        private static int AFLAG(IntPtr a)
        {
            return Marshal.ReadInt32(a, 1 * _iSz);
        }
        private static int AM(IntPtr a)
        {
            return Marshal.ReadInt32(a, 2 * _iSz);
        }
        private static int AT(IntPtr a)
        {
            return Marshal.ReadInt32(a, 3 * _iSz);
        }
        private static int AC(IntPtr a)
        {
            return Marshal.ReadInt32(a, 4 * _iSz);
        }
        private static int AN(IntPtr a)
        {
            return Marshal.ReadInt32(a, 5 * _iSz);
        }
        private static int AR(IntPtr a)
        {
            return Marshal.ReadInt32(a, 6 * _iSz);
        }
#endif
        //#define AS(x)           ((x)->s)        /* Pointer to shape                */
        private static IntPtr AS(IntPtr a)
        {
            return IntPtr.Add(a, 7 * _iSz);
        }
        //#define AV(x)           ( (I*)((C*)(x)+AK(x)))  /* pointer to ravel        */
        private static IntPtr AV(IntPtr a)
        {
            return IntPtr.Add(a, (int)AK(a));
        }
        //#define BAV(x)          (      (C*)(x)+AK(x) )  /* boolean                 */
        private static bool BAV(IntPtr a)
        {
            return (byte)0 != Marshal.ReadByte(a, (int)AK(a));
        }
        //#define CAV(x)          (      (C*)(x)+AK(x) )  /* character               */
        private static string CAV(IntPtr a)
        {
            return Encoding.UTF8.GetString(JSession.BytesFromPtr(AV(a), AN(a)));
        }

        internal static int Parse(int t, IntPtr w, ref IntPtr z)
        {
            if (0 != t || AT(w) != Wd.LIT)
            {
                return Wd.EVDOMAIN; // only 11!:0 supported and only literal params please.
            }
            string wdArg = CAV(w);
            System.Console.WriteLine(wdArg);
            return 0;
        }
    }
}
