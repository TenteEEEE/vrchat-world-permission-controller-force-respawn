# PermissionController Force Respawn

A VPM package that adds a standalone world-space panel for Yodokorochan PermissionController. Administrators (Auth) and collaborators (Collab) can choose another player and send them back to their own respawn point (the target's client runs `VRCPlayerApi.Respawn()`).

The destination is the target player's current permission respawn point, the same point they would use when respawning themselves. There is no destination selector. PermissionController continues to manage its normal permission-based spawn points, and sequential-spawn works naturally if it is installed.

## Requirements

- Unity 2022.3
- VRChat Worlds SDK 3.8.1 or later (tested with 3.10.5)
- UdonSharp
- Yodokorochan PermissionController, installed separately

This package has no dependency on sequential-spawn. It contains only this package's source code and does not patch or redistribute PermissionController source or assets. Runtime integration reads only the upstream player object's `Controller` and `CurrentPermission` program variables by name.

## Setup

1. Add this package to the world project through VCC.
2. Select the panel location in the scene, then choose **Tools > TenteEEEE > PermissionController Force Respawn > Create Panel** (or **GameObject > TenteEEEE > PermissionController Force Respawn Panel**). If nothing is selected, or the PermissionController itself (or one of its children) is selected, the panel is placed 0.1 m to the right of all upstream canvases (including the password keypad), with the same rotation and size (1.1 m x 0.9 m) as `MainCanvas`. If no `MainCanvas` is found, it is placed at the controller; with no controller, it is placed at the origin.
3. The generated world-space canvas includes its UI, row template, `VRCUiShape`, and button event wiring. The builder assigns the first PermissionController found in the open scene. If none is found, assign it in the panel Inspector.
4. Keep the panel's `permissionController` reference pointed at the PermissionController used by the world. The Inspector reports missing or incompatible upstream fields. A world build stops if the panel has no valid PermissionController or is missing any of its UI references.

The screen shows your permission in the upper-right chip, other players in join order, their permission badges, and a respawn button on each row. Players who cannot operate see "(view only)" on their permission chip, dimmed rows, and disabled buttons. The last result appears briefly over the bottom of the list. Selecting a player opens a confirmation showing the destination permission; only the operator sees this dialog. The panel is generated into the scene by editor code and is not included as a prefab.

To send force-respawned players somewhere else, assign Transforms to the panel's `authDestination` / `collabDestination` / `visitorDestination`. Permissions left empty use the player's normal respawn point. The override only applies to force respawns; menu respawns and fall respawns keep the upstream PermissionController settings. Note that with `visitorDestination` set, visitors go to that single point even when sequential-spawn assigns them individual points.

UI text uses Unity's built-in `LegacyRuntime.ttf` with `UnityEngine.UI.Text`, so no font asset is redistributed.

## Behavior and security

- Only Auth and Collab can operate the panel. The list is visible to everyone, but its action buttons are disabled for other permissions.
- Operation works only within 2 m of the panel by default (adjustable in the Inspector). Pressing from farther away shows a notice, and the target also checks the sender's position.
- A user cannot select themselves. A collaborator may select an administrator; permission rank does not restrict the target.
- Only the operator sees the confirmation dialog. The target receives no confirmation or notification and cannot reject the request.
- After confirmation, the panel sends a parameterized network event. Each receiving client independently checks that it is the target and that the event sender is currently Auth or Collab and within 2 m of the panel by default (adjustable in the Inspector) before moving (`TeleportTo` an override point if one is set, otherwise `Respawn()`). Hiding the buttons is only a UI measure; the receiver check is the permission enforcement.
- The target ignores requests that arrive less than one second after the previous accepted one. The operator's row button also shows "sent" and stays disabled for 1.5 seconds.
- If the target leaves while confirmation is open, the dialog closes. The panel also rechecks the target and the operator's permission when **Respawn** is pressed.
- Sitting in a station is left to VRChat's standard `Respawn()` behavior.

## Performance

- No synced variables. The only network traffic is one event per force respawn.
- The list refreshes once per second only while the local player is within 20 m of the panel; it pauses farther away and catches up within a second on return. Joins and leaves are applied regardless of distance.
- Each player's permission source is located once and then read as a single variable. The burst of join events a late joiner receives is coalesced into one refresh.

Permission values follow PermissionController's current enum layout: Auth = 0, Collab = 1, Visitor = 2. A future upstream change to the `Controller` or `CurrentPermission` field names is reported by the Inspector and requires a package update.

## License

MIT. See [LICENSE](LICENSE).
