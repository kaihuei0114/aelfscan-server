using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.TokenDataFunction.Dtos.Input;

namespace AElfScanServer.TokenDataFunction.Controllers;

public interface ITokenController
{
   Task<ListResponseDto<TokenCommonDto>> GetTokenListAsync(TokenListInput input);
}