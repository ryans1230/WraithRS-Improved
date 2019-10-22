# WraithRS-Improved
A C# fork of WraithRS by WolfKnight, to include better radar results and perpendicular radar. It includes many improvements:
- Player must have line of sight to the target vehicle. This prevents running radar behind solid objects.
- More realistic radar limitations. This prevents running radar perpendicular to the road. A true correct implementation would use the cosine of the angle to show the radar speed.
- Radar for front seat passengers. They can have their own separate radar configuration (for now).
- Perpendicular radar beams. Some vehicles may be equipped with this type of radar.
- Wider and more radar beams. The radar beam is one meter wider, and if a vehicle isn't found, up to 2 more radar beams will fan out and look for a vehicle.

### Keybinds:
- `LCtrl+Z`: Opens/closes the control panel
- `Z`: Resets the fast lock when in the vehicle

### Commands:
- `/radardebug`: Toggles the showing of the radar beams when they are active, and the locked vehicle if it meets the criteria.

WolfKnight's original script: https://forum.fivem.net/t/release-wraithrs-advanced-radar-system-1-0-2/48543
