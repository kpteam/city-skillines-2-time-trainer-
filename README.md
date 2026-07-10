# City: Skylines II Time Controller (BepInEx Plugin)

This project provides a **mod/plugin** approach for controlling time behavior in-game:

- Increase/decrease simulation speed
- Pause/resume simulation time
- Reset to normal speed
- Set time-of-day presets (morning/noon/evening/midnight)
- Freeze/unfreeze time-of-day at the selected hour

## Hotkeys (default)

- `F6`: Speed up
- `F5`: Slow down
- `F7`: Pause/Resume
- `F8`: Normal speed (x1)
- `F9`: Set 08:00
- `F10`: Set 12:00
- `F11`: Set 18:00
- `F12`: Set 00:00
- `F4`: Toggle freeze time-of-day

All keys and speed limits are configurable through the BepInEx config file.

## Build

1. Install .NET Framework 4.7.2 Developer Pack.
2. Restore/build in Visual Studio.

## Install into game

1. Install BepInEx for Cities: Skylines II.
2. Build this project.
3. Copy `CitySkylines2TimeController.dll` into:
   - `...\Cities Skylines II\BepInEx\plugins\`
4. Launch the game.

## Notes

- This is a plugin-based implementation in your workspace.
- Time-of-day setting uses runtime reflection to locate writable game time fields/properties and may need adjustment after game updates.
