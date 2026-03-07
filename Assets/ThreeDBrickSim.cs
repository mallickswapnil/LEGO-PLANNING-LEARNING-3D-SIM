using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("3DBrickSim")]
public partial class ThreeDBrickSim : MonoBehaviour
{
    [Header("Bounded Area")]
    [SerializeField] private Vector3 boundsCenter = Vector3.zero;
    [SerializeField] private float boundsLength = 101f;
    [SerializeField] private float boundsWidth = 101f;
    [SerializeField] private float wallHeight = 3f;
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float floorThickness = 0.2f;
    [SerializeField] private Material boundsMaterial;

    [Header("Layout")]
    [SerializeField] private Vector3 normalBrickPosition = new Vector3(-4f, 1f, 0f);
    [SerializeField] private Vector3 legoBrickPosition = new Vector3(4f, BrickHeight * 0.5f, 0f);

    [Header("Inventory")]
    [SerializeField] private int inventoryRows = 5;
    [SerializeField] private int inventoryColumns = 5;
    [SerializeField] private int inventoryLayers = 10;
    [SerializeField] private float inventoryGapX = 0.5f;
    [SerializeField] private float inventoryGapZ = 0.5f;
    [SerializeField] private float inventoryGapY = 0.5f;
    [SerializeField] private float inventoryCornerInset = 1f;
    [SerializeField] private float inventorySeparationGap = 5f;

    [Header("Optional Materials")]
    [SerializeField] private Material normalBrickMaterial;
    [SerializeField] private Material legoBrickMaterial;

    [Header("Orange LEGO Brick")]
    [SerializeField] private GameObject orangeLegoBrickPrefab;
    [SerializeField] private Vector3 orangeLegoBrickPosition = new Vector3(0f, BrickHeight * 0.5f, 0f);
    [Header("Yellow LEGO Brick")]
    [SerializeField] private GameObject yellowLegoBrickPrefab;
    [SerializeField] private Vector3 yellowLegoBrickPosition = new Vector3(2.5f, BrickHeight * 0.5f, 0f);
    [Header("Green LEGO Brick")]
    [SerializeField] private GameObject greenLegoBrickPrefab;
    [SerializeField] private Vector3 greenLegoBrickPosition = new Vector3(5f, BrickHeight * 0.5f, 0f);
    [Header("Orange PLAEX Long Brick")]
    [SerializeField] private GameObject orangePlaexLongBrickPrefab;
    [SerializeField] private Vector3 orangePlaexLongBrickPosition = new Vector3(7.5f, BrickHeight * 0.5f, 0f);
    [Header("Yellow PLAEX Long Brick")]
    [SerializeField] private GameObject yellowPlaexLongBrickPrefab;
    [SerializeField] private Vector3 yellowPlaexLongBrickPosition = new Vector3(10f, BrickHeight * 0.5f, 0f);

    [Header("Camera")]
    [SerializeField] private float cameraHeight = 20f;
    [SerializeField] private float cameraDistanceFactor = 1.2f;
    [SerializeField] private float cameraYaw = 0f;
    [SerializeField] private float cameraPitch = 35f;
    [SerializeField] private float cameraFov = 60f;
    [SerializeField] private bool enableCameraDrag = true;
    [SerializeField] private bool requireCtrlForCameraDrag = true;
    [SerializeField] private int cameraDragMouseButton = 0;
    [SerializeField] private float cameraDragSensitivity = 2.5f;
    [SerializeField] private bool invertVerticalDrag = false;
    [SerializeField] private float cameraMinPitch = 10f;
    [SerializeField] private float cameraMaxPitch = 85f;

    [Header("Plan Execution")]
    [SerializeField] private string selectedPlanName = "";
    [SerializeField] private string planResourceName = "lego_brick_plan";
    [SerializeField] private float planUiWidth = 240f;
    [SerializeField] private float planUiTopMargin = 24f;
    [SerializeField] private float planUiLeftMargin = 16f;
    [SerializeField] private float planUiDropdownHeight = 30f;
    [SerializeField] private float planUiButtonHeight = 30f;

    [Header("Wall Break Physics")]
    [SerializeField] private bool enableWallBreakInput = true;
    [SerializeField] private KeyCode wallBreakModifierKey = KeyCode.LeftShift;
    [SerializeField] private int wallBreakMouseButton = 0;
    [SerializeField] private float wallBreakImpulse = 12f;
    [Min(0.1f)]
    [SerializeField] private float wallBreakMaxImpulsePerMass = 4f;
    [SerializeField] private bool wallBreakHorizontalForceOnly = true;
    [SerializeField] private bool wallBreakUseHitHeightOnly = true;
    [SerializeField] private bool requireImpulseToBreakSupports = true;
    [Min(0f)]
    [SerializeField] private float supportBreakImpulsePerMass = 3f;
    [Min(1f)]
    [SerializeField] private float supportBreakBottomHitMultiplier = 4f;
    [SerializeField] private bool enableDepthBasedWallBreakClicks = true;
    [Min(1)]
    [SerializeField] private int wallBreakTopBrickClicks = 1;
    [Min(1)]
    [SerializeField] private int wallBreakBottomBrickMinClicks = 7;
    [Min(1)]
    [SerializeField] private int wallBreakBottomBrickMaxClicks = 9;
    [SerializeField] private float supportVerticalTolerance = 0.2f;
    [SerializeField] private float supportMinOverlap = 0.1f;

    [Header("Physics Placement")]
    [SerializeField] private bool enforcePhysicsPlacement = true;
    [SerializeField] private bool physicsPlacementUseGravity = false;
    [SerializeField] private bool rejectOverlappingPlacement = true;
    [SerializeField] private float placementPenetrationEpsilon = 0.0005f;

    [Header("Orange LEGO Physics Snap")]
    [SerializeField] private bool enableOrangeLegoPhysicsSnap = true;
    [SerializeField] private float orangeLegoPhysicsSnapProbeRadius = 0.11f;
    [SerializeField] private float orangeLegoPhysicsSnapMaxHorizontalOffset = 0.45f;
    [SerializeField] private float orangeLegoPhysicsSnapMaxVerticalOffset = 0.55f;
    [SerializeField] private int orangeLegoPhysicsSnapMinContacts = 2;
    [SerializeField] private bool orangeLegoSnapShieldCollisionsDuringInsertion = true;
    [SerializeField] private int orangeLegoSnapStabilizationFixedSteps = 2;
    [SerializeField] private float orangeLegoPhysicsSnapJointBreakForce = 3000f;
    [SerializeField] private float orangeLegoPhysicsSnapJointBreakTorque = 3000f;

    [Header("PLAEX Long Physics Snap")]
    [SerializeField] private bool enablePlaexLongPhysicsSnap = true;
    [SerializeField] private float plaexLongPhysicsSnapMaxHorizontalOffset = 0.4f;
    [SerializeField] private float plaexLongInterlockHorizontalTolerance = 0.05f;
    [SerializeField] private float plaexLongPhysicsSnapMaxVerticalOffset = 0.05f;
    [SerializeField] private float plaexLongPhysicsSnapRotationToleranceDegrees = 5f;
    [SerializeField] private bool requirePlaexLongVerticalSnapWhenLowerBrickExists = true;
    [SerializeField] private float plaexLongVerticalSupportMinHeightDelta = 0.05f;
    [SerializeField] private float plaexLongVerticalAllowedInterlockOverlap = 0.35f;
    [SerializeField] private int plaexLongSnapStabilizationFixedSteps = 2;
    [SerializeField] private float plaexLongPhysicsSnapJointBreakForce = 3000f;
    [SerializeField] private float plaexLongPhysicsSnapJointBreakTorque = 3000f;
    [SerializeField] private bool keepPlacedPlaexLongBricksKinematic = true;

    [Header("PLAEX Side Physics Snap")]
    [SerializeField] private bool enablePlaexSidePhysicsSnap = true;
    [SerializeField] private float plaexSidePhysicsSnapNominalCenterSpacing = 5f;
    [SerializeField] private float plaexSidePhysicsSnapCenterSpacingTolerance = 0.05f;
    [SerializeField] private float plaexSidePhysicsSnapMaxConnectorGap = 1.25f;
    [SerializeField] private float plaexSidePhysicsSnapMaxLateralOffset = 0.05f;
    [SerializeField] private float plaexSidePhysicsSnapMaxVerticalOffset = 0.05f;
    [SerializeField] private float plaexSidePhysicsSnapRotationToleranceDegrees = 6f;
    [SerializeField] private float plaexSidePhysicsSnapAllowedInterlockOverlap = 0.8f;
    [SerializeField] private bool plaexSidePhysicsSnapUseInsertionMotion = true;
    [SerializeField] private float plaexSidePhysicsSnapInsertionLift = 1.5f;
    [SerializeField] private int plaexSideSnapInsertionFixedSteps = 12;
    [SerializeField] private bool requirePlaexSideSnapConnectionWhenComplementaryExists = true;
    [SerializeField] private float plaexSidePhysicsSnapJointBreakForce = 3000f;
    [SerializeField] private float plaexSidePhysicsSnapJointBreakTorque = 3000f;

    [Header("Debug")]
    [SerializeField] private bool logPlacementDebug = false;

    private readonly List<string> availablePlanNames = new List<string>();
    private readonly HashSet<Transform> plannedWallBricks = new HashSet<Transform>();
    private readonly HashSet<Transform> detachedWallBricks = new HashSet<Transform>();
    private readonly Dictionary<Transform, int> wallBreakClickCounts = new Dictionary<Transform, int>();
    private Coroutine activePlanCoroutine;
    private Dropdown planDropdown;
    private bool isInitializingDropdown;
    private Camera controlledCamera;
    private float cameraOrbitDistance;
    private int activePlaexSideInsertionCount;

    private const float NormalBrickLength = 5f;
    private const float NormalBrickHeight = 2f;
    private const float NormalBrickWidth = 2f;
    private const float LegoBrickLength = 5f * StudPitch;
    private const float LegoBrickHeight = BrickHeight;
    private const float LegoBrickWidth = BrickWidth;

    private const float StudPitch = 0.8f;
    private const float BrickWidth = 1.6f;
    private const float BrickHeight = 0.96f;
    private const float StudDiameter = 0.48f;
    private const float StudHeight = 0.18f;

    private void Start()
    {
        EnsureBoundsCanFitInventories();
        CreateBoundedArea();
        SpawnNormalBrickInventory();
        SpawnLegoBrickInventory();
        SpawnOrangeLegoBrickInventory();
        SpawnGreenPlaexLongBrickInventory();
        SpawnOrangePlaexLongBrickInventory();
        SpawnYellowPlaexLongBrickInventory();
        SpawnYellowPlaexSideBrickInventory();

        // Demo lineup brick 1: orange LEGO brick.
        // SpawnOrangeLegoBrick();
        // Demo lineup brick 2: yellow PLAEX side brick.
        // SpawnYellowLegoBrick();
        // Demo lineup brick 3: green PLAEX long brick.
        // SpawnGreenLegoBrick();
        // Demo lineup brick 4: orange PLAEX long brick.
        // SpawnOrangePlaexLongBrick();
        // Demo lineup brick 5: yellow PLAEX long brick.
        // SpawnYellowPlaexLongBrick();

        ConfigureCameraToFitEnvironment();
        InitializePlanUiAndExecution();
    }

    private void Update()
    {
        HandleCameraDragInput();
        HandleWallBreakInput();
    }
}
