# LEGO/PLAEX Planning Learning

Unity 6 simulation for plan-driven brick placement, physics snapping, and wall-break behavior.

## Unity Version
- Tested with `6000.3.9f1` (Unity 6)

## Current Runtime Features
- Builds a bounded play area (floor + invisible walls) at runtime.
- Spawns 7 inventory groups:
  - Normal bricks (`InventoryBrick_*`)
  - Procedural LEGO bricks (`InventoryLegoBrick_*`)
  - Orange LEGO FBX bricks (`InventoryOrangeLegoBrick_*`)
  - Green PLAEX long bricks (`InventoryGreenPlaexLongBrick_*`)
  - Orange PLAEX long bricks (`InventoryOrangePlaexLongBrick_*`)
  - Yellow PLAEX long bricks (`InventoryYellowPlaexLongBrick_*`)
  - Yellow PLAEX side/cavity bricks (`InventoryYellowPlaexSideBrick_*`)
- Loads all `Resources` text assets ending in `_plan` and builds a runtime dropdown UI.
- Executes each plan step by moving one brick at a time with optional per-step delay.
- Supports overlap rejection and snap-aware placement validation.
- Supports three snap systems:
  - Orange LEGO stud/tube physics snap.
  - PLAEX long vertical snap (stacking).
  - PLAEX side tab/cavity snap with optional insertion animation.
- Supports break input for placed wall bricks with support propagation and depth-based click thresholds.
- Supports editor-only MP4 plan export through Unity Recorder (`com.unity.recorder`).
- Supports a recording-only close-follow camera that frames the active placement area during video export.

## How To Run
1. Open the project in Unity.
2. Open scene `Assets/Scenes/SampleScene.unity`.
3. Press Play.
4. In the top-left runtime panel:
   - Select a plan from the dropdown to execute it.
   - Click `Refresh` to reload the current scene.
   - Click `Export Video` to restart the scene, run the selected plan, and save an MP4 to `videos/`.

## Controls (Default)
- Camera orbit: hold `Ctrl` + hold left mouse + drag.
- Break placed wall bricks: hold `Left Shift` + left click.
- These are configurable on the `ThreeDBrickSim` component.

## Video Export
- Requires `com.unity.recorder` (present in `Packages/manifest.json`).
- Export is supported in the Unity Editor only.
- Recorder output is written to the project-local `videos/` directory.
- The recording camera can use a closer interior/follow framing via the `Plan Video Camera` settings on `ThreeDBrickSim`.

## Plan Files (Currently In `Assets/Resources`)
- `brick_plan.json`
- `lego_brick_plan.json`
- `normal_l_wall_plan.json`
- `orange_wall_5x5_plan.json`
- `orange_stack_5_plan.json`
- `orange_lego_10x10_plan.json`
- `orange_green_plaex_wall_5x2_plan.json`
- `green_plaex_overlap_plan.json`
- `green_plaex_stack_5_plan.json`
- `plaex_side_snap_test_plan.json`
- `plaex_room_4x4_plan.json`
- `plaex_wall_10x5_plan.json`

## Plan JSON Format
Each plan file must be a `TextAsset` under `Assets/Resources`:

```json
{
  "steps": [
    {
      "brickId": "InventoryOrangeLegoBrick_250",
      "targetPosition": { "x": 0.0, "y": 1.0, "z": 0.0 },
      "targetRotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "delay": 0.2
    }
  ]
}
```

Rules:
- `brickId` must exactly match an existing runtime GameObject name.
- Plan file name must end in `_plan` to appear in the dropdown.
- Available model classes are in `Assets/ThreeDBrickSimPlanModels.cs`.

## Brick ID Prefixes You Can Use In Plans
- `InventoryBrick_`
- `InventoryLegoBrick_`
- `InventoryOrangeLegoBrick_`
- `InventoryGreenPlaexLongBrick_`
- `InventoryOrangePlaexLongBrick_`
- `InventoryYellowPlaexLongBrick_`
- `InventoryYellowPlaexSideBrick_`

## Code Map (Project-Authored)
- `Assets/ThreeDBrickSim.cs`
  - Main partial `MonoBehaviour`, serialized settings, startup/update wiring.
- `Assets/ThreeDBrickSim.Environment.cs`
  - Bounds/inventory creation, rigidbody/collider setup, `PickOrientAndPlaceBrick`.
- `Assets/ThreeDBrickSim.PlanExecution.cs`
  - Runtime plan UI, `_plan` discovery, coroutine execution, scene refresh, queued export flow.
- `Assets/ThreeDBrickSim.CameraInput.cs`
  - Orbit camera setup and drag input.
- `Assets/ThreeDBrickSim.WallPhysics.cs`
  - Click-to-break logic, support graph traversal, impulse application.
- `Assets/ThreeDBrickSim.OrangeLegoSnap.cs`
  - Orange LEGO stud/tube snap point generation and fixed-joint attachment.
- `Assets/ThreeDBrickSim.PlaexLongSnap.cs`
  - PLAEX long vertical snap candidate detection and jointing.
- `Assets/ThreeDBrickSim.PlaexSideSnap.cs`
  - PLAEX side connector matching and insertion snap animation.
- `Assets/ThreeDBrickSim.PlanVideoRecording.cs`
  - Editor-only recording helpers (`com.unity.recorder`), output to `videos/`.
- `Assets/ThreeDBrickSim.PlanVideoCamera.cs`
  - Recording-only close-follow camera framing for exported videos.
- Inventory/spawn helpers:
  - `Assets/ThreeDBrickSim.OrangeLegoBrickInventory.cs`
  - `Assets/ThreeDBrickSim.GreenPlaexLongBrickInventory.cs`
  - `Assets/ThreeDBrickSim.OrangePlaexLongBrickInventory.cs`
  - `Assets/ThreeDBrickSim.YellowPlaexLongBrickInventory.cs`
  - `Assets/ThreeDBrickSim.YellowPlaexSideBrickInventory.cs`
  - `Assets/ThreeDBrickSim.OrangeBrickSpawn.cs`
  - `Assets/ThreeDBrickSim.YellowBrickSpawn.cs`
  - `Assets/ThreeDBrickSim.GreenBrickSpawn.cs`
  - `Assets/ThreeDBrickSim.OrangePlaexLongBrickSpawn.cs`
  - `Assets/ThreeDBrickSim.YellowPlaexLongBrickSpawn.cs`

## Assets And Materials
- Scene: `Assets/Scenes/SampleScene.unity`
- FBX bricks used by inventory helpers:
  - `Assets/orangeLEGOBrick.fbx`
  - `Assets/greenPLAEXLong.fbx`
  - `Assets/orangePLAEXLong.fbx`
  - `Assets/yellowPLAEXLong.fbx`
  - `Assets/yellowPLAEXSide.fbx`
- Base materials:
  - `Assets/Scenes/normalBrickMaterial.mat`
  - `Assets/Scenes/legoBrickMaterial.mat`

## Notes
- If prefab fields are not assigned in the inspector, FBX loading paths are editor-only (`AssetDatabase`), so assign prefab references for player builds.
- `Assets/TextMesh Pro/*` and `Assets/TutorialInfo/*` are package/template content, not core simulation logic.
