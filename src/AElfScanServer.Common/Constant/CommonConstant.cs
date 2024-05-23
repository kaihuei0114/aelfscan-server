namespace AElfScanServer.Constant;

public static class CommonConstant
{
    public const long LongError = -1;
    public const string Comma = ",";
    public const string Underline = "_";
    public const int UsdValueDecimals = 2;
    public const int UsdPriceValueDecimals = 8;
    public const int ElfValueDecimals = 8;
    public const int LargerPercentageValueDecimals = 8;
    public const int PercentageValueDecimals = 4;
    public const string DefaultMarket = "Forest";
    public const int DefaultMaxResultCount = 1000;
    public const string SearchKeyPattern = "[^a-zA-Z0-9-_]";
}


public class CurrencyConstant
{
    public const string UsdCurrency = "USDT";
    public const string ElfCurrency = "ELF";
}