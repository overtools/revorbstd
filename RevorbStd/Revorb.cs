using System;
using System.IO;
using System.Runtime.InteropServices;
using static RevorbStd.Native;

namespace RevorbStd
{
    public class Revorb
    {
        private static int fread(IntPtr buffer, int size, int count, Stream stream)
        {
            int total = size * count;
            byte[] local = new byte[total];
            int read = stream.Read(local, 0, total);
            Marshal.Copy(local, 0, buffer, read);
            return read;
        }

        private static int fwrite(IntPtr buffer, int size, int count, Stream stream)
        {
            int total = size * count;
            byte[] local = new byte[total];
            Marshal.Copy(buffer, local, 0, total);
            stream.Write(local, 0, total);
            return total;
        }

        private static unsafe bool CopyBuffers(MemoryStream fi, ogg_sync_state si, ogg_stream_state @is,
                                        MemoryStream fo, ogg_sync_state so, ogg_stream_state os,
                                        vorbis_info vi)
        {
            IntPtr buffer = ogg_sync_buffer(ref si, 4096);
            int numread = fread(buffer, 1, 4096, fi);
            ogg_sync_wrote(ref si, numread);

            ogg_page page = new ogg_page { };
            if(ogg_sync_pageout(ref si, ref page) != 1)
            {
                return false;
            }

            ogg_stream_init(ref @is, ogg_page_serialno(ref page));
            ogg_stream_init(ref @os, ogg_page_serialno(ref page));

            if(ogg_stream_pagein(ref @is, ref page) < 0)
            {
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            ogg_packet packet = new ogg_packet { };
            if(ogg_stream_packetout(ref @is, ref packet) != 1)
            {
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            vorbis_comment vc = new vorbis_comment { };
            vorbis_comment_init(ref vc);
            if(vorbis_synthesis_headerin(ref vi, ref vc, ref packet) < 0)
            {
                vorbis_comment_clear(ref vc);
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            ogg_stream_packetin(ref os, ref packet);

            int i = 0;
            while(i < 2)
            {
                int res = ogg_sync_pageout(ref si, ref page);

                if(res == 0)
                {
                    buffer = ogg_sync_buffer(ref si, 4096);
                    numread = fread(buffer, 1, 4096, fi);
                    if(numread == 0 && i < 2)
                    {
                        vorbis_comment_clear(ref vc);
                        ogg_stream_clear(ref @is);
                        ogg_stream_clear(ref os);
                        return false;
                    }

                    ogg_sync_wrote(ref si, numread);
                    continue;
                }

                if(res == 1)
                {
                    ogg_stream_pagein(ref @is, ref page);
                    while(i < 2)
                    {
                        res = ogg_stream_packetout(ref @is, ref packet);
                        if(res == 0)
                        {
                            break;
                        }
                        if(res < 0)
                        {
                            vorbis_comment_clear(ref vc);
                            ogg_stream_clear(ref @is);
                            ogg_stream_clear(ref os);
                            return false;
                        }
                        vorbis_synthesis_headerin(ref vi, ref vc, ref packet);
                        ogg_stream_packetin(ref os, ref packet);
                        i++;
                    }
                }
            }

            vorbis_comment_clear(ref vc);

            while(ogg_stream_flush(ref os, ref page) > 0)
            {
                if(fwrite(page.header, 1, page.header_len, fo) != page.header_len || fwrite(page.body, 1, page.body_len, fo) != page.body_len)
                {
                    ogg_stream_clear(ref @is);
                    ogg_stream_clear(ref os);
                    return false;
                }
            }

            return true;
        }

        public static MemoryStream Jiggle(MemoryStream data)
        {
            return null;
        }
    }
}
