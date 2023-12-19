# InfiniWindows

[InfiniTime](https://github.com/InfiniTimeOrg/InfiniTime) companion app for Windows.

## Features

Currently supported features:

- Automatic InfiniTime device detection
- Set Time and Date
- Firmware update via BLE

## Download and install

Download the latest release from [the github releases page](https://github.com/TailyFair/InfiniWindows/releases).

## Build and run

Requirements:

- NET 8 or later
- Windows SDK

```bash
dotnet run --project .\InfiniWindows\InfiniWindows.csproj
```

## Publish

```bash
dotnet publish -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true
```