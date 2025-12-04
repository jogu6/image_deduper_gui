namespace ImageDeduper.Core.Models;

public sealed class ImageRecord
{
    public ImageRecord(
        string path,
        int width,
        int height,
        long length,
        ulong perceptualHash,
        string sha1,
        float[] pixels)
    {
        Path = path;
        Width = width;
        Height = height;
        Length = length;
        PerceptualHash = perceptualHash;
        Sha1 = sha1;
        Pixels = pixels;
    }

    public string Path { get; }
    public int Width { get; }
    public int Height { get; }
    public long Length { get; }
    public ulong PerceptualHash { get; }
    public string Sha1 { get; }
    public float[] Pixels { get; }

    public long Resolution => (long)Width * Height;
}
