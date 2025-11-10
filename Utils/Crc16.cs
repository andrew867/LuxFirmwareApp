namespace LuxFirmwareApp.Utils;

public static class Crc16
{
    private static readonly ushort[] CrcTable = new ushort[256];
    private const ushort Polynomial = 0xA001; // Modbus CRC16 polynomial

    static Crc16()
    {
        for (ushort i = 0; i < 256; i++)
        {
            ushort crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc = (ushort)((crc >> 1) ^ Polynomial);
                }
                else
                {
                    crc >>= 1;
                }
            }
            CrcTable[i] = crc;
        }
    }

    public static ushort CalculateModbusCrc16(byte[] data, int offset = 0, int length = -1)
    {
        if (length < 0)
        {
            length = data.Length - offset;
        }

        ushort crc = 0xFFFF;

        for (int i = offset; i < offset + length; i++)
        {
            byte index = (byte)(crc ^ data[i]);
            crc = (ushort)((crc >> 8) ^ CrcTable[index]);
        }

        return crc;
    }

    public static byte[] ConvertUshortToBytes(ushort value, bool littleEndian = false)
    {
        if (littleEndian)
        {
            return new byte[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) };
        }
        else
        {
            return new byte[] { (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };
        }
    }
}

