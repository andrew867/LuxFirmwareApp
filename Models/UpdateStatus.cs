namespace LuxFirmwareApp.Models;

public enum UpdateStatus
{
    READY,
    WAITING,
    SUCCESS,
    FAILURE
}

public class UpdateProgressDetail
{
    public string InverterSn { get; set; } = "";
    public string DatalogSn { get; set; } = "";
    public int PackageIndex { get; set; } = 1;
    public UpdateStatus UpdateStatus { get; set; } = UpdateStatus.READY;
    public bool SendUpdateStart_0x21 { get; set; } = false;
    public bool SendUpdateReset_0x23 { get; set; } = false;
    public long LastTimeSendPackage { get; set; } = 0;
    public int ErrorCount { get; set; } = 0;
    public bool TotallyStandardUpdate { get; set; } = false;

    public int CurrentProgress => PackageIndex;
    public bool TotallyStandardUpdateValue => TotallyStandardUpdate;
}

