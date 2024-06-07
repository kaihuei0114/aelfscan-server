using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.HttpApi.Dtos.Input;

namespace AElfScanServer.Common.Token.HttpApi.Controllers;

public interface ITokenController
{
   Task<ListResponseDto<TokenCommonDto>> GetTokenListAsync(TokenListInput input);
}