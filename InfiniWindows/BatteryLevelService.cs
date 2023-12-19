namespace InfiniWindows;

public class BatteryLevelService : BaseBleService
{
    public BatteryLevelService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    protected override string Uuid => "0000180f-0000-1000-8000-00805f9b34fb";

    private const string BatteryLevelUuid = "00002a19-0000-1000-8000-00805f9b34fb";

    public async Task<string> GetBatteryLevelAsync() => await ReadCharacteristicAsync(BatteryLevelUuid, Helpers.DataFormat.Dec);
}