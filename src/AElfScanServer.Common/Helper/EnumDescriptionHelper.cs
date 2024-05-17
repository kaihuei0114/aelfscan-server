using System;
using System.ComponentModel;
using AElfScanServer.Enums;

namespace AElfScanServer.Helper;

public class EnumDescriptionHelper
{
    public static string GetEnumDescription(TokenCreatedExternalInfoEnum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute));
        return attribute?.Description ?? value.ToString();
    }
}