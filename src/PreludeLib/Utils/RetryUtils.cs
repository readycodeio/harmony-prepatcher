using System;
using System.IO;

namespace PreludeLib.Utils;

internal static class RetryUtils
{
    public static bool IsTransientException(Exception ex)
        => ex is IOException || 
           ex is UnauthorizedAccessException || 
           ex is InvalidOperationException;
}