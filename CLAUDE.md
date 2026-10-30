# AR Furniture Designer — notes for Claude

Unity 6 AR app (Android/ARCore): scan the floor, place furniture, adjust it, capture a screenshot. Student capstone.

**Read `docs/PROGRESS.md` first.** It holds the full history, decisions, open Editor steps and the Stage B plan.

## Current state
- Work is split into Stage A (features 1–2: RGB color changer, finger gestures) and Stage B (features 3–4: live size display with per-prefab calibration, Capture + spec-sheet screenshot saved to the Android gallery).
- Stage A is written and compiles (0 errors/warnings, Unity 6000.3.27f1) but is **not yet confirmed on a phone**. Do not start Stage B until the user confirms Stage A works on device.
- All changes are uncommitted. Commit only when the user asks.

## Environment
- Open with Unity **6000.3.27f1** (repo was saved with 6000.3.16f1, which is not installed).
- URP 17.3, AR Foundation / ARCore 6.3.5, Input System, TextMeshPro (uGUI 2.0). Linear color space. Android only.
- Active Input Handling = Input System Package (New). Use `UnityEngine.InputSystem.EnhancedTouch`, never legacy `Input.GetTouch`.
- Binary assets are in Git LFS. Never use GitHub "Download ZIP" (it gives LFS pointer stubs).
- Unity asset/scene YAML must not contain `#` comments (this broke `Mobile_Renderer.asset` once).

## Architecture
- `TouchInteractionManager` — placement, gestures (tap/drag/pinch/twist), re-anchoring after drag, `SelectionChanged` event, Delete/Reset.
- `SelectableFurniture` — user color via MaterialPropertyBlock (source of truth), `LocalBounds` (local-space mesh bounds), `ScaleFactor`/`OriginalScale`, `ApplyFootprintPose(footprint, yaw, scale)`, floor-ring highlight (`SetHighlightVisible` for capture).
- `ColorPickerPanel` + `ColorFormat` — color UI and allocation-free formatting.
- `Assets/Editor/ARDesignerUIBuilder` — menu that builds Figma-styled UI (390×800 overlay canvas) and wires references; has reusable helpers (NewPillButton, NewSlider, NewText, rounded sprite).
- Catalog (see PROGRESS §10): `FurnitureCatalogBuilder` (editor) generates real-size, +Z-facing wrapper prefabs in `Assets/Catalog/Items`, 15 room sets in `Assets/Catalog/RoomSets`, thumbnails, and `FurnitureCatalog.asset`. `FurnitureCatalogPanel` (runtime) shows it; `CatalogUIBuilder` builds the MainScene/MainMenu UI. Edit the item/set tables and re-run the menu, never hand-edit generated prefabs. Old `FurnitureCatalogManager` and the old Canvas panels are unused.
- Convention: every catalog item and set has its front on +Z; placement turns +Z toward the user (`FacingUserYaw`). Wrapper scale 1 = real size, so Stage B calibration can build on it.
- Batch mode works when the Editor is closed: `Unity.exe -batchmode -projectPath . -executeMethod <Class.Method> -quit` (`FurnitureCatalogBuilder.BuildBatch`, `CatalogUIBuilder.BuildAllBatch -previewOut <dir>`, `CatalogSurvey.RunBatch`). Use it to render and inspect results.
- The "Screenshot saved" screen exists only in Figma (`figma-plugin/code.js`), not yet in Unity.

## User preferences
- Wants complete files, plain-language explanations, and click-by-click Unity Editor steps.
- Visual style: professional and corporate. From the button pack, use only the flat sprites (29 blue, 30 red); everything else is navy panels and Figma tokens.
- Code standards: no per-frame allocations in Update, null-safe, no deprecated Unity 6 / AR Foundation 6 APIs, comment non-obvious logic.
