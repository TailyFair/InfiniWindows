namespace InfiniWindows.Services;

public class AlertService : BaseBleService
{
    public AlertService(DeviceManager deviceManager) : base(deviceManager)
    {
    }

    protected override string Uuid => "00001811-0000-1000-8000-00805f9b34fb";

    private const string NewAlertUuid = "00002a46-0000-1000-8000-00805f9b34fb";
}