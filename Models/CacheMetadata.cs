using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class CacheMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("platforms")]
    public List<PlatformMetadata> Platforms { get; set; } = new();
}

public class PlatformMetadata
{
    [JsonPropertyName("platform")]
    public Platform Platform { get; set; }
    
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "";
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("deviceTypes")]
    public List<DeviceTypeMetadata> DeviceTypes { get; set; } = new();
}

public class DeviceTypeMetadata
{
    [JsonPropertyName("deviceType")]
    public FirmwareDeviceType DeviceType { get; set; }
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("firmwareList")]
    public List<FirmwareListItem> FirmwareList { get; set; } = new();
    
    [JsonPropertyName("cachedRecordIds")]
    public List<string> CachedRecordIds { get; set; } = new();
    
    [JsonPropertyName("changelog")]
    public List<FirmwareChangeLogItem>? Changelog { get; set; }
    
    [JsonPropertyName("changelogLastUpdated")]
    public DateTime? ChangelogLastUpdated { get; set; }
}

