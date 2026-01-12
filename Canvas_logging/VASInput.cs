using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VASInput : MonoBehaviour
{
    [Header("UI")]
    public Slider vasSlider;                 // assign the Slider under your BarArea
    public RectTransform panelArea;          // the BarArea RectTransform
    public Canvas canvas;                    // your SessionUI_Canvas
    public TextMeshProUGUI promptLabel;      // TMP prompt text

    [Header("Meta")]
    public string metricName = "VAS";        // e.g., "pain", "liking", "difficulty"

    [Header("Input")]
    public bool submitOnRightClick = true;   // right mouse button
    public KeyCode submitKey = KeyCode.Return;

    [Header("Cursor")]
    public bool unlockCursorWhileActive = true;

    // Raised on submit (value)
    public event Action<float> OnSubmitted;

    private bool isActive = true;
    private CursorLockMode _prevLock;
    private bool _prevVisible;

    void OnEnable()
    {
        // Pause world
        Time.timeScale = 0f;
        isActive = true;

        // Show cursor (FPS controllers usually lock it)
        if (unlockCursorWhileActive)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        DataLogger.Instance?.LogEvent("VAS_SHOW", "metric", metricName);
    }

    void Update()
    {
        if (!isActive || panelArea == null || vasSlider == null) return;

        // Choose the right event camera for ScreenSpaceCamera/WorldSpace
        Camera eventCam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        // Map mouse X within panelArea to 0..1 robustly using rect.xMin/xMax
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelArea, Input.mousePosition, eventCam, out var local))
        {
            Rect r = panelArea.rect; // local space rect (pivot-aware)
            float nx = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            nx = Mathf.Clamp01(nx);

            vasSlider.value = Mathf.Lerp(vasSlider.minValue, vasSlider.maxValue, nx);
        }

        // Submit
        if ((submitOnRightClick && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(submitKey))
            SubmitVASValue(vasSlider.value);
    }

    public void Configure(string metric, string prompt)
    {
        metricName = metric;
        if (promptLabel) promptLabel.text = prompt;
    }

    void SubmitVASValue(float value)
    {
        Debug.Log($"VAS Submitted [{metricName}]: {value:F3}");
        DataLogger.Instance?.LogEvent("VAS_SUBMIT", "metric", metricName, "value", value.ToString("F3"));
        try { OnSubmitted?.Invoke(value); } catch { /* ignore */ }

        isActive = false;

        // Resume world
        Time.timeScale = 1f;

        // Restore cursor state
        if (unlockCursorWhileActive)
        {
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        // Hide panel
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // Safety: ensure timescale & cursor restored even if disabled externally
        Time.timeScale = 1f;
        if (unlockCursorWhileActive)
        {
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }
    }
}
