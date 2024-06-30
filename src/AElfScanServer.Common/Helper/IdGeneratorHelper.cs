using System;
using System.Collections.Generic;
using System.Globalization;

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
        return new DateTime(1970, 1, 1).AddMilliseconds(milliseconds).ToUtc8String("yyyy-MM-dd HH:mm:ss");
    }

    public static long ConvertYYMMDD(string dateString)
    {
        string dateFormat = "yyyy-MM-dd";

        DateTime dateTime;
        if (DateTime.TryParseExact(dateString, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out dateTime))
        {
            DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTime);

            TimeSpan timeSinceEpoch =
                dateTimeOffset.UtcDateTime - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

            long timestamp = (long)timeSinceEpoch.TotalMilliseconds;

            return timestamp;
        }

        return 0;
    }


    public static List<long> GetRangeDayList(long start, long end)
    {
        DateTime startTime = DateTimeOffset.FromUnixTimeMilliseconds(start).UtcDateTime;
        DateTime endTime = DateTimeOffset.FromUnixTimeMilliseconds(end).UtcDateTime;
        List<long> dailyTimestamps = new List<long>();

        TimeSpan timeSpan = endTime - startTime;

        for (int i = 0; i <= timeSpan.Days; i++)
        {
            DateTime dailyStart = startTime.AddDays(i);
            long dailyTimestamp = ((DateTimeOffset)dailyStart).ToUnixTimeMilliseconds();
            dailyTimestamps.Add(dailyTimestamp);
        }

        return dailyTimestamps;
    }


    public static List<long> GetDayHourList(long milliseconds)
    {
        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).DateTime;


        List<long> hourTimestamps = new List<long>();

        for (int hour = 0; hour < 24; hour++)
        {
            DateTime hourDateTime = dateTime.AddHours(hour);

            TimeSpan timeSpan = hourDateTime - new DateTime(1970, 1, 1);

            long timestamp = (long)timeSpan.TotalMilliseconds;

            hourTimestamps.Add(timestamp);
        }

        return hourTimestamps;
    }

    public static long GetDateTimeLong(long milliseconds)
    {
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        DateTime dateTime = dateTimeOffset.DateTime;

        var dateTotalMilliseconds = GetDateTotalMilliseconds(dateTime);
        return dateTotalMilliseconds;
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

    public static long GetAfterDayTotalSeconds(long t)
    {
        DateTime currentDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(t);
        DateTime nextDate = currentDate.AddDays(1);
        return (long)(nextDate - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
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