using System.Text.Json;
using System.Text.Json.Serialization;
using LuxFirmwareApp.Models;
using LuxFirmwareApp.Utils;

namespace LuxFirmwareApp.Services;

public class FirmwareDownloader
{
    private readonly HttpClientService _httpClient;
    private readonly string _firmwareDir;

    public FirmwareDownloader(string? firmwareDir = null)
    {
        _httpClient = new HttpClientService();
        _firmwareDir = firmwareDir ?? Constants.DEFAULT_FIRMWARE_DIR;
        
        if (!Directory.Exists(_firmwareDir))
        {
            Directory.CreateDirectory(_firmwareDir);
        }
    }
    
    public void SetSessionId(string sessionId)
    {
        _httpClient.SetSessionId(sessionId);
    }
    
    public async Task<bool> LoginAsync(Platform platform, string username, string password, string language = "en")
    {
        var response = await _httpClient.LoginAsync(platform, username, password, language);
        return response.Success;
    }
    
    public string? GetSessionId()
    {
        return _httpClient.SessionId;
    }

    public async Task<List<FirmwareListItem>> ListFirmwareAsync(Platform platform, FirmwareDeviceType deviceType, bool useBeta = false)
    {
        Console.WriteLine($"Listing firmware for {platform} - {deviceType.GetDisplayName()}...");
        
        try
        {
            // WiFi dongle uses a different endpoint
            if (deviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE)
            {
                return await ListWiFiDongleFirmwareAsync(platform);
            }
            
            var response = await _httpClient.ListFirmwareByTypeAsync(platform, deviceType, useBeta);
            
            if (!response.Success)
            {
                Console.WriteLine("Failed to list firmware.");
                return new List<FirmwareListItem>();
            }

            Console.WriteLine($"Found {response.Rows.Count} firmware file(s).");
            
            // Update metadata
            var metadata = LoadCacheMetadata();
            var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
            if (platformMetadata == null)
            {
                platformMetadata = new PlatformMetadata
                {
                    Platform = platform,
                    BaseUrl = Constants.GetBaseUrlForPlatform(platform)
                };
                metadata.Platforms.Add(platformMetadata);
            }
            
            var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
            if (deviceTypeMetadata == null)
            {
                deviceTypeMetadata = new DeviceTypeMetadata
                {
                    DeviceType = deviceType,
                    DisplayName = deviceType.GetDisplayName()
                };
                platformMetadata.DeviceTypes.Add(deviceTypeMetadata);
            }
            
            deviceTypeMetadata.LastUpdated = DateTime.UtcNow;
            deviceTypeMetadata.FirmwareList = response.Rows;
            
            // Update cached record IDs
            var cachedRecordIds = response.Rows
                .Where(item => GetFirmwareByRecordId(item.RecordId, platform) != null)
                .Select(item => item.RecordId)
                .ToList();
            deviceTypeMetadata.CachedRecordIds = cachedRecordIds;
            
            // Note: Changelog download is handled in DownloadAllFirmwareAsync to avoid duplicates
            // Only download here if not in bulk mode
            
            metadata.LastUpdated = DateTime.UtcNow;
            await SaveCacheMetadataAsync(metadata);
            
            return response.Rows;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("400"))
        {
            // 400 errors are expected for some device types that aren't supported by the server
            Console.WriteLine($"    Device type {deviceType.GetDisplayName()} not supported by server (400 error)");
            return new List<FirmwareListItem>();
        }
    }

    private async Task<List<FirmwareListItem>> ListWiFiDongleFirmwareAsync(Platform platform)
    {
        // Use getAllFirmware endpoint (same as Java app uses during login)
        var response = await _httpClient.GetAllFirmwareAsync(platform);
        
        if (response.Data == null || response.Data.Count == 0)
        {
            Console.WriteLine($"No WiFi dongle firmware found from getAllFirmware endpoint. Data count: {response.Data?.Count ?? 0}");
            return new List<FirmwareListItem>();
        }
        
        Console.WriteLine($"Found {response.Data.Count} total firmware item(s) from getAllFirmware API");

        // Convert GetAllFirmwareItem to FirmwareListItem, filtering for WiFi dongle types
        var firmwareList = new List<FirmwareListItem>();
        int recordIdCounter = 10000; // Use a high starting ID to avoid conflicts
        
        foreach (var item in response.Data)
        {
            if (string.IsNullOrEmpty(item.SourceName))
                continue;
            
            // Filter by datalogType (ESP_WIFI, ESP_WIFI6, ESP_WIFI_E)
            var datalogType = item.DatalogType ?? "";
            if (datalogType != "ESP_WIFI" && datalogType != "ESP_WIFI6" && datalogType != "ESP_WIFI_E")
                continue;
            
            firmwareList.Add(new FirmwareListItem
            {
                RecordId = recordIdCounter++.ToString(),
                FileName = item.SourceName,
                Standard = "",
                V1 = -1,
                V2 = -1,
                V3 = -1
            });
        }

        Console.WriteLine($"Found {firmwareList.Count} WiFi dongle firmware file(s) after filtering.");
        
        // Update metadata
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        if (platformMetadata == null)
        {
            platformMetadata = new PlatformMetadata
            {
                Platform = platform,
                BaseUrl = Constants.GetBaseUrlForPlatform(platform)
            };
            metadata.Platforms.Add(platformMetadata);
        }
        
        var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE);
        if (deviceTypeMetadata == null)
        {
            deviceTypeMetadata = new DeviceTypeMetadata
            {
                DeviceType = FirmwareDeviceType.DONGLE_E_WIFI_DONGLE,
                DisplayName = FirmwareDeviceType.DONGLE_E_WIFI_DONGLE.GetDisplayName()
            };
            platformMetadata.DeviceTypes.Add(deviceTypeMetadata);
        }
        
        deviceTypeMetadata.LastUpdated = DateTime.UtcNow;
        deviceTypeMetadata.FirmwareList = firmwareList;
        
        metadata.LastUpdated = DateTime.UtcNow;
        await SaveCacheMetadataAsync(metadata);
        
        return firmwareList;
    }

    public async Task<UpdateFileCache?> DownloadFirmwareAsync(Platform platform, string recordId, FirmwareDeviceType deviceType, bool useBeta = false)
    {
        // WiFi dongle firmware is downloaded directly from the remote firmware URL
        if (deviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE)
        {
            return await DownloadWiFiDongleFirmwareAsync(platform, recordId);
        }
        
        Console.WriteLine($"Downloading firmware record {recordId}...");
        
        var updateFileCache = new UpdateFileCache
        {
            RecordId = recordId,
            FirmwareDeviceType = deviceType,
            DoneDownload = false
        };

        int startIndex = 1;
        bool hasNext = true;

        while (hasNext)
        {
            var response = await _httpClient.GetFirmwareDataAsync(platform, recordId, startIndex, useBeta);
            
            if (!response.Success)
            {
                Console.WriteLine($"Failed to download firmware data at index {startIndex}.");
                return null;
            }

            // Initialize cache on first response
            if (startIndex == 1)
            {
                updateFileCache.FileName = response.FileName;
                updateFileCache.FileType = response.FileType;
                updateFileCache.FileSize = response.FileSize;
                updateFileCache.Crc32 = response.Crc32;
                updateFileCache.IsLuxVersion = response.IsLuxVersion;
                updateFileCache.TailEncoded = response.TailEncoded;
                updateFileCache.FirmwareLengthArrayEncoded = response.FirmwareLengthArrayEncoded;
                
                if (response.BmsHeaderId.HasValue)
                {
                    updateFileCache.BmsHeaderId = response.BmsHeaderId;
                }
                
                if (response.FileHandleType.HasValue)
                {
                    updateFileCache.FileHandleType = response.FileHandleType;
                }

                // Process physical address data
                if (response.PhysicalAddrData != null)
                {
                    foreach (var item in response.PhysicalAddrData)
                    {
                        updateFileCache.PhysicalAddr[item.Index] = item.PhysicalAddr;
                    }
                }
            }

            // Process firmware data chunks
            foreach (var item in response.FirmwareData)
            {
                updateFileCache.Firmware[item.Index] = item.Data;
            }

            hasNext = response.HasNext;
            startIndex += response.FirmwareData.Count;
            
            Console.WriteLine($"Downloaded {updateFileCache.Firmware.Count} firmware chunks...");
        }

        updateFileCache.DoneDownload = true;
        
        // Save to disk (with platform subdirectory)
        await SaveFirmwareToDiskAsync(updateFileCache, platform);
        
        // Update metadata
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        if (platformMetadata != null)
        {
            var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
            if (deviceTypeMetadata != null)
            {
                if (!deviceTypeMetadata.CachedRecordIds.Contains(recordId))
                {
                    deviceTypeMetadata.CachedRecordIds.Add(recordId);
                }
                deviceTypeMetadata.LastUpdated = DateTime.UtcNow;
            }
        }
        metadata.LastUpdated = DateTime.UtcNow;
        await SaveCacheMetadataAsync(metadata);
        
        Console.WriteLine($"Firmware download completed: {updateFileCache.FileName}");
        return updateFileCache;
    }

    private async Task<UpdateFileCache?> DownloadWiFiDongleFirmwareAsync(Platform platform, string recordId)
    {
        // Find the firmware file name from the metadata
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        var deviceTypeMetadata = platformMetadata?.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE);
        
        var firmwareItem = deviceTypeMetadata?.FirmwareList.FirstOrDefault(f => f.RecordId == recordId);
        if (firmwareItem == null || string.IsNullOrEmpty(firmwareItem.FileName))
        {
            Console.WriteLine($"WiFi dongle firmware record {recordId} not found in metadata.");
            return null;
        }
        
        var fileName = firmwareItem.FileName;
        Console.WriteLine($"Downloading WiFi dongle firmware: {fileName}...");
        
        // Download directly from the remote firmware URL
        var firmwareUrl = Constants.REMOTE_FIRMWARE_URL + fileName;
        
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            
            var response = await httpClient.GetAsync(firmwareUrl);
            response.EnsureSuccessStatusCode();
            
            var firmwareBytes = await response.Content.ReadAsByteArrayAsync();
            
            // Create UpdateFileCache from downloaded bytes
            var updateFileCache = new UpdateFileCache
            {
                RecordId = recordId,
                FileName = fileName,
                FirmwareDeviceType = FirmwareDeviceType.DONGLE_E_WIFI_DONGLE,
                FileSize = firmwareBytes.Length,
                DoneDownload = true
            };
            
            // Store firmware as a single chunk (base64 encoded string)
            updateFileCache.Firmware[0] = Convert.ToBase64String(firmwareBytes);
            
            // Save to disk
            await SaveFirmwareToDiskAsync(updateFileCache, platform);
            
            // Update metadata
            if (deviceTypeMetadata != null)
            {
                if (!deviceTypeMetadata.CachedRecordIds.Contains(recordId))
                {
                    deviceTypeMetadata.CachedRecordIds.Add(recordId);
                }
                deviceTypeMetadata.LastUpdated = DateTime.UtcNow;
            }
            metadata.LastUpdated = DateTime.UtcNow;
            await SaveCacheMetadataAsync(metadata);
            
            Console.WriteLine($"WiFi dongle firmware download completed: {fileName} ({firmwareBytes.Length} bytes)");
            return updateFileCache;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading WiFi dongle firmware {fileName}: {ex.Message}");
            return null;
        }
    }

    public async Task SaveFirmwareToDiskAsync(UpdateFileCache cache, Platform? platform = null)
    {
        if (string.IsNullOrEmpty(cache.RecordId))
        {
            throw new ArgumentException("RecordId is required to save firmware.");
        }

        // Create platform subdirectory if provided
        var targetDir = _firmwareDir;
        if (platform.HasValue)
        {
            targetDir = Path.Combine(_firmwareDir, platform.Value.ToString());
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
        }

        // Use original filename if available, otherwise use recordId
        var fileName = !string.IsNullOrEmpty(cache.FileName) 
            ? cache.FileName 
            : $"{cache.RecordId}.bin";
        
        // Ensure filename is safe (remove invalid characters)
        fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        
        var filePath = Path.Combine(targetDir, fileName);
        
        // Also save a metadata file with recordId for lookup
        var metadataPath = Path.Combine(targetDir, $"{cache.RecordId}.json");
        
        // For WiFi dongle firmware, save as binary file; for others, save as JSON
        if (cache.FirmwareDeviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE && cache.Firmware.Count > 0)
        {
            // Save as binary file (convert from base64 string to bytes)
            var firmwareDataBase64 = cache.Firmware[0];
            var firmwareData = Convert.FromBase64String(firmwareDataBase64);
            await File.WriteAllBytesAsync(filePath, firmwareData);
            
            // Also save metadata as JSON
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(metadataPath, json);
        }
        else
        {
            // Save as JSON (for standard firmware)
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(filePath, json);
            await File.WriteAllTextAsync(metadataPath, json);
        }
        
        Console.WriteLine($"Saved firmware to {filePath}");
    }

    public List<UpdateFileCache> RestoreFirmwareFromDisk()
    {
        var caches = new List<UpdateFileCache>();
        
        if (!Directory.Exists(_firmwareDir))
        {
            return caches;
        }

        // Search all platform subdirectories
        var platformDirs = Directory.GetDirectories(_firmwareDir);
        foreach (var platformDir in platformDirs)
        {
            var files = Directory.GetFiles(platformDir, "*.json");
            foreach (var file in files)
            {
                // Skip metadata file
                if (Path.GetFileName(file) == "_metadata.json")
                {
                    continue;
                }
                
                try
                {
                    var json = File.ReadAllText(file);
                    var cache = JsonSerializer.Deserialize<UpdateFileCache>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (cache != null)
                    {
                        caches.Add(cache);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error restoring firmware from {file}: {ex.Message}");
                    // Delete corrupted file
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }
        }
        
        // Also check root directory for backward compatibility
        var rootFiles = Directory.GetFiles(_firmwareDir, "*.json");
        foreach (var file in rootFiles)
        {
            // Skip metadata file
            if (Path.GetFileName(file) == "_metadata.json")
            {
                continue;
            }
            
            try
            {
                var json = File.ReadAllText(file);
                var cache = JsonSerializer.Deserialize<UpdateFileCache>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (cache != null && !caches.Any(c => c.RecordId == cache.RecordId))
                {
                    caches.Add(cache);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring firmware from {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"Restored {caches.Count} firmware file(s) from disk.");
        return caches;
    }

    public UpdateFileCache? GetFirmwareByRecordId(string recordId, Platform? platform = null)
    {
        // Try platform-specific directory first if provided
        if (platform.HasValue)
        {
            var platformDir = Path.Combine(_firmwareDir, platform.Value.ToString());
            var metadataPath = Path.Combine(platformDir, $"{recordId}.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(metadataPath);
                    return JsonSerializer.Deserialize<UpdateFileCache>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading firmware {recordId} from {platformDir}: {ex.Message}");
                }
            }
        }
        
        // Search all platform subdirectories
        if (Directory.Exists(_firmwareDir))
        {
            var platformDirs = Directory.GetDirectories(_firmwareDir);
            foreach (var platformDir in platformDirs)
            {
                var metadataPath = Path.Combine(platformDir, $"{recordId}.json");
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        var json = File.ReadAllText(metadataPath);
                        return JsonSerializer.Deserialize<UpdateFileCache>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading firmware {recordId} from {platformDir}: {ex.Message}");
                    }
                }
            }
        }
        
        // Fallback: check root directory (for backward compatibility)
        var rootPath = Path.Combine(_firmwareDir, recordId);
        if (File.Exists(rootPath))
        {
            try
            {
                var json = File.ReadAllText(rootPath);
                return JsonSerializer.Deserialize<UpdateFileCache>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading firmware {recordId}: {ex.Message}");
            }
        }
        
        return null;
    }

    public async Task<DownloadAllResult> DownloadAllFirmwareAsync(bool useBeta = false, bool skipExisting = true, Platform? filterPlatform = null)
    {
        var result = new DownloadAllResult();
        var allPlatforms = Enum.GetValues<Platform>();
        var allDeviceTypes = Enum.GetValues<FirmwareDeviceType>();
        
        // Filter platforms if specified
        if (filterPlatform.HasValue)
        {
            allPlatforms = new[] { filterPlatform.Value };
        }
        
        // Load existing metadata
        var metadata = LoadCacheMetadata();
        
        Console.WriteLine($"Starting bulk download for {allPlatforms.Length} platform(s) and {allDeviceTypes.Length} device type(s)...");
        if (filterPlatform.HasValue)
        {
            Console.WriteLine($"Filtered to platform: {filterPlatform.Value}");
        }
        Console.WriteLine();
        
        foreach (var platform in allPlatforms)
        {
            Console.WriteLine($"=== Processing {platform} ===");
            result.PlatformsProcessed++;
            
            // Get or create platform metadata
            var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
            if (platformMetadata == null)
            {
                platformMetadata = new PlatformMetadata
                {
                    Platform = platform,
                    BaseUrl = Constants.GetBaseUrlForPlatform(platform)
                };
                metadata.Platforms.Add(platformMetadata);
            }
            platformMetadata.LastUpdated = DateTime.UtcNow;
            
            var platformDeviceTypes = allDeviceTypes.Where(dt => dt.IsSupportedForPlatform(platform)).ToList();
            
            foreach (var deviceType in platformDeviceTypes)
            {
                try
                {
                    Console.WriteLine($"  Checking {deviceType.GetDisplayName()}...");
                    
                    // List firmware for this device type
                    var firmwareList = await ListFirmwareAsync(platform, deviceType, useBeta);
                    
                    // Get or create device type metadata
                    var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
                    if (deviceTypeMetadata == null)
                    {
                        deviceTypeMetadata = new DeviceTypeMetadata
                        {
                            DeviceType = deviceType,
                            DisplayName = deviceType.GetDisplayName()
                        };
                        platformMetadata.DeviceTypes.Add(deviceTypeMetadata);
                    }
                    deviceTypeMetadata.LastUpdated = DateTime.UtcNow;
                    deviceTypeMetadata.FirmwareList = firmwareList;
                    
                    // Download changelog for this device type (even if no firmware found)
                    var changelog = await DownloadChangelogAsync(platform, deviceType, skipExisting);
                    if (changelog != null && changelog.Count > 0)
                    {
                        result.ChangelogsDownloaded++;
                    }
                    else if (GetCachedChangelog(platform, deviceType) != null)
                    {
                        result.ChangelogsSkipped++;
                    }
                    else
                    {
                        result.ChangelogsFailed++;
                    }
                    
                    if (firmwareList.Count == 0)
                    {
                        Console.WriteLine($"    No firmware found for {deviceType.GetDisplayName()}");
                        result.DeviceTypesSkipped++;
                        continue;
                    }
                    
                    result.DeviceTypesProcessed++;
                    Console.WriteLine($"    Found {firmwareList.Count} firmware file(s)");
                    
                    // Download each firmware file
                    foreach (var item in firmwareList)
                    {
                        // Check if already downloaded
                        if (skipExisting)
                        {
                            var existing = GetFirmwareByRecordId(item.RecordId, platform);
                            if (existing != null && existing.DoneDownload)
                            {
                                Console.WriteLine($"    Skipping {item.FileName} (already cached)");
                                result.FilesSkipped++;
                                
                                // Update cached record IDs list
                                if (!deviceTypeMetadata.CachedRecordIds.Contains(item.RecordId))
                                {
                                    deviceTypeMetadata.CachedRecordIds.Add(item.RecordId);
                                }
                                continue;
                            }
                        }
                        
                        try
                        {
                            var firmware = await DownloadFirmwareAsync(platform, item.RecordId, deviceType, useBeta);
                            if (firmware != null)
                            {
                                result.FilesDownloaded++;
                                Console.WriteLine($"    ✓ Downloaded: {firmware.FileName}");
                                
                                // Update cached record IDs list
                                if (!deviceTypeMetadata.CachedRecordIds.Contains(item.RecordId))
                                {
                                    deviceTypeMetadata.CachedRecordIds.Add(item.RecordId);
                                }
                            }
                            else
                            {
                                result.FilesFailed++;
                                Console.WriteLine($"    ✗ Failed to download: {item.FileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FilesFailed++;
                            Console.WriteLine($"    ✗ Error downloading {item.FileName}: {ex.Message}");
                        }
                        
                        // Small delay to avoid overwhelming the server
                        await Task.Delay(500);
                    }
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("400"))
                {
                    // 400 errors are expected for some device types that aren't supported
                    Console.WriteLine($"    Device type {deviceType.GetDisplayName()} not supported by server (400 error)");
                    result.DeviceTypesSkipped++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ Error processing {deviceType.GetDisplayName()}: {ex.Message}");
                    result.DeviceTypesFailed++;
                }
            }
            
            Console.WriteLine();
        }
        
        // Save metadata
        metadata.LastUpdated = DateTime.UtcNow;
        await SaveCacheMetadataAsync(metadata);
        
        return result;
    }
    
    public async Task SaveCacheMetadataAsync(CacheMetadata metadata)
    {
        var metadataPath = Path.Combine(_firmwareDir, "_metadata.json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(metadataPath, json);
        Console.WriteLine($"Saved cache metadata to {metadataPath}");
    }
    
    public CacheMetadata LoadCacheMetadata()
    {
        var metadataPath = Path.Combine(_firmwareDir, "_metadata.json");
        
        if (!File.Exists(metadataPath))
        {
            return new CacheMetadata();
        }
        
        try
        {
            var json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<CacheMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return metadata ?? new CacheMetadata();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cache metadata: {ex.Message}");
            return new CacheMetadata();
        }
    }
    
    public List<Platform> GetCachedPlatforms()
    {
        var metadata = LoadCacheMetadata();
        return metadata.Platforms.Select(p => p.Platform).ToList();
    }
    
    public List<FirmwareDeviceType> GetCachedDeviceTypes(Platform platform)
    {
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        if (platformMetadata == null)
        {
            return new List<FirmwareDeviceType>();
        }
        return platformMetadata.DeviceTypes.Select(dt => dt.DeviceType).ToList();
    }
    
    public List<FirmwareListItem> GetCachedFirmwareList(Platform platform, FirmwareDeviceType deviceType)
    {
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        if (platformMetadata == null)
        {
            return new List<FirmwareListItem>();
        }
        var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
        if (deviceTypeMetadata == null)
        {
            return new List<FirmwareListItem>();
        }
        return deviceTypeMetadata.FirmwareList;
    }
    
    public async Task<List<FirmwareChangeLogItem>?> DownloadChangelogAsync(Platform platform, FirmwareDeviceType deviceType, bool skipExisting = true)
    {
        try
        {
            // Check if already cached
            if (skipExisting)
            {
                var cached = GetCachedChangelog(platform, deviceType);
                if (cached != null && cached.Count > 0)
                {
                    Console.WriteLine($"    Changelog for {deviceType.GetDisplayName()} already cached");
                    return cached;
                }
            }
            
            Console.WriteLine($"  Downloading changelog for {deviceType.GetDisplayName()}...");
            
            // WiFi dongle uses the getAllFirmware endpoint for changelog (same as firmware list)
            List<FirmwareChangeLogItem>? changelogItems = null;
            if (deviceType == FirmwareDeviceType.DONGLE_E_WIFI_DONGLE)
            {
                var allFirmwareResponse = await _httpClient.GetAllFirmwareAsync(platform);
                
                if (allFirmwareResponse.Data == null || allFirmwareResponse.Data.Count == 0)
                {
                    Console.WriteLine($"    No changelog found for {deviceType.GetDisplayName()}");
                    return null;
                }
                
                // Convert GetAllFirmwareItem to FirmwareChangeLogItem, filtering for WiFi dongle types
                changelogItems = allFirmwareResponse.Data
                    .Where(item => !string.IsNullOrEmpty(item.SourceName))
                    .Where(item => 
                    {
                        var datalogType = item.DatalogType ?? "";
                        return datalogType == "ESP_WIFI" || datalogType == "ESP_WIFI6" || datalogType == "ESP_WIFI_E";
                    })
                    .Select(item => new FirmwareChangeLogItem
                    {
                        FwCode = item.SourceName,
                        CreateTime = item.CreateTime,
                        Description = item.Description ?? item.Version,
                        Version = item.Version
                    })
                    .ToList();
                
                Console.WriteLine($"    Found {changelogItems.Count} changelog entry(ies)");
            }
            else
            {
                var response = await _httpClient.GetFirmwareChangeLogAsync(platform, deviceType);
                
                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    Console.WriteLine($"    No changelog found for {deviceType.GetDisplayName()}");
                    return null;
                }
                
                changelogItems = response.Data;
                Console.WriteLine($"    Found {changelogItems.Count} changelog entry(ies)");
            }
            
            if (changelogItems == null || changelogItems.Count == 0)
            {
                return null;
            }
            
            // Save changelog to file in platform subdirectory
            var platformDir = Path.Combine(_firmwareDir, platform.ToString());
            if (!Directory.Exists(platformDir))
            {
                Directory.CreateDirectory(platformDir);
            }
            
            var changelogFileName = $"changelog_{deviceType}.json";
            var changelogPath = Path.Combine(platformDir, changelogFileName);
            
            var json = JsonSerializer.Serialize(changelogItems, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await File.WriteAllTextAsync(changelogPath, json);
            Console.WriteLine($"    Saved changelog to {changelogPath}");
            
            // Update metadata
            var metadata = LoadCacheMetadata();
            var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
            if (platformMetadata == null)
            {
                platformMetadata = new PlatformMetadata
                {
                    Platform = platform,
                    BaseUrl = Constants.GetBaseUrlForPlatform(platform)
                };
                metadata.Platforms.Add(platformMetadata);
            }
            
            var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
            if (deviceTypeMetadata == null)
            {
                deviceTypeMetadata = new DeviceTypeMetadata
                {
                    DeviceType = deviceType,
                    DisplayName = deviceType.GetDisplayName()
                };
                platformMetadata.DeviceTypes.Add(deviceTypeMetadata);
            }
            
            deviceTypeMetadata.Changelog = changelogItems;
            deviceTypeMetadata.ChangelogLastUpdated = DateTime.UtcNow;
            metadata.LastUpdated = DateTime.UtcNow;
            await SaveCacheMetadataAsync(metadata);
            
            return changelogItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error downloading changelog for {deviceType.GetDisplayName()}: {ex.Message}");
            return null;
        }
    }
    
    public List<FirmwareChangeLogItem>? GetCachedChangelog(Platform platform, FirmwareDeviceType deviceType)
    {
        var metadata = LoadCacheMetadata();
        var platformMetadata = metadata.Platforms.FirstOrDefault(p => p.Platform == platform);
        if (platformMetadata == null)
        {
            return null;
        }
        var deviceTypeMetadata = platformMetadata.DeviceTypes.FirstOrDefault(dt => dt.DeviceType == deviceType);
        return deviceTypeMetadata?.Changelog;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class DownloadAllResult
{
    public int PlatformsProcessed { get; set; }
    public int DeviceTypesProcessed { get; set; }
    public int DeviceTypesSkipped { get; set; }
    public int DeviceTypesFailed { get; set; }
    public int FilesDownloaded { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public int ChangelogsDownloaded { get; set; }
    public int ChangelogsSkipped { get; set; }
    public int ChangelogsFailed { get; set; }
    
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Download Summary ===");
        Console.WriteLine($"Platforms processed: {PlatformsProcessed}");
        Console.WriteLine($"Device types processed: {DeviceTypesProcessed}");
        Console.WriteLine($"Device types skipped (no firmware): {DeviceTypesSkipped}");
        Console.WriteLine($"Device types failed: {DeviceTypesFailed}");
        Console.WriteLine($"Files downloaded: {FilesDownloaded}");
        Console.WriteLine($"Files skipped (already cached): {FilesSkipped}");
        Console.WriteLine($"Files failed: {FilesFailed}");
        Console.WriteLine($"Total files: {FilesDownloaded + FilesSkipped + FilesFailed}");
        Console.WriteLine($"Changelogs downloaded: {ChangelogsDownloaded}");
        Console.WriteLine($"Changelogs skipped (already cached): {ChangelogsSkipped}");
        Console.WriteLine($"Changelogs failed: {ChangelogsFailed}");
    }
}

