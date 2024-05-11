using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.GraphQL;
using AElfScanServer.TokenDataFunction.Dtos;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using GraphQL;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.TokenDataFunction.Provider;

public interface ITokenIndexerProvider
{
    public Task<IndexerTokenInfoListDto> GetTokenListAsync(TokenListInput input);
    public Task<List<IndexerTokenInfoDto>> GetTokenDetailAsync(string chainId, string symbol);
    public Task<IndexerTokenTransferListDto> GetTokenTransferInfoAsync(TokenTransferInput input);
    Task<IndexerTokenHolderInfoListDto> GetTokenHolderInfoAsync(TokenHolderInput input);
    Task<List<HolderInfo>> GetHolderInfoAsync(string chainId, string address, SymbolType? symbolType = null);
    Task<HolderInfo> GetHolderInfoAsync(string chainId, string symbol, string address);
    Task<Dictionary<string, IndexerTokenInfoDto>> GetTokenDictAsync(string chainId, List<string> symbols);
}

public class TokenIndexerProvider : ITokenIndexerProvider, ISingletonDependency
{
    private readonly IGraphQlFactory _graphQlFactory;
    private readonly IObjectMapper _objectMapper;

    public TokenIndexerProvider(IGraphQlFactory graphQlFactory, IObjectMapper objectMapper)
    {
        _graphQlFactory = graphQlFactory;
        _objectMapper = objectMapper;
    }


    public async Task<IndexerTokenInfoListDto> GetTokenListAsync(TokenListInput input)
    {
        var graphQlHelper = GetGraphQlHelper();
        var indexerResult = await graphQlHelper.QueryAsync<IndexerTokenInfosDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$skipCount:Int!,$maxResultCount:Int!,
                        $types:[SymbolType!],$symbols:[String!],$collectionSymbols:[String!],
                        $sort:String,$orderBy:String){
                    tokenInfo(input: {chainId:$chainId,skipCount:$skipCount,maxResultCount:$maxResultCount,types:$types,
                        symbols:$symbols,collectionSymbols:$collectionSymbols,sort:$sort,orderBy:$orderBy})
                {
                   totalCount,
                   items{
                        tokenName,
                        symbol,
                        collectionSymbol,
    					type,
                        decimals,
                        totalSupply,
                        supply,
                        issued,
    					issuer,
                        owner,
                        isPrimaryToken
                        isBurnable,
                        issueChainId,
                        externalInfo { key, value },
                        holderCount,
                        transferCount
                  }
                }
            }",
            Variables = new
            {
                chainId = input.ChainId, types = input.Types, symbols = input.Symbols, skipCount = input.SkipCount,
                maxResultCount = input.MaxResultCount, collectionSymbols = input.CollectionSymbols,
                sort = input.Sort, orderBy = input.OrderBy
            }
        });
        return indexerResult?.TokenInfo ?? new IndexerTokenInfoListDto();
    }

    public async Task<List<IndexerTokenInfoDto>> GetTokenDetailAsync(string chainId, string symbol)
    {
        var graphQlHelper = GetGraphQlHelper();
        var indexerResult = await graphQlHelper.QueryAsync<IndexerTokenInfosDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$symbols:[String!],$skipCount:Int!,$maxResultCount:Int!){
                    tokenInfo(input: {chainId:$chainId,symbols:$symbols,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                       totalCount,
                       items{
                        tokenName,
                        symbol,
                        collectionSymbol,
    					type,
                        decimals,
                        totalSupply,
                        supply,
                        issued,
    					issuer,
                        owner,
                        isPrimaryToken
                        isBurnable,
                        issueChainId,
                        externalInfo { key, value },
                        holderCount,
                        transferCount
                  }
                }
            }",
            Variables = new
            {
                chainId, symbols = new ArrayList { symbol }, skipCount = 0, maxResultCount = 1
            }
        });
        return indexerResult.TokenInfo?.Items;
    }

    public async Task<IndexerTokenTransferListDto> GetTokenTransferInfoAsync(TokenTransferInput input)
    {
        var graphQlHelper = GetGraphQlHelper();
        var indexerResult = await graphQlHelper.QueryAsync<IndexerTokenTransfersDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$symbol:String!,$address:String,$collectionSymbol:String,
                    $search:String,$skipCount:Int!,$maxResultCount:Int!,$types:[SymbolType!],$sort:String,$orderBy:String){
                    transferInfo(input: {chainId:$chainId,symbol:$symbol,collectionSymbol:$collectionSymbol,address:$address,types:$types,search:$search,
                    skipCount:$skipCount,maxResultCount:$maxResultCount,sort:$sort,orderBy:$orderBy}){     
                    totalCount,
                    items{
                        metadata{chainId,block{blockHash,blockTime,blockHeight}},
                        transactionId,
                        from,
                        to,
                        method,
                        amount,
                        formatAmount
                        token {symbol, collectionSymbol, type, decimals}
                        status,
                        extraProperties{ key, value }
                  }                     
                }
            }",
            Variables = new
            {
                chainId = input.ChainId, symbol = input.Symbol, address = input.Address, search = input.Search,
                skipCount = input.SkipCount, maxResultCount = input.MaxResultCount,
                collectionSymbol = input.CollectionSymbol, types = input.Types,
                sort = input.Sort, orderBy = input.OrderBy
            }
        });
        return indexerResult == null ? new IndexerTokenTransferListDto() : indexerResult.TransferInfo;
    }

    public async Task<IndexerTokenHolderInfoListDto> GetTokenHolderInfoAsync(TokenHolderInput input)
    {
        var graphQlHelper = GetGraphQlHelper();
        var indexerResult = await graphQlHelper.QueryAsync<IndexerTokenHolderInfosDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$symbol:String!,$collectionSymbol:String,$skipCount:Int!,$maxResultCount:Int!,$address:String,
                    $types:[SymbolType!],$sort:String,$orderBy:String){
                    accountToken(input: {chainId:$chainId,symbol:$symbol,collectionSymbol:$collectionSymbol,skipCount:$skipCount,types:$types,
                    maxResultCount:$maxResultCount,address:$address,sort:$sort,orderBy:$orderBy}){
                    totalCount,
                    items{
                        address,
                        token {
                            symbol,
                            collectionSymbol,
                            type,
                            decimals
                        },
                        amount,
                        formatAmount,
                        transferCount,
                        firstNftTransactionId,
                        firstNftTime                        
                    }
                }
            }",
            Variables = new
            {
                chainId = input.ChainId, symbol = input.Symbol, collectionSymbol = input.CollectionSymbol,
                skipCount = input.SkipCount, maxResultCount = input.MaxResultCount, address = input.Address,
                types = input.Types, sort = input.Sort, orderBy = input.OrderBy
            }
        });
        return indexerResult == null ? new IndexerTokenHolderInfoListDto() : indexerResult.AccountToken;
    }

    private IGraphQlHelper GetGraphQlHelper()
    {
        return _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.TokenIndexer);
    }
    
    public async Task<List<HolderInfo>> GetHolderInfoAsync(string chainId, string address, SymbolType? symbolType = null)
    {
        return await GetHolderInfosAsync(chainId, null, address, symbolType);
    }
    
    public async Task<HolderInfo> GetHolderInfoAsync(string chainId, string symbol, string address)
    {
        var list = await GetHolderInfosAsync(chainId, symbol, address);
        return list.IsNullOrEmpty() ? new HolderInfo() : list[0];
    }
    
    private async Task<List<HolderInfo>> GetHolderInfosAsync(string chainId, string symbol, string address, SymbolType? symbolType = null)
    {
        var tokenHolderInput = new TokenHolderInput
        {
            ChainId = chainId,
            Address = address,
            MaxResultCount = 1000
        };
        if (!symbol.IsNullOrEmpty())
        {
            tokenHolderInput.Symbol = symbol;
        }
        if (symbolType != null)
        {
            tokenHolderInput.Types = new List<SymbolType> { symbolType.Value };
        }
        var indexerNftHolder = await GetTokenHolderInfoAsync(tokenHolderInput);
        return indexerNftHolder.Items.Select(i => new HolderInfo
        {
            Balance = i.FormatAmount,
            Symbol = i.Token.Symbol
        }).ToList();
    }
    
    public async Task<Dictionary<string, IndexerTokenInfoDto>> GetTokenDictAsync(string chainId, List<string> symbols)
    {
        var input = new TokenListInput
        {
            ChainId = chainId,
            Symbols = symbols,
            MaxResultCount = symbols.Count
        };
        var indexerTokenListDto = await GetTokenListAsync(input);
        return indexerTokenListDto.Items.ToDictionary(token => token.Symbol, token => token);
    }
}