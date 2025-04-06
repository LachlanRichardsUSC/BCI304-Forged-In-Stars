using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all character movement including basic locomotion, jumping, jump jets, and dashing.
/// Requires MovementSettings component for configuration.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(GroundDetector))]
[RequireComponent(typeof(ResourceManager))]
[RequireComponent(typeof(MovementSettings))]
public class CharacterMovementController : MonoBehaviour
{
    // Component references
    private CharacterController m_Controller;
    private GroundDetector m_GroundDetector;
    private ResourceManager m_ResourceManager;
    private MovementSettings m_Settings;
    private Transform m_CameraTransform;
    private InputSystem_Actions m_Actions;

    // Movement state
    private Vector3 m_Movement;
    private float m_VerticalVelocity;
    private float m_JumpBufferCounter;
    private float m_FallTime;
    private float m_LastJumpTime;


    // Dash state
    private bool m_IsDashing;
    private float m_DashTimeRemaining;
    private float m_DashCooldownRemaining;
    private Vector3 m_DashDirection;

    // Jump jet state
    private bool m_IsUsingJumpJets;
    private float m_JumpJetDuration;
    private float m_JumpJetDelayTimer = 0f;  // Add this as a class field
    private const float k_JumpJetDelay = 0.25f;  // Add this as a class constant

    // Events
    public event System.Action OnJumpInitiated;
    public event System.Action OnDashInitiated;
    public event System.Action<float> OnMoveSpeedChanged;
    public event System.Action OnJumpJetStarted;
    public event System.Action OnJumpJetEnded;



    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Get required components
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

        if (!m_IsDashing)
        {
            HandleMovement();
            HandleJumpAndGravity();
            HandleJumpJets();
        }

        HandleDash();
        UpdateCooldowns();
    }

    private void HandleMovement()
    {
        Vector2 input = m_Actions.Player.Move.ReadValue<Vector2>();

        if (input == Vector2.zero)
        {
            m_Movement = Vector3.zero;
            OnMoveSpeedChanged?.Invoke(0f);
            return;
        }

        float currentSpeed = GetCurrentMovementSpeed();

        // Regular movement relative to camera
        float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + m_CameraTransform.eulerAngles.y;
        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // Rotate character with movement
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.Euler(0f, targetAngle, 0f),
            Time.deltaTime * m_Settings.BasicMovement.turnSpeed
        );

        m_Movement = moveDirection * currentSpeed;
        m_Controller.Move(m_Movement * Time.deltaTime);
        OnMoveSpeedChanged?.Invoke(m_Movement.magnitude / m_Settings.BasicMovement.runSpeed);
    }

    private float GetCurrentMovementSpeed()
    {
        bool isSprinting = m_Actions.Player.Sprint.IsPressed() &&
                          m_ResourceManager.HasEnoughStamina(m_Settings.BasicMovement.sprintStaminaCost * Time.deltaTime);

        if (isSprinting)
        {
            m_ResourceManager.TryUseStamina(m_Settings.BasicMovement.sprintStaminaCost * Time.deltaTime);
            return m_Settings.BasicMovement.runSpeed;
        }

        return m_Settings.BasicMovement.walkSpeed;
    }

    private void HandleJumpAndGravity()
    {
        // Jump input buffering
        if (m_Actions.Player.Jump.WasPerformedThisFrame())
        {
            m_JumpBufferCounter = m_Settings.Jump.bufferDuration;
        }
        else if (m_JumpBufferCounter > 0)
        {
            m_JumpBufferCounter -= Time.deltaTime;
        }
        // Handle jump if conditions are met
        if (m_GroundDetector.IsGrounded && m_JumpBufferCounter > 0 &&
            Time.time - m_LastJumpTime >= m_Settings.Jump.jumpCooldown)
        {
            m_VerticalVelocity = m_Settings.Jump.jumpForce;
            m_LastJumpTime = Time.time;
            OnJumpInitiated?.Invoke();
            m_JumpBufferCounter = 0f;
        }
        // Apply gravity with fall multiplier
        if (!m_GroundDetector.IsGrounded && !m_IsUsingJumpJets)
        {
            float multiplier = m_VerticalVelocity < 0 ? m_Settings.Jump.fallMultiplier : 1f;
            m_VerticalVelocity += Physics.gravity.y * multiplier * Time.deltaTime;
        }
        // Ground snap
        else if (m_GroundDetector.IsGrounded && m_VerticalVelocity < 0)
        {
            m_VerticalVelocity = -2f; // Small downward force to maintain ground contact
        }
        // Apply vertical movement
        m_Controller.Move(new Vector3(0, m_VerticalVelocity, 0) * Time.deltaTime);
    }

    private void HandleJumpJets()
    {
        // Update fall time tracking
        if (!m_GroundDetector.IsGrounded)
        {
            m_FallTime += Time.deltaTime;

            if (m_Actions.Player.JumpJet.IsPressed() && Time.time - m_LastJumpTime < m_Settings.JumpJet.postJumpDelay)
            {
                m_JumpJetDelayTimer += Time.deltaTime;
            }
            else
            {
                m_JumpJetDelayTimer = 0f;
            }
        }
        else
        {
            // Reset timers and states when grounded
            m_FallTime = 0f;
            m_JumpJetDelayTimer = 0f;
            if (m_IsUsingJumpJets)
            {
                m_IsUsingJumpJets = false;
                OnJumpJetEnded?.Invoke();
            }
        }

        bool isInAirFromJump = Time.time - m_LastJumpTime < m_Settings.JumpJet.postJumpDelay;
        bool requiresDelay = isInAirFromJump && m_JumpJetDelayTimer < m_Settings.JumpJet.postJumpDelay;

        // Only check if we're in the air and have enough stamina
        bool canUseJumpJets = !m_GroundDetector.IsGrounded &&
                             !requiresDelay;

        // Check if the button was just pressed (for reactivation)
        bool justPressed = m_Actions.Player.JumpJet.WasPressedThisFrame();

        // Allow reactivation if the button is pressed again
        if (justPressed)
        {
            m_IsUsingJumpJets = false;
        }

        if ((m_Actions.Player.JumpJet.IsPressed() || justPressed) && canUseJumpJets)
        {
            if (m_ResourceManager.TryUseStamina(m_Settings.JumpJet.staminaCostPerSecond * Time.deltaTime))
            {
                if (!m_IsUsingJumpJets)
                {
                    m_IsUsingJumpJets = true;
                    OnJumpJetStarted?.Invoke();
                }

                m_VerticalVelocity = m_Settings.JumpJet.thrustForce;
            }
            else
            {
                DeactivateJumpJets();
            }
        }
        else if (m_IsUsingJumpJets)
        {
            DeactivateJumpJets();
        }
    }

    private void DeactivateJumpJets()
    {
        if (m_IsUsingJumpJets)
        {
            m_IsUsingJumpJets = false;
            OnJumpJetEnded?.Invoke();
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

        if (m_DashCooldownRemaining <= 0f &&
            m_Actions.Player.Dash.WasPerformedThisFrame() &&
            m_ResourceManager.TryUseStamina(m_Settings.Dash.dashStaminaCost))
        {
            InitiateDash();
        }
    }

    private void InitiateDash()
    {
        m_IsDashing = true;
        m_DashTimeRemaining = m_Settings.Dash.dashDuration;
        m_DashCooldownRemaining = m_Settings.Dash.dashCooldown;

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

    private void UpdateCooldowns()
    {
        if (m_DashCooldownRemaining > 0f)
        {
            m_DashCooldownRemaining -= Time.deltaTime;
        }
    }

    private void OnDestroy()
    {
        m_Actions?.Dispose();
    }
}