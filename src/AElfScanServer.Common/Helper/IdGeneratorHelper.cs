using System;
using System.Collections.Generic;

namespace AElfScanServer.Helper;

public static class IdGeneratorHelper
{
    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");
}

public static class DateTimeHelper
{
    public static long GetTotalSeconds(DateTime dateTime)
    {
        return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }

    public static long GetTotalMilliseconds(DateTime dateTime)
    {
        return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }
}