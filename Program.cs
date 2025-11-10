using System.CommandLine;
using LuxFirmwareApp.Models;
using LuxFirmwareApp.Services;
using LuxFirmwareApp.Utils;

var rootCommand = new RootCommand("LuxFirmwareApp - Firmware downloader and updater for Lux/EG4 inverters and battery packs");

// Global options
var brandOption = new Option<Platform>(
    aliases: new[] { "--brand", "-b" },
    description: "Brand/platform (LUX_POWER, EG4, GSL, MID, etc.)",
    getDefaultValue: () => Platform.LUX_POWER // Defaults to LUX_POWER which now uses NA server
);

var sessionIdOption = new Option<string?>(
    aliases: new[] { "--session-id", "--cookie" },
    description: "JSESSIONID cookie value (optional, will login if not provided)"
);

var usernameOption = new Option<string?>(
    aliases: new[] { "--username", "-u" },
    description: "Username for login (optional)"
);

var passwordOption = new Option<string?>(
    aliases: new[] { "--password", "-p" },
    description: "Password for login (optional)"
);

var outputDirOption = new Option<string>(
    aliases: new[] { "--output-dir", "-o" },
    description: "Output directory for firmware files",
    getDefaultValue: () => Constants.DEFAULT_FIRMWARE_DIR
);

var betaOption = new Option<bool>(
    aliases: new[] { "--beta" },
    description: "Use beta firmware endpoint"
);

var baseUrlOption = new Option<string?>(
    aliases: new[] { "--base-url" },
    description: "Override base URL (optional, auto-selected by brand)"
);

// Download command
var downloadCommand = new Command("download", "Download firmware for specified device type");
var deviceTypeOption = new Option<string>(
    aliases: new[] { "--device-type", "-t" },
    description: "Firmware device type (e.g., LXP_LB_8_12K, BATT_hi_5_v1)"
) { IsRequired = true };

var recordIdOption = new Option<string?>(
    aliases: new[] { "--record-id", "-r" },
    description: "Specific firmware record ID to download"
);

downloadCommand.AddOption(brandOption);
downloadCommand.AddOption(deviceTypeOption);
downloadCommand.AddOption(recordIdOption);
downloadCommand.AddOption(outputDirOption);
downloadCommand.AddOption(betaOption);
downloadCommand.AddOption(baseUrlOption);
downloadCommand.AddOption(sessionIdOption);
downloadCommand.AddOption(usernameOption);
downloadCommand.AddOption(passwordOption);

downloadCommand.SetHandler(async (brand, deviceTypeStr, recordId, outputDir, useBeta, sessionId, username, password) =>
{
    if (!Enum.TryParse<FirmwareDeviceType>(deviceTypeStr, true, out var deviceType))
    {
        Console.WriteLine($"Invalid device type: {deviceTypeStr}");
        Console.WriteLine($"Valid types: {string.Join(", ", Enum.GetNames<FirmwareDeviceType>())}");
        return;
    }

    if (!deviceType.IsSupportedForPlatform(brand))
    {
        Console.WriteLine($"Device type {deviceType} is not supported for platform {brand}");
        return;
    }

    var downloader = new FirmwareDownloader(outputDir);
    
    // Handle authentication
    if (!string.IsNullOrEmpty(sessionId))
    {
        downloader.SetSessionId(sessionId);
    }
    else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
    {
        Console.WriteLine("Logging in...");
        var loginSuccess = await downloader.LoginAsync(brand, username, password);
        if (!loginSuccess)
        {
            Console.WriteLine("Login failed. Please check your credentials or provide a session ID.");
            return;
        }
    }
    else
    {
        Console.WriteLine("Warning: No authentication provided. Some requests may fail.");
        Console.WriteLine("Use --session-id or --username/--password to authenticate.");
    }

    try
    {
        if (!string.IsNullOrEmpty(recordId))
        {
            // Download specific record
            var firmware = await downloader.DownloadFirmwareAsync(brand, recordId, deviceType, useBeta);
            if (firmware != null)
            {
                Console.WriteLine($"Successfully downloaded: {firmware.FileName}");
            }
            else
            {
                Console.WriteLine("Failed to download firmware");
            }
        }
        else
        {
            // List and download all available firmware
            var firmwareList = await downloader.ListFirmwareAsync(brand, deviceType, useBeta);
            
            if (firmwareList.Count == 0)
            {
                Console.WriteLine("No firmware files found");
                return;
            }

            Console.WriteLine($"Found {firmwareList.Count} firmware file(s):");
            foreach (var item in firmwareList)
            {
                Console.WriteLine($"  - {item.FileName} (Record ID: {item.RecordId}, Standard: {item.Standard}, V1: {item.V1}, V2: {item.V2}, V3: {item.V3})");
            }

            // Download all
            foreach (var item in firmwareList)
            {
                var firmware = await downloader.DownloadFirmwareAsync(brand, item.RecordId, deviceType, useBeta);
                if (firmware != null)
                {
                    Console.WriteLine($"Successfully downloaded: {firmware.FileName}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    finally
    {
        downloader.Dispose();
    }
}, brandOption, deviceTypeOption, recordIdOption, outputDirOption, betaOption, sessionIdOption, usernameOption, passwordOption);

// List command
var listCommand = new Command("list", "List available firmware");
var listDeviceTypeOption = new Option<string>(
    aliases: new[] { "--device-type", "-t" },
    description: "Firmware device type (e.g., LXP_LB_8_12K, BATT_hi_5_v1)"
) { IsRequired = true };

listCommand.AddOption(brandOption);
listCommand.AddOption(listDeviceTypeOption);
listCommand.AddOption(betaOption);
listCommand.AddOption(sessionIdOption);
listCommand.AddOption(usernameOption);
listCommand.AddOption(passwordOption);

listCommand.SetHandler(async (brand, deviceTypeStr, useBeta, sessionId, username, password) =>
{
    if (!Enum.TryParse<FirmwareDeviceType>(deviceTypeStr, true, out var deviceType))
    {
        Console.WriteLine($"Invalid device type: {deviceTypeStr}");
        return;
    }

    if (!deviceType.IsSupportedForPlatform(brand))
    {
        Console.WriteLine($"Device type {deviceType} is not supported for platform {brand}");
        return;
    }

    var downloader = new FirmwareDownloader();
    
    // Handle authentication
    if (!string.IsNullOrEmpty(sessionId))
    {
        downloader.SetSessionId(sessionId);
    }
    else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
    {
        Console.WriteLine("Logging in...");
        var loginSuccess = await downloader.LoginAsync(brand, username, password);
        if (!loginSuccess)
        {
            Console.WriteLine("Login failed. Please check your credentials or provide a session ID.");
            return;
        }
    }
    else
    {
        Console.WriteLine("Warning: No authentication provided. Some requests may fail.");
        Console.WriteLine("Use --session-id or --username/--password to authenticate.");
    }

    try
    {
        var firmwareList = await downloader.ListFirmwareAsync(brand, deviceType, useBeta);
        
        if (firmwareList.Count == 0)
        {
            Console.WriteLine("No firmware files found");
            return;
        }

        Console.WriteLine($"Found {firmwareList.Count} firmware file(s) for {deviceType.GetDisplayName()}:");
        Console.WriteLine();
        
        foreach (var item in firmwareList)
        {
            Console.WriteLine($"File Name: {item.FileName}");
            Console.WriteLine($"  Record ID: {item.RecordId}");
            Console.WriteLine($"  Standard: {item.Standard}");
            
            // Format version numbers: -1 means "not applicable"
            var v1Str = item.V1 == -1 ? "N/A" : item.V1.ToString();
            var v2Str = item.V2 == -1 ? "N/A" : item.V2.ToString();
            var v3Str = item.V3 == -1 ? "N/A" : item.V3.ToString();
            Console.WriteLine($"  Version: V1={v1Str}, V2={v2Str}, V3={v3Str}");
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    finally
    {
        downloader.Dispose();
    }
}, brandOption, listDeviceTypeOption, betaOption, sessionIdOption, usernameOption, passwordOption);

// Download-all command
var downloadAllCommand = new Command("download-all", "Download and cache all firmware for all brands and device types");
downloadAllCommand.AddOption(betaOption);
downloadAllCommand.AddOption(sessionIdOption);
downloadAllCommand.AddOption(usernameOption);
downloadAllCommand.AddOption(passwordOption);
downloadAllCommand.AddOption(outputDirOption);

var skipExistingOption = new Option<bool>(
    aliases: new[] { "--skip-existing", "-s" },
    description: "Skip firmware files that are already cached",
    getDefaultValue: () => true
);
downloadAllCommand.AddOption(skipExistingOption);

var downloadAllBrandOption = new Option<Platform?>(
    aliases: new[] { "--brand", "-b" },
    description: "Filter to specific brand/platform (optional, for faster testing)",
    getDefaultValue: () => null
);
downloadAllCommand.AddOption(downloadAllBrandOption);

downloadAllCommand.SetHandler(async (useBeta, sessionId, username, password, outputDir, skipExisting, filterBrand) =>
{
    var downloader = new FirmwareDownloader(outputDir);
    
    // Handle authentication
    if (!string.IsNullOrEmpty(sessionId))
    {
        downloader.SetSessionId(sessionId);
        Console.WriteLine("Using provided session ID for authentication.");
    }
    else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
    {
        Console.WriteLine("Logging in...");
        // Try login with LUX_POWER first (default platform)
        var loginSuccess = await downloader.LoginAsync(Platform.LUX_POWER, username, password);
        if (loginSuccess)
        {
            Console.WriteLine("Login successful.");
        }
        else
        {
            Console.WriteLine("Login failed. Please check your credentials or provide a session ID.");
            return;
        }
    }
    else
    {
        Console.WriteLine("Warning: No authentication provided. Some requests may fail.");
        Console.WriteLine("Use --session-id or --username/--password to authenticate.");
        Console.WriteLine("Press Enter to continue anyway, or Ctrl+C to cancel...");
        Console.ReadLine();
    }
    
    try
    {
        var result = await downloader.DownloadAllFirmwareAsync(useBeta, skipExisting, filterBrand);
        result.PrintSummary();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
    }
    finally
    {
        downloader.Dispose();
    }
}, betaOption, sessionIdOption, usernameOption, passwordOption, outputDirOption, skipExistingOption, downloadAllBrandOption);

// Restore command
var restoreCommand = new Command("restore", "Restore firmware from local cache");
restoreCommand.AddOption(outputDirOption);

restoreCommand.SetHandler((outputDir) =>
{
    var downloader = new FirmwareDownloader(outputDir);
    
    try
    {
        var caches = downloader.RestoreFirmwareFromDisk();
        
        if (caches.Count == 0)
        {
            Console.WriteLine("No firmware files found in cache");
            return;
        }

        Console.WriteLine($"Found {caches.Count} firmware file(s) in cache:");
        Console.WriteLine();
        
        foreach (var cache in caches)
        {
            Console.WriteLine($"File Name: {cache.FileName}");
            Console.WriteLine($"  Record ID: {cache.RecordId}");
            Console.WriteLine($"  Device Type: {cache.FirmwareDeviceType?.GetDisplayName() ?? "Unknown"}");
            Console.WriteLine($"  Standard: {cache.Standard}");
            Console.WriteLine($"  Version: V1={cache.V1}, V2={cache.V2}, V3={cache.V3}");
            Console.WriteLine($"  File Size: {cache.FileSize} bytes");
            Console.WriteLine($"  Packages: {cache.Firmware.Count}");
            Console.WriteLine($"  Downloaded: {cache.DoneDownload}");
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    finally
    {
        downloader.Dispose();
    }
}, outputDirOption);

// Update command
var updateCommand = new Command("update", "Update inverter/battery with downloaded firmware");
var ipAddressOption = new Option<string>(
    aliases: new[] { "--ip-address", "-i" },
    description: "Device IP address",
    getDefaultValue: () => Constants.DEFAULT_TCP_IP
);

var serialNumberOption = new Option<string>(
    aliases: new[] { "--serial-number", "-s" },
    description: "Device serial number",
    getDefaultValue: () => Constants.DEFAULT_DATALOG_SN
);

var updateRecordIdOption = new Option<string>(
    aliases: new[] { "--record-id", "-r" },
    description: "Firmware record ID to update"
) { IsRequired = true };

updateCommand.AddOption(updateRecordIdOption);
updateCommand.AddOption(ipAddressOption);
updateCommand.AddOption(serialNumberOption);
updateCommand.AddOption(outputDirOption);

updateCommand.SetHandler(async (recordId, ipAddress, serialNumber, outputDir) =>
{
    var downloader = new FirmwareDownloader(outputDir);
    
    try
    {
        var firmware = downloader.GetFirmwareByRecordId(recordId);
        
        if (firmware == null)
        {
            Console.WriteLine($"Firmware with record ID {recordId} not found in cache");
            Console.WriteLine("Please download the firmware first using the 'download' command");
            return;
        }

        if (!firmware.DoneDownload)
        {
            Console.WriteLine($"Firmware {recordId} is not fully downloaded");
            return;
        }

        Console.WriteLine($"Updating device with firmware: {firmware.FileName}");
        Console.WriteLine($"Device IP: {ipAddress}");
        Console.WriteLine($"Serial Number: {serialNumber}");
        Console.WriteLine();

        var updater = new FirmwareUpdater(serialNumber, serialNumber, ipAddress);
        
        try
        {
            var success = await updater.UpdateFirmwareAsync(firmware);
            
            if (success)
            {
                Console.WriteLine();
                Console.WriteLine("Firmware update completed successfully!");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Firmware update failed");
            }
        }
        finally
        {
            updater.Dispose();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
    finally
    {
        downloader.Dispose();
    }
}, updateRecordIdOption, ipAddressOption, serialNumberOption, outputDirOption);

// Login command
var loginCommand = new Command("login", "Login to Lux Cloud server and get session ID");
var loginUsernameOption = new Option<string>(
    aliases: new[] { "--username", "-u" },
    description: "Username/account"
) { IsRequired = true };

var loginPasswordOption = new Option<string>(
    aliases: new[] { "--password", "-p" },
    description: "Password"
) { IsRequired = true };

var loginBrandOption = new Option<Platform>(
    aliases: new[] { "--brand", "-b" },
    description: "Brand/platform",
    getDefaultValue: () => Platform.LUX_POWER
);

loginCommand.AddOption(loginBrandOption);
loginCommand.AddOption(loginUsernameOption);
loginCommand.AddOption(loginPasswordOption);

loginCommand.SetHandler(async (brand, username, password) =>
{
    var httpClient = new HttpClientService();
    
    try
    {
        Console.WriteLine($"Logging in to {brand}...");
        var response = await httpClient.LoginAsync(brand, username, password);
        
        if (response.Success)
        {
            var sessionId = httpClient.SessionId;
            if (!string.IsNullOrEmpty(sessionId))
            {
                Console.WriteLine();
                Console.WriteLine("Login successful!");
                Console.WriteLine($"Session ID: {sessionId}");
                Console.WriteLine();
                Console.WriteLine("You can use this session ID with --session-id option:");
                Console.WriteLine($"  --session-id {sessionId}");
            }
            else
            {
                Console.WriteLine("Login successful but no session ID received.");
            }
        }
        else
        {
            Console.WriteLine($"Login failed: {response.Message ?? "Unknown error"}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Login error: {ex.Message}");
    }
    finally
    {
        httpClient.Dispose();
    }
}, loginBrandOption, loginUsernameOption, loginPasswordOption);

// List devices command
var listDevicesCommand = new Command("list-devices", "List all available device models");
var filterBrandOption = new Option<Platform?>(
    aliases: new[] { "--brand", "-b" },
    description: "Filter by brand/platform (optional)"
);

listDevicesCommand.AddOption(filterBrandOption);

listDevicesCommand.SetHandler((brand) =>
{
    var deviceTypes = Enum.GetValues<FirmwareDeviceType>();
    
    if (brand.HasValue)
    {
        // Filter by brand
        var supportedTypes = deviceTypes.Where(dt => dt.IsSupportedForPlatform(brand.Value)).ToList();
        
        Console.WriteLine($"Device models for brand: {brand.Value}");
        Console.WriteLine($"Total: {supportedTypes.Count} device type(s)");
        Console.WriteLine();
        
        foreach (var deviceType in supportedTypes)
        {
            Console.WriteLine($"  {deviceType} - {deviceType.GetDisplayName()}");
        }
    }
    else
    {
        // Show all device types grouped by brand support
        Console.WriteLine("All available device models:");
        Console.WriteLine();
        
        var allBrands = Enum.GetValues<Platform>();
        
        foreach (var deviceType in deviceTypes)
        {
            Console.WriteLine($"{deviceType} - {deviceType.GetDisplayName()}");
            
            var supportedBrands = allBrands.Where(b => deviceType.IsSupportedForPlatform(b)).ToList();
            if (supportedBrands.Count > 0)
            {
                Console.WriteLine($"  Supported brands: {string.Join(", ", supportedBrands)}");
            }
            else
            {
                Console.WriteLine($"  Supported brands: None");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine($"Total: {deviceTypes.Length} device type(s)");
    }
}, filterBrandOption);

// Add commands to root
rootCommand.AddCommand(loginCommand);
rootCommand.AddCommand(downloadCommand);
rootCommand.AddCommand(listCommand);
rootCommand.AddCommand(restoreCommand);
rootCommand.AddCommand(updateCommand);
rootCommand.AddCommand(listDevicesCommand);
rootCommand.AddCommand(downloadAllCommand);

// Run
await rootCommand.InvokeAsync(args);

