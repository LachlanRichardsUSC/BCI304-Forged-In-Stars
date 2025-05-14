using UnityEngine;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Settings")]
    public int maxDashes = 4;                // Max dashes before cooldown
    public float dashCooldown = 5.5f;        // Time before all dashes reset

    private int currentDashes;
    private float dashResetTimer;
    private bool dashesInCooldown = false;

    private MovementSettings m_Settings;
    private InputSystem_Actions m_Actions;
    private CharacterController m_Controller;
    private GroundDetector m_GroundDetector;
    private ResourceManager m_ResourceManager;
    public Transform m_CameraTransform;

    private bool m_IsDashing;
    private float m_DashTimeRemaining;
    private Vector3 m_DashDirection;

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
    }

    void Start()
    {
        currentDashes = maxDashes;
        dashResetTimer = 0f;
    }

    private void OnEnable()
    {
        m_Actions?.Player.Enable();
    }

    private void Update()
    {
        if (!enabled) return;

        m_GroundDetector.CheckGround();
        HandleDash();

        // Handle dash cooldown
        if (dashesInCooldown)
        {
            dashResetTimer -= Time.deltaTime;

            if (dashResetTimer <= 0f)
            {
                currentDashes = maxDashes;
                dashesInCooldown = false;
                Debug.Log("Dashes reset! Available dashes: " + currentDashes);
            }
        }
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

        // Check if we can dash - requires: player input, stamina available, and dashes available
        if (m_Actions.Player.Dash.WasPerformedThisFrame() &&
            m_ResourceManager.TryUseStamina(m_Settings.Dash.dashStaminaCost) &&
            currentDashes > 0)
        {
            InitiateDash();
        }
    }

    private void InitiateDash()
    {
        m_IsDashing = true;
        m_DashTimeRemaining = m_Settings.Dash.dashDuration;

        // Decrement available dashes
        currentDashes--;
        Debug.Log("Dashed! Dashes left: " + currentDashes);

        // Start cooldown when all dashes are used
        if (currentDashes <= 0)
        {
            dashesInCooldown = true;
            dashResetTimer = dashCooldown;
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
}