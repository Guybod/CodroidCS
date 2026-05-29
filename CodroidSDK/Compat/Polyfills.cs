namespace Codroid;

internal static class Polyfills
{
#if NET462
    public static void ThrowIfNull(object? argument, [System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
            throw new System.ArgumentNullException(paramName);
    }
#else
    public static void ThrowIfNull(object? argument, [System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
        => ArgumentNullException.ThrowIfNull(argument);
#endif
}
