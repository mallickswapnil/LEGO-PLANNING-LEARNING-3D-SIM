using System;
using System.Collections;
using UnityEngine;

public partial class ThreeDBrickSim
{
    private const string PlaexLongModelNodeName = "PLAEXLong_5x2x2";
    private const string PlaexSideModelNodeName = "PLAEXSide_2x2x2";

    private struct PlaexLongPhysicsSnapCandidate
    {
        public Rigidbody lowerRigidbody;
        public float snappedCenterY;
        public float score;
    }

    private void TrySnapPlaexLongBrickWithPhysics(Transform movingBrick)
    {
        if (!enablePlaexLongPhysicsSnap || !IsPlaexLongBrick(movingBrick))
        {
            return;
        }

        Rigidbody movingRigidbody = movingBrick.GetComponent<Rigidbody>();
        if (movingRigidbody == null)
        {
            return;
        }

        if (!TryGetPlaexLongBodyBounds(movingBrick, out Bounds movingBodyBounds))
        {
            return;
        }

        if (!TryFindBestPlaexLongSnapCandidate(movingBrick, movingBodyBounds, out PlaexLongPhysicsSnapCandidate candidate))
        {
            return;
        }

        ApplyPlaexLongPhysicsSnap(movingBrick, movingRigidbody, candidate);
    }

    private bool TryFindBestPlaexLongSnapCandidate(
        Transform movingBrick,
        Bounds movingBodyBounds,
        out PlaexLongPhysicsSnapCandidate bestCandidate)
    {
        bestCandidate = default;
        bool found = false;
        float maxHorizontalInterlockOffset = ResolvePlaexLongInterlockHorizontalTolerance();

        float movingBodyMinOffset = movingBodyBounds.min.y - movingBrick.position.y;

        foreach (Transform lowerBrick in plannedWallBricks)
        {
            if (lowerBrick == null ||
                lowerBrick == movingBrick ||
                detachedWallBricks.Contains(lowerBrick) ||
                !IsPlaexLongBrick(lowerBrick))
            {
                continue;
            }

            if (!ArePlaexLongBricksOrientationCompatible(movingBrick, lowerBrick))
            {
                continue;
            }

            if (!TryGetPlaexLongBodyBounds(lowerBrick, out Bounds lowerBodyBounds))
            {
                continue;
            }

            float lowerToMovingHorizontalDistance = Vector2.Distance(
                new Vector2(movingBrick.position.x, movingBrick.position.z),
                new Vector2(lowerBrick.position.x, lowerBrick.position.z));

            if (lowerToMovingHorizontalDistance > maxHorizontalInterlockOffset)
            {
                continue;
            }

            float snappedCenterY = lowerBodyBounds.max.y - movingBodyMinOffset;
            float verticalOffset = snappedCenterY - movingBrick.position.y;
            float maxVerticalSnapOffset = plaexLongPhysicsSnapMaxVerticalOffset;
            if (Mathf.Abs(verticalOffset) > maxVerticalSnapOffset)
            {
                continue;
            }

            // Snap only when moving brick is above the candidate lower brick.
            if (movingBodyBounds.center.y <= lowerBodyBounds.center.y)
            {
                continue;
            }

            if (rejectOverlappingPlacement)
            {
                Vector3 snappedPosition = movingBrick.position;
                snappedPosition.y = snappedCenterY;
                if (IsPlaexLongSnapPoseBlocked(
                    movingBrick,
                    snappedPosition,
                    movingBrick.rotation,
                    lowerBrick))
                {
                    continue;
                }
            }

            Rigidbody lowerRigidbody = lowerBrick.GetComponent<Rigidbody>();
            if (lowerRigidbody == null)
            {
                continue;
            }

            float score = -lowerToMovingHorizontalDistance - Mathf.Abs(verticalOffset);
            if (found && score <= bestCandidate.score)
            {
                continue;
            }

            found = true;
            bestCandidate = new PlaexLongPhysicsSnapCandidate
            {
                lowerRigidbody = lowerRigidbody,
                snappedCenterY = snappedCenterY,
                score = score
            };
        }

        return found;
    }

    private bool IsPlaexLongSnapPoseBlocked(
        Transform movingBrick,
        Vector3 snappedPosition,
        Quaternion snappedRotation,
        Transform allowedLowerBrick)
    {
        if (!IsPlacementPoseBlocked(
            movingBrick,
            snappedPosition,
            snappedRotation,
            allowedLongVerticalInterlockBrick: allowedLowerBrick,
            requireExistingJointForAllowedLongInterlock: false))
        {
            return false;
        }

        if (!enablePlaexSidePhysicsSnap ||
            !IsPlaexSideSnapBrick(movingBrick))
        {
            return true;
        }

        Vector3 originalPosition = movingBrick.position;
        Quaternion originalRotation = movingBrick.rotation;
        movingBrick.SetPositionAndRotation(snappedPosition, snappedRotation);
        Physics.SyncTransforms();

        try
        {
            if (!TryBuildPlaexSideConnectors(movingBrick, out PlaexSideConnector[] movingConnectors))
            {
                return true;
            }

            foreach (Transform placedBrick in plannedWallBricks)
            {
                if (placedBrick == null ||
                    placedBrick == movingBrick ||
                    detachedWallBricks.Contains(placedBrick) ||
                    !IsPlaexSideSnapBrick(placedBrick) ||
                    !CanPlaexSideBrickTypesPotentiallyConnect(movingBrick, placedBrick))
                {
                    continue;
                }

                if (!TryBuildPlaexSideConnectors(placedBrick, out PlaexSideConnector[] placedConnectors))
                {
                    continue;
                }

                if (!TryFindBestPlaexSideConnectionToCandidate(
                    movingBrick,
                    movingConnectors,
                    placedBrick,
                    placedConnectors,
                    enforceOccupancyAndClearance: true,
                    out _,
                    out _,
                    out _,
                    out _))
                {
                    continue;
                }

                if (!IsPlacementPoseBlocked(
                    movingBrick,
                    snappedPosition,
                    snappedRotation,
                    allowedSideInterlockBrick: placedBrick,
                    requireExistingJointForAllowedInterlock: false))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            movingBrick.SetPositionAndRotation(originalPosition, originalRotation);
            Physics.SyncTransforms();
        }
    }

    private float ResolvePlaexLongInterlockHorizontalTolerance()
    {
        float legacyTolerance = Mathf.Max(0.001f, plaexLongPhysicsSnapMaxHorizontalOffset);
        float strictTolerance = Mathf.Max(0.001f, plaexLongInterlockHorizontalTolerance);
        return Mathf.Min(legacyTolerance, strictTolerance);
    }

    private void ApplyPlaexLongPhysicsSnap(
        Transform movingBrick,
        Rigidbody movingRigidbody,
        PlaexLongPhysicsSnapCandidate candidate)
    {
        if (movingBrick == null || movingRigidbody == null || candidate.lowerRigidbody == null)
        {
            return;
        }

        RemoveExistingSnapJoints(movingBrick);

        float sourceY = movingBrick.position.y;
        Vector3 snappedPosition = movingBrick.position;
        snappedPosition.y = candidate.snappedCenterY;

        if (logPlacementDebug)
        {
            Transform lowerTransform = candidate.lowerRigidbody != null ? candidate.lowerRigidbody.transform : null;
            string lowerName = lowerTransform != null ? lowerTransform.name : "null";
            float deltaY = candidate.snappedCenterY - sourceY;
            Debug.Log(
                $"PLAEX long snap candidate: moving='{movingBrick.name}', lower='{lowerName}', sourceY={sourceY:F4}, snappedCenterY={candidate.snappedCenterY:F4}, deltaY={deltaY:F4}.");
        }

        if (!movingRigidbody.isKinematic)
        {
            movingRigidbody.linearVelocity = Vector3.zero;
            movingRigidbody.angularVelocity = Vector3.zero;
        }

        movingRigidbody.isKinematic = true;
        movingRigidbody.useGravity = false;
        movingRigidbody.position = snappedPosition;

        Physics.SyncTransforms();

        if (logPlacementDebug)
        {
            Debug.Log($"PLAEX long snap applied: moving='{movingBrick.name}', finalY={movingRigidbody.position.y:F4}.");
        }

        FixedJoint snapJoint = movingBrick.gameObject.AddComponent<FixedJoint>();
        snapJoint.connectedBody = candidate.lowerRigidbody;
        snapJoint.enableCollision = false;
        snapJoint.breakForce = plaexLongPhysicsSnapJointBreakForce;
        snapJoint.breakTorque = plaexLongPhysicsSnapJointBreakTorque;

        StartCoroutine(RestoreDynamicAfterPlaexSnapInsertion(movingRigidbody));
    }

    private IEnumerator RestoreDynamicAfterPlaexSnapInsertion(Rigidbody movingRigidbody)
    {
        int stabilizationSteps = Mathf.Max(1, plaexLongSnapStabilizationFixedSteps);
        for (int i = 0; i < stabilizationSteps; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        if (movingRigidbody != null)
        {
            bool keepPlaexKinematic =
                enablePlaexLongPhysicsSnap &&
                keepPlacedPlaexLongBricksKinematic &&
                IsPlaexLongBrick(movingRigidbody.transform);

            if (keepPlaexKinematic)
            {
                movingRigidbody.isKinematic = true;
                movingRigidbody.useGravity = false;
                movingRigidbody.Sleep();
                yield break;
            }

            ConfigureBrickRigidbodyForSimulation(movingRigidbody);
            if (!movingRigidbody.isKinematic)
            {
                movingRigidbody.linearVelocity = Vector3.zero;
                movingRigidbody.angularVelocity = Vector3.zero;
            }

            movingRigidbody.WakeUp();
        }
    }

    private bool TryGetPlaexLongBodyBounds(Transform brickTransform, out Bounds bodyBounds)
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

            if (renderer.transform.name.StartsWith(PlaexLongModelNodeName, StringComparison.OrdinalIgnoreCase) ||
                renderer.transform.name.StartsWith(PlaexSideModelNodeName, StringComparison.OrdinalIgnoreCase))
            {
                bodyBounds = renderer.bounds;
                return true;
            }
        }

        // Fallback: use largest renderer volume, which is typically the long body mesh.
        int largestIndex = -1;
        float largestVolume = float.NegativeInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Vector3 size = renderer.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume <= largestVolume)
            {
                continue;
            }

            largestVolume = volume;
            largestIndex = i;
        }

        if (largestIndex < 0)
        {
            return false;
        }

        bodyBounds = renderers[largestIndex].bounds;
        return true;
    }

    private bool ArePlaexLongBricksOrientationCompatible(Transform first, Transform second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        float angle = Quaternion.Angle(first.rotation, second.rotation);
        return angle <= plaexLongPhysicsSnapRotationToleranceDegrees;
    }

    private static bool IsPlaexLongBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return false;
        }

        string brickName = brickTransform.name;
        return
            string.Equals(brickName, "GreenLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(brickName, "OrangePlaexLongBrick", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(brickName, "YellowPlaexLongBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryGreenPlaexLongBrick_", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryOrangePlaexLongBrick_", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryYellowPlaexLongBrick_", StringComparison.OrdinalIgnoreCase) ||
            IsYellowPlaexSideCavityBrick(brickTransform);
    }
}
