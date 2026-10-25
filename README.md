# PermissionController Force Respawn

A panel for [Yodokorochan's PermissionController](https://yodokoro.booth.pm/items/7174370) that lets **administrators and collaborators send a chosen player back to that player's respawn point**.

[日本語](README.ja.md) · [VPM repository](https://tenteeeee.github.io/vpm-repos/) · [Package details](Packages/com.tentee.permission-controller-force-respawn/README.md)

- Pick a player from the list, confirm, and they return to the respawn point for **their own** permission (the same place they would go by respawning themselves). Optional per-permission override points are available for force respawns only.
- Only Auth and Collab can operate, and only within **3 m** of the panel. The target's own client checks the sender's permission and distance, so events sent by players without permission or from far away are ignored.
- The confirmation dialog is shown only to the operator. The target cannot refuse.
- The panel matches PermissionController's default canvas (1.1 m x 0.9 m) and is placed next to it automatically.
- **No PermissionController files are patched or redistributed.** The package reads two upstream program variables (`Controller`, `CurrentPermission`) by name.

## Usage

1. Install [PermissionController](https://yodokoro.booth.pm/items/7174370) in your project.
2. Add `https://tenteeeee.github.io/vpm-repos/index.json` to VRChat Creator Companion as a custom VPM repository and add **PermissionController Force Respawn**.
3. In Unity, run **Tools > TenteEEEE > PermissionController Force Respawn > Create Panel** (or right-click the PermissionController in the Hierarchy and choose **TenteEEEE > PermissionController Force Respawn Panel**).

## Development

The package lives in [`Packages/com.tentee.permission-controller-force-respawn/`](Packages/com.tentee.permission-controller-force-respawn/). Run `python -X utf8 Tools/verify-package.py` before committing.

[`Tools/ClientSim/`](Tools/ClientSim/) holds the batch-mode test harness used during development (real Udon in ClientSim with simulated remote players). Copy `ForceRespawnSim.cs` into `Assets/<tmp>/Editor/` and `ForceRespawnSimDriver.cs` into `Assets/<tmp>/` of a project that has PermissionController and this package, then run `-executeMethod ForceRespawnSim.RunBatch` without `-quit`.

Releases are published by pushing an `x.x.x` / `vx.x.x` tag.

## License

[MIT](LICENSE). PermissionController itself is licensed by its author.
