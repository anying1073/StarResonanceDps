using ZstdNet;

namespace StarResonanceDpsAnalysis.Core.Extends.Data;

public static class ZstdExtension
{
    private const uint ZSTD_MAGIC = 0xFD2FB528;
    private const uint SKIPPABLE_MAGIC_MIN = 0x184D2A50;
    private const uint SKIPPABLE_MAGIC_MAX = 0x184D2A5F;
    private const long MAX_OUT = 32L * 1024 * 1024;

    /// <summary>
    /// 如果数据包含Zstd帧则解压缩，否则原样返回<br/>
    /// if the data contains Zstd frames, decompress it; otherwise, return it as is.
    /// </summary>
    public static byte[] DecompressZstdIfNeeded(this byte[]? buffer)
    {
        if (buffer == null || buffer.Length < 4) return [];

        var off = 0;
        while (off + 4 <= buffer.Length)
        {
            var magic = BitConverter.ToUInt32(buffer, off);
            if (magic == ZSTD_MAGIC) break;
            if (magic >= SKIPPABLE_MAGIC_MIN && magic <= SKIPPABLE_MAGIC_MAX)
            {
                if (off + 8 > buffer.Length) throw new InvalidDataException("不完整的skippable帧头");

                var size = BitConverter.ToUInt32(buffer, off + 4);
                if (off + 8 + size > buffer.Length) throw new InvalidDataException("不完整的skippable帧数据");

                off += 8 + (int)size;
                continue;
            }

            off++;
        }

        if (off + 4 > buffer.Length) return buffer;

        using var input = new MemoryStream(buffer, off, buffer.Length - off, false);
        using var decoder = new DecompressionStream(input);
        using var output = new MemoryStream();

        var temp = new byte[8192];
        long total = 0;
        int read;
        while ((read = decoder.Read(temp, 0, temp.Length)) > 0)
        {
            total += read;

            if (total > MAX_OUT) throw new InvalidDataException("解压结果超过32MB限制");

            output.Write(temp, 0, read);
        }

        return output.ToArray();
    }
}