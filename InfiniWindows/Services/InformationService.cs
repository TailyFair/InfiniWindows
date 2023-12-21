namespace InfiniWindows.Services;

public class DeviceInformationService : BaseBleService
{
    public DeviceInformationService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    protected override string Uuid => "0000180a-0000-1000-8000-00805f9b34fb";

    private const string ManufacturerNameUuid = "00002a29-0000-1000-8000-00805f9b34fb";
    private const string ModelNumberUuid = "00002a24-0000-1000-8000-00805f9b34fb";
    private const string SerialNumberUuid = "00002a25-0000-1000-8000-00805f9b34fb";
    private const string FirmwareRevisionUuid = "00002a26-0000-1000-8000-00805f9b34fb";
    private const string HardwareRevisionUuid = "00002a27-0000-1000-8000-00805f9b34fb";
    private const string SoftwareRevisionUuid = "00002a28-0000-1000-8000-00805f9b34fb";

    public async Task<string> GetFirmwareRevisionAsync() => await ReadCharacteristicAsync(FirmwareRevisionUuid);
    public async Task<string> GetManufacturerNameUuid() => await ReadCharacteristicAsync(ManufacturerNameUuid);
    public async Task<string> GetModelNumberAsync() => await ReadCharacteristicAsync(ModelNumberUuid);
    public async Task<string> GetSerialNumberAsync() => await ReadCharacteristicAsync(SerialNumberUuid);
    public async Task<string> GetHardwareRevisionAsync() => await ReadCharacteristicAsync(HardwareRevisionUuid);
    public async Task<string> GetSoftwareRevisionAsync() => await ReadCharacteristicAsync(SoftwareRevisionUuid);
}