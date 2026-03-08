using System;
using System.Collections;
using UnityEngine;

public partial class ThreeDBrickSim
{
    private const float PlaexSideConnectorFacingTolerance = 0.95f;
    private const float PlaexLongCanonicalHalfLength = 2.5f;
    private const float PlaexSideCanonicalHalfExtent = 1f;
    private const PlaexSideHorizontalFace YellowSideCavityFace = PlaexSideHorizontalFace.PositiveZ;

    private enum PlaexSideBrickKind
    {
        Unsupported = 0,
        GreenLong = 1,
        OrangeLong = 2,
        YellowSide = 3
    }

    private enum PlaexSideConnectorKind
    {
        Tab = 0,
        Cavity = 1
    }

    private enum PlaexSideHorizontalFace
    {
        PositiveX = 0,
        NegativeX = 1,
        PositiveZ = 2,
        NegativeZ = 3
    }

    private struct PlaexSidePhysicsSnapCandidate
    {
        public Rigidbody connectedRigidbody;
        public Vector3 offset;
        public Vector3 anchorWorldPosition;
        public PlaexSideConnector movingConnector;
        public PlaexSideConnector candidateConnector;
        public float score;
    }

    private struct PlaexSideConnector
    {
        public Transform brickTransform;
        public Vector3 anchorWorldPosition;
        public Vector3 outwardNormal;
        public PlaexSideConnectorKind kind;
        public PlaexSideHorizontalFace face;
        public float clearanceRadius;
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

        if (!TryFindBestPlaexSideSnapCandidate(
            movingBrick,
            movingConnectors,
            out PlaexSidePhysicsSnapCandidate candidate))
        {
            return false;
        }

        return ApplyPlaexSidePhysicsSnap(movingBrick, movingRigidbody, candidate);
    }

    private bool TryFindBestPlaexSideSnapCandidate(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        out PlaexSidePhysicsSnapCandidate bestCandidate)
    {
        bestCandidate = default;
        bool found = false;
        float bestScore = float.PositiveInfinity;

        foreach (Transform candidateBrick in plannedWallBricks)
        {
            if (candidateBrick == null ||
                candidateBrick == movingBrick ||
                detachedWallBricks.Contains(candidateBrick) ||
                !IsPlaexSideSnapBrick(candidateBrick) ||
                !CanPlaexSideBrickTypesPotentiallyConnect(movingBrick, candidateBrick))
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

            if (!TryFindBestPlaexSideConnectionToCandidate(
                movingBrick,
                movingConnectors,
                candidateBrick,
                candidateConnectors,
                enforceOccupancyAndClearance: true,
                out Vector3 offset,
                out PlaexSideConnector movingConnector,
                out PlaexSideConnector candidateConnector,
                out float score))
            {
                continue;
            }

            Vector3 snappedPosition = movingBrick.position + offset;
            if (rejectOverlappingPlacement &&
                IsPlacementPoseBlocked(
                    movingBrick,
                    snappedPosition,
                    movingBrick.rotation,
                    allowedSideInterlockBrick: candidateBrick,
                    requireExistingJointForAllowedInterlock: false))
            {
                continue;
            }

            if (found && score >= bestScore)
            {
                continue;
            }

            found = true;
            bestScore = score;
            bestCandidate = new PlaexSidePhysicsSnapCandidate
            {
                connectedRigidbody = candidateRigidbody,
                offset = offset,
                anchorWorldPosition = candidateConnector.anchorWorldPosition,
                movingConnector = movingConnector,
                candidateConnector = candidateConnector,
                score = score
            };
        }

        if (!found && logPlacementDebug)
        {
            Debug.Log($"PLAEX side snap: no candidate found for moving='{movingBrick.name}'.");
        }

        return found;
    }

    private bool TryFindBestPlaexSideConnectionToCandidate(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        Transform candidateBrick,
        PlaexSideConnector[] candidateConnectors,
        bool enforceOccupancyAndClearance,
        out Vector3 bestOffset,
        out PlaexSideConnector bestMovingConnector,
        out PlaexSideConnector bestCandidateConnector,
        out float bestScore)
    {
        bestOffset = Vector3.zero;
        bestMovingConnector = default;
        bestCandidateConnector = default;
        bestScore = float.PositiveInfinity;

        if (movingBrick == null ||
            candidateBrick == null ||
            movingConnectors == null ||
            candidateConnectors == null ||
            movingConnectors.Length == 0 ||
            candidateConnectors.Length == 0 ||
            !ArePlaexSideBricksOrientationCompatible(movingBrick, candidateBrick))
        {
            return false;
        }

        float maxConnectorGap = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxConnectorGap);
        float maxLateralOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxLateralOffset);
        float maxVerticalOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxVerticalOffset);
        bool found = false;

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

            if (enforceOccupancyAndClearance)
            {
                if (IsPlaexSideConnectorOccupied(movingBrick, movingConnector, candidateBrick) ||
                    !HasPlaexSideConnectorLocalClearance(movingBrick, movingConnector, candidateBrick))
                {
                    continue;
                }
            }

            for (int candidateIndex = 0; candidateIndex < candidateConnectors.Length; candidateIndex++)
            {
                PlaexSideConnector candidateConnector = candidateConnectors[candidateIndex];
                if (!ArePlaexSideConnectorKindsCompatible(movingConnector.kind, candidateConnector.kind))
                {
                    continue;
                }

                float facingDot = Vector3.Dot(movingConnector.outwardNormal, candidateConnector.outwardNormal);
                if (facingDot > -PlaexSideConnectorFacingTolerance)
                {
                    continue;
                }

                if (enforceOccupancyAndClearance)
                {
                    if (IsPlaexSideConnectorOccupied(candidateBrick, candidateConnector, movingBrick) ||
                        !HasPlaexSideConnectorLocalClearance(candidateBrick, candidateConnector, movingBrick))
                    {
                        continue;
                    }
                }

                Vector3 rawOffset = candidateConnector.anchorWorldPosition - movingConnector.anchorWorldPosition;
                float rawConnectorGap = Mathf.Abs(Vector3.Dot(rawOffset, centerAxis));
                if (rawConnectorGap > maxConnectorGap)
                {
                    continue;
                }

                float rawLateralOffset = Mathf.Abs(Vector3.Dot(rawOffset, lateralAxis));
                if (rawLateralOffset > maxLateralOffset)
                {
                    continue;
                }

                float rawVerticalOffset = Mathf.Abs(rawOffset.y);
                if (rawVerticalOffset > maxVerticalOffset)
                {
                    continue;
                }

                float score = rawConnectorGap + rawLateralOffset + (rawVerticalOffset * 2f);
                if (found && score >= bestScore)
                {
                    continue;
                }

                found = true;
                bestScore = score;
                bestOffset = rawOffset;
                bestMovingConnector = movingConnector;
                bestCandidateConnector = candidateConnector;
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
        if (brickTransform == null)
        {
            return false;
        }

        PlaexSideBrickKind brickKind = ResolvePlaexSideBrickKind(brickTransform);
        if (brickKind == PlaexSideBrickKind.Unsupported)
        {
            return false;
        }

        switch (brickKind)
        {
            case PlaexSideBrickKind.YellowSide:
                connectors = new[]
                {
                    BuildPlaexSideConnector(brickTransform, Vector3.right * PlaexSideCanonicalHalfExtent, Vector3.right, PlaexSideHorizontalFace.PositiveX, ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.PositiveX)),
                    BuildPlaexSideConnector(brickTransform, Vector3.left * PlaexSideCanonicalHalfExtent, Vector3.left, PlaexSideHorizontalFace.NegativeX, ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.NegativeX)),
                    BuildPlaexSideConnector(brickTransform, Vector3.forward * PlaexSideCanonicalHalfExtent, Vector3.forward, PlaexSideHorizontalFace.PositiveZ, ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.PositiveZ)),
                    BuildPlaexSideConnector(brickTransform, Vector3.back * PlaexSideCanonicalHalfExtent, Vector3.back, PlaexSideHorizontalFace.NegativeZ, ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.NegativeZ))
                };
                return true;

            case PlaexSideBrickKind.GreenLong:
            case PlaexSideBrickKind.OrangeLong:
                PlaexSideConnectorKind connectorKind = brickKind == PlaexSideBrickKind.GreenLong
                    ? PlaexSideConnectorKind.Tab
                    : PlaexSideConnectorKind.Cavity;

                connectors = new[]
                {
                    BuildPlaexSideConnector(brickTransform, Vector3.right * PlaexLongCanonicalHalfLength, Vector3.right, PlaexSideHorizontalFace.PositiveX, connectorKind),
                    BuildPlaexSideConnector(brickTransform, Vector3.left * PlaexLongCanonicalHalfLength, Vector3.left, PlaexSideHorizontalFace.NegativeX, connectorKind)
                };
                return true;

            default:
                return false;
        }
    }

    private PlaexSideConnector BuildPlaexSideConnector(
        Transform brickTransform,
        Vector3 localAnchor,
        Vector3 localNormal,
        PlaexSideHorizontalFace face,
        PlaexSideConnectorKind kind)
    {
        return new PlaexSideConnector
        {
            brickTransform = brickTransform,
            anchorWorldPosition = brickTransform.TransformPoint(localAnchor),
            outwardNormal = brickTransform.TransformDirection(localNormal).normalized,
            kind = kind,
            face = face,
            clearanceRadius = Mathf.Max(0.05f, plaexSideConnectorLocalClearanceRadius)
        };
    }

    private bool ArePlaexSideBricksOrientationCompatible(Transform first, Transform second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        float toleranceDegrees = Mathf.Max(0f, plaexSidePhysicsSnapRotationToleranceDegrees);
        float cosineTolerance = Mathf.Cos(toleranceDegrees * Mathf.Deg2Rad);
        float upDot = Vector3.Dot(first.up.normalized, second.up.normalized);
        return upDot >= cosineTolerance;
    }

    private bool IsPlaexSideConnectorOccupied(
        Transform brickTransform,
        PlaexSideConnector connector,
        Transform ignoredConnectedBrick = null)
    {
        if (brickTransform == null)
        {
            return false;
        }

        FixedJoint[] joints = brickTransform.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            Transform connectedBrick = joint.connectedBody.transform;
            if (connectedBrick == null ||
                connectedBrick == ignoredConnectedBrick ||
                !IsPlaexSideSnapBrick(connectedBrick))
            {
                continue;
            }

            if (!TryGetCurrentPlaexSideConnectionPair(brickTransform, connectedBrick, out PlaexSideConnector ownConnector, out _))
            {
                continue;
            }

            if (ownConnector.face == connector.face)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPlaexSideConnectorLocalClearance(
        Transform brickTransform,
        PlaexSideConnector selectedConnector,
        Transform ignoredConnectedBrick = null)
    {
        if (brickTransform == null)
        {
            return false;
        }

        FixedJoint[] joints = brickTransform.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            Transform connectedBrick = joint.connectedBody.transform;
            if (connectedBrick == null ||
                connectedBrick == ignoredConnectedBrick ||
                !IsPlaexSideSnapBrick(connectedBrick))
            {
                continue;
            }

            if (!TryGetCurrentPlaexSideConnectionPair(brickTransform, connectedBrick, out PlaexSideConnector occupiedConnector, out _))
            {
                continue;
            }

            float minimumSeparation = occupiedConnector.clearanceRadius + selectedConnector.clearanceRadius;
            float anchorDistance = Vector3.Distance(occupiedConnector.anchorWorldPosition, selectedConnector.anchorWorldPosition);
            if (anchorDistance + 0.0001f < minimumSeparation)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetCurrentPlaexSideConnectionPair(
        Transform firstBrick,
        Transform secondBrick,
        out PlaexSideConnector firstConnector,
        out PlaexSideConnector secondConnector)
    {
        firstConnector = default;
        secondConnector = default;

        if (firstBrick == null ||
            secondBrick == null ||
            !TryBuildPlaexSideConnectors(firstBrick, out PlaexSideConnector[] firstConnectors) ||
            !TryBuildPlaexSideConnectors(secondBrick, out PlaexSideConnector[] secondConnectors))
        {
            return false;
        }

        return TryFindBestPlaexSideConnectionToCandidate(
            firstBrick,
            firstConnectors,
            secondBrick,
            secondConnectors,
            enforceOccupancyAndClearance: false,
            out _,
            out firstConnector,
            out secondConnector,
            out _);
    }

    private bool TryGetExistingLongToYellowSideConnection(
        Transform longBrick,
        Transform yellowSideBrick,
        bool requireExistingJoint,
        out PlaexSideConnector longConnector,
        out PlaexSideConnector yellowConnector)
    {
        longConnector = default;
        yellowConnector = default;

        if (longBrick == null ||
            yellowSideBrick == null ||
            ResolvePlaexSideBrickKind(yellowSideBrick) != PlaexSideBrickKind.YellowSide)
        {
            return false;
        }

        PlaexSideBrickKind longKind = ResolvePlaexSideBrickKind(longBrick);
        if (longKind != PlaexSideBrickKind.GreenLong && longKind != PlaexSideBrickKind.OrangeLong)
        {
            return false;
        }

        if (requireExistingJoint && !HasFixedJointConnection(longBrick, yellowSideBrick))
        {
            return false;
        }

        if (!TryGetCurrentPlaexSideConnectionPair(longBrick, yellowSideBrick, out longConnector, out yellowConnector))
        {
            return false;
        }

        return true;
    }

    private bool IsAllowedPlaexDirectSideInterlockOverlap(
        Transform movingBrick,
        Transform otherBrick,
        float penetrationDistance,
        Transform allowedSideInterlockBrick,
        bool requireExistingJointForAllowedInterlock)
    {
        if (movingBrick == null || otherBrick == null)
        {
            return false;
        }

        float maxAllowedOverlap = Mathf.Max(placementPenetrationEpsilon, plaexSidePhysicsSnapAllowedInterlockOverlap);
        if (penetrationDistance > maxAllowedOverlap)
        {
            return false;
        }

        if (!TryGetCurrentPlaexSideConnectionPair(movingBrick, otherBrick, out _, out _))
        {
            return false;
        }

        if (allowedSideInterlockBrick != null &&
            allowedSideInterlockBrick != movingBrick &&
            allowedSideInterlockBrick != otherBrick)
        {
            // Candidate evaluation can legitimately brush multiple compatible
            // side-interlock neighbors at once when a long brick bridges a span.
            if (requireExistingJointForAllowedInterlock)
            {
                return false;
            }
        }

        if (!requireExistingJointForAllowedInterlock)
        {
            return true;
        }

        return HasFixedJointConnection(movingBrick, otherBrick);
    }

    private bool IsAllowedPlaexPerpendicularLongCornerOverlap(
        Transform movingBrick,
        Transform otherBrick,
        float penetrationDistance,
        Transform allowedSideInterlockBrick,
        bool requireExistingJointForAllowedInterlock)
    {
        if (movingBrick == null ||
            otherBrick == null ||
            movingBrick == otherBrick)
        {
            return false;
        }

        PlaexSideBrickKind movingKind = ResolvePlaexSideBrickKind(movingBrick);
        PlaexSideBrickKind otherKind = ResolvePlaexSideBrickKind(otherBrick);
        if ((movingKind != PlaexSideBrickKind.GreenLong && movingKind != PlaexSideBrickKind.OrangeLong) ||
            (otherKind != PlaexSideBrickKind.GreenLong && otherKind != PlaexSideBrickKind.OrangeLong))
        {
            return false;
        }

        float maxAllowedOverlap = Mathf.Max(
            placementPenetrationEpsilon,
            Mathf.Min(plaexSidePhysicsSnapAllowedInterlockOverlap, plaexSidePerpendicularLongCornerAllowedOverlap));
        if (penetrationDistance > maxAllowedOverlap)
        {
            return false;
        }

        float maxVerticalOffset = Mathf.Max(0.001f, plaexSidePhysicsSnapMaxVerticalOffset);
        if (Mathf.Abs(movingBrick.position.y - otherBrick.position.y) > maxVerticalOffset)
        {
            return false;
        }

        if (!ArePerpendicularPlaexLongBricks(movingBrick, otherBrick))
        {
            return false;
        }

        Transform sharedYellowBrick = allowedSideInterlockBrick;
        if (sharedYellowBrick == null || ResolvePlaexSideBrickKind(sharedYellowBrick) != PlaexSideBrickKind.YellowSide)
        {
            return false;
        }

        if (!TryGetExistingLongToYellowSideConnection(
                movingBrick,
                sharedYellowBrick,
                requireExistingJoint: false,
                out _,
                out PlaexSideConnector movingYellowConnector) ||
            !TryGetExistingLongToYellowSideConnection(
                otherBrick,
                sharedYellowBrick,
                requireExistingJoint: true,
                out _,
                out PlaexSideConnector otherYellowConnector))
        {
            return false;
        }

        if (movingYellowConnector.face == otherYellowConnector.face)
        {
            return false;
        }

        if (!requireExistingJointForAllowedInterlock)
        {
            return true;
        }

        return HasFixedJointConnection(otherBrick, sharedYellowBrick);
    }

    private bool ArePerpendicularPlaexLongBricks(Transform firstBrick, Transform secondBrick)
    {
        if (!TryGetPlaexLongHorizontalAxis(firstBrick, out Vector3 firstAxis) ||
            !TryGetPlaexLongHorizontalAxis(secondBrick, out Vector3 secondAxis))
        {
            return false;
        }

        float axisDot = Mathf.Abs(Vector3.Dot(firstAxis, secondAxis));
        return axisDot <= 0.25f;
    }

    private bool TryGetPlaexLongHorizontalAxis(Transform brickTransform, out Vector3 axis)
    {
        axis = Vector3.zero;
        if (!TryBuildPlaexSideConnectors(brickTransform, out PlaexSideConnector[] connectors) || connectors.Length < 2)
        {
            return false;
        }

        Vector3 anchorDelta = connectors[0].anchorWorldPosition - connectors[1].anchorWorldPosition;
        axis = Vector3.ProjectOnPlane(anchorDelta, Vector3.up);
        if (axis.sqrMagnitude <= 0.000001f)
        {
            axis = Vector3.zero;
            return false;
        }

        axis.Normalize();
        return true;
    }

    private static bool ArePlaexSideConnectorKindsCompatible(PlaexSideConnectorKind firstKind, PlaexSideConnectorKind secondKind)
    {
        return firstKind != secondKind;
    }

    private static PlaexSideConnectorKind ResolveYellowSideConnectorKind(PlaexSideHorizontalFace face)
    {
        return face == YellowSideCavityFace
            ? PlaexSideConnectorKind.Cavity
            : PlaexSideConnectorKind.Tab;
    }

    private static bool HasPlaexSideTabs(Transform brickTransform)
    {
        PlaexSideBrickKind brickKind = ResolvePlaexSideBrickKind(brickTransform);
        return brickKind == PlaexSideBrickKind.GreenLong || brickKind == PlaexSideBrickKind.YellowSide;
    }

    private static bool HasPlaexSideCavities(Transform brickTransform)
    {
        PlaexSideBrickKind brickKind = ResolvePlaexSideBrickKind(brickTransform);
        return brickKind == PlaexSideBrickKind.OrangeLong || brickKind == PlaexSideBrickKind.YellowSide;
    }

    private static bool CanPlaexSideBrickTypesPotentiallyConnect(Transform firstBrick, Transform secondBrick)
    {
        if (!IsPlaexSideSnapBrick(firstBrick) || !IsPlaexSideSnapBrick(secondBrick))
        {
            return false;
        }

        return
            (HasPlaexSideTabs(firstBrick) && HasPlaexSideCavities(secondBrick)) ||
            (HasPlaexSideCavities(firstBrick) && HasPlaexSideTabs(secondBrick));
    }

    private static PlaexSideBrickKind ResolvePlaexSideBrickKind(Transform brickTransform)
    {
        if (IsGreenPlaexSideTabBrick(brickTransform))
        {
            return PlaexSideBrickKind.GreenLong;
        }

        if (IsOrangePlaexSideCavityBrick(brickTransform))
        {
            return PlaexSideBrickKind.OrangeLong;
        }

        if (IsYellowPlaexSideCavityBrick(brickTransform))
        {
            return PlaexSideBrickKind.YellowSide;
        }

        return PlaexSideBrickKind.Unsupported;
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
