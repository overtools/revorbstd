using System;
using System.IO;
using System.Runtime.InteropServices;
using static RevorbStd.Native;

namespace RevorbStd
{
    public class Revorb
    {
        public static RevorbStream Jiggle(Stream fi)
        {
            byte[] raw = new byte[fi.Length];
            long pos = fi.Position;
            fi.Position = 0;
            fi.Read(raw, 0, raw.Length);
            fi.Position = pos;

            GCHandle rawHandle = GCHandle.Alloc(raw, GCHandleType.Pinned);

            REVORB_FILE input = new REVORB_FILE {
                start = rawHandle.AddrOfPinnedObject(),
                size = raw.Length
            };
            input.cursor = input.start;

            IntPtr ptr = Marshal.AllocHGlobal(4096);

            REVORB_FILE output = new REVORB_FILE
            {
                start = ptr,
                size = 4096
            };
            output.cursor = output.start;

            rawHandle.Free();

            return new RevorbStream(output);
        }

        public unsafe class RevorbStream : UnmanagedMemoryStream
        {
            private REVORB_FILE revorb;

            public RevorbStream(REVORB_FILE revorb) : base((byte*)revorb.start.ToPointer(), revorb.size)
            {
                this.revorb = revorb;
            }
            
            public new void Dispose()
            {
                base.Dispose();
                Marshal.FreeHGlobal(revorb.start);
            }
        }

        public static void Main(string[] args)
        {
            using (Stream file = File.OpenRead(args[0]))
            {
                using (Stream data = Jiggle(file))
                {
                    using (Stream outp = File.OpenWrite(args[1]))
                    {
                        data.Position = 0;
                        data.CopyTo(outp);
                    }
                }
            }
        }
    }
}
