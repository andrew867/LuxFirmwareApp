using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class GetAllFirmwareResponse
{
    [JsonPropertyName("data")]
    public List<GetAllFirmwareItem>? Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool? Success { get; set; }
    
    [JsonPropertyName("code")]
    public int? Code { get; set; }
}

public class GetAllFirmwareItem
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

