using UnityEngine;
using System.IO.Ports;

public class ListComPorts : MonoBehaviour
{
    void Start()
    {
        var ports = SerialPort.GetPortNames();
        Debug.Log("[QST] Available ports: " + string.Join(", ", ports));
    }
}
