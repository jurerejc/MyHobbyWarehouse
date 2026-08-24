# MyHobbyWarehouse

Component inventory & project BOM manager for electronics hobbyists and makers.
Track components (SKU, stock, location, suppliers, datasheets), organize them into
projects/BOMs, check stock, build PCBs (deduct stock), and generate purchase
orders for missing parts.

> Built with .NET 8 (WPF).

## Features

- **Component library** — manage parts with images, locations, categories,
  suppliers and prices.
- **Locations** — rack/zone based storage with thumbnails.
- **Projects & BOM** — import Eagle CSV/ODS, check stock, build PCBs
  (auto stock deduction), and **create orders** for missing parts.
- **Export** — BOM to XLSX, library to CSV/XLSX.
- **Self-contained** — the installer bundles everything; **no .NET install needed**.

## Download & Install

Download the latest `MyHobbyWarehouse-setup-<version>.exe` from
[GitHub Releases](https://github.com/jurerejc/EagleManager/releases) and run it.
It installs to `C:\Program Files\MyHobbyWarehouse` with Start Menu and Desktop
shortcuts. On first launch it asks for the database location (e.g. your
OwnCloud / network folder).

## Build from source

```powershell
# Restore & build
dotnet build MyHobbyWarehouse/MyHobbyWarehouse.csproj -c Release

# Build a self-contained single-file installer (requires Inno Setup 6)
.\build-setup.ps1
```

The resulting `installer/MyHobbyWarehouse-setup-<version>.exe` is ready to
distribute.

## Support the project

If this tool is useful to you, consider buying me a coffee — it helps keep the
project alive and ad-free:

[![Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/cendof)

**☕ [Donate on Ko-fi](https://ko-fi.com/cendof)**
