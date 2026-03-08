using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ThreeDBrickSim
{
    private const float PlaexSideConnectorFacingTolerance = 0.95f;
    private const float PlaexLongHalfLength = 2.5f;
    private const float PlaexSideHalfExtent = 1f;
    private const string PlaexSideSnapPointContainerName = "__PlaexSideSnapPoints";
    private const string PlaexSideSnapPointDefinitionsResourcePath = "plaex_side_snap_points";
    // The imported yellow side brick exposes its single cavity on local +Z.
    private const PlaexSideHorizontalFace YellowSideCavityFace = PlaexSideHorizontalFace.PositiveZ;
    private static bool plaexSideSnapPointDefinitionsLoaded;
    private static Dictionary<PlaexSideSnapBrickKind, PlaexSideSnapPointDefinition[]> plaexSideSnapPointDefinitionsByKind;

    private enum PlaexSideSnapBrickKind
    {
        Unknown = 0,
        GreenLong = 1,
        OrangeLong = 2,
        YellowSide = 3
    }

    private enum PlaexSideConnectorKind
    {
        Tab = 1,
        Cavity = 2
    }

    [Flags]
    private enum PlaexSideConnectorKindMask
    {
        None = 0,
        Tab = 1,
        Cavity = 2
    }

    private enum PlaexSideHorizontalFace
    {
        PositiveX = 0,
        NegativeX = 1,
        PositiveZ = 2,
        NegativeZ = 3
    }

    [Serializable]
    private sealed class PlaexSideSnapPointDefinitionFile
    {
        public PlaexSideBrickSnapPointDefinition[] bricks;
    }

    [Serializable]
    private sealed class PlaexSideBrickSnapPointDefinition
    {
        public string brickKind;
        public PlaexSideSnapPointDefinition[] points;
    }

    [Serializable]
    private sealed class PlaexSideSnapPointDefinition
    {
        public string name;
        public string connectorKind;
        public PlaexSerializedVector3 localPosition;
        public PlaexSerializedVector3 localForward;
    }

    [Serializable]
    private struct PlaexSerializedVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }
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
        public PlaexSideConnectorKind connectorKind;
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
        int compatibleCandidateCount = 0;

        foreach (Transform candidateBrick in plannedWallBricks)
        {
            if (candidateBrick == null ||
                candidateBrick == movingBrick ||
                detachedWallBricks.Contains(candidateBrick) ||
                !IsPlaexSideSnapBrick(candidateBrick))
            {
                continue;
            }

            if (!ArePlaexSideSnapBricksCompatible(movingBrick, candidateBrick))
            {
                continue;
            }

            compatibleCandidateCount++;

            Rigidbody candidateRigidbody = candidateBrick.GetComponent<Rigidbody>();
            if (candidateRigidbody == null)
            {
                if (logPlacementDebug)
                {
                    Debug.Log(
                        $"PLAEX side snap rejected: moving='{movingBrick.name}', candidate='{candidateBrick.name}', reason='candidate has no rigidbody'.");
                }
                continue;
            }

            if (!TryBuildPlaexSideConnectors(candidateBrick, out PlaexSideConnector[] candidateConnectors))
            {
                if (logPlacementDebug)
                {
                    Debug.Log(
                        $"PLAEX side snap rejected: moving='{movingBrick.name}', candidate='{candidateBrick.name}', reason='failed to build candidate connectors'.");
                }
                continue;
            }

            if (!ArePlaexSideBricksOrientationCompatible(
                movingBrick,
                candidateBrick,
                movingConnectors,
                candidateConnectors))
            {
                if (logPlacementDebug)
                {
                    Debug.Log(
                        $"PLAEX side snap rejected: moving='{movingBrick.name}', candidate='{candidateBrick.name}', reason='orientation incompatible'.");
                }
                continue;
            }

            if (!TryFindBestPlaexSideConnectorMatch(
                movingBrick,
                movingConnectors,
                candidateBrick,
                candidateConnectors,
                out Vector3 offset,
                out Vector3 anchorWorldPosition,
                out float score))
            {
                if (logPlacementDebug)
                {
                    LogPlaexSideCandidateMismatch(
                        movingBrick,
                        candidateBrick,
                        movingConnectors,
                        candidateConnectors);
                }
                continue;
            }

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

            if (found && score <= bestCandidate.score)
            {
                continue;
            }

            found = true;
            bestCandidate = new PlaexSidePhysicsSnapCandidate
            {
                connectedRigidbody = candidateRigidbody,
                offset = offset,
                anchorWorldPosition = anchorWorldPosition,
                score = score
            };
        }

        if (!found && logPlacementDebug)
        {
            Debug.Log(
                $"PLAEX side snap: no candidate found for moving='{movingBrick.name}'. compatibleCandidates={compatibleCandidateCount}.");
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

    private bool TryFindBestPlaexSideConnectorMatch(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        Transform candidateBrick,
        PlaexSideConnector[] candidateConnectors,
        out Vector3 bestOffset,
        out Vector3 bestAnchorWorldPosition,
        out float bestScore)
    {
        bestOffset = Vector3.zero;
        bestAnchorWorldPosition = Vector3.zero;
        bestScore = float.NegativeInfinity;

        if (movingConnectors == null ||
            candidateConnectors == null ||
            movingConnectors.Length == 0 ||
            candidateConnectors.Length == 0 ||
            movingBrick == null ||
            candidateBrick == null)
        {
            return false;
        }

        if (!TryGetFacingPlaexSideConnectorPair(
            movingBrick,
            movingConnectors,
            candidateBrick,
            candidateConnectors,
            out PlaexSideConnector movingConnector,
            out PlaexSideConnector candidateConnector,
            out _,
            out _))
        {
            return false;
        }

        if (!TryResolvePlaexSideConnectorOffset(
            movingConnector,
            candidateConnector,
            out Vector3 offset,
            out float score))
        {
            return false;
        }

        bestOffset = offset;
        bestAnchorWorldPosition = candidateConnector.anchorWorldPosition;
        bestScore = score;
        return true;
    }

    private bool TryResolvePlaexSideConnectorOffset(
        PlaexSideConnector movingConnector,
        PlaexSideConnector candidateConnector,
        out Vector3 offset,
        out float score)
    {
        offset = Vector3.zero;
        score = float.NegativeInfinity;

        if (!ArePlaexSideConnectorKindsCompatible(
            movingConnector.connectorKind,
            candidateConnector.connectorKind))
        {
            return false;
        }

        float facingDot = Vector3.Dot(movingConnector.outwardNormal, candidateConnector.outwardNormal);
        if (facingDot > -PlaexSideConnectorFacingTolerance)
        {
            return false;
        }

        if (!TryBuildWorldHorizontalSnapFrame(
            movingConnector.outwardNormal,
            out Vector3 centerAxis,
            out Vector3 lateralAxis))
        {
            return false;
        }

        Vector3 rawOffset = candidateConnector.anchorWorldPosition - movingConnector.anchorWorldPosition;
        float connectorAxisOffset = Vector3.Dot(rawOffset, centerAxis);
        float rawConnectorGap = Mathf.Abs(connectorAxisOffset);
        if (rawConnectorGap > Mathf.Max(0.001f, plaexSidePhysicsSnapMaxConnectorGap))
        {
            return false;
        }

        float lateralAxisOffset = Vector3.Dot(rawOffset, lateralAxis);
        float rawLateralOffset = Mathf.Abs(lateralAxisOffset);
        if (rawLateralOffset > Mathf.Max(0.001f, plaexSidePhysicsSnapMaxLateralOffset))
        {
            return false;
        }

        float verticalOffset = rawOffset.y;
        float rawVerticalOffset = Mathf.Abs(verticalOffset);
        if (rawVerticalOffset > Mathf.Max(0.001f, plaexSidePhysicsSnapMaxVerticalOffset))
        {
            return false;
        }

        offset =
            (centerAxis * connectorAxisOffset) +
            (lateralAxis * lateralAxisOffset) +
            (Vector3.up * verticalOffset);

        score = -(rawConnectorGap + rawLateralOffset + (rawVerticalOffset * 2f));
        return true;
    }

    private void LogPlaexSideCandidateMismatch(
        Transform movingBrick,
        Transform candidateBrick,
        PlaexSideConnector[] movingConnectors,
        PlaexSideConnector[] candidateConnectors)
    {
        if (movingBrick == null ||
            candidateBrick == null ||
            movingConnectors == null ||
            candidateConnectors == null)
        {
            return;
        }

        Debug.Log(
            $"PLAEX side snap mismatch: moving='{movingBrick.name}', candidate='{candidateBrick.name}', movingCount={movingConnectors.Length}, candidateCount={candidateConnectors.Length}.");

        if (!TryGetFacingPlaexSideConnectorPair(
            movingBrick,
            movingConnectors,
            candidateBrick,
            candidateConnectors,
            out PlaexSideConnector movingConnector,
            out PlaexSideConnector candidateConnector,
            out int movingIndex,
            out int candidateIndex))
        {
            Debug.Log(
                $"PLAEX side snap mismatch: moving='{movingBrick.name}', candidate='{candidateBrick.name}', reason='failed to select facing connectors'.");
            return;
        }

        LogPlaexSideConnectorPairEvaluation(
            movingIndex,
            movingConnector,
            candidateIndex,
            candidateConnector);
    }

    private void LogPlaexSideConnectorPairEvaluation(
        int movingIndex,
        PlaexSideConnector movingConnector,
        int candidateIndex,
        PlaexSideConnector candidateConnector)
    {
        bool kindsCompatible = ArePlaexSideConnectorKindsCompatible(
            movingConnector.connectorKind,
            candidateConnector.connectorKind);
        float facingDot = Vector3.Dot(movingConnector.outwardNormal, candidateConnector.outwardNormal);
        Vector3 rawOffset = candidateConnector.anchorWorldPosition - movingConnector.anchorWorldPosition;

        if (!TryBuildWorldHorizontalSnapFrame(
            movingConnector.outwardNormal,
            out Vector3 centerAxis,
            out Vector3 lateralAxis))
        {
            Debug.Log(
                $"PLAEX side pair: movingIndex={movingIndex}, candidateIndex={candidateIndex}, movingKind={movingConnector.connectorKind}, candidateKind={candidateConnector.connectorKind}, kindsCompatible={kindsCompatible}, facingDot={facingDot:F4}, rawOffset={rawOffset}, reason='failed to build horizontal snap frame'.");
            return;
        }

        float connectorAxisOffset = Vector3.Dot(rawOffset, centerAxis);
        float lateralAxisOffset = Vector3.Dot(rawOffset, lateralAxis);
        float verticalOffset = rawOffset.y;
        float rawConnectorGap = Mathf.Abs(connectorAxisOffset);
        float rawLateralOffset = Mathf.Abs(lateralAxisOffset);
        float rawVerticalOffset = Mathf.Abs(verticalOffset);

        Debug.Log(
            $"PLAEX side pair: movingIndex={movingIndex}, candidateIndex={candidateIndex}, movingKind={movingConnector.connectorKind}, candidateKind={candidateConnector.connectorKind}, kindsCompatible={kindsCompatible}, movingAnchor={movingConnector.anchorWorldPosition}, candidateAnchor={candidateConnector.anchorWorldPosition}, movingNormal={movingConnector.outwardNormal}, candidateNormal={candidateConnector.outwardNormal}, facingDot={facingDot:F4}, connectorGap={rawConnectorGap:F4}, lateralOffset={rawLateralOffset:F4}, verticalOffset={rawVerticalOffset:F4}.");
    }

    private bool HasCompatiblePlaexSideAlignmentAtCurrentPose(
        Transform movingBrick,
        Transform candidateBrick)
    {
        if (movingBrick == null ||
            candidateBrick == null ||
            movingBrick == candidateBrick ||
            !ArePlaexSideSnapBricksCompatible(movingBrick, candidateBrick))
        {
            return false;
        }

        if (!TryBuildPlaexSideConnectors(movingBrick, out PlaexSideConnector[] movingConnectors))
        {
            return false;
        }

        return HasCompatiblePlaexSideAlignmentAtCurrentPose(
            movingBrick,
            movingConnectors,
            candidateBrick);
    }

    private bool HasCompatiblePlaexSideAlignmentAtCurrentPose(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        Transform candidateBrick)
    {
        if (movingBrick == null ||
            candidateBrick == null ||
            movingBrick == candidateBrick ||
            movingConnectors == null ||
            movingConnectors.Length == 0 ||
            !ArePlaexSideSnapBricksCompatible(movingBrick, candidateBrick))
        {
            return false;
        }

        if (!TryBuildPlaexSideConnectors(candidateBrick, out PlaexSideConnector[] candidateConnectors))
        {
            return false;
        }

        if (!ArePlaexSideBricksOrientationCompatible(
            movingBrick,
            candidateBrick,
            movingConnectors,
            candidateConnectors))
        {
            return false;
        }

        return TryFindBestPlaexSideConnectorMatch(
            movingBrick,
            movingConnectors,
            candidateBrick,
            candidateConnectors,
            out _,
            out _,
            out _);
    }

    private bool TryGetFacingPlaexSideConnectorPair(
        Transform movingBrick,
        PlaexSideConnector[] movingConnectors,
        Transform candidateBrick,
        PlaexSideConnector[] candidateConnectors,
        out PlaexSideConnector movingConnector,
        out PlaexSideConnector candidateConnector,
        out int movingIndex,
        out int candidateIndex)
    {
        movingConnector = default;
        candidateConnector = default;
        movingIndex = -1;
        candidateIndex = -1;

        if (movingBrick == null ||
            candidateBrick == null ||
            movingConnectors == null ||
            candidateConnectors == null ||
            movingConnectors.Length == 0 ||
            candidateConnectors.Length == 0)
        {
            return false;
        }

        Vector3 horizontalDirectionToCandidate = Vector3.ProjectOnPlane(
            candidateBrick.position - movingBrick.position,
            Vector3.up);
        if (horizontalDirectionToCandidate.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        horizontalDirectionToCandidate.Normalize();
        if (!TrySelectPlaexSideConnectorFacingDirection(
            movingConnectors,
            horizontalDirectionToCandidate,
            out movingIndex))
        {
            return false;
        }

        if (!TrySelectPlaexSideConnectorFacingDirection(
            candidateConnectors,
            -horizontalDirectionToCandidate,
            out candidateIndex))
        {
            return false;
        }

        movingConnector = movingConnectors[movingIndex];
        candidateConnector = candidateConnectors[candidateIndex];
        return true;
    }

    private static bool TrySelectPlaexSideConnectorFacingDirection(
        PlaexSideConnector[] connectors,
        Vector3 desiredDirection,
        out int selectedIndex)
    {
        selectedIndex = -1;
        if (connectors == null || connectors.Length == 0)
        {
            return false;
        }

        Vector3 horizontalDesiredDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
        if (horizontalDesiredDirection.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        horizontalDesiredDirection.Normalize();
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < connectors.Length; i++)
        {
            Vector3 connectorDirection = Vector3.ProjectOnPlane(connectors[i].outwardNormal, Vector3.up);
            if (connectorDirection.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            connectorDirection.Normalize();
            float score = Vector3.Dot(connectorDirection, horizontalDesiredDirection);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            selectedIndex = i;
        }

        return selectedIndex >= 0;
    }

    private bool TryBuildPlaexSideConnectors(Transform brickTransform, out PlaexSideConnector[] connectors)
    {
        connectors = null;
        if (brickTransform == null)
        {
            return false;
        }

        PlaexSideSnapBrickKind brickKind = ResolvePlaexSideSnapBrickKind(brickTransform);
        if (TryBuildAuthoredPlaexSideConnectors(brickTransform, brickKind, out connectors))
        {
            return true;
        }

        switch (brickKind)
        {
            case PlaexSideSnapBrickKind.GreenLong:
            case PlaexSideSnapBrickKind.OrangeLong:
                PlaexSideConnectorKind longConnectorKind = brickKind == PlaexSideSnapBrickKind.GreenLong
                    ? PlaexSideConnectorKind.Tab
                    : PlaexSideConnectorKind.Cavity;

                connectors = new PlaexSideConnector[2];
                connectors[0] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(PlaexLongHalfLength, 0f, 0f),
                    Vector3.right,
                    longConnectorKind);
                connectors[1] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(-PlaexLongHalfLength, 0f, 0f),
                    Vector3.left,
                    longConnectorKind);
                return true;

            case PlaexSideSnapBrickKind.YellowSide:
                connectors = new PlaexSideConnector[4];
                connectors[0] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(PlaexSideHalfExtent, 0f, 0f),
                    Vector3.right,
                    ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.PositiveX));
                connectors[1] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(-PlaexSideHalfExtent, 0f, 0f),
                    Vector3.left,
                    ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.NegativeX));
                connectors[2] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(0f, 0f, PlaexSideHalfExtent),
                    Vector3.forward,
                    ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.PositiveZ));
                connectors[3] = CreatePlaexSideRootConnector(
                    brickTransform,
                    new Vector3(0f, 0f, -PlaexSideHalfExtent),
                    Vector3.back,
                    ResolveYellowSideConnectorKind(PlaexSideHorizontalFace.NegativeZ));
                return true;

            default:
                return false;
        }
    }

    private bool TryBuildAuthoredPlaexSideConnectors(
        Transform brickTransform,
        PlaexSideSnapBrickKind brickKind,
        out PlaexSideConnector[] connectors)
    {
        connectors = null;
        int expectedCount = ResolveExpectedPlaexSideSnapPointCount(brickKind);
        if (brickTransform == null || expectedCount <= 0)
        {
            return false;
        }

        EnsurePlaexSideSnapPoints(brickTransform, brickKind);

        Transform snapContainer = brickTransform.Find(PlaexSideSnapPointContainerName);
        if (snapContainer == null)
        {
            return false;
        }

        List<PlaexSideConnector> authoredConnectors = new List<PlaexSideConnector>(Mathf.Max(expectedCount, snapContainer.childCount));
        for (int i = 0; i < snapContainer.childCount; i++)
        {
            Transform snapPointTransform = snapContainer.GetChild(i);
            if (!TryParsePlaexSideSnapPointTransform(snapPointTransform, out PlaexSideConnector connector))
            {
                continue;
            }

            authoredConnectors.Add(connector);
        }

        if (authoredConnectors.Count < expectedCount)
        {
            return false;
        }

        connectors = authoredConnectors.ToArray();
        return true;
    }

    private void EnsurePlaexSideSnapPoints(Transform brickTransform, PlaexSideSnapBrickKind brickKind)
    {
        if (brickTransform == null)
        {
            return;
        }

        int expectedCount = ResolveExpectedPlaexSideSnapPointCount(brickKind);
        if (expectedCount <= 0)
        {
            return;
        }

        Transform snapContainer = brickTransform.Find(PlaexSideSnapPointContainerName);
        if (HasSufficientAuthoredPlaexSideSnapPoints(snapContainer, expectedCount))
        {
            return;
        }

        if (!TryGetPlaexSideSnapPointDefinitions(brickKind, out PlaexSideSnapPointDefinition[] definitions) ||
            definitions == null ||
            definitions.Length < expectedCount)
        {
            return;
        }

        if (snapContainer != null)
        {
            DestroyNowOrLater(snapContainer.gameObject);
        }

        snapContainer = new GameObject(PlaexSideSnapPointContainerName).transform;
        snapContainer.SetParent(brickTransform, false);
        CreatePlaexSideSnapPointsFromDefinitions(snapContainer, definitions);
    }

    private static bool HasSufficientAuthoredPlaexSideSnapPoints(Transform snapContainer, int expectedCount)
    {
        if (snapContainer == null || expectedCount <= 0)
        {
            return false;
        }

        int parsedCount = 0;
        for (int i = 0; i < snapContainer.childCount; i++)
        {
            if (TryResolvePlaexSideSnapPointKindFromName(snapContainer.GetChild(i).name, out _))
            {
                parsedCount++;
            }
        }

        return parsedCount >= expectedCount;
    }

    private static void CreatePlaexSideSnapPointsFromDefinitions(
        Transform container,
        PlaexSideSnapPointDefinition[] definitions)
    {
        if (container == null || definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            PlaexSideSnapPointDefinition definition = definitions[i];
            if (definition == null ||
                !TryResolvePlaexSideSnapPointKindFromName(definition.connectorKind, out PlaexSideConnectorKind connectorKind))
            {
                continue;
            }

            string pointName = string.IsNullOrWhiteSpace(definition.name)
                ? $"{connectorKind}_{i}"
                : definition.name;

            Vector3 localForward = definition.localForward.ToVector3();
            if (localForward.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            GameObject pointObject = new GameObject(pointName);
            Transform pointTransform = pointObject.transform;
            pointTransform.SetParent(container, false);
            pointTransform.localPosition = definition.localPosition.ToVector3();
            pointTransform.localRotation = CreatePlaexSideSnapPointRotation(localForward);
        }
    }

    private static Quaternion CreatePlaexSideSnapPointRotation(Vector3 localForward)
    {
        Vector3 normalizedForward = localForward.normalized;
        Vector3 upReference = Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) >= 0.99f
            ? Vector3.forward
            : Vector3.up;
        return Quaternion.LookRotation(normalizedForward, upReference);
    }

    private static bool TryParsePlaexSideSnapPointTransform(
        Transform snapPointTransform,
        out PlaexSideConnector connector)
    {
        connector = default;
        if (snapPointTransform == null ||
            !TryResolvePlaexSideSnapPointKindFromName(snapPointTransform.name, out PlaexSideConnectorKind connectorKind))
        {
            return false;
        }

        Vector3 outwardNormal = Vector3.ProjectOnPlane(snapPointTransform.forward, Vector3.up);
        if (outwardNormal.sqrMagnitude <= 0.000001f)
        {
            outwardNormal = snapPointTransform.forward;
        }

        if (outwardNormal.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        connector = new PlaexSideConnector
        {
            anchorWorldPosition = snapPointTransform.position,
            outwardNormal = outwardNormal.normalized,
            connectorKind = connectorKind
        };
        return true;
    }

    private static bool TryResolvePlaexSideSnapPointKindFromName(
        string value,
        out PlaexSideConnectorKind connectorKind)
    {
        connectorKind = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("Tab", StringComparison.OrdinalIgnoreCase))
        {
            connectorKind = PlaexSideConnectorKind.Tab;
            return true;
        }

        if (value.StartsWith("Cavity", StringComparison.OrdinalIgnoreCase))
        {
            connectorKind = PlaexSideConnectorKind.Cavity;
            return true;
        }

        return false;
    }

    private static bool TryGetPlaexSideSnapPointDefinitions(
        PlaexSideSnapBrickKind brickKind,
        out PlaexSideSnapPointDefinition[] definitions)
    {
        definitions = null;
        EnsurePlaexSideSnapPointDefinitionCacheLoaded();
        if (plaexSideSnapPointDefinitionsByKind == null)
        {
            return false;
        }

        return plaexSideSnapPointDefinitionsByKind.TryGetValue(brickKind, out definitions) &&
            definitions != null &&
            definitions.Length > 0;
    }

    private static void EnsurePlaexSideSnapPointDefinitionCacheLoaded()
    {
        if (plaexSideSnapPointDefinitionsLoaded)
        {
            return;
        }

        plaexSideSnapPointDefinitionsLoaded = true;
        plaexSideSnapPointDefinitionsByKind = new Dictionary<PlaexSideSnapBrickKind, PlaexSideSnapPointDefinition[]>();

        TextAsset definitionAsset = Resources.Load<TextAsset>(PlaexSideSnapPointDefinitionsResourcePath);
        if (definitionAsset == null)
        {
            return;
        }

        PlaexSideSnapPointDefinitionFile definitionFile =
            JsonUtility.FromJson<PlaexSideSnapPointDefinitionFile>(definitionAsset.text);
        if (definitionFile == null || definitionFile.bricks == null)
        {
            return;
        }

        for (int i = 0; i < definitionFile.bricks.Length; i++)
        {
            PlaexSideBrickSnapPointDefinition brickDefinition = definitionFile.bricks[i];
            if (brickDefinition == null ||
                !TryParsePlaexSideSnapBrickKind(brickDefinition.brickKind, out PlaexSideSnapBrickKind resolvedKind) ||
                brickDefinition.points == null ||
                brickDefinition.points.Length == 0)
            {
                continue;
            }

            plaexSideSnapPointDefinitionsByKind[resolvedKind] = brickDefinition.points;
        }
    }

    private static bool TryParsePlaexSideSnapBrickKind(
        string value,
        out PlaexSideSnapBrickKind brickKind)
    {
        brickKind = PlaexSideSnapBrickKind.Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "GreenLong":
                brickKind = PlaexSideSnapBrickKind.GreenLong;
                return true;
            case "OrangeLong":
                brickKind = PlaexSideSnapBrickKind.OrangeLong;
                return true;
            case "YellowSide":
                brickKind = PlaexSideSnapBrickKind.YellowSide;
                return true;
            default:
                return false;
        }
    }

    private static int ResolveExpectedPlaexSideSnapPointCount(PlaexSideSnapBrickKind brickKind)
    {
        switch (brickKind)
        {
            case PlaexSideSnapBrickKind.GreenLong:
            case PlaexSideSnapBrickKind.OrangeLong:
                return 2;
            case PlaexSideSnapBrickKind.YellowSide:
                return 4;
            default:
                return 0;
        }
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

        return true;
    }

    private static bool TryResolvePlaexLongLocalAxis(Bounds localBounds, out Vector3 localLongAxis)
    {
        localLongAxis = Vector3.zero;
        float halfLongExtent;
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

        return halfLongExtent > 0.0001f;
    }

    private static PlaexSideConnector CreatePlaexSideConnector(
        Transform bodyTransform,
        Bounds localBounds,
        Vector3 localNormal,
        PlaexSideConnectorKind connectorKind)
    {
        Vector3 normalizedLocalNormal = localNormal.normalized;
        float axisExtent = ResolveLocalBoundsExtent(localBounds.extents, normalizedLocalNormal);
        Vector3 localAnchor = localBounds.center + (normalizedLocalNormal * axisExtent);

        return new PlaexSideConnector
        {
            anchorWorldPosition = bodyTransform.TransformPoint(localAnchor),
            outwardNormal = bodyTransform.TransformDirection(normalizedLocalNormal).normalized,
            connectorKind = connectorKind
        };
    }

    private static PlaexSideConnector CreatePlaexSideRootConnector(
        Transform brickTransform,
        Vector3 localAnchor,
        Vector3 localNormal,
        PlaexSideConnectorKind connectorKind)
    {
        Vector3 normalizedLocalNormal = localNormal.normalized;
        return new PlaexSideConnector
        {
            anchorWorldPosition = brickTransform.TransformPoint(localAnchor),
            outwardNormal = brickTransform.TransformDirection(normalizedLocalNormal).normalized,
            connectorKind = connectorKind
        };
    }

    private static float ResolveLocalBoundsExtent(Vector3 extents, Vector3 axis)
    {
        Vector3 absoluteAxis = new Vector3(
            Mathf.Abs(axis.x),
            Mathf.Abs(axis.y),
            Mathf.Abs(axis.z));

        if (absoluteAxis.x >= absoluteAxis.y && absoluteAxis.x >= absoluteAxis.z)
        {
            return extents.x;
        }

        if (absoluteAxis.z >= absoluteAxis.x && absoluteAxis.z >= absoluteAxis.y)
        {
            return extents.z;
        }

        return extents.y;
    }

    private static PlaexSideConnectorKind ResolveYellowSideConnectorKind(PlaexSideHorizontalFace face)
    {
        return face == YellowSideCavityFace
            ? PlaexSideConnectorKind.Cavity
            : PlaexSideConnectorKind.Tab;
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
        return ResolvePlaexSideSnapBrickKind(brickTransform) != PlaexSideSnapBrickKind.Unknown;
    }

    private static bool ArePlaexSideSnapBricksCompatible(Transform firstBrick, Transform secondBrick)
    {
        if (firstBrick == null || secondBrick == null || firstBrick == secondBrick)
        {
            return false;
        }

        PlaexSideSnapBrickKind firstKind = ResolvePlaexSideSnapBrickKind(firstBrick);
        PlaexSideSnapBrickKind secondKind = ResolvePlaexSideSnapBrickKind(secondBrick);
        return ArePlaexSideSnapKindsCompatible(firstKind, secondKind);
    }

    private static bool ArePlaexSideSnapKindsCompatible(
        PlaexSideSnapBrickKind firstKind,
        PlaexSideSnapBrickKind secondKind)
    {
        PlaexSideConnectorKindMask firstMask = ResolvePlaexSideConnectorKindMask(firstKind);
        PlaexSideConnectorKindMask secondMask = ResolvePlaexSideConnectorKindMask(secondKind);
        if (firstMask == PlaexSideConnectorKindMask.None ||
            secondMask == PlaexSideConnectorKindMask.None)
        {
            return false;
        }

        bool firstHasTab = (firstMask & PlaexSideConnectorKindMask.Tab) != 0;
        bool firstHasCavity = (firstMask & PlaexSideConnectorKindMask.Cavity) != 0;
        bool secondHasTab = (secondMask & PlaexSideConnectorKindMask.Tab) != 0;
        bool secondHasCavity = (secondMask & PlaexSideConnectorKindMask.Cavity) != 0;

        return
            (firstHasTab && secondHasCavity) ||
            (firstHasCavity && secondHasTab);
    }

    private static bool ArePlaexSideConnectorKindsCompatible(
        PlaexSideConnectorKind firstKind,
        PlaexSideConnectorKind secondKind)
    {
        return
            (firstKind == PlaexSideConnectorKind.Tab && secondKind == PlaexSideConnectorKind.Cavity) ||
            (firstKind == PlaexSideConnectorKind.Cavity && secondKind == PlaexSideConnectorKind.Tab);
    }

    private static PlaexSideConnectorKindMask ResolvePlaexSideConnectorKindMask(PlaexSideSnapBrickKind brickKind)
    {
        switch (brickKind)
        {
            case PlaexSideSnapBrickKind.GreenLong:
                return PlaexSideConnectorKindMask.Tab;
            case PlaexSideSnapBrickKind.OrangeLong:
                return PlaexSideConnectorKindMask.Cavity;
            case PlaexSideSnapBrickKind.YellowSide:
                return PlaexSideConnectorKindMask.Tab | PlaexSideConnectorKindMask.Cavity;
            default:
                return PlaexSideConnectorKindMask.None;
        }
    }

    private static PlaexSideSnapBrickKind ResolvePlaexSideSnapBrickKind(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return PlaexSideSnapBrickKind.Unknown;
        }

        string brickName = brickTransform.name;
        if (string.Equals(brickName, "GreenLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryGreenPlaexLongBrick_", StringComparison.OrdinalIgnoreCase))
        {
            return PlaexSideSnapBrickKind.GreenLong;
        }

        if (string.Equals(brickName, "OrangePlaexLongBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryOrangePlaexLongBrick_", StringComparison.OrdinalIgnoreCase))
        {
            return PlaexSideSnapBrickKind.OrangeLong;
        }

        if (string.Equals(brickName, "YellowLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryYellowPlaexSideBrick_", StringComparison.OrdinalIgnoreCase))
        {
            return PlaexSideSnapBrickKind.YellowSide;
        }

        return PlaexSideSnapBrickKind.Unknown;
    }

    private static bool IsGreenPlaexSideTabBrick(Transform brickTransform)
    {
        return ResolvePlaexSideSnapBrickKind(brickTransform) == PlaexSideSnapBrickKind.GreenLong;
    }

    private static bool IsOrangePlaexSideCavityBrick(Transform brickTransform)
    {
        return ResolvePlaexSideSnapBrickKind(brickTransform) == PlaexSideSnapBrickKind.OrangeLong;
    }

    private static bool IsYellowPlaexSideCavityBrick(Transform brickTransform)
    {
        return ResolvePlaexSideSnapBrickKind(brickTransform) == PlaexSideSnapBrickKind.YellowSide;
    }
}


