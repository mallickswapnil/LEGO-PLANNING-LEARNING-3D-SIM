using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ThreeDBrickSim
{
    private const int OrangeLegoStudColumns = 3;
    private const int OrangeLegoStudRows = 2;
    private const float OrangeLegoTopStudCenterY = (BrickHeight * 0.5f) + (StudHeight * 0.5f);
    private const float OrangeLegoBottomTubeCenterY = -(BrickHeight * 0.5f) + (StudHeight * 0.5f);
    private const string OrangeLegoSnapPointContainerName = "__OrangeLegoSnapPoints";
    private const int OrangeLegoSnapProbeBufferSize = 64;
    private const int OrangeLegoSnapMatchBufferSize = 64;

    private readonly Collider[] orangeLegoSnapProbeBuffer = new Collider[OrangeLegoSnapProbeBufferSize];
    private readonly Collider[] orangeLegoSnapMatchBuffer = new Collider[OrangeLegoSnapMatchBufferSize];

    private enum OrangeLegoSnapPointType
    {
        Stud,
        Tube
    }

    private sealed class OrangeLegoSnapPoint : MonoBehaviour
    {
        public OrangeLegoSnapPointType pointType;
        public Transform ownerBrick;
        public Rigidbody ownerRigidbody;
    }

    private struct OrangeLegoPhysicsSnapCandidate
    {
        public Transform lowerBrick;
        public Rigidbody lowerRigidbody;
        public Vector3 offset;
        public Vector3 anchorWorldPosition;
        public int matchedContacts;
        public float score;
    }

    private struct IgnoredCollisionPair
    {
        public Collider first;
        public Collider second;
    }

    private void TrySnapBrickWithPhysics(Transform movingBrick)
    {
        if (!enableOrangeLegoPhysicsSnap || !IsOrangeLegoBrick(movingBrick))
        {
            return;
        }

        EnsureOrangeLegoSnapPoints(movingBrick);
        EnsurePlacedOrangeBricksHaveSnapPoints();

        OrangeLegoSnapPoint[] movingTubePoints = GetSnapPointsByType(movingBrick, OrangeLegoSnapPointType.Tube);
        if (movingTubePoints.Length == 0)
        {
            return;
        }

        if (!TryFindBestOrangeLegoPhysicsSnapCandidate(movingBrick, movingTubePoints, out OrangeLegoPhysicsSnapCandidate candidate))
        {
            return;
        }

        ApplyOrangeLegoPhysicsSnap(movingBrick, candidate);
    }

    private bool TryFindBestOrangeLegoPhysicsSnapCandidate(
        Transform movingBrick,
        OrangeLegoSnapPoint[] movingTubePoints,
        out OrangeLegoPhysicsSnapCandidate bestCandidate)
    {
        bestCandidate = default;
        bool found = false;

        for (int i = 0; i < movingTubePoints.Length; i++)
        {
            OrangeLegoSnapPoint movingTube = movingTubePoints[i];
            if (movingTube == null)
            {
                continue;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                movingTube.transform.position,
                orangeLegoPhysicsSnapProbeRadius,
                orangeLegoSnapProbeBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = orangeLegoSnapProbeBuffer[hitIndex];
                if (hitCollider == null)
                {
                    continue;
                }

                OrangeLegoSnapPoint studPoint = hitCollider.GetComponent<OrangeLegoSnapPoint>();
                if (!IsValidStudSnapTarget(movingBrick, studPoint))
                {
                    continue;
                }

                Vector3 offset = studPoint.transform.position - movingTube.transform.position;
                if (!IsSnapOffsetWithinTolerance(offset))
                {
                    continue;
                }

                if (rejectOverlappingPlacement)
                {
                    Vector3 snappedPosition = movingBrick.position + offset;
                    if (IsPlacementPoseBlocked(movingBrick, snappedPosition, movingBrick.rotation))
                    {
                        continue;
                    }
                }

                int matchedContacts = CountMatchedSnapContacts(
                    movingTubePoints,
                    studPoint.ownerBrick,
                    offset,
                    orangeLegoSnapMatchBuffer);

                if (matchedContacts < orangeLegoPhysicsSnapMinContacts)
                {
                    continue;
                }

                float horizontalDistanceSqr = (new Vector2(offset.x, offset.z)).sqrMagnitude;
                float score = (matchedContacts * 100f) - horizontalDistanceSqr - Mathf.Abs(offset.y);

                if (found && score <= bestCandidate.score)
                {
                    continue;
                }

                found = true;
                bestCandidate = new OrangeLegoPhysicsSnapCandidate
                {
                    lowerBrick = studPoint.ownerBrick,
                    lowerRigidbody = studPoint.ownerRigidbody,
                    offset = offset,
                    anchorWorldPosition = studPoint.transform.position,
                    matchedContacts = matchedContacts,
                    score = score
                };
            }
        }

        return found;
    }

    private int CountMatchedSnapContacts(
        OrangeLegoSnapPoint[] movingTubePoints,
        Transform targetLowerBrick,
        Vector3 offset,
        Collider[] overlapBuffer)
    {
        int matchedContacts = 0;
        HashSet<int> claimedStudIds = new HashSet<int>();

        for (int i = 0; i < movingTubePoints.Length; i++)
        {
            OrangeLegoSnapPoint tubePoint = movingTubePoints[i];
            if (tubePoint == null)
            {
                continue;
            }

            Vector3 expectedTubePosition = tubePoint.transform.position + offset;
            int hitCount = Physics.OverlapSphereNonAlloc(
                expectedTubePosition,
                orangeLegoPhysicsSnapProbeRadius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            OrangeLegoSnapPoint nearestStud = null;
            float nearestDistanceSqr = float.PositiveInfinity;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = overlapBuffer[hitIndex];
                if (hitCollider == null)
                {
                    continue;
                }

                OrangeLegoSnapPoint studPoint = hitCollider.GetComponent<OrangeLegoSnapPoint>();
                if (studPoint == null ||
                    studPoint.pointType != OrangeLegoSnapPointType.Stud ||
                    studPoint.ownerBrick != targetLowerBrick)
                {
                    continue;
                }

                int studId = studPoint.GetInstanceID();
                if (claimedStudIds.Contains(studId))
                {
                    continue;
                }

                float distanceSqr = (studPoint.transform.position - expectedTubePosition).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestDistanceSqr = distanceSqr;
                nearestStud = studPoint;
            }

            if (nearestStud == null)
            {
                continue;
            }

            claimedStudIds.Add(nearestStud.GetInstanceID());
            matchedContacts++;
        }

        return matchedContacts;
    }

    private void ApplyOrangeLegoPhysicsSnap(Transform movingBrick, OrangeLegoPhysicsSnapCandidate candidate)
    {
        if (movingBrick == null || candidate.lowerRigidbody == null)
        {
            return;
        }

        RemoveExistingSnapJoints(movingBrick);

        Rigidbody movingRigidbody = movingBrick.GetComponent<Rigidbody>();
        Vector3 snappedPosition = movingBrick.position + candidate.offset;

        if (movingRigidbody == null)
        {
            return;
        }

        List<IgnoredCollisionPair> ignoredCollisionPairs = IgnoreCollisionsDuringSnapInsertion(movingBrick);
        if (!movingRigidbody.isKinematic)
        {
            movingRigidbody.linearVelocity = Vector3.zero;
            movingRigidbody.angularVelocity = Vector3.zero;
        }

        movingRigidbody.isKinematic = true;
        movingRigidbody.useGravity = false;
        movingRigidbody.position = snappedPosition;

        Physics.SyncTransforms();

        FixedJoint snapJoint = movingBrick.gameObject.AddComponent<FixedJoint>();
        snapJoint.connectedBody = candidate.lowerRigidbody;
        snapJoint.autoConfigureConnectedAnchor = false;
        snapJoint.anchor = movingBrick.InverseTransformPoint(candidate.anchorWorldPosition);
        snapJoint.connectedAnchor = candidate.lowerRigidbody.transform.InverseTransformPoint(candidate.anchorWorldPosition);
        snapJoint.enableCollision = false;
        snapJoint.breakForce = orangeLegoPhysicsSnapJointBreakForce;
        snapJoint.breakTorque = orangeLegoPhysicsSnapJointBreakTorque;

        StartCoroutine(RestoreDynamicAfterSnapInsertion(movingRigidbody, ignoredCollisionPairs));
    }

    private List<IgnoredCollisionPair> IgnoreCollisionsDuringSnapInsertion(Transform movingBrick)
    {
        if (!orangeLegoSnapShieldCollisionsDuringInsertion || movingBrick == null)
        {
            return null;
        }

        List<IgnoredCollisionPair> ignoredPairs = new List<IgnoredCollisionPair>();
        Collider[] movingColliders = movingBrick.GetComponentsInChildren<Collider>(true);
        if (movingColliders == null || movingColliders.Length == 0)
        {
            return ignoredPairs;
        }

        foreach (Transform placedBrick in plannedWallBricks)
        {
            if (placedBrick == null || placedBrick == movingBrick || detachedWallBricks.Contains(placedBrick))
            {
                continue;
            }

            Collider[] otherColliders = placedBrick.GetComponentsInChildren<Collider>(true);
            if (otherColliders == null || otherColliders.Length == 0)
            {
                continue;
            }

            for (int movingIndex = 0; movingIndex < movingColliders.Length; movingIndex++)
            {
                Collider movingCollider = movingColliders[movingIndex];
                if (movingCollider == null || !movingCollider.enabled || movingCollider.isTrigger)
                {
                    continue;
                }

                for (int otherIndex = 0; otherIndex < otherColliders.Length; otherIndex++)
                {
                    Collider otherCollider = otherColliders[otherIndex];
                    if (otherCollider == null || !otherCollider.enabled || otherCollider.isTrigger)
                    {
                        continue;
                    }

                    if (Physics.GetIgnoreCollision(movingCollider, otherCollider))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(movingCollider, otherCollider, true);
                    ignoredPairs.Add(new IgnoredCollisionPair { first = movingCollider, second = otherCollider });
                }
            }
        }

        return ignoredPairs;
    }

    private IEnumerator RestoreDynamicAfterSnapInsertion(Rigidbody movingRigidbody, List<IgnoredCollisionPair> ignoredCollisionPairs)
    {
        int stabilizationSteps = Mathf.Max(1, orangeLegoSnapStabilizationFixedSteps);
        for (int i = 0; i < stabilizationSteps; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        if (movingRigidbody != null)
        {
            ConfigureBrickRigidbodyForSimulation(movingRigidbody);
            if (!movingRigidbody.isKinematic)
            {
                movingRigidbody.linearVelocity = Vector3.zero;
                movingRigidbody.angularVelocity = Vector3.zero;
            }

            movingRigidbody.WakeUp();
        }

        RestoreIgnoredCollisionPairs(ignoredCollisionPairs);
    }

    private static void RestoreIgnoredCollisionPairs(List<IgnoredCollisionPair> ignoredCollisionPairs)
    {
        if (ignoredCollisionPairs == null)
        {
            return;
        }

        for (int i = 0; i < ignoredCollisionPairs.Count; i++)
        {
            IgnoredCollisionPair pair = ignoredCollisionPairs[i];
            if (pair.first == null || pair.second == null)
            {
                continue;
            }

            Physics.IgnoreCollision(pair.first, pair.second, false);
        }
    }

    private void EnsurePlacedOrangeBricksHaveSnapPoints()
    {
        foreach (Transform plannedBrick in plannedWallBricks)
        {
            if (plannedBrick == null || detachedWallBricks.Contains(plannedBrick) || !IsOrangeLegoBrick(plannedBrick))
            {
                continue;
            }

            EnsureOrangeLegoSnapPoints(plannedBrick);
        }
    }

    private void EnsureOrangeLegoSnapPoints(Transform brickTransform)
    {
        if (!IsOrangeLegoBrick(brickTransform))
        {
            return;
        }

        Transform snapContainer = brickTransform.Find(OrangeLegoSnapPointContainerName);
        OrangeLegoSnapPoint[] snapPoints;

        if (snapContainer == null)
        {
            snapContainer = new GameObject(OrangeLegoSnapPointContainerName).transform;
            snapContainer.SetParent(brickTransform, false);
            snapPoints = CreateOrangeLegoSnapPoints(snapContainer, brickTransform);
        }
        else
        {
            snapPoints = snapContainer.GetComponentsInChildren<OrangeLegoSnapPoint>(true);
            if (snapPoints.Length != OrangeLegoStudColumns * OrangeLegoStudRows * 2)
            {
                DestroyNowOrLater(snapContainer.gameObject);
                snapContainer = new GameObject(OrangeLegoSnapPointContainerName).transform;
                snapContainer.SetParent(brickTransform, false);
                snapPoints = CreateOrangeLegoSnapPoints(snapContainer, brickTransform);
            }
        }

        Rigidbody ownerRigidbody = brickTransform.GetComponent<Rigidbody>();
        float colliderRadius = Mathf.Max(orangeLegoPhysicsSnapProbeRadius * 0.5f, 0.01f);

        for (int i = 0; i < snapPoints.Length; i++)
        {
            OrangeLegoSnapPoint snapPoint = snapPoints[i];
            if (snapPoint == null)
            {
                continue;
            }

            snapPoint.ownerBrick = brickTransform;
            snapPoint.ownerRigidbody = ownerRigidbody;

            SphereCollider sphere = snapPoint.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = snapPoint.gameObject.AddComponent<SphereCollider>();
            }

            sphere.isTrigger = true;
            sphere.radius = colliderRadius;
        }
    }

    private OrangeLegoSnapPoint[] CreateOrangeLegoSnapPoints(Transform container, Transform ownerBrick)
    {
        List<OrangeLegoSnapPoint> points = new List<OrangeLegoSnapPoint>(OrangeLegoStudColumns * OrangeLegoStudRows * 2);
        Vector2[] localGridPoints = BuildOrangeLegoGridLocalPoints();

        for (int i = 0; i < localGridPoints.Length; i++)
        {
            Vector2 localPoint = localGridPoints[i];

            OrangeLegoSnapPoint topStud = CreateSnapPoint(
                container,
                $"Stud_{i}",
                new Vector3(localPoint.x, OrangeLegoTopStudCenterY, localPoint.y),
                OrangeLegoSnapPointType.Stud,
                ownerBrick);
            points.Add(topStud);

            OrangeLegoSnapPoint bottomTube = CreateSnapPoint(
                container,
                $"Tube_{i}",
                new Vector3(localPoint.x, OrangeLegoBottomTubeCenterY, localPoint.y),
                OrangeLegoSnapPointType.Tube,
                ownerBrick);
            points.Add(bottomTube);
        }

        return points.ToArray();
    }

    private static Vector2[] BuildOrangeLegoGridLocalPoints()
    {
        Vector2[] points = new Vector2[OrangeLegoStudColumns * OrangeLegoStudRows];
        float startX = -((OrangeLegoStudColumns - 1) * StudPitch) * 0.5f;
        float startZ = -((OrangeLegoStudRows - 1) * StudPitch) * 0.5f;
        int index = 0;

        for (int x = 0; x < OrangeLegoStudColumns; x++)
        {
            for (int z = 0; z < OrangeLegoStudRows; z++)
            {
                points[index++] = new Vector2(startX + (x * StudPitch), startZ + (z * StudPitch));
            }
        }

        return points;
    }

    private static OrangeLegoSnapPoint CreateSnapPoint(
        Transform parent,
        string pointName,
        Vector3 localPosition,
        OrangeLegoSnapPointType pointType,
        Transform ownerBrick)
    {
        GameObject pointObject = new GameObject(pointName);
        pointObject.transform.SetParent(parent, false);
        pointObject.transform.localPosition = localPosition;

        OrangeLegoSnapPoint snapPoint = pointObject.AddComponent<OrangeLegoSnapPoint>();
        snapPoint.pointType = pointType;
        snapPoint.ownerBrick = ownerBrick;
        snapPoint.ownerRigidbody = ownerBrick.GetComponent<Rigidbody>();

        SphereCollider sphere = pointObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.05f;

        return snapPoint;
    }

    private OrangeLegoSnapPoint[] GetSnapPointsByType(Transform brickTransform, OrangeLegoSnapPointType pointType)
    {
        OrangeLegoSnapPoint[] allSnapPoints = brickTransform.GetComponentsInChildren<OrangeLegoSnapPoint>(true);
        if (allSnapPoints == null || allSnapPoints.Length == 0)
        {
            return Array.Empty<OrangeLegoSnapPoint>();
        }

        List<OrangeLegoSnapPoint> filteredPoints = new List<OrangeLegoSnapPoint>(allSnapPoints.Length);
        for (int i = 0; i < allSnapPoints.Length; i++)
        {
            OrangeLegoSnapPoint snapPoint = allSnapPoints[i];
            if (snapPoint != null && snapPoint.pointType == pointType && snapPoint.ownerBrick == brickTransform)
            {
                filteredPoints.Add(snapPoint);
            }
        }

        return filteredPoints.ToArray();
    }

    private bool IsValidStudSnapTarget(Transform movingBrick, OrangeLegoSnapPoint studPoint)
    {
        if (studPoint == null ||
            studPoint.pointType != OrangeLegoSnapPointType.Stud ||
            studPoint.ownerBrick == null ||
            studPoint.ownerBrick == movingBrick ||
            studPoint.ownerRigidbody == null)
        {
            return false;
        }

        if (!plannedWallBricks.Contains(studPoint.ownerBrick) ||
            detachedWallBricks.Contains(studPoint.ownerBrick) ||
            !IsOrangeLegoBrick(studPoint.ownerBrick))
        {
            return false;
        }

        return true;
    }

    private bool IsSnapOffsetWithinTolerance(Vector3 offset)
    {
        float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
        if (horizontalDistance > orangeLegoPhysicsSnapMaxHorizontalOffset)
        {
            return false;
        }

        return Mathf.Abs(offset.y) <= orangeLegoPhysicsSnapMaxVerticalOffset;
    }

    private static bool IsOrangeLegoBrick(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return false;
        }

        string brickName = brickTransform.name;
        return
            string.Equals(brickName, "OrangeLegoBrick", StringComparison.OrdinalIgnoreCase) ||
            brickName.StartsWith("InventoryOrangeLegoBrick_", StringComparison.OrdinalIgnoreCase);
    }

    private static void DestroyNowOrLater(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void RemoveExistingSnapJoints(Transform brickTransform)
    {
        if (brickTransform == null)
        {
            return;
        }

        FixedJoint[] joints = brickTransform.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            DestroyNowOrLater(joints[i]);
        }
    }
}
