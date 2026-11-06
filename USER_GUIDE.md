# QFG5Extractor User Guide

QFG5Extractor is a Windows Forms toolkit for extracting, previewing, editing, and repacking selected *Quest for Glory V: Dragon Fire* resources. Its file-format support is reverse-engineered; keep backups and test edited files before using them in a live game.

## Before you start

### Requirements

- A copy of the game and access to its `DATA` folder.
- Windows with .NET Framework 4.5 or later, or a compatible Mono runtime on Linux/macOS.
- FFmpeg on `PATH` (or beside the application) for GRA video export and 3D Viewer recording. On Linux, `ffplay` is also used for Audio Explorer playback.

See [README.md](README.md#build-and-run) for build and run instructions.

### Configure folders

1. Open **Settings**.
2. Set **Game DATA Folder** to the `DATA` directory itself, not the game root. The folder can contain `.SPK` archives, extracted `cdn`/`hdnw` trees, or both.
3. Set **Mods / Extracted Data** to a separate writable folder. The default is `Documents/QFG5 Mods`.
4. Enable **Save Settings on Change**, or click **Save Settings**.

Tools organize extracted and modified assets under corresponding folders in Mods, for example `cdn/GRA`, `cdn/QGM`, `cdn/NOD`, `cdn/IMG`, `cda`, `hdn`, and `hdnw/mdl`. Repacked archives are placed in `Mods/repacked`. Do not point Mods at the original game folder.

## Navigation

The navigation menu contains:

1. **SPK Archive**
2. **Model Extractor MDL**
3. **Pano Extractor IMG/NOD**
4. **Messages (Demo)**
5. **Sprites Extractor GRA**
6. **3D Viewer**
7. **Painter**
8. **Save Editor**
9. **Settings**
10. **Scenes (Demo)**
11. **ANM Lab (Experimental)**
12. **Audio Explorer**

Use the menu toggle to collapse or expand navigation. Most tools load available files when opened; use their scan, reload, or extract action after changing folders. Read the status area and `qfg5extractor.log` when an operation fails.

## Tool screens

### SPK Archive

- Click **Scan** to find `.SPK` archives in the configured `DATA` folder.
- The audit summary checks every listed archive for entries missing from its matching Mods folder and counts candidate extensions outside the app's known dedicated workflows. Click **Audit** to refresh the check and **Audit Details** to see the exact archive and entry paths; use its filter to search filenames and archive names.
- “Candidate” does not mean an entry failed to extract: SPK extraction copies entries as raw files, and the audit cannot determine whether an unknown format contains an asset the game uses. The known workflow extensions are GRA, MDL, ANM, IMG, NOD, QGM, and FTR.
- Select archives and click **Extract**. **All** and **None** change the selection.
- Extraction follows the paths stored in the archive and writes beneath matching Mods folders.
- Click **Repack All** to rebuild archives from corresponding edited files in Mods.
- Enable **Uppercase filename** if the generated archive should use uppercase naming.

Install generated archives into the game's `DATA` folder using your normal mod workflow. Keep an untouched copy of any archive you plan to replace.

### Model Extractor MDL

Choose an operation and click **RUN**:

- **Export BMP (MDL -> BMP):** export embedded model textures.
- **Import BMP (BMP -> MDL):** inject supported edited textures.
- **Export HAK (MDL -> HAK):** export mesh geometry.
- **Import HAK (HAK -> MDL):** import supported edited geometry.

Select an exported BMP to preview it. **List** and **Thumbs** change the output view. Source and destination paths come from Settings.

### Pano Extractor IMG/NOD

1. Click **Extract** to create BMP previews from available NOD/IMG panorama data.
2. Select a panorama and click **Edit Selected** to open it in Painter, or edit a BMP externally.
3. Click **Inject BMP** to write the edited image back to its matching panorama files under Mods.
4. Repack the corresponding archive in **SPK Archive** and install the result into the game's `DATA` folder.

Injection targets the matching NOD/IMG files. Keep the original files backed up and verify the result in-game.

### Messages (Demo)

1. Click **Extract All** to create editable text files under `Mods/cdn/QGM`.
2. Edit a text file and click **Save Changes**.
3. Click **Repack All** to rebuild changed QGM files and produce `Mods/repacked/CDN.SPK`.

Keep the blank-line paragraph boundaries unchanged and retain each `.qgmmeta` sidecar beside its text file. The repacker rejects ambiguous boundaries, unsupported text characters, and unsupported data rather than guessing. Unchanged records are retained byte-for-byte; edited text records are rebuilt using the supported plain-text representation.

### Audio Explorer

- The tab scans the configured game `DATA` folder and Mods/extracted-data folder for `.WAV`, `.AUD`, and ID-named speech resources. The original files are read-only; refresh after extracting or changing folders.
- Select a resource to inspect its full path, RIFF/WAVE chunk identifiers, format tag, channel/sample-rate details, and optional `INFO` name/artist/comment fields.
- The list shows clip duration when available, optional header names, and recognized resource IDs alongside each filename. QFG5 speech filenames encode the QGM ID and four base-36 resource IDs; recognized IDs are shown in both their encoded and numeric forms.
- Use the star button to favorite the selected resource; favorites persist between runs and can be filtered with **Favorites only**.
- Header titles are checked in RIFF `INFO/INAM`, Broadcast Wave `bext` descriptions, CART titles, and ID3 title frames. Other recognized `INFO` fields are shown in the selected-resource details.
- Click **Play** and **Stop** to audition valid RIFF/WAVE files in the app. Linux playback uses `ffplay` (from FFmpeg) to avoid dependence on Mono's ALSA/PulseAudio plugin; it must be available on `PATH` or beside the app. Windows playback uses the system audio support. AUD resources are WAV-formatted in the reverse-engineering notes, but playback still depends on codec support. Files whose declared chunk data exceeds the available bytes remain unplayable; a complete final odd-length chunk without its pad byte is accepted.
- SNC lipsync resources are not included in this browser.

### Sprites Extractor GRA

- The browser scans both `cdn/GRA` and `hdn/gra`; same-named resources in different folders remain separate.
- Switch between **List** and **Thumbs** views. Multi-select with Shift/Ctrl where supported.
- Select a resource to preview it; use **Play Anim** to play or pause multi-frame sprites.
- **CM** toggles column-major decoding. **Auto CM** applies the bundled reviewed list to selected files, or all visible files if none are selected. **Save CM List** stores the current choices for later launches.
- Use favorites and the favorites filter to organize resources.
- Choose PNG/MP4 export options, then click **Extract Selected**. FFmpeg is required for video output.
- Click **Edit Graphic** to send the selected frame to Painter. Hold Shift while placing a prop to apply it across frames when editing a GRA animation.
- GRA injection preserves the source file's other data and is limited to the original frame dimensions and available encoded space. A one-time `.qfg5extractor.bak` backup is kept beside the source; **Restore** restores from that backup without deleting it.

After editing a file beneath Mods, repack the archive corresponding to its source folder, for example `CDN.SPK` for `cdn/GRA`.

### 3D Viewer

- Search models by filename or name; use favorites to narrow the list.
- Select a model to inspect it. The viewer can assemble submeshes with a matching ANM when available.
- The animation dropdown shows each ANM filename and its embedded animation name when available.
- The viewer checks animation bone count, not the animation's original model. Renamed or cross-model ANMs may move the mesh parts into incorrect positions; use matching model/animation files.
- Drag to orbit, use the mouse wheel to zoom, and middle-drag to pan.
- Use the view controls to reset, rotate by 90 degrees on X/Y, toggle auto-spin, show an exploded view, or display axes.
- Use **Edit Texture** to send a model texture to Painter.
- Save a viewport screenshot as PNG, export a static GLB, or **Record MP4** with FFmpeg.
- **Sync DB** refreshes the local model/animation compatibility database.

The viewer is an inspection aid, not the game's renderer. Some models may not have a matching animation, and assembled poses or exported appearance can differ from the game.

### ANM Lab (Experimental)

This isolated prototype creates a new recipient-sized ANM by mapping donor tracks to recipient part slots. The donor and recipient MDL lists show filenames with embedded model names, and ANM lists show embedded animation names. Choose the donor MDL/target ANM and recipient MDL; the target lists are filtered to the selected model's numeric filename prefix. The recipient pose list offers **None — identity pose** and is used for static mappings.

**Preserve recipient scale / translation** is enabled by default. Choose a donor reference ANM and recipient reference ANM from the controls above the previews (for example, Magnum's original PUSH and Gort's original PUSH while SWIM is the selected target). The references may contain different frame counts; the tool averages each track's translation and per-axis matrix scale over its reference clip. For mapped parts, the target donor rotation is retained, the recipient reference scale is restored, and target translation motion relative to the donor reference average is scaled by the recipient/donor average scale ratio and added to the recipient reference average. This is an experimental approximation, not confirmed game-engine behavior. Disable the option to compare against raw donor-track copying. Missing or invalid references block scale-preserving preview/export and are reported in the status area. Static mappings continue to use the recipient pose (or identity) rather than donor calibration.

Separate donor and recipient previews show the selected models; selecting a mapping row outlines the recipient part and its mapped donor part in orange. Click and drag to rotate, use each preview's **Spin On/Off** and **Play/Pause** controls, and zoom with **+**/**-** or the mouse wheel. The recipient preview updates when mappings, offsets, mode, or reference selections change. Click **Refresh lists** after adding assets, then **Load parts** to build the mapping. Exact part names are preferred; common body-part terms are suggested only when a clear match exists. Opposite-side or ambiguous matches remain static. One donor track can drive multiple recipient parts.

The **X deg**, **Y deg**, and **Z deg** columns apply per-part orientation offsets from -180 to +180 degrees and are included in preview and export. **Estimate offsets** uses the selected reference clips in scale-preserving mode; they must be synchronized clips with matching frame counts for this separate orientation-estimation operation. With raw-copy mode, it uses the selected donor animation and recipient pose ANM. Review offsets and preview output before transplanting. After export, the recipient preview switches to the generated ANM.

Click **Transplant** to write the selected animation, or **Transplant all** to process every ANM matching the donor model's filename prefix. Both ask for an output folder and use the recipient model prefix followed by the next available numeric sequence; existing filenames are skipped and files are created without overwrite. **Export new ANM...** remains available when you want to choose a custom output filename. Reusing one donor track for multiple recipient parts is supported; combining multiple donor tracks into one recipient track is not. Verify generated animations in the 3D Viewer and keep backups.

### Painter

Painter can open BMP, PNG, or JPG images, extract an embedded MDL texture, or receive images from the GRA and panorama extraction screens.

1. Use **Open Image** or **Open MDL**.
2. Choose a toolbox action: Pencil, Eraser, Fill, Pick Color, Magic Wand, Select Color, Lasso Select, Rectangle Select, or Clear Selection.
3. Choose a color with the active-color swatch or the RGB555 game-color palette.
4. Set zoom and brush size in the context bar. Use **Hue/Saturation** on the image or selection, or **Add Overlay** to composite a PNG.
5. Use **Undo** to reverse an edit and **Reset Original** to restore the loaded snapshot on the canvas.
6. Use **Save As** for a standalone BMP/PNG, or **Inject to MDL**, **Inject to GRA**, or **Inject to Pano** when the editor was opened from a supported source.

When zoomed in, hold Space and drag, or use middle-mouse drag, to pan. Open **Props**, select a sticker, and click the canvas to place it. Scroll over the canvas to resize the selected prop; Shift+scroll changes zoom. Custom transparent PNGs and animated GIFs can be added to the prop gallery. GIFs animate in the placement preview; a normal click paints the currently previewed frame. For GRA animations, Shift-click applies the prop across frames. Choose **Cycle** to repeat GIF frames in order, or **Span** to map the GIF's first and last frames across the GRA animation. Each GRA frame keeps its own per frame prop placement, scale, and rotation so a prop can be adjusted to match the sprite’s motion and animation from frame to frame.

MDL, GRA, and panorama injection each have format and dimension constraints. Supported injections keep a one-time `.qfg5extractor.bak` beside the source. Do not overwrite the only copy of an original asset.

### Save Editor

The Inventory, Character, Flags, and Patches screens share one loaded save and one **Save** action. Work on a separate test copy of a `.QG5` file.

#### Inventory

- The table shows item location, file offset, item ID, name, and amount.
- Edit item IDs and amounts in the table. Known names come from the bundled item catalog.
- Switch between list and icon views. Search or filter the categorized item catalog, and drag a catalog item onto an inventory entry to replace it.
- Right-click a row for duplicate, insert, insert-empty, or delete actions where available.
- The editor checks the supported inventory range and count before saving.

#### Character

- Choose a model by ID or name. The preview updates as the choice changes; click **Apply Model** to stage it.
- Choose a location by room ID or name. The room preview uses an extracted panorama when available; click **Apply Room** to stage the change.
- Drag the splitters to resize the details, room preview, and model preview panes.
- Coordinates, rotation, size, and save-file size are shown for inspection; rotation and size are read-only.
- The model preview can use a matching MDL and ANM to show a static pose. It does not play animation.

Room previews are approximate. Model, room, and animation combinations have not all been verified in-game and may be unsupported.
When a save is on the global map (room 0 or 900), the editor can display that location. Some map saves still contain readable character model and placement data, but the map location has no room payload to edit, so **Apply Room** remains unavailable.

#### Flags

- Click a flag's value to toggle it.
- Search flag names or indices in the search field.
- Check flags in the **Select** column or use Ctrl+click to select multiple rows. **Toggle Selected** inverts the checked and highlighted flags.
- Shift-click a flag's value to set the checked flags to the value you just clicked (True or False).
- The tool locates the packed flag array by parsing supported variable-length save sections; flags use low-bit-first packing (eight flags per byte).
- If the save cannot be parsed safely, values show **Unknown** and editing is disabled instead of guessing an offset.

Two user-reported in-game checks confirmed an edited flag on the deeds screen. That is encouraging but does not establish coverage for every flag or save.

#### Patches

The screen currently offers **Get Boat**, which stages the `fRowBoat` flag in the loaded save. Click **Save**, then leave the current scene in-game for the game to trigger the rowboat. This is based on the reported flag behavior and has not been independently verified in-game.

The action is enabled only when `fRowBoat` is present in the loaded flag-name catalog and the save's flag data is safely editable. The bundled catalog currently does not contain `fRowBoat`, so the button remains disabled rather than changing the similarly named `fBoat` flag. The other options shown in the customization screenshots are not included unless they can be mapped to supported save data; game-wide executable/configuration patches are outside this screen's scope.

#### Save and backups

- Before the first overwrite, the editor creates a neighboring `.bak` file if one does not already exist. Existing backups are not replaced.
- The editor checks for external changes since load and verifies that the saved file length is unchanged.
- If structural validation fails or editing is disabled, do not force a write; load a complete backup or a fresh in-game save.
- A structurally valid save is not guaranteed to be accepted by the game. Keep a separate original copy and test edited saves before relying on them.

### Settings

Set the game `DATA` and Mods folders here. Settings can be saved automatically or with **Save Settings**.

- **START GAME** launches `QFG5.exe` from the parent of the configured `DATA` folder. On Unix-like systems it invokes `wine`.
- **RESTORE ORIGINALS** restores files from a `backup` folder in the game directory after confirmation. Use only if you intend to replace modded files with those backups.

### Scenes (Demo)

The screen lists extracted room views, shows known room names when available, and previews panorama images using matching IMG angle metadata when available.

- Drag to pan and scroll to zoom. Camera movement is constrained to the available panorama.
- Enable **FTR overlay** to draw room-region polygons. Scenes loads FTR files from the extracted `cdn/FTR` folder (and legacy loose files directly under `cdn`), then lists files matching the selected room. Matching resource IDs are selected automatically where available; the dropdown can switch among the room's FTR resources.
- Regions with a nonzero candidate portal field are highlighted and labeled with their region index. FTR coordinates are mapped from their matching IMG dimensions, including the quarter-turn used by extracted panorama bitmaps. Files without matching IMG metadata use direct pixel coordinates.
- Use **Load BMP** to preview an external image. A matching IMG file under `Mods/cdn/IMG` may provide its angle range.
- The preview does not apply all game effects, sprites, underwater distortion, or room-specific ROM limits; it is an approximation.

## Common workflows

### Edit a panorama

1. Configure the game and Mods folders.
2. Extract or select a panorama in **Pano Extractor IMG/NOD**.
3. Click **Edit Selected**, make changes in Painter, then inject with **Inject to Pano**.
4. In **SPK Archive**, click **Repack All** and install the generated archive from `Mods/repacked`.

### Edit a GRA sprite

1. Select the resource and frame in **Sprites Extractor GRA**.
2. Click **Edit Graphic** and make changes in Painter without resizing the frame.
3. Click **Inject to GRA**.
4. Repack the archive corresponding to the source folder and install the generated archive.

### Edit a save

1. Copy the save to a safe test location.
2. Open **Save Editor**, select the copied `.QG5`, and click **Load**.
3. Edit Inventory, Character, or Flags. Click **Apply Model** or **Apply Room** for character changes.
4. Click **Save** and test the copy in-game before using it as the active save.

## Troubleshooting

- **No files appear:** Confirm that Game DATA Folder points to the `DATA` folder. Check that archives or extracted folders exist, then use the screen's **Scan**, **Reload**, or **Extract** action.
- **Previews are missing:** Some previews require files under Mods. Extract the relevant resources and verify both configured paths.
- **A save field is unavailable:** The save may not match a supported structure. Keep it unchanged and try a complete backup or a fresh in-game save.
- **A modified save fails in-game:** Restore the original or test-copy backup. Try one change at a time; save editing has limited in-game validation.
- **An operation reports an error:** Read the on-screen status and `qfg5extractor.log` in the working directory. Use **View Log** if available.
