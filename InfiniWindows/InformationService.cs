namespace InfiniWindows;

public class AlertService : BaseBleService
{
    public AlertService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    public override string Uuid => "00001811-0000-1000-8000-00805f9b34fb";

    private const string NewAlertUuid = "00002a46-0000-1000-8000-00805f9b34fb";

    public async Task WriteAlertAsync(string data) =>
        await WriteCharacteristicAsync(NewAlertUuid, $"0001Title00Test Body", Helpers.DataFormat.ASCII);
}

public class DeviceInformationService : BaseBleService
{
    public DeviceInformationService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    public override string Uuid => "0000180a-0000-1000-8000-00805f9b34fb";
    
    private const string ManufacturerNameUuid = "00002a29-0000-1000-8000-00805f9b34fb";
    private const string ModelNumberUuid = "00002a24-0000-1000-8000-00805f9b34fb";
    private const string SerialNumberUuid = "00002a25-0000-1000-8000-00805f9b34fb";
    private const string FirmwareRevisionUuid = "00002a26-0000-1000-8000-00805f9b34fb";
    private const string HardwareRevisionUuid = "00002a27-0000-1000-8000-00805f9b34fb";
    private const string SoftwareRevisionUuid = "00002a28-0000-1000-8000-00805f9b34fb";

    public async Task<string> GetFirmwareRevisionAsync() => await ReadCharacteristicAsync(FirmwareRevisionUuid);
}