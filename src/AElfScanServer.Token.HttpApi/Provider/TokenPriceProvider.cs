using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using Binance.Spot;
using AElfScanServer.Token.HttpApi.Dtos;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Common.Token.HttpApi.Provider;

public interface ITokenPriceProvider
{
    Task<Dictionary<string, BinancePriceDto>> GetPriceListAsync(TokenPriceDto tokenPriceDto);
    Task<BinancePriceDto> GetPriceAsync(CurrencyDto currency);
}

public class TokenPriceProvider : AbpRedisCache, ITokenPriceProvider, ISingletonDependency
{
    private readonly ILogger<TokenPriceProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;


    public TokenPriceProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<TokenPriceProvider> logger,
        IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public async Task<Dictionary<string, BinancePriceDto>> GetPriceListAsync(TokenPriceDto tokenPriceDto)
    {
        try
        {
            _logger.LogInformation("[TokenPriceProvider] [Binance] Price List Start.");
            var market = new Market();
            var symbols = tokenPriceDto.Currencies
                .Select(currency => $"{currency.BaseCurrency}{currency.QuoteCurrency}").ToList();

            var keys = symbols.Select(symbol => (RedisKey)symbol).ToArray();
            var values = await RedisDatabase.StringGetAsync(keys);
            var prices = new List<BinancePriceDto>();
            var missingSymbols = new List<string>();

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    prices.Add(_serializer.Deserialize<BinancePriceDto>(values[i]));
                }
                else
                {
                    missingSymbols.Add(symbols[i]);
                }
            }

            if (!missingSymbols.Any())
            {
                return prices.ToDictionary(price => price.Symbol, price => price);
            }

            var symbolPriceTicker =
                await market.TwentyFourHrTickerPriceChangeStatistics(
                    symbols: JsonConvert.SerializeObject(missingSymbols));

            var missingPrices = JsonConvert.DeserializeObject<List<BinancePriceDto>>(symbolPriceTicker);
            foreach (var price in missingPrices)
            {
                await RedisDatabase.StringSetAsync(price.Symbol, _serializer.Serialize(price),
                    TimeSpan.FromHours(2));
            }

            prices.AddRange(missingPrices);
            return prices.ToDictionary(price => price.Symbol, price => price);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenPriceProvider] [Binance] Parse response error.");
            return new Dictionary<string, BinancePriceDto>();
        }
    }

    public async Task<BinancePriceDto> GetPriceAsync(CurrencyDto currency)
    {
        try
        {
            _logger.LogInformation("[TokenPriceProvider] [Binance] Start.");
            var market = new Market();
            var symbol = $"{currency.BaseCurrency}{currency.QuoteCurrency}";

            await ConnectAsync();
            var redisValue = await RedisDatabase.StringGetAsync(symbol);
            if (redisValue.HasValue)
            {
                return _serializer.Deserialize<BinancePriceDto>(redisValue);
            }

            var symbolPriceTicker = await market.TwentyFourHrTickerPriceChangeStatistics(symbol);
            var binancePriceDto = JsonConvert.DeserializeObject<BinancePriceDto>(symbolPriceTicker);
            await RedisDatabase.StringSetAsync(symbol, _serializer.Serialize(binancePriceDto), TimeSpan.FromHours(2));
            return binancePriceDto;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TokenPriceProvider] [Binance] Parse response error.");
            return new BinancePriceDto();
        }
    }

    private static string FormatParam(IEnumerable<CurrencyDto> currencies)
    {
        var formattedCurrencies =
            currencies.Select(currency => $"{currency.BaseCurrency}{currency.QuoteCurrency}").ToList();
        return JsonConvert.SerializeObject(formattedCurrencies);
    }
}