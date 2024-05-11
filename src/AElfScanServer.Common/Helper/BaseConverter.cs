using System.Collections.Generic;
using System.Linq;
using AElfScanServer.Dtos;

namespace AElfScanServer.Helper;

public class BaseConverter
{
    public static string OfChainId(MetadataDto metadata)
    {
        return metadata?.ChainId;
    }
    
    public static long OfBlockHeight(MetadataDto metadata)
    {
        return metadata?.Block?.BlockHeight ?? 0;
    }
    
    public static long OfBlockTime(MetadataDto metadata)
    {
        var blockTime = metadata?.Block?.BlockTime;
        if (blockTime == null)
        {
            return 0;
        }
        var blockTimeNew = blockTime.Value;
        return TimeHelper.GetTimeStampFromDateTimeInSeconds(blockTimeNew);
    }
    
    public static string OfExternalInfoKeyValue(List<ExternalInfoDto> externalInfo, string key)
    {
        return externalInfo.Where(e => e.Key == key).Select(e => e.Value).FirstOrDefault();
    }
}