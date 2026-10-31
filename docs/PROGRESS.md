# AR Furniture Designer — Progress Report

**Capstone feature work: color changer, finger gestures, size display, spec-sheet capture**

Last updated: 30 October 2026 · Status: **Stage A written and compiled, waiting for on-phone testing** · Stage B not started

---

## 1. Summary

The goal is to add four features to the existing Unity AR app:

| # | Feature | Stage | Status |
|---|---|---|---|
| 1 | RGB color changer (sliders + HEX, live, keeps texture) | A | Code done, compiles, not yet tested on a phone |
| 2 | Finger-only gestures (drag, pinch, twist) replacing the Rotate/Scale/Move buttons | A | Code done, compiles, not yet tested on a phone |
| 3 | Live, accurate size display (W × L × H in cm) with per-prefab calibration | B | Not started |
| 4 | Capture button + composite "spec sheet" screenshot saved to the Android gallery | B | Not started |
| + | Furniture catalog (82 real-size items, 15 room sets) and professional UI redesign | A+ | Built and previewed in batch mode, not yet tested on a phone (section 10) |

Stage B starts once Stage A has been confirmed working on a physical ARCore phone.

Along the way, three problems in the project setup were found and fixed (section 3).

---

## 2. Where the project lives

| Item | Value |
|---|---|
| **Working project folder** | `C:\Users\User\Downloads\AR-Interior-Designer-full` |
| Source | `git clone https://github.com/Yvantrey/AR-Interior-Designer` with Git LFS files downloaded |
| Unity Editor to use | **6000.3.27f1 (LTS)** |
| Old folder (do not use) | `C:\Users\User\Downloads\AR-Interior-Designer-main` — downloaded as a ZIP, so it has no real textures or models |
| Git state | All changes below are **uncommitted**. Run `git status` in the project folder to see them. |

Main packages: URP 17.3, AR Foundation 6.3.5, ARCore XR Plugin 6.3.5, Input System (1.19 → auto-updated to 1.20.1 by Unity), TextMeshPro (part of uGUI 2.0). Android only.

---

## 3. Setup problems found and fixed

### 3.1 Missing textures and models ("File could not be read")
- **Cause:** the project had been downloaded with GitHub's **Download ZIP** button. That button does not include files stored with **Git LFS**. All 573 binary assets (textures, `.fbx` furniture models, fonts) were 130-byte text placeholders.
- **Fix:** cloned the repository with Git LFS into `AR-Interior-Designer-full` (about 1 GB of real assets). Compared it with the ZIP copy: the repo had no newer changes, so nothing was lost. The Stage A changes were copied into the clone.
- **Rule going forward:** always get this project with `git clone` (Git LFS installed), never with Download ZIP.

### 3.2 Unity version mismatch
- `ProjectSettings/ProjectVersion.txt` said **6000.3.16f1** (correct for the repo); the README said 6000.6.0f1 (wrong).
- 6000.3.16f1 is not installed on this PC. **6000.3.27f1** is a later patch of the same LTS line (bug fixes only), so it is the safe choice. 6000.0.x would be a downgrade and 6000.4/6000.6 would be upgrades; both were avoided.
- Opening with 6000.3.27f1 updates `ProjectVersion.txt` and applies a few minor package updates. This is expected.
- **To do:** change the README's version line to 6000.3.27f1.

### 3.3 Mobile renderer would not load
- **Error:** `Unable to parse file Assets/Settings/Mobile_Renderer.asset: [Parser Failure at line 32 ...]`
- **Cause:** two `#` comment lines had been typed by hand into the asset file in commit `23dc910`. Unity's asset format does not allow comments, so the whole renderer (which draws the AR camera feed on the phone) failed to load.
- **Fix:** deleted only those two comment lines. Both renderer features (`ARBackgroundRendererFeature` and `ARCommandBufferSupportRendererFeature`) are still listed and wired correctly. No other file had this problem.

### 3.4 Touch input could not work with the project's settings
- Player Settings → Active Input Handling is **"Input System Package (New)"**, and the EventSystem uses `InputSystemUIInputModule`. With that setting, the old code's `Input.GetTouch` throws an exception.
- **Fix:** all touch code was rewritten on the Input System's **EnhancedTouch** API. No setting change is needed.

---

## 4. Stage A — what was built

### 4.1 Files

| File | Status | Purpose |
|---|---|---|
| `Assets/Scripts/TouchInteractionManager.cs` | Modified | Placement plus all finger gestures (tap, drag, pinch, twist). Re-anchors furniture after a move. Raises a `SelectionChanged` event. Placement, Delete, Reset and the catalog entry point work as before. |
| `Assets/Scripts/SelectableFurniture.cs` | Modified | Holds the user's color (single source of truth), the furniture's local-space bounds, and the selection highlight (a cyan ring on the floor). |
| `Assets/Scripts/ColorPickerPanel.cs` | New | The color bottom sheet: R/G/B sliders (0–255), HEX field, swatch, value label, Reset color, Done. |
| `Assets/Scripts/ColorFormat.cs` | New | Formats and parses `RGB(r, g, b)` and `#RRGGBB` without per-frame memory allocation. Reused in Stage B. |
| `Assets/Editor/ARDesignerUIBuilder.cs` | New | Menu **Tools → AR Furniture Designer → Create Color Picker UI**. Builds the color UI in the scene in the Figma style and wires every Inspector field. |
| `Assets/Scenes/MainMenu.unity` | Modified | "How it works" text: step 4 = *Tap furniture to select it. Drag to move, pinch to resize, twist to rotate.*; step 5 = *Use Color to recolor it, or Delete to remove it.* |
| `Assets/Settings/Mobile_Renderer.asset` | Modified | Fix from section 3.3. |

**Compile check:** the full Stage A code was compiled with Unity 6000.3.27f1 in batch mode: **0 errors, 0 warnings** from project scripts. It has **not** yet been run on a phone.

### 4.2 Feature 1 — RGB color changer

- Select a placed piece → a round **Color** button appears → it opens a bottom sheet with a swatch, a label such as `RGB(142, 98, 61)  /  #8E623D`, three channel sliders and a HEX field.
- **Live and smooth:** sliders update the furniture on every tick. Colors are applied with `MaterialPropertyBlock`, so no new materials are created and there are no per-frame allocations. The value labels use TextMeshPro's allocation-free text APIs.
- **Texture stays visible:** the chosen color replaces each material's `_BaseColor` (or `_Color` on older shaders). URP Lit multiplies that with the texture, so wood grain and detail remain.
- **Every part is tinted:** all `MeshRenderer`s and `SkinnedMeshRenderer`s, and every material slot on each.
- **Reset color** restores each material's original color exactly.
- **HEX input:** accepts `8E623D` or `#8E623D`, only hex characters can be typed, applies live as soon as six digits are entered, and puts the previous value back if the entry is invalid.
- **Highlight conflict fixed:** the old gold tint on selection (which overwrote colors) is gone. Selection is now shown by a pulsing cyan ring on the floor (Figma cyan `#2FD6E6`, matching the Figma "Selected" oval). Selecting or deselecting never touches material colors, so each piece keeps its own chosen color.

### 4.3 Feature 2 — finger-only gestures

| Gesture | Result |
|---|---|
| Tap a piece | Select it |
| Tap empty floor (nothing being placed) | Deselect |
| Tap floor while a piece is being placed | Place it |
| One-finger drag starting on a piece | Move it along the floor (also selects it) |
| Two-finger pinch | Resize, clamped to 25 %–300 % of the original size |
| Two-finger twist | Rotate around the vertical axis |

How it behaves:
- **Touches that start on UI** (sliders, buttons, panels) are ignored for their whole life, using a direct UI raycast so there is no one-frame delay.
- **Stays on the floor:** the piece follows real detected planes near its current height (so it will not jump onto a table top). Over unscanned floor it slides along an invisible plane at floor height. The bottom of the mesh always rests on the floor.
- **Rotates and scales around the centre of its footprint**, even if the prefab's pivot is off-centre.
- **No drift after moving:** when a drag ends, the piece gets a fresh `ARAnchor` on the plane under its new spot and the old anchor is removed. (An anchor only corrects drift near where it was created.)
- **Smooth:** exponential smoothing makes the piece glide after the finger. It snaps exactly to the final position when the fingers lift.
- **Edge cases handled:** drag → second finger joins → pinch continues without a jump; lifting one finger mid-pinch ends the gesture and the remaining finger is ignored until lifted; twisting past 180° keeps turning; dragging toward the horizon stops at 8 m; app backgrounded mid-gesture; Delete/Reset pressed mid-gesture.
- **During placement:** dragging is disabled, but pinch and twist resize and rotate the preview before it is placed (this replaces the old Scale button behaviour).
- Tuning values are in the Inspector on **InteractionManager → Gesture Tuning** (follow sharpness, drag threshold, pinch/twist thresholds, max drag distance, floor height tolerance).

### 4.4 Design decisions and assumptions
- Highlight = floor ring (not outline or emission) because it matches the Figma design and needs no extra shaders.
- User color replaces `_BaseColor`; for textured materials this is a multiply over the texture.
- Size and rotation are expressed as "footprint centre on the floor + yaw + scale factor". The same data will drive the Stage B size readout.
- `RotateSelected` and `ScaleSelected` are kept as public methods (no longer used by buttons). `ToggleMoveMode` was removed.
- New UI lives on its own **AR Overlay Canvas** using Figma's 390 × 800 frame, so Figma positions map one-to-one. The existing Canvas is unchanged.

---

## 5. Unity Editor steps still to do (Stage A)

1. Open `AR-Interior-Designer-full` in Unity **6000.3.27f1**.
2. If the Mobile_Renderer error is still in the Console: Project window → right-click `Assets/Settings/Mobile_Renderer` → **Reimport**, then **Clear** the Console.
3. Open **Assets → Scenes → MainScene** (double-click). The Hierarchy should show XR Origin, Canvas, InteractionManager, etc.
4. Menu **Tools → AR Furniture Designer → Create Color Picker UI**. This creates `AR Overlay Canvas → Color Picker` and `Assets/UI/ARD_Rounded.png`.
5. Press Play once and check that `Color Picker → Color Button` does not overlap the existing ControlsPanel. If it does, change its **Pos Y** (it is anchored bottom-right).
6. Hierarchy → **Canvas → ControlsPanel**: delete **Rotate Left, Rotate Right, Scale Up, Scale Down, Move**. Keep **Delete** and **Reset**.
7. Select **InteractionManager** and check that Raycast Manager, Plane Manager, Anchor Manager and AR Camera are still assigned.
8. Open **MainMenu** → InstructionsPanel → Instructions Text: confirm the new text fits. If it overflows, tick **Auto Size** (min 18, max 36).
9. Menu **Tools → AR Furniture Designer → Apply App Icon**. This sets the Default Icon and every Android icon slot from `Assets/UI/AppIcon/` (made from the user's AR hand-and-chair artwork, navy `#272240`). Check **Edit → Project Settings → Player → Android → Icon**: under Adaptive, Background is the plain navy image and Foreground is the artwork.
10. Save both scenes. **File → Build Profiles → Android → Build And Run** on the phone. If an older install still shows the old icon, uninstall it first (launchers cache icons).

Note: in the Editor's Game view the app shows "Starting AR camera…". This is normal, because ARCore only runs on a phone.

---

## 6. On-phone test checklist (Stage A)

**Color**
- [ ] Selecting a piece shows the cyan ring and the Color button; the button opens the sheet.
- [ ] Dragging R, G, B recolors the piece smoothly; texture still visible; label shows e.g. `RGB(142, 98, 61) / #8E623D`.
- [ ] Typing `8E623D` applies on the 6th digit and moves sliders to 142/98/61. Typing `12` then closing the keyboard restores the previous value.
- [ ] Switching between pieces: each keeps its own color; select/deselect never changes it.
- [ ] Reset color restores the original look (sliders 255/255/255).
- [ ] Multi-material prefabs: every part is tinted.

**Gestures**
- [ ] Drag a piece: it stays on the floor and does not snap its centre to the fingertip. Walk around for 30 s: no drift, sinking or floating.
- [ ] Pinch: smooth, stops at 25 % and 300 %.
- [ ] Twist: rotates in place around its centre; past 180° keeps turning.
- [ ] Pinch/twist during placement: the placed piece keeps that size and rotation.
- [ ] Lift one finger mid-pinch: no jump; remaining finger does nothing until lifted.
- [ ] Drag, then add a second finger: switches smoothly to pinch.
- [ ] Fast flicks: no teleporting. Dragging toward the horizon stops at about 8 m.
- [ ] Touching a slider never moves furniture. Tap empty floor deselects. Tap another piece selects it.
- [ ] Delete and Reset work, including mid-drag.

Useful command for errors from the phone: `adb logcat -s Unity`

---

## 7. Likely problems and fixes

| Symptom | Likely cause | Fix |
|---|---|---|
| Gestures ignored in one area of the screen | A decorative UI Image/Text has **Raycast Target** ticked | Untick Raycast Target on anything that is not a button |
| No touch response at all | EventSystem module or input setting changed | EventSystem must use **InputSystemUIInputModule**; Active Input Handling = "Input System Package (New)" or "Both" |
| Selection ring missing or pink | Shader stripped from build | Project Settings → Graphics → Always Included Shaders → add **Sprites/Default** |
| A piece doesn't change color | Its shader has no `_BaseColor`/`_Color` | Switch its material to **URP/Lit** |
| Dragging feels too loose/stiff | Tuning | InteractionManager → Gesture Tuning → Follow Sharpness (20–30), Drag Threshold Dp |
| Piece drifts after being moved | No scanned plane under the new spot, so the old anchor was kept | Scan more floor, then nudge the piece again |
| Done button behind Android nav bar | No safe-area handling yet | Raise the sheet's Pos Y (safe-area support planned for Stage B) |
| "Open MainScene first" dialog | Builder run with the wrong scene open | Open Assets/Scenes/MainScene, then run the menu again |

---

## 8. Stage B plan (not started)

**Feature 3 — live size display**
- Measure from mesh bounds in the object's **local** space × its scale, so rotation never inflates the numbers (`SelectableFurniture.LocalBounds` and `ScaleFactor` already exist for this).
- Convention: Width = local X, Length/depth = local Z, Height = local Y.
- Per-prefab calibration goes on a **new component attached to each prefab** (real width/length/height in cm + display name), not on `FurnitureCatalogItem`, because `FurnitureCatalogManager` is not used in MainScene (the 4 furniture buttons call `SetFurnitureToPlace` directly).
- Show the scale vs. original (e.g. "110 % of original"); round to the nearest 0.5 cm; update the text only while the piece is changing.
- Include an Editor helper to measure each prefab's authored size, and a tape-measure verification procedure.

**Feature 4 — Capture + spec sheet**
- Capture button styled like the Figma "Capture" control.
- Hide the editing UI and the selection ring, capture the AR view, then build one high-resolution composite: AR image on the left, spec sheet on the right (name, W × L × H in cm, color swatch + RGB + HEX, material and texture names with a thumbnail, scale vs. original, date/time, "AR Furniture Designer").
- The sheet describes the selected piece, or the most recently placed piece if none is selected.
- Save to the gallery in an "AR Furniture Designer" album, handling Android 10+ scoped storage. Planned approach: evaluate **NativeGallery** (yasirkula) against a small custom plugin and recommend one, with install steps.
- Build the "Screenshot saved" screen (it currently exists only in Figma) with "Keep designing" and "Back to start", plus a clear failure message.

---

## 9. Design tokens (from `figma-plugin/code.js`)

| Token | Hex | Use |
|---|---|---|
| Teal | `#0E8A9C` | Logo, step numbers |
| Cyan | `#2FD6E6` | Detected floor, selection ring |
| Gold | `#EFC65E` | Primary buttons |
| Navy | `#081E2A` | Panel backdrops |
| Ink | `#3A2A05` | Text on gold buttons |
| Sheet | `#0F3140` | Bottom sheets |
| Body text | `#DBEAEE` | Body copy on dark backgrounds |
| Muted text | `#B9D0D7` | Secondary text |
| Ghost button | white at 16 % | Secondary buttons |

Figma frame: 390 × 800. Font in Figma: Inter (Unity currently uses the TMP default font).

---

## 10. Catalog, room sets and UI redesign (Stage A+)

Requested: use the imported button pack, add many more items (a whole living room and more), several designs per setup, professional and corporate look. Packs used: Minimalist ArchViz Bedroom, Polygon Dining Room, Fast Food Restaurant Kit (imported as `Assets/Brick Project Studio`), plus the existing Furniture and Modern Furniture packs.

### 10.1 What was found
- **Pink materials:** Polygon Dining Room and the Fast Food kit use Built-in Standard shaders. Dining was already upgraded at import; the 73 Fast Food materials were converted to URP Lit by `BuiltinMaterialConverter` (colour, textures, smoothness, metallic, normal, emission, transparency).
- **Wrong scale:** measured every prefab. Fast Food kit ~1.6x too big (chairs 1.5 m tall); old Furniture pack 1.6-1.9x (3-seat sofa 4.18 m wide); Modern Furniture inconsistent (Curve Chair 2.33 m tall). Bedroom and Dining packs are close to real size.
- **Furniture placed backwards:** placement turned each piece's +Z away from the user, and these models' fronts are +Z, so sofas showed their backs. Fixed with `FacingUserYaw()` in `TouchInteractionManager` (camera heading + 180°).
- **Stage A bug:** the colour picker hid only its circle, so the "Color" caption stayed on screen. Fixed (the button now lives in the action bar; the builder also parents the caption correctly).

### 10.2 How it works
- `Assets/Editor/FurnitureCatalogBuilder.cs` (menu **Tools → AR Furniture Designer → Build Furniture Catalog**). Holds the item table (source prefab, display name, front direction, real-size scale) and the 15 room-set recipes. Generates:
  - `Assets/Catalog/Items/*.prefab` — wrapper per item: real-world size, front = +Z, footprint centred, base at y = 0. Pack files are untouched. "100 %" now means true size (useful for Stage B).
  - `Assets/Catalog/RoomSets/*.prefab` — complete arrangements, placed and moved as one piece; front (+Z) faces the user.
  - `Assets/Catalog/Thumbnails/*.png` — rendered card images (transparent background).
  - `Assets/Catalog/FurnitureCatalog.asset` — tabs: Room Sets, Living Room, Bedroom, Dining, Office, Café. Card subtitles show W × D × H in cm, or the room and piece count.
- `Assets/Editor/CatalogUIBuilder.cs` — menus **Create Catalog UI (open MainScene first)** and **Style Main Menu (open MainMenu first)**. Re-runnable.
- `Assets/Editor/PrefabThumbnailRenderer.cs`, `CatalogSurvey.cs` (developer survey: 4-side renders + sizes), `BuiltinMaterialConverter.cs`.
- Runtime: `FurnitureCatalog.cs` (data), `FurnitureCatalogPanel.cs` (tabs, pooled cards, collapse, Cancel/Delete label, Clear all with tap-to-confirm), `ScanningStatusUI.cs` (context hints).

Room sets: Classic lounge, Modern living, Media lounge · Minimalist bedroom, Classic bedroom suite · Family dining for 6, Modern dining for 8, Bistro for four · Executive office, Meeting room for 6, Reception lounge · Café table for four, Diner booth, Counter bar, Order point.

### 10.3 UI (390 × 800 overlay canvas)
- **MainScene:** status pill at the top (contextual hints) · bottom catalog sheet (tabs + horizontally scrolling cards with thumbnails, cyan ring on the card being placed, Hide/Show) · action bar riding on the sheet: Color (with swatch), Delete (pack red flat sprite; reads "Cancel" while placing), Clear all (asks for a second tap).
- **Colour sheet:** Done uses the pack's blue flat sprite.
- **MainMenu:** background photo with navy tint and gradient, app icon, title, tagline, feature card, Start designing (blue flat sprite), How it works, Exit · "How it works" sheet with 5 numbered steps and Got it.
- Button pack: only the flat sprites 29 (blue) and 30 (red) are used, 9-sliced with borders (24, 30, 24, 22). Secondary actions are dark navy chips, which stay readable over the camera feed.
- The old Canvas FurniturePanel, ControlsPanel and StatusText are switched off. Rotate/Scale/Move buttons were deleted (Editor step 6 is done). `InteractionManager.furniturePanel` = Catalog and `controlsPanel` = Action Bar.

### 10.4 On-phone checks (add to section 6)
- [ ] Sofas, chairs, beds and room sets appear **facing you** when placed.
- [ ] Sizes look real next to real furniture (sofa about 2 m wide, chairs about 0.9 m tall).
- [ ] Every Café and Dining item shows its real colours (no pink).
- [ ] Tabs scroll and switch; cards scroll; tapping a card shows the preview with a cyan ring; tapping it again cancels.
- [ ] Room sets place, drag, pinch and twist as one piece.
- [ ] Hide/Show slides the sheet; the action bar slides with it.
- [ ] Delete reads "Cancel" while placing; Clear all needs two taps.
- [ ] Main menu: Start, How it works (Got it closes it), Exit.
- [ ] Frame rate is smooth with a full room set (if not, prefer the lighter sets or fewer décor pieces).

---

## 11. Capture + spec sheet (Stage B, feature 4)

Built on 31 October 2026 after Stage A was confirmed on a phone. Compiles with 0 errors and 0 warnings; the capture was run in batch mode on real catalog pieces and the output checked. **Not yet tested on a phone.**

### How it works
- **Capture** button (pack blue) in the catalog header, enabled when at least one piece is placed.
- One tap: cancels any pending placement, hides all overlay UI, the selection ring, the floor reticle and the plane outlines, then grabs the AR frame.
- Renders a **1080 × 1920 spec sheet** off-screen (own canvas and camera at y = -1000):
  - header with app icon, date and time;
  - the photo;
  - "Pieces in this room" with totals (piece count, ≈ total kg);
  - up to 5 rows, selected piece first: thumbnail, name, colour swatch, materials (+ HEX if recoloured), live W × D × H in cm at the current scale, ≈ weight (scales with size³), size %;
  - "+ N more" and a note that weights are estimates.
- Saves a JPEG to **Gallery › Pictures/AR Furniture Designer** through Android MediaStore (`GallerySaver`, no plugin, no permission: minSdk 29). In the Editor it writes to `Application.persistentDataPath`.
- **Screenshot saved** screen (as in Figma): preview, Keep designing, Back to start. On failure: "Couldn't save screenshot" plus the reason.

### Data
- `FurnitureSpec` component on every generated item and room set: display name, type, material, estimated weight, thumbnail, piece count.
- Values live in the `Specs` table in `FurnitureCatalogBuilder.cs`. **Weights and materials are estimates written from each model's appearance**; the packs ship no such data. Edit the table, then re-run Build Furniture Catalog. Room sets sum their pieces' weights and list their three most-used materials.

### Files
New: `FurnitureSpec.cs`, `SpecSheetCapture.cs`, `GallerySaver.cs`. Changed: `FloorPlaneIndicator.cs` and `PlaneTrackingVisualizer.cs` (`SetVisible`), `FurnitureCatalogBuilder.cs` (spec table), `CatalogUIBuilder.cs` (capture UI and preview), `MainScene.unity`.

### On-phone checks
- [ ] Capture is greyed out until something is placed.
- [ ] The saved image has no app UI, cyan ring, reticle or plane outlines.
- [ ] Rows match what is placed; resizing a piece changes its size, weight and % on the next capture.
- [ ] A recoloured piece shows its swatch and HEX.
- [ ] The image appears in Gallery › AR Furniture Designer.
- [ ] Keep designing returns to the room as it was; Back to start opens the main menu.

Feature 3 (live size display while editing) is still to do; the same measurements (`LocalBounds × ScaleFactor`) will drive it.
