using System;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Represents a class that can be consumed to compress/decompress string.
/// </summary>
public static class Compressor
{
    /// <summary>
    /// Use this to compress UTF-8 string to Base-64 string.
    /// </summary>
    /// <param name="text">The string value to compress.</param>
    /// <returns></returns>
    public static string Compress(this string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text);
        var memoryStream = new MemoryStream();
        using (var stream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        {
            stream.Write(buffer, 0, buffer.Length);
        }
        memoryStream.Position = 0;
        var compressed = new byte[memoryStream.Length];
        memoryStream.Read(compressed, 0, compressed.Length);
        var gZipBuffer = new byte[compressed.Length + 4];
        Buffer.BlockCopy(compressed, 0, gZipBuffer, 4, compressed.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
        return Convert.ToBase64String(gZipBuffer);
    }

    /// <summary>
    /// Use this to decompress Base-64 string to UTF-8 string.
    /// </summary>
    /// <param name="compressedText"></param>
    /// <returns></returns>
    public static string Decompress(this string compressedText)
    {
        var gZipBuffer = Convert.FromBase64String(compressedText);
        using (var memoryStream = new MemoryStream())
        {
            int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
            memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);
            var buffer = new byte[dataLength];
            memoryStream.Position = 0;
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                gZipStream.Read(buffer, 0, buffer.Length);
            }
            return Encoding.UTF8.GetString(buffer);
        }
    }

}
