using System.Collections;
using TMPro;                 // <- base class TMP_Text covers both UGUI & 3D
using UnityEngine;

public class DoorFeedbackUI : MonoBehaviour
{
    [Header("Assign a TMP text (UGUI or 3D). If left empty, will auto-find in children.")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private float defaultSeconds = 3f;

    private Coroutine _co;

    void Awake()
    {
        EnsureReady();             // auto-find & hide label
    }

    public void EnsureReady()
    {
        if (!label)
            label = GetComponentInChildren<TMP_Text>(true); // look in children too

        if (!label)
        {
            Debug.LogWarning($"[DoorFeedbackUI] No TMP_Text found under '{name}'. Assign a label in the Inspector.");
            return;
        }

        // Keep this component's GO active; hide only the text GO
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (label.gameObject.activeSelf) label.gameObject.SetActive(false);
    }

    public void ShowUnlocked(float seconds = -1f, string text = "Door unlocked")
    {
        if (!label)
        {
            label = GetComponentInChildren<TMP_Text>(true);
            if (!label)
            {
                Debug.LogWarning($"[DoorFeedbackUI] ShowUnlocked called but no TMP_Text found on '{name}'.");
                return;
            }
        }

        // make sure we're able to run coroutines & display
        if (!enabled) enabled = true;
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Run(text, seconds < 0 ? defaultSeconds : seconds));
    }

    private IEnumerator Run(string msg, float seconds)
    {
        label.text = msg;

        // Harden visibility a bit
        var c = label.color; c.a = 1f; label.color = c;

        float end = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        while (Time.unscaledTime < end) yield return null;

        if (label) label.gameObject.SetActive(false);
        _co = null;
    }
}
