using Domain;

namespace Application.Extensions;

public static class ExceptionExtensions
{
    public static string Trim(this Exception exception)
    {
        var exceptionString = exception.ToString();

        return exceptionString.Length > Constants.MaxExceptionLength
            ? string.Concat(
                exceptionString.AsSpan(0, Constants.MaxExceptionLength - Constants.TruncationIndicator.Length),
                Constants.TruncationIndicator)
            : exceptionString;
    }
}