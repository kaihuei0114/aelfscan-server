using AElfScanServer.TokenDataFunction.Dtos;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace NFT.backend;

[RemoteService]
[Area("app")]
[ControllerName("Nft2")]
[Route("api/app2/token/nft/")]
public class NftController
{
    private readonly INftService _nftService;

    public NftController(INftService nftService)
    {
        _nftService = nftService;
    }

    [HttpGet("item-detail")]
    public async Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol)
    {
        return await _nftService.GetNftItemDetailAsync(chainId, symbol);
    }
    
}