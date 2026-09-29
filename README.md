# WhichCOM

**Which COM port is my ESP32 on?** WhichCOM answers that without opening Device Manager.

WhichCOM is a Windows 11 widget provider that shows your connected serial devices (ESP32, Arduino,
CP210x, CH340, FTDI…) on the widget board, along with your Wi-Fi, Ethernet and audio device status.
Open the widget board (<kbd>Win</kbd> + <kbd>W</kbd>) and see everything at a glance. It comes with
`comls`, a small command line tool for scripts.

> 🚧 **Work in progress.** `comls` and the Serial Ports widget work today. The System Status widget is under development.

<!-- Screenshot placeholder: docs/images/widgets.png -->

## Status

| Part | State |
| --- | --- |
| Shared port library (`WhichCOM.Core`) | ✅ Available |
| `comls` command line tool | ✅ Available |
| Widget provider, MSIX package, certificate script | ✅ Available |
| "Serial Ports" widget | ✅ Available |
| "System Status" widget | ⏳ Planned |
| Nicknames from the widget's customize screen | ⏳ Planned |

## Features

- **Serial Ports widget** (small / medium / large)
  - Lists connected COM ports with chip type detection
  - One-click copy of the port name
  - Marks the port you plugged in most recently
  - Nicknames for your boards (e.g. "Sensor board #2")
- **System Status widget** (small / medium)
  - Wi-Fi (SSID, signal strength, IP address) and Ethernet (link speed, IP address)
  - Internet connectivity
  - Default audio output and input device
- **`comls` command line tool**
  - Lists serial ports as a table or as JSON
  - Script friendly: `pio run -t upload --upload-port $(comls --latest)`

The widgets only collect data while the widget board is visible. When the board is closed,
WhichCOM does no work.

## Requirements

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source
- Developer Mode enabled, to install the widget package (not needed for `comls`)

## Build

```powershell
git clone https://github.com/halil158/WhichCOM.git
cd WhichCOM
dotnet build WhichCOM.slnx --configuration Release
dotnet test WhichCOM.slnx --configuration Release
```

Visual Studio is not required.

## comls

Build a single `comls.exe` and put it in a folder that is on your `PATH`:

```powershell
dotnet publish src/WhichCOM.Cli --configuration Release --runtime win-x64 --self-contained false -p:PublishSingleFile=true --output publish/comls
```

```text
> comls
PORT   ALIAS            CHIP              VID:PID    SERIAL             DESCRIPTION
COM3   -                CH340             1A86:7523  -                  USB-SERIAL CH340
COM7*  Sensor board #2  ESP32 native USB  303A:1001  AA:BB:CC:DD:EE:FF  USB Serial Device
```

`*` marks the port that was plugged in most recently.

| Command | Result |
| --- | --- |
| `comls` | Table of the serial ports |
| `comls --json` | The same list as JSON, with all details |
| `comls --latest` | Only the name of the most recently plugged port, e.g. `COM7` |
| `comls --match esp32` | Only ports whose nickname, chip type or description contains the text |
| `comls --all` | Also lists Bluetooth serial ports, which are hidden by default |
| `comls --help` | Help |

Options can be combined: `comls --latest --match esp32`.

Exit codes: `0` success, `1` no matching port, `2` invalid arguments, `3` error. With `--latest`,
nothing is written to the standard output when there is no port, so `$(comls --latest)` expands to
an empty string.

### Examples

```powershell
# PlatformIO
pio run -t upload --upload-port $(comls --latest)

# esptool, only ESP32 boards
esptool.py --port $(comls --latest --match esp32) chip_id

# PowerShell objects
comls --json | ConvertFrom-Json | Where-Object chipLabel -like 'FTDI*'
```

## Settings and nicknames

Settings are shared by the widgets and `comls`. They are stored in
`%LOCALAPPDATA%\WhichCOM\settings.json`. The file is optional; copy
[settings.example.json](settings.example.json) to get started.

A nickname belongs to a device, not to a port number:

- A device with a serial number is identified by that serial number.
- A device without a serial number is identified by its VID:PID together with the USB socket it is
  plugged into. `comls --json` prints this value as `locationPath`.

To add chips that WhichCOM does not know yet, see [CONTRIBUTING.md](CONTRIBUTING.md). For private
additions, create `%LOCALAPPDATA%\WhichCOM\chips.json` with the same format as
[usb-serial-chips.json](src/WhichCOM.Core/Data/usb-serial-chips.json).

## Install the widgets

All scripts are in the [scripts](scripts) folder and run in PowerShell 7 (`pwsh`). Turn on
Developer Mode first: **Settings > System > For developers > Developer Mode**.

### Quick: register without a certificate

```powershell
./scripts/Register-DevPackage.ps1
```

This builds the provider and registers it for your user. Nothing is signed and no certificate is
needed. Run it again after every change; pinned widgets stay on the board.

### Full: signed package

1. Create your own signing certificate. It is stored in your Windows certificate store; the
   private key cannot be exported and is never written to a file.

   ```powershell
   ./scripts/New-DevCert.ps1
   ```

2. Let Windows trust the certificate. This needs an **elevated** PowerShell and is done once.
   An elevated PowerShell starts in the system folder, so use the full path of the script;
   step 1 prints the exact command.

   ```powershell
   pwsh -File "<path to the repository>\scripts\New-DevCert.ps1" -Trust
   ```

3. Build, sign and install.

   ```powershell
   ./scripts/Build-Package.ps1
   ./scripts/Install-Package.ps1
   ```

The package contains everything it needs, including .NET and the Windows App SDK files.

### Add a widget to the board

1. Open the widget board with <kbd>Win</kbd> + <kbd>W</kbd>.
2. Choose **Add widgets** (the **+** button).
3. Select **WhichCOM** in the list, pick a widget and choose **Pin**.
4. Use the **…** menu of the widget to change its size or to unpin it.

### Uninstall

```powershell
./scripts/Uninstall-Package.ps1                      # keeps your settings
./scripts/Uninstall-Package.ps1 -RemoveSettings      # also deletes your nicknames
./scripts/Uninstall-Package.ps1 -RemoveCertificate   # also deletes the certificate
```

### Troubleshooting

| Problem | What to check |
| --- | --- |
| WhichCOM is missing in **Add widgets** | Close the board and open it again; the list is cached. If it is still missing, sign out and in again, or end the `Widgets` and `WidgetService` processes in Task Manager. |
| | `Get-AppxPackage Yigisoft.WhichCOM` must list the package. If not, the installation failed. |
| | Make sure **Widgets** is enabled in **Settings > Personalization > Taskbar** and that the "Windows Web Experience Pack" is up to date in the Microsoft Store. |
| The widget is pinned but stays empty or shows an error | Read `%LOCALAPPDATA%\WhichCOM\provider.log`. It records when the provider starts and every failure. |
| | Check that the provider runs while the board is open: `Get-Process WhichCOM.WidgetProvider`. |
| `Install-Package.ps1` says the signature is not trusted | Run `./scripts/New-DevCert.ps1 -Trust` in an elevated PowerShell. |
| Installation fails with `0x80073CFB` or "a package with the same identity is already installed" | Run `./scripts/Uninstall-Package.ps1`, then install again. |
| The build fails with `PRI210 ... File move failed` | The widget host has a file of an older build open. Rename `resources.pri` in the build output folder, or end the `WidgetService` process, and build again. |

## Known limitations

- The time a device was plugged in comes from Windows. Built-in ports count as plugged in at every
  boot, so `comls --latest` prefers USB devices.
- Many low-cost USB serial adapters have no serial number, or all share the same one. Their
  nickname then belongs to the USB socket, not to the adapter.
- Device descriptions are shown in the display language of Windows.
- A widget has a fixed height and cannot scroll. The Serial Ports widget shows up to 4 (small),
  3 (medium) or 5 (large) ports, preferring recently plugged USB devices. `comls` lists all.
- The widgets are available in English and Turkish. Set `language` in the settings to override
  the display language of Windows.

## Releases

There are no prebuilt packages yet. A release will need:

- `comls.exe` for x64 and ARM64
- A signed MSIX package of the widget provider, together with the public certificate (`.cer`)
  or signed with a certificate that Windows already trusts
- The Windows App SDK runtime packages the MSIX depends on
- Installation notes and checksums

Signed releases are not automated. The private key of a signing certificate never belongs in
this repository.

## Contributing

Contributions are welcome. Adding a USB serial chip is a one-line change; see
[CONTRIBUTING.md](CONTRIBUTING.md).

## License

This project is licensed under the [MIT License](LICENSE).

Developed by **Yigisoft**.

---

## Türkçe

WhichCOM, bilgisayara USB ile bağladığınız ESP32, Arduino gibi kartların hangi COM portuna
düştüğünü Aygıt Yöneticisi'ne girmeden, Windows 11 widget panosundan (<kbd>Win</kbd> + <kbd>W</kbd>)
görmenizi sağlar. Ayrıca Wi-Fi, Ethernet ve ses cihazı durumunu gösterir.

Proje geliştirme aşamasındadır. Şu anda `comls` komut satırı aracı kullanılabilir:

- `comls`: seri portları tablo olarak listeler
- `comls --json`: aynı listeyi JSON olarak verir
- `comls --latest`: en son takılan portun adını yazar
- `comls --match esp32`: takma ada veya çip tipine göre filtreler

Takma adlar `%LOCALAPPDATA%\WhichCOM\settings.json` dosyasında tutulur; örnek için
[settings.example.json](settings.example.json) dosyasına bakın.

Yigisoft tarafından geliştirilmiştir.
