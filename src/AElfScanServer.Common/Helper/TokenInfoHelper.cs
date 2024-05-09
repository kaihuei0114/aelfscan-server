using System;
using System.Collections.Generic;
using System.Linq;
using AElfScanServer.Dtos;
using AElfScanServer.Enums;

namespace AElfScanServer.Helper;

public class TokenInfoHelper
{
    public static string GetImageUrl(List<ExternalInfoDto> externalInfo, Func<string> getImageUrlFunc)
    {
        var keysToCheck = new List<TokenCreatedExternalInfoEnum>
        {
            TokenCreatedExternalInfoEnum.NFTLogoImageUrl,
            TokenCreatedExternalInfoEnum.SpecialInscriptionImage,
            TokenCreatedExternalInfoEnum.NFTImageUri
        };

        foreach (var key in keysToCheck)
        {
            var imageUrl = OfExternalInfoKeyValue(externalInfo, key);
            if (!imageUrl.IsNullOrWhiteSpace())
            {
                return imageUrl;
            }
        }
        return getImageUrlFunc();
    }
    
    public static string OfExternalInfoKeyValue(List<ExternalInfoDto> externalInfo, TokenCreatedExternalInfoEnum keyEnum)
    {
        var key = EnumDescriptionHelper.GetEnumDescription(keyEnum);
        return externalInfo.Where(e => e.Key == key).Select(e => e.Value).FirstOrDefault();
    }

}