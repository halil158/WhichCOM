# WhichCOM

**Which COM port is my ESP32 on?** WhichCOM answers that without opening Device Manager.

WhichCOM is a Windows 11 widget that shows your connected serial devices (ESP32, Arduino, CP210x, CH340, FTDI…) right on the widget board, along with your Wi-Fi, Ethernet and audio device status. Just hover over the widgets area on the taskbar and see everything at a glance.

> 🚧 **Work in progress.** This project is in early development. Features and instructions below may change.

## Planned features

- **Serial ports widget**
  - Lists connected COM ports with chip type detection (CP210x, CH340/CH9102, FTDI, ESP32 native USB, Arduino)
  - One-click copy of the port name
  - Custom nicknames for your boards (e.g. "Sensor board #2")
- **System status widget**
  - Wi-Fi (SSID, signal strength, IP) and Ethernet (link speed, IP) status
  - Internet connectivity
  - Default audio input/output devices
- **`comls` CLI**
  - List serial ports from the terminal
  - Script-friendly output, e.g. `pio run -t upload --upload-port $(comls --latest)`

## Requirements

- Windows 11
- Developer Mode enabled (for sideloading the widget package)
- .NET 8 SDK (for building from source)

Build and installation instructions will be added as the project progresses.

## Contributing

Contributions are welcome! Adding support for a new USB-serial chip will be as simple as adding its VID/PID to a JSON table. A contribution guide is coming soon.

## License

This project is licensed under the [MIT License](LICENSE).

Developed by **Yigisoft**.

---

## Türkçe

WhichCOM, bilgisayara USB ile bağladığınız ESP32, Arduino gibi cihazların hangi COM portunda olduğunu Aygıt Yöneticisi'ne girmeden, Windows 11 widget panosundan görmenizi sağlayan bir araçtır. Ayrıca Wi-Fi, Ethernet ve ses cihazı durumunu da gösterir. Proje geliştirme aşamasındadır.