namespace LuxFirmwareApp.Models;

public enum FirmwareDeviceType
{
    // SNA Series
    SNA_3000_6000,
    SNA_US_6000,
    SNA_12K,
    SNA_US_12K,
    
    // LXP Series
    LXP_3_6K_HYBRID_STANDARD,
    LXP_3_6K_HYBRID_PARALLEL,
    LXP_3600_ACS_STANDARD,
    LXP_3600_ACS_PARALLEL,
    LXP_LB_8_12K,
    
    // Other Inverters
    LSP_100K,
    LXP_HV_6K_HYBRID,
    Lite_Stor,
    TRIP_HB_EU_6_20K,
    TRIP_LV_5_20K,
    GEN_LB_EU_3_6K,
    GEN_LB_EU_7_10K_GST,
    POWER_HUB,
    
    // Battery Packs
    BATT_hi_5_v1,
    BATT_hi_5_v2,
    BATT_power_gem,
    BATT_power_gem_plus,
    BATT_j_of_10kWh,
    BATT_eco_beast,
    BATT_p_shield,
    BATT_p_shield_max,
    BATT_power_stack,
    BATT_c14,
    BATT_power_gem_max,
    BATT_e0b_Hi_Li,
    
    // Dongles
    DONGLE_E_WIFI_DONGLE
}

public static class FirmwareDeviceTypeExtensions
{
    public static string GetDisplayName(this FirmwareDeviceType deviceType)
    {
        return deviceType switch
        {
            FirmwareDeviceType.SNA_3000_6000 => "SNA 3000-6000",
            FirmwareDeviceType.SNA_US_6000 => "SNA-US 6000",
            FirmwareDeviceType.SNA_12K => "SNA 12K",
            FirmwareDeviceType.SNA_US_12K => "SNA-US 12K",
            FirmwareDeviceType.LXP_3_6K_HYBRID_STANDARD => "LXP-3-6K Hybrid (Standard)",
            FirmwareDeviceType.LXP_3_6K_HYBRID_PARALLEL => "LXP-3-6K Hybrid (Parallel)",
            FirmwareDeviceType.LXP_3600_ACS_STANDARD => "LXP_3600 ACS (Standard)",
            FirmwareDeviceType.LXP_3600_ACS_PARALLEL => "LXP_3600 ACS (Parallel)",
            FirmwareDeviceType.LXP_LB_8_12K => "LXP-LB-8-12K",
            FirmwareDeviceType.LSP_100K => "LSP-100K",
            FirmwareDeviceType.LXP_HV_6K_HYBRID => "LXP-HV-6K Hybrid",
            FirmwareDeviceType.Lite_Stor => "LiteStor",
            FirmwareDeviceType.TRIP_HB_EU_6_20K => "TRIP 6-30K",
            FirmwareDeviceType.TRIP_LV_5_20K => "TRIP-LV 5-20K",
            FirmwareDeviceType.GEN_LB_EU_3_6K => "GEN-LB-EU 3-6K",
            FirmwareDeviceType.GEN_LB_EU_7_10K_GST => "GEN-LB-EU 7-10K",
            FirmwareDeviceType.POWER_HUB => "PowerHub",
            FirmwareDeviceType.BATT_hi_5_v1 => "Batt - Hi-5 GEN 1",
            FirmwareDeviceType.BATT_hi_5_v2 => "Batt - Hi-5 GEN 2 / Li 5",
            FirmwareDeviceType.BATT_power_gem => "Batt - Power GEM / PGEM",
            FirmwareDeviceType.BATT_power_gem_plus => "Batt - Power GEM Plus / PGEM PRO",
            FirmwareDeviceType.BATT_j_of_10kWh => "Batt - J-OF 10kWh / LiteStor",
            FirmwareDeviceType.BATT_eco_beast => "Batt - Eco Beast",
            FirmwareDeviceType.BATT_p_shield => "Batt - P SHIELD",
            FirmwareDeviceType.BATT_p_shield_max => "Batt - PowerShield Max / PSHIELD MAX",
            FirmwareDeviceType.BATT_power_stack => "Batt - Power Stack / PSTACK",
            FirmwareDeviceType.BATT_c14 => "Batt - C14",
            FirmwareDeviceType.BATT_power_gem_max => "Batt - Powergem Max / PGEMMAX",
            FirmwareDeviceType.BATT_e0b_Hi_Li => "Batt - E0B-100 / Hi-11.8(GEN3) / Li-11.8",
            FirmwareDeviceType.DONGLE_E_WIFI_DONGLE => "E-WiFi Dongle",
            _ => deviceType.ToString()
        };
    }
    
    public static FirmwareDeviceType? GetEnumByName(string name)
    {
        if (Enum.TryParse<FirmwareDeviceType>(name, true, out var result))
        {
            return result;
        }
        return null;
    }
    
    public static bool IsSupportedForPlatform(this FirmwareDeviceType deviceType, Platform platform)
    {
        // EG4 only supports a subset
        if (platform == Platform.EG4)
        {
            return deviceType switch
            {
                FirmwareDeviceType.LXP_LB_8_12K => true,
                FirmwareDeviceType.SNA_US_6000 => true,
                FirmwareDeviceType.SNA_US_12K => true,
                FirmwareDeviceType.POWER_HUB => true,
                FirmwareDeviceType.DONGLE_E_WIFI_DONGLE => true,
                _ => false
            };
        }
        
        // All other platforms support all device types
        return true;
    }
}

