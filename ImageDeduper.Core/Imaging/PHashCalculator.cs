using System.Numerics;

namespace ImageDeduper.Core.Imaging;

public static class PHashCalculator
{
    private const int HashSize = 32;
    private const int LowFreqSize = 8;

    public static ulong Compute(ReadOnlySpan<float> pixels)
    {
        if (pixels.Length != HashSize * HashSize)
        {
            throw new ArgumentException($"Expected {HashSize * HashSize} pixels for pHash.", nameof(pixels));
        }

        var input = new double[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            input[i] = pixels[i];
        }

        var temp = new double[input.Length];
        var dct = new double[input.Length];

        // Row-wise DCT.
        for (var row = 0; row < HashSize; row++)
        {
            var start = row * HashSize;
            var slice = input.AsSpan(start, HashSize);
            var dest = temp.AsSpan(start, HashSize);
            Dct1D(slice, dest);
        }

        // Column-wise DCT.
        var column = new double[HashSize];
        var transformed = new double[HashSize];

        for (var col = 0; col < HashSize; col++)
        {
            for (var row = 0; row < HashSize; row++)
            {
                column[row] = temp[row * HashSize + col];
            }

            Dct1D(column, transformed);

            for (var row = 0; row < HashSize; row++)
            {
                dct[row * HashSize + col] = transformed[row];
            }
        }

        Span<double> low = stackalloc double[LowFreqSize * LowFreqSize];
        var idx = 0;
        for (var y = 0; y < LowFreqSize; y++)
        {
            for (var x = 0; x < LowFreqSize; x++)
            {
                low[idx++] = dct[y * HashSize + x];
            }
        }

        var median = FindMedian(low);
        ulong value = 0;
        for (var i = 0; i < low.Length; i++)
        {
            value <<= 1;
            if (low[i] > median)
            {
                value |= 1;
            }
        }

        return value;
    }

    public static int HammingDistance(ulong a, ulong b)
    {
        return BitOperations.PopCount(a ^ b);
    }

    private static void Dct1D(ReadOnlySpan<double> input, Span<double> output)
    {
        var n = input.Length;
        var factor = Math.PI / (2.0 * n);
        for (var k = 0; k < n; k++)
        {
            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                sum += input[i] * Math.Cos((2 * i + 1) * k * factor);
            }

            var scale = k == 0 ? Math.Sqrt(1.0 / n) : Math.Sqrt(2.0 / n);
            output[k] = scale * sum;
        }
    }

    private static double FindMedian(ReadOnlySpan<double> values)
    {
        var buffer = values.ToArray();
        Array.Sort(buffer);
        var middle = buffer.Length / 2;
        if (buffer.Length % 2 == 0)
        {
            return (buffer[middle - 1] + buffer[middle]) / 2.0;
        }

        return buffer[middle];
    }
}
