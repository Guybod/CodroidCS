#if NET462
namespace Codroid;

internal static class DoublePolyfills
{
    public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
}
#else
namespace Codroid;

internal static class DoublePolyfills
{
    public static bool IsFinite(double d) => double.IsFinite(d);
}
#endif
