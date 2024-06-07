using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Token.HttpApi.Dtos;
using AElfScanServer.Token.HttpApi.Dtos.Input;
using AElfScanServer.Token.HttpApi.Service;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace AElfScanServer.Common.Token.HttpApi.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Nft")]
[Route("api/app/token/nft/")]
public class NftController
{
    private readonly INftService _nftService;

    public NftController(INftService nftService)
    {
        _nftService = nftService;
    }

    [HttpGet("collection-list")]
    public async Task<ListResponseDto<NftInfoDto>> GetNftCollectionListAsync(TokenListInput input)
    {
        return await _nftService.GetNftCollectionListAsync(input);
    }

    [HttpGet("collection-detail")]
    public async Task<NftDetailDto> GetNftCollectionDetailAsync(string chainId, string collectionSymbol)
    {
        return await _nftService.GetNftCollectionDetailAsync(chainId, collectionSymbol);
    }

    [HttpGet("transfers")]
    public async Task<NftTransferInfosDto> GetNftCollectionTransferInfosAsync(TokenTransferInput input)
    {
        return await _nftService.GetNftCollectionTransferInfosAsync(input);
    }

    [HttpGet("holders")]
    public async Task<ListResponseDto<TokenHolderInfoDto>> GetNftCollectionHolderInfosAsync(
        TokenHolderInput input)
    {
        return await _nftService.GetNftCollectionHolderInfosAsync(input);
    }
    
    [HttpGet("inventory")]
    public async Task<NftInventorysDto> GetNftCollectionInventoryAsync(NftInventoryInput input)
    {
        return await _nftService.GetNftCollectionInventoryAsync(input);
    }

    [HttpGet("item-detail")]
    public async Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol)
    {
        return await _nftService.GetNftItemDetailAsync(chainId, symbol);
    }
    
    [HttpGet("item-activity")]
    public async Task<ListResponseDto<NftItemActivityDto>> GetNftItemDetailAsync(NftItemActivityInput input)
    {
        return await _nftService.GetNftItemActivityAsync(input);
    }
    
    [HttpGet("item-holders")]
    public async Task<ListResponseDto<NftItemHolderInfoDto>> GetNftItemHoldersAsync(NftItemHolderInfoInput input)
    {
        return await _nftService.GetNftItemHoldersAsync(input);
    }
}