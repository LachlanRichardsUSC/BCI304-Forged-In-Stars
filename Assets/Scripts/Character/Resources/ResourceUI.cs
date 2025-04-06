using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ResourceUI : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private Slider m_HealthSlider;
    [SerializeField] private TextMeshProUGUI m_HealthText;

    [Header("Stamina UI")]
    [SerializeField] private Slider m_StaminaSlider;
    [SerializeField] private TextMeshProUGUI m_StaminaText;

    [Header("References")]
    [SerializeField] private ResourceManager m_ResourceManager;

    private void OnEnable()
    {
        if (m_ResourceManager == null)
        {
            Debug.LogError($"[{GetType().Name}] ResourceManager reference is missing!");
            enabled = false;
            return;
        }

        SubscribeToEvents();
        UpdateUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        m_ResourceManager.OnHealthChanged.AddListener(UpdateHealthUI);
        m_ResourceManager.OnStaminaChanged.AddListener(UpdateStaminaUI);
    }

    private void UnsubscribeFromEvents()
    {
        m_ResourceManager.OnHealthChanged.RemoveListener(UpdateHealthUI);
        m_ResourceManager.OnStaminaChanged.RemoveListener(UpdateStaminaUI);
    }

    private void UpdateUI()
    {
        UpdateHealthUI(m_ResourceManager.GetHealthPercentage());
        UpdateStaminaUI(m_ResourceManager.GetStaminaPercentage());
    }

    private void UpdateHealthUI(float percentage)
    {
        if (m_HealthSlider != null)
            m_HealthSlider.value = percentage;

        if (m_HealthText != null)
            m_HealthText.text = $"{(percentage * 100):F0}%";
    }

    private void UpdateStaminaUI(float percentage)
    {
        if (m_StaminaSlider != null)
            m_StaminaSlider.value = percentage;

        if (m_StaminaText != null)
            m_StaminaText.text = $"{(percentage * 100):F0}%";
    }
}
