using Unity.Netcode;
using UnityEngine;

public class DisconnectManager : MonoBehaviour
{
    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
    }
}