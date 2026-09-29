# GForceDisplay for KSP1

Displays the active vessel's current G-force in flight as a numerical value and an analog dial or graph. The main display can be configured to show either the total G-force magnitude (always non-negative) or a signed axial G-force (positive and negative values). Peak G values are tracked and can be reset with a button.

## Features

- Main analog G-force dial can be enabled or disabled independently.
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

## Signed G note

KSP exposes `Vessel.geeForce` as a scalar magnitude, so it does not contain a negative direction. Signed mode therefore calculates a directional value by differentiating the vessel's orbital velocity, subtracting `vessel.graviticAcceleration`, and projecting the resulting proper acceleration onto `vessel.ReferenceTransform.up`. This makes signed mode useful for detecting positive versus negative axial loading, while Total G remains the authoritative stock KSP scalar G-load.

