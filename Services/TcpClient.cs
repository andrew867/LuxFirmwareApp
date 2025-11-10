using System.Net.Sockets;
using System.Text;
using LuxFirmwareApp.Utils;

namespace LuxFirmwareApp.Services;

public class TcpClient : IDisposable
{
    private System.Net.Sockets.TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly string _host;
    private readonly int _port;
    private bool _disposed = false;

    public TcpClient(string host = Constants.DEFAULT_TCP_IP, int port = Constants.DEFAULT_TCP_PORT)
    {
        _host = host;
        _port = port;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _tcpClient = new System.Net.Sockets.TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            _stream = _tcpClient.GetStream();
            _stream.ReadTimeout = 5000;
            _stream.WriteTimeout = 5000;
            Console.WriteLine($"Connected to {_host}:{_port}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to {_host}:{_port}: {ex.Message}");
            return false;
        }
    }

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public async Task<string> SendCommandAsync(string commandName, byte[] frame)
    {
        if (_stream == null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to device");
        }

        try
        {
            // Send frame
            await _stream.WriteAsync(frame, 0, frame.Length);
            await _stream.FlushAsync();

            // Read response
            var responseBuffer = new byte[1024];
            var bytesRead = await _stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
            
            if (bytesRead > 0)
            {
                // Convert response to hex string (matching Java implementation)
                var response = new StringBuilder();
                for (int i = 0; i < bytesRead; i++)
                {
                    response.Append(responseBuffer[i].ToString("X2"));
                }
                return response.ToString();
            }

            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command {commandName}: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ReadMultiHoldAsync(int address, int count)
    {
        // This is a simplified version - in the real implementation, this would create
        // a proper Modbus read holding registers frame
        // For now, we'll use a basic implementation
        var frame = CreateReadMultiHoldFrame(address, count);
        return await SendCommandAsync("read_03_1", frame);
    }

    private byte[] CreateReadMultiHoldFrame(int address, int count)
    {
        // Simplified Modbus RTU frame for reading holding registers
        // This should match the actual protocol used by the devices
        var frame = new List<byte>();
        
        // Device address (assuming 1 for now)
        frame.Add(0x01);
        
        // Function code (0x03 = Read Holding Registers)
        frame.Add(0x03);
        
        // Starting address (2 bytes, big endian)
        frame.Add((byte)((address >> 8) & 0xFF));
        frame.Add((byte)(address & 0xFF));
        
        // Quantity (2 bytes, big endian)
        frame.Add((byte)((count >> 8) & 0xFF));
        frame.Add((byte)(count & 0xFF));
        
        // CRC16 (2 bytes, little endian)
        var crc16 = Crc16.CalculateModbusCrc16(frame.ToArray());
        var crcBytes = Crc16.ConvertUshortToBytes(crc16, true);
        frame.AddRange(crcBytes);
        
        return frame.ToArray();
    }

    public void Close()
    {
        _stream?.Close();
        _tcpClient?.Close();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}

