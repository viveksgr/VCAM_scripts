using System.Collections;
using UnityEngine;

public class GlobalQuitWatcher : MonoBehaviour
{
    public SessionUI ui;
    public KeyCode quitHotkey = KeyCode.Escape;
    public string prompt = "Quit the session? (Y/N)";
    private bool running;

    void Reset() { if (!ui) ui = FindObjectOfType<SessionUI>(); }

    void Update()
    {
        if (running || !ui) return;
        if (Input.GetKeyDown(quitHotkey))
            StartCoroutine(ConfirmAndQuit());
    }

    IEnumerator ConfirmAndQuit()
    {
        running = true;
        yield return ui.ShowQuitConfirm(prompt);
        if (ui.LastQuitConfirmResult)
        {
            // YES → quit app (or stop Play mode in Editor)
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        running = false;
    }
}
