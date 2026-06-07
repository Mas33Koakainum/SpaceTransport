using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine;

public class ConnectButton : MonoBehaviour
{
    public TMP_InputField ipInput;

    public void Connect()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport.ConnectionData.Address == null)
        { ipInput.text = "0.0.0.0"; }
        transport.ConnectionData.Address = ipInput.text;


        NetworkManager.Singleton.StartClient();
    }
}