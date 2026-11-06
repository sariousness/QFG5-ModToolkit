# QFG5Extractor

QFG5Extractor is a Windows Forms toolkit for exploring and modifying resources from *Quest for Glory V: Dragon Fire*. It provides extraction, conversion, preview, editing, and repacking workflows for several game formats. The formats are reverse-engineered, so support and in-game compatibility can vary by file and game release.

## Features

- **SPK Archive:** Scan, extract, and repack selected `.SPK` archives. Repacked files are written under `Mods/repacked`.
- **Model Extractor MDL:** Export embedded textures as BMP and meshes as HAK, or import edited BMP/HAK data into MDL files.
- **3D Viewer:** Inspect models with animation-assisted submesh assembly, multiple embedded textures, external texture overrides, rotation and camera controls, PNG screenshots, and static GLB export. Live viewport recording is available when FFmpeg is installed.
- **Pano Extractor IMG/NOD:** Convert NOD/IMG room backgrounds to BMP, edit them, and inject the result back into the matching panorama data.
- **Messages (Demo):** Extract QGM text with metadata sidecars, edit and rebuild supported message files, and repack `CDN.SPK`. Unedited message records are preserved.
- **Audio Explorer:** Browse extracted WAV/AUD files from `cda` and `hdn`, play supported RIFF/WAVE audio in the app, inspect title metadata and QFG5 speech IDs, and keep a persistent favorites list.
- **Sprites Extractor GRA:** Browse resources in both `cdn/GRA` and `hdn/gra`, preview animations, export images/video, edit a frame, and inject it back into its source. Supports favorites and column-major decoding settings.
- **Painter:** Edit standalone images and textures passed from MDL, GRA, and panorama screens. Includes selections, drawing tools, PNG and animated GIF props, undo/reset, and supported MDL/GRA/panorama injection workflows. GIF props can be cycled or spanned across GRA frames, and a prop’s placement/scale/rotation can be tuned per frame so it follows the sprite motion.
- **Save Editor:** Edit inventory, selected character model and room fields, and named flags through a shared save session. The Patches screen includes a guarded **Get Boat** action when the `fRowBoat` flag is present in the flag-name catalog. Other screenshot options are not applied when they require game-wide patches or lack a confirmed save-field mapping. Flags can be searched, selected with checkboxes or Ctrl+click, bulk-set with Shift+click, and toggled together. Save parsing and edits are guarded by structural checks; not every save or field has been verified in-game.
- **Scenes (Demo):** Browse extracted room views, preview panoramas with IMG angle metadata where available, and optionally overlay matching FTR room regions.
- **Settings:** Configure the game `DATA` directory and a separate Mods/extraction directory; launch the game or restore files from the game's `backup` folder.
- And more..

The Save Editor is functional but remains a limited, reverse-engineered editor rather than a complete save-format implementation. Inventory records are read according to the save's declared count, including counts above 120; changing the number of inventory slots is disabled because it would shift subsequent save data.

## Data folders

Set the game's `DATA` folder itself in **Settings**. It may contain archives, extracted `cdn`/`hdnw` folders, or both. Choose a separate writable Mods folder; extracted and edited assets are organized beneath matching paths such as `cdn/GRA`, `cdn/QGM`, `cdn/NOD`, `cdn/IMG`, `cda`, and `hdnw/mdl`. Repacked archives are written beneath `Mods/repacked`.

For message edits, retain the blank-line paragraph structure in the generated text file and its `.qgmmeta` sidecar. For panorama and GRA edits, use the matching source archive when repacking. Keep independent backups of game files and saves before testing any changes.

See the [User Guide](USER_GUIDE.md) for setup, screen details, workflows, backups, and limitations. The [Feature Status](FEATURE_STATUS.md) records implementation and validation status. [Reverse-engineering notes](re-notes.MD) are research observations, not a complete format specification.

## Build and run

### Requirements

- Windows 7 or later: .NET Framework 4.0 or later. The UI and icon images are embedded in the application; the app extracts them to the current user's Local AppData on first launch.
- The Windows release is built as x86 and runs on both 32-bit and 64-bit Windows.
- Linux/macOS: a compatible Mono runtime and MSBuild toolchain. Windows Forms behavior depends on the installed runtime and desktop environment.
- FFmpeg on `PATH` (or beside the application) for GRA video export and live viewer recording; Linux Audio Explorer playback also uses `ffplay`.

The project targets .NET Framework 4.0 and defines x86 and x64 build configurations. On Linux with Mono:

```bash
cd src
msbuild QFG5Extractor.csproj
mono bin/Debug/QFG5Extractor.exe
```

Use `xbuild` only if MSBuild is unavailable in the installed Mono toolchain.

### Windows release

Tagged releases (`v*`) publish both a portable ZIP and a Windows installer on GitHub. Both require the .NET Framework 4.0 or later runtime; the installer checks for it but does not bundle it. If needed, install it from [Microsoft's .NET Framework download page](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net40) first. On Windows 7, .NET Framework 4.0 is supported; later .NET 4.x versions are in-place upgrades.

The portable ZIP can be extracted to any writable folder and launched with `QFG5Extractor.exe`. The installer creates a per-user installation and Start Menu shortcut.

## Tests

The repository includes Python source-level regression tests for save editing, GRA source-folder handling, QGM repacking, and model/texture UI behavior:

```bash
python3 -m unittest discover -s tests
```

These tests do not replace testing against real game archives, saves, or in-game behavior.

## Logs

The application writes `qfg5extractor.log` in its working directory. Use the **View Log** action in the app when available to inspect diagnostic output.

## History

See [CHANGELOG.md](CHANGELOG.md) for release history.
