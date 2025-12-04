using System.Security.Cryptography;

namespace ImageDeduper.Core.Utils;

public static class FileHasher
{
    public static string ComputeSha1(string filePath)
    {
        using var sha = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }
}
