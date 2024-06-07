﻿using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Token.HttpApi.Dtos.Input;
using AElfScanServer.Token.HttpApi.Service;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

using TokenPriceDto = AElfScanServer.Dtos.TokenPriceDto;
namespace AElfScanServer.Common.Token.HttpApi.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Token")]
[Route("api/app/token")]
public class TokenController : AbpControllerBase
{
    private readonly ITokenService _tokenService;

    public TokenController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet("list")]
    public async Task<ListResponseDto<TokenCommonDto>> GetTokenListAsync(TokenListInput input)
    {
        return await _tokenService.GetTokenListAsync(input);
    }

    [HttpGet("detail")]
    public async Task<TokenDetailDto> GetTokenListAsync(string chainId, string symbol)
    {
        return await _tokenService.GetTokenDetailAsync(symbol, chainId);
    }

    [HttpGet("transfers")]
    public async Task<TokenTransferInfosDto> GetTokenTransferInfosAsync(
        TokenTransferInput input)
    {
        return await _tokenService.GetTokenTransferInfosAsync(input);
    }

    [HttpGet("holders")]
    public async Task<ListResponseDto<TokenHolderInfoDto>> GetTokenTransferInfoAsync(
        TokenHolderInput input)
    {
        return await _tokenService.GetTokenHolderInfosAsync(input);
    }

    [HttpGet("price")]
    public async Task<TokenPriceDto> GetTokenPriceInfoAsync(CurrencyDto input)
    {
        return await _tokenService.GetTokenPriceInfoAsync(input);
    }

    [HttpGet("info")]
    public async Task<IndexerTokenInfoDto> GetTokenBaseInfoAsync(string symbol, string chainId)
    {
        return await _tokenService.GetTokenBaseInfoAsync(symbol, chainId);
    }
}