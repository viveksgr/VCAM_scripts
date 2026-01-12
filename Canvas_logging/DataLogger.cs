using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class DataLogger : MonoBehaviour
{
    public static DataLogger Instance { get; private set; }

    // -------- Subject / run IDs --------
    [Header("Subject/Run")]
    public string subjectId = "S001";        // set per participant in Inspector
    public static string SubjectId => Instance ? Instance.subjectId : "unknown";

    public string sessionId = "A";
    public string mazeId = "Maze1";

    // -------- Files & cadence --------
    [Header("Files & cadence")]
    public int targetHz = 60;                 // 0 = every frame
    public string baseFolder = "";            // empty = Application.persistentDataPath
    public bool flushEveryWrite = false;      // safer; can disable for speed

    public static string OutputFolder
    {
        get
        {
            if (Instance == null) return Application.persistentDataPath;
            var f = string.IsNullOrEmpty(Instance.baseFolder)
                ? Application.persistentDataPath
                : Instance.baseFolder;
            return f;
        }
    }

    // -------- Live state (mirrored by other scripts) --------
    [NonSerialized] public bool thermodeActive;
    [NonSerialized] public float thermodeTempC;
    [NonSerialized] public int thermodeSurface; // 0=all,1..5
    [NonSerialized] public int lastLptCode;     // byte 0..255, or last bit mask
    [NonSerialized] public string lastButton;     // free text (e.g., "STOP_CODE_258")

    [NonSerialized] public int lever1State; // +1 on, -1 off, 0 unknown
    [NonSerialized] public int lever2State;
    [NonSerialized] public int lever3State;

    // -------- Yoke accumulation (per CONTROL round) --------
    [Header("Yoke control (managed by SessionDirector)")]
    public bool yokeActive = false;      // true only during ThermodeControl rounds
    public int yokeRoundIndex = 0;      // increment each time you start a new control round

    private readonly List<float?> yokeAbortOffsetsSec = new List<float?>();
    private float currentTrainStartUnscaled = -1f;

    // -------- Writers & timing --------
    private StreamWriter frameW;
    private StreamWriter eventsW;

    private float nextSampleTime;

    // -------- Helpers --------
    string Stamp => DateTime.UtcNow.ToString("o"); // ISO8601 UTC
    string PrefixFileName => $"{subjectId}_{sessionId}_{mazeId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var folder = OutputFolder;
        Directory.CreateDirectory(folder);

        var framePath = Path.Combine(folder, PrefixFileName + "_frames.csv");
        var eventsPath = Path.Combine(folder, PrefixFileName + "_events.csv");

        frameW = new StreamWriter(framePath, false, Encoding.UTF8);
        eventsW = new StreamWriter(eventsPath, false, Encoding.UTF8);

        // headers
        frameW.WriteLine("stamp_utc,time,unscaledTime,pos_x,pos_y,pos_z,head_yaw,head_pitch,head_roll,therm_active,therm_tempC,therm_surface,last_lpt,last_button,lever1,lever2,lever3");
        eventsW.WriteLine("stamp_utc,time,unscaledTime,type,k,v1,v2,notes");
        Flush();

        nextSampleTime = Time.unscaledTime;
        Debug.Log($"[LOG] Writing to:\n  {framePath}\n  {eventsPath}");
    }

    void OnApplicationQuit() => Finish();

    public void Finish()
    {
        // If a yoke block is still open, close & save it
        if (yokeActive)
        {
            Debug.LogWarning("[LOG] Finish(): yoke block was still active — saving it now.");
            EndYokeBlockAndSave();
        }

        if (frameW != null) { frameW.Flush(); frameW.Close(); frameW = null; }
        if (eventsW != null) { eventsW.Flush(); eventsW.Close(); eventsW = null; }
    }

    void Flush()
    {
        if (flushEveryWrite) { frameW?.Flush(); eventsW?.Flush(); }
    }

    // ---------------- frame sampling (call from DataSampler) ----------------
    public void LogFrame(Transform subject, Transform head)
    {
        if (frameW == null) return;

        // cadence
        if (targetHz > 0)
        {
            if (Time.unscaledTime < nextSampleTime) return;
            nextSampleTime += 1f / targetHz;
        }

        var p = subject ? subject.position : Vector3.zero;
        var e = head ? head.eulerAngles : Vector3.zero;

        frameW.WriteLine($"{Stamp},{Time.time:F6},{Time.unscaledTime:F6},{p.x:F4},{p.y:F4},{p.z:F4},{e.y:F2},{e.x:F2},{e.z:F2},{(thermodeActive ? 1 : 0)},{thermodeTempC:F1},{thermodeSurface},{lastLptCode},{lastButton},{lever1State},{lever2State},{lever3State}");
        Flush();

        // clear one-shot button tag so it doesn’t repeat every frame
        lastButton = "";
    }

    // ---------------- events ----------------
    public void LogEvent(string type, string k = "", string v1 = "", string v2 = "", string notes = "", string v = null)
    {
        if (eventsW == null) return;
        eventsW.WriteLine($"{Stamp},{Time.time:F6},{Time.unscaledTime:F6},{type},{k},{v1},{v2},{notes}");
        Flush();
    }

    public void LogLptPulse(int code)
    {
        lastLptCode = code;
        LogEvent("LPT", "code", code.ToString());
    }

    public void LogButton(string name, bool success)
    {
        lastButton = name;
        LogEvent("BUTTON", name, success ? "1" : "0");
    }

    // ---------------- train/spike hooks (called by QSTSpikeTrain) ----------------
    public void TrainStart(int trainIndex)
    {
        currentTrainStartUnscaled = Time.unscaledTime;
        LogEvent("TRAIN_START", "idx", trainIndex.ToString());
    }

    public void TrainEnd(int trainIndex, bool aborted)
    {
        float? abortOffset = null;
        if (aborted && currentTrainStartUnscaled >= 0f)
            abortOffset = Time.unscaledTime - currentTrainStartUnscaled;

        // Only record yoke offsets if a yoke block is active (ThermodeControl round)
        if (yokeActive) yokeAbortOffsetsSec.Add(abortOffset);

        LogEvent("TRAIN_END", "idx", trainIndex.ToString(), "aborted", aborted ? "1" : "0");
        currentTrainStartUnscaled = -1f;
    }

    public void SpikeStart(int trainIdx, int spikeIdx, int surface, float tempC, int durMs)
    {
        thermodeActive = true; thermodeTempC = tempC; thermodeSurface = surface;
        LogEvent("SPIKE_START", "t,s", $"{trainIdx},{spikeIdx}", "surf", surface.ToString(), $"temp={tempC:F1},durMs={durMs}");
    }

    public void SpikeEnd(int trainIdx, int spikeIdx, int surface)
    {
        thermodeActive = false;
        LogEvent("SPIKE_END", "t,s", $"{trainIdx},{spikeIdx}", "surf", surface.ToString());
    }

    public void LogLever(int idx, int state)
    {
        // state: +1 = ON, -1 = OFF
        switch (idx)
        {
            case 1: lever1State = state; break;
            case 2: lever2State = state; break;
            case 3: lever3State = state; break;
        }
        LogEvent("LEVER", "idx", idx.ToString(), "state", state.ToString());
    }

    // ---------------- YOKE API (call from SessionDirector) ----------------

    /// <summary>
    /// Start recording a new yoke block (one ThermodeControl round).
    /// </summary>
    public void BeginYokeBlock(int roundIndex)
    {
        yokeActive = true;
        yokeRoundIndex = roundIndex;
        yokeAbortOffsetsSec.Clear();
        LogEvent("YOKE_BEGIN", "round", roundIndex.ToString());
        Debug.Log($"[LOG] YOKE_BEGIN round={roundIndex}");
    }

    /// <summary>
    /// Stop recording and write the yoke JSON file. Returns full path (or null on error).
    /// </summary>
    public string EndYokeBlockAndSave()
    {
        yokeActive = false;

        try
        {
            var folder = OutputFolder;
            Directory.CreateDirectory(folder);

            string fileName = $"{subjectId}_{sessionId}_{mazeId}_Y{yokeRoundIndex:00}_yoke.json";
            string fullPath = Path.Combine(folder, fileName);

            var payload = new YokePayload { abortOffsetsSec = yokeAbortOffsetsSec.ToArray() };
            var json = JsonUtility.ToJson(payload, true);
            File.WriteAllText(fullPath, json, Encoding.UTF8);

            LogEvent("YOKE_SAVED", "round", yokeRoundIndex.ToString(), "", fileName);
            Debug.Log($"[LOG] Yoke file: {fullPath}");
            return fullPath;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LOG] Yoke write failed: " + e.Message);
            return null;
        }
    }

    // Shape of the file QSTSpikeTrain will read
    [Serializable]
    public class YokePayload { public float?[] abortOffsetsSec; }
}
