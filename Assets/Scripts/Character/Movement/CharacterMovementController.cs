using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all character movement including basic locomotion, jumping, and jump jets.
/// Requires MovementSettings component for configuration.
/// Will be refactored later to adhere to the Component Pattern.
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
    private PlayerDash m_PlayerDash;

    // Movement state
    private Vector3 m_Movement;
    private float m_VerticalVelocity;
    private float m_JumpBufferCounter;
    private float m_FallTime;
    private float m_LastJumpTime;

    // Jump jet state
    private bool m_IsUsingJumpJets;
    private float m_JumpJetDelayTimer = 0f;

    // Events
    public event System.Action OnJumpInitiated;
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
        m_PlayerDash = GetComponent<PlayerDash>(); // Optional dash component
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

        // Don't handle movement if dashing
        if (m_PlayerDash != null && m_PlayerDash.IsDashing)
            return;

        HandleMovement();
        HandleJumpAndGravity();
        HandleJumpJets();
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
            m_VerticalVelocity = -m_Settings.GroundHandling.groundSnapForce;
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

    private void OnDestroy()
    {
        m_Actions?.Dispose();
    }
}