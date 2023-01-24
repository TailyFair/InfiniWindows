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

    public override string Uuid => "00001530-1212-efde-1523-785feabcd123";

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

    private void ControlPointOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var val = args.CharacteristicValue.ToArray();

        // Console.WriteLine("Got event!");
        if (val.SequenceEqual(new byte[] { 0x10, 0x01, 0x01 }))
        {
            // Step 3
            _currentProcessStep = 3;
            Console.WriteLine("Sending 'INIT DFU' + Init Packet Command");
            WriteBytesAsync(ControlPointUuid, new byte[] { 0x02, 0x00 }).GetAwaiter().GetResult();

            // Step 4
            _currentProcessStep = 4;
            Console.WriteLine("Sending the Init image (DAT)");
            WriteBytesAsync(PacketUuid, _datFileBytes).GetAwaiter().GetResult();

            Console.WriteLine("Send 'INIT DFU' + Init Packet Complete Command");
            WriteBytesAsync(ControlPointUuid, new byte[] { 0x02, 0x01 }).GetAwaiter().GetResult();
            Console.WriteLine("Waiting for INIT DFU notification");
        }
        else
        {
            if (val.SequenceEqual(new byte[] { 0x10, 0x02, 0x01 }))
            {
                // Step 5
                _currentProcessStep = 5;
                Console.WriteLine("Setting packet receipt notification interval");
                WriteBytesAsync(ControlPointUuid, new byte[] { 0x08, 0x0A }).GetAwaiter().GetResult();

                // Step 6
                _currentProcessStep = 6;
                Console.WriteLine("Send 'RECEIVE FIRMWARE IMAGE' command to set DFU in firmware receive state");
                WriteBytesAsync(ControlPointUuid, new byte[] { 0x03 }).GetAwaiter().GetResult();

                // Prep Step 7
                _currentProcessStep = 7;
                _binFileChunks = _binFileBytes.Chunk(ChunkSize).ToList();
                _chunksCount = _binFileChunks.Count;
                Console.WriteLine($"Sending {_chunksCount} chunks in total");
                StepSeven();
            }
            else if (val.Length == 5 && val[0] == 0x11)
            {
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(val.Skip(1).ToArray());
                var sentBytes = (_currentChunk * ChunkSize);
                if (sentBytes == offset)
                {
                    var totalSize = _chunksCount * ChunkSize;
                    var percent = (double)(((double)sentBytes / (double)totalSize) * 100);
                    if (percent > lastProgressPercent + 1)
                    {
                        PrintProgress(sentBytes, totalSize, percent);
                        lastProgressPercent = percent;
                    }

                    StepSeven();
                }
                else
                {
                    Console.WriteLine("Offset mismatch!");
                }
            }
            else if (val.SequenceEqual(new byte[] { 0x10, 0x03, 0x01 }))
            {
                // Step 8
                _currentProcessStep = 8;
                Console.WriteLine("Sending Validate command");
                WriteBytesAsync(ControlPointUuid, new byte[] { 0x04 }).GetAwaiter().GetResult();
            }
            else if (val.SequenceEqual(new byte[] { 0x10, 0x04, 0x01 }))
            {
                // Step 9
                _currentProcessStep = 9;
                Console.WriteLine("Activate and reset");
                WriteBytesAsync(ControlPointUuid, new byte[] { 0x05 }).GetAwaiter().GetResult();
                isUpdateInProgress = false;
            }
            else
            {
                Console.WriteLine($"Error: {BitConverter.ToString(val)}");
            }
        }
    }

    private static void PrintProgress(int sentBytes, int totalSize, double percent)
    {
        Console.WriteLine($"[{DateTime.UtcNow}] Sent {sentBytes:D6}/{totalSize:D6} - {percent:F2}%");
    }

    private void StepSeven()
    {
        var chunk = _binFileChunks[_currentChunk];
        WriteBytesAsync(PacketUuid, chunk).GetAwaiter().GetResult();
        _currentChunk++;
        if (_currentChunk == _chunksCount)
        {
            PrintProgress(_chunksCount * ChunkSize, _chunksCount * ChunkSize, 100);
            Console.WriteLine("All segments are sent");
        }
        else if ((_currentChunk % SegmentsInterval) != 0)
        {
            StepSeven();
        }
        else
        {
            // Console.WriteLine("Waiting for confirmation");
        }
    }

    public async Task UpdateAsync()
    {
        await SubscribeControlPointAsync();

        // Step 1
        Console.WriteLine("Sending ('Start DFU' (0x01), 'Application' (0x04)) to DFU Control Point");
        await WriteBytesAsync(ControlPointUuid, new byte[] { 0x01, 0x04 });
        _currentProcessStep = 1;

        // Step 2
        Console.WriteLine("Sending Image size to the DFU Packet characteristic");
        var destination = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(destination, _binFileBytes.Length);
        var fullSize = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            .Concat(destination)
            .ToArray();
        await WriteBytesAsync(PacketUuid, fullSize);
        Console.WriteLine("Waiting for Image Size notification");
        _currentProcessStep = 2;


        while (isUpdateInProgress)
        {
            await Task.Delay(250);
        }

        Console.WriteLine("Update finished!");
    }

    private async Task SubscribeControlPointAsync()
    {
        var controlPoint = await GetCharacteristicAsync(ControlPointUuid);

        var status = await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify
        );
        if (status == GattCommunicationStatus.Success)
        {
            controlPoint.ValueChanged += ControlPointOnValueChanged;
        }
        else
        {
            Console.WriteLine("Can't subscribe to Control Point");
        }
    }
}