# QFG5Extractor

QFG5Extractor is a suite of reverse-engineered C# extraction tools for parsing, extracting, and modifying bulk assets from the classic Sierra game *Quest for Glory V: Dragon Fire*. This toolkit enables automated, batch capabilities for `.SPK` game archives, `.MDL` (Hakenberg & BMPs) meshes, and panorama backgrounds directly through a unified, cross-platform Windows Forms UI.

## Features
- **SPK Tab:** Supports parsing nested archive structures within `.SPK` files. Supports multiple file selections and iterating over an entire origin folder automatically via `Batch Output`.
- **Model Tab:** Transpiles native QFG5 `.mdl` models dynamically into open `.hak` files.
- **MDL Viewer (OpenTK):** A hardware-accelerated 3D viewer supporting skeletal animation assembly, multi-texture rendering, and external texture overrides.
- **Msg & Pano Tabs:** Converts legacy message blocks and `.nod` panorama backgrounds into modern image formats dynamically.

## Changelog
See [CHANGELOG.md](CHANGELOG.md) for a full history of changes.

## Building and Running

### Prerequisites
- .NET Framework 4.5+ (Windows)
- Mono Runtime (Linux / Mac OS)

### Compiling on Linux/Mac
Ensure `mono-devel` or your distro's equivalent is installed with the MSBuild engine:
```bash
sudo apt install mono-complete
cd src
msbuild QFG5Extractor.csproj  # or use `xbuild` on deprecated Mono versions
mono bin/Debug/QFG5Extractor.exe
```

### Logs
The application provides real-time transaction debugging across all batches. If a parsing operation encounters anomalies, the suite gracefully ignores the corrupted mesh nodes without crashing and explicitly details the warning logs in `<Working_Directory>/qfg5extractor.log`, which can be cross-referenced natively inside the GUI by clicking **View Log**.
