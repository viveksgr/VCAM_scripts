// QSTController.cs
// Unity-friendly, thread-safe serial driver for QST thermode devices.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class QSTController : MonoBehaviour
{
    [Header("Serial Settings")]
    [Tooltip("Windows COM port name, e.g., COM5")]
    public string comPort = "COM5";
    public int baudRate = 115200;
    public int readTimeoutMs = 5000;

    [Tooltip("Set true if your device requires DTR asserted")]
    public bool dtrEnable = true;

    [Tooltip("Set true if your device requires RTS asserted")]
    public bool rtsEnable = false;

    [Header("Auto-init")]
    public bool openOnStart = true;

    // Status/diagnostic callbacks (optional)
    public event Action<string> OnInfo;
    public event Action<string> OnError;

    // Event fired once the port is opened
    public event Action OnOpened;

    // Public state
    public bool IsOpen => _sp != null && _sp.IsOpen;

    // --- internals ---
    private SerialPort _sp;
    private Thread _worker;
    private readonly ConcurrentQueue<Action<SerialPort>> _queue = new ConcurrentQueue<Action<SerialPort>>();
    private readonly AutoResetEvent _signal = new AutoResetEvent(false);
    private volatile bool _running;

    // ---------------- Unity lifecycle ----------------

    private void Awake()
    {
        if (openOnStart)
            TryOpen();
    }

    private void OnDestroy()
    {
        TryClose();
    }

    // ---------------- Public API ----------------

    public void TryOpen()
    {
        if (_running && IsOpen) return;

        try
        {
            _sp = new SerialPort(comPort, baudRate)
            {
                ReadTimeout = readTimeoutMs,
                DtrEnable = dtrEnable,
                RtsEnable = rtsEnable,
                NewLine = "\r\n"
            };

            _sp.Open();
            _running = true;

            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "QST-Serial-Worker" };
            _worker.Start();

            // Silence device chatter (per protocol) after opening
            Enqueue(sp => sp.Write("F"));

            Info($"Opened {_sp.PortName} @ {baudRate}.");
            try { OnOpened?.Invoke(); } catch { /* ignore */ }
        }
        catch (Exception e)
        {
            Error($"Failed to open {comPort}: {e.Message}");
            SafeDisposePort();
        }
    }

    public void TryClose()
    {
        if (!_running)
        {
            SafeDisposePort();
            return;
        }

        _running = false;
        try { _signal.Set(); } catch { /* ignore */ }

        try
        {
            if (_worker != null && _worker.IsAlive)
            {
                if (!_worker.Join(1000))
                    _worker.Interrupt();
            }
        }
        catch { /* ignore */ }

        SafeDisposePort();
        Info("Serial closed.");
    }

    /// <summary>Coroutine you can yield on before sending commands.</summary>
    public System.Collections.IEnumerator WaitUntilOpen()
    {
        while (!IsOpen) yield return null;
    }

    // ---------------- QST commands ----------------

    /// <summary>Set base temperature (°C). Valid: 20.0 – 45.0 (one decimal).</summary>
    public void SetBaseTemperature(float tCelsius)
    {
        int tenths = (int)Math.Round(tCelsius * 10.0f, MidpointRounding.AwayFromZero);
        if (tenths < 200 || tenths > 450)
            throw new PainlabProtocolException("QST base temperature limits are 20.0 – 45.0 °C.");

        string cmd = "N" + tenths.ToString("000", CultureInfo.InvariantCulture);
        if (Enqueue(sp => sp.Write(cmd)))
            Info($"Base T set: {tCelsius:F1}°C -> {cmd}");
    }

    /// <summary>Set target temperature (°C). Valid: 0.0 – 60.0 (one decimal). surfaceIndex: 0=all, 1–5 single.</summary>
    public void SetTargetTemperature(float tCelsius, int surfaceIndex = 0)
    {
        int tenths = (int)Math.Round(tCelsius * 10.0f, MidpointRounding.AwayFromZero);
        if (tenths < 0 || tenths > 600)
            throw new PainlabProtocolException("QST target temperature limits are 0.0 – 60.0 °C.");
        if (surfaceIndex < 0 || surfaceIndex > 5)
            throw new PainlabProtocolException("Surface index must be 0 (all) or 1–5.");

        string cmd = "C" + surfaceIndex.ToString(CultureInfo.InvariantCulture) +
                     tenths.ToString("000", CultureInfo.InvariantCulture);
        if (Enqueue(sp => sp.Write(cmd)))
            Info($"Target T set: {tCelsius:F1}°C (surface {surfaceIndex}) -> {cmd}");
    }

    /// <summary>Set stimulation duration (ms). Valid: 10 – 99,999. surfaceIndex: 0=all, 1–5 single.</summary>
    public void SetDuration(int durationMs, int surfaceIndex = 0)
    {
        if (durationMs < 10 || durationMs > 99999)
            throw new PainlabProtocolException("QST duration must be 10 – 99,999 ms.");
        if (surfaceIndex < 0 || surfaceIndex > 5)
            throw new PainlabProtocolException("Surface index must be 0 (all) or 1–5.");

        string cmd = "D" + surfaceIndex.ToString(CultureInfo.InvariantCulture) +
                     durationMs.ToString("00000", CultureInfo.InvariantCulture);
        if (Enqueue(sp => sp.Write(cmd)))
            Info($"Duration set: {durationMs} ms (surface {surfaceIndex}) -> {cmd}");
    }

    /// <summary>Trigger stimulation.</summary>
    public void StartStimulation()
    {
        if (Enqueue(sp => sp.Write("L")))
            Info("Stimulation started (L).");
    }

    /// <summary>Send a raw command string (advanced).</summary>
    public void SendRaw(string command)
    {
        if (string.IsNullOrEmpty(command)) return;
        if (Enqueue(sp => sp.Write(command)))
            Info($"Raw -> {command}");
    }

    // ---------------- Worker & plumbing ----------------

    private bool Enqueue(Action<SerialPort> action)
    {
        if (!_running || _sp == null || !_sp.IsOpen)
        {
            Error("Serial not open; command ignored.");
            return false;
        }
        _queue.Enqueue(action);
        _signal.Set();
        return true;
    }

    private void WorkerLoop()
    {
        try
        {
            while (_running)
            {
                if (_queue.TryDequeue(out var act))
                {
                    try
                    {
                        act(_sp);
                    }
                    catch (Exception ex)
                    {
                        Error("Serial write failed: " + ex.Message);
                    }
                    continue;
                }
                _signal.WaitOne(50);
            }
        }
        catch (ThreadInterruptedException) { /* exiting */ }
        catch (Exception e)
        {
            Error("Worker crashed: " + e.Message);
        }
    }

    private void SafeDisposePort()
    {
        try { if (_sp != null && _sp.IsOpen) _sp.Close(); } catch { /* ignore */ }
        try { _sp?.Dispose(); } catch { /* ignore */ }
        _sp = null;
    }

    private void Info(string msg)
    {
        Debug.Log("[QST] " + msg);
        try { OnInfo?.Invoke(msg); } catch { /* ignore */ }
    }

    private void Error(string msg)
    {
        Debug.LogError("[QST] " + msg);
        try { OnError?.Invoke(msg); } catch { /* ignore */ }
    }
}

// ---------------- Exception type ----------------

public class PainlabProtocolException : Exception
{
    public PainlabProtocolException() { }
    public PainlabProtocolException(string message) : base(message) { }
    public PainlabProtocolException(string message, Exception inner) : base(message, inner) { }
}
