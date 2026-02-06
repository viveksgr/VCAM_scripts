using UnityEngine;
using System.Collections;

public class LPTTrigger : MonoBehaviour
{
    [Header("Parallel Port")]
    [Tooltip("LPT base I/O address (hex). Common: 0x378 or 0x278")]
    public int baseAddressHex = 0x378;

    public bool forceOutputModeOnStart = true;

    [Header("Mode")]
    public bool sendByteCode = true;   // true = 8-bit event codes; false = single-bit
    [Tooltip("When sendByteCode=true: 0..255 event value. When false: which bit (0..7).")]
    public int valueOrBit = 0;         // event code or bit index

    [Header("Pulse")]
    [Tooltip("Pulse width in milliseconds")]
    public int pulseMs = 5;
    [Tooltip("Auto-reset to 0 after pulse")]
    public bool autoReset = true;

    short DataReg => (short)baseAddressHex;       // Data at base
    short StatusReg => (short)(baseAddressHex + 1); // read-only
    short ControlReg => (short)(baseAddressHex + 2);// (not used here)

    void Start()
    {
        if (!forceOutputModeOnStart) return;

        // Clear bit 5 (0x20) on the Control register to force data pins to OUTPUT.
        short ctrl = LPT.Inp32(ControlReg);
        short newCtrl = (short)(ctrl & ~0x20);
        LPT.Out32(ControlReg, newCtrl);

        Debug.Log($"[LPT] ControlReg @ 0x{ControlReg:X} was {(ctrl & 0xFF)} now {(newCtrl & 0xFF)} (forced OUTPUT)");
    }


    public void SendPulse()
    {
        if (sendByteCode)
        {
            byte code = (byte)Mathf.Clamp(valueOrBit, 0, 255);
            StartCoroutine(PulseByte(code));
        }
        else
        {
            int bit = Mathf.Clamp(valueOrBit, 0, 7);
            byte mask = (byte)(1 << bit);
            StartCoroutine(PulseByte(mask));
        }
    }

    IEnumerator PulseByte(byte code)
    {
        // Write code on data lines
        DataLogger.Instance?.LogLptPulse(code);  // <— add
        LPT.Out32(DataReg, code);

        short rb = LPT.Inp32(DataReg);
        Debug.Log($"[LPT] Wrote {code} readback {(rb & 0xFF)} @ 0x{DataReg:X}");

        yield return new WaitForSecondsRealtime(pulseMs / 1000f);

        if (autoReset) LPT.Out32(DataReg, 0);
    }

    // Optional: quick readback (often just echoes last written value on some cards)
    public byte ReadData()
    {
        short v = LPT.Inp32(DataReg);
        return (byte)(v & 0xFF);
        // Or: return LPT.DlPortReadPortUchar((ushort)DataReg);
    }

    // Demo: Space sends a pulse
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) SendPulse();
    }
}
