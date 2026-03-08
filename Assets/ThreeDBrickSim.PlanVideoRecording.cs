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

#if UNITY_EDITOR
    private RecorderController activePlanVideoRecorderController;
    private RecorderControllerSettings activePlanVideoRecorderSettings;
    private MovieRecorderSettings activePlanVideoMovieRecorderSettings;
    private string activePlanVideoOutputPath;
#endif

    private void StartPlanExecutionVideoRecording(string planName)
    {
        if (!enablePlanVideoRecording)
        {
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
            Debug.LogWarning("StartPlanExecutionVideoRecording: Recorder failed to start.");
            return;
        }

        activePlanVideoRecorderController = recorderController;
        activePlanVideoRecorderSettings = controllerSettings;
        activePlanVideoMovieRecorderSettings = movieRecorderSettings;
        activePlanVideoOutputPath = Path.Combine(absoluteOutputDirectory, fileNameWithoutExtension + ".mp4");

        Debug.Log($"Plan recording started: {activePlanVideoOutputPath}");
#else
        Debug.LogWarning("Plan video recording is only supported in the Unity Editor with com.unity.recorder installed.");
#endif
    }

    private void StopPlanExecutionVideoRecording()
    {
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

    private void OnDisable()
    {
        StopPlanExecutionVideoRecording();
    }
}
