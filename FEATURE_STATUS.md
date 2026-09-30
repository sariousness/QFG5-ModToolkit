# Feature Status Report (v2.0 Prep)

This document tracks the current implementation status of the QFG5Extractor suite as we approach the v2.0 stable release.

## 🟢 Fully Operational

| Feature | Status | Description |
| :--- | :---: | :--- |
| **SPK Extraction** | Done | Full support for nested archives and batch output. |
| **SPK Repacking** | Done | Rebuilds SPK files from folders with correct headers and offsets. |
| **Panorama Converter** | Done | Converts `.nod` files into standard image formats with previews. |
| **Message Parser** | Done | Extracts game text and message blocks into readable formats. |
| **Multi-Texture MDL** | Done | Parses and renders all sub-bitmaps within MDL files. |
| **90° Step Rotation** | Done | Matrix-based X/Y rotation without breaking coordinate frames. |
| **Texture Overrides** | Done | Scans and applies external textures with prefix matching. |

## 🟡 Beta / Functional (Needs Fine-Tuning)

| Feature | Status | Description |
| :--- | :---: | :--- |
| **Skeletal Assembly** | Beta | Assemblies submeshes using ANM matrices; some clipping/offsets remain. |
| **Auto-Animation Match** | Beta | Matches MDLs to potential ANMs using the persistent database. |
| **UI Layout (v2)** | WIP | Grouped sections in place; some scaling/clipping fixes ongoing. |
| **Save Editor** | WIP | Basic parsing works; UI integration and value modification pending. |

## 🔴 Upcoming / Planned

| Feature | Status | Description |
| :--- | :---: | :--- |
| **GLTF Export** | Planned | Export assembled character models for use in modern 3D engines. |
| **Full Repack Suite** | Planned | Unified "Build Mod" button to repack all asset types at once. |
| **Auto-Updater** | Planned | Check for newer tool versions directly from the UI. |

---
*Last Updated: 2026-03-03*
