using System.Text;
using LuxFirmwareApp.Utils;

namespace LuxFirmwareApp.Utils;

public static class DataFrameFactory
{
    // Device function codes
    private const byte UPDATE_PREPARE_CODE = 0x21;
    private const byte UPDATE_SEND_DATA_CODE = 0x22;
    private const byte UPDATE_RESET_CODE = 0x23;
    private const byte LUX_UPDATE_PREPARE_CODE = 0x21;
    private const byte LUX_UPDATE_SEND_DATA_CODE = 0x22;
    private const byte LUX_UPDATE_RESET_CODE = 0x23;

    // Serial number length
    private const int SERIAL_NUM_LENGTH = 10;

    public static byte[] CreateUpdatePrepareDataFrame(string datalogSn, string inverterSn, string tailEncoded, int dataCount, long crc32)
    {
        var tail = Convert.FromBase64String(tailEncoded);
        var frame = new byte[24];
        
        frame[0] = 0;
        frame[1] = UPDATE_PREPARE_CODE;
        
        // Serial number (10 bytes, positions 2-11)
        var serialBytes = Encoding.ASCII.GetBytes(inverterSn ?? datalogSn);
        Array.Copy(serialBytes, 0, frame, 2, Math.Min(serialBytes.Length, SERIAL_NUM_LENGTH));
        
        // Tail (4 bytes, positions 12-15)
        Array.Copy(tail, 0, frame, 12, Math.Min(tail.Length, 4));
        
        // Data count (2 bytes, positions 16-17, little endian)
        ConvertLongToByte2(frame, 16, dataCount, 0, true);
        
        // CRC32 (4 bytes, positions 18-21, little endian)
        ConvertLongToByte4(frame, 18, crc32, 0, true);
        
        // CRC16 (2 bytes, positions 22-23, little endian)
        var crc16 = Crc16.CalculateModbusCrc16(frame, 0, 22);
        ConvertLongToByte2(frame, 22, crc16, 0, true);
        
        return frame;
    }

    public static byte[] CreateLuxUpdatePrepareDataFrame(string datalogSn, string inverterSn, string tailEncoded, int dataCount, long crc32)
    {
        // Same structure as standard prepare
        return CreateUpdatePrepareDataFrame(datalogSn, inverterSn, tailEncoded, dataCount, crc32);
    }

    public static byte[] CreateUpdateSendDataDataFrame(string datalogSn, string inverterSn, int dataIndex, int fileType, long physicalAddr, string firmwareDataBase64)
    {
        var dataList = Convert.FromBase64String(firmwareDataBase64);
        var length = dataList.Length + 19 + 4;
        var frame = new byte[length];
        
        frame[0] = 0;
        frame[1] = UPDATE_SEND_DATA_CODE;
        
        // Serial number (10 bytes, positions 2-11)
        var serialBytes = Encoding.ASCII.GetBytes(inverterSn ?? datalogSn);
        Array.Copy(serialBytes, 0, frame, 2, Math.Min(serialBytes.Length, SERIAL_NUM_LENGTH));
        
        // Data index (2 bytes, positions 12-13, little endian)
        ConvertLongToByte2(frame, 12, dataIndex, 0, true);
        
        // File type (1 byte, position 14)
        frame[14] = (byte)fileType;
        
        // Data length + 4 (2 bytes, positions 15-16, little endian)
        ConvertLongToByte2(frame, 15, dataList.Length + 4, 0, true);
        
        // Physical address (4 bytes, positions 17-20, little endian)
        ConvertLongToByte4(frame, 17, physicalAddr, 0, true);
        
        // Firmware data (variable length, starting at position 21)
        Array.Copy(dataList, 0, frame, 21, dataList.Length);
        
        // CRC16 (2 bytes, last 2 bytes, little endian)
        var crc16 = Crc16.CalculateModbusCrc16(frame, 0, length - 2);
        ConvertLongToByte2(frame, length - 2, crc16, 0, true);
        
        return frame;
    }

    public static byte[] CreateLuxUpdateSendDataDataFrame(string datalogSn, string inverterSn, int dataIndex, int fileType, string firmwareLengthArrayEncoded, string firmwareDataBase64)
    {
        var dataList = Convert.FromBase64String(firmwareDataBase64);
        var firmwareLengthArray = Convert.FromBase64String(firmwareLengthArrayEncoded);
        var length = dataList.Length + 19 + 4;
        var frame = new byte[length];
        
        frame[0] = 0;
        frame[1] = LUX_UPDATE_SEND_DATA_CODE;
        
        // Serial number (10 bytes, positions 2-11)
        var serialBytes = Encoding.ASCII.GetBytes(inverterSn ?? datalogSn);
        Array.Copy(serialBytes, 0, frame, 2, Math.Min(serialBytes.Length, SERIAL_NUM_LENGTH));
        
        // Data index (2 bytes, positions 12-13, little endian)
        ConvertLongToByte2(frame, 12, dataIndex, 0, true);
        
        // File type (1 byte, position 14)
        frame[14] = (byte)fileType;
        
        // Data length + 4 (2 bytes, positions 15-16, little endian)
        ConvertLongToByte2(frame, 15, dataList.Length + 4, 0, true);
        
        // Firmware length array (4 bytes, positions 17-20)
        Array.Copy(firmwareLengthArray, 0, frame, 17, Math.Min(firmwareLengthArray.Length, 4));
        
        // Firmware data (variable length, starting at position 21)
        Array.Copy(dataList, 0, frame, 21, dataList.Length);
        
        // CRC16 (2 bytes, last 2 bytes, little endian)
        var crc16 = Crc16.CalculateModbusCrc16(frame, 0, length - 2);
        ConvertLongToByte2(frame, length - 2, crc16, 0, true);
        
        return frame;
    }

    public static byte[] CreateUpdateResetDataFrame(string datalogSn, string inverterSn, int fileType, int dataCount, long crc32)
    {
        var frame = new byte[21];
        
        frame[0] = 0;
        frame[1] = UPDATE_RESET_CODE;
        
        // Serial number (10 bytes, positions 2-11)
        var serialBytes = Encoding.ASCII.GetBytes(inverterSn ?? datalogSn);
        Array.Copy(serialBytes, 0, frame, 2, Math.Min(serialBytes.Length, SERIAL_NUM_LENGTH));
        
        // File type (1 byte, position 12)
        frame[12] = (byte)fileType;
        
        // Data count (2 bytes, positions 13-14, little endian)
        ConvertLongToByte2(frame, 13, dataCount, 0, true);
        
        // CRC32 (4 bytes, positions 15-18, little endian)
        ConvertLongToByte4(frame, 15, crc32, 0, true);
        
        // CRC16 (2 bytes, positions 19-20, little endian)
        var crc16 = Crc16.CalculateModbusCrc16(frame, 0, 19);
        ConvertLongToByte2(frame, 19, crc16, 0, true);
        
        return frame;
    }

    public static byte[] CreateLuxUpdateResetDataFrame(string datalogSn, string inverterSn, int fileType, int fileHandleType, int bmsHeaderId, long crc32)
    {
        // For Lux version, use bmsHeaderId as dataCount if provided
        var dataCount = bmsHeaderId > 0 ? bmsHeaderId : 0;
        return CreateUpdateResetDataFrame(datalogSn, inverterSn, fileType, dataCount, crc32);
    }

    // Helper methods for byte conversion (matching Java ProTool behavior)
    private static void ConvertLongToByte2(byte[] buffer, int offset, long value, int unused, bool littleEndian)
    {
        if (littleEndian)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
        else
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }
    }

    private static void ConvertLongToByte4(byte[] buffer, int offset, long value, int unused, bool littleEndian)
    {
        if (littleEndian)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
        else
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }
    }
}

