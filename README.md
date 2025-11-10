# LuxFirmwareApp

A cross-platform C# .NET Core command-line application for downloading and updating firmware on LuxPower, EG4, GSL, and other compatible inverters and battery packs.

## Quick Start

```bash
# Build the project
dotnet build

# Run the application
dotnet run -- --help
```

## Features

- Download firmware from official servers
- Update inverters and battery packs via TCP/IP
- Support for multiple brands (LuxPower, EG4, GSL, MID, etc.)
- Support for 30+ device types (inverters and battery packs)
- Local firmware cache management
- Cross-platform (Windows, Linux, macOS)

## Installation

1. Ensure you have .NET 8.0 SDK installed
2. Clone or download this repository
3. Navigate to the `LuxFirmwareApp` directory
4. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Login

Login to the Lux Cloud server to get a session ID:

```bash
dotnet run -- login --username <USERNAME> --password <PASSWORD> --brand LUX_POWER
```

After successful login, you'll receive a session ID that can be used with other commands:

```bash
dotnet run -- list --brand LUX_POWER --device-type LXP_LB_8_12K --session-id <SESSION_ID>
```

Alternatively, you can use username/password directly with commands:

```bash
dotnet run -- list --brand LUX_POWER --device-type LXP_LB_8_12K --username <USERNAME> --password <PASSWORD>
```

### Download All Firmware (Bulk Cache)

Download and cache all firmware for all brands and device types:

```bash
dotnet run -- download-all --session-id <SESSION_ID>
```

Or with username/password:

```bash
dotnet run -- download-all --username <USERNAME> --password <PASSWORD>
```

Options:
- `--skip-existing, -s`: Skip firmware files that are already cached (default: true)
- `--beta`: Include beta firmware versions
- `--output-dir, -o`: Output directory for firmware files (default: `firmware`)

This command will:
- Iterate through all supported platforms/brands
- For each brand, check all supported device types
- Download all available firmware files
- Cache them locally for offline use
- Skip already downloaded files (unless `--skip-existing false` is used)
- Show a summary at the end

### List Available Firmware

List all available firmware for a specific device type:

```bash
dotnet run -- list --brand LUX_POWER --device-type LXP_LB_8_12K
```

### Download Firmware

Download firmware for a device type:

```bash
dotnet run -- download --brand LUX_POWER --device-type LXP_LB_8_12K
```

Download a specific firmware by record ID:

```bash
dotnet run -- download --brand LUX_POWER --device-type LXP_LB_8_12K --record-id <RECORD_ID>
```

### Restore from Cache

List all firmware files in local cache:

```bash
dotnet run -- restore
```

### List Device Models

List all available device models:

```bash
dotnet run -- list-devices
```

List device models for a specific brand:

```bash
dotnet run -- list-devices --brand EG4
```

### Update Device

Update an inverter or battery pack with downloaded firmware:

```bash
dotnet run -- update --record-id <RECORD_ID> --ip-address 10.10.10.1 --serial-number <SERIAL_NUMBER>
```

## Command Options

### Global Options

- `--brand, -b`: Brand/platform (LUX_POWER, EG4, GSL, MID, etc.) - default: LUX_POWER (uses NA server: na.luxpowertek.com)
- `--output-dir, -o`: Output directory for firmware files - default: `firmware`
- `--beta`: Use beta firmware endpoint
- `--base-url`: Override base URL (optional, auto-selected by brand)

### Download Command

- `--device-type, -t`: Firmware device type (required)
- `--record-id, -r`: Specific firmware record ID to download (optional)

### Update Command

- `--record-id, -r`: Firmware record ID to update (required)
- `--ip-address, -i`: Device IP address - default: `10.10.10.1`
- `--serial-number, -s`: Device serial number - default: `FFFFFFFFFFFFFFFFFFFF`

### Login Command

- `--username, -u`: Username/account (required)
- `--password, -p`: Password (required)
- `--brand, -b`: Brand/platform - default: LUX_POWER

### List Devices Command

- `--brand, -b`: Filter by brand/platform (optional) - shows only device types supported by the specified brand

### Authentication Options

All commands that require server access support authentication via:

- `--session-id, --cookie`: JSESSIONID cookie value (obtained from login command)
- `--username, -u`: Username for automatic login
- `--password, -p`: Password for automatic login

## Supported Device Types

### Inverters
- SNA_3000_6000, SNA_US_6000, SNA_12K, SNA_US_12K
- LXP_3_6K_HYBRID_STANDARD, LXP_3_6K_HYBRID_PARALLEL
- LXP_3600_ACS_STANDARD, LXP_3600_ACS_PARALLEL
- LXP_LB_8_12K
- LSP_100K
- LXP_HV_6K_HYBRID
- Lite_Stor
- TRIP_HB_EU_6_20K, TRIP_LV_5_20K
- GEN_LB_EU_3_6K, GEN_LB_EU_7_10K_GST
- POWER_HUB

### Battery Packs
- BATT_hi_5_v1, BATT_hi_5_v2
- BATT_power_gem, BATT_power_gem_plus, BATT_power_gem_max
- BATT_j_of_10kWh
- BATT_eco_beast
- BATT_p_shield, BATT_p_shield_max
- BATT_power_stack
- BATT_c14
- BATT_e0b_Hi_Li

### Dongles
- DONGLE_E_WIFI_DONGLE

## Supported Brands

- LUX_POWER
- EG4
- GSL
- MID
- HUAYU
- TECLOMAN
- WARU_ENERGY
- RENON
- SUNBEAT
- FORTRESS
- INVT
- BOER
- INCCO
- CROWN
- POWER_ZONE
- GTEC

## Notes

- Firmware files are cached locally in the `firmware/` directory
- The application uses the same API endpoints and protocols as the official mobile apps
- Ensure your device is connected to the same network and accessible via TCP/IP
- The update process may take several minutes depending on firmware size

## License

This project is provided as-is for educational and personal use.

