using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public float Health, MaxHealth, Width, Height;

    [SerializeField]
    private RectTransform HealthStatus;

    public void SetMaxHealth(float maxHealth)
    {
        MaxHealth = maxHealth;
    }

    public void SetHealth(float healthStatus)
    {
        Health = healthStatus;
        float newWidth = (Health / MaxHealth) * Width;

        HealthStatus.sizeDelta = new Vector2(newWidth, Height);
    }
}