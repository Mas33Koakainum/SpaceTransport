# Wire Connection Minigame

An Among Us-inspired wire-connecting task for SpaceTransport.

## Use
- The task is bootstrapped automatically after the first scene loads.
- It opens on start by default; press **F3** to hide/show it.
- Drag each colored connector from the left side to the matching colored socket on the right.
- All five connections must be correct to restore power.
- The task uses a runtime-generated uGUI Canvas and Input System UI module; no prefab or scene references are required.

## Configuration
If you want to add the component manually instead of using the runtime bootstrap, add `WireConnectionMinigame` to an active GameObject. Toggle `Show On Start` and change `Open Key` in the Inspector.

This is currently a local UI minigame prototype. It does not yet synchronize task progress between Netcode clients or apply ship-system damage/repair.
