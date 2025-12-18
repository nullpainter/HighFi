# Building

## Windows
`dotnet publish -f net10.0-windows10.0.19041.0 -r win-arm64 --self-contained`

## Linux ARM (Raspberry Pi)

`dotnet publish -f net10.0 -r linux-arm64 --self-contained`

# Dependencies

The Linux build depends on BlueZ for Bluetooth support. Install it with:

`sudo apt-get install bluez`
