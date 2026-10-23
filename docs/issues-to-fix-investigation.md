# Reading the Issues to fix part list

Investigated against the locally installed CMS 2021 assemblies on 2026-10-22.
This is a static investigation; no game session was launched or modified.

## Access the selected issue

The page is the `Order` tab of `CMS.UI.Windows.CarInfoWindow`.
The mod can retrieve it through the existing window manager:

```csharp
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;

var manager = WindowManager.Instance;
if (manager == null)
    return;

var window = manager.GetWindowByID<CarInfoWindow>(WindowID.CarInfo);
if (window == null || !window.IsActive || window.currentTab != CarInfoTabs.Order)
    return;

var orderTab = window.orderTab;
if (orderTab == null || orderTab.orderTabPageManager == null)
    return;

var task = orderTab.orderTabPageManager.currentJobTask;
if (task == null || task.Parts == null)
    return;

// This is the selected issue's complete list, independent of visible rows.
for (var i = 0; i < task.Parts.Count; i++)
{
    var part = task.Parts[i];
    // Read part.ID, part.Name, part.Found, and part.Done here.
    // Found && !Done is the candidate filter for discovered unfinished parts.
    // ID must still be resolved to a shop item; do not add it directly.
}
```

The snippet uses verified assembly signatures, but has not been compiled or
run in-game. A runtime check should also confirm the intended window has input
focus if a global hotkey is used while another window overlays it.

For every issue on the order, use `orderTab.job.jobTasks`. The same job is
exposed through `window.carInfoWindowData.Job`. Each `JobTask` exposes
`type`, `subtype`, `Parts`, and `Done`. The selected task is preferable when
the action means “add the list I am looking at.”

## Verified page population path

The installed managed DLLs are IL2CPP interop wrappers, so their method bodies
do not contain the original game logic. I inspected their metadata and the
original native instructions in `GameAssembly.dll`, using the existing
Cpp2IL address metadata and Iced decoder bundled with MelonLoader.

`OrderTabPageManager.PrepareFirstPage(CarLoader, ref Job, int jobPartID)`:

1. Reads `job.jobTasks[jobPartID]` into `currentJobTask`.
2. Calls `JobHelper.CheckJobPartAndGetItems(carLoader, ref job, ref currentJobTask)`.
3. Stores the returned display models in the inherited `parts` list.
4. Calls `SetFixedState(currentJobTask)` later in the method.

The underlying `JobPart` has `ID`, `Name`, `Done`, and `Found` fields.
The display model, `CarInfoPart`, has name, condition, examination and marking
state, but no item ID. Rendered `PartListItem` / `PartListOrderItem` rows also
do not expose the originating `JobPart`. Reading row text would lose identity.

## IDs need conversion before adding to the shop list

In the mechanical-part branch of `CheckJobPartAndGetItems`, native code:

1. Compares `PartScript.GetGameObjectPathWithoutRoot(StringBuilder)` against
   `JobPart.ID` to find the physical part.
2. Calls `GameInventory.GetItemProperty(partScript.id)` for its item metadata.
3. Sets the job part's name from the item's localized name.
4. Computes `Done` with `PartScript.IsRepaired(job.globalCondition)`.
5. Sets `Found` from `PartScript.IsExamined` and writes the updated job part
   back to the task list.

Therefore `JobPart.ID` is a car-part path in this branch, not a catalog ID;
`JobPart.Name` is a display name, not a reliable catalog key either.

For mechanical parts, build a lookup from
`orderTab.carLoader.GetPartScriptCache()` using each part's
`GetGameObjectPathWithoutRoot(...)` value. Resolve the matching object's
`GetID()` / `id` to the base catalog item. `GetIDWithTuned()` is also exposed,
but preserving a tuning variant is a separate product choice. Do not collapse
different physical positions before counting required quantities.

The helper also has separate body-part and service-task branches. Its body
branch calls `CarLoader.GetCarPart(string)`, `GetDefaultName(string)`, and
`GameInventory.GetBodyLocalizedName(string)`. Exact body catalog-ID construction
still needs tracing before implementation. Service rows include fluids,
alignment and tuning requirements, so a nonempty `Parts` list alone does not
prove that every entry can be bought. Skip entries that cannot be resolved to
an actual replacement part.

For tire/rim entries, the job-part fields do not contain dimensions. These
must come from the matched car/wheel data. The shopping list supports them in
`ShopListItemDataEx` (`Tire`, `Rim`, `Width`, `Size`, `Profile`, `ET`).

## Shopping-list destination

Both of these existing game methods are exposed:

```csharp
UIManager.Get().AddToShopList(string ID, string bonusText,
    CMS.Containers.ShopListItemDataEx additionalData);

UIManager.Get().ShopListWindow.AddToShopList(string ID, string bonusText,
    CMS.Containers.ShopListItemDataEx additionalData);
```

These are signatures, not executable calls. Quantity/duplicate handling and
the required defaults for `bonusText` and `additionalData` were not traced.
Use the game's addition API after resolving item identity; do not directly
append to its backing list. Existing mod code already reads list amounts and
removes entries through the corresponding game API in `TransferAll.cs`.

## Runtime verification before implementing the feature

- Log task type/subtype and each entry's ID, Name, Found, Done with the order
  tab open. Compare with visible rows, including a multipage issue.
- Verify path matching for repeated mechanical parts and unmounted parts.
- Check an undiscovered part, then examine it and reopen the tab to confirm
  status refresh. Check a completed replacement similarly.
- Confirm whether tasks that have not been selected have fresh flags before
  offering an “all issues” operation. The helper takes mutable references and
  updates task state; it should not be treated as a side-effect-free getter.
- Resolve body variants and wheel dimensions and confirm service tasks are
  excluded before writing anything to the shopping list.

## Native evidence locations

Addresses are RVAs for this installed binary only, not offsets to hard-code:

| Method / operation | RVA |
| --- | --- |
| `OrderTabPageManager.PrepareFirstPage` | `0x9E2640` |
| Selected task assignment | `0x9E280A`–`0x9E2816` |
| Call to `CheckJobPartAndGetItems` | `0x9E2875` |
| Store returned display list | `0x9E2881` |
| `JobHelper.CheckJobPartAndGetItems` | `0x18705E0` |
| Mechanical part path lookup | `0x1870AC7`–`0x1870AE1` |
| Catalog lookup using `PartScript.id` | `0x1870B4D`–`0x1870B57` |
| Repair / examination checks | `0x1870B8F`–`0x1870BC5` |

Metadata sources: `MelonLoader/Managed/Assembly-CSharp*.dll` and
`MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/Assembly-CSharp-firstpass.dll`
under the installed game's directory.
