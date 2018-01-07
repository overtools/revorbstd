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

        private static unsafe bool CopyHeaders(Stream fi, ogg_sync_state si, ogg_stream_state @is,
                                               Stream fo, ogg_sync_state so, ogg_stream_state os,
                                               vorbis_info vi)
        {
            IntPtr buffer = ogg_sync_buffer(ref si, 4096);
            int numread = fread(buffer, 1, 4096, fi);
            ogg_sync_wrote(ref si, numread);

            ogg_page page = new ogg_page { };
            if (ogg_sync_pageout(ref si, ref page) != 1)
            {
                return false;
            }

            ogg_stream_init(ref @is, ogg_page_serialno(ref page));
            ogg_stream_init(ref @os, ogg_page_serialno(ref page));

            if (ogg_stream_pagein(ref @is, ref page) < 0)
            {
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            ogg_packet packet = new ogg_packet { };
            if (ogg_stream_packetout(ref @is, ref packet) != 1)
            {
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            vorbis_comment vc = new vorbis_comment { };
            vorbis_comment_init(ref vc);
            if (vorbis_synthesis_headerin(ref vi, ref vc, ref packet) < 0)
            {
                vorbis_comment_clear(ref vc);
                ogg_stream_clear(ref @is);
                ogg_stream_clear(ref os);
                return false;
            }

            ogg_stream_packetin(ref os, ref packet);

            int i = 0;
            while (i < 2)
            {
                int res = ogg_sync_pageout(ref si, ref page);

                if (res == 0)
                {
                    buffer = ogg_sync_buffer(ref si, 4096);
                    numread = fread(buffer, 1, 4096, fi);
                    if (numread == 0 && i < 2)
                    {
                        vorbis_comment_clear(ref vc);
                        ogg_stream_clear(ref @is);
                        ogg_stream_clear(ref os);
                        return false;
                    }

                    ogg_sync_wrote(ref si, numread);
                    continue;
                }

                if (res == 1)
                {
                    ogg_stream_pagein(ref @is, ref page);
                    while (i < 2)
                    {
                        res = ogg_stream_packetout(ref @is, ref packet);
                        if (res == 0)
                        {
                            break;
                        }
                        if (res < 0)
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

            while (ogg_stream_flush(ref os, ref page) != 0)
            {
                if (fwrite(page.header, 1, page.header_len, fo) != page.header_len || fwrite(page.body, 1, page.body_len, fo) != page.body_len)
                {
                    ogg_stream_clear(ref @is);
                    ogg_stream_clear(ref os);
                    return false;
                }
            }

            return true;
        }

        public static MemoryStream Jiggle(Stream fi)
        {
            MemoryStream fo = new MemoryStream();

            ogg_sync_state sync_in = new ogg_sync_state { };
            ogg_sync_state sync_out = new ogg_sync_state { };

            ogg_sync_init(ref sync_in);
            ogg_sync_init(ref sync_out);

            ogg_stream_state stream_in = new ogg_stream_state { };
            ogg_stream_state stream_out = new ogg_stream_state { };
            vorbis_info vi = new vorbis_info { };
            vorbis_info_init(ref vi);

            ogg_packet packet = new ogg_packet { };
            ogg_page page = new ogg_page { };
            if (CopyHeaders(fi, sync_in, stream_in, fo, sync_out, stream_out, vi))
            {
                long granpos = 0;
                long packetnum = 0;
                int lastbs = 0;
                while (true)
                {
                    int eos = 0;
                    while (eos == 0)
                    {
                        int res = ogg_sync_pageout(ref sync_in, ref page);
                        if (res == 0)
                        {
                            IntPtr buffer = ogg_sync_buffer(ref sync_in, 4096);
                            int numread = fread(buffer, 1, 4096, fi);
                            if (numread > 0)
                            {
                                ogg_sync_wrote(ref sync_in, numread);
                            }
                            else
                            {
                                eos = 2;
                            }
                            continue;
                        }

                        if (res < 0)
                        {
                            break;
                        }
                        else
                        {
                            if (ogg_page_eos(ref page) == 1)
                            {
                                eos = 1;
                            }
                            ogg_stream_pagein(ref stream_in, ref page);
                            while (true)
                            {
                                res = ogg_stream_packetout(ref stream_in, ref packet);
                                if (res == 0)
                                {
                                    break;
                                }
                                if (res < 0)
                                {
                                    continue;
                                }
                                int bs = vorbis_packet_blocksize(ref vi, ref packet);
                                if (lastbs > 0)
                                {
                                    granpos += (lastbs * bs) / 4;
                                }
                                lastbs = bs;

                                packet.granulepos = granpos;
                                packet.packetno = packetnum++;
                                if (packet.e_o_s == 0)
                                {
                                    ogg_stream_packetin(ref stream_out, ref packet);
                                    ogg_page opage = new ogg_page { };
                                    while (ogg_stream_pageout(ref stream_out, ref page) != 0)
                                    {
                                        if (fwrite(opage.header, 1, opage.header_len, fo) != opage.header_len || fwrite(opage.body, 1, opage.body_len, fo) != opage.body_len)
                                        {
                                            eos = 2;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (eos == 2)
                    {
                        break;
                    }

                    {
                        packet.e_o_s = 1;
                        ogg_stream_packetin(ref stream_out, ref packet);
                        ogg_page opage = new ogg_page { };
                        while (ogg_stream_flush(ref stream_out, ref opage) != 0)
                        {
                            if (fwrite(opage.header, 1, opage.header_len, fo) != opage.header_len || fwrite(opage.body, 1, opage.body_len, fo) != opage.body_len)
                            {
                                break;
                            }
                        }
                        ogg_stream_clear(ref stream_in);
                        break;
                    }
                }
                ogg_stream_clear(ref stream_out);
            }

            vorbis_info_clear(ref vi);
            ogg_sync_clear(ref sync_in);
            ogg_sync_clear(ref sync_out);

            return fo;
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
