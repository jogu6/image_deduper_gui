namespace ImageDeduper.Core.Imaging;

public static class SsimCalculator
{
    private const double K1 = 0.01;
    private const double K2 = 0.03;
    private const double L = 255.0;
    private const double C1 = (K1 * L) * (K1 * L);
    private const double C2 = (K2 * L) * (K2 * L);

    public static double Compute(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Images must be the same length.");
        }

        double sumA = 0;
        double sumB = 0;
        double sumA2 = 0;
        double sumB2 = 0;
        double sumAB = 0;
        var length = a.Length;

        for (var i = 0; i < length; i++)
        {
            var va = a[i];
            var vb = b[i];
            sumA += va;
            sumB += vb;
            sumA2 += va * va;
            sumB2 += vb * vb;
            sumAB += va * vb;
        }

        var meanA = sumA / length;
        var meanB = sumB / length;
        var varA = sumA2 / length - meanA * meanA;
        var varB = sumB2 / length - meanB * meanB;
        var cov = sumAB / length - meanA * meanB;

        var numerator = (2 * meanA * meanB + C1) * (2 * cov + C2);
        var denominator = (meanA * meanA + meanB * meanB + C1) * (varA + varB + C2);

        if (denominator == 0)
        {
            return 0;
        }

        return Math.Clamp(numerator / denominator, -1.0, 1.0);
    }
}
