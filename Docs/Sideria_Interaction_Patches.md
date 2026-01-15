# Sideria Interaction Patches Documentation

## Overview
This document describes the Harmony patches implemented to customize player interactions with Sideria and Spirit Dragons. These entities are technically `ToolUser` animals to support drafting, but standard animal interactions like slaughtering, releasing, or training are inappropriate for them.

## Patches Implementation

The patches are located in `The Second Seat/Source/TheSecondSeat/Patches/Sideria_Interaction_Patches.cs`.

### 1. Slaughter Prohibition
**Target:** `RimWorld.Designator_Slaughter.CanDesignateThing` (Postfix)

**Functionality:**
- Prevents the "Slaughter" designator from being used on Sideria or Spirit Dragons.
- Even if the base game logic allows it (e.g., because they are animals of the player faction), this patch forces the acceptance report to `false`.

### 2. Release to Wild Prohibition
**Target:** `RimWorld.Designator_ReleaseAnimalToWild.CanDesignateThing` (Postfix)

**Functionality:**
- Prevents the "Release to Wild" designator from being used on Sideria or Spirit Dragons.
- Ensures that even if the designator is somehow activated, it cannot be applied to these entities.

### 3. Release Button Removal
**Target:** `Verse.Pawn.GetGizmos` (Postfix)

**Functionality:**
- Filters the gizmos (command buttons) returned for Sideria and Spirit Dragons.
- Specifically removes:
    - `Designator_ReleaseAnimalToWild` instances.
    - Any command with the label "Release to wild" (or its translated equivalent).
- This ensures the "Release" button does not appear in the inspection pane.

### 4. Training Tab Hiding
**Target:** `RimWorld.ITab_Pawn_Training.IsVisible` (Getter Postfix)

**Functionality:**
- Hides the "Training" tab in the inspection pane for Sideria and Spirit Dragons.
- Overrides the default visibility logic which would otherwise show the tab for `ToolUser` animals of the player faction.
- Uses `Traverse` to access the protected `SelPawn` property of the tab to identify the selected entity.

## Technical Details
- **Utility Class:** `SideriaInteractionUtils` provides a centralized method `IsSideriaOrDragon(Thing t)` to identify the target entities based on their `defName`.
    - Checks for `Sideria_DescentRace`.
    - Checks for defNames starting with `Sideria_SpiritDragon_`.

These patches ensure a more immersive experience by removing game mechanics that contradict the lore and status of Sideria and her dragons.
