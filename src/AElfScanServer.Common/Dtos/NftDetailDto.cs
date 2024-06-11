namespace AElfScanServer.Common.Dtos;

public class NftDetailDto : NftInfoDto
{
    public decimal FloorPrice { get; set; }
    public decimal? FloorPriceOfUsd { get; set; }
    public string TokenContractAddress { get; set; }
}