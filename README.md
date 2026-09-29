# WhichCOM

**Which COM port is my ESP32 on?** WhichCOM answers that without opening Device Manager.

WhichCOM is a Windows 11 widget provider that shows your connected serial devices (ESP32, Arduino,
CP210x, CH340, FTDI…) on the widget board, along with your Wi-Fi, Ethernet and audio device status.
Open the widget board (<kbd>Win</kbd> + <kbd>W</kbd>) and see everything at a glance. It comes with
`comls`, a small command line tool for scripts.

<!-- Screenshot placeholder: docs/images/widgets.png -->

## Features

- **Serial Ports widget** (small / medium / large)
  - Lists connected COM ports with chip type detection
  - One-click copy of the port name
  - Marks the port you plugged in most recently
  - Nicknames for your boards (e.g. "Sensor board #2")
- **System Status widget** (small / medium / large)
  - Wi-Fi (network name, signal strength, IP address) and Ethernet (link speed, IP address)
  - Internet connectivity
  - Default audio output and input device
  - The large size also lists the serial ports, so one widget shows everything
- **`comls` command line tool**
  - Lists serial ports as a table or as JSON
  - Script friendly: `pio run -t upload --upload-port $(comls --latest)`

The widgets only collect data while the widget board is visible. When the board is closed,
WhichCOM does no work. It sends no data anywhere.

## Requirements

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) and PowerShell 7 (`pwsh`) to build from source
- Developer Mode, to install the widget package:
  **Settings > System > For developers > Developer Mode**

Visual Studio is not required.

## Build

```powershell
git clone https://github.com/halil158/WhichCOM.git
cd WhichCOM
dotnet build WhichCOM.slnx --configuration Release
dotnet test WhichCOM.slnx --configuration Release
```

## Install

All scripts are in the [scripts](scripts) folder.

### Quick: register without a certificate

```powershell
./scripts/Register-DevPackage.ps1
```

This builds the package and registers it for your user. Nothing is signed and no certificate is
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
4. Use the **…** menu of the widget to change its size, to customize or to unpin it.

### Uninstall

```powershell
./scripts/Uninstall-Package.ps1                      # keeps your settings
./scripts/Uninstall-Package.ps1 -RemoveSettings      # also deletes your nicknames
./scripts/Uninstall-Package.ps1 -RemoveCertificate   # also deletes the certificate
```

## comls

Installing the package puts `comls` on the `PATH` of every terminal. To use `comls` without the
widgets, build a single `comls.exe` and copy it to a folder that is on your `PATH`:

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

## Nicknames

A nickname belongs to a device, not to a port number:

- A device with a serial number is identified by that serial number.
- A device without a serial number is identified by its VID:PID together with the USB socket it is
  plugged into. `comls --json` prints this value as `locationPath`.

To set nicknames, open the **…** menu of a widget and choose **Customize widget**. The card lists
the connected devices; type a nickname and choose **Save**. An empty nickname removes it. The
card has room for 1 (small), 2 (medium) or 5 (large) devices.

Nicknames can also be written into the settings file by hand.

## Settings

Settings are shared by the widgets and `comls`. They are stored in
`%LOCALAPPDATA%\WhichCOM\settings.json`. The file is optional; see
[settings.example.json](settings.example.json) for every setting. Changes are picked up at the
next refresh.

| Setting | Default | Meaning |
| --- | --- | --- |
| `aliases` | empty | Nicknames |
| `language` | `"auto"` | `"auto"` follows the display language of Windows; `"en"` or `"tr"` |
| `showBluetoothPorts` | `false` | Also list Bluetooth serial ports |
| `useWifiApi` | `false` | Exact Wi-Fi name and signal percentage; Windows asks for location access |
| `newBadgeSeconds` | `120` | How long a freshly plugged port is marked as new |
| `refreshSeconds` | `4` | Refresh interval while the widget board is open |

To add chips that WhichCOM does not know yet, see [CONTRIBUTING.md](CONTRIBUTING.md). For private
additions, create `%LOCALAPPDATA%\WhichCOM\chips.json` with the same format as
[usb-serial-chips.json](src/WhichCOM.Core/Data/usb-serial-chips.json).

## Troubleshooting

| Problem | What to check |
| --- | --- |
| WhichCOM is missing in **Add widgets** | Close the board and open it again; the list is cached. If it is still missing, sign out and in again, or end the `Widgets` and `WidgetService` processes in Task Manager. |
| | `Get-AppxPackage Yigisoft.WhichCOM` must list the package. If not, the installation failed. |
| | Make sure **Widgets** is enabled in **Settings > Personalization > Taskbar** and that the "Windows Web Experience Pack" is up to date in the Microsoft Store. |
| The widget is pinned but stays empty or shows an error | Read `%LOCALAPPDATA%\WhichCOM\provider.log`. It records when the provider starts, when widgets are shown and hidden, and every failure. |
| | Check that the provider runs while the board is open: `Get-Process WhichCOM.WidgetProvider`. |
| A widget disappeared from the board | The log says `DeleteWidget` when a widget was unpinned. Add it again with **Add widgets**. |
| `Install-Package.ps1` says the signature is not trusted | Run `New-DevCert.ps1 -Trust` in an elevated PowerShell. |
| Installation fails with `0x80073CFB` or "a package with the same identity is already installed" | Run `./scripts/Uninstall-Package.ps1`, then install again. |
| The build fails with `PRI210 ... File move failed` | The widget host has a file of an older build open. Rename `resources.pri` in the build output folder, or end the `WidgetService` process, and build again. |
| `comls` is not found after installing the package | Open a new terminal. `%LOCALAPPDATA%\Microsoft\WindowsApps` must be on the `PATH`. |

## Known limitations

- The time a device was plugged in comes from Windows. Built-in ports count as plugged in at every
  boot, so `comls --latest` prefers USB devices.
- Many low-cost USB serial adapters have no serial number, or all share the same one. Their
  nickname then belongs to the USB socket, not to the adapter.
- Device descriptions are shown in the display language of Windows.
- A widget has a fixed height and cannot scroll. The Serial Ports widget shows up to 4 (small),
  3 (medium) or 5 (large) ports, preferring recently plugged USB devices. `comls` lists all.
- **Wi-Fi name and signal.** Windows treats the Wi-Fi network name as location data and asks for
  location access when an application reads it. WhichCOM does not do that by default. It shows
  the name of the network profile, which Windows creates from the network name, and the signal
  in bars (steps of 20%). Set `useWifiApi` to `true` in the settings for the exact name and
  percentage, and allow location access when Windows asks.
- Virtual network adapters (Hyper-V, VPN, VirtualBox, loopback, Bluetooth) are hidden. The
  System Status widget shows at most two network adapters, connected ones first.
- The **…** menu of a widget can open behind a neighbouring widget. The menu belongs to the
  widget board, not to WhichCOM. Moving the widget to another position helps.
- After the customization card was shown, the **…** menu of the widget may stop responding until
  the board is opened again. This is a known problem of the widget board.
- The widgets are available in English and Turkish. The names in the **Add widgets** list are
  English only.
- The package is not in the Microsoft Store. It has to be signed with a certificate that the
  computer trusts, or registered in Developer Mode.

## Releases

There are no prebuilt packages yet. A release will need:

- A signed MSIX package for x64 and ARM64. `comls` is part of the package.
- The public certificate (`.cer`) the package is signed with, unless Windows already trusts it
- `comls.exe` for x64 and ARM64, for use without the widgets
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

**Widget'lar**

- **Serial Ports:** bağlı COM portları, çip tipi, takma ad ve Kopyala düğmesi
- **System Status:** ağ, internet ve varsayılan ses cihazları; büyük boyutta COM portları da görünür

Widget'lar yalnızca pano açıkken veri toplar.

**Kurulum**

1. Geliştirici Modu'nu açın: Ayarlar > Sistem > Geliştiriciler için.
2. `./scripts/Register-DevPackage.ps1` komutunu çalıştırın.
3. Panoyu açın, **Widget ekle** ile WhichCOM widget'larını sabitleyin.

İmzalı paket kurmak için yukarıdaki "Full: signed package" adımlarını izleyin.

**comls**

- `comls`: seri portları tablo olarak listeler
- `comls --json`: aynı listeyi JSON olarak verir
- `comls --latest`: en son takılan portun adını yazar
- `comls --match esp32`: takma ada veya çip tipine göre filtreler

**Takma adlar**

Widget'ın **…** menüsünden **Widget'ı özelleştir** seçeneğiyle verilir. Takma ad cihaza bağlıdır;
port numarası değişse de kalır. Ayarlar `%LOCALAPPDATA%\WhichCOM\settings.json` dosyasında
tutulur; örnek için [settings.example.json](settings.example.json) dosyasına bakın.

Yigisoft tarafından geliştirilmiştir.
