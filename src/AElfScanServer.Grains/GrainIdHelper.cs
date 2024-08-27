namespace AElfScanServer.Grains;

public static class GrainIdHelper
{
    private static string BlockPushCheckGrainId => "BlockPushCheck";

    public static string GenerateAdsKey(params object[] ids)
    {
        return "ads" + ids.JoinAsString("-");
    }

    public static string GenerateAdsBannerKey(params object[] ids)
    {
        return "banner" + ids.JoinAsString("-");
    }
    
    public static string GenerateContractFileKey(params object[] ids)
    {
        return "file" + ids.JoinAsString("-");
    }
    
    public static string GenerateSynchronizationKey(params object[] ids)
    {
        return "synchronization" + ids.JoinAsString("-");
    }
   
}