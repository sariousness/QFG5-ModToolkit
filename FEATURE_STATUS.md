# Feature Status

Status reflects the application source and regression tests as of 2026-10-26. “Implemented” means the workflow exists in the UI/source; it does not imply support for every game file variant or complete in-game validation.

## Implemented workflows

| Feature | Status | Current scope |
| :--- | :---: | :--- |
| **SPK Archive** | Implemented | Scan, extract, and repack selected archives using the configured DATA and Mods paths. |
| **Model Extractor MDL** | Implemented | Export/import supported BMP textures and HAK mesh data. |
| **3D Viewer** | Implemented | Model preview, animation-assisted assembly, external texture overrides, transform controls, PNG screenshot, static GLB export, and optional FFmpeg recording. Model fidelity and asset compatibility vary. |
| **ANM Lab (Experimental)** | Prototype | Select MDL/ANM assets, map donor tracks to recipient parts, and preview/export recipient-sized ANMs. Raw track copying remains available; an experimental reference-calibrated mode restores recipient matrix scale and adapts translations relative to donor/recipient reference clips. Game-engine transform behavior and in-game results remain unvalidated. |
| **Pano Extractor IMG/NOD** | Implemented | Convert NOD/IMG room backgrounds to BMP and inject supported edits. |
| **Messages (Demo)** | Implemented; format coverage limited | Extract and rebuild supported QGM records with `.qgmmeta` metadata; repack the edited content into `CDN.SPK`. |
| **Audio Explorer** | Implemented; playback depends on system codecs | Browse extracted WAV/AUD and ID-named speech resources from `cda` and `hdn`, inspect supported title metadata and encoded resource IDs, keep persistent favorites, and play valid WAV resources in-app (Linux playback uses ffplay). Complete odd-length final chunks without padding are accepted; truncated data remains unplayable. SNC lipsync parsing is not included. |
| **Sprites Extractor GRA** | Implemented | Browse `cdn/GRA` and `hdn/gra`, decode previews, select column-major settings, export images/video, edit frames, and restore from backups. |
| **Painter** | Implemented | Image editing tools, selections, palette, PNG and animated GIF props with Cycle/Span GRA mapping, undo/reset, and supported injection paths for MDL, GRA, and panoramas. |
| **Save Editor** | Beta / limited validation | Inventory, character model/room fields, flags, and supported patches share a guarded load/save session. Patches includes a **Get Boat** action that only becomes available when the named `fRowBoat` flag is present and safely editable. The current bundled flag-name list does not contain `fRowBoat`, so this action remains unavailable until its mapping is validated. Global-map saves (room 0 or 900) show the map location while keeping room and character-placement edits unavailable. Flags support name/index search, checkbox and Ctrl-click multi-selection, shift-click bulk set, and bulk toggle. Unsupported structures disable affected edits rather than relying on guessed offsets. |
| **Settings** | Implemented | Configure DATA and Mods folders, save settings, launch the game, and restore originals from the game's backup folder. |
| **Scenes (Demo)** | Implemented | Browse extracted room views, preview panorama data with IMG angle metadata when available, automatically load room-matching FTR resources, and optionally overlay parsed FTR region polygons using IMG coordinate dimensions. Region semantics and full renderer behavior remain outside scope. |

## Validation and limitations

- Source-level regression tests cover save editing, GRA source-folder behavior, QGM repacking, model/texture UI behavior, and Audio Explorer navigation/parser wiring. The current full suite runs 83 tests; audio/navigation tests pass, while three existing save/model UI expectations fail (`test_character_model_selection_updates_spinning_preview` and two `test_model_texture_ui.py` assertions). This records test status, not proof of a user-visible defect. Run the suite with `python3 -m unittest discover -s tests` from the `QFG5Extractor` directory.
- Two user-reported in-game checks confirmed that an edited save flag appeared on the deeds screen. Broader flag, save-file, inventory, character model, and room coverage has not been established.
- Reverse-engineered formats can have variants not represented by the current checks. Back up archives, assets, and saves before editing, and verify results in-game.
- Video features require FFmpeg. The 3D viewer and Scenes (Demo) are approximations and can differ from game rendering.
- For format observations and explicitly unknown areas, see [re-notes.MD](re-notes.MD).

## Not implemented

| Feature | Status |
| :--- | :--- |
| **Unified Build Mod action** | Not present in the current source; archive repacking remains a separate workflow. |
| **Automatic updater** | Not present in the current source. |
| **Full game-format coverage** | Not implemented; formats such as RGD, ZZZ, ROM, STR, QGF, SNC/lipsync, and cutscene resources remain outside the demonstrated tool workflows. |
