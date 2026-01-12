using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public static class BHAPI_Discovery
{
    [DllImport("mpdev", EntryPoint = "findAllMP160", CallingConvention = CallingConvention.Cdecl)]
    public static extern int findAllMP160();

    [DllImport("mpdev", EntryPoint = "readAvailableMP160SN", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int readAvailableMP160SN(byte[] buffer, int bufferLen);
}

public class MP160Discovery : MonoBehaviour
{
    [Tooltip("How many characters of response buffer to allocate. 256 is usually enough.")]
    public int bufferSize = 256;

    private void Start()
    {
        Debug.Log("[BHAPI] Searching for MP160 units...");

        int rc = BHAPI_Discovery.findAllMP160();
        if (rc != 0)
        {
            Debug.LogError($"[BHAPI] findAllMP160 returned error code {rc}");
            return;
        }

        byte[] buf = new byte[bufferSize];
        int rc2 = BHAPI_Discovery.readAvailableMP160SN(buf, buf.Length);
        if (rc2 != 0)
        {
            Debug.LogError($"[BHAPI] readAvailableMP160SN returned error code {rc2}");
            return;
        }

        string result = Encoding.ASCII.GetString(buf).Trim('\0', '\r', '\n');
        if (string.IsNullOrEmpty(result))
        {
            Debug.LogWarning("[BHAPI] No MP160 units found on the network.");
        }
        else
        {
            Debug.Log($"[BHAPI] Discovered MP160: {result}");
            Debug.Log("👉 Use this in connectMPDev string, e.g., MP160://<IP-address>");
        }
    }
}
