using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class ThreeDBrickSim
{
    private void ConfigureCameraToFitEnvironment()
    {
        controlledCamera = Camera.main;
        if (controlledCamera == null)
        {
            controlledCamera = FindFirstObjectByType<Camera>();
            if (controlledCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                controlledCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
        }

        float halfLength = boundsLength * 0.5f;
        float halfWidth = boundsWidth * 0.5f;
        float maxHalfExtent = Mathf.Max(halfLength, halfWidth);
        float horizontalDistance = maxHalfExtent * cameraDistanceFactor;
        cameraOrbitDistance = Mathf.Sqrt((horizontalDistance * horizontalDistance) + (cameraHeight * cameraHeight));
        cameraPitch = Mathf.Atan2(cameraHeight, horizontalDistance) * Mathf.Rad2Deg;
        cameraPitch = Mathf.Clamp(cameraPitch, cameraMinPitch, cameraMaxPitch);

        controlledCamera.orthographic = false;
        controlledCamera.fieldOfView = cameraFov;
        controlledCamera.nearClipPlane = 0.1f;
        controlledCamera.farClipPlane = 1000f;
        controlledCamera.allowMSAA = true;
        controlledCamera.allowDynamicResolution = false;
        ApplyOrbitCameraTransform();
    }

    private void HandleCameraDragInput()
    {
        if (!enableCameraDrag || controlledCamera == null || IsPlanVideoCloseFollowCameraActive())
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (requireCtrlForCameraDrag && !IsControlPressed())
        {
            return;
        }

        if (!IsDragButtonHeld())
        {
            return;
        }

        Vector2 pointerDelta = GetPointerDelta();
        float mouseX = pointerDelta.x;
        float mouseY = pointerDelta.y;
        if (Mathf.Approximately(mouseX, 0f) && Mathf.Approximately(mouseY, 0f))
        {
            return;
        }

        cameraYaw += mouseX * cameraDragSensitivity;
        float verticalDirection = invertVerticalDrag ? 1f : -1f;
        cameraPitch = Mathf.Clamp(
            cameraPitch + (mouseY * cameraDragSensitivity * verticalDirection),
            cameraMinPitch,
            cameraMaxPitch);

        ApplyOrbitCameraTransform();
    }

    private bool IsControlPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }
#endif
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private bool IsDragButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return cameraDragMouseButton switch
            {
                0 => mouse.leftButton.isPressed,
                1 => mouse.rightButton.isPressed,
                2 => mouse.middleButton.isPressed,
                _ => mouse.leftButton.isPressed
            };
        }
#endif
        return Input.GetMouseButton(cameraDragMouseButton);
    }

    private static Vector2 GetPointerDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.delta.ReadValue();
        }
#endif
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
#endif
        return Input.mousePosition;
    }

    private void ApplyOrbitCameraTransform()
    {
        if (controlledCamera == null)
        {
            return;
        }

        float yawRadians = cameraYaw * Mathf.Deg2Rad;
        float pitchRadians = cameraPitch * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(pitchRadians) * cameraOrbitDistance;

        Vector3 offset = new Vector3(
            Mathf.Sin(yawRadians) * horizontalDistance,
            Mathf.Sin(pitchRadians) * cameraOrbitDistance,
            -Mathf.Cos(yawRadians) * horizontalDistance);

        controlledCamera.transform.position = boundsCenter + offset;
        controlledCamera.transform.LookAt(boundsCenter);
    }
}
