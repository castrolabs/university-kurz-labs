using System.IO.Compression;

namespace CompressionLab;

public static class Codecs
{
    // TODO-00
    public static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        // leaveOpen keeps the MemoryStream usable; disposing the compressor writes the trailer.
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] Gunzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    // TODO-01
    public static byte[] BrotliCompress(byte[] data, CompressionLevel level)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, level, leaveOpen: true))
        {
            brotli.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] BrotliDecompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

#if NET11_0_OR_GREATER
    // TODO-08 (optional)
    public static byte[] ZstdCompress(byte[] data, CompressionLevel level)
    {
        using var output = new MemoryStream();
        using (var zstd = new ZstandardStream(output, level, leaveOpen: true))
        {
            zstd.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] ZstdDecompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zstd = new ZstandardStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zstd.CopyTo(output);
        return output.ToArray();
    }
#endif
}
