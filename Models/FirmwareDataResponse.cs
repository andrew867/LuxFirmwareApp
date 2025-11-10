using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class FirmwareDataResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
    
    [JsonPropertyName("fileType")]
    public int FileType { get; set; }
    
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
    
    [JsonPropertyName("crc32")]
    public long Crc32 { get; set; }
    
    [JsonPropertyName("bmsHeaderId")]
    public int? BmsHeaderId { get; set; }
    
    [JsonPropertyName("isLuxVersion")]
    public bool IsLuxVersion { get; set; }
    
    [JsonPropertyName("fileHandleType")]
    public int? FileHandleType { get; set; }
    
    [JsonPropertyName("tailEncoded")]
    public string? TailEncoded { get; set; }
    
    [JsonPropertyName("firmwareLengthArrayEncoded")]
    public string? FirmwareLengthArrayEncoded { get; set; }
    
    [JsonPropertyName("physicalAddrData")]
    public List<PhysicalAddrItem>? PhysicalAddrData { get; set; }
    
    [JsonPropertyName("firmwareData")]
    public List<FirmwareDataItem> FirmwareData { get; set; } = new();
    
    [JsonPropertyName("hasNext")]
    public bool HasNext { get; set; }
}

public class PhysicalAddrItem
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("physicalAddr")]
    public int PhysicalAddr { get; set; }
}

public class FirmwareDataItem
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("data")]
    public string Data { get; set; } = "";
}

