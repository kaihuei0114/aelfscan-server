using System;
using System.Collections.Generic;

namespace AElfScanServer.Helper;

public static class IdGeneratorHelper
{
    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");


    public static string GetId(params object[] inputs)
    {
        return inputs.JoinAsString("-");
    }

    public static string GetNftInfoId(string chainId, string symbol)
    {
        return GetId(chainId, symbol);
    }
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

    public static long GetTotalMinutes(DateTime dateTime)
    {
        return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalMinutes;
    }

    
    public static long GetNowMilliSeconds()
    {
        return GetTotalMilliseconds(DateTime.Now);
    }

    public static long GetBeforeHoursMilliSeconds(int hours)
    {
        return GetTotalMilliseconds(DateTime.Now.AddHours(-hours));
    }

    public static long GetBeforeMinutesMilliSeconds(int minutes)
    {
        return GetTotalMilliseconds(DateTime.Now.AddMinutes(-minutes));
    }


    public static long GetBeforeDayMilliSeconds(int day)
    {
        return GetTotalMilliseconds(DateTime.Now.AddDays(-day));
    }
}