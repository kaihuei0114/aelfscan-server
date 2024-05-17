using System;
using Nethereum.Util;

namespace AElfScanServer.Helper;

public class CommomHelper
{
    public static bool IsValidAddress(string address)
    {
        try
        {
            AElf.Types.Address.FromBase58(address);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public static string GetNftImageKey()
    {
        return "__nft_image_url";
    }
    
    
    public static string GetInscriptionImageKey()
    {
        return "inscription_image";
    }

    

    public static DateTime ConvertStringToDate(string s)
    {
        return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(s)).DateTime;
    }
}

public static class NumberFormatter
{
    public static string ToDecimalsString(this long number, int decimals)
    {
        var num = number / Math.Pow(10, decimals);
        return new BigDecimal(num).ToNormalizeString();
    }

    public static string ToDecimalsString(this double number, int decimals)
    {
        var num = number / Math.Pow(10, decimals);
        return new BigDecimal(num).ToNormalizeString();
    }

    public static string ToNormalizeString(this BigDecimal bigDecimal)
    {
        if (bigDecimal >= 0)
        {
            return bigDecimal.ToString();
        }

        var value = -bigDecimal;
        return "-" + value;
    }
}