using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Enums;
using AElfScanServer.Token.Dtos.Input;
using Volo.Abp.Application.Dtos;

namespace AElfScanServer.Common.Address.HttpApi.Dtos;

public class GetDetailBasicDto
{
    [Required] public string ChainId { get; set; }
}

public class GetListInputBasicDto : PagedResultRequestDto
{
    [Required] public string ChainId { get; set; }
    
    public string OrderBy { get; set; }
    
    public string Sort { get; set; }
    
    public List<OrderInfo> OrderInfos { get; set; }
    
    public List<string> SearchAfter { get; set; }
    
    public void OfOrderInfos(params (SortField sortField, SortDirection sortDirection)[] orderInfos)
    {
        OrderInfos = OrderInfo.BuildOrderInfos(orderInfos);
    }
}