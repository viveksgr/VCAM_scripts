using UnityEngine;
using System.Collections;

public class QSTSequence : MonoBehaviour
{
    public QSTController qst;   // drag your QSTController object here in the Inspector

    private void Start()
    {
        // Kick off your coroutine automatically when Play starts
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        if (qst == null) qst = FindObjectOfType<QSTController>();

        // Example sequence: baseline → stimulus → back to baseline
        qst.SetBaseTemperature(32.0f);
        yield return new WaitForSeconds(1f);

        qst.SetTargetTemperature(46.0f);
        qst.SetDuration(3000); // 3 seconds
        qst.StartStimulation();
        yield return new WaitForSeconds(4f); // wait for it to finish

        qst.SetTargetTemperature(32.0f);
        qst.SetDuration(2000);
        qst.StartStimulation();

        Debug.Log("Sequence complete!");
    }
}
