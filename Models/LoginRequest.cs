using System.Text.Json.Serialization;

namespace LuxFirmwareApp.Models;

public class LoginRequest
{
    [JsonPropertyName("account")]
    public string Account { get; set; } = "";
    
    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
    
    [JsonPropertyName("userPlatForm")]
    public string? UserPlatform { get; set; }
    
    [JsonPropertyName("withSatoken")]
    public string? WithSatoken { get; set; } = "true";
}

public class LoginResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

