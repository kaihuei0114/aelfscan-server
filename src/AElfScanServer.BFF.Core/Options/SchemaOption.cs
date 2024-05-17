using System.Collections.Generic;

namespace AElfScanServer.BFF.Core.Options;

public class SchemaOption
{
    public string Group { get; set; }

    public bool OpenReadFromS3 { get; set; } = true;

    public List<SchemaItem> Items { get; set; } = new();
}

public class SchemaItem
{
    public string Route { get; set; }
    public string FileKey { get; set; }

    public Dictionary<string, string> UrlDict { get; set; } = new ();
}