using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace InfiniWindows;

public class FirmwareUpdateService : BaseBleService
{
    public FirmwareUpdateService(DeviceManager deviceManager, string path) : base(deviceManager)
    {
        PrepareFiles(path);
    }

    protected override string Uuid => "00001530-1212-efde-1523-785feabcd123";

    private const string ControlPointUuid = "00001531-1212-efde-1523-785feabcd123";
    private const string PacketUuid = "00001532-1212-efde-1523-785feabcd123";

    // Settings
    private const int ChunkSize = 20;
    private const int SegmentsInterval = 0x0A;

    private bool isUpdateInProgress = true;

    private int _currentProcessStep = 0;
    private double lastProgressPercent = 0;
    private List<byte[]> _binFileChunks;
    private int _currentChunk = 0;
    private int _chunksCount;

    private byte[] _datFileBytes;
    private byte[] _binFileBytes;

    private void PrepareFiles(string zipPath)
    {
        zipPath = zipPath.Trim('"');

        if (!zipPath.EndsWith(".zip"))
            throw new ArgumentException("Firmware file must be a zip archive!");

        if (!File.Exists(zipPath))
            throw new ArgumentException("File does not exist!");

        using FileStream fs = new FileStream(zipPath, FileMode.Open);
        using ZipArchive zip = new ZipArchive(fs);

        // DAT
        var datFile = zip.Entries.FirstOrDefault(x => x.Name.EndsWith(".dat"));
        if (datFile == null)
            throw new ArgumentException("DAT file cannot be found");
        _datFileBytes = ReadFully(datFile.Open());

        // BIN
        var binFile = zip.Entries.FirstOrDefault(x => x.Name.EndsWith(".bin"));
        if (binFile == null)
            throw new ArgumentException("BIN file cannot be found");
        _binFileBytes = ReadFully(binFile.Open());

        // Prepare BIN file
        _binFileChunks = _binFileBytes.Chunk(ChunkSize).ToList();
        _chunksCount = _binFileChunks.Count;
    }

    public async Task UpdateAsync()
    {
        await SubscribeToCharacteristicAsync(ControlPointUuid, OnControlPointOnValueChanged);

        await RunStepOneAsync();

        await RunStepTwoAsync();

        while (_currentProcessStep != 10)
        {
            await Task.Delay(250);
        }

        Console.WriteLine("Update finished!");
    }

    private void OnControlPointOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) => Task.Run(
        async () =>
        {
            var value = args.CharacteristicValue.ToArray();

            if (value.SequenceEqual(new byte[] { 0x10, 0x01, 0x01 }))
            {
                await RunStepThreeAsync();

                await RunStepFourAsync();
            }
            else if (value.SequenceEqual(new byte[] { 0x10, 0x02, 0x01 }))
            {
                await RunStepFiveAsync();

                await RunStepSixAsync();

                await RunStepSevenAsync();
            }
            else if (value.Length == 5 && value[0] == 0x11)
            {
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(value.Skip(1).ToArray());
                var sentBytes = (_currentChunk * ChunkSize);
                if (sentBytes != offset)
                {
                    Console.WriteLine("Offset mismatch!");
                    // TODO Handle mismatch
                    return;
                }

                var totalSize = _chunksCount * ChunkSize;
                var percent = (sentBytes / (double)totalSize) * 100;
                if (percent > lastProgressPercent + 1)
                {
                    PrintProgress(sentBytes, totalSize, percent);
                    lastProgressPercent = percent;
                }

                await RunStepSevenAsync();
            }
            else if (value.SequenceEqual(new byte[] { 0x10, 0x03, 0x01 }))
            {
                await RunStepEightAsync();
            }
            else if (value.SequenceEqual(new byte[] { 0x10, 0x04, 0x01 }))
            {
                await RunStepNineAsync();
            }
            else
            {
                Console.WriteLine($"Error: {BitConverter.ToString(value)}");
            }
        });

    private async Task RunStepOneAsync()
    {
        _currentProcessStep = 1;
        Console.WriteLine("Sending ('Start DFU' (0x01), 'Application' (0x04)) to DFU Control Point");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x01, 0x04 });
    }

    private async Task RunStepTwoAsync()
    {
        _currentProcessStep = 2;
        Console.WriteLine("Sending Image size to the DFU Packet characteristic");
        var destination = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(destination, _binFileBytes.Length);
        var fullSize = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            .Concat(destination)
            .ToArray();
        await WriteBytesAsync(PacketUuid, fullSize);
        Console.WriteLine("Waiting for Image Size notification");
    }

    private async Task RunStepThreeAsync()
    {
        _currentProcessStep = 3;
        Console.WriteLine("Sending 'INIT DFU' + Init Packet Command");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x02, 0x00 });
    }

    private async Task RunStepFourAsync()
    {
        _currentProcessStep = 4;
        Console.WriteLine("Sending the Init image (DAT)");
        await WriteBytesAsync(PacketUuid, _datFileBytes);

        Console.WriteLine("Send 'INIT DFU' + Init Packet Complete Command");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x02, 0x01 });
        Console.WriteLine("Waiting for INIT DFU notification");
    }

    private async Task RunStepFiveAsync()
    {
        _currentProcessStep = 5;
        Console.WriteLine("Setting packet receipt notification interval");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x08, 0x0A });
    }

    private async Task RunStepSixAsync()
    {
        _currentProcessStep = 6;
        Console.WriteLine("Send 'RECEIVE FIRMWARE IMAGE' command to set DFU in firmware receive state");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x03 });
    }

    private async Task RunStepSevenAsync()
    {
        _currentProcessStep = 7;
        var chunk = _binFileChunks[_currentChunk];
        await WriteBytesAsync(PacketUuid, chunk);
        _currentChunk++;
        if (_currentChunk == _chunksCount)
        {
            PrintProgress(_chunksCount * ChunkSize, _chunksCount * ChunkSize, 100);
            Console.WriteLine("All chunks are sent");
        }
        else if ((_currentChunk % SegmentsInterval) != 0)
        {
            await RunStepSevenAsync();
        }
    }

    private async Task RunStepEightAsync()
    {
        _currentProcessStep = 8;
        Console.WriteLine("Sending Validate command");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x04 });
    }

    private async Task RunStepNineAsync()
    {
        _currentProcessStep = 9;
        Console.WriteLine("Activate and reset");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x05 });
        isUpdateInProgress = false;
        _currentProcessStep = 10;
        Console.WriteLine("Finished");
    }

    private static void PrintProgress(int sentBytes, int totalSize, double percent)
    {
        Console.WriteLine($"[{DateTime.UtcNow}] Sent {sentBytes.ToString(),6}/{totalSize:D6} - {percent:F2}%");
    }

    private static byte[] ReadFully(Stream input)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }
}