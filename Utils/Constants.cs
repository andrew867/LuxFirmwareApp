namespace LuxFirmwareApp.Utils;

public static class Constants
{
    // Base URLs
    public const string LUXPOWER_MAIN_URL = "https://as.luxpowertek.com/WManage/";
    public const string LUXPOWER_SECONDARY_URL = "https://us.luxpowertek.com/WManage/";
    public const string LUXPOWER_NA_URL = "https://na.luxpowertek.com/WManage/";
    public const string EG4_MAIN_URL = "https://as.luxpowertek.com/WManage/";
    public const string REMOTE_FIRMWARE_URL = "http://47.254.33.206:8083/resource/firmware/";
    public const string RESOURCE_URL = "https://res.solarcloudsystem.com:8443/resource/findAllTypeInfo";
    public const string FIRMWARE_RECORD_CHANGELOG_URL = "http://47.254.33.206:8083/firmwareRecord/findAllTypeInfo";
    public const string OS_SOLARCLOUD_URL = "http://os.solarcloudsystem.com/";
    public const string FIRMWARE_INFO_URL = "http://47.254.33.206:8083/firmwareInformation/getInformation";
    public const string GET_ALL_FIRMWARE_URL = "https://res.solarcloudsystem.com:8443/resource/getAllFirmware";
    
    // API Endpoints
    public const string ENDPOINT_LIST_FIRMWARE = "web/maintain/appLocalUpdate/listForAppByType";
    public const string ENDPOINT_GET_FIRMWARE_DATA = "web/maintain/appLocalUpdate/getUploadFileAnalyzeInfo";
    public const string ENDPOINT_FIRMWARE_CHANGELOG = "resource/findAllTypeInfo";
    
    // Beta endpoint suffix
    public const string BETA_SUFFIX = "/beta";
    
    // Default values
    public const string DEFAULT_DATALOG_SN = "FFFFFFFFFFFFFFFFFFFF";
    public const string DEFAULT_TCP_IP = "10.10.10.1";
    public const int DEFAULT_TCP_PORT = 8899;
    
    // Update protocol constants
    public const byte UPDATE_PREPARE = 0x21;
    public const byte UPDATE_SEND_DATA = 0x22;
    public const byte UPDATE_RESET = 0x23;
    
    // Wait times (milliseconds)
    public const int MAX_WAIT_MILLISECONDS = 600000; // 10 minutes
    public const int MIN_WAIT_MILLISECONDS = 360000; // 6 minutes
    public const int WAIT_12K_NO_POWER_OFF_MILLISECONDS = 30000; // 30 seconds
    
    // File storage
    public const string DEFAULT_FIRMWARE_DIR = "firmware";
    
    // Get base URL for a platform
    public static string GetBaseUrlForPlatform(Models.Platform platform)
    {
        return platform switch
        {
            Models.Platform.EG4 => EG4_MAIN_URL,
            Models.Platform.LUX_POWER => LUXPOWER_NA_URL, // Default to NA for LuxPower
            _ => LUXPOWER_NA_URL // Default to NA
        };
    }
    
    // Get major URL (for firmware downloads)
    public static string GetMajorUrlForPlatform(Models.Platform platform)
    {
        return platform switch
        {
            Models.Platform.EG4 => LUXPOWER_SECONDARY_URL,
            Models.Platform.LUX_POWER => LUXPOWER_NA_URL, // Default to NA for LuxPower
            _ => LUXPOWER_NA_URL // Default to NA
        };
    }
}

