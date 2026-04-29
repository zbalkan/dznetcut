# dznetcut

`dznetcut` is a Windows LAN operations tool for **host discovery** and **targeted ARP disruption testing** with both a GUI and a CLI.

> ⚠️ **Authorized use only:** ARP spoofing can interrupt network connectivity. Use this tool only on systems and networks you own or are explicitly authorized to test.

## What’s in this version

This codebase now ships as a complete, usable application with:

- A working **Windows Forms GUI** for adapter selection, scanning, target selection, and cut control.
- A working **CLI** with command routing for adapter listing, host scans, and cut sessions.
- **Adapter inventory and selection logic** that favors usable/physical interfaces.
- **Evidence-based host discovery** (ARP/ICMP/passive signals) with confidence scoring.
- **ARP protection controls** that guard protected identities by default.
- **Unit tests** for core parsing and behavior.

## Features

- **Dual interface**: run as desktop GUI (default) or as CLI from the same executable.
- **Discovery workflow**: enumerate hosts on the local LAN through the chosen adapter.
- **Targeted cut workflow**: repeatedly send forged ARP replies between target(s) and gateway during a bounded session.
- **Duration-bound execution**: scan and cut commands support a `--duration` window.
- **JSON adapter output**: `list-adapters` supports `--json`.
- **Safety defaults**: ARP protection enabled unless explicitly disabled.

## Requirements

- **OS**: Windows
- **Runtime target**: .NET Framework 4.8.1 (installed by default on Windows 10 & 11)
- **Packet driver**: Npcap (required for SharpPcap/LibPcap capture + transmit)
- **Permissions**: Administrator privileges are typically required for packet-level operations
- **Build tooling**: Visual Studio (recommended) or `dotnet` with .NET Framework tooling available

## Build

From repository root:

```bash
dotnet restore src/dznetcut.sln
dotnet build src/dznetcut.sln -c Release
```

## Run modes

- **Default GUI launch**
  ```bash
  dznetcut
  ```

- **Force GUI launch**
  ```bash
  dznetcut --gui
  ```

- **CLI help**
  ```bash
  dznetcut --help
  # or
  dznetcut help
  ```

## CLI reference

### Commands

- `list-adapters` — list available capture adapters
- `scan` — discover LAN hosts
- `cut` — start targeted ARP cut traffic
- `stop` — reserved for future background sessions (currently informational)

### Global options

- `--help`, `-h` — show help
- `--gui` — force GUI mode
- `--json` — JSON output where supported
- `--verbose`, `-v` — verbose logging

### cut-specific option

- `--no-arp-protection`, `-nap` — disable ARP protection (default is enabled)

### Examples

List adapters:

```bash
dznetcut list-adapters
```

List adapters as JSON:

```bash
dznetcut list-adapters --json
```

Scan:

```bash
dznetcut scan \
  --adapter "Ethernet" \
  --gateway-ip 192.168.1.1 \
  --duration 25
```

Cut one target:

```bash
dznetcut cut \
  --adapter "Ethernet" \
  --gateway-ip 192.168.1.1 \
  --gateway-mac AA-BB-CC-DD-EE-FF \
  --target 192.168.1.42@11-22-33-44-55-66 \
  --duration 30
```

Disable ARP protection intentionally:

```bash
dznetcut cut \
  --adapter "Ethernet" \
  --gateway-ip 192.168.1.1 \
  --gateway-mac AA-BB-CC-DD-EE-FF \
  --target 192.168.1.42@11-22-33-44-55-66 \
  --duration 30 \
  --no-arp-protection
```

## GUI quick workflow

1. Run as Administrator.
2. Choose the active LAN adapter.
3. Start scan and wait for host list stabilization.
4. Confirm gateway/local protected entries.
5. Select non-protected target(s).
6. Start cut.
7. Stop cut to end active poisoning loop.

## Testing

Run unit tests:

```bash
dotnet test src/dznetcut.Tests/dznetcut.Tests.csproj
```

## Safety, legality, and ethics

Use only in:

- internal labs,
- authorized security tests,
- defensive validation,
- incident-response diagnostics on owned infrastructure.

Never run this tool against third-party or shared networks without explicit written permission.

## License

- Current project code: **GPL-3.0-only** (`LICENSE`)
- Historical upstream notice: **MIT** (`LICENSE.MIT`)
