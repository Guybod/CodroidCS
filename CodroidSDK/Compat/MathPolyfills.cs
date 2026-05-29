#if NET462
namespace Codroid;

internal static class MathPolyfills
{
    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
#else
namespace Codroid;

internal static class MathPolyfills
{
    public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    public static double Clamp(double value, double min, double max) => Math.Clamp(value, min, max);
}
#endif
