using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

[DisallowMultipleComponent]
public class NetworkLauncher : MonoBehaviour
{
    [Header("Domyœlne (u¿ywane gdy brak argumentów)")]
    [SerializeField] private string defaultServerIp = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;

    private UnityTransport transport;
    private NetworkManager networkManager;

    void Awake()
    {
        transport = FindFirstObjectByType<UnityTransport>();
        networkManager = FindFirstObjectByType<NetworkManager>();

        if (transport == null || networkManager == null)
        {
            Debug.LogError("NetworkLauncher: brakuje UnityTransport lub NetworkManager w scenie.");
            return;
        }

        ApplyCommandLineConfiguration();
    }

    private void ApplyCommandLineConfiguration()
    {
        var args = Environment.GetCommandLineArgs();

        // Try read explicit -role (server/client) or fallback to flags -server / -client
        bool runAsServer = ArgExists(args, "-server") || ArgEquals(args, "-role", "server");
        bool runAsClient = ArgExists(args, "-client") || ArgEquals(args, "-role", "client");

        // If neither specified, domyœlnie uruchom klienta (zmieñ jeœli chcesz inny zachowanie)
        if (!runAsServer && !runAsClient)
            runAsClient = true;

        string ip = GetArgValue(args, "-ip") ?? GetArgValue(args, "--ip") ?? defaultServerIp;
        string portArg = GetArgValue(args, "-port") ?? GetArgValue(args, "--port");
        ushort port = defaultPort;
        if (portArg != null && ushort.TryParse(portArg, out var p)) port = p;

        if (runAsServer)
        {
            // Nas³uch na wszystkich interfejsach — pewny sposób by inne komputery w LAN mog³y siê po³¹czyæ
            transport.SetConnectionData(ip, port, "0.0.0.0");
            if (!networkManager.IsListening)
            {
                Debug.Log($"NetworkLauncher: uruchamiam serwer. Address={ip} Port={port} Listen=0.0.0.0");
                networkManager.StartServer();
            }
        }
        else // klient
        {
            transport.SetConnectionData(ip, port);
            if (!networkManager.IsClient)
            {
                Debug.Log($"NetworkLauncher: uruchamiam klienta. Connect to {ip}:{port}");
                networkManager.StartClient();
            }
        }
    }

    // Helpery
    private static bool ArgExists(string[] args, string key)
    {
        foreach (var a in args) if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool ArgEquals(string[] args, string key, string expectedValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(args[i + 1], expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}