using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LuxFirmwareApp.Models;
using LuxFirmwareApp.Utils;

namespace LuxFirmwareApp.Services;

public class HttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _sessionId;
    private readonly CookieContainer _cookieContainer;
    private readonly HttpClientHandler _handler;

    public HttpClientService()
    {
        _cookieContainer = new CookieContainer();
        _handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        };
        _httpClient = new HttpClient(_handler);
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
    
    public string? SessionId => _sessionId;

    public async Task<T?> PostJsonAsync<T>(string url, Dictionary<string, string>? parameters = null)
    {
        try
        {
            var content = parameters != null 
                ? new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json")
                : new StringContent("{}", Encoding.UTF8, "application/json");

            // Add standard headers
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                // Make sure cookie is in container for this domain
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer header to mimic browser behavior
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));

            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from Set-Cookie header
            ExtractSessionIdFromResponse(response);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response body: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error in POST request to {url}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in POST request to {url}: {ex.Message}");
            throw;
        }
    }
    
    private void ExtractSessionIdFromResponse(HttpResponseMessage response)
    {
        // Extract JSESSIONID from Set-Cookie headers
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var cookieHeader in setCookieValues)
            {
                if (cookieHeader.Contains("JSESSIONID="))
                {
                    var parts = cookieHeader.Split(';');
                    foreach (var part in parts)
                    {
                        if (part.Trim().StartsWith("JSESSIONID="))
                        {
                            _sessionId = part.Trim().Substring("JSESSIONID=".Length);
                            break;
                        }
                    }
                }
            }
        }
        
        // Also check cookies from CookieContainer
        var uri = new Uri(response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "");
        var cookies = _cookieContainer.GetCookies(uri);
        foreach (Cookie cookie in cookies)
        {
            if (cookie.Name == "JSESSIONID")
            {
                _sessionId = cookie.Value;
                break;
            }
        }
    }
    
    public void SetSessionId(string sessionId)
    {
        _sessionId = sessionId;
        // Set in cookie container for all known base URLs
        var uris = new[]
        {
            new Uri(Constants.LUXPOWER_MAIN_URL),
            new Uri(Constants.LUXPOWER_SECONDARY_URL),
            new Uri(Constants.LUXPOWER_NA_URL),
            new Uri(Constants.EG4_MAIN_URL)
        };
        
        foreach (var uri in uris)
        {
            try
            {
                _cookieContainer.Add(uri, new Cookie("JSESSIONID", sessionId) { Path = "/" });
            }
            catch { }
        }
    }
    
    public async Task<LoginResponse> LoginAsync(Platform platform, string account, string password, string language = "en")
    {
        var baseUrl = Constants.GetBaseUrlForPlatform(platform);
        var url = baseUrl + "api/login";
        
        // The server expects form-encoded parameters, not JSON
        var formData = new List<KeyValuePair<string, string>>
        {
            new("account", account),
            new("password", password),
            new("language", language),
            new("userPlatForm", platform.ToString()),
            new("withSatoken", "true")
        };
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from response
            ExtractSessionIdFromResponse(response);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Login error response: {errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, _jsonOptions);
            
            if (loginResponse?.Success == true && !string.IsNullOrEmpty(_sessionId))
            {
                Console.WriteLine($"Login successful. Session ID: {_sessionId}");
            }
            else if (loginResponse != null)
            {
                Console.WriteLine($"Login failed: {loginResponse.Message ?? "Unknown error"}");
            }
            
            return loginResponse ?? new LoginResponse { Success = false };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Login HTTP error: {ex.Message}");
            return new LoginResponse { Success = false, Message = ex.Message };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return new LoginResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<FirmwareListResponse> ListFirmwareByTypeAsync(Platform platform, FirmwareDeviceType deviceType, bool useBeta = false)
    {
        var baseUrl = Constants.GetMajorUrlForPlatform(platform);
        var endpoint = Constants.ENDPOINT_LIST_FIRMWARE;
        if (useBeta)
        {
            endpoint = endpoint.Replace("/listForAppByType", Constants.BETA_SUFFIX + "/listForAppByType");
        }
        var url = baseUrl + endpoint;

        // The server expects form-encoded parameters, not JSON
        var formData = new List<KeyValuePair<string, string>>
        {
            new("firmwareDeviceType", deviceType.ToString())
        };
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from Set-Cookie header
            ExtractSessionIdFromResponse(response);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response body: {errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FirmwareListResponse>(responseContent, _jsonOptions) ?? new FirmwareListResponse { Success = false };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error in POST request to {url}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in POST request to {url}: {ex.Message}");
            throw;
        }
    }

    public async Task<FirmwareDataResponse> GetFirmwareDataAsync(Platform platform, string recordId, int startIndex, bool useBeta = false)
    {
        var baseUrl = Constants.GetMajorUrlForPlatform(platform);
        var endpoint = Constants.ENDPOINT_GET_FIRMWARE_DATA;
        if (useBeta)
        {
            endpoint = endpoint.Replace("/getUploadFileAnalyzeInfo", Constants.BETA_SUFFIX + "/getUploadFileAnalyzeInfo");
        }
        var url = baseUrl + endpoint;

        // The server expects form-encoded parameters, not JSON
        var formData = new List<KeyValuePair<string, string>>
        {
            new("recordId", recordId),
            new("startIndex", startIndex.ToString())
        };
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from Set-Cookie header
            ExtractSessionIdFromResponse(response);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response body: {errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FirmwareDataResponse>(responseContent, _jsonOptions) ?? new FirmwareDataResponse { Success = false };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error in POST request to {url}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in POST request to {url}: {ex.Message}");
            throw;
        }
    }

    public async Task<FirmwareChangeLogResponse> GetFirmwareChangeLogAsync(Platform platform, FirmwareDeviceType deviceType)
    {
        // Use the firmwareRecord endpoint for changelogs (works for all brands)
        var url = Constants.FIRMWARE_RECORD_CHANGELOG_URL;
        var deviceTypeStr = deviceType.ToString();
        
        // Handle GEN_LB_EU_7_10K_GST special case
        if (deviceType == Models.FirmwareDeviceType.GEN_LB_EU_7_10K_GST)
        {
            deviceTypeStr = "GEN_LB_EU_7_10K";
        }

        // The server expects form-encoded parameters, not JSON
        // Note: This endpoint uses "selectType" instead of "firmwareDeviceType"
        var formData = new List<KeyValuePair<string, string>>
        {
            new("platform", platform.ToString()),
            new("selectType", deviceTypeStr)
        };
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from Set-Cookie header
            ExtractSessionIdFromResponse(response);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response body: {errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FirmwareChangeLogResponse>(responseContent, _jsonOptions) ?? new FirmwareChangeLogResponse { Success = false };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error in POST request to {url}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in POST request to {url}: {ex.Message}");
            throw;
        }
    }

    public async Task<FirmwareInfoResponse> GetFirmwareInfoAsync(Platform platform, FirmwareDeviceType deviceType)
    {
        var url = Constants.FIRMWARE_INFO_URL;
        
        // The server expects form-encoded parameters via POST, not GET query string
        var formData = new List<KeyValuePair<string, string>>
        {
            new("platform", platform.ToString()),
            new("selectType", deviceType.ToString())
        };
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Debug: print response for troubleshooting
            if (responseContent.Length > 0)
            {
                Console.WriteLine($"Firmware info response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...");
            }
            
            var firmwareInfoResponse = JsonSerializer.Deserialize<FirmwareInfoResponse>(responseContent, _jsonOptions);
            
            if (firmwareInfoResponse == null)
            {
                Console.WriteLine("Failed to deserialize firmware info response");
                return new FirmwareInfoResponse { Success = false, Code = (int)response.StatusCode };
            }
            
            // Check for authentication error
            if (firmwareInfoResponse.Code == 401 || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"Authentication required for firmware info endpoint. Code: {firmwareInfoResponse.Code}, Message: {firmwareInfoResponse.Message}");
                return firmwareInfoResponse;
            }
            
            // If response uses code instead of success, map it
            if (!firmwareInfoResponse.Success && firmwareInfoResponse.Code.HasValue)
            {
                firmwareInfoResponse.Success = firmwareInfoResponse.Code.Value == 200;
            }
            
            // If Success is false or not set, but we have data, treat it as success
            if (!firmwareInfoResponse.Success && firmwareInfoResponse.Data != null && firmwareInfoResponse.Data.Count > 0)
            {
                Console.WriteLine($"Warning: Response marked as unsuccessful but contains {firmwareInfoResponse.Data.Count} items");
                firmwareInfoResponse.Success = true;
            }
            
            if (!response.IsSuccessStatusCode && firmwareInfoResponse.Code != 401)
            {
                Console.WriteLine($"Firmware info HTTP error: {response.StatusCode}");
            }
            
            return firmwareInfoResponse;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Firmware info HTTP error: {ex.Message}");
            return new FirmwareInfoResponse { Success = false };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Firmware info error: {ex.Message}");
            return new FirmwareInfoResponse { Success = false };
        }
    }

    public async Task<GetAllFirmwareResponse> GetAllFirmwareAsync(Platform platform, bool isTest = false)
    {
        var url = Constants.GET_ALL_FIRMWARE_URL;
        
        // The server expects form-encoded parameters, not JSON
        var formData = new List<KeyValuePair<string, string>>
        {
            new("encrypted", "true"),
            new("platform", platform.ToString())
        };
        
        if (isTest)
        {
            formData.Add(new KeyValuePair<string, string>("isTest", "true"));
        }
        
        var content = new FormUrlEncodedContent(formData);
        
        try
        {
            var uri = new Uri(url);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            
            // Ensure session ID cookie is sent if we have one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    var existingCookie = _cookieContainer.GetCookies(uri)["JSESSIONID"];
                    if (existingCookie == null)
                    {
                        _cookieContainer.Add(uri, new Cookie("JSESSIONID", _sessionId) { Path = "/", Domain = uri.Host });
                    }
                }
                catch { }
            }
            
            // Add Referer and Origin headers
            request.Headers.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
            request.Headers.Add("Origin", uri.GetLeftPart(UriPartial.Authority));
            
            var response = await _httpClient.SendAsync(request);
            
            // Extract JSESSIONID from Set-Cookie header
            ExtractSessionIdFromResponse(response);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"GetAllFirmware error response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}");
                response.EnsureSuccessStatusCode();
            }
            
            var getAllFirmwareResponse = JsonSerializer.Deserialize<GetAllFirmwareResponse>(responseContent, _jsonOptions);
            
            return getAllFirmwareResponse ?? new GetAllFirmwareResponse();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"GetAllFirmware HTTP error: {ex.Message}");
            return new GetAllFirmwareResponse();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllFirmware error: {ex.Message}");
            return new GetAllFirmwareResponse();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

