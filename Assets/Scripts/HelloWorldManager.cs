using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HelloWorld
{
    public class HelloWorldManager : MonoBehaviour
    {
        [Header("Assign in Inspector (Canvas UI)")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;

        [Header("Network Settings")]
        [SerializeField] private TMP_InputField ipInputField;
        [SerializeField] private TMP_InputField portInputField;

        void OnEnable()
        {
            if (hostButton == null || clientButton == null || serverButton == null)
            {
                Debug.LogError("HelloWorldManager: przypisz pola UI w Inspektorze.");
                return;
            }

            hostButton.onClick.AddListener(OnHostButtonClicked);
            clientButton.onClick.AddListener(OnClientButtonClicked);
            serverButton.onClick.AddListener(OnServerButtonClicked);

            var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (transport != null)
            {
                if (ipInputField != null) ipInputField.text = transport.ConnectionData.Address;
                if (portInputField != null) portInputField.text = transport.ConnectionData.Port.ToString();
            }
        }

        void OnDisable()
        {
            if (hostButton != null) hostButton.onClick.RemoveListener(OnHostButtonClicked);
            if (clientButton != null) clientButton.onClick.RemoveListener(OnClientButtonClicked);
            if (serverButton != null) serverButton.onClick.RemoveListener(OnServerButtonClicked);
        }

        void Update()
        {
            UpdateUI();
        }

        void ApplyConnectionData()
        {
            var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (transport == null) return;

            string ip = (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
                ? ipInputField.text
                : transport.ConnectionData.Address;

            ushort port = transport.ConnectionData.Port;
            if (portInputField != null && ushort.TryParse(portInputField.text, out ushort parsedPort))
                port = parsedPort;

            transport.SetConnectionData(ip, port);
        }

        void OnHostButtonClicked()
        {
            ApplyConnectionData();
            NetworkManager.Singleton?.StartHost();
        }

        void OnClientButtonClicked()
        {
#if UNITY_EDITOR
            var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("127.0.0.1", 7777);
#else
            ApplyConnectionData();
#endif
            NetworkManager.Singleton?.StartClient();
        }

        void OnServerButtonClicked()
        {
            ApplyConnectionData();
            NetworkManager.Singleton?.StartServer();
        }

        void UpdateUI()
        {
            if (NetworkManager.Singleton == null)
            {
                SetStartButtons(false);
                return;
            }

            bool notConnected = !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;
            SetStartButtons(notConnected);

            if (ipInputField != null) ipInputField.gameObject.SetActive(notConnected);
            if (portInputField != null) portInputField.gameObject.SetActive(notConnected);
        }

        void SetStartButtons(bool state)
        {
            if (hostButton != null) hostButton.gameObject.SetActive(state);
            if (clientButton != null) clientButton.gameObject.SetActive(state);
            if (serverButton != null) serverButton.gameObject.SetActive(state);
        }
    }
}