using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using InfiniWindows;

class Program
{
    // "Magic" string for all BLE devices
    static string _aqsAllBLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

    static string[] _requestedBLEProperties =
        { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.Bluetooth.Le.IsConnectable", };


    private static readonly List<DeviceInformation> _deviceList = new();
    private static BluetoothLEDevice _selectedDevice = null;

    // Current data format
    static readonly Helpers.DataFormat _dataFormat = Helpers.DataFormat.UTF8;

    static readonly List<Helpers.BleAttributeDisplay> _services = new();
    private static Helpers.BleAttributeDisplay _selectedService = null;

    static readonly List<Helpers.BleAttributeDisplay> _characteristics = new();
    private static Helpers.BleAttributeDisplay _selectedCharacteristic = null;


    private static async Task Main(string[] args)
    {
        Console.WriteLine("Hello World!");

        // Start endless BLE device watcher
        var watcher = CreateDeviceWatcher();

        Console.WriteLine("Scanning for InfiniTime device...");
        var deviceManager = new DeviceManager();
        _selectedDevice = await deviceManager.FindDeviceAsync(_deviceList);
        _services.AddRange(await deviceManager.GetServices());

        var deviceInformationService = new DeviceInformationService(deviceManager);
        Console.WriteLine($"Firmware Version: {await deviceInformationService.GetFirmwareRevisionAsync()}");

        Console.WriteLine("Connected!");

        watcher.Stop();
    }

    private static DeviceWatcher CreateDeviceWatcher()
    {
        var watcher = DeviceInformation.CreateWatcher(_aqsAllBLEDevices, _requestedBLEProperties,
            DeviceInformationKind.AssociationEndpoint);
        watcher.Added += (DeviceWatcher sender, DeviceInformation devInfo) =>
        {
            if (_deviceList.FirstOrDefault(d => d.Id.Equals(devInfo.Id) || d.Name.Equals(devInfo.Name)) == null)
                _deviceList.Add(devInfo);
        };
        watcher.Updated += (_, __) => { }; // We need handler for this event, even an empty!
        //Watch for a device being removed by the watcher
        //watcher.Removed += (DeviceWatcher sender, DeviceInformationUpdate devInfo) =>
        //{
        //    _deviceList.Remove(FindKnownDevice(devInfo.Id));
        //};
        watcher.EnumerationCompleted += (DeviceWatcher sender, object arg) => { sender.Stop(); };
        watcher.Stopped += (DeviceWatcher sender, object arg) =>
        {
            _deviceList.Clear();
            sender.Start();
        };
        watcher.Start();

        return watcher;
    }
}