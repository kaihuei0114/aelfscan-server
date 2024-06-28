using System;
using System.Collections.Generic;

namespace AElfScanServer.Common.Helper;

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

    public static string GetDateTimeString(long milliseconds)
    {
        return new DateTime(1970, 1, 1).AddMilliseconds(milliseconds).ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static long GetDateTotalMilliseconds(DateTime dateTime)
    {
        DateTime currentDate = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
        return (long)currentDate.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }


    public static long GetTotalMinutes(DateTime dateTime)
    {
        return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalMinutes;
    }

    public static long GetTodayTotalSeconds()
    {
        var now = DateTime.Now;
        DateTime currentDate = new DateTime(now.Year, now.Month, now.Day);
        return GetTotalSeconds(currentDate);
    }
    
    public static long GetTodayTotalMilliSeconds()
    {
        var now = DateTime.Now;
        DateTime currentDate = new DateTime(now.Year, now.Month, now.Day);
        return GetTotalSeconds(currentDate);
    }



    public static long GetTomorrowTotalSeconds()
    {
        var now = DateTime.Now;
        DateTime currentDate = new DateTime(now.Year, now.Month, now.Day + 1);
        return GetTotalSeconds(currentDate);
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