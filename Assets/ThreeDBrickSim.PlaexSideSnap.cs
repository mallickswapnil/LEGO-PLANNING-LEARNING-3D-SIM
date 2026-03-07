using System;
using System.Collections;
using UnityEngine;

public partial class ThreeDBrickSim
{
    private const float PlaexSideConnectorFacingTolerance = 0.95f;

    private struct PlaexSidePhysicsSnapCandidate
    {
        public Rigidbody connectedRigidbody;
        public Vector3 offset;
        public Vector3 anchorWorldPosition;
        public float score;
    }

    private struct PlaexSideConnector
    {
        public Vector3 anchorWorldPosition;
        public Vector3 outwardNormal;
    }

    private bool TrySnapPlaexSideBrickWithPhysics(Transform movingBrick)
    {
        if (!enablePlaexSidePhysicsSnap || !IsPlaexSideSnapBrick(movingBrick))
        {
            return false;
        }

        Rigidbody movingRigidbody = movingBrick.GetComponent<Rigidbody>();
        if (movingRigidbody == null)
        {
            return false;
        }

        if (!TryBuildPlaexSideConnectors(movingBrick, out PlaexSideConnector[] movingConnectors))
        {
            return false;
        }

        bool movingHasTabs = IsGreenPlaexSideTabBrick(movingBrick);
        if (!TryFindBestPlaexSideSnapCandidate(
            movingBrick,
            movingConnectors,
            movingHasTabs,
            out PlaexSidePhysicsSnapCandidate candidate))
        {
            return false;
        }

        return ApplyPlaexSidePhysicsSnap(movingBrick, movingRigidbody, candidate);
    }

    private bool TryFindBestPlaexSideSnapCandidate(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        bool movingHasTabs,
        out PlaexSidePhysicsSnapCandidate bestCandidate)
    {
        bestCandidate = default;
        bool found = false;
        float maxConnectorGap = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxConnectorGap);
        float nominalCenterSpacing = Mathf.Max(0.01f, plaexSidePhysicsSnapNominalCenterSpacing);
        float centerSpacingTolerance = Mathf.Max(0.001f, plaexSidePhysicsSnapCenterSpacingTolerance);

        foreach (Transform candidateBrick in plannedWallBricks)
        {
            if (candidateBrick == null ||
                candidateBrick == movingBrick ||
                detachedWallBricks.Contains(candidateBrick) ||
                !IsPlaexSideSnapBrick(candidateBrick))
            {
                continue;
            }

            bool candidateHasTabs = IsGreenPlaexSideTabBrick(candidateBrick);
            if (candidateHasTabs == movingHasTabs)
            {
                continue;
            }

            Rigidbody candidateRigidbody = candidateBrick.GetComponent<Rigidbody>();
            if (candidateRigidbody == null)
            {
                continue;
            }

            if (!TryBuildPlaexSideConnectors(candidateBrick, out PlaexSideConnector[] candidateConnectors))
            {
                continue;
            }

            if (!ArePlaexSideBricksOrientationCompatible(
                movingBrick,
                candidateBrick,
                movingConnectors,
                candidateConnectors))
            {
                continue;
            }

            for (int movingIndex = 0; movingIndex < movingConnectors.Length; movingIndex++)
            {
                PlaexSideConnector movingConnector = movingConnectors[movingIndex];
                if (!TryBuildWorldHorizontalSnapFrame(
                    movingConnector.outwardNormal,
                    out Vector3 centerAxis,
                    out Vector3 lateralAxis))
                {
                    continue;
                }

                float centerVerticalOffset = Mathf.Abs(movingBrick.position.y - candidateBrick.position.y);
                if (centerVerticalOffset > plaexSidePhysicsSnapMaxVerticalOffset)
                {
                    continue;
                }

                for (int candidateIndex = 0; candidateIndex < candidateConnectors.Length; candidateIndex++)
                {
                    PlaexSideConnector candidateConnector = candidateConnectors[candidateIndex];
                    float facingDot = Vector3.Dot(movingConnector.outwardNormal, candidateConnector.outwardNormal);
                    if (facingDot > -PlaexSideConnectorFacingTolerance)
                    {
                        continue;
                    }

                    Vector3 centerDelta = candidateBrick.position - movingBrick.position;
                    float centerDistanceAlongAxis = Mathf.Abs(Vector3.Dot(centerDelta, centerAxis));
                    float centerSpacingOffset = Mathf.Abs(centerDistanceAlongAxis - nominalCenterSpacing);
                    if (centerSpacingOffset > centerSpacingTolerance)
                    {
                        continue;
                    }

                    Vector3 rawOffset = candidateConnector.anchorWorldPosition - movingConnector.anchorWorldPosition;
                    float centerAxisOffset = Vector3.Dot(rawOffset, centerAxis);
                    float rawConnectorGap = Mathf.Abs(centerAxisOffset);
                    if (rawConnectorGap > maxConnectorGap)
                    {
                        continue;
                    }

                    float lateralAxisOffset = Vector3.Dot(rawOffset, lateralAxis);
                    float rawLateralOffset = Mathf.Abs(lateralAxisOffset);
                    if (rawLateralOffset > plaexSidePhysicsSnapMaxLateralOffset)
                    {
                        continue;
                    }

                    float verticalOffset = rawOffset.y;
                    float rawVerticalOffset = Mathf.Abs(verticalOffset);
                    if (rawVerticalOffset > plaexSidePhysicsSnapMaxVerticalOffset)
                    {
                        continue;
                    }

                    // Keep nominal center spacing fixed; only correct lateral and vertical drift.
                    Vector3 offset =
                        (lateralAxis * lateralAxisOffset) +
                        (Vector3.up * verticalOffset);

                    float scoreGap = rawConnectorGap;
                    if (rejectOverlappingPlacement &&
                        IsPlacementPoseBlocked(
                            movingBrick,
                            movingBrick.position + offset,
                            movingBrick.rotation,
                            candidateBrick,
                            requireExistingJointForAllowedInterlock: false))
                    {
                        continue;
                    }

                    float score = -(scoreGap + rawLateralOffset + (rawVerticalOffset * 2f));
                    if (found && score <= bestCandidate.score)
                    {
                        continue;
                    }

                    found = true;
                    bestCandidate = new PlaexSidePhysicsSnapCandidate
                    {
                        connectedRigidbody = candidateRigidbody,
                        offset = offset,
                        anchorWorldPosition = candidateConnector.anchorWorldPosition,
                        score = score
                    };
                }
            }
        }

        return found;
    }

    private bool ApplyPlaexSidePhysicsSnap(
        Transform movingBrick,
        Rigidbody movingRigidbody,
        PlaexSidePhysicsSnapCandidate candidate)
    {
        if (movingBrick == null || movingRigidbody == null || candidate.connectedRigidbody == null)
        {
            return false;
        }

        RemoveExistingSnapJoints(movingBrick);

        Vector3 sourcePosition = movingBrick.position;
        Vector3 finalSnappedPosition = movingBrick.position + candidate.offset;
        Transform candidateBrick = candidate.connectedRigidbody.transform;
        if (logPlacementDebug)
        {
            string connectedName = candidateBrick != null ? candidateBrick.name : "null";
            Vector3 appliedDelta = finalSnappedPosition - sourcePosition;
            Debug.Log(
                $"PLAEX side snap candidate: moving='{movingBrick.name}', connected='{connectedName}', sourcePosition={sourcePosition}, appliedDelta={appliedDelta}, finalPosition={finalSnappedPosition}.");
        }

        if (rejectOverlappingPlacement &&
            IsPlacementPoseBlocked(
                movingBrick,
                finalSnappedPosition,
                movingBrick.rotation,
                candidateBrick,
                requireExistingJointForAllowedInterlock: false))
        {
            return false;
        }

        if (!movingRigidbody.isKinematic)
        {
            movingRigidbody.linearVelocity = Vector3.zero;
            movingRigidbody.angularVelocity = Vector3.zero;
        }

        movingRigidbody.isKinematic = true;
        movingRigidbody.useGravity = false;

        if (!plaexSidePhysicsSnapUseInsertionMotion)
        {
            movingRigidbody.position = finalSnappedPosition;
            Physics.SyncTransforms();
            AttachPlaexSideSnapJoint(movingBrick, candidate.connectedRigidbody, candidate.anchorWorldPosition);
            if (logPlacementDebug)
            {
                Debug.Log(
                    $"PLAEX side snap applied (direct): moving='{movingBrick.name}', finalPosition={movingRigidbody.position}.");
            }
            StartCoroutine(RestoreDynamicAfterPlaexSnapInsertion(movingRigidbody));
            return false;
        }

        Vector3 insertionStartPosition = ResolvePlaexSideInsertionStartPosition(movingBrick, finalSnappedPosition);
        if (logPlacementDebug)
        {
            Debug.Log(
                $"PLAEX side insertion start: moving='{movingBrick.name}', start={insertionStartPosition}, target={finalSnappedPosition}.");
        }

        movingRigidbody.position = insertionStartPosition;
        Physics.SyncTransforms();

        activePlaexSideInsertionCount++;
        StartCoroutine(
            AnimatePlaexSideInsertion(
                movingBrick,
                movingRigidbody,
                candidate.connectedRigidbody,
                candidate.anchorWorldPosition,
                insertionStartPosition,
                finalSnappedPosition));

        return true;
    }

    private IEnumerator AnimatePlaexSideInsertion(
        Transform movingBrick,
        Rigidbody movingRigidbody,
        Rigidbody connectedRigidbody,
        Vector3 anchorWorldPosition,
        Vector3 startPosition,
        Vector3 finalPosition)
    {
        try
        {
            int insertionSteps = Mathf.Max(1, plaexSideSnapInsertionFixedSteps);
            for (int step = 1; step <= insertionSteps; step++)
            {
                if (movingRigidbody == null || connectedRigidbody == null)
                {
                    yield break;
                }

                float t = step / (float)insertionSteps;
                Vector3 stepPosition = Vector3.Lerp(startPosition, finalPosition, t);
                movingRigidbody.position = stepPosition;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }

            if (movingBrick == null || movingRigidbody == null || connectedRigidbody == null)
            {
                yield break;
            }

            movingRigidbody.position = finalPosition;
            Physics.SyncTransforms();

            if (rejectOverlappingPlacement &&
                IsPlacementPoseBlocked(
                    movingBrick,
                    finalPosition,
                    movingBrick.rotation,
                    connectedRigidbody.transform,
                    requireExistingJointForAllowedInterlock: false))
            {
                ConfigureBrickRigidbodyForSimulation(movingRigidbody);
                yield break;
            }

            AttachPlaexSideSnapJoint(movingBrick, connectedRigidbody, anchorWorldPosition);
            if (logPlacementDebug)
            {
                Debug.Log(
                    $"PLAEX side snap applied (insertion): moving='{movingBrick.name}', finalPosition={movingRigidbody.position}, connected='{connectedRigidbody.transform.name}'.");
            }
            StartCoroutine(RestoreDynamicAfterPlaexSnapInsertion(movingRigidbody));
        }
        finally
        {
            activePlaexSideInsertionCount = Mathf.Max(0, activePlaexSideInsertionCount - 1);
        }
    }

    private Vector3 ResolvePlaexSideInsertionStartPosition(Transform movingBrick, Vector3 finalPosition)
    {
        Vector3 upAxis = Vector3.up;

        float insertionLift = Mathf.Max(0.1f, plaexSidePhysicsSnapInsertionLift);
        float requiredClearanceLift = 0.1f;

        Bounds overallBounds = GetWorldBounds(movingBrick);
        if (overallBounds.size.y > 0.0001f)
        {
            requiredClearanceLift = Mathf.Max(requiredClearanceLift, overallBounds.size.y + 0.15f);
        }

        if (TryGetPlaexLongBodyBounds(movingBrick, out Bounds bodyBounds))
        {
            requiredClearanceLift = Mathf.Max(requiredClearanceLift, bodyBounds.size.y + 0.15f);
        }

        insertionLift = Mathf.Max(insertionLift, requiredClearanceLift);
        return finalPosition + (upAxis * insertionLift);
    }

    private static bool TryBuildWorldHorizontalSnapFrame(
        Vector3 connectorOutwardNormal,
        out Vector3 centerAxis,
        out Vector3 lateralAxis)
    {
        centerAxis = Vector3.ProjectOnPlane(connectorOutwardNormal, Vector3.up);
        if (centerAxis.sqrMagnitude <= 0.000001f)
        {
            lateralAxis = Vector3.zero;
            return false;
        }

        centerAxis.Normalize();
        lateralAxis = Vector3.Cross(Vector3.up, centerAxis);
        if (lateralAxis.sqrMagnitude <= 0.000001f)
        {
            centerAxis = Vector3.zero;
            lateralAxis = Vector3.zero;
            return false;
        }

        lateralAxis.Normalize();
        return true;
    }

    private void AttachPlaexSideSnapJoint(
        Transform movingBrick,
        Rigidbody connectedRigidbody,
        Vector3 anchorWorldPosition)
    {
        if (movingBrick == null || connectedRigidbody == null)
        {
            return;
        }

        FixedJoint snapJoint = movingBrick.gameObject.AddComponent<FixedJoint>();
        snapJoint.connectedBody = connectedRigidbody;
        snapJoint.autoConfigureConnectedAnchor = false;
        snapJoint.anchor = movingBrick.InverseTransformPoint(anchorWorldPosition);
        snapJoint.connectedAnchor = connectedRigidbody.transform.InverseTransformPoint(anchorWorldPosition);
        snapJoint.enableCollision = false;
        snapJoint.breakForce = plaexSidePhysicsSnapJointBreakForce;
        snapJoint.breakTorque = plaexSidePhysicsSnapJointBreakTorque;
    }

    private bool TryBuildPlaexSideConnectors(Transform brickTransform, out PlaexSideConnector[] connectors)
    {
        connectors = null;
        if (!TryGetPlaexLongBodyRenderer(brickTransform, out Renderer bodyRenderer))
        {
            return false;
        }

        Transform bodyTransform = bodyRenderer.transform;
        Bounds localBounds = bodyRenderer.localBounds;
        float halfLongExtent;
        Vector3 localLongAxis;
        if (localBounds.extents.x >= localBounds.extents.z)
        {
            halfLongExtent = localBounds.extents.x;
            localLongAxis = Vector3.right;
        }
        else
        {
            halfLongExtent = localBounds.extents.z;
            localLongAxis = Vector3.forward;
        }

        if (halfLongExtent <= 0.0001f)
        {
            return false;
        }

        Vector3 positiveLocal = localBounds.center + (localLongAxis * halfLongExtent);
        Vector3 negativeLocal = localBounds.center - (localLongAxis * halfLongExtent);
        Vector3 positiveNormal = bodyTransform.TransformDirection(localLongAxis).normalized;

        connectors = new PlaexSideConnector[2];
        connectors[0] = new PlaexSideConnector
        {
            anchorWorldPosition = bodyTransform.TransformPoint(positiveLocal),
            outwardNormal = positiveNormal
        };
        connectors[1] = new PlaexSideConnector
        {
            anchorWorldPosition = bodyTransform.TransformPoint(negativeLocal),
            outwardNormal = -positiveNormal
        };

        return true;
    }

    private bool ArePlaexSideBricksOrientationCompatible(
        Transform first,
        Transform second,
        PlaexSideConnector[] firstConnectors,
        PlaexSideConnector[] secondConnectors)
    {
        if (first == null || second == null)
        {
            return false;
        }

        float toleranceDegrees = Mathf.Max(0f, plaexSidePhysicsSnapRotationToleranceDegrees);
        float cosineTolerance = Mathf.Cos(toleranceDegrees * Mathf.Deg2Rad);

        float upDot = Vector3.Dot(first.up.normalized, second.up.normalized);
        if (upDot < cosineTolerance)
        {
            return false;
        }

        if (firstConnectors == null ||
            secondConnectors == null ||
            firstConnectors.Length == 0 ||
            secondConnectors.Length == 0)
        {
            return false;
        }

        float longAxisDot = Mathf.Abs(Vector3.Dot(
            firstConnectors[0].outwardNormal.normalized,
            secondConnectors[0].outwardNormal.normalized));
        return longAxisDot >= cosineTolerance;
    }

    private static bool TryGetPlaexLongBodyRenderer(Transform brickTransform, out Renderer bodyRenderer)
    {
        bodyRenderer = null;
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
                bodyRenderer = renderer;
                return true;
            }
        }

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

        bodyRenderer = renderers[largestIndex];
        return bodyRenderer != null;
    }

    private static bool IsPlaexSideSnapBrick(Transform brickTransform)
    {
        return
            IsGreenPlaexSideTabBrick(brickTransform) ||
            IsOrangePlaexSideCavityBrick(brickTransform) ||
            IsYellowPlaexSideCavityBrick(brickTransform);
    }

    private static bool IsGreenPlaexSideTabBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return false;
        }

        string brickName = brickTransform.name;
        return
            string.Equals(brickName, "GreenLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryGreenPlaexLongBrick_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrangePlaexSideCavityBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return false;
        }

        string brickName = brickTransform.name;
        return
            string.Equals(brickName, "OrangePlaexLongBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryOrangePlaexLongBrick_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYellowPlaexSideCavityBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return false;
        }

        string brickName = brickTransform.name;
        return
            string.Equals(brickName, "YellowLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryYellowPlaexSideBrick_", StringComparison.OrdinalIgnoreCase);
    }
}
