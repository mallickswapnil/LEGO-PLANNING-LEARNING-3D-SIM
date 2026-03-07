using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class ThreeDBrickSim
{
    private void HandleWallBreakInput()
    {
        if (!enableWallBreakInput || controlledCamera == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (!IsWallBreakModifierPressed() || !DidPressWallBreakMouseButtonThisFrame())
        {
            return;
        }

        Vector2 pointerPosition = GetPointerScreenPosition();
        Ray ray = controlledCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 1000f))
        {
            return;
        }

        Transform brickTransform = ResolveBrickRootFromHit(hitInfo.transform);
        if (brickTransform == null || !plannedWallBricks.Contains(brickTransform))
        {
            return;
        }

        Vector3 forceDirection = ResolveWallBreakForceDirection(ray.direction);
        ApplyForceToWallBrick(brickTransform, forceDirection * wallBreakImpulse, hitInfo.point);
    }

    public bool ApplyForceToWallBrick(string brickId, Vector3 force)
    {
        Transform brickTransform = FindBrickTransformById(brickId);
        return ApplyForceToWallBrick(brickTransform, force, null);
    }

    public bool ApplyForceToWallBrick(Transform targetBrick, Vector3 force)
    {
        return ApplyForceToWallBrick(targetBrick, force, null);
    }

    private bool ApplyForceToWallBrick(Transform targetBrick, Vector3 force, Vector3? forceApplicationPoint)
    {
        if (targetBrick == null || !plannedWallBricks.Contains(targetBrick))
        {
            return false;
        }

        List<Transform> supportedBricks = CollectSupportedBricks(targetBrick);
        ExpandWithJointConnectedBricks(supportedBricks);
        bool shouldBreakSupports = ShouldBreakSupportedBricks(
            force.magnitude,
            supportedBricks,
            targetBrick,
            forceApplicationPoint);

        if (!shouldBreakSupports)
        {
            return false;
        }

        for (int i = 0; i < supportedBricks.Count; i++)
        {
            Transform brickTransform = supportedBricks[i];
            ReleaseBrickForPhysics(brickTransform);
        }

        Rigidbody targetRigidbody = targetBrick.GetComponent<Rigidbody>();
        if (targetRigidbody != null)
        {
            if (targetRigidbody.isKinematic)
            {
                targetRigidbody.isKinematic = false;
            }

            Vector3 applicationPoint = ResolveForceApplicationPoint(targetBrick, targetRigidbody, forceApplicationPoint);
            Vector3 clampedForce = ClampWallBreakImpulse(force, targetRigidbody);
            targetRigidbody.AddForceAtPosition(clampedForce, applicationPoint, ForceMode.Impulse);
        }

        return true;
    }

    private bool ShouldBreakSupportedBricks(
        float impulseMagnitude,
        List<Transform> supportedBricks,
        Transform targetBrick,
        Vector3? forceApplicationPoint)
    {
        if (supportedBricks == null || supportedBricks.Count == 0 || targetBrick == null)
        {
            return false;
        }

        if (supportedBricks.Count <= 1)
        {
            wallBreakClickCounts.Remove(targetBrick);
            return true;
        }

        // Depth-based click mode is the primary break rule for realistic "lower bricks need more clicks".
        if (enableDepthBasedWallBreakClicks)
        {
            int requiredClicks = GetRequiredBreakClicksForBrickDepth(targetBrick, supportedBricks);
            int accumulatedClicks = IncrementWallBreakClickCount(targetBrick);
            if (accumulatedClicks < requiredClicks)
            {
                return false;
            }

            wallBreakClickCounts.Remove(targetBrick);
            return true;
        }

        float requiredImpulse = CalculateRequiredSupportBreakImpulse(
            supportedBricks,
            targetBrick,
            forceApplicationPoint);

        if (!requireImpulseToBreakSupports || impulseMagnitude >= requiredImpulse)
        {
            wallBreakClickCounts.Remove(targetBrick);
            return true;
        }
        return false;
    }

    private float CalculateRequiredSupportBreakImpulse(
        List<Transform> supportedBricks,
        Transform targetBrick,
        Vector3? forceApplicationPoint)
    {
        float requiredImpulse = CalculateCombinedMass(supportedBricks) * Mathf.Max(0f, supportBreakImpulsePerMass);
        requiredImpulse *= GetBottomToTopImpulseMultiplier(targetBrick, forceApplicationPoint);
        return requiredImpulse;
    }

    private int GetRequiredBreakClicksForBrickDepth(Transform targetBrick, List<Transform> connectedBricks = null)
    {
        int topClicks = Mathf.Max(1, wallBreakTopBrickClicks);
        int bottomMinClicks = Mathf.Max(topClicks, wallBreakBottomBrickMinClicks);
        int bottomMaxClicks = Mathf.Max(bottomMinClicks, wallBreakBottomBrickMaxClicks);

        List<Transform> depthBricks = connectedBricks != null
            ? new List<Transform>(connectedBricks)
            : CollectConnectedSupportBricks(targetBrick);

        depthBricks.RemoveAll(brick => brick == null || detachedWallBricks.Contains(brick));
        if (targetBrick != null && !depthBricks.Contains(targetBrick))
        {
            depthBricks.Add(targetBrick);
        }

        if (depthBricks.Count <= 1)
        {
            return topClicks;
        }

        depthBricks.Sort(CompareBrickHeightDescending);
        int depthFromTop = depthBricks.IndexOf(targetBrick);
        if (depthFromTop < 0)
        {
            depthFromTop = 0;
        }

        float normalizedDepth = depthBricks.Count > 1
            ? depthFromTop / (float)(depthBricks.Count - 1)
            : 0f;
        float minClicksAtDepth = Mathf.Lerp(topClicks, bottomMinClicks, normalizedDepth);
        float maxClicksAtDepth = Mathf.Lerp(topClicks, bottomMaxClicks, normalizedDepth);
        float noise = GetDeterministicNoise01(targetBrick.GetInstanceID());
        float interpolatedClicks = Mathf.Lerp(minClicksAtDepth, maxClicksAtDepth, noise);
        return Mathf.Clamp(Mathf.RoundToInt(interpolatedClicks), topClicks, bottomMaxClicks);
    }

    private int IncrementWallBreakClickCount(Transform targetBrick)
    {
        if (targetBrick == null)
        {
            return 0;
        }

        if (!wallBreakClickCounts.TryGetValue(targetBrick, out int currentClicks))
        {
            currentClicks = 0;
        }

        currentClicks++;
        wallBreakClickCounts[targetBrick] = currentClicks;
        return currentClicks;
    }

    private List<Transform> CollectConnectedSupportBricks(Transform startBrick)
    {
        List<Transform> connectedBricks = new List<Transform>();
        if (startBrick == null)
        {
            return connectedBricks;
        }

        Queue<Transform> queue = new Queue<Transform>();
        HashSet<Transform> visited = new HashSet<Transform>();
        queue.Enqueue(startBrick);
        visited.Add(startBrick);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            connectedBricks.Add(current);

            foreach (Transform candidate in plannedWallBricks)
            {
                if (candidate == null || visited.Contains(candidate) || detachedWallBricks.Contains(candidate))
                {
                    continue;
                }

                if (!HasDirectSupportConnection(candidate, current))
                {
                    continue;
                }

                visited.Add(candidate);
                queue.Enqueue(candidate);
            }
        }

        ExpandWithJointConnectedBricks(connectedBricks);
        return connectedBricks;
    }

    private bool HasDirectSupportConnection(Transform firstBrick, Transform secondBrick)
    {
        return IsDirectlySupportedBy(firstBrick, secondBrick) || IsDirectlySupportedBy(secondBrick, firstBrick);
    }

    private static int CompareBrickHeightDescending(Transform firstBrick, Transform secondBrick)
    {
        if (firstBrick == secondBrick)
        {
            return 0;
        }

        if (firstBrick == null)
        {
            return 1;
        }

        if (secondBrick == null)
        {
            return -1;
        }

        float firstY = GetBrickWorldBounds(firstBrick).center.y;
        float secondY = GetBrickWorldBounds(secondBrick).center.y;
        return secondY.CompareTo(firstY);
    }

    private static float GetDeterministicNoise01(int seed)
    {
        float noise = Mathf.Sin((seed * 12.9898f) + 78.233f) * 43758.5453f;
        return noise - Mathf.Floor(noise);
    }

    private float GetBottomToTopImpulseMultiplier(Transform targetBrick, Vector3? forceApplicationPoint)
    {
        if (targetBrick == null ||
            !forceApplicationPoint.HasValue ||
            supportBreakBottomHitMultiplier <= 1f)
        {
            return 1f;
        }

        Bounds targetBounds = GetBrickWorldBounds(targetBrick);
        float normalizedHitHeight = Mathf.InverseLerp(targetBounds.min.y, targetBounds.max.y, forceApplicationPoint.Value.y);
        return Mathf.Lerp(supportBreakBottomHitMultiplier, 1f, normalizedHitHeight);
    }

    private Vector3 ResolveForceApplicationPoint(Transform targetBrick, Rigidbody targetRigidbody, Vector3? forceApplicationPoint)
    {
        if (targetRigidbody == null || !forceApplicationPoint.HasValue)
        {
            return targetRigidbody != null ? targetRigidbody.worldCenterOfMass : Vector3.zero;
        }

        if (!wallBreakUseHitHeightOnly || targetBrick == null)
        {
            return forceApplicationPoint.Value;
        }

        Bounds targetBounds = GetBrickWorldBounds(targetBrick);
        Vector3 centerOfMass = targetRigidbody.worldCenterOfMass;
        float clampedHitY = Mathf.Clamp(forceApplicationPoint.Value.y, targetBounds.min.y, targetBounds.max.y);
        return new Vector3(centerOfMass.x, clampedHitY, centerOfMass.z);
    }

    private Vector3 ResolveWallBreakForceDirection(Vector3 rawDirection)
    {
        Vector3 normalizedDirection = rawDirection.normalized;
        if (!wallBreakHorizontalForceOnly)
        {
            return normalizedDirection;
        }

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(normalizedDirection, Vector3.up);
        if (horizontalDirection.sqrMagnitude <= 0.000001f)
        {
            return normalizedDirection;
        }

        return horizontalDirection.normalized;
    }

    private static float CalculateCombinedMass(List<Transform> bricks)
    {
        if (bricks == null || bricks.Count == 0)
        {
            return 0f;
        }

        float totalMass = 0f;
        for (int i = 0; i < bricks.Count; i++)
        {
            Transform brickTransform = bricks[i];
            if (brickTransform == null)
            {
                continue;
            }

            Rigidbody rb = brickTransform.GetComponent<Rigidbody>();
            totalMass += rb != null ? rb.mass : 1f;
        }

        return totalMass;
    }

    private List<Transform> CollectSupportedBricks(Transform baseBrick)
    {
        List<Transform> result = new List<Transform>();
        Queue<Transform> queue = new Queue<Transform>();
        HashSet<Transform> visited = new HashSet<Transform>();

        queue.Enqueue(baseBrick);
        visited.Add(baseBrick);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            result.Add(current);

            foreach (Transform candidate in plannedWallBricks)
            {
                if (candidate == null || visited.Contains(candidate) || detachedWallBricks.Contains(candidate))
                {
                    continue;
                }

                if (!IsDirectlySupportedBy(candidate, current))
                {
                    continue;
                }

                visited.Add(candidate);
                queue.Enqueue(candidate);
            }
        }

        return result;
    }

    private bool IsDirectlySupportedBy(Transform upperBrick, Transform lowerBrick)
    {
        Bounds upperBounds = GetBrickWorldBounds(upperBrick);
        Bounds lowerBounds = GetBrickWorldBounds(lowerBrick);

        if (upperBounds.center.y <= lowerBounds.center.y)
        {
            return false;
        }

        float verticalGap = Mathf.Abs(upperBounds.min.y - lowerBounds.max.y);
        if (verticalGap > supportVerticalTolerance)
        {
            return false;
        }

        float overlapX = Mathf.Min(upperBounds.max.x, lowerBounds.max.x) - Mathf.Max(upperBounds.min.x, lowerBounds.min.x);
        float overlapZ = Mathf.Min(upperBounds.max.z, lowerBounds.max.z) - Mathf.Max(upperBounds.min.z, lowerBounds.min.z);

        return overlapX >= supportMinOverlap && overlapZ >= supportMinOverlap;
    }

    private static Bounds GetBrickWorldBounds(Transform brickTransform)
    {
        Collider[] colliders = brickTransform.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return new Bounds(brickTransform.position, Vector3.zero);
        }

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }

        return bounds;
    }

    private static Transform ResolveBrickRootFromHit(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return null;
        }

        Rigidbody attachedRigidbody = hitTransform.GetComponentInParent<Rigidbody>();
        return attachedRigidbody != null ? attachedRigidbody.transform : hitTransform;
    }

    private void ReleaseBrickForPhysics(Transform brickTransform)
    {
        if (brickTransform == null || detachedWallBricks.Contains(brickTransform))
        {
            return;
        }

        RemoveExistingSnapJoints(brickTransform);

        Rigidbody rb = brickTransform.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = brickTransform.gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        detachedWallBricks.Add(brickTransform);
        wallBreakClickCounts.Remove(brickTransform);
    }

    private void ExpandWithJointConnectedBricks(List<Transform> baseBricks)
    {
        if (baseBricks == null || baseBricks.Count == 0)
        {
            return;
        }

        Queue<Transform> queue = new Queue<Transform>();
        HashSet<Transform> visited = new HashSet<Transform>();

        for (int i = 0; i < baseBricks.Count; i++)
        {
            Transform brick = baseBricks[i];
            if (brick == null || !visited.Add(brick))
            {
                continue;
            }

            queue.Enqueue(brick);
        }

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            foreach (Transform candidate in plannedWallBricks)
            {
                if (candidate == null || detachedWallBricks.Contains(candidate) || visited.Contains(candidate))
                {
                    continue;
                }

                if (!HasFixedJointConnection(current, candidate))
                {
                    continue;
                }

                visited.Add(candidate);
                queue.Enqueue(candidate);
                baseBricks.Add(candidate);
            }
        }
    }

    private static bool HasFixedJointConnection(Transform firstBrick, Transform secondBrick)
    {
        return HasFixedJointTo(firstBrick, secondBrick) || HasFixedJointTo(secondBrick, firstBrick);
    }

    private static bool HasFixedJointTo(Transform sourceBrick, Transform targetBrick)
    {
        if (sourceBrick == null || targetBrick == null)
        {
            return false;
        }

        FixedJoint[] joints = sourceBrick.GetComponents<FixedJoint>();
        for (int i = 0; i < joints.Length; i++)
        {
            FixedJoint joint = joints[i];
            if (joint == null || joint.connectedBody == null)
            {
                continue;
            }

            if (joint.connectedBody.transform == targetBrick)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ClampWallBreakImpulse(Vector3 requestedImpulse, Rigidbody targetRigidbody)
    {
        if (targetRigidbody == null)
        {
            return requestedImpulse;
        }

        float capPerMass = Mathf.Max(0.1f, wallBreakMaxImpulsePerMass);
        float maxImpulse = Mathf.Max(0.1f, targetRigidbody.mass * capPerMass);
        float requestedMagnitude = requestedImpulse.magnitude;
        if (requestedMagnitude <= maxImpulse || requestedMagnitude <= 0.0001f)
        {
            return requestedImpulse;
        }

        return requestedImpulse * (maxImpulse / requestedMagnitude);
    }

    private bool DidPressWallBreakMouseButtonThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return wallBreakMouseButton switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => mouse.leftButton.wasPressedThisFrame
            };
        }
#endif
        return Input.GetMouseButtonDown(wallBreakMouseButton);
    }

    private bool IsWallBreakModifierPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (wallBreakModifierKey == KeyCode.LeftShift || wallBreakModifierKey == KeyCode.RightShift)
            {
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }

            if (wallBreakModifierKey == KeyCode.LeftControl || wallBreakModifierKey == KeyCode.RightControl)
            {
                return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            }

            if (wallBreakModifierKey == KeyCode.LeftAlt || wallBreakModifierKey == KeyCode.RightAlt)
            {
                return keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            }
        }
#endif
        if (wallBreakModifierKey == KeyCode.LeftShift || wallBreakModifierKey == KeyCode.RightShift)
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        if (wallBreakModifierKey == KeyCode.LeftControl || wallBreakModifierKey == KeyCode.RightControl)
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        if (wallBreakModifierKey == KeyCode.LeftAlt || wallBreakModifierKey == KeyCode.RightAlt)
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        return Input.GetKey(wallBreakModifierKey);
    }
}
