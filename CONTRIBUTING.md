# Contributing to WhichCOM

Thank you for helping. This guide is short on purpose.

## Set up

```powershell
git clone https://github.com/halil158/WhichCOM.git
cd WhichCOM
git config core.hooksPath .githooks
winget install Gitleaks.Gitleaks
dotnet test WhichCOM.slnx
```

You need Windows 11 and the .NET 10 SDK. Visual Studio is optional.

## Add a USB serial chip

The chip table is [src/WhichCOM.Core/Data/usb-serial-chips.json](src/WhichCOM.Core/Data/usb-serial-chips.json).

1. Plug the device in and find its IDs:

   ```powershell
   comls --json
   ```

   Look at `vid` and `pid` of your port, e.g. `"vid": "1A86"`, `"pid": "7523"`.

2. Add one line to the `chips` list:

   ```json
   { "vid": "1A86", "pid": "7523", "label": "CH340", "vendor": "WCH", "tags": [ "ch34x" ] }
   ```

   | Field | Meaning |
   | --- | --- |
   | `vid` | USB vendor ID, four hex digits |
   | `pid` | USB product ID, four hex digits, or `"*"` for every product of the vendor |
   | `label` | Short name shown in the widget. Keep it under 20 characters |
   | `vendor` | Optional. Manufacturer of the chip |
   | `tags` | Optional. Lower-case search words for `comls --match` |

   An entry with an exact `pid` wins over a `"*"` entry of the same vendor.

3. Add the IDs to a test in
   [tests/WhichCOM.Core.Tests/ChipDatabaseTests.cs](tests/WhichCOM.Core.Tests/ChipDatabaseTests.cs)
   and run `dotnet test WhichCOM.slnx`.

4. Open a pull request. Mention the board or adapter you tested with.

Label the chip, not the board. Many boards share a chip: a CP210x can be an ESP32 development
board or a plain USB adapter. Users give their boards a nickname for that.

## No secrets, no personal data

This repository is public. Do not commit:

- Certificates or private keys (`.pfx`, `.p12`, `.key`, …), passwords, tokens
- Paths that contain a user name, computer names
- Real serial numbers, MAC addresses, IP addresses, Wi-Fi network names
- Your own `settings.json`; change `settings.example.json` instead

Use made-up values in tests and documentation:

| Kind | Use |
| --- | --- |
| Serial number | `TESTSERIAL0001` |
| MAC address | `AA:BB:CC:DD:EE:FF` |
| IP address | `192.0.2.10` |
| E-mail address | `someone@example.com` |

The pre-commit hook scans staged changes with [gitleaks](https://github.com/gitleaks/gitleaks), and
the same scan runs for every push. To scan everything by hand:

```powershell
./scripts/Scan-Repo.ps1
```

If gitleaks reports a false positive, prefer changing the value to one from the table above. If
that is not possible, extend the allow list of the rule in [.gitleaks.toml](.gitleaks.toml) and
explain why in the pull request.

## Project layout

| Folder | Content |
| --- | --- |
| `src/WhichCOM.Core` | Port reading, device ID parsing, chip table, settings, card data. No dependency on the widget API |
| `src/WhichCOM.Cli` | `comls` |
| `src/WhichCOM.WidgetProvider` | COM server for the widget board, card templates, package manifest, images |
| `tests/WhichCOM.Core.Tests` | Unit tests |
| `tools/WhichCOM.CardPreview` | Draws the cards with example data in a web page |
| `scripts` | Certificate, package, install, preview and scan scripts |

## Change a widget

A widget is drawn from two parts: a template and data.

- Templates are Adaptive Cards in `src/WhichCOM.WidgetProvider/Templates`, one per widget size.
  Use theme colors (`accent`, `good`, `attention`, `isSubtle`) and never fixed colors, so that
  the cards work in light and dark mode.
- Data is built in `src/WhichCOM.Core/Cards`. Texts are in `CardStrings.cs`, in English and
  Turkish; add new texts to both.

Design rules:

- Every row looks the same: an icon, a bold title with a small subtle detail below, and at most
  one short status text at the end.
- Sections are divided by a separator line, not by headings.
- No buttons in rows. A row that does something is clickable as a whole (`selectAction`).
- Widgets cannot scroll and the large widget is only about 470 pixels high. When you add rows,
  check the capacity constants in the card builders.

To see a change:

1. `./scripts/Preview-Cards.ps1` draws all cards with example data in the browser, in both
   themes and languages. A red line marks a card that is taller than its widget.
2. `dotnet test` expands every template with real card data.
3. `./scripts/Register-DevPackage.ps1` puts the change on the widget board. The preview is an
   approximation, so look at the real widget in all its sizes before you finish.

## Code

- Comments and commit messages are in English.
- Code that reads ports or parses device IDs belongs in `WhichCOM.Core` and needs unit tests.
  The widget provider and `comls` share it.
- The provider must do no work while the widget board is closed. Read data only between
  `Activate` and `Deactivate`.
- Do not call APIs that make Windows ask the user for a permission, unless the user opted in
  through a setting.
- Package versions are pinned in [Directory.Packages.props](Directory.Packages.props).
- The build treats warnings as errors.
