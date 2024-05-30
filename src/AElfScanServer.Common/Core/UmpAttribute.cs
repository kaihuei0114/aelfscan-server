using System;

namespace AElfScanServer.Core;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class UmpAttribute : Attribute
{
    public UmpAttribute()
    {
    }
}