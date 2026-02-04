using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class YokeDurationsPayload
{
    public string ctx;
    public string mode;
    public double[] durations; // seconds since train start
}


public class QSTSpikeTrain : MonoBehaviour
{
    [Header("Refs")]
    public QSTController qst;

    [Header("Temps (°C)")]
    public float baselineC = 32f;
    public float startTempC = 39f;
    public float endTempC = 49f;
    public float stepC = 1f;

    [Header("Per-train settings")]
    public int maxSpikes = 10;            // set 11 for 39..49 inclusive
    public int startSurface = 1;          // cycles 1→5 per spike
    public bool forceBaselineBetweenSpikes = true;

    [Header("Biopac settings")]
    public LPTTrigger lpt;                // drag your LPTTrigger here in Inspector
    public bool sendTrainStartPulse = true;

    [Header("Timing (ms)")]
    public int spikeDurationMs = 2000;    // dwell at target
    public int isiMs = 2000;              // cool-down between spikes
    public int baselinePulseMs = 300;     // brief enforced return to baseline

    [Header("Repeat trains")]
    public bool repeatTrains = true;      // keep restarting after a gap
    public float interTrainGapSec = 20f;  // gap between trains
    public int maxTrains = 0;             // 0 = infinite; otherwise limit

    [Header("Emergency stop (per-train)")]
    public string stopCode = "258";       // default; override via SetStopCode

    [Header("Yoke playback (NoControl)")]
    public bool yokePlayback = false;     // set true after calling LoadYokeFiles(...)
    [Tooltip("Template or pattern, e.g. '{subject}_{session}_{maze}_Y*_yoke.json'")]
    public string yokePattern = "{subject}_{session}_{maze}_Y*_yoke.json";
    [Tooltip("If set, load abort times from this durations JSON (your computed file).")]
    public string yokeDurationsJsonPath = "";
    [Tooltip("If true, yoke playback uses the durations JSON instead of *_yoke.json payloads.")]
    public bool yokeUseDurationsJson = false;
    public System.Action<int, bool> OnTrainFinished; // (trainIndex, aborted)


    // --- internal state ---
    private string typed = "";
    private bool abortThisTrain = false;         // resets each train
    private bool inTrain = false;                // Update() listens only during a train
    private Coroutine _runner;
    private string _runtimeStopCode = null;

    private float _trainStartTime;               // Time.time when a train starts
    private int _trainIndex;                     // 0,1,2,...

    // Yoke playback queue
    private readonly List<float> _yokeQueue = new List<float>(); // per-train offsets (sec), -1 = no abort
    private int _yokePtr = 0;

    private string EffectiveStopCode => string.IsNullOrEmpty(_runtimeStopCode) ? stopCode : _runtimeStopCode;

    // ---------- Public control from SessionDirector ----------
    public void SetStopCode(string code)
    {
        _runtimeStopCode = code ?? "";
        Debug.Log($"[QST] Stop code set to '{EffectiveStopCode}'");
    }

    /// <summary>Clear any loaded yoke offsets (start a fresh NoControl block).</summary>
    public void ClearYokePlayback()
    {
        _yokeQueue.Clear();
        _yokePtr = 0;
        yokePlayback = false;
    }

    public bool LoadYokeDurationsJson(string filePathTemplateOrAbsolute = null)
    {
        try
        {
            string p = string.IsNullOrEmpty(filePathTemplateOrAbsolute) ? yokeDurationsJsonPath : filePathTemplateOrAbsolute;
            if (string.IsNullOrEmpty(p))
            {
                Debug.LogWarning("[QST] LoadYokeDurationsJson: empty path.");
                return false;
            }

            // Reuse your existing token/path resolver (so {subject},{session},{maze} still work)
            string full = ResolveConcretePath(p);

            if (!File.Exists(full))
            {
                Debug.LogWarning($"[QST] Durations JSON not found: {full}");
                return false;
            }

            var json = File.ReadAllText(full);
            var payload = JsonUtility.FromJson<YokeDurationsPayload>(json);

            if (payload == null || payload.durations == null || payload.durations.Length == 0)
            {
                Debug.LogWarning($"[QST] Durations JSON invalid/empty: {full}");
                return false;
            }

            _yokeQueue.Clear();
            _yokePtr = 0;

            foreach (var d in payload.durations)
            {
                // Treat <=0 as “no abort” sentinel
                float sec = (float)d;
                _yokeQueue.Add(sec > 0f ? sec : -1f);
            }

            yokePlayback = true;
            yokeUseDurationsJson = true;

            Debug.Log($"[QST] Durations yoke loaded: {Path.GetFileName(full)} (trains={_yokeQueue.Count}) ctx={payload.ctx} mode={payload.mode}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[QST] LoadYokeDurationsJson failed: " + e.Message);
            return false;
        }
    }


    /// <summary>
    /// Load one or more yoke files. Supports patterns and tokens {subject},{session},{maze}.
    /// Appends all found files (sorted by write time ascending) to the playback queue.
    /// Returns true if any offsets were loaded.
    /// </summary>
    public bool LoadYokeFiles(string fileTemplateOrPattern = null)
    {
        if (yokeUseDurationsJson)
        {
            Debug.Log("[QST] LoadYokeFiles ignored because yokeUseDurationsJson=true.");
            return (_yokeQueue.Count > 0);
        }

        try
        {
            string resolved = ResolveConcretePath(string.IsNullOrEmpty(fileTemplateOrPattern) ? yokePattern : fileTemplateOrPattern);
            string dir = Path.GetDirectoryName(resolved);
            string name = Path.GetFileName(resolved);
            if (string.IsNullOrEmpty(dir)) dir = DataLogger.OutputFolder;
            if (string.IsNullOrEmpty(name)) name = "*_yoke.json";

            string[] files;
            if (name.Contains("*"))
            {
                files = Directory.GetFiles(dir, name, SearchOption.TopDirectoryOnly);
                System.Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b))); // oldest→newest
            }
            else
            {
                string full = Path.Combine(dir, name);
                if (!File.Exists(full))
                {
                    Debug.LogWarning($"[QST] Yoke file not found: {full}");
                    return false;
                }
                files = new[] { full };
            }

            int appended = 0;
            foreach (var f in files)
            {
                var json = File.ReadAllText(f);
                var payload = JsonUtility.FromJson<DataLogger.YokePayload>(json);
                if (payload?.abortOffsetsSec == null) continue;

                foreach (var maybe in payload.abortOffsetsSec)
                    _yokeQueue.Add(maybe.HasValue ? (maybe.Value >= 0f ? maybe.Value : -1f) : -1f);

                appended++;
                Debug.Log($"[QST] Yoke loaded: {Path.GetFileName(f)} (trains += {payload.abortOffsetsSec.Length})");
            }

            yokePlayback = (_yokeQueue.Count > 0);
            Debug.Log($"[QST] Yoke queue total trains = {_yokeQueue.Count} (from {appended} file(s)).");
            return yokePlayback;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[QST] LoadYokeFiles failed: " + e.Message);
            return false;
        }
    }

    public void ForceStart()
    {
        if (!enabled) enabled = true;
        if (_runner == null)
        {
            Debug.Log("[QST] ForceStart(): starting spike-train loop.");
            _runner = StartCoroutine(Run());
        }
    }

    // ---------- Unity lifecycle ----------
    void OnEnable()
    {
        if (qst == null) qst = FindObjectOfType<QSTController>();
        qst?.TryOpen();
        if (_runner == null) _runner = StartCoroutine(Run());
    }

    void OnDisable()
    {
        abortThisTrain = true;
        inTrain = false;
        if (_runner != null)
        {
            StopCoroutine(_runner);
            _runner = null;
        }
    }

    // ---------- Main loop ----------
    IEnumerator Run()
    {
        if (qst != null)
        {
            qst.TryOpen();
            yield return qst.WaitUntilOpen();
        }

        _trainIndex = 0;
        int trainCount = 0;

        while (true)
        {
            if (maxTrains > 0 && trainCount >= maxTrains) break;

            // (re)start a train
            abortThisTrain = false;
            typed = "";
            inTrain = true;
            _trainStartTime = Time.time;

            if (sendTrainStartPulse && lpt != null) lpt.SendPulse();
            DataLogger.Instance?.TrainStart(trainCount);

            if (qst != null) qst.SetBaseTemperature(baselineC);

            // Planned abort from yoke (one per train)
            float plannedAbortSec = -1f;
            if (yokePlayback && _yokePtr < _yokeQueue.Count)
                plannedAbortSec = _yokeQueue[_yokePtr];

            int sent = 0;
            float t = startTempC;

            while (!abortThisTrain && sent < maxSpikes && t <= endTempC + 1e-3f)
            {
                // honor planned auto-abort
                if (plannedAbortSec >= 0f && (Time.time - _trainStartTime) >= plannedAbortSec)
                {
                    abortThisTrain = true;
                    Debug.Log($"[QST] Yoke playback: auto-abort at {plannedAbortSec:0.000}s (train {_trainIndex}).");
                    break;
                }
                int surf = 0; // 1..5 cycling
                // int surf = 1 + ((startSurface - 1 + sent) % 5); // 1..5 cycling
                Debug.Log($"[QST] Train {trainCount + 1} | Spike {sent + 1}: {t:F1}°C on surface {surf}");
                DataLogger.Instance?.SpikeStart(trainCount, sent, surf, t, spikeDurationMs);

                if (qst != null)
                {
                    qst.SetTargetTemperature(t, surf);
                    qst.SetDuration(spikeDurationMs, surf);
                    qst.StartStimulation();
                }
                yield return WaitSecondsWithAbortOrYoke(spikeDurationMs / 1000f, plannedAbortSec);

                if (!abortThisTrain && forceBaselineBetweenSpikes && qst != null)
                {
                    qst.SetTargetTemperature(baselineC, surf);
                    qst.SetDuration(Mathf.Max(50, baselinePulseMs), surf);
                    qst.StartStimulation();
                    yield return WaitSecondsWithAbortOrYoke(baselinePulseMs / 1000f, plannedAbortSec);
                }

                DataLogger.Instance?.SpikeEnd(trainCount, sent, surf);

                if (!abortThisTrain)
                    yield return WaitSecondsWithAbortOrYoke(isiMs / 1000f, plannedAbortSec);

                sent++;
                t += stepC;
            }

            inTrain = false;

            Debug.Log($"[QST] Train {trainCount + 1} complete. AbortedThisTrain={abortThisTrain}");
            DataLogger.Instance?.TrainEnd(trainCount, aborted: abortThisTrain);
            OnTrainFinished?.Invoke(trainCount, abortThisTrain);


            _trainIndex++;
            if (yokePlayback) _yokePtr++; // consume one yoke entry per train
            trainCount++;

            if (!repeatTrains) break;
            if (interTrainGapSec > 0f) yield return new WaitForSeconds(interTrainGapSec);
        }

        Debug.Log($"[QST] All done. Trains run: {trainCount}");
        _runner = null; // allow ForceStart later
    }

    // Wait helper that also watches yoked auto-abort time
    IEnumerator WaitSecondsWithAbortOrYoke(float seconds, float plannedAbortSec)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            if (abortThisTrain) yield break;

            if (yokePlayback && plannedAbortSec >= 0f &&
                (Time.time - _trainStartTime) >= plannedAbortSec)
            {
                abortThisTrain = true;
                yield break;
            }
            yield return null;
        }
    }

    void Update()
    {
        if (!inTrain || abortThisTrain) return;

        foreach (char c in Input.inputString)
        {
            if (char.IsDigit(c))
            {
                typed += c;
                var code = EffectiveStopCode;
                if (!string.IsNullOrEmpty(code))
                {
                    if (typed.Length > code.Length)
                        typed = typed.Substring(typed.Length - code.Length);

                    if (typed == code)
                    {
                        abortThisTrain = true; // only affects current train
                        DataLogger.Instance?.LogButton($"STOP_CODE_{code}", true);
                        Debug.Log($"[QST] STOP code '{code}' received. Ending this train.");
                    }
                }
            }
        }
    }

    // ---------- Path helper ----------
    // Expand tokens and anchor to DataLogger.OutputFolder (or keep absolute)
    string ResolveConcretePath(string templateOrPattern)
    {
        string template = templateOrPattern;
        if (string.IsNullOrEmpty(template))
            template = "{subject}_{session}_{maze}_Y*_yoke.json"; // default: all rounds for this subject/session/maze

        string subject = DataLogger.SubjectId;
        string session = (DataLogger.Instance ? DataLogger.Instance.sessionId : "X");
        string maze = (DataLogger.Instance ? DataLogger.Instance.mazeId : "Maze");

        string name = template
            .Replace("{subject}", string.IsNullOrEmpty(subject) ? "unknown" : subject)
            .Replace("{session}", string.IsNullOrEmpty(session) ? "X" : session)
            .Replace("{maze}", string.IsNullOrEmpty(maze) ? "Maze" : maze);

        if (Path.IsPathRooted(name)) return name;
        return Path.Combine(DataLogger.OutputFolder, name);
    }
}
