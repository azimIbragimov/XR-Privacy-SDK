# XR-Privacy SDK — Quick Start

This sample documents how to wire the SDK into a scene. (A ready-made scene can't be
shipped because it depends on XR Interaction Toolkit rig assets, which aren't
redistributable — so import those first, then follow the steps below.)

## 1. Prerequisites

Install via Package Manager → XR Interaction Toolkit → **Samples**:
- **Starter Assets** (provides the `XR Origin (XR Rig)` prefab)
- **XR Device Simulator** (to drive the rig with mouse/keyboard, no headset needed)

## 2. Scene setup

1. Drag **`XR Origin (XR Rig)`** into the scene. For editor testing also drop in the
   **XR Device Simulator** prefab.
2. Create an empty GameObject `XRPrivacyManager` and add the **XR Privacy Manager** component.
3. Drag the **Dashboard** prefab (from the package's `Runtime/`) into the scene; wire its
   Dropdown / Slider / Confirm button to the manager's **UI References**.
4. Wire **XR References**:
   - Head Transform → the rig's `Camera Offset`
   - Left/Right Controller Transform → the controller transforms
   - Tracking Origin → `XR Origin (XR Rig)`

## 3. Add mechanisms

- **Body channel:** add e.g. a `GaussianNoise` (or `MetaGuardMechanism`, `DMMMechanism`,
  or a `DMM*` composite) component to a GameObject and drop it into a **Mechanism** slot.
  For DMM, assign `anonymizer.onnx` + `normalizer.onnx` (in `Runtime/Models/`).
- **Eye channel:** add a `GazeCursor` GameObject → assign it to **Gaze Transform**, and put
  an `EyeGaussian` (etc.) in the **Eye Mechanism** slot.

## 4. Run

Press Play → use the simulator (WASD + mouse, Q/E vertical) → set the **strength slider**
to 100 → click **Confirm**. The controllers/gaze are now privatized.

## 5. Record (optional)

Tick **Record Session** on the manager (or add a `MotionRecorder`). A CSV of true +
privatized poses is written to `Application.persistentDataPath` (path is logged to the
Console on start).
