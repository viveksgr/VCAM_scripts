using UnityEngine;
using System.IO.Ports;

public class SerialPortProbe : MonoBehaviour
{
    public string portName = "COM7"; // put your thermode COM here
    public int baud = 115200;

    void Start()
    {
        try
        {
            Debug.Log($"[PROBE] Trying {portName} @ {baud}...");
            var sp = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = false,
                NewLine = "\r\n"
            };
            sp.Open();
            Debug.Log($"[PROBE] OPEN OK: {sp.PortName}. IsOpen={sp.IsOpen}");
            sp.Write("F");  // harmless for your device
            sp.Close();
            Debug.Log("[PROBE] CLOSED.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PROBE] OPEN FAILED for {portName}: {e.GetType().Name}: {e.Message}");
        }
    }
}
