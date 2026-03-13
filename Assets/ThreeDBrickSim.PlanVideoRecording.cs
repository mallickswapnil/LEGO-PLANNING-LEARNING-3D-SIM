using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
#endif

public partial class ThreeDBrickSim
{
    private enum PlanVideoRecordingProfile
    {
        Fast,
        Custom
    }

    [Header("Plan Video Recording")]
    [SerializeField] private bool enablePlanVideoRecording = true;
    [SerializeField] private string planVideoOutputDirectoryName = "videos";
    [SerializeField] private PlanVideoRecordingProfile planVideoRecordingProfile = PlanVideoRecordingProfile.Fast;
    [Min(1)]
    [SerializeField] private int planVideoFrameRate = 30;
    [Min(64)]
    [SerializeField] private int planVideoOutputWidth = 3840;
    [Min(64)]
    [SerializeField] private int planVideoOutputHeight = 2160;
    [SerializeField] private bool planVideoCaptureAudio = false;
    [SerializeField] private bool planVideoUseTargetedCameraInput = true;
    [SerializeField] private bool planVideoCaptureUi = false;
    [Header("Plan Video Camera")]
    [SerializeField] private bool planVideoUseCloseFollowCamera = true;
    [Min(0.1f)]
    [SerializeField] private float planVideoCameraDistance = 18f;
    [SerializeField] private float planVideoCameraYawDegrees = 35f;
    [Range(5f, 85f)]
    [SerializeField] private float planVideoCameraPitchDegrees = 24f;
    [Min(0f)]
    [SerializeField] private float planVideoCameraFocusHeightOffset = 1.25f;
    [Min(0f)]
    [SerializeField] private float planVideoCameraSmoothTime = 0.18f;

#if UNITY_EDITOR
    private RecorderController activePlanVideoRecorderController;
    private RecorderControllerSettings activePlanVideoRecorderSettings;
    private MovieRecorderSettings activePlanVideoMovieRecorderSettings;
    private string activePlanVideoOutputPath;
#endif

    private bool planVideoCloseFollowCameraRequested;
    private bool planVideoCloseFollowCameraActive;
    private Vector3 planVideoCameraDesiredFocusPoint;
    private Vector3 planVideoCameraCurrentFocusPoint;
    private Vector3 planVideoCameraFocusVelocity;

    private void StartPlanExecutionVideoRecording(string planName)
    {
        if (!enablePlanVideoRecording)
        {
            planVideoCloseFollowCameraRequested = false;
            return;
        }

#if UNITY_EDITOR
        StopPlanExecutionVideoRecording();

        string outputDirectoryName = string.IsNullOrWhiteSpace(planVideoOutputDirectoryName)
            ? "videos"
            : planVideoOutputDirectoryName.Trim();
        string safePlanName = SanitizePlanVideoPathSegment(planName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileNameWithoutExtension = $"{safePlanName}_{timestamp}";
        string relativeDirectory = outputDirectoryName.Replace('\\', '/');
        string relativeOutputPath = string.Concat(relativeDirectory, "/", fileNameWithoutExtension);

        string projectRoot = ResolveProjectRootPath();
        string absoluteOutputDirectory = Path.Combine(projectRoot, outputDirectoryName);
        Directory.CreateDirectory(absoluteOutputDirectory);

        int outputWidth = ResolvePlanVideoOutputWidth();
        int outputHeight = ResolvePlanVideoOutputHeight();
        int frameRate = ResolvePlanVideoFrameRate();

        RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieRecorderSettings.name = "PlanExecutionRecorder";
        movieRecorderSettings.Enabled = true;
        movieRecorderSettings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = ResolvePlanVideoEncodingQuality()
        };
        movieRecorderSettings.OutputFile = relativeOutputPath;
        movieRecorderSettings.CaptureAudio = planVideoCaptureAudio;
        movieRecorderSettings.ImageInputSettings = CreatePlanVideoInputSettings(outputWidth, outputHeight);

        if (movieRecorderSettings.AudioInputSettings != null)
        {
            movieRecorderSettings.AudioInputSettings.PreserveAudio = planVideoCaptureAudio;
        }

        controllerSettings.AddRecorderSettings(movieRecorderSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = frameRate;
        controllerSettings.CapFrameRate = true;

        RecorderController recorderController = new RecorderController(controllerSettings);
        recorderController.PrepareRecording();
        if (!recorderController.StartRecording())
        {
            Destroy(movieRecorderSettings);
            Destroy(controllerSettings);
            planVideoCloseFollowCameraRequested = false;
            Debug.LogWarning("StartPlanExecutionVideoRecording: Recorder failed to start.");
            return;
        }

        activePlanVideoRecorderController = recorderController;
        activePlanVideoRecorderSettings = controllerSettings;
        activePlanVideoMovieRecorderSettings = movieRecorderSettings;
        activePlanVideoOutputPath = Path.Combine(absoluteOutputDirectory, fileNameWithoutExtension + ".mp4");
        planVideoCloseFollowCameraRequested = planVideoUseCloseFollowCamera;

        Debug.Log($"Plan recording started: {activePlanVideoOutputPath}");
#else
        planVideoCloseFollowCameraRequested = false;
        Debug.LogWarning("Plan video recording is only supported in the Unity Editor with com.unity.recorder installed.");
#endif
    }

    private void StopPlanExecutionVideoRecording()
    {
        DeactivatePlanVideoCamera();

#if UNITY_EDITOR
        if (activePlanVideoRecorderController == null)
        {
            return;
        }

        try
        {
            activePlanVideoRecorderController.StopRecording();
            if (!string.IsNullOrWhiteSpace(activePlanVideoOutputPath))
            {
                Debug.Log($"Plan recording saved: {activePlanVideoOutputPath}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StopPlanExecutionVideoRecording: Failed to stop recording cleanly: {exception.Message}");
        }

        if (activePlanVideoRecorderSettings != null)
        {
            Destroy(activePlanVideoRecorderSettings);
            activePlanVideoRecorderSettings = null;
        }

        if (activePlanVideoMovieRecorderSettings != null)
        {
            Destroy(activePlanVideoMovieRecorderSettings);
            activePlanVideoMovieRecorderSettings = null;
        }

        activePlanVideoRecorderController = null;
        activePlanVideoOutputPath = null;
#endif
    }

#if UNITY_EDITOR
    private int ResolvePlanVideoFrameRate()
    {
        return planVideoRecordingProfile == PlanVideoRecordingProfile.Fast
            ? 24
            : Mathf.Max(1, planVideoFrameRate);
    }

    private int ResolvePlanVideoOutputWidth()
    {
        return planVideoRecordingProfile == PlanVideoRecordingProfile.Fast
            ? 1280
            : Mathf.Max(64, planVideoOutputWidth);
    }

    private int ResolvePlanVideoOutputHeight()
    {
        return planVideoRecordingProfile == PlanVideoRecordingProfile.Fast
            ? 720
            : Mathf.Max(64, planVideoOutputHeight);
    }

    private CoreEncoderSettings.VideoEncodingQuality ResolvePlanVideoEncodingQuality()
    {
        return planVideoRecordingProfile == PlanVideoRecordingProfile.Fast
            ? CoreEncoderSettings.VideoEncodingQuality.Medium
            : CoreEncoderSettings.VideoEncodingQuality.High;
    }

    private ImageInputSettings CreatePlanVideoInputSettings(int outputWidth, int outputHeight)
    {
        if (planVideoUseTargetedCameraInput)
        {
            return new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                CaptureUI = planVideoCaptureUi,
                OutputWidth = outputWidth,
                OutputHeight = outputHeight
            };
        }

        return new GameViewInputSettings
        {
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }
#endif

    private static string ResolveProjectRootPath()
    {
        string assetsPath = Application.dataPath;
        string projectRoot = Path.GetDirectoryName(assetsPath);
        return string.IsNullOrWhiteSpace(projectRoot) ? assetsPath : projectRoot;
    }

    private static string SanitizePlanVideoPathSegment(string value)
    {
        string fallback = "plan";
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            candidate = candidate.Replace(invalidChars[i], '_');
        }

        candidate = candidate.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private bool IsPlanVideoCloseFollowCameraActive()
    {
        return planVideoCloseFollowCameraActive && controlledCamera != null;
    }

    private void PreparePlanVideoCameraForPlan(ThreeDBrickSimPlan plan)
    {
        if (!planVideoCloseFollowCameraRequested || controlledCamera == null || plan == null || plan.steps == null || plan.steps.Length == 0)
        {
            planVideoCloseFollowCameraActive = false;
            return;
        }

        Vector3 initialFocusPoint = GetPlanVideoCameraFocusPoint(plan.steps[0]);
        planVideoCloseFollowCameraActive = true;
        planVideoCameraDesiredFocusPoint = initialFocusPoint;
        planVideoCameraCurrentFocusPoint = initialFocusPoint;
        planVideoCameraFocusVelocity = Vector3.zero;
        ApplyPlanVideoCameraTransform(initialFocusPoint);
    }

    private void SetPlanVideoCameraTarget(ThreeDBrickSimPlanStep step, bool snapImmediately)
    {
        if (!planVideoCloseFollowCameraRequested || controlledCamera == null || step == null)
        {
            return;
        }

        Vector3 focusPoint = GetPlanVideoCameraFocusPoint(step);
        planVideoCloseFollowCameraActive = true;
        planVideoCameraDesiredFocusPoint = focusPoint;

        if (snapImmediately)
        {
            planVideoCameraCurrentFocusPoint = focusPoint;
            planVideoCameraFocusVelocity = Vector3.zero;
            ApplyPlanVideoCameraTransform(focusPoint);
        }
    }

    private void UpdatePlanVideoCamera()
    {
        if (!planVideoCloseFollowCameraActive || controlledCamera == null)
        {
            return;
        }

        if (planVideoCameraSmoothTime <= 0f)
        {
            planVideoCameraCurrentFocusPoint = planVideoCameraDesiredFocusPoint;
        }
        else
        {
            planVideoCameraCurrentFocusPoint = Vector3.SmoothDamp(
                planVideoCameraCurrentFocusPoint,
                planVideoCameraDesiredFocusPoint,
                ref planVideoCameraFocusVelocity,
                planVideoCameraSmoothTime);
        }

        ApplyPlanVideoCameraTransform(planVideoCameraCurrentFocusPoint);
    }

    private void DeactivatePlanVideoCamera()
    {
        planVideoCloseFollowCameraRequested = false;
        planVideoCloseFollowCameraActive = false;
        planVideoCameraDesiredFocusPoint = Vector3.zero;
        planVideoCameraCurrentFocusPoint = Vector3.zero;
        planVideoCameraFocusVelocity = Vector3.zero;

        if (controlledCamera != null)
        {
            ApplyOrbitCameraTransform();
        }
    }

    private Vector3 GetPlanVideoCameraFocusPoint(ThreeDBrickSimPlanStep step)
    {
        return step.targetPosition + (Vector3.up * planVideoCameraFocusHeightOffset);
    }

    private void ApplyPlanVideoCameraTransform(Vector3 focusPoint)
    {
        Vector3 cameraPosition = focusPoint + GetPlanVideoCameraOffset();
        controlledCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(focusPoint - cameraPosition, Vector3.up));
    }

    private Vector3 GetPlanVideoCameraOffset()
    {
        float yawRadians = planVideoCameraYawDegrees * Mathf.Deg2Rad;
        float pitchRadians = planVideoCameraPitchDegrees * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(pitchRadians) * planVideoCameraDistance;

        return new Vector3(
            Mathf.Sin(yawRadians) * horizontalDistance,
            Mathf.Sin(pitchRadians) * planVideoCameraDistance,
            -Mathf.Cos(yawRadians) * horizontalDistance);
    }

    private void OnDisable()
    {
        StopPlanExecutionVideoRecording();
    }
}
