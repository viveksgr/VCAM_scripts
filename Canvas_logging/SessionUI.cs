using System.Collections;
using UnityEngine;
using TMPro;

public class SessionUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pleaseWaitPanel;
    public GameObject instructionsPanel;
    public GameObject thankYouPanel;
    public GameObject quitPanel;

    [Header("Escape overlay (NEW)")]
    public GameObject escapePanel;
    public TextMeshProUGUI escapeTitle;
    public TextMeshProUGUI escapeCountdown;

    [Header("TMP Texts")]
    public TextMeshProUGUI pleaseWaitText;
    public TextMeshProUGUI instructionTitle;
    public TextMeshProUGUI instructionBody;
    public TextMeshProUGUI thankYouText;
    public TextMeshProUGUI quitText;

    [Header("VAS (single reusable panel)")]
    public VASInput vas; // assign your VAS panel/GameObject with VASInput component

    [Header("Behavior")]
    public bool pauseDuringOverlay = true;

    // ----- PLEASE WAIT -----
    public void ShowPleaseWaitInstant(string message = "Please wait…")
    {
        if (pleaseWaitText) pleaseWaitText.text = message;
        if (pauseDuringOverlay) Time.timeScale = 0f;
        if (pleaseWaitPanel) pleaseWaitPanel.SetActive(true);
    }

    public IEnumerator HidePleaseWaitAfter(float seconds = 0f)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
        if (pleaseWaitPanel) pleaseWaitPanel.SetActive(false);
        if (pauseDuringOverlay) Time.timeScale = 1f;
    }

    // ----- INSTRUCTIONS (per-block or break) -----
    public IEnumerator ShowInstructions(string title, string body, KeyCode advanceKey = KeyCode.Space)
    {
        // Require key to be released before we show the panel,
        // so the same Space press from a previous screen can't dismiss this one.
        while (Input.GetKey(advanceKey)) yield return null;             // wait release
        yield return null;                                              // one extra frame

        if (instructionTitle) instructionTitle.text = title ?? "";
        if (instructionBody) instructionBody.text = body ?? "";

        if (pauseDuringOverlay) Time.timeScale = 0f;
        if (instructionsPanel) instructionsPanel.SetActive(true);

        // Now wait for a fresh press
        while (!Input.GetKeyDown(advanceKey)) yield return null;

        if (instructionsPanel) instructionsPanel.SetActive(false);
        if (pauseDuringOverlay) Time.timeScale = 1f;
    }

    // ----- THANK YOU -----
    public void ShowThankYouPersistent(string message = "Thank you!")
    {
        if (thankYouText) thankYouText.text = message;
        if (pauseDuringOverlay) Time.timeScale = 0f;
        if (thankYouPanel) thankYouPanel.SetActive(true);
    }

    // ----- QUIT CONFIRM -----
    public bool LastQuitConfirmResult { get; private set; } = false;

    public IEnumerator ShowQuitConfirm(string prompt = "Quit the session? (Y/N)",
                                       KeyCode yesKey = KeyCode.Y,
                                       KeyCode noKey = KeyCode.N)
    {
        if (quitText) quitText.text = prompt;
        if (pauseDuringOverlay) Time.timeScale = 0f;
        if (quitPanel) quitPanel.SetActive(true);

        bool? decided = null;
        while (!decided.HasValue)
        {
            if (Input.GetKeyDown(yesKey)) decided = true;
            else if (Input.GetKeyDown(noKey)) decided = false;
            yield return null;
        }

        LastQuitConfirmResult = decided.Value;

        if (quitPanel) quitPanel.SetActive(false);
        if (pauseDuringOverlay && !LastQuitConfirmResult) Time.timeScale = 1f;
    }

    // ----- ESCAPE COUNTDOWN (NEW) -----
    public IEnumerator ShowEscapeCountdown(int seconds = 3,
                                           string title = "Congratulations!",
                                           string countdownTemplate = "Respawning in {0}…")
    {
        if (!escapePanel || !escapeCountdown)
        {
            // Fallback: use instructions panel if escape panel not wired
            yield return ShowInstructions(title, string.Format(countdownTemplate, seconds));
            yield break;
        }

        if (escapeTitle) escapeTitle.text = title;
        if (pauseDuringOverlay) Time.timeScale = 0f;
        escapePanel.SetActive(true);

        int t = Mathf.Max(1, seconds);
        while (t > 0)
        {
            escapeCountdown.text = string.Format(countdownTemplate, t);
            float end = Time.unscaledTime + 1f;
            while (Time.unscaledTime < end) yield return null;
            t--;
        }

        escapePanel.SetActive(false);
        if (pauseDuringOverlay) Time.timeScale = 1f;
    }

    // ----- RATINGS SEQUENCE (NEW) -----
    /// <summary>
    /// Shows a sequence of VAS prompts (uses one VAS panel repeatedly).
    /// Each item is (metricName, prompt). Logs are emitted by VASInput on submit.
    /// </summary>
    public IEnumerator ShowRatingsSequence(ContextId ctx, int cycle, (string metric, string prompt)[] items)
    {
        if (vas == null || items == null || items.Length == 0) yield break;

        DataLogger.Instance?.LogEvent("RATINGS_BEGIN", "ctx", ctx.ToString(), "cycle", cycle.ToString());

        for (int i = 0; i < items.Length; i++)
        {
            bool done = false;
            float valueCaptured = 0f;

            System.Action<float> handler = (v) => { valueCaptured = v; done = true; };

            // Configure + subscribe + show
            vas.Configure(items[i].metric, items[i].prompt);
            vas.OnSubmitted += handler;
            vas.gameObject.SetActive(true); // triggers OnEnable pause

            // Wait for submit
            while (!done) yield return null;

            vas.OnSubmitted -= handler;

            DataLogger.Instance?.LogEvent(
         "RATING_CAPTURED",
         "metric", items[i].metric,
         "value", valueCaptured.ToString("F3"),
         $"ctx={ctx};cycle={cycle}"
     );
        }

        DataLogger.Instance?.LogEvent("RATINGS_END", "ctx", ctx.ToString(), "cycle", cycle.ToString());
    }

    public IEnumerator ShowExitCountdown(int seconds = 3,
                                     string title = "Exiting context",
                                     string countdownTemplate = "Exiting in {0}…")
    {
        // Reuse our escape UI if you wired it; otherwise fall back to instructions.
        if (!escapePanel || !escapeCountdown)
        {
            yield return ShowInstructions(title, string.Format(countdownTemplate, seconds));
            yield break;
        }

        if (escapeTitle) escapeTitle.text = title;
        if (pauseDuringOverlay) Time.timeScale = 0f;
        escapePanel.SetActive(true);

        int t = Mathf.Max(1, seconds);
        while (t > 0)
        {
            escapeCountdown.text = string.Format(countdownTemplate, t);
            float end = Time.unscaledTime + 1f;
            while (Time.unscaledTime < end) yield return null;
            t--;
        }

        escapePanel.SetActive(false);
        if (pauseDuringOverlay) Time.timeScale = 1f;
    }

}
