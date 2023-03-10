using System.Reflection;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using InfiniWindows;
using Spectre.Console;

class Program
{
    // "Magic" string for all BLE devices
    private const string _aqsAllBLEDevices =
        "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

    static string[] _requestedBLEProperties =
        { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.Bluetooth.Le.IsConnectable", };

    private static readonly List<DeviceInformation> _deviceList = new();

    private static class Actions
    {
        public const string Quit = "Quit";
        public const string UpdateFirmware = "Update Firmware";
        public const string SetTime = "Set Time";
    }

    private static async Task Main(string[] args)
    {
        // Start endless BLE device watcher
        var watcher = CreateDeviceWatcher();

        var version = Assembly.GetEntryAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

        Console.WriteLine($"InfiniWindows v{version}");
        Console.WriteLine("Scanning for InfiniTime device...");

        var deviceManager = new DeviceManager();
        BluetoothLEDevice device = null;
        while (device == null)
        {
            try
            {
                var foundDevice = await deviceManager.FindDeviceAsync(_deviceList);
                await Task.Delay(100);
                var deviceInformationService = new DeviceInformationService(deviceManager);
                Console.WriteLine($"Firmware Version: {await deviceInformationService.GetFirmwareRevisionAsync()}");
                device = foundDevice;
            }
            catch
            {
                // Ignore
            }
        }

        var quit = false;
        while (quit == false)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select action:")
                    .AddChoices(Actions.SetTime, Actions.UpdateFirmware, Actions.Quit)
            );

            Console.Clear();

            switch (action)
            {
                case Actions.SetTime:
                    await RunSetTimeAsync(deviceManager);
                    break;
                case Actions.UpdateFirmware:
                    await RunUpdateFirmwareAsync(deviceManager);
                    break;
                case Actions.Quit:
                    quit = true;
                    break;
            }
        }

        watcher.Stop();
    }

    private static async Task RunSetTimeAsync(DeviceManager deviceManager)
    {
        var timeService = new CurrentTimeService(deviceManager);
        await timeService.SetCurrentTimeAsync();
    }

    private static async Task RunUpdateFirmwareAsync(DeviceManager deviceManager)
    {
        Console.Write("Enter path to firmware zip archive: ");
        var zipPath = Console.ReadLine();
        await new FirmwareUpdateService(deviceManager, zipPath)
            .UpdateAsync();
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