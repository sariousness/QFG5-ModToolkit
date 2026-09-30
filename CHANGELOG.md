# Changelog

All notable changes to this project will be documented in this file.

## [2.2.0-beta] - 2026-03-03

### Added
- **Modern UI Overlay:** Preview controls (Rotate, Reset, Spin, Exploded View) are now icons/buttons overlaid directly on the 3D viewport for a cleaner, more focused interface.
- **Enhanced Tooltips:** Hovering over the overlay buttons provides clear descriptions of their functions.

### Fixed
- **Deep Texture Loading:** Fixed a bug where multi-texture models (like 115.mdl and 133.mdl) only loaded the first texture. Now uses the internal bitmap offset table.
- **Upright Default Rotation:** Adjusted default rotation to 270° X-axis to ensure character models stand upright immediately upon loading.

## [2.1.0-beta] - 2026-03-03

### Added
- **3D View Panning:** Added support for panning the camera in the 3D viewport using **Right-Click + Drag**.
- **Enhanced View Controls:** Swapped rotation buttons to prioritize the X-Axis (standing up models) as the primary toggle.

### Fixed
- **Default Rotation Axis:** Corrected the default 90° offset to apply to the X-axis by default, ensuring most models load upright immediately.
- **Reset Logic:** "Reset All" now correctly centers the model and returns it to the upright default orientation.

## [2.0.0-beta.2] - 2026-03-03

### Fixed
- **Default Model Orientation:** Models now load upright (90° X rotation) by default, fixing the "sideways" viewing issue.
- **Reset Logic:** Reset All now returns models to the correct upright orientation.

### Added
- **Project Structure:** Restored original research scripts to the scratch directory.

## [2.0.0-beta] - 2026-03-03

### Added
- **Hardware-Accelerated MDL Viewer:** Replaced GDI+ rendering with OpenTK OpenGL for high-performance 3D visualization.
- **Character Skeleton Assembly:** Support for loading `.ANM` files to automatically assemble character submeshes into their correct skeletal pose.
- **Multi-Texture Support:** MDL files with multiple embedded bitmaps are now correctly parsed and rendered on a per-face basis.
- **Advanced Model Transforms:** 
  - Added 90-degree step rotations for X and Y axes.
  - Added UV rotation (90°, 180°, 270°) to fix misaligned textures.
  - Added "Exploded View" toggle to separate overlapping submeshes.
- **Texture Overrides:** Support for loading external `.bmp` and `.png` textures from a folder, with auto-matching based on model prefixes.
- **Persistent Model Database:** Added `model_db.json` sync to track compatible animations and submesh metadata across sessions.

### Changed
- **UI Refinement:** Grouped viewer controls into logical sections (Orientation, Matched Assets, Utilities) for a cleaner workspace.
- **Improved Rotation Logic:** Moved rotation handling to global matrices to prevent "breaking" models during orientation adjustments.
- **Mono Compatibility:** Optimized OpenTK and WinForms integration for stable performance in Wine/Linux environments.

### Fixed
- Fixed "Closed Stream" bug that caused white textures on certain models.
- Resolved "Invalid bitmap header address" errors by implementing more robust MDL section parsing.
- Corrected Z-clipping issues that caused `OutOfMemoryException` during complex renders.

## [1.0.1] - 2026-02-20

### Added
- Added SPK repack support to rebuild `.spk` archives from extracted/modded folders.
- Added a new **Repack SPK** action in the SPK tab UI.
- Internal SPK parsing logic consolidated for reuse between extraction and repack paths.
