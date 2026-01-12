// SessionDirectorAdditive.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionDirectorAdditive : MonoBehaviour
{
    [Header("Context scene names (Build Settings)")]
    public string contextAScene = "Context_A_Scene";
    public string contextBScene = "Context_B_Scene";
    public string contextCScene = "Context_C_Scene";

    [Header("MainRoom references")]
    public Transform player;          // root with CharacterController
    public QSTSpikeTrain spikeTrain;  // thermode train controller
    public LPTTrigger lpt;            // optional sync pulse

    [Header("Schedule")]
    public int blockSeconds = 300;    // 5 min per context
    public int cycles = 4;            // A,B,C repeated this many times

    [Header("Assignments (A/B/C -> condition)")]
    public ContextAssignment[] assignments =
    {
        new ContextAssignment{ context = ContextId.A, condition = ThermodeCondition.NoThermode },
        new ContextAssignment{ context = ContextId.B, condition = ThermodeCondition.ThermodeControl },
        new ContextAssignment{ context = ContextId.C, condition = ThermodeCondition.ThermodeNoControl },
    };

    [Header("UI (in MainRoom)")]
    public SessionUI ui;                          // assign your SessionUI on the Canvas
    public float preLoadHoldSeconds = 1.5f;       // "please wait" linger
    public KeyCode instructionAdvanceKey = KeyCode.Space;
    public ContextInstruction[] blockInstructions = new ContextInstruction[]
    {
        new ContextInstruction{ id=ContextId.A, title="Context A", body="Follow the cues. Press Space to begin."},
        new ContextInstruction{ id=ContextId.B, title="Context B", body="You may control aspects here. Press Space to begin."},
        new ContextInstruction{ id=ContextId.C, title="Context C", body="Observe and proceed. Press Space to begin."},
    };

    [Header("Break screen (after each full cycle)")]
    public string breakTitle = "Break";
    [TextArea] public string breakBody = "You can rest now.\nPress Space to continue.";


    [Header("Spawn options")]
    public bool alignToSpawnRotation = false;   // keep false if you don't want yaw alignment

    [Header("Safety / Debug")]
    public float escapeDebounceSec = 0.75f;     // ignore rapid duplicate hits
    public bool logVerbose = true;

    [Header("Ratings after each block")]
    [TextArea] public string ratingPainPrompt = "Rate your pain during this block.\n(Right-click to submit)";
    [TextArea] public string ratingLikingPrompt = "How much did you like this context?";
    [TextArea] public string ratingDifficultyPrompt = "How difficult was this context?";

    [Header("Yoke (NoControl playback)")]
    [Tooltip("Durations JSON used to yoke ThermodeNoControl. Can be absolute or use tokens like {subject},{session},{maze}.")]
    public string yokeDurationsJsonPath = "";

    // --- internal state ---
    private readonly Dictionary<ContextId, Scene> scenes = new();
    private readonly Dictionary<ContextId, ContextDescriptor> descs = new();
    private bool pendingEscape;
    private bool respawnInProgress;
    private float lastEscapeTime = -999f;

    // Called by trigger inside contexts
    public void NotifyEscape()
    {
        if (Time.unscaledTime - lastEscapeTime < escapeDebounceSec) return;
        lastEscapeTime = Time.unscaledTime;
        if (logVerbose) Debug.Log("[Director] NotifyEscape() received.");
        pendingEscape = true;
    }

    private IEnumerator Start()
    {
        // Ensure player reference
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // UI: please wait while loading
        if (ui) ui.ShowPleaseWaitInstant("Loading contexts… please wait");

        // Load contexts additively
        yield return LoadContext(ContextId.A, contextAScene);
        yield return LoadContext(ContextId.B, contextBScene);
        yield return LoadContext(ContextId.C, contextCScene);

        // Hide all contexts initially
        SetSceneActive(ContextId.A, false);
        SetSceneActive(ContextId.B, false);
        SetSceneActive(ContextId.C, false);

        // UI: brief hold then hide
        if (ui) yield return ui.HidePleaseWaitAfter(preLoadHoldSeconds);

        // Log assignments
        foreach (var a in assignments)
            DataLogger.Instance?.LogEvent("ASSIGNMENT", a.context.ToString(), a.condition.ToString());

        // Run schedule
        var order = new[] { ContextId.A, ContextId.B, ContextId.C };
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            foreach (var id in order)
                yield return RunBlock(id, cycle);


            // Show Break unless it was the last cycle
            if (cycle < cycles - 1 && ui)
                yield return ui.ShowInstructions(breakTitle, breakBody, instructionAdvanceKey);
        }

        DataLogger.Instance?.LogEvent("SESSION_END");

        // UI: final thank you
        // Persistent thank-you (stays up)
        if (ui) ui.ShowThankYouPersistent("Thank you! This session is complete.");

        if (logVerbose) Debug.Log("[Director] Session complete.");
    }

    // -------------------- Core loop per block --------------------
    private IEnumerator RunBlock(ContextId id, int cycle)
    {
        if (!descs.TryGetValue(id, out var d) || d == null)
        {
            Debug.LogError($"[Director] Missing ContextDescriptor for {id}.");
            yield break;
        }

        // Show active only
        ActivateOnly(id);

        // UI: pre-block instructions (press Space to continue)
        if (ui)
        {
            var ins = GetInstructions(id);
            yield return ui.ShowInstructions(ins.title, ins.body, instructionAdvanceKey);
        }

        // Start-of-block setup
        yield return RespawnRoutine(d, "BLOCK_START");

        // Thermode mode
        var cond = GetCondition(id);
        ApplyThermodeCondition(cond);

        // Sync + log
        lpt?.SendPulse();
        DataLogger.Instance?.LogEvent("CONTEXT_BLOCK_START", "ctx", id.ToString(), "cycle", cycle.ToString(), cond.ToString());
        if (logVerbose) Debug.Log($"[Director] Block start: {id} (cycle {cycle}) mode={cond}");

        // Timer loop
        float t0 = Time.unscaledTime;
        pendingEscape = false;

        while (Time.unscaledTime - t0 < blockSeconds)
        {
            if (pendingEscape && !respawnInProgress)
            {
                pendingEscape = false;
                DataLogger.Instance?.LogEvent("ESCAPE", "ctx", id.ToString(), "cycle", cycle.ToString());

                // NEW: pause + countdown overlay before we actually respawn
                if (ui) yield return ui.ShowEscapeCountdown(3, "Congratulations!", "Respawning in {0}…");

                if (logVerbose) Debug.Log("[Director] ESCAPE: Respawning…");
                yield return RespawnRoutine(d, "ESCAPE");
                DataLogger.Instance?.LogEvent("RESPAWN", "ctx", id.ToString(), "cycle", cycle.ToString());
            }
            yield return null;
        }

        // Block time is up -> close any CONTROL yoke round now
        EndCurrentRound();

        // Block time is up → show a brief exit countdown, then ratings.
        if (ui) yield return ui.ShowExitCountdown(3, "Exiting context", "Exiting in {0}…");

        // Ratings (3 VAS in sequence), then block end
        if (ui)
        {
            var items = new (string metric, string prompt)[]
            {
        ("pain",       ratingPainPrompt),
        ("liking",     ratingLikingPrompt),
        ("difficulty", ratingDifficultyPrompt),
            };
            yield return ui.ShowRatingsSequence(id, cycle, items);
        }

        // Block end
        DataLogger.Instance?.LogEvent("CONTEXT_BLOCK_END", "ctx", id.ToString(), "cycle", cycle.ToString());
        if (logVerbose) Debug.Log($"[Director] Block end: {id} (cycle {cycle}).");
    }

    // -------------------- Respawn (random spawn + lever refresh) --------------------
    private IEnumerator RespawnRoutine(ContextDescriptor d, string why)
    {
        respawnInProgress = true;

        TeleportToRandomStart(d);
        RefreshLevers(d);

        // Close door visually if present (door itself doesn’t trigger escape)
        if (d.door) d.door.open = false;

        if (logVerbose) Debug.Log($"[Director] Respawn done ({why}).");
        yield return null; // allow one frame to settle

        respawnInProgress = false;
    }

    // -------------------- Helpers --------------------
    private IEnumerator LoadContext(ContextId id, string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[Director] Missing scene name for {id}.");
            yield break;
        }

        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            if (logVerbose) Debug.Log($"[Director] Loading scene '{sceneName}' additively…");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return op;
        }

        var scn = SceneManager.GetSceneByName(sceneName);
        scenes[id] = scn;

        // Find ContextDescriptor inside that scene
        ContextDescriptor found = null;
        foreach (var go in scn.GetRootGameObjects())
        {
            found = go.GetComponentInChildren<ContextDescriptor>(true);
            if (found) break;
        }
        if (!found)
        {
            Debug.LogError($"[Director] No ContextDescriptor found in scene '{sceneName}'.");
        }
        else
        {
            descs[id] = found;
            if (logVerbose) Debug.Log($"[Director] Linked descriptor for {id} from '{sceneName}'.");
        }
    }

    private void ActivateOnly(ContextId id)
    {
        SetSceneActive(id, true);
        foreach (var other in new[] { ContextId.A, ContextId.B, ContextId.C })
            if (other != id) SetSceneActive(other, false);
    }

    private void SetSceneActive(ContextId id, bool on)
    {
        if (!scenes.TryGetValue(id, out var scn)) return;
        foreach (var go in scn.GetRootGameObjects()) go.SetActive(on);
    }

    private void TeleportToRandomStart(ContextDescriptor d)
    {
        if (!player || d.startPoints == null || d.startPoints.Count == 0)
        {
            Debug.LogWarning("[Director] Teleport skipped: missing player or start points.");
            return;
        }

        var sp = d.startPoints[Random.Range(0, d.startPoints.Count)];
        SafeTeleport(player, sp, alignToSpawnRotation);

        int idx = d.startPoints.IndexOf(sp);
        DataLogger.Instance?.LogEvent("SPAWN_PICK", "idx", idx.ToString());
        if (logVerbose) Debug.Log($"[Director] Teleport → start #{idx} at {sp.position}");
    }

    private void SafeTeleport(Transform target, Transform spawn, bool alignRot)
    {
        if (!target || !spawn) return;

        var cc = target.GetComponent<CharacterController>();
        var pm = target.GetComponent<PlayerMovement>();

        Vector3 pos = spawn.position;
        Quaternion rot = alignRot ? spawn.rotation : target.rotation;

        if (cc) cc.enabled = false;

        target.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();

        if (pm) pm.ResetVerticalVelocity();   // <-- direct call

        if (cc) cc.enabled = true;
    }


    private void RefreshLevers(ContextDescriptor d)
    {
        if (!d)
        {
            Debug.LogWarning("[Director] RefreshLevers skipped: no descriptor.");
            return;
        }

        if (d.placer != null)
        {
            var indices = d.placer.PlaceRandomAndReset();
            if (logVerbose) Debug.Log($"[Director] LeverPlacer moved levers to [{indices[0]}, {indices[1]}, {indices[2]}].");
            return;
        }

        if (d.spawner != null)
        {
            d.spawner.ResetState();
            var arr = d.spawner.SpawnThree();
            if (logVerbose) Debug.Log($"[Director] LeverSpawner created levers at [{arr[0]}, {arr[1]}, {arr[2]}].");
            return;
        }

        Debug.Log("[Director] No LeverPlacer/Spawner set on descriptor — lever layout unchanged.");
    }

    private ThermodeCondition GetCondition(ContextId id)
    {
        foreach (var a in assignments) if (a.context == id) return a.condition;
        return ThermodeCondition.NoThermode;
    }

    // In SessionDirector (or wherever you switch conditions)

    [SerializeField] private int roundIndex = 0;  // increment each time you start a new Control round
    [SerializeField] private string donorSubjectId = ""; // optional: if yoking between subjects

    private ThermodeCondition _activeCond = ThermodeCondition.NoThermode;

    private void ApplyThermodeCondition(ThermodeCondition cond)
    {
        if (!spikeTrain) return;

        switch (cond)
        {
            case ThermodeCondition.NoThermode:
                spikeTrain.enabled = false;
                DataLogger.Instance?.LogEvent("THERMODE_MODE", "OFF");
                break;

            case ThermodeCondition.ThermodeControl:
                spikeTrain.enabled = true;
                spikeTrain.yokeUseDurationsJson = false;   // important: record / free control
                DataLogger.Instance?.LogEvent("THERMODE_MODE", "CONTROL");
                break;

            case ThermodeCondition.ThermodeNoControl:
                spikeTrain.enabled = true;

                // 🔹 ADD THESE LINES
                spikeTrain.yokeUseDurationsJson = true;
                spikeTrain.LoadYokeDurationsJson(yokeDurationsJsonPath);

                DataLogger.Instance?.LogEvent("THERMODE_MODE", "NO_CONTROL");
                break;
        }

        if (logVerbose)
            Debug.Log($"[Director] Thermode condition = {cond}, spikeTrain.enabled={spikeTrain.enabled}");
    }


    // Closes out a CONTROL round (if active), saves the yoke file, and bumps the round index.
    private void EndCurrentRound()
    {
        if (_activeCond == ThermodeCondition.ThermodeControl)
        {
            var saved = DataLogger.Instance?.EndYokeBlockAndSave();
            if (!string.IsNullOrEmpty(saved))
                Debug.Log($"[Director] Yoke saved: {saved}");

            roundIndex++; // next CONTROL round will be Y01, Y02, ...
        }
    }

    private ContextInstruction GetInstructions(ContextId id)
    {
        foreach (var ci in blockInstructions) if (ci.id == id) return ci;
        return new ContextInstruction { id = id, title = id.ToString(), body = "Press Space to begin." };
    }
}
