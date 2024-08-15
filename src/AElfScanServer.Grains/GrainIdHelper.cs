namespace AeFinder.Grains;

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
    public static string GenerateAppSubscriptionGrainId(string appId)
    {
        return GenerateAdsKey(appId);
    }

    public static string GenerateAeFinderNameGrainId(string appName)
    {
        const string namePrefix = "AeFinderApp";
        return GenerateAdsKey(namePrefix, appName);
    }

    public static string GenerateAeFinderAppGrainId(string adminId)
    {
        const string namePrefix = "AeFinderApp";
        return GenerateAdsKey(namePrefix, adminId);
    }

    public static string GenerateBlockPusherGrainId(string appId, string version, string chainId)
    {
        return GenerateAdsKey(appId, version, chainId);
    }

    public static int GenerateBlockPusherManagerGrainId()
    {
        return 0;
    }

    public static string GenerateGetAppCodeGrainId(string appId, string version)
    {
        return GenerateAdsKey(appId, version);
    }

    public static string GenerateAppStateGrainId(string appId, string version, string chainId, string key)
    {
        return GenerateAdsKey(appId, version, chainId, key);
    }

    public static string GenerateAppBlockStateSetStatusGrainId(string appId, string version, string chainId)
    {
        return GenerateAdsKey(appId, version, chainId);
    }

    public static string GenerateAppBlockStateSetGrainId(string appId, string version, string chainId, string blockHash)
    {
        return GenerateAdsKey(appId, version, chainId, blockHash);
    }

    public static string GenerateBlockPushCheckGrainId()
    {
        return GenerateAdsKey(BlockPushCheckGrainId);
    }

    public static string GenerateUserAppsGrainId(string userId)
    {
        const string userAppPrefix = "UserApps";
        return GenerateAdsKey(userAppPrefix, userId);
    }

    public static string GenerateAppGrainId(string appId)
    {
        return GenerateAdsKey(appId);
    }

    public static string GenerateOrganizationAppGrainId(string organizationId)
    {
        return GenerateAdsKey(organizationId);
    }

    public static string GenerateAppBlockStateChangeGrainId(string appId, string version, string chainId,
        long blockHeight)
    {
        return GenerateAdsKey(appId, version, chainId, blockHeight);
    }

    public static int GenerateMessageStreamNamespaceManagerGrainId()
    {
        return 0;
    }
}