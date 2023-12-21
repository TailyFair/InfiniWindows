using InfiniWindows.Services;

namespace InfiniWindows;

public class CurrentTimeService : BaseBleService
{
    public CurrentTimeService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    protected override string Uuid => "00001805-0000-1000-8000-00805f9b34fb";

    private const string CurrentTimeUuid = "00002a2b-0000-1000-8000-00805f9b34fb";

    public async Task SetCurrentTimeAsync()
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var currentDateTime = DateTime.Now;

        binaryWriter.Write((ushort)currentDateTime.Year);
        binaryWriter.Write((byte)currentDateTime.Month);
        binaryWriter.Write((byte)currentDateTime.Day);
        binaryWriter.Write((byte)currentDateTime.Hour);
        binaryWriter.Write((byte)currentDateTime.Minute);
        binaryWriter.Write((byte)currentDateTime.Second);
        binaryWriter.Write((byte)currentDateTime.DayOfWeek);
        binaryWriter.Write((byte)currentDateTime.Millisecond / 1e6 * 256);
        binaryWriter.Write(new byte[] { 0x00, 0x01 });

        var dateTimeBytes = memoryStream.ToArray();

        await WriteBytesAsync(CurrentTimeUuid, dateTimeBytes);
        Console.WriteLine($"Time is set to: {currentDateTime}");
    }
}