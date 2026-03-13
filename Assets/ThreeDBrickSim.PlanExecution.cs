using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class ThreeDBrickSim
{
    private static string queuedPlanExecutionResourceName;
    private static bool queuedPlanExecutionShouldRecordVideo;

    private IEnumerator ExecutePlanFromResources(string resourceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                Debug.LogWarning("ExecutePlanFromResources: resource name is empty.");
                yield break;
            }

            TextAsset planAsset = Resources.Load<TextAsset>(resourceName);
            if (planAsset == null)
            {
                Debug.LogWarning($"ExecutePlanFromResources: Could not find Resources/{resourceName}.json");
                yield break;
            }

            ThreeDBrickSimPlan plan = JsonUtility.FromJson<ThreeDBrickSimPlan>(planAsset.text);
            if (plan == null || plan.steps == null || plan.steps.Length == 0)
            {
                Debug.LogWarning("ExecutePlanFromResources: plan has no steps.");
                yield break;
            }

            PreparePlanVideoCameraForPlan(plan);
            SetPlanVideoCameraTarget(plan.steps[0], snapImmediately: true);

            for (int i = 0; i < plan.steps.Length; i++)
            {
                ThreeDBrickSimPlanStep step = plan.steps[i];
                SetPlanVideoCameraTarget(step, snapImmediately: false);
                if (logPlacementDebug)
                {
                    Debug.Log($"Plan step {i + 1}/{plan.steps.Length}: brick='{step.brickId}', targetPosition={step.targetPosition}, targetRotation={step.targetRotation}.");
                }

                bool success = PickOrientAndPlaceBrick(step.brickId, step.targetRotation, step.targetPosition);
                if (!success)
                {
                    Debug.LogWarning($"Plan step {i + 1} failed for brick '{step.brickId}'.");
                    Debug.LogWarning($"Plan execution stopped at failed step {i + 1}.");
                    yield break;
                }

                while (activePlaexSideInsertionCount > 0)
                {
                    yield return new WaitForFixedUpdate();
                }

                // Allow one fixed step so MovePosition/physics-settled pose is reflected in logs.
                yield return new WaitForFixedUpdate();

                if (logPlacementDebug)
                {
                    Transform placedBrick = FindBrickTransformById(step.brickId);
                    if (placedBrick != null)
                    {
                        Debug.Log(
                            $"Plan step {i + 1} settled pose: brick='{step.brickId}', finalPosition={placedBrick.position}, finalRotation={placedBrick.rotation.eulerAngles}.");
                    }
                }

                if (step.delay > 0f)
                {
                    yield return new WaitForSeconds(step.delay);
                }
                else
                {
                    yield return null;
                }
            }
        }
        finally
        {
            StopPlanExecutionVideoRecording();
        }
    }

    private void InitializePlanUiAndExecution()
    {
        LoadAvailablePlanNames();
        if (availablePlanNames.Count == 0)
        {
            Debug.LogWarning("No plan JSON files found in Resources.");
            return;
        }

        EnsureEventSystem();
        CreatePlanDropdownUi();
        SetDropdownValueSilently(0);
        TryStartQueuedPlanExecution();
    }

    private void LoadAvailablePlanNames()
    {
        availablePlanNames.Clear();
        TextAsset[] planAssets = Resources.LoadAll<TextAsset>("");
        for (int i = 0; i < planAssets.Length; i++)
        {
            TextAsset planAsset = planAssets[i];
            if (planAsset == null || string.IsNullOrWhiteSpace(planAsset.name))
            {
                continue;
            }

            if (!planAsset.name.EndsWith("_plan", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!availablePlanNames.Contains(planAsset.name))
            {
                availablePlanNames.Add(planAsset.name);
            }
        }

        availablePlanNames.Sort(System.StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void CreatePlanDropdownUi()
    {
        DefaultControls.Resources resources = new DefaultControls.Resources();

        GameObject canvasObject = new GameObject("PlanSelectionCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = DefaultControls.CreatePanel(resources);
        panelObject.name = "PlanSelectionPanel";
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(planUiLeftMargin, -planUiTopMargin);
        panelRect.sizeDelta = new Vector2(planUiWidth, 170f);
        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color32(16, 24, 36, 220);
        }

        GameObject labelObject = DefaultControls.CreateText(resources);
        labelObject.name = "PlanSelectionLabel";
        labelObject.transform.SetParent(panelObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(12f, -32f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        Text labelText = labelObject.GetComponent<Text>();
        labelText.text = "Plan";
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = new Color32(238, 245, 255, 255);
        labelText.fontSize = 18;
        labelText.fontStyle = FontStyle.Bold;

        GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
        dropdownObject.name = "PlanDropdown";
        dropdownObject.transform.SetParent(panelObject.transform, false);

        RectTransform dropdownRect = dropdownObject.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0f, 1f);
        dropdownRect.anchorMax = new Vector2(1f, 1f);
        dropdownRect.pivot = new Vector2(0.5f, 1f);
        dropdownRect.offsetMin = new Vector2(12f, -66f);
        dropdownRect.offsetMax = new Vector2(-12f, -66f + planUiDropdownHeight);

        planDropdown = dropdownObject.GetComponent<Dropdown>();
        StylePlanDropdown(dropdownObject, planDropdown);
        planDropdown.options.Clear();
        planDropdown.options.Add(new Dropdown.OptionData("Select a plan..."));
        for (int i = 0; i < availablePlanNames.Count; i++)
        {
            planDropdown.options.Add(new Dropdown.OptionData(availablePlanNames[i]));
        }

        planDropdown.onValueChanged.AddListener(OnPlanDropdownValueChanged);

        GameObject refreshButtonObject = DefaultControls.CreateButton(resources);
        refreshButtonObject.name = "RefreshButton";
        refreshButtonObject.transform.SetParent(panelObject.transform, false);

        RectTransform refreshButtonRect = refreshButtonObject.GetComponent<RectTransform>();
        refreshButtonRect.anchorMin = new Vector2(0f, 1f);
        refreshButtonRect.anchorMax = new Vector2(1f, 1f);
        refreshButtonRect.pivot = new Vector2(0.5f, 1f);
        refreshButtonRect.offsetMin = new Vector2(12f, -106f);
        refreshButtonRect.offsetMax = new Vector2(-12f, -106f + planUiButtonHeight);

        Text refreshButtonText = refreshButtonObject.GetComponentInChildren<Text>();
        if (refreshButtonText != null)
        {
            refreshButtonText.text = "Refresh";
            refreshButtonText.color = Color.white;
            refreshButtonText.fontSize = 15;
            refreshButtonText.fontStyle = FontStyle.Bold;
        }

        Button refreshButton = refreshButtonObject.GetComponent<Button>();
        ColorBlock buttonColors = refreshButton.colors;
        buttonColors.normalColor = new Color32(41, 128, 185, 255);
        buttonColors.highlightedColor = new Color32(52, 152, 219, 255);
        buttonColors.pressedColor = new Color32(31, 97, 141, 255);
        buttonColors.selectedColor = new Color32(52, 152, 219, 255);
        buttonColors.disabledColor = new Color32(70, 70, 70, 140);
        buttonColors.colorMultiplier = 1f;
        buttonColors.fadeDuration = 0.08f;
        refreshButton.colors = buttonColors;
        refreshButton.onClick.AddListener(OnRefreshButtonClicked);

        GameObject exportButtonObject = DefaultControls.CreateButton(resources);
        exportButtonObject.name = "ExportVideoButton";
        exportButtonObject.transform.SetParent(panelObject.transform, false);

        RectTransform exportButtonRect = exportButtonObject.GetComponent<RectTransform>();
        exportButtonRect.anchorMin = new Vector2(0f, 1f);
        exportButtonRect.anchorMax = new Vector2(1f, 1f);
        exportButtonRect.pivot = new Vector2(0.5f, 1f);
        exportButtonRect.offsetMin = new Vector2(12f, -146f);
        exportButtonRect.offsetMax = new Vector2(-12f, -146f + planUiButtonHeight);

        Text exportButtonText = exportButtonObject.GetComponentInChildren<Text>();
        if (exportButtonText != null)
        {
            exportButtonText.text = "Export Video";
            exportButtonText.color = Color.white;
            exportButtonText.fontSize = 15;
            exportButtonText.fontStyle = FontStyle.Bold;
        }

        Button exportButton = exportButtonObject.GetComponent<Button>();
        ColorBlock exportColors = exportButton.colors;
        exportColors.normalColor = new Color32(46, 134, 92, 255);
        exportColors.highlightedColor = new Color32(62, 168, 116, 255);
        exportColors.pressedColor = new Color32(34, 99, 68, 255);
        exportColors.selectedColor = new Color32(62, 168, 116, 255);
        exportColors.disabledColor = new Color32(70, 70, 70, 140);
        exportColors.colorMultiplier = 1f;
        exportColors.fadeDuration = 0.08f;
        exportButton.colors = exportColors;
        exportButton.onClick.AddListener(OnExportVideoButtonClicked);
    }

    private static void StylePlanDropdown(GameObject dropdownObject, Dropdown dropdown)
    {
        Image dropdownImage = dropdownObject.GetComponent<Image>();
        if (dropdownImage != null)
        {
            dropdownImage.color = new Color32(245, 249, 255, 255);
        }

        Text captionText = dropdown.captionText;
        if (captionText != null)
        {
            captionText.color = new Color32(19, 26, 39, 255);
            captionText.fontSize = 15;
            captionText.fontStyle = FontStyle.Bold;
        }

        Text itemText = dropdown.itemText;
        if (itemText != null)
        {
            itemText.color = new Color32(242, 247, 255, 255);
            itemText.fontSize = 14;
            itemText.fontStyle = FontStyle.Bold;
        }

        Transform arrow = dropdownObject.transform.Find("Arrow");
        if (arrow != null)
        {
            Text arrowText = arrow.GetComponent<Text>();
            if (arrowText != null)
            {
                arrowText.color = new Color32(19, 26, 39, 255);
                arrowText.fontStyle = FontStyle.Bold;
            }
        }

        Transform template = dropdownObject.transform.Find("Template");
        if (template != null)
        {
            Image templateImage = template.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.color = new Color32(20, 30, 45, 245);
            }

            Transform viewport = template.Find("Viewport");
            if (viewport != null)
            {
                Image viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                {
                    viewportImage.color = new Color32(20, 30, 45, 240);
                }
            }

            Transform item = template.Find("Viewport/Content/Item");
            if (item != null)
            {
                Toggle itemToggle = item.GetComponent<Toggle>();
                if (itemToggle != null)
                {
                    ColorBlock colors = itemToggle.colors;
                    colors.normalColor = new Color32(28, 42, 60, 230);
                    colors.highlightedColor = new Color32(41, 60, 85, 255);
                    colors.pressedColor = new Color32(22, 34, 49, 255);
                    colors.selectedColor = new Color32(41, 60, 85, 255);
                    colors.disabledColor = new Color32(60, 60, 60, 130);
                    colors.colorMultiplier = 1f;
                    colors.fadeDuration = 0.08f;
                    itemToggle.colors = colors;
                }

                Image checkmarkImage = item.Find("Item Checkmark")?.GetComponent<Image>();
                if (checkmarkImage != null)
                {
                    checkmarkImage.color = new Color32(72, 201, 176, 255);
                }

                Text itemLabel = item.Find("Item Label")?.GetComponent<Text>();
                if (itemLabel != null)
                {
                    itemLabel.color = new Color32(244, 248, 255, 255);
                    itemLabel.fontStyle = FontStyle.Bold;
                    itemLabel.fontSize = 14;
                }
            }
        }

        ColorBlock dropdownColors = dropdown.colors;
        dropdownColors.normalColor = Color.white;
        dropdownColors.highlightedColor = new Color32(232, 240, 252, 255);
        dropdownColors.pressedColor = new Color32(209, 223, 243, 255);
        dropdownColors.selectedColor = new Color32(224, 236, 252, 255);
        dropdownColors.disabledColor = new Color32(100, 100, 100, 120);
        dropdownColors.colorMultiplier = 1f;
        dropdownColors.fadeDuration = 0.08f;
        dropdown.colors = dropdownColors;
    }

    private void SetDropdownValueSilently(int index)
    {
        if (planDropdown == null)
        {
            return;
        }

        isInitializingDropdown = true;
        planDropdown.value = index;
        planDropdown.RefreshShownValue();
        isInitializingDropdown = false;
    }

    private void OnPlanDropdownValueChanged(int selectedIndex)
    {
        if (isInitializingDropdown || !Application.isPlaying)
        {
            return;
        }

        if (selectedIndex <= 0)
        {
            return;
        }

        ExecutePlanByIndex(selectedIndex, false);
    }

    private void ExecutePlanByIndex(int selectedIndex, bool recordVideo)
    {
        int planIndex = selectedIndex - 1;
        if (planIndex < 0 || planIndex >= availablePlanNames.Count)
        {
            return;
        }

        string resourceName = availablePlanNames[planIndex];
        ExecutePlan(resourceName, recordVideo);
    }

    private void OnRefreshButtonClicked()
    {
        queuedPlanExecutionResourceName = null;
        queuedPlanExecutionShouldRecordVideo = false;
        RestartSceneForPlanExecution();
    }

    private void OnExportVideoButtonClicked()
    {
        string resourceName = ResolveSelectedPlanForExecution();
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            Debug.LogWarning("OnExportVideoButtonClicked: Select a plan before exporting a video.");
            return;
        }

        queuedPlanExecutionResourceName = resourceName;
        queuedPlanExecutionShouldRecordVideo = true;
        RestartSceneForPlanExecution();
    }

    private string ResolvePlanName()
    {
        string cliPlanName = GetCommandLinePlanName();
        if (!string.IsNullOrWhiteSpace(cliPlanName))
        {
            return cliPlanName;
        }

        if (!string.IsNullOrWhiteSpace(selectedPlanName))
        {
            return selectedPlanName;
        }

        return planResourceName;
    }

    private static string GetCommandLinePlanName()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "-plan", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int valueIndex = i + 1;
            if (valueIndex < args.Length && !string.IsNullOrWhiteSpace(args[valueIndex]))
            {
                return args[valueIndex];
            }

            return null;
        }

        return null;
    }

    private void ExecutePlan(string resourceName, bool recordVideo)
    {
        selectedPlanName = resourceName;

        if (activePlanCoroutine != null)
        {
            StopCoroutine(activePlanCoroutine);
            activePlanCoroutine = null;
        }

        StopPlanExecutionVideoRecording();
        if (recordVideo)
        {
            StartPlanExecutionVideoRecording(resourceName);
        }

        activePlanCoroutine = StartCoroutine(ExecutePlanFromResources(resourceName));
    }

    private string ResolveSelectedPlanForExecution()
    {
        if (planDropdown != null && planDropdown.value > 0)
        {
            int planIndex = planDropdown.value - 1;
            if (planIndex >= 0 && planIndex < availablePlanNames.Count)
            {
                return availablePlanNames[planIndex];
            }
        }

        string resolvedPlanName = ResolvePlanName();
        return availablePlanNames.Contains(resolvedPlanName) ? resolvedPlanName : null;
    }

    private void RestartSceneForPlanExecution()
    {
        if (activePlanCoroutine != null)
        {
            StopCoroutine(activePlanCoroutine);
            activePlanCoroutine = null;
        }

        StopPlanExecutionVideoRecording();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void TryStartQueuedPlanExecution()
    {
        if (string.IsNullOrWhiteSpace(queuedPlanExecutionResourceName))
        {
            return;
        }

        string resourceName = queuedPlanExecutionResourceName;
        bool recordVideo = queuedPlanExecutionShouldRecordVideo;
        queuedPlanExecutionResourceName = null;
        queuedPlanExecutionShouldRecordVideo = false;

        int planIndex = availablePlanNames.IndexOf(resourceName);
        if (planIndex < 0)
        {
            Debug.LogWarning($"TryStartQueuedPlanExecution: Could not find queued plan '{resourceName}'.");
            return;
        }

        SetDropdownValueSilently(planIndex + 1);
        ExecutePlan(resourceName, recordVideo);
    }
}
