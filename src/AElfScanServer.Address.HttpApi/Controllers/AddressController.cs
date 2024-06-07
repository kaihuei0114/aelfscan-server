using System.Threading.Tasks;
using AElfScanServer.Address.HttpApi.AppServices;
using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Core;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace AElfScanServer.Common.Address.HttpApi.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Address")]
[Route("api/app/address")]
public class AddressController : AElfScanServerAddressController
{
    private readonly IAddressAppService _addressAppService;
    private readonly IContractAppService _contractAppService;

    public AddressController(IAddressAppService addressAppService, IContractAppService contractAppService)
    {
        _addressAppService = addressAppService;
        _contractAppService = contractAppService;
    }

    // address common
    [HttpGet("detail")]
    public async Task<GetAddressDetailResultDto> GetAddressDetailAsync(GetAddressDetailInput input)
        => await _addressAppService.GetAddressDetailAsync(input);

    [HttpGet("tokens")]
    public async Task<GetAddressTokenListResultDto> GetAddressTokenListAsync(
        GetAddressTokenListInput input) => await _addressAppService.GetAddressTokenListAsync(input);
    
    [HttpGet("nft-assets")]
    public async Task<GetAddressNftListResultDto> GetNftAssetsAsync(
        GetAddressTokenListInput input) => await _addressAppService.GetAddressNftListAsync(input);

    [HttpGet("transfers")]
    public async Task<GetTransferListResultDto> GetTransferListAsync(GetTransferListInput input)
        => await _addressAppService.GetTransferListAsync(input);

 

    // account
    [HttpGet("accounts")]
    public async Task<GetAddressListResultDto> GetAddressListAsync(GetListInputInput input)
        => await _addressAppService.GetAddressListAsync(input);

    // contract
    [HttpGet("contracts")]
    public async Task<GetContractListResultDto> GetContractListAsync(GetContractContracts input)
        => await _contractAppService.GetContractListAsync(input);

    [HttpGet("contract/file")]
    public async Task<GetContractFileResultDto> GetContractFileAsync(
        GetContractFileInput input) => await _contractAppService.GetContractFileAsync(input);

    [HttpGet("contract/events")]
    public async Task<GetContractEventListResultDto> GetContractEventsAsync(
        GetContractEventContractsInput input) => await _contractAppService.GetContractEventsAsync(input);


    [HttpGet("contract/history")]
    public async Task<GetContractHistoryResultDto> GetContractHistoryAsync(
        GetContractHistoryInput input) => await _contractAppService.GetContractHistoryAsync(input);
}