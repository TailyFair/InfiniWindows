using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace InfiniWindows;

public class DeviceManager
{
    private List<DeviceInformation> _deviceList;
    private BluetoothLEDevice _selectedDevice;

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public async Task<BluetoothLEDevice> FindDeviceAsync(List<DeviceInformation> deviceList)
    {
        _deviceList = deviceList;
        _selectedDevice = await FindDeviceAsync();
        return _selectedDevice;
    }

    private async Task<BluetoothLEDevice> FindDeviceAsync()
    {
        var deviceDiscovered = false;
        while (deviceDiscovered == false)
        {
            var device = GetDeviceInformation();
            if (device == null)
                continue;

            Console.WriteLine($"Device found! Name: {device.Name} ID: {device.Id}");

            if (await OpenDevice(device.Name) != 0)
            {
                Console.WriteLine($"Can't open the device!");
                return null;
            }

            return _selectedDevice;
        }

        return null;
    }

    private DeviceInformation GetDeviceInformation()
    {
        try
        {
            var prefixes = new string[] { "InfiniTime", "Pinetime-JF", "PineTime", "Y7S" };
            foreach (var prefix in prefixes)
            {
                var device = _deviceList.FirstOrDefault(x => x.Name.StartsWith(prefix));
                if (device != null)
                {
                    return device;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<Helpers.BleAttributeDisplay>> GetServices()
    {
        if (_selectedDevice == null)
            await FindDeviceAsync();

        var services = new List<Helpers.BleAttributeDisplay>();

        var result = await _selectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (result.Status == GattCommunicationStatus.Success)
        {
            for (int i = 0; i < result.Services.Count; i++)
            {
                var serviceToDisplay = new Helpers.BleAttributeDisplay(result.Services[i]);
                services.Add(serviceToDisplay);
            }
        }
        else
        {
            Console.WriteLine($"Device {_selectedDevice.Name} is unreachable.");
        }

        return services;
    }

    async Task<int> OpenDevice(string deviceName)
    {
        int retVal = 0;
        if (!string.IsNullOrEmpty(deviceName))
        {
            var devs = _deviceList
                .OrderBy(d => d.Name)
                .Where(d => !string.IsNullOrEmpty(d.Name))
                .ToList();

            string foundId = Helpers.Utilities.GetIdByNameOrNumber(devs, deviceName);

            // If device is found, connect to device and enumerate all services
            if (!string.IsNullOrEmpty(foundId))
            {
                try
                {
                    // only allow for one connection to be open at a time
                    if (_selectedDevice != null)
                        CloseDevice();

                    _selectedDevice = await BluetoothLEDevice.FromIdAsync(foundId).AsTask().TimeoutAfter(_timeout);
                    if (!Console.IsInputRedirected)
                        Console.WriteLine($"Connecting to {_selectedDevice.Name} ...");
                }
                catch
                {
                    Console.WriteLine($"Device {deviceName} is unreachable.");
                    retVal += 1;
                }
            }
            else
            {
                retVal += 1;
            }
        }
        else
        {
            Console.WriteLine("Device name can not be empty.");
            retVal += 1;
        }

        return retVal;
    }


    /// <summary>
    /// Disconnect current device and clear list of services and characteristics
    /// </summary>
    private void CloseDevice()
    {
        if (_selectedDevice != null)
        {
            if (!Console.IsInputRedirected)
                Console.WriteLine($"Device {_selectedDevice.Name} is disconnected.");

            _selectedDevice?.Dispose();
        }
    }
}