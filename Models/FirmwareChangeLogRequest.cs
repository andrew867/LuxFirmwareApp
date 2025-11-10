using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class FirmwareChangeLogRequest
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";
    
    [JsonPropertyName("firmwareDeviceType")]
    public string FirmwareDeviceType { get; set; } = "";
}

public class FirmwareChangeLogResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("data")]
    public List<FirmwareChangeLogItem>? Data { get; set; }
}

public class FirmwareChangeLogItem
{
    [JsonPropertyName("fwCode")]
    public string? FwCode { get; set; }
    
    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    // Legacy properties for backward compatibility
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    
    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }
}

