# Project Sun - FPS foundation

Open `SampleScene` and press Play. The scene creates an isolated combat range at runtime, so no manual scene wiring is needed for this first vertical slice.

Controls: `WASD` move, `Shift` sprint, `Space` jump, `C`/`Ctrl` crouch, left mouse fire, right mouse aim, `R` reload, `Q` dash and `E` combat focus.
Press `Tab` to open the live weapon loadout screen. Each slot has two sample components plus a remove option, and the right panel reports the immediately recalculated values.

## Architecture

- `Player/FpsPlayerController`: character movement and mouse look.
- `Weapons/HitscanWeapon`: automatic hitscan firing, reload, spread and damage application.
- `Weapons/WeaponConfiguration`: `WeaponDefinition` and `WeaponAttachment` ScriptableObject authoring types. An attachment can be equipped at runtime with `HitscanWeapon.TryEquip`; one item is retained per attachment slot.
- `UI/WeaponCustomizationUI`: the working prototype loadout screen. Its sample components are runtime-only; replace these with authored `WeaponAttachment` assets before adding persistence or inventory.
- `Abilities/FpsAbilityController`: input and cooldown ownership for dash and combat focus. More abilities should follow this boundary, instead of being inserted into weapon or movement scripts.
- `Core/Health`: shared damage contract for players, AI and destructibles.

## Production roadmap

This is an offline gameplay foundation, not a finished competitive shooter. The next high-value milestones are authoritative networking, input rebinding (Unity Input System), animation/weapon view models, AI/navmesh, audio/VFX, a proper loadout menu and telemetry-driven balance tests. Keep game rules server-authoritative before adding ranked or progression systems.
