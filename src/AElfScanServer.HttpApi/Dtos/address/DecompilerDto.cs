using System.Collections.Generic;

namespace AElfScanServer.HttpApi.Dtos.address;

public class GetContractFilesResponseDto
{
    public int Code { get; set; }
    public string Msg { get; set; }
    public string Version { get; set; }
    public List<DecompilerContractDto> Data { get; set; }
}

public class DecompilerContractDto
{
    public string Name { get; set; }
    public List<DecompilerContractFileDto> Files { get; set; }
}

public class DecompilerContractFileDto
{
    public string Name { get; set; }
    public string Content { get; set; }
    public string FileType { get; set; }
}