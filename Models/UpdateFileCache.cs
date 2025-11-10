using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class UpdateFileCache
{
    [JsonPropertyName("recordId")]
    public string? RecordId { get; set; }
    
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
    
    [JsonPropertyName("standard")]
    public string Standard { get; set; } = "";
    
    [JsonPropertyName("v1")]
    public int? V1 { get; set; }
    
    [JsonPropertyName("v2")]
    public int? V2 { get; set; }
    
    [JsonPropertyName("v3")]
    public int? V3 { get; set; }
    
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
    
    [JsonPropertyName("firmwareDeviceType")]
    public FirmwareDeviceType? FirmwareDeviceType { get; set; }
    
    [JsonPropertyName("tailEncoded")]
    public string? TailEncoded { get; set; }
    
    [JsonPropertyName("firmwareLengthArrayEncoded")]
    public string? FirmwareLengthArrayEncoded { get; set; }
    
    [JsonPropertyName("doneDownload")]
    public bool DoneDownload { get; set; }
    
    [JsonPropertyName("physicalAddr")]
    public Dictionary<int, int> PhysicalAddr { get; set; } = new();
    
    [JsonPropertyName("firmware")]
    public Dictionary<int, string> Firmware { get; set; } = new();
    
    public int? GetV(int index)
    {
        return index switch
        {
            1 => V1,
            2 => V2,
            3 => V3,
            _ => null
        };
    }
    
    public void SetV(int index, int? value)
    {
        switch (index)
        {
            case 1:
                V1 = value;
                break;
            case 2:
                V2 = value;
                break;
            case 3:
                V3 = value;
                break;
        }
    }
}

