# Whole-location transfer panel

`TransferSettingsPanel` owns the IMGUI presentation and input/cursor lifetime. It reads the existing configuration and `TransferAll.TransferMode` each frame. Future filter controls belong in `DrawSettings`; selection rules belong in `TransferAll`, shared with the hotkeys. Shopping-list-only selection is controlled solely by the `ShopListItemsOnly` transfer mode, which selects shopping-list parts regardless of minimum condition. Independent `MoveMaps` and `MoveCases` settings include or exclude those categories in every mode. Outside the panel, Shift is ignored for barn/junkyard K/L commands. Inside the panel, Shift-L remains the separate return-to-piles shortcut.

`CollectEntireJunkyardOrBarn` explicitly requests collection. The legacy L command still chooses collection or undo based on the active window. Both paths use the same item selection and stash bookkeeping. The panel's transfer request runs once on the next update, outside IMGUI layout/repaint callbacks.

The panel displays settings relevant to whole-location collection. Warehouse IDs, warehouse warning counts and current-category transfers affect other commands and are omitted. Move maps and Move cases are editable independently and default to On. These settings add their categories alongside the parts selected by the mode; disabling either excludes that category. The name filter applies to all results.

## In-game verification

- Use **Return collected items to junk piles** with inventory closed, inventory open, and a pile's collected tab open. It must return all items previously collected by this mod that remain in the shopping cart to their original piles, regardless of filters. Repeat with nothing to return and check for a single summary popup. Verify both inventory and pile tabs refresh.

- At a barn and junkyard, open F7 while walking. Verify the cursor is usable and clicking the panel does not interact with the world. Close with F7, Escape, and Close; verify normal controls return.
- Change mode and condition with both the panel buttons and their hotkeys. Reopen and verify values agree. Check condition limits at 0 and 100 and the existing 80/85/90 steps.
- Collect with inventory closed, with inventory open, and with a pile's collected tab open. Each must collect from every pile into the shopping cart, never return collected items. Verify visible inventory tabs refresh.
- Check all four modes with each combination of Move maps and Move cases. Outside the panel, verify K/Shift-K and L/Shift-L use identical selection under the same mode. Inside the panel, verify L collects with the selected mode and Shift-L returns collected items. Enabled maps/cases and shopping-list parts bypass mode and condition checks; the name filter still applies to all results.
- Leave the location while the panel is open; verify it closes and input is restored. F7 outside barn/junkyard or while typing in a game search field must not open it.
- Verify the separate scrap-repair overlay and legacy K/L transfers still work. Check panel layout at the game's supported resolutions.

Build verification is possible against the installed game assemblies; input routing and appearance require a running game.

Mouse capture saves and disables FPS camera input and the game's `Cursor3D` component, hides its crosshair images, and unlocks the OS cursor for IMGUI. The panel also reapplies cursor visibility/unlocking in late update and before drawing, after ordinary game updates. Closing (including scene changes) restores the saved camera, cursor component, crosshair, and OS cursor states. Verify this specifically while walking in 3D mode: moving the mouse should move a free pointer without rotating the camera; closing should resume the previous camera/cursor behavior.
