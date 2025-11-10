using LuxFirmwareApp.Models;
using LuxFirmwareApp.Utils;

namespace LuxFirmwareApp.Services;

public class FirmwareUpdater
{
    private readonly TcpClient _tcpClient;
    private readonly string _datalogSn;
    private readonly string _inverterSn;

    public FirmwareUpdater(string datalogSn, string inverterSn, string? ipAddress = null, int port = Constants.DEFAULT_TCP_PORT)
    {
        _datalogSn = datalogSn;
        _inverterSn = inverterSn;
        _tcpClient = new TcpClient(ipAddress ?? Constants.DEFAULT_TCP_IP, port);
    }

    public async Task<bool> InitializeAsync()
    {
        return await _tcpClient.ConnectAsync();
    }

    public async Task<bool> UpdateFirmwareAsync(UpdateFileCache firmwareCache)
    {
        if (!_tcpClient.IsConnected)
        {
            if (!await InitializeAsync())
            {
                Console.WriteLine("Failed to connect to device");
                return false;
            }
        }

        var progress = new UpdateProgressDetail
        {
            InverterSn = _inverterSn,
            DatalogSn = _datalogSn,
            PackageIndex = 1,
            UpdateStatus = UpdateStatus.READY
        };

        var firmware = firmwareCache.Firmware;
        var size = firmware.Count;

        Console.WriteLine($"Starting firmware update: {firmwareCache.FileName}");
        Console.WriteLine($"Total packages: {size}");

        try
        {
            // Step 1: Send prepare command (0x21)
            if (!progress.SendUpdateStart_0x21)
            {
                byte[] prepareFrame;
                if (firmwareCache.IsLuxVersion)
                {
                    prepareFrame = DataFrameFactory.CreateLuxUpdatePrepareDataFrame(
                        _datalogSn, _inverterSn, firmwareCache.TailEncoded ?? "", size, firmwareCache.Crc32);
                }
                else
                {
                    prepareFrame = DataFrameFactory.CreateUpdatePrepareDataFrame(
                        _datalogSn, _inverterSn, firmwareCache.TailEncoded ?? "", size, firmwareCache.Crc32);
                }

                var prepareResponse = await _tcpClient.SendCommandAsync("tcpUpdate_Prepare", prepareFrame);
                
                if (string.IsNullOrEmpty(prepareResponse) || prepareResponse.Length < 35)
                {
                    Console.WriteLine("Failed to receive prepare response");
                    return false;
                }

                // Parse response to get package index (from positions 33-34, converted from hex)
                var packageIndexStr = prepareResponse.Substring(32, 4); // Positions 33-34 in hex string = 2 bytes
                int packageIndex = 1;
                if (packageIndexStr.Length >= 4)
                {
                    try
                    {
                        // Convert from hex string (little endian)
                        var byte1 = Convert.ToByte(packageIndexStr.Substring(2, 2), 16);
                        var byte2 = Convert.ToByte(packageIndexStr.Substring(0, 2), 16);
                        packageIndex = (byte2 << 8) | byte1;
                    }
                    catch
                    {
                        packageIndex = 1;
                    }
                }

                if (packageIndex <= 0 || packageIndex >= size)
                {
                    packageIndex = 1;
                }

                progress.PackageIndex = packageIndex;
                progress.SendUpdateStart_0x21 = true;
                progress.UpdateStatus = UpdateStatus.WAITING;

                // Check if totally standard update (character at position 32 = 'A')
                if (prepareResponse.Length > 32)
                {
                    var statusChar = (char)Convert.ToByte(prepareResponse.Substring(32, 2), 16);
                    if (statusChar == 'A')
                    {
                        progress.TotallyStandardUpdate = true;
                    }
                }

                Console.WriteLine($"Prepare successful, starting from package {packageIndex}");
            }

            // Step 2: Send firmware data packages (0x22)
            while (progress.PackageIndex <= size && progress.UpdateStatus != UpdateStatus.FAILURE)
            {
                var packageIndex = progress.PackageIndex;
                
                // Determine file size/address
                long fileSize = 0;
                if (firmwareCache.FileType == 2 && firmwareCache.PhysicalAddr.ContainsKey(packageIndex))
                {
                    fileSize = firmwareCache.PhysicalAddr[packageIndex];
                }
                else if (firmwareCache.FileType == 1 || firmwareCache.FileType == 3)
                {
                    fileSize = firmwareCache.FileSize;
                }

                // Get firmware data
                if (!firmware.ContainsKey(packageIndex))
                {
                    Console.WriteLine($"Missing firmware data for package {packageIndex}");
                    progress.UpdateStatus = UpdateStatus.FAILURE;
                    break;
                }

                var firmwareData = firmware[packageIndex];

                byte[] sendDataFrame;
                if (firmwareCache.IsLuxVersion)
                {
                    sendDataFrame = DataFrameFactory.CreateLuxUpdateSendDataDataFrame(
                        _datalogSn, _inverterSn, packageIndex, firmwareCache.FileType,
                        firmwareCache.FirmwareLengthArrayEncoded ?? "", firmwareData);
                }
                else
                {
                    sendDataFrame = DataFrameFactory.CreateUpdateSendDataDataFrame(
                        _datalogSn, _inverterSn, packageIndex, firmwareCache.FileType, fileSize, firmwareData);
                }

                var sendResponse = await _tcpClient.SendCommandAsync($"tcpUpdate_Send_{packageIndex}", sendDataFrame);
                
                if (string.IsNullOrEmpty(sendResponse))
                {
                    progress.ErrorCount++;
                    if (progress.ErrorCount >= 10)
                    {
                        Console.WriteLine("Too many errors, aborting update");
                        progress.UpdateStatus = UpdateStatus.FAILURE;
                        break;
                    }
                    await Task.Delay(500);
                    continue;
                }

                progress.UpdateStatus = UpdateStatus.WAITING;
                progress.ErrorCount = 0;
                progress.PackageIndex++;
                progress.LastTimeSendPackage = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (packageIndex % 10 == 0 || packageIndex == size)
                {
                    Console.WriteLine($"Progress: {packageIndex}/{size} packages sent ({(packageIndex * 100 / size)}%)");
                }

                await Task.Delay(100); // Small delay between packages
            }

            // Step 3: Send reset command (0x23)
            if (progress.PackageIndex > size && !progress.SendUpdateReset_0x23 && progress.UpdateStatus != UpdateStatus.FAILURE)
            {
                byte[] resetFrame;
                if (firmwareCache.IsLuxVersion && firmwareCache.FileHandleType.HasValue)
                {
                    var bmsHeaderId = firmwareCache.BmsHeaderId ?? size;
                    resetFrame = DataFrameFactory.CreateLuxUpdateResetDataFrame(
                        _datalogSn, _inverterSn, firmwareCache.FileType,
                        firmwareCache.FileHandleType.Value, bmsHeaderId, firmwareCache.Crc32);
                }
                else
                {
                    var dataCount = firmwareCache.BmsHeaderId ?? size;
                    resetFrame = DataFrameFactory.CreateUpdateResetDataFrame(
                        _datalogSn, _inverterSn, firmwareCache.FileType, dataCount, firmwareCache.Crc32);
                }

                var resetResponse = await _tcpClient.SendCommandAsync("tcpUpdate_Reset", resetFrame);
                
                if (!string.IsNullOrEmpty(resetResponse) && resetResponse.Length > 32)
                {
                    var statusByte = Convert.ToByte(resetResponse.Substring(32, 2), 16);
                    if (statusByte == 1)
                    {
                        progress.SendUpdateReset_0x23 = true;
                        progress.UpdateStatus = UpdateStatus.SUCCESS;
                        Console.WriteLine("Firmware update completed successfully!");
                        return true;
                    }
                }

                progress.SendUpdateReset_0x23 = true;
                progress.UpdateStatus = UpdateStatus.WAITING;
            }

            return progress.UpdateStatus == UpdateStatus.SUCCESS;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during firmware update: {ex.Message}");
            progress.UpdateStatus = UpdateStatus.FAILURE;
            return false;
        }
        finally
        {
            _tcpClient.Close();
        }
    }

    public void Dispose()
    {
        _tcpClient?.Dispose();
    }
}

