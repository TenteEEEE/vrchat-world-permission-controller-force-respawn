# Changelog

## 0.1.1

- Shorten the operating range from 3 m to 2 m and make it adjustable in the Inspector (Operating range).

## 0.1.0

- Add a standalone world-space Force Respawn panel sized and styled to match PermissionController's default canvas.
- Show the local operator permission, a stable join-order player list with permission badges, and a confirmation dialog that follows the target's current respawn permission.
- Show view-only state through the permission chip and dimmed rows, add transient success and information notices over the list, an empty-list state, and temporary sent feedback on the selected row.
- Add optional per-permission force-respawn destination overrides.
- Place the panel to the right of all upstream canvases (keypad included) when nothing or the PermissionController is selected; otherwise at the selected position.
- Allow Auth and Collab operators to request another player's standard respawn at their own permission respawn point.
- Validate the sender's permission and its distance to the panel (3 m) on the receiving client, and ignore repeats within one second.
- Generate and wire the panel UI through Unity editor menus; stop the build when the controller or a UI reference is missing.
- Bilingual (Japanese / English) Inspector, tooltips, and build messages.
- Refresh the list only within 20 m of the panel, cache each player's permission source, and coalesce join bursts into one refresh.
