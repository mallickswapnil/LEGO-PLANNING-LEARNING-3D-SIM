using UnityEngine;

public partial class ThreeDBrickSim
{
    private const int PlacementOverlapBufferSize = 256;
    private readonly Collider[] placementOverlapBuffer = new Collider[PlacementOverlapBufferSize];

    private void CreateBoundedArea()
    {
        GameObject boundsRoot = new GameObject("EnvironmentBounds");

        GameObject floor = CreateBox(
            "Floor",
            new Vector3(boundsCenter.x, boundsCenter.y - (floorThickness * 0.5f), boundsCenter.z),
            new Vector3(boundsLength, floorThickness, boundsWidth),
            boundsRoot.transform);
        ApplyMaterial(floor, boundsMaterial);

        float halfLength = boundsLength * 0.5f;
        float halfWidth = boundsWidth * 0.5f;
        float wallCenterY = boundsCenter.y + (wallHeight * 0.5f);

        GameObject wallLeft = CreateBox(
            "Wall_Left",
            new Vector3(boundsCenter.x - halfLength + (wallThickness * 0.5f), wallCenterY, boundsCenter.z),
            new Vector3(wallThickness, wallHeight, boundsWidth),
            boundsRoot.transform);
        ApplyMaterial(wallLeft, boundsMaterial);
        SetVisible(wallLeft, false);

        GameObject wallRight = CreateBox(
            "Wall_Right",
            new Vector3(boundsCenter.x + halfLength - (wallThickness * 0.5f), wallCenterY, boundsCenter.z),
            new Vector3(wallThickness, wallHeight, boundsWidth),
            boundsRoot.transform);
        ApplyMaterial(wallRight, boundsMaterial);
        SetVisible(wallRight, false);

        GameObject wallFront = CreateBox(
            "Wall_Front",
            new Vector3(boundsCenter.x, wallCenterY, boundsCenter.z + halfWidth - (wallThickness * 0.5f)),
            new Vector3(boundsLength, wallHeight, wallThickness),
            boundsRoot.transform);
        ApplyMaterial(wallFront, boundsMaterial);
        SetVisible(wallFront, false);

        GameObject wallBack = CreateBox(
            "Wall_Back",
            new Vector3(boundsCenter.x, wallCenterY, boundsCenter.z - halfWidth + (wallThickness * 0.5f)),
            new Vector3(boundsLength, wallHeight, wallThickness),
            boundsRoot.transform);
        ApplyMaterial(wallBack, boundsMaterial);
        SetVisible(wallBack, false);
    }

    private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        return box;
    }

    private void SpawnNormalBrick()
    {
        CreateNormalBrick("Brick_Normal_5x2x2", normalBrickPosition, null);
    }

    private void SpawnNormalBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject inventoryRoot = new GameObject("Inventory_NormalBricks");
        Vector3 normalStart = GetNormalInventoryStartPosition();
        float baseY = boundsCenter.y + (NormalBrickHeight * 0.5f);
        float stepX = NormalBrickLength + inventoryGapX;
        float stepZ = NormalBrickWidth + inventoryGapZ;
        float stepY = NormalBrickHeight + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = normalStart.x + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = normalStart.z - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;
                    CreateNormalBrick($"InventoryBrick_{brickIndex}", brickPosition, inventoryRoot.transform);
                }
            }
        }
    }

    private void SpawnLegoBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject inventoryRoot = new GameObject("Inventory_LegoBricks");
        Vector3 normalStart = GetNormalInventoryStartPosition();
        float normalStepX = NormalBrickLength + inventoryGapX;
        float normalRightEdge = normalStart.x + ((inventoryColumns - 1) * normalStepX) + (NormalBrickLength * 0.5f);

        float legoStartX = normalRightEdge + inventorySeparationGap + (LegoBrickLength * 0.5f);
        float legoStartZ = normalStart.z;
        float baseY = boundsCenter.y + (LegoBrickHeight * 0.5f);
        float stepX = LegoBrickLength + inventoryGapX;
        float stepZ = LegoBrickWidth + inventoryGapZ;
        float stepY = LegoBrickHeight + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = legoStartX + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = legoStartZ - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;
                    CreateLegoBrick($"InventoryLegoBrick_{brickIndex}", brickPosition, inventoryRoot.transform);
                }
            }
        }
    }

    private Vector3 GetNormalInventoryStartPosition()
    {
        float halfLength = boundsLength * 0.5f;
        float halfWidth = boundsWidth * 0.5f;
        float startX = boundsCenter.x - halfLength + wallThickness + (NormalBrickLength * 0.5f) + inventoryCornerInset;
        float startZ = boundsCenter.z + halfWidth - wallThickness - (NormalBrickWidth * 0.5f) - inventoryCornerInset;
        return new Vector3(startX, 0f, startZ);
    }

    private GameObject CreateNormalBrick(string brickName, Vector3 position, Transform parent)
    {
        GameObject normalBrick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        normalBrick.name = brickName;
        normalBrick.transform.position = position;
        normalBrick.transform.localScale = new Vector3(NormalBrickLength, NormalBrickHeight, NormalBrickWidth);
        if (parent != null)
        {
            normalBrick.transform.SetParent(parent, true);
        }

        if (normalBrickMaterial != null)
        {
            Renderer renderer = normalBrick.GetComponent<Renderer>();
            renderer.sharedMaterial = normalBrickMaterial;
        }

        EnsureBrickRigidbody(normalBrick);
        return normalBrick;
    }

    private void SpawnLegoProportionedBrick()
    {
        CreateLegoBrick("Brick_LEGO_5x2x2", legoBrickPosition, null);
    }

    private GameObject CreateLegoBrick(string brickName, Vector3 position, Transform parent)
    {
        GameObject legoBrickRoot = new GameObject(brickName);
        legoBrickRoot.transform.position = position;
        if (parent != null)
        {
            legoBrickRoot.transform.SetParent(parent, true);
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(legoBrickRoot.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth);
        ApplyMaterial(body, legoBrickMaterial);

        float topY = (LegoBrickHeight * 0.5f) + (StudHeight * 0.5f);
        float startX = -((5 - 1) * StudPitch) * 0.5f;
        float startZ = -((2 - 1) * StudPitch) * 0.5f;

        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stud.name = $"Stud_{x}_{z}";
                stud.transform.SetParent(legoBrickRoot.transform, false);
                stud.transform.localScale = new Vector3(StudDiameter, StudHeight * 0.5f, StudDiameter);
                stud.transform.localPosition = new Vector3(startX + (x * StudPitch), topY, startZ + (z * StudPitch));
                ApplyMaterial(stud, legoBrickMaterial);
            }
        }

        EnsureBrickRigidbody(legoBrickRoot);
        return legoBrickRoot;
    }

    private void EnsureBrickRigidbody(GameObject brickRoot)
    {
        EnsureBrickCollisionVolume(brickRoot);

        Rigidbody rb = brickRoot.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = brickRoot.AddComponent<Rigidbody>();
        }

        ConfigureBrickRigidbodyForSimulation(rb);
    }

    private void ConfigureBrickRigidbodyForSimulation(Rigidbody rb)
    {
        if (rb == null)
        {
            return;
        }

        rb.isKinematic = !enforcePhysicsPlacement;
        rb.useGravity = enforcePhysicsPlacement && physicsPlacementUseGravity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private static void EnsureBrickCollisionVolume(GameObject brickRoot)
    {
        if (brickRoot == null)
        {
            return;
        }

        bool isBodyOnlyOrangeBrick = IsOrangeLegoBrick(brickRoot.transform);
        bool isBodyOnlyPlaexLongBrick = IsPlaexLongBrick(brickRoot.transform);
        bool isBodyOnlyYellowPlaexSideBrick = IsYellowPlaexSideCavityBrick(brickRoot.transform);
        Collider[] existingColliders = brickRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < existingColliders.Length && !isBodyOnlyOrangeBrick && !isBodyOnlyPlaexLongBrick && !isBodyOnlyYellowPlaexSideBrick; i++)
        {
            Collider collider = existingColliders[i];
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return;
            }
        }

        BoxCollider boxCollider = brickRoot.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = brickRoot.AddComponent<BoxCollider>();
        }

        if (isBodyOnlyOrangeBrick)
        {
            ConfigureBodyOnlyOrangeCollider(brickRoot.transform, boxCollider, existingColliders);
            return;
        }

        if (isBodyOnlyPlaexLongBrick || isBodyOnlyYellowPlaexSideBrick)
        {
            ConfigureBodyOnlyPlaexLongCollider(brickRoot.transform, boxCollider, existingColliders);
            return;
        }

        Bounds worldBounds = GetWorldBounds(brickRoot.transform);
        Vector3 localSize = ConvertWorldSizeToLocalSize(brickRoot.transform, worldBounds.size);

        boxCollider.isTrigger = false;
        boxCollider.center = brickRoot.transform.InverseTransformPoint(worldBounds.center);
        boxCollider.size = localSize;
    }

    private static void ConfigureBodyOnlyOrangeCollider(Transform brickTransform, BoxCollider bodyCollider, Collider[] existingColliders)
    {
        if (brickTransform == null || bodyCollider == null)
        {
            return;
        }

        if (existingColliders != null)
        {
            for (int i = 0; i < existingColliders.Length; i++)
            {
                Collider collider = existingColliders[i];
                if (collider == null || collider == bodyCollider || collider.isTrigger)
                {
                    continue;
                }

                collider.enabled = false;
            }
        }

        Vector3 desiredWorldBodySize = new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth);
        bodyCollider.isTrigger = false;
        bodyCollider.center = Vector3.zero;
        bodyCollider.size = ConvertWorldSizeToLocalSize(brickTransform, desiredWorldBodySize);
    }

    private static void ConfigureBodyOnlyPlaexLongCollider(Transform brickTransform, BoxCollider bodyCollider, Collider[] existingColliders)
    {
        if (brickTransform == null || bodyCollider == null)
        {
            return;
        }

        if (existingColliders != null)
        {
            for (int i = 0; i < existingColliders.Length; i++)
            {
                Collider collider = existingColliders[i];
                if (collider == null || collider == bodyCollider || collider.isTrigger)
                {
                    continue;
                }

                collider.enabled = false;
            }
        }

        if (!TryGetPlaexLongBodyWorldBounds(brickTransform, out Bounds bodyWorldBounds))
        {
            bodyWorldBounds = GetWorldBounds(brickTransform);
        }

        bodyCollider.isTrigger = false;
        bodyCollider.center = brickTransform.InverseTransformPoint(bodyWorldBounds.center);
        bodyCollider.size = ConvertWorldSizeToLocalSize(brickTransform, bodyWorldBounds.size);
    }

    private static bool TryGetPlaexLongBodyWorldBounds(Transform brickTransform, out Bounds bodyBounds)
    {
        bodyBounds = default;
        if (brickTransform == null)
        {
            return false;
        }

        Renderer[] renderers = brickTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!renderer.transform.name.StartsWith(PlaexLongModelNodeName, System.StringComparison.OrdinalIgnoreCase) &&
                !renderer.transform.name.StartsWith(PlaexSideModelNodeName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bodyBounds = renderer.bounds;
            return true;
        }

        return false;
    }

    private static Vector3 ConvertWorldSizeToLocalSize(Transform referenceTransform, Vector3 worldSize)
    {
        if (referenceTransform == null)
        {
            return worldSize;
        }

        Vector3 lossyScale = referenceTransform.lossyScale;
        float scaleX = Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f);
        float scaleY = Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f);
        float scaleZ = Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f);

        return new Vector3(
            Mathf.Max(worldSize.x / scaleX, 0.01f),
            Mathf.Max(worldSize.y / scaleY, 0.01f),
            Mathf.Max(worldSize.z / scaleZ, 0.01f));
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
    }

    private static void SetVisible(GameObject target, bool isVisible)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = isVisible;
        }
    }

    public bool PickOrientAndPlaceBrick(string brickId, Vector3 orientationEuler, Vector3 targetLocation)
    {
        if (string.IsNullOrWhiteSpace(brickId))
        {
            Debug.LogWarning("PickOrientAndPlaceBrick: brickId is empty.");
            return false;
        }

        Transform brickTransform = FindBrickTransformById(brickId);
        if (brickTransform == null)
        {
            Debug.LogWarning($"PickOrientAndPlaceBrick: Brick '{brickId}' was not found.");
            return false;
        }

        Vector3 originalPosition = brickTransform.position;
        Quaternion originalRotation = brickTransform.rotation;
        Quaternion targetRotation = Quaternion.Euler(orientationEuler);
        Rigidbody brickRigidbody = brickTransform.GetComponent<Rigidbody>();
        if (brickRigidbody == null)
        {
            brickRigidbody = brickTransform.gameObject.AddComponent<Rigidbody>();
        }

        // Repositioning a previously placed brick should start from a clean
        // attachment state so stale joints do not affect new snap validation.
        RemoveExistingSnapJoints(brickTransform);

        ConfigureBrickRigidbodyForSimulation(brickRigidbody);
        bool isSnapManagedPlaexBrick =
            IsPlaexLongBrick(brickTransform) &&
            enablePlaexLongPhysicsSnap;
        bool isSnapManagedPlaexSideBrick =
            IsPlaexSideSnapBrick(brickTransform) &&
            enablePlaexSidePhysicsSnap;
        bool canResolveOverlapViaSnap = isSnapManagedPlaexBrick || isSnapManagedPlaexSideBrick;
        bool requiresPlaexSideSnap =
            ShouldRequirePlaexSideSnapConnection(brickTransform);
        bool requiresPlaexLongVerticalSnap =
            ShouldRequirePlaexLongVerticalSnapConnection(brickTransform, targetLocation);
        if (logPlacementDebug && IsPlaexLongBrick(brickTransform))
        {
            Debug.Log(
                $"PickOrientAndPlaceBrick start: brick='{brickId}', originalPosition={originalPosition}, targetPosition={targetLocation}, targetRotation={targetRotation.eulerAngles}, requiresPlaexLongVerticalSnap={requiresPlaexLongVerticalSnap}.");
        }


        if (rejectOverlappingPlacement &&
            !canResolveOverlapViaSnap &&
            IsPlacementPoseBlocked(brickTransform, targetLocation, targetRotation))
        {
            Debug.LogWarning($"PickOrientAndPlaceBrick: Placement for '{brickId}' was rejected due to overlap at target pose.");
            return false;
        }

        if (!brickRigidbody.isKinematic)
        {
            brickRigidbody.linearVelocity = Vector3.zero;
            brickRigidbody.angularVelocity = Vector3.zero;
        }

        if (isSnapManagedPlaexBrick || isSnapManagedPlaexSideBrick)
        {
            // For PLAEX snap-managed placements, set the target pose directly to avoid
            // large sweep velocities that can eject bricks during long-distance moves.
            brickTransform.SetPositionAndRotation(targetLocation, targetRotation);
        }
        else if (enforcePhysicsPlacement || !brickRigidbody.isKinematic)
        {
            brickRigidbody.MovePosition(targetLocation);
            brickRigidbody.MoveRotation(targetRotation);
        }
        else
        {
            brickTransform.SetPositionAndRotation(targetLocation, targetRotation);
        }

        Physics.SyncTransforms();
        TrySnapBrickWithPhysics(brickTransform);
        TrySnapPlaexLongBrickWithPhysics(brickTransform);
        if (logPlacementDebug && IsPlaexLongBrick(brickTransform))
        {
            bool hasAnyLongJoint = HasAnyPlaexLongJointConnection(brickTransform);
            Debug.Log(
                $"PickOrientAndPlaceBrick after long snap: brick='{brickId}', currentPosition={brickTransform.position}, hasAnyPlaexLongJoint={hasAnyLongJoint}.");
        }

        if (requiresPlaexLongVerticalSnap &&
            !HasPlaexLongVerticalSnapJointConnection(brickTransform))
        {
            RemoveExistingSnapJoints(brickTransform);
            brickTransform.SetPositionAndRotation(originalPosition, originalRotation);
            if (brickRigidbody != null)
            {
                brickRigidbody.position = originalPosition;
                brickRigidbody.rotation = originalRotation;
                if (!brickRigidbody.isKinematic)
                {
                    brickRigidbody.linearVelocity = Vector3.zero;
                    brickRigidbody.angularVelocity = Vector3.zero;
                }
            }

            Physics.SyncTransforms();
            Debug.LogWarning($"PickOrientAndPlaceBrick: Placement for '{brickId}' was rejected because no valid PLAEX vertical snap connection was formed.");
            return false;
        }

        bool hasPendingPlaexSideInsertion = TrySnapPlaexSideBrickWithPhysics(brickTransform);

        if (requiresPlaexSideSnap &&
            !hasPendingPlaexSideInsertion &&
            !IsWithinRequiredPlaexSideSnapWindow(
                brickTransform,
                brickTransform.position,
                brickTransform.rotation))
        {
            RemoveExistingSnapJoints(brickTransform);
            brickTransform.SetPositionAndRotation(originalPosition, originalRotation);
            if (brickRigidbody != null)
            {
                brickRigidbody.position = originalPosition;
                brickRigidbody.rotation = originalRotation;
                if (!brickRigidbody.isKinematic)
                {
                    brickRigidbody.linearVelocity = Vector3.zero;
                    brickRigidbody.angularVelocity = Vector3.zero;
                }
            }

            Physics.SyncTransforms();
            Debug.LogWarning($"PickOrientAndPlaceBrick: Placement for '{brickId}' was rejected because the snapped pose is outside the required PLAEX side snap window.");
            return false;
        }

        if (rejectOverlappingPlacement &&
            !hasPendingPlaexSideInsertion &&
            HasBlockingOverlap(brickTransform))
        {
            RemoveExistingSnapJoints(brickTransform);
            brickTransform.SetPositionAndRotation(originalPosition, originalRotation);
            if (brickRigidbody != null)
            {
                brickRigidbody.position = originalPosition;
                brickRigidbody.rotation = originalRotation;
                if (!brickRigidbody.isKinematic)
                {
                    brickRigidbody.linearVelocity = Vector3.zero;
                    brickRigidbody.angularVelocity = Vector3.zero;
                }
            }

            Physics.SyncTransforms();
            Debug.LogWarning($"PickOrientAndPlaceBrick: Placement for '{brickId}' was rejected due to overlap after snapping.");
            return false;
        }

        RegisterPlannedWallBrick(brickTransform);
        return true;
    }

    private bool ShouldRequirePlaexSideSnapConnection(Transform movingBrick)
    {
        if (!enablePlaexSidePhysicsSnap ||
            !requirePlaexSideSnapConnectionWhenComplementaryExists ||
            !IsPlaexSideSnapBrick(movingBrick))
        {
            return false;
        }

        return HasComplementaryPlacedPlaexSideBrick(movingBrick);
    }

    private bool ShouldRequirePlaexLongVerticalSnapConnection(
        Transform movingBrick,
        Vector3 targetPosition)
    {
        if (!enablePlaexLongPhysicsSnap ||
            !requirePlaexLongVerticalSnapWhenLowerBrickExists ||
            !IsPlaexLongBrick(movingBrick))
        {
            return false;
        }

        float minHeightDelta = Mathf.Max(0.01f, plaexLongVerticalSupportMinHeightDelta);
        foreach (Transform placedBrick in plannedWallBricks)
        {
            if (placedBrick == null ||
                placedBrick == movingBrick ||
                detachedWallBricks.Contains(placedBrick) ||
                !IsPlaexLongBrick(placedBrick))
            {
                continue;
            }

            if (targetPosition.y > placedBrick.position.y + minHeightDelta)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPlaexLongVerticalSnapJointConnection(Transform movingBrick)
    {
        if (movingBrick == null)
        {
            return false;
        }

        float minHeightDelta = Mathf.Max(0.01f, plaexLongVerticalSupportMinHeightDelta);
        float maxHorizontalInterlockOffset = ResolvePlaexLongInterlockHorizontalTolerance();
        FixedJoint[] joints = movingBrick.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            Transform connectedBrick = joint.connectedBody.transform;
            if (connectedBrick == null || !IsPlaexLongBrick(connectedBrick))
            {
                continue;
            }

            float verticalDelta = movingBrick.position.y - connectedBrick.position.y;
            if (verticalDelta <= minHeightDelta)
            {
                continue;
            }

            float horizontalDistance = Vector2.Distance(
                new Vector2(movingBrick.position.x, movingBrick.position.z),
                new Vector2(connectedBrick.position.x, connectedBrick.position.z));
            if (horizontalDistance > maxHorizontalInterlockOffset)
            {
                continue;
            }

            if (!ArePlaexLongBricksOrientationCompatible(movingBrick, connectedBrick))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasAnyPlaexLongJointConnection(Transform movingBrick)
    {
        if (movingBrick == null)
        {
            return false;
        }

        FixedJoint[] joints = movingBrick.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            Transform connectedBrick = joint.connectedBody.transform;
            if (connectedBrick != null && IsPlaexLongBrick(connectedBrick))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasComplementaryPlacedPlaexSideBrick(Transform movingBrick)
    {
        if (movingBrick == null)
        {
            return false;
        }

        bool movingIsGreen = IsGreenPlaexSideTabBrick(movingBrick);
        foreach (Transform placedBrick in plannedWallBricks)
        {
            if (placedBrick == null ||
                placedBrick == movingBrick ||
                detachedWallBricks.Contains(placedBrick) ||
                !IsPlaexSideSnapBrick(placedBrick))
            {
                continue;
            }

            bool placedIsGreen = IsGreenPlaexSideTabBrick(placedBrick);
            if (placedIsGreen == movingIsGreen)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasPlaexSideSnapJointConnection(Transform movingBrick)
    {
        if (movingBrick == null)
        {
            return false;
        }

        FixedJoint[] joints = movingBrick.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            Transform connectedBrick = joint.connectedBody.transform;
            if (connectedBrick == null || !IsPlaexSideSnapBrick(connectedBrick))
            {
                continue;
            }

            bool movingIsGreen = IsGreenPlaexSideTabBrick(movingBrick);
            bool connectedIsGreen = IsGreenPlaexSideTabBrick(connectedBrick);
            if (movingIsGreen == connectedIsGreen)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsWithinRequiredPlaexSideSnapWindow(
        Transform movingBrick,
        Vector3 targetPosition,
        Quaternion targetRotation)
    {
        if (movingBrick == null || !IsPlaexSideSnapBrick(movingBrick))
        {
            return true;
        }

        float nominalCenterSpacing = Mathf.Max(0.01f, plaexSidePhysicsSnapNominalCenterSpacing);
        float centerSpacingTolerance = Mathf.Max(0.001f, plaexSidePhysicsSnapCenterSpacingTolerance);
        float maxLateralOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxLateralOffset);
        float maxVerticalOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxVerticalOffset);
        float rotationTolerance = Mathf.Max(0f, plaexSidePhysicsSnapRotationToleranceDegrees);
        bool movingIsGreen = IsGreenPlaexSideTabBrick(movingBrick);

        foreach (Transform placedBrick in plannedWallBricks)
        {
            if (placedBrick == null ||
                placedBrick == movingBrick ||
                detachedWallBricks.Contains(placedBrick) ||
                !IsPlaexSideSnapBrick(placedBrick))
            {
                continue;
            }

            bool placedIsGreen = IsGreenPlaexSideTabBrick(placedBrick);
            if (placedIsGreen == movingIsGreen)
            {
                continue;
            }

            float angle = Quaternion.Angle(targetRotation, placedBrick.rotation);
            if (angle > rotationTolerance)
            {
                continue;
            }

            if (!TryBuildPlaexSideConnectors(placedBrick, out PlaexSideConnector[] placedConnectors))
            {
                continue;
            }

            for (int connectorIndex = 0; connectorIndex < placedConnectors.Length; connectorIndex++)
            {
                PlaexSideConnector connector = placedConnectors[connectorIndex];
                if (!TryBuildWorldHorizontalSnapFrame(
                    connector.outwardNormal,
                    out Vector3 axis,
                    out Vector3 lateralAxis))
                {
                    continue;
                }

                Vector3 delta = targetPosition - placedBrick.position;
                float centerDistanceAlongAxis = Mathf.Abs(Vector3.Dot(delta, axis));
                float spacingOffset = Mathf.Abs(centerDistanceAlongAxis - nominalCenterSpacing);
                if (spacingOffset > centerSpacingTolerance)
                {
                    continue;
                }

                float verticalOffset = Mathf.Abs(targetPosition.y - placedBrick.position.y);
                if (verticalOffset > maxVerticalOffset)
                {
                    continue;
                }

                float lateralOffset = Mathf.Abs(Vector3.Dot(delta, lateralAxis));
                if (lateralOffset > maxLateralOffset)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static Transform FindBrickTransformById(string brickId)
    {
        GameObject brickObject = GameObject.Find(brickId);
        return brickObject != null ? brickObject.transform : null;
    }

    private void RegisterPlannedWallBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return;
        }

        plannedWallBricks.Add(brickTransform);
        detachedWallBricks.Remove(brickTransform);
        wallBreakClickCounts.Remove(brickTransform);

        Rigidbody rb = brickTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool keepPlaexKinematic =
                enablePlaexLongPhysicsSnap &&
                keepPlacedPlaexLongBricksKinematic &&
                IsPlaexLongBrick(brickTransform);

            if (keepPlaexKinematic)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                ConfigureBrickRigidbodyForSimulation(rb);
            }

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private bool IsPlacementPoseBlocked(
        Transform brickTransform,
        Vector3 position,
        Quaternion rotation,
        Transform allowedSideInterlockBrick = null,
        bool requireExistingJointForAllowedInterlock = true,
        Transform allowedLongVerticalInterlockBrick = null,
        bool requireExistingJointForAllowedLongInterlock = true)
    {
        if (brickTransform == null)
        {
            return false;
        }

        Vector3 originalPosition = brickTransform.position;
        Quaternion originalRotation = brickTransform.rotation;

        brickTransform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        bool hasBlockingOverlap = HasBlockingOverlap(
            brickTransform,
            allowedSideInterlockBrick,
            requireExistingJointForAllowedInterlock,
            allowedLongVerticalInterlockBrick,
            requireExistingJointForAllowedLongInterlock);

        brickTransform.SetPositionAndRotation(originalPosition, originalRotation);
        Physics.SyncTransforms();

        return hasBlockingOverlap;
    }

    private bool HasBlockingOverlap(
        Transform brickTransform,
        Transform allowedSideInterlockBrick = null,
        bool requireExistingJointForAllowedInterlock = true,
        Transform allowedLongVerticalInterlockBrick = null,
        bool requireExistingJointForAllowedLongInterlock = true)
    {
        Collider[] ownColliders = brickTransform.GetComponentsInChildren<Collider>();
        for (int ownIndex = 0; ownIndex < ownColliders.Length; ownIndex++)
        {
            Collider ownCollider = ownColliders[ownIndex];
            if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger)
            {
                continue;
            }

            Bounds ownBounds = ownCollider.bounds;
            int hitCount = Physics.OverlapBoxNonAlloc(
                ownBounds.center,
                ownBounds.extents,
                placementOverlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider otherCollider = placementOverlapBuffer[hitIndex];
                if (otherCollider == null || !otherCollider.enabled || otherCollider.isTrigger)
                {
                    continue;
                }

                if (otherCollider.transform.IsChildOf(brickTransform))
                {
                    continue;
                }

                Vector3 separationDirection;
                float separationDistance;
                bool isPenetrating = Physics.ComputePenetration(
                    ownCollider,
                    ownCollider.transform.position,
                    ownCollider.transform.rotation,
                    otherCollider,
                    otherCollider.transform.position,
                    otherCollider.transform.rotation,
                    out separationDirection,
                    out separationDistance);

                if (!isPenetrating)
                {
                    continue;
                }

                if (separationDistance <= placementPenetrationEpsilon)
                {
                    continue;
                }

                Transform otherBrick = ResolveBrickRootFromHit(otherCollider.transform);
                if (IsAllowedPlaexLongVerticalInterlockOverlap(
                    brickTransform,
                    otherBrick,
                    separationDistance,
                    allowedLongVerticalInterlockBrick,
                    requireExistingJointForAllowedLongInterlock))
                {
                    continue;
                }

                if (IsAllowedPlaexSideInterlockOverlap(
                    brickTransform,
                    otherCollider,
                    separationDistance,
                    allowedSideInterlockBrick,
                    requireExistingJointForAllowedInterlock))
                {
                    continue;
                }

                if (logPlacementDebug && IsPlaexLongBrick(brickTransform))
                {
                    string otherName = otherBrick != null ? otherBrick.name : otherCollider.transform.name;
                    Debug.Log(
                        $"Blocking overlap detected: moving='{brickTransform.name}', other='{otherName}', penetration={separationDistance:F4}, ownCollider='{ownCollider.name}', otherCollider='{otherCollider.name}'.");
                }

                return true;
            }
        }

        return false;
    }

    private bool IsAllowedPlaexLongVerticalInterlockOverlap(
        Transform movingBrick,
        Transform otherBrick,
        float penetrationDistance,
        Transform allowedLongVerticalInterlockBrick = null,
        bool requireExistingJointForAllowedLongInterlock = true)
    {
        if (movingBrick == null ||
            otherBrick == null ||
            movingBrick == otherBrick ||
            !IsPlaexLongBrick(movingBrick) ||
            !IsPlaexLongBrick(otherBrick))
        {
            return false;
        }

        float maxAllowedOverlap = Mathf.Max(placementPenetrationEpsilon, plaexLongVerticalAllowedInterlockOverlap);
        if (penetrationDistance > maxAllowedOverlap)
        {
            return false;
        }

        Transform upperBrick = movingBrick.position.y >= otherBrick.position.y ? movingBrick : otherBrick;
        Transform lowerBrick = upperBrick == movingBrick ? otherBrick : movingBrick;

        float minHeightDelta = Mathf.Max(0.01f, plaexLongVerticalSupportMinHeightDelta);
        float verticalDelta = upperBrick.position.y - lowerBrick.position.y;
        if (verticalDelta <= minHeightDelta)
        {
            return false;
        }

        float horizontalDistance = Vector2.Distance(
            new Vector2(upperBrick.position.x, upperBrick.position.z),
            new Vector2(lowerBrick.position.x, lowerBrick.position.z));
        float maxHorizontalInterlockOffset = ResolvePlaexLongInterlockHorizontalTolerance();
        if (horizontalDistance > maxHorizontalInterlockOffset)
        {
            return false;
        }

        if (!ArePlaexLongBricksOrientationCompatible(upperBrick, lowerBrick))
        {
            return false;
        }

        if (allowedLongVerticalInterlockBrick != null &&
            allowedLongVerticalInterlockBrick != movingBrick &&
            allowedLongVerticalInterlockBrick != otherBrick)
        {
            return false;
        }

        if (!requireExistingJointForAllowedLongInterlock)
        {
            return true;
        }

        return HasFixedJointConnection(movingBrick, otherBrick);
    }

    private bool IsAllowedPlaexSideInterlockOverlap(
        Transform movingBrick,
        Collider otherCollider,
        float penetrationDistance,
        Transform allowedSideInterlockBrick = null,
        bool requireExistingJointForAllowedInterlock = true)
    {
        if (movingBrick == null ||
            otherCollider == null ||
            !IsPlaexSideSnapBrick(movingBrick))
        {
            return false;
        }

        Transform otherBrick = ResolveBrickRootFromHit(otherCollider.transform);
        if (otherBrick == null ||
            otherBrick == movingBrick ||
            !IsPlaexSideSnapBrick(otherBrick))
        {
            return false;
        }

        bool movingIsGreen = IsGreenPlaexSideTabBrick(movingBrick);
        bool otherIsGreen = IsGreenPlaexSideTabBrick(otherBrick);
        if (movingIsGreen == otherIsGreen)
        {
            return false;
        }

        float maxAllowedOverlap = Mathf.Max(placementPenetrationEpsilon, plaexSidePhysicsSnapAllowedInterlockOverlap);
        if (penetrationDistance > maxAllowedOverlap)
        {
            return false;
        }

        float maxVerticalOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxVerticalOffset);
        if (Mathf.Abs(movingBrick.position.y - otherBrick.position.y) > maxVerticalOffset)
        {
            return false;
        }

        float rotationTolerance = Mathf.Max(0f, plaexSidePhysicsSnapRotationToleranceDegrees);
        if (Quaternion.Angle(movingBrick.rotation, otherBrick.rotation) > rotationTolerance)
        {
            return false;
        }

        if (allowedSideInterlockBrick != null &&
            allowedSideInterlockBrick != movingBrick &&
            allowedSideInterlockBrick != otherBrick)
        {
            return false;
        }

        if (!requireExistingJointForAllowedInterlock)
        {
            return true;
        }

        return HasFixedJointConnection(movingBrick, otherBrick);
    }
}
