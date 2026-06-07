using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostIPDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI ipAddressText;
    [SerializeField] private Button showIPButton;

    private string ipAddress;

    private void Start()
    {
        // Ukryj tekst na pocz¹tku
        ipAddressText.text = "";

        // Podepnij funkcjê pod przycisk
        showIPButton.onClick.AddListener(OnShowIPButtonClicked);
    }

    private void OnDestroy()
    {
        showIPButton.onClick.RemoveListener(OnShowIPButtonClicked);
    }

    private void OnShowIPButtonClicked()
    {
        try
        {
            GetLocalIPAddress();
        }
        catch (System.Exception e)
        {
            ipAddressText.text = "Brak po³¹czenia sieciowego!";
            Debug.LogError($"B³¹d pobierania IP: {e.Message}");
        }
    }

    public string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                ipAddressText.text = $"IP Hosta: {ip}";
                ipAddress = ip.ToString();
                return ip.ToString();
            }
        }
        throw new System.Exception("No network adapters with an IPv4 address in the system!");
    }
}