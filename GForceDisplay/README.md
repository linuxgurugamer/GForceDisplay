# GForceDisplay for KSP1

Displays the active vessel's current G-force in flight as a numerical value and an analog dial.

## Features

- Large numerical G readout.
- Analog dial with configurable maximum range.
- Configurable warning and redline thresholds; dial ticks, needle, and value change color at the thresholds.
- Two display modes:
  - **Total G** uses KSP's `FlightGlobals.ActiveVessel.geeForce` and is always non-negative.
  - **Signed axial G** estimates the felt acceleration along the vessel reference transform's `up` axis, allowing positive and negative G values.
- Peak G tracking with a Reset Peak button. Signed mode tracks positive and negative peaks separately.
- Peak values reset automatically when the active vessel changes.
- Movable main and settings windows with saved positions.
- ClickThroughBlocker support so clicks on the windows do not pass through into the flight scene.
- ToolbarController support for stock Application Launcher and Blizzy Toolbar selection.

## Dependencies

These are required at runtime and when building:

- ClickThroughBlocker
- ToolbarController

The DLLs are **not** bundled in this source archive. The project expects the standard install paths:

- `GameData/000_ClickThroughBlocker/Plugins/ClickThroughBlocker.dll`
- `GameData/001_ToolbarControl/Plugins/ToolbarControl.dll`

## Building

Set the `KSP_ROOT` environment variable to your KSP installation directory, for example:

```bat
set KSP_ROOT=C:\Games\Kerbal Space Program
```

Then build the project in `Source/GForceDisplay` with Visual Studio or:

```bat
dotnet build -c Release
```

The DLL is written directly to:

`GameData/GForceDisplay/Plugins/GForceDisplay.dll`

Copy the `GameData/GForceDisplay` folder into your KSP installation's `GameData` directory.

## Signed G note

KSP exposes `Vessel.geeForce` as a scalar magnitude, so it does not contain a negative direction. Signed mode therefore calculates a directional value by differentiating the vessel's orbital velocity, subtracting `vessel.graviticAcceleration`, and projecting the resulting proper acceleration onto `vessel.ReferenceTransform.up`. This makes signed mode useful for detecting positive versus negative axial loading, while Total G remains the authoritative stock KSP scalar G-load.

## 0.2.1 UI changes

- The main G-force display can be dragged from anywhere in the window except the resize grip; normal buttons remain clickable.
- The settings window can also be dragged from anywhere while normal controls remain usable.
- The main display window is resizable from the bottom-right corner.
- The analog G-force dial automatically expands or contracts with the available window area.
- Window width and height are saved and restored between sessions.
- Minimum main-window size is 240 x 300 pixels.

## 0.2.2 extrema markers

- The dial now shows markers for the minimum and maximum G observed since the last reset.
- MAX is shown with a green marker and value; MIN is shown with a blue marker and value.
- In signed axial mode the markers can span the negative and positive sides of the dial.
- In total-G mode the minimum is the actual lowest observed scalar G value, not a hard-coded zero.
- The markers move and scale with the dial when the window is resized.
- Reset Peak also clears both extrema markers and starts a new observation interval.

## 0.2.3 vertical/horizontal scale and extrema

- The vertical G slider now has tick marks and numeric scale labels from `-Dial Max` through `+Dial Max`.
- The horizontal G slider now has tick marks and numeric scale labels from `0` through `Dial Max`.
- Vertical G tracks and displays its own observed MIN and MAX values.
- Horizontal G tracks and displays its own observed MIN and MAX values.
- MIN markers are blue and MAX markers are green, matching the main dial convention.
- Reset Peak clears the vertical and horizontal extrema as well as the main dial extrema.
- All scale marks and extrema markers remain positioned correctly when the window is resized.

## 0.2.4 external graph markings

- Vertical scale tick marks are short and drawn only to the left of the vertical G track.
- Vertical MIN/MAX extrema markers are short and drawn only to the left of the track.
- Horizontal scale tick marks are short and drawn only below the horizontal G track.
- Horizontal MIN/MAX extrema markers are short and drawn only below the track.
- The live/current-G indicators continue to span the tracks for visibility.


## 0.2.6 vertical zero label

- The center/zero tick on the vertical G graph is now explicitly labeled `0`.


## 0.2.7

- Added independent settings to show/hide the vertical and horizontal G graphs.
- Both component graphs are enabled by default.
- The main dial reflows to use space freed by hidden component graphs.
