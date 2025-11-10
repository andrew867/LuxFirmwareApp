using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class FirmwareInfoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("code")]
    public int? Code { get; set; }
    
    [JsonPropertyName("msg")]
    public string? Message { get; set; }
    
    [JsonPropertyName("data")]
    public List<FirmwareInfoItem>? Data { get; set; }
    
    // Helper property to check if request was successful
    public bool IsSuccess => Success || (Code.HasValue && Code.Value == 200) || (Data != null && Data.Count > 0);
}

public class FirmwareInfoItem
{
    [JsonPropertyName("sourceName")]
    public string? SourceName { get; set; }
    
    [JsonPropertyName("datalogType")]
    public string? DatalogType { get; set; }
    
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }
}

