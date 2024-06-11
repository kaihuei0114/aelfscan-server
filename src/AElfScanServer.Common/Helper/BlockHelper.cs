using System;
using AElf.Types;
using AElfScanServer.Common.Options;

namespace AElfScanServer.Common.Helper;

public class BlockHelper
{
    public static bool IsTxHash(string transactionId)
    {
        try
        {
            Hash.LoadFromHex(transactionId);
        }
        catch
        {
            return false;
        }

        return true;
    }


    public static bool IsAddress(string address)
    {
        try
        {
            AElf.Types.Address.FromBase58(address);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool IsBlockHeight(string input)
    {
        if (int.TryParse(input, out int height) && height >= 0)
        {
            return true;
        }

        return false;
    }

    public static string GetContractName(GlobalOptions option, string chainId, string address)
    {
        try
        {
            if (option.ContractNames.TryGetValue(chainId, out var names))
            {
                if (names.TryGetValue(address, out var name))
                {
                    return name;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return "";
    }
}