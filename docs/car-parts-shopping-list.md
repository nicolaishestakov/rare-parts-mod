# Full Parts-list transfer

Investigated against the installed CMS 2021 assemblies on 2026-10-29.

The old `part == null || !part.IsExamined` guard combined unrelated cases:

- Examined mechanical rows capture a `PartScript` in `CarInfoPartsHelper.__c__DisplayClass3_0`.
- Unexamined mechanical rows use `__c__DisplayClass4_0`, which was not handled.
- Missing/unmounted mechanical rows use `__c__DisplayClass5_0`, but need not be examined.
- Body rows have no `MarkAction` at all, so casting its target cannot resolve them.

The full Parts-list shortcut now uses the selected page/index to retrieve the
actual `CarInfoPart` display model. All three mechanical closure types are
supported, without requiring examination. Wheel dimensions still come from the
matched part's wheel handle.

For body rows, `CarBodyShoppingListIds` captures the IDs passed to
`GameInventory.GetBodyLocalizedName` while either body-list helper runs. Each
helper calls that method once per emitted row, in result order. Its argument is
already the complete catalog ID, including the car and tuned body variant, or
the interior item's ID. The postfix associates those IDs with the returned
display models. This avoids recreating the game's filtering/sorting rules and
does not rely on unique localized names.

Capture is scoped to the helper call and restored by a finalizer, including on
exceptions. Row counts must match before any association is saved. Cached models
are retained to prevent native pointer reuse, then released by a prefix on
`PartsTabPageManager.PrepareFirstPage(CarLoader)`, immediately before it calls
`GetParts`. Body IDs bypass the mechanical `PartProperty` lookup: body
catalog IDs are not ordinary entries in that property list.

Do not patch `CarInfoPartsHelper.GetParts` itself. The installed generated interop
wrapper for its `ref List<CarInfoPart>` parameter constructs an open generic
`Il2CppSystem.Collections.Generic.List<>` at IL offset `0x006A`. Harmony's compiled
wrapper fails there with `BadImageFormatException: Method with open type while
not compiling gshared`, preventing the page from loading. Moving the cache reset
to the non-generic UI caller avoids compiling that wrapper and leaves native
`GetParts` execution intact.

Native evidence (RVAs specific to this installed binary):

| Method | RVA | Relevant behavior |
| --- | --- | --- |
| `CarInfoPartsHelper.GetParts` | `0x4BAB50` | Concatenates mechanical and body display lists |
| `PartsTabPageManager.PrepareFirstPage` | `0x8D4970` | Calls `GetParts` directly at `0x8D4989`; cache-reset hook belongs here |
| `GetMountedBodyParts` | `0x4BB730` | Filters/sorts body parts, resolves IDs, emits rows |
| `GetUnmountedBodyParts` | `0x4BAE40` | Resolves IDs and emits missing body rows |
| Mounted body name lookup | `0x4BBD4C` | Calls `GetBodyLocalizedName` before appending each row |
| Unmounted body name lookup | `0x4BB2AC` | Same one-to-one lookup and append sequence |

The mod builds against the installed interop assemblies. Runtime verification
still requires a game session: add a mounted body panel, a missing body panel,
a missing mechanical part, an unexamined part, and tires/rims on later pages.
Include a tuned body panel and repeated display names to verify variant and
selection identity. The Issues-to-fix path is unchanged.
