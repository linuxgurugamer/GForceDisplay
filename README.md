# GForceDisplay for KSP1

GForceDisplay is a flight instrument for Kerbal Space Program 1 that shows the G-forces acting on the active vessel in a compact, resizable window.

It provides an analog G-force dial together with separate vertical and horizontal G indicators, allowing you to see both the overall load on the vessel and how that acceleration is oriented.

## What the mod displays

### Main G-force dial

The main display shows the vessel's current G-force both numerically and on an analog dial.

It supports two modes:

- **Total G** — uses KSP's `FlightGlobals.ActiveVessel.geeForce` and displays the overall G-load as a non-negative value.
- **Signed axial G** — shows acceleration along the vessel reference transform's `up` axis, allowing both positive and negative G readings.

The dial includes:

- Configurable maximum G range.
- Configurable warning threshold.
- Configurable redline threshold.
- Color changes as the warning and redline limits are reached.
- Current G-force numerical readout.
- Minimum and maximum observed G markers.
- Peak/reset controls.

### Vertical G indicator

A vertical sliding scale is displayed to the left of the main dial.

- Shows signed vertical G relative to the current celestial body.
- Positive values represent acceleration away from the body's center.
- Negative values represent acceleration downward.
- Includes scale markings and a labeled zero point.
- Tracks and displays its own minimum and maximum observed values.
- MIN and MAX markers are shown as short markers to the left of the scale.

The vertical indicator can be independently enabled or disabled in Settings.

### Horizontal G indicator

A horizontal sliding scale is displayed below the main dial.

- Shows the magnitude of acceleration in the local horizontal plane.
- Uses the configured maximum G value for its scale.
- Includes scale markings.
- Tracks and displays its own minimum and maximum observed values.
- MIN and MAX markers are shown as short markers below the scale.

The horizontal indicator can be independently enabled or disabled in Settings.

## Minimum and maximum tracking

GForceDisplay tracks extrema independently for:

- Main G-force dial.
- Vertical G.
- Horizontal G.

The **Reset Peak** control resets all tracked minimum and maximum values.

Extrema are also reset automatically when the active vessel changes.

## Window behavior

- Main window is movable by dragging from almost anywhere on the window.
- Window is resizable using the resize grip.
- The dial and component indicators resize with the window.
- The main dial automatically expands into unused space when the vertical or horizontal indicator is disabled.
- Window position and size are remembered.
- The main window and Settings window start closed and are opened from the toolbar.
- Optional automatic hiding while the game is paused is enabled by default.
- Pressing **F2** or otherwise hiding the KSP UI always hides GForceDisplay temporarily.
- When the KSP UI is restored, the display returns if it was previously open.

## Toolbar and click-through support

GForceDisplay supports:

- **ToolbarController** for integration with the stock Application Launcher and Blizzy Toolbar.
- **ClickThroughBlocker** so mouse clicks on the GForceDisplay windows do not pass through to the flight scene.

## Dependencies

The mod requires:

- ClickThroughBlocker
- ToolbarController

These dependencies are not bundled with the source package.

## Signed G calculation

KSP exposes `Vessel.geeForce` as a scalar magnitude and therefore does not provide a negative direction.

For signed axial G, GForceDisplay calculates proper acceleration from the vessel's change in orbital velocity, removes gravitational acceleration, and projects the resulting acceleration onto `vessel.ReferenceTransform.up`.

This provides a directional positive/negative axial G reading while Total G continues to use KSP's stock scalar G-force value.

## Current version

### 0.2.8

- Main and Settings windows no longer open automatically when entering flight.
- Window visibility is controlled by the toolbar during the current session.
- Existing window position, size, graph, threshold, and display settings remain persistent.
