using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace InfiniWindows;

public abstract class BaseBleService
{
    public abstract string Uuid { get; }

    private readonly DeviceManager _deviceManager;

    // Current data format
    static readonly Helpers.DataFormat _dataFormat = Helpers.DataFormat.UTF8;
    private IReadOnlyList<GattCharacteristic> _characteristics;

    protected BaseBleService(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    internal async Task<string> ReadCharacteristicAsync(string characteristicUuid)
    {
        var characteristics = await GetCharacteristicsAsync();

        var attr = characteristics
            .FirstOrDefault(x => x.Uuid.ToString() == characteristicUuid);

        // Read characteristic value
        var result = await attr.ReadValueAsync(BluetoothCacheMode.Uncached);

        if (result.Status == GattCommunicationStatus.Success)
        {
            var value = Helpers.Utilities.FormatValue(result.Value, _dataFormat);
            return value;
        }

        Console.WriteLine($"Read failed: {result.Status}");

        return "";
    }

    internal async Task WriteBytesAsync(string uuid, byte[] bytes)
    {
        if (_characteristics == null)
        {
            _characteristics = await GetCharacteristicsAsync();
        }

        var attr = _characteristics
            .FirstOrDefault(x => x.Uuid.ToString() == uuid);

        var buffer = Helpers.Utilities.FormatData(bytes);
        var result = await attr.WriteValueWithResultAsync(buffer);

        if (result.Status != GattCommunicationStatus.Success)
        {
            Console.WriteLine($"Failed to write with status: {result.Status}");
        }
    }

    internal async Task WriteCharacteristicAsync(string uuid, string data, Helpers.DataFormat dataFormat)
    {
        var characteristics = await GetCharacteristicsAsync();


        var attr = characteristics
            .FirstOrDefault(x => x.Uuid.ToString() == uuid);


        var buffer = Helpers.Utilities.FormatData(data, dataFormat);
        var result = await attr.WriteValueWithResultAsync(buffer);

        if (result.Status != GattCommunicationStatus.Success)
        {
            Console.WriteLine($"Failed to write with status: {result.Status}");
        }
    }

    protected async Task<GattCharacteristic> GetCharacteristicAsync(string uuid) => (await GetCharacteristicsAsync())
        .FirstOrDefault(x => x.Uuid.ToString() == uuid);

    private async Task<IReadOnlyList<GattCharacteristic>> GetCharacteristicsAsync()
    {
        var service = (await _deviceManager.GetServices())
            .First(x => x.Uuid.ToString() == Uuid);


        IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

        var access = await service.service.RequestAccessAsync();
        if (access == DeviceAccessStatus.Allowed)
        {
            // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
            // and the new Async functions to get the characteristics of unpaired devices as well. 
            var result = await service.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                characteristics = result.Characteristics;
                return characteristics;

                // for (int i = 0; i < characteristics.Count; i++)
                // {
                //     var charToDisplay = new Helpers.BleAttributeDisplay(characteristics[i]);
                //     if (!Console.IsInputRedirected)
                //         Console.WriteLine($"#{i:00}: {charToDisplay.Name}\t{charToDisplay.Chars} \t{charToDisplay.Uuid}");
                // }
            }
            else
            {
                Console.WriteLine("Error accessing service.");
            }
        }
        else
        {
            Console.WriteLine("Error accessing service.");
        }

        return null;
    }
}