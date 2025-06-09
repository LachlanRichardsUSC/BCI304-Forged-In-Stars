using UnityEngine;
using UnityEngine.Events;

public class ResourceManager : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float m_MaxHealth = 100f;
    [SerializeField] private float m_HealthRegenRate = 0f;
    [SerializeField] private float m_HealthRegenDelay = 5f;

    [Header("Stamina Settings")]
    [SerializeField] private float m_MaxStamina = 100f;
    [SerializeField] private float m_StaminaRegenRate = 10f;
    [SerializeField] private float m_GroundedRegenMultiplier = 2f;
    [SerializeField] private float m_StaminaRegenDelay = 1f;

    [SerializeField] private GroundDetector m_GroundDetector;

    private float m_CurrentHealth;
    private float m_CurrentStamina;
    private float m_LastStaminaUseTime;
    private float m_LastHealthLossTime;
    private bool m_IsRegenerating = true;

    // Events
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<float> OnHealthDepleted;
    public UnityEvent<float> OnStaminaChanged;
    public UnityEvent<float> OnStaminaDepleted;


    private void Awake()
    {
        m_GroundDetector = GetComponent<GroundDetector>();
        InitializeResources();
    }

    private void InitializeResources()
    {
        m_CurrentHealth = m_MaxHealth;
        m_CurrentStamina = m_MaxStamina;

        // Trigger initial UI updates
        OnHealthChanged?.Invoke(GetHealthPercentage());
        OnStaminaChanged?.Invoke(GetStaminaPercentage());
    }

    private void Update()
    {
        if (m_IsRegenerating)
        {
            HandleStaminaRegeneration();
            HandleHealthRegeneration();
        }
    }

    private void HandleStaminaRegeneration()
    {
        if (Time.time - m_LastStaminaUseTime < m_StaminaRegenDelay)
            return;

        float currentRegenRate = m_StaminaRegenRate;

        // Apply multiplier if grounded
        if (m_GroundDetector.IsGrounded)
        {
            currentRegenRate *= m_GroundedRegenMultiplier;
        }

        m_CurrentStamina = Mathf.Min(m_CurrentStamina + (currentRegenRate * Time.deltaTime), m_MaxStamina);
        OnStaminaChanged?.Invoke(GetStaminaPercentage());
    }

    private void HandleHealthRegeneration()
    {
        if (m_HealthRegenRate <= 0 || Time.time - m_LastHealthLossTime < m_HealthRegenDelay)
            return;

        m_CurrentHealth = Mathf.Min(m_CurrentHealth + (m_HealthRegenRate * Time.deltaTime), m_MaxHealth);
        OnHealthChanged?.Invoke(GetHealthPercentage());
    }

    public bool TryUseStamina(float cost)
    {
        if (!HasEnoughStamina(cost))
            return false;

        m_CurrentStamina -= cost;
        m_LastStaminaUseTime = Time.time;

        OnStaminaChanged?.Invoke(GetStaminaPercentage());

        if (m_CurrentStamina <= 0)
            OnStaminaDepleted?.Invoke(0f);

        return true;
    }

    public void TakeDamage(float damage)
    {
        m_CurrentHealth = Mathf.Max(0, m_CurrentHealth - damage);
        m_LastHealthLossTime = Time.time;

        OnHealthChanged?.Invoke(GetHealthPercentage());

        if (m_CurrentHealth <= 0)
            OnHealthDepleted?.Invoke(0f);
    }


    public void Heal(float amount)
    {
        m_CurrentHealth = Mathf.Min(m_CurrentHealth + amount, m_MaxHealth);
        OnHealthChanged?.Invoke(GetHealthPercentage());
    }

    public bool HasEnoughStamina(float cost) => m_CurrentStamina >= cost;
    public float GetHealthPercentage() => m_CurrentHealth / m_MaxHealth;
    public float GetStaminaPercentage() => m_CurrentStamina / m_MaxStamina;
    public void SetRegenerating(bool state) => m_IsRegenerating = state;

    public void ResetHealth()
    {
        m_CurrentHealth = m_MaxHealth;
        OnHealthChanged?.Invoke(GetHealthPercentage());
    }
}


