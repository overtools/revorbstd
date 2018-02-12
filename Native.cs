using System;
using System.Runtime.InteropServices;

namespace RevorbStd
{
    public class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct REVORB_FILE
        {
            public IntPtr start;
            public IntPtr cursor;
            public int size;
        }

        public const int REVORB_ERR_SUCCESS = 0;
        public const int REVORB_ERR_NOT_OGG = 1;
        public const int REVORB_ERR_FIRST_PAGE = 2;
        public const int REVORB_ERR_FIRST_PACKET = 3;
        public const int REVORB_ERR_HEADER = 4;
        public const int REVORB_ERR_TRUNCATED = 5;
        public const int REVORB_ERR_SECONDARY_HEADER = 6;
        public const int REVORB_ERR_HEADER_WRITE = 7;
        public const int REVORB_ERR_CORRUPT = 8;
        public const int REVORB_ERR_BITSTREAM_CORRUPT = 9;
        public const int REVORB_ERR_WRITE_FAIL = 10;
        public const int REVORB_ERR_WRITE_FAIL2 = 11;

        [DllImport("librevorb.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "revorb")]
        public static extern int revorb(ref REVORB_FILE fi, ref REVORB_FILE fo);
    }
}