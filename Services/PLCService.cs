using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PLCDataLogger.Services
{
    public class PLCService
    {
        private static readonly Lazy<PLCService> _instance = new(() => new PLCService());
        public static PLCService Instance => _instance.Value;

        private string PLCIp = "192.168.3.39";
        private int PLCPort = 9005;

        // A semaphore to prevent multiple threads from trying to talk to the PLC at the exact same time.
        private readonly SemaphoreSlim _plcLock = new SemaphoreSlim(1, 1);

        private PLCService() { }

        public void SetPLCConnection(string ip, int port)
        {
            PLCIp = ip;
            PLCPort = port;
        }

        public async Task WritePCStatusToPLC(bool PCStatus, CancellationToken token = default)
        {
            try
            {
                ushort value = PCStatus ? (ushort)1 : (ushort)0;
                byte[] writeCommand = BuildWriteCommand(1000, value);

                await ExecutePlcCommunicationAsync(writeCommand, token);
            }
            catch (OperationCanceledException)
            {
                // Re-throw the cancellation exception so the caller knows it was a timeout.
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, "Error while writing PC status to PLC");
                throw new InvalidOperationException("Failed to write PC status to PLC.", ex);
            }
        }

        public async Task WriteSingleWord(int address, ushort value)
        {
            try
            {
                byte[] writeCommand = BuildWriteCommand(address, value);

                await ExecutePlcCommunicationAsync(writeCommand);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, $"Error while writing Single Word of {value} to D{address}");
            }
        }

        public async Task<ushort?> ReadRegisterAsync(int address)
        {
            ushort[]? result = await ReadRegistersAsync(address, 1);
            return result?.FirstOrDefault();
        }

        /// <summary>
        /// Reads one or more consecutive 16-bit words from D registers on the PLC.
        /// </summary>
        /// <param name="startAddress">The starting D register address (e.g., 1056).</param>
        /// <param name="numberOfPoints">The number of registers to read (e.g., 5).</param>
        /// <returns>An array of ushort values, or null if the read operation failed.</returns>
        public async Task<ushort[]?> ReadRegistersAsync(int startAddress, int numberOfPoints)
        {
            try
            {
                // Use the enhanced build method
                byte[] readCommand = BuildReadCommand(startAddress, numberOfPoints);
                byte[]? response = await ExecutePlcCommunicationAsync(readCommand);

                // A valid read response has a header and then the data.
                // Header is 11 bytes. Each point is a ushort (2 bytes).
                int expectedResponseLength = 11 + (numberOfPoints * 2);

                if (response != null && response.Length >= expectedResponseLength && response[8] == 0x00) // Check for no error code
                {
                    var values = new ushort[numberOfPoints];
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        // The data for each point starts at index 11. Each point is 2 bytes.
                        int dataIndex = 11 + (i * 2);
                        values[i] = BitConverter.ToUInt16(response, dataIndex);
                    }
                    return values;
                }
                else
                {
                    var errorCode = response != null && response.Length > 8 ? $"0x{response[8]:X2}" : "N/A";
                    LoggingService.Instance.LogError(null, $"Invalid response from PLC. Length: {response?.Length ?? 0}, Error Code: {errorCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, $"Error while reading {numberOfPoints} points from PLC at D{startAddress}");
                return null;
            }
        }

        private async Task<byte[]?> ExecutePlcCommunicationAsync(byte[] command, CancellationToken token = default)
        {
            await _plcLock.WaitAsync();

            try
            {
                using (var tcpClient = new TcpClient())
                {
                    Task connectTask = tcpClient.ConnectAsync(PLCIp, PLCPort);
                    Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(6), token);
                    Task completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        throw new OperationCanceledException($"Connection to PLC timed out");
                    }

                    while (!tcpClient.Connected)
                    {
                        await Task.Delay(500);
                    }

                    using (var stream = tcpClient.GetStream())
                    {
                        stream.WriteTimeout = 2000;
                        stream.ReadTimeout = 2000;
                        await stream.WriteAsync(command, 0, command.Length);

                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        if (bytesRead == 0) return null;

                        byte[] response = new byte[bytesRead];
                        Array.Copy(buffer, response, bytesRead);

                        return response;
                    }
                }
            }
            finally
            {
                // CRITICAL: Always release the semaphore.
                _plcLock.Release();
            }
        }

        private static byte[] BuildWriteCommand(int address, ushort value)
        {
            var packet = new List<byte>
            {
                0x50, 0x00, // Subheader
                0x00,       // Network No
                0xFF,       // PC No
                0xFF, 0x03, // Request Destination Module I/O No
                0x00,       // Request Destination Module Station No
                0x0E, 0x00, // Request Data Length (14 bytes for this command)
                0x10, 0x00, // CPU Monitoring Timer (4 seconds)
                0x01, 0x14, // Command: Batch Write in Word Units
                0x00, 0x00, // Subcommand: Word units
                (byte)(address & 0xFF), (byte)((address >> 8) & 0xFF), (byte)((address >> 16) & 0xFF), // Address
                0xA8,       // Device Code for D registers
                0x01, 0x00, // Number of points (1)
                (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) // Data
            };
            return packet.ToArray();
        }

        /// <summary>
        /// Builds an MC Protocol frame for reading one or more D registers.
        /// </summary>
        /// <param name="startAddress">The starting D register address (e.g., 1056).</param>
        /// <param name="numberOfPoints">The number of consecutive registers to read (e.g., 5).</param>
        private static byte[] BuildReadCommand(int startAddress, int numberOfPoints)
        {
            var packet = new List<byte>
            {
                0x50, 0x00, // Subheader
                0x00,       // Network No
                0xFF,       // PC No
                0xFF, 0x03, // Request Destination Module I/O No
                0x00,       // Request Destination Module Station No
                0x0C, 0x00, // Request Data Length (12 bytes, this does not change for a read command)
                0x10, 0x00, // CPU Monitoring Timer (4 seconds)
                0x01, 0x04, // Command: Batch Read in Word Units
                0x00, 0x00, // Subcommand: Word units

                // Starting Address (e.g., 1056)
                (byte)(startAddress & 0xFF),
                (byte)((startAddress >> 8) & 0xFF),
                (byte)((startAddress >> 16) & 0xFF),

                0xA8,       // Device Code for D registers

                // Number of Points to read (e.g., 5)
                // Sent as a 16-bit little-endian value.
                (byte)(numberOfPoints & 0xFF),
                (byte)((numberOfPoints >> 8) & 0xFF)
            };
            return packet.ToArray();
        }
    }
}
