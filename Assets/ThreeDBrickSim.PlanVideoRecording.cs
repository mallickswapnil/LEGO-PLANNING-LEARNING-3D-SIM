using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

public partial class ThreeDBrickSim
{
    [Header("Plan Video Recording")]
    [SerializeField] private bool enablePlanVideoRecording = true;
    [SerializeField] private string planVideoOutputDirectoryName = "videos";
    [Min(1)]
    [SerializeField] private int planVideoFrameRate = 30;
    [Min(64)]
    [SerializeField] private int planVideoOutputWidth = 3840;
    [Min(64)]
    [SerializeField] private int planVideoOutputHeight = 2160;
    [SerializeField] private bool planVideoCaptureAudio = false;

#if UNITY_EDITOR
    private RecorderController activePlanVideoRecorderController;
    private RecorderControllerSettings activePlanVideoRecorderSettings;
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

        RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieRecorderSettings.name = "PlanExecutionRecorder";
        movieRecorderSettings.Enabled = true;
        movieRecorderSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
        movieRecorderSettings.VideoBitRateMode = VideoBitrateMode.High;
        movieRecorderSettings.OutputFile = relativeOutputPath;
        movieRecorderSettings.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = Mathf.Max(64, planVideoOutputWidth),
            OutputHeight = Mathf.Max(64, planVideoOutputHeight)
        };

        if (movieRecorderSettings.AudioInputSettings != null)
        {
            movieRecorderSettings.AudioInputSettings.PreserveAudio = planVideoCaptureAudio;
        }

        controllerSettings.AddRecorderSettings(movieRecorderSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = Mathf.Max(1, planVideoFrameRate);
        controllerSettings.CapFrameRate = true;

        RecorderController recorderController = new RecorderController(controllerSettings);
        recorderController.PrepareRecording();
        recorderController.StartRecording();

        activePlanVideoRecorderController = recorderController;
        activePlanVideoRecorderSettings = controllerSettings;
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

        activePlanVideoRecorderController = null;
        activePlanVideoOutputPath = null;
#endif
    }

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
