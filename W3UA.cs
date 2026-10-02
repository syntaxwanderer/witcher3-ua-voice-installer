using System;
using System.Collections.Generic;
using System.IO;

// Ukrainian voice-over for The Witcher 3 (Remastered 5.0) - patcher core.
// Builds the patch from the player's OWN game files: only our audio (block A),
// a XOR delta of the lipsync block (B) and ADX frame bodies are shipped.
public static class W3UA
{
    static uint[] T;
    static void InitCrc()
    {
        if (T != null) return;
        T = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            T[i] = c;
        }
    }
    public static uint Crc(uint crc, byte[] b, int off, int len)
    {
        InitCrc();
        uint c = crc ^ 0xFFFFFFFFu;
        for (int i = off; i < off + len; i++) c = T[(c ^ b[i]) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
    public static uint CrcFile(string path)
    {
        uint c = 0; byte[] buf = new byte[1 << 22];
        using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        {
            int n;
            while ((n = f.Read(buf, 0, buf.Length)) > 0) c = Crc(c, buf, 0, n);
        }
        return c;
    }
    static void ReadExact(Stream s, byte[] b, int off, int len)
    {
        while (len > 0)
        {
            int n = s.Read(b, off, len);
            if (n <= 0) throw new IOException("unexpected end of file");
            off += n; len -= n;
        }
    }
    static void Copy(Stream src, Stream dst, long len, byte[] buf)
    {
        while (len > 0)
        {
            int n = src.Read(buf, 0, (int)Math.Min(buf.Length, len));
            if (n <= 0) throw new IOException("unexpected end of file");
            dst.Write(buf, 0, n); len -= n;
        }
    }

    // ---- w3speech ---------------------------------------------------------
    // Header is 12 or 13 bytes (varint count). Detect H by self-consistency:
    // A and B adjacent, (firstOffset - H) % 40 == 2 (2-byte language marker).
    public static int DetectHeader(byte[] head, long size, out int N, out long firstOff)
    {
        for (int H = 8; H < 32; H++)
        {
            long offA = BitConverter.ToInt64(head, H + 8);
            long szA = BitConverter.ToInt64(head, H + 16);
            long offB = BitConverter.ToInt64(head, H + 24);
            if (offA > 0 && offA < size && (offA - H) % 40 == 2 && offA + szA == offB)
            {
                N = (int)((offA - H) / 40); firstOff = offA; return H;
            }
        }
        throw new Exception("w3speech header not recognised");
    }

    // ei = entry indices, ao/al = our block A in dat, bo/bl = XOR delta of block B in dat
    // rekey != 0: entry keys are XORed with it and the language key in the header (bytes 8-9) and the
    // 2-byte marker before the data are zeroed -> a key-0 language file (br/ua/cn/kr/esmx) from a PL one.
    public static void PatchSpeech(string src, string dst, string dat,
        int[] ei, long[] ao, int[] al, long[] bo, int[] bl, uint rekey)
    {
        Dictionary<int, int> map = new Dictionary<int, int>();
        for (int k = 0; k < ei.Length; k++) map[ei[k]] = k;
        byte[] buf = new byte[1 << 22];
        using (FileStream s = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        using (FileStream d = new FileStream(dat, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        using (FileStream o = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        {
            byte[] head = new byte[64]; ReadExact(s, head, 0, 64);
            int N; long firstOff;
            int H = DetectHeader(head, s.Length, out N, out firstOff);
            byte[] pre = new byte[firstOff];
            s.Seek(0, SeekOrigin.Begin); ReadExact(s, pre, 0, (int)firstOff);
            long[] oa = new long[N], sa = new long[N], ob = new long[N], sb = new long[N];
            byte[] idx = new byte[N * 40];
            long cur = firstOff;
            for (int i = 0; i < N; i++)
            {
                int p = H + i * 40;
                oa[i] = BitConverter.ToInt64(pre, p + 8); sa[i] = BitConverter.ToInt64(pre, p + 16);
                ob[i] = BitConverter.ToInt64(pre, p + 24); sb[i] = BitConverter.ToInt64(pre, p + 32);
                long la = map.ContainsKey(i) ? al[map[i]] : sa[i];
                Buffer.BlockCopy(pre, p, idx, i * 40, 8);
                if (rekey != 0) Buffer.BlockCopy(BitConverter.GetBytes(BitConverter.ToUInt32(pre, p) ^ rekey), 0, idx, i * 40, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(cur), 0, idx, i * 40 + 8, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(la), 0, idx, i * 40 + 16, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(cur + la), 0, idx, i * 40 + 24, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(sb[i]), 0, idx, i * 40 + 32, 8);
                cur += la + sb[i];
            }
            if (rekey != 0) { pre[8] = 0; pre[9] = 0; pre[firstOff - 2] = 0; pre[firstOff - 1] = 0; }
            o.Write(pre, 0, H);
            o.Write(idx, 0, idx.Length);
            o.Write(pre, H + N * 40, (int)(firstOff - H - N * 40));
            for (int i = 0; i < N; i++)
            {
                int k;
                if (map.TryGetValue(i, out k))
                {
                    d.Seek(ao[k], SeekOrigin.Begin); Copy(d, o, al[k], buf);
                    byte[] b = new byte[sb[i]]; s.Seek(ob[i], SeekOrigin.Begin); ReadExact(s, b, 0, b.Length);
                    if (bl[k] != b.Length) throw new Exception("lipsync block size mismatch at entry " + i);
                    byte[] x = new byte[bl[k]]; d.Seek(bo[k], SeekOrigin.Begin); ReadExact(d, x, 0, x.Length);
                    for (int j = 0; j < b.Length; j++) b[j] ^= x[j];
                    o.Write(b, 0, b.Length);
                }
                else
                {
                    // A and B are not always adjacent in the Remastered file (33 entries): copy each on its own
                    s.Seek(oa[i], SeekOrigin.Begin); Copy(s, o, sa[i], buf);
                    s.Seek(ob[i], SeekOrigin.Begin); Copy(s, o, sb[i], buf);
                }
            }
        }
    }

    // ---- storybook movies (CRI USM inside movies.bundle) -------------------
    // Bundle TOC: 32-byte header (dataOffset = TOC size @16), then fixed-size entries.
    // 4.04:        0x140 = name[0x100], hash[16], u32 0, u32 size @0x114, u32 zsize @0x118,
    //              u32 offset @0x11C, ..., u32 crc32(data) @0x138, u32 compression @0x13C.
    // Remastered:  0x130 = name[0x100], hash[16], u64 offset @0x110, u32 size @0x118,
    //              u32 zsize @0x11C, u32 crc32(data) @0x120, 12 bytes padding.
    // The layout is the one whose entry size divides the TOC exactly (0x130 preferred).
    public static long FindInBundle(FileStream f, string name, out int size, out long tocPos, out int crcOff)
    {
        byte[] h = new byte[32]; f.Seek(0, SeekOrigin.Begin); ReadExact(f, h, 0, 32);
        if (h[0] != 'P' || h[1] != 'O' || h[2] != 'T' || h[3] != 'A') throw new Exception("not a bundle");
        int toc = BitConverter.ToInt32(h, 16);
        byte[] t = new byte[toc]; ReadExact(f, t, 0, toc);
        int E = (toc % 0x130 == 0) ? 0x130 : 0x140;
        for (int i = 0; i + E <= toc; i += E)
        {
            int e = 0; while (e < 0x100 && t[i + e] != 0) e++;
            string n = System.Text.Encoding.ASCII.GetString(t, i, e);
            if (!string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) continue;
            tocPos = 32 + i;
            if (E == 0x130)
            {
                size = BitConverter.ToInt32(t, i + 0x118);
                if (BitConverter.ToInt32(t, i + 0x11C) != size) throw new Exception("compressed entry " + name);
                crcOff = 0x120;
                return BitConverter.ToInt64(t, i + 0x110);
            }
            int comp = BitConverter.ToInt32(t, i + 0x13C);
            size = BitConverter.ToInt32(t, i + 0x114);
            if (comp != 0 || BitConverter.ToInt32(t, i + 0x118) != size) throw new Exception("compressed entry " + name);
            crcOff = 0x138;
            return (long)BitConverter.ToUInt32(t, i + 0x11C);
        }
        throw new Exception("not found in bundle: " + name);
    }

    static int BE32(byte[] b, int p) { return (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]; }
    static int BE16(byte[] b, int p) { return (b[p] << 8) | b[p + 1]; }

    // data parts (type 0) of one @SFA channel, in stream order
    public static List<int[]> AudioParts(byte[] u, int ch)
    {
        List<int[]> parts = new List<int[]>();
        int pos = 0;
        while (pos + 32 <= u.Length)
        {
            string sig = System.Text.Encoding.ASCII.GetString(u, pos, 4);
            int size = BE32(u, pos + 4);
            if (sig != "CRID" && sig != "@SFV" && sig != "@SFA" && sig != "@SBT" && sig != "@CUE" && sig != "@ALP")
                throw new Exception("bad USM chunk at " + pos);
            int poff = u[pos + 9], pad = BE16(u, pos + 10), c = u[pos + 12], typ = u[pos + 15] & 3;
            if (sig == "@SFA" && c == ch && typ == 0) parts.Add(new int[] { pos + 8 + poff, pos + 8 + size - pad });
            pos += 8 + size;
        }
        return parts;
    }

    // Replace ADX frame body [hlen, hlen+len) of channel ch with ours. In place, same size.
    public static void ReplaceAdxBody(byte[] u, int ch, int hlen, byte[] body)
    {
        List<int[]> parts = AudioParts(u, ch);
        long k = 0; long a = hlen, b = hlen + body.Length;   // positions inside the ADX stream
        foreach (int[] pr in parts)
        {
            int n = pr[1] - pr[0];
            long s0 = Math.Max(k, a), s1 = Math.Min(k + n, b);
            if (s1 > s0) Buffer.BlockCopy(body, (int)(s0 - a), u, pr[0] + (int)(s0 - k), (int)(s1 - s0));
            k += n;
        }
        if (k < b) throw new Exception("ADX stream shorter than expected");
    }

    public static void PatchMovie(string bundle, string name, string dat, uint origCrc, uint newCrc,
        int[] ch, int[] hlen, long[] off, int[] len)
    {
        using (FileStream f = new FileStream(bundle, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1 << 20))
        using (FileStream d = new FileStream(dat, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int size, crcOff; long tocPos;
            long at = FindInBundle(f, name, out size, out tocPos, out crcOff);
            byte[] u = new byte[size]; f.Seek(at, SeekOrigin.Begin); ReadExact(f, u, 0, size);
            uint c0 = Crc(0, u, 0, size);
            if (c0 == newCrc) return;                       // already ours
            if (c0 != origCrc) throw new Exception("unexpected version of " + name);
            for (int i = 0; i < ch.Length; i++)
            {
                byte[] body = new byte[len[i]]; d.Seek(off[i], SeekOrigin.Begin); ReadExact(d, body, 0, len[i]);
                ReplaceAdxBody(u, ch[i], hlen[i], body);
            }
            uint c1 = Crc(0, u, 0, size);
            if (c1 != newCrc) throw new Exception("self-check failed for " + name);
            f.Seek(at, SeekOrigin.Begin); f.Write(u, 0, size);
            f.Seek(tocPos + crcOff, SeekOrigin.Begin); f.Write(BitConverter.GetBytes(c1), 0, 4);
            f.Flush(true);
        }
    }

    // CRC of one movie inside the bundle (0 if absent)
    public static uint MovieCrc(string bundle, string name)
    {
        using (FileStream f = new FileStream(bundle, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int size, crcOff; long tocPos;
            long at = FindInBundle(f, name, out size, out tocPos, out crcOff);
            byte[] u = new byte[size]; f.Seek(at, SeekOrigin.Begin); ReadExact(f, u, 0, size);
            return Crc(0, u, 0, size);
        }
    }

    // ---- w3strings (Remastered v164, languages with key 0 such as UA: plain UTF-8) ----------
    // RTSW, u32 164, u16 key1 | bit6 n1, n1*(id, off, len) | bit6 n2, n2*8 | bit6 nbytes, UTF-8 blob (NUL-terminated) | u16 key2
    static int Rd6(byte[] b, ref int p)
    {
        int v = b[p] & 0x3F; bool more = (b[p] & 0x40) != 0; p++; int s = 6;
        while (more) { int c = b[p++]; v |= (c & 0x7F) << s; s += 7; more = (c & 0x80) != 0; }
        return v;
    }
    static byte[] Wr6(int v)
    {
        List<byte> o = new List<byte>(); byte f = (byte)(v & 0x3F); v >>= 6; if (v != 0) f |= 0x40; o.Add(f);
        while (v != 0) { byte c = (byte)(v & 0x7F); v >>= 7; if (v != 0) c |= 0x80; o.Add(c); }
        return o.ToArray();
    }
    static void Layout(byte[] b, out int n1, out int rec, out int sec2, out int sec2End, out int s0, out int nb)
    {
        if (b.Length < 12 || b[0] != 'R' || b[1] != 'T' || b[2] != 'S' || b[3] != 'W' || BitConverter.ToUInt32(b, 4) != 164) throw new Exception("w3strings: невідомий формат");
        if (b[8] != 0 || b[9] != 0) throw new Exception("w3strings: зашифрована мова");
        int p = 10; n1 = Rd6(b, ref p); rec = p; p += 12 * n1;
        sec2 = p; int n2 = Rd6(b, ref p); p += 8 * n2; sec2End = p;
        nb = Rd6(b, ref p); s0 = p;
        if (s0 + nb + 2 != b.Length) throw new Exception("w3strings: невідомий формат");
    }
    public static Dictionary<uint, string> ReadStrings(byte[] b, ICollection<uint> ids)
    {
        int n1, rec, sec2, sec2End, s0, nb; Layout(b, out n1, out rec, out sec2, out sec2End, out s0, out nb);
        Dictionary<uint, string> r = new Dictionary<uint, string>();
        for (int i = 0; i < n1; i++)
        {
            uint id = BitConverter.ToUInt32(b, rec + 12 * i);
            if (ids.Contains(id)) r[id] = System.Text.Encoding.UTF8.GetString(b, s0 + BitConverter.ToInt32(b, rec + 12 * i + 4), BitConverter.ToInt32(b, rec + 12 * i + 8));
        }
        return r;
    }
    // New strings are appended to the blob and their records repointed; everything else stays byte-identical.
    public static byte[] PatchStrings(byte[] b, Dictionary<uint, string> s)
    {
        int n1, rec, sec2, sec2End, s0, nb; Layout(b, out n1, out rec, out sec2, out sec2End, out s0, out nb);
        byte[] recs = new byte[12 * n1]; Buffer.BlockCopy(b, rec, recs, 0, recs.Length);
        MemoryStream blob = new MemoryStream(); blob.Write(b, s0, nb);
        for (int i = 0; i < n1; i++)
        {
            uint id = BitConverter.ToUInt32(recs, 12 * i); string t;
            if (!s.TryGetValue(id, out t)) continue;
            byte[] u = System.Text.Encoding.UTF8.GetBytes(t);
            Buffer.BlockCopy(BitConverter.GetBytes((int)blob.Length), 0, recs, 12 * i + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(u.Length), 0, recs, 12 * i + 8, 4);
            blob.Write(u, 0, u.Length); blob.WriteByte(0);
        }
        MemoryStream o = new MemoryStream();
        o.Write(b, 0, 10); byte[] w = Wr6(n1); o.Write(w, 0, w.Length); o.Write(recs, 0, recs.Length);
        o.Write(b, sec2, sec2End - sec2);
        w = Wr6((int)blob.Length); o.Write(w, 0, w.Length); blob.WriteTo(o);
        o.Write(b, b.Length - 2, 2);
        return o.ToArray();
    }
}
