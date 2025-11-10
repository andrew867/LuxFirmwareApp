using System.Text.Json;
using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class FirmwareListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("rows")]
    public List<FirmwareListItem> Rows { get; set; } = new();
}

public class FirmwareListItem
{
    [JsonPropertyName("recordId")]
    [JsonConverter(typeof(StringOrNumberConverter))]
    public string RecordId { get; set; } = "";
    
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";
    
    [JsonPropertyName("standard")]
    public string Standard { get; set; } = "";
    
    [JsonPropertyName("v1")]
    public int V1 { get; set; }
    
    [JsonPropertyName("v2")]
    public int V2 { get; set; }
    
    [JsonPropertyName("v3")]
    public int V3 { get; set; }
}

// Custom converter to handle recordId as either string or number
public class StringOrNumberConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? "";
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64().ToString();
        }
        else
        {
            throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

