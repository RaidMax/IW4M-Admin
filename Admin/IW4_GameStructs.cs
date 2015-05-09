using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;


namespace IW4MAdmin
{
    [StructLayout(LayoutKind.Explicit)]
    public struct netadr_t
    {
        [FieldOffset(0x0)]
        Int32 type;

        [FieldOffset(0x4)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Byte[] ip;

        [FieldOffset(0x8)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public Byte[] ipx;

        [FieldOffset(0x12)]
        public short port;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct client_s
    {
        [FieldOffset(0x0)]
        public Int32 state;

        [FieldOffset(0x28)]
        public netadr_t adr;
        
        [FieldOffset(0x65C)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public String connectInfoString;

        [FieldOffset(0x20EA4)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=400)] // doubt this is the correct size
        public String lastUserCmd;

        [FieldOffset(0x212A4)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public String name;

        [FieldOffset(0x212C0)]
        public int snapNum;
      
        [FieldOffset(0x212C8)]
        public short ping;
        
        [FieldOffset(0x41AF0)]
        public int isBot;
        
        [FieldOffset(0x43F00)]
        public UInt64 steamid;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct dvar_t
    {
        [FieldOffset(0)]
        public IntPtr name;

        [FieldOffset(4)]
        public IntPtr description;
        
        [FieldOffset(8)]
        public uint flags;

        [FieldOffset(12)]
        short type;

        [FieldOffset(16)]
        public IntPtr current;

        [FieldOffset(32)]
        public IntPtr latched;

        [FieldOffset(48)]
        public IntPtr _default;

        [FieldOffset(65)]
        public IntPtr min;

        [FieldOffset(68)]
        public IntPtr max;
    }

    // not iw4
    public struct dvar
    {
        public String name;

        public String description;

        public int flags;

        short type;

        public String current;

        public String latched;

        public String _default;

        public int min;

        public int max;
    }



    class Helpers
    {
        public static String NET_AdrToString(netadr_t a)
        {
            // not worrying about NA_TYPE
            StringBuilder s = new StringBuilder(64);
            s.AppendFormat("{0}.{1}.{2}.{3}:{4}", a.ip[0], a.ip[1], a.ip[2], a.ip[3], a.port);
            return s.ToString();
        }

        public static unsafe T ReadStruct<T>(byte[] buffer) where T : struct
        {
            fixed (byte* b = buffer)
            return (T)Marshal.PtrToStructure(new IntPtr(b), typeof(T));
        }
    }   

}
