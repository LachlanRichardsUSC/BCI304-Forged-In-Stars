using UnityEngine;

/// <summary>
/// Dash component that is attached to the Player object.
/// Configurable parameters are stored in MovementSettings.cs
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(GroundDetector))]
[RequireComponent(typeof(ResourceManager))]
[RequireComponent(typeof(MovementSettings))]
public class PlayerDash : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField]
    [Tooltip("VFX prefab to spawn when dashing")]
    private GameObject dashVFXPrefab;

    [SerializeField]
    [Tooltip("Duration before destroying the VFX")]
    private float vfxDuration = 1f;

    [SerializeField]
    [Tooltip("Transform where VFX should spawn (leave null to use player position)")]
    private Transform vfxSpawnTransform;

    // Runtime state
    private int currentDashes;
    private float dashResetTimer;
    private bool dashesInCooldown = false;

    // Component references
    private InputSystem_Actions m_Actions;
    private CharacterController m_Controller;
    private GroundDetector m_GroundDetector;
    private ResourceManager m_ResourceManager;
    private MovementSettings m_Settings;
    private Transform m_CameraTransform;

    // Dash state
    private bool m_IsDashing;
    private float m_DashTimeRemaining;
    private Vector3 m_DashDirection;

    // Events
    public event System.Action OnDashInitiated;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        m_Controller = GetComponent<CharacterController>();
        m_GroundDetector = GetComponent<GroundDetector>();
        m_ResourceManager = GetComponent<ResourceManager>();
        m_Settings = GetComponent<MovementSettings>();
        m_Actions = new InputSystem_Actions();

        if (!ValidateComponents()) enabled = false;
    }

    private bool ValidateComponents()
    {
        if (m_Controller == null || m_GroundDetector == null ||
            m_ResourceManager == null || m_Settings == null)
        {
            Debug.LogError($"[{GetType().Name}] Required components missing!");
            return false;
        }
        return true;
    }

    private void Start()
    {
        currentDashes = m_Settings.Dash.maxDashes;
        dashResetTimer = 0f;

        m_CameraTransform = Camera.main?.transform;
        if (m_CameraTransform == null)
        {
            Debug.LogError($"[{GetType().Name}] Main camera not found!");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        m_Actions?.Player.Enable();
    }

    private void OnDisable()
    {
        m_Actions?.Player.Disable();
    }

    private void Update()
    {
        if (!enabled) return;

        m_GroundDetector.CheckGround();
        HandleDash();
        HandleDashCooldown();
    }

    private void HandleDash()
    {
        if (m_IsDashing)
        {
            m_Controller.Move(m_DashDirection * m_Settings.Dash.dashForce * Time.deltaTime);
            m_DashTimeRemaining -= Time.deltaTime;

            if (m_DashTimeRemaining <= 0f)
            {
                m_IsDashing = false;
            }
            return;
        }

        // Check conditions in order: input > dashes available > stamina
        // This prevents stamina consumption when dashes aren't available
        if (m_Actions.Player.Dash.WasPerformedThisFrame() &&
            currentDashes > 0 &&
            m_ResourceManager.TryUseStamina(m_Settings.Dash.dashStaminaCost))
        {
            InitiateDash();
        }
    }

    private void HandleDashCooldown()
    {
        if (dashesInCooldown)
        {
            dashResetTimer -= Time.deltaTime;
            if (dashResetTimer <= 0f)
            {
                currentDashes = m_Settings.Dash.maxDashes;
                dashesInCooldown = false;
                Debug.Log("Dashes reset! Available dashes: " + currentDashes);
            }
        }
    }

    private void InitiateDash()
    {
        m_IsDashing = true;
        m_DashTimeRemaining = m_Settings.Dash.dashDuration;

        // Spawn VFX if prefab is assigned
        if (dashVFXPrefab != null)
        {
            Vector3 spawnPosition = vfxSpawnTransform != null ? vfxSpawnTransform.position : transform.position;
            Quaternion spawnRotation = vfxSpawnTransform != null ? vfxSpawnTransform.rotation : transform.rotation;

            GameObject vfx = Instantiate(dashVFXPrefab, spawnPosition, spawnRotation);
            Destroy(vfx, vfxDuration);
        }

        // Decrement available dashes
        currentDashes--;
        Debug.Log("Dashed! Dashes left: " + currentDashes);

        // Start cooldown when all dashes are used
        if (currentDashes <= 0)
        {
            dashesInCooldown = true;
            dashResetTimer = m_Settings.Dash.dashCooldown;
        }

        // Get current movement input
        Vector2 input = m_Actions.Player.Move.ReadValue<Vector2>();

        // Handle dash direction based on input
        if (input.magnitude > 0.1f)  // If there's movement input, dash in that direction
        {
            float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + m_CameraTransform.eulerAngles.y;
            m_DashDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }
        else  // If no movement input, use camera or character forward
        {
            m_DashDirection = m_Settings.Dash.useCameraForward ?
                m_CameraTransform.forward : transform.forward;
        }

        // Keep dash movement horizontal and normalized
        m_DashDirection.y = 0;
        m_DashDirection.Normalize();
        OnDashInitiated?.Invoke();
    }

    private void OnDestroy()
    {
        m_Actions?.Dispose();
    }

    // Public properties for external access
    public bool IsDashing => m_IsDashing;
    public int CurrentDashes => currentDashes;
    public int MaxDashes => m_Settings.Dash.maxDashes;
    public bool InCooldown => dashesInCooldown;
    public float CooldownTimeRemaining => dashResetTimer;
    public float TotalCooldownTime => m_Settings.Dash.dashCooldown;
}