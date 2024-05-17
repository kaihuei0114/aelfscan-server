using System;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Dtos;
using AElfScanServer.Token.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Token;

public interface ITokenPriceService
{
    Task<TokenPriceDto> GetTokenPriceAsync(string baseCoin, string quoteCoin);
    
    Task<TokenPriceDto> GetTokenHistoryPriceAsync(string baseCoin, string quoteCoin, long timestamp);
}

public class TokenPriceService : ITokenPriceService, ISingletonDependency
{
    private readonly ILogger<TokenPriceService> _logger;
    private readonly ITokenExchangeProvider _tokenExchangeProvider;

    public TokenPriceService(ILogger<TokenPriceService> logger, ITokenExchangeProvider tokenExchangeProvider)
    {
        _logger = logger;
        _tokenExchangeProvider = tokenExchangeProvider;
    }

    public async Task<TokenPriceDto> GetTokenPriceAsync(string baseCoin, string quoteCoin)
    {
        try
        {
            AssertHelper.IsTrue(!baseCoin.IsNullOrEmpty() && !quoteCoin.IsNullOrEmpty(),
                "Get token price fail, baseCoin or quoteCoin is empty.");
            if (baseCoin.ToUpper().Equals(quoteCoin.ToUpper()))
            {
                return new TokenPriceDto { Price = 1.00m };
            }
            var exchange = await _tokenExchangeProvider.GetAsync(baseCoin, quoteCoin);
            AssertHelper.NotEmpty(exchange, $"Exchange data {baseCoin}/{quoteCoin} not found.", baseCoin, quoteCoin);
            var avgExchange = exchange.Values
                .Where(ex => ex.Exchange > 0)
                .Average(ex => ex.Exchange);
            AssertHelper.IsTrue(avgExchange > 0, "Exchange amount error {avgExchange}", avgExchange);
            return new TokenPriceDto
            {
                Price = avgExchange
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[GetTokenPriceAsync] error.");
            return new TokenPriceDto();
        }
    }

    public async Task<TokenPriceDto> GetTokenHistoryPriceAsync(string baseCoin, string quoteCoin, long timestamp)
    {
        try
        {
            AssertHelper.IsTrue(!baseCoin.IsNullOrEmpty() && !quoteCoin.IsNullOrEmpty() && timestamp > 0,
                "Get token price fail, baseCoin or quoteCoin is empty.");
            if (baseCoin.ToUpper().Equals(quoteCoin.ToUpper()))
            {
                return new TokenPriceDto { Price = 1.00m };
            }
            var exchange = await _tokenExchangeProvider.GetHistoryAsync(baseCoin, quoteCoin, timestamp);
            AssertHelper.NotEmpty(exchange, $"History Exchange data {baseCoin}/{quoteCoin} timestamp {timestamp} not found.", 
                baseCoin, quoteCoin, timestamp);
            var avgExchange = exchange.Values
                .Where(ex => ex.Exchange > 0)
                .Average(ex => ex.Exchange);
            AssertHelper.IsTrue(avgExchange > 0, "History Exchange amount error {avgExchange}", avgExchange);
            return new TokenPriceDto
            {
                Price = avgExchange
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[GetTokenHistoryPriceAsync] error.");
            return new TokenPriceDto();
        }
    }
}