using UnityEngine;

/// <summary>
/// Handles animation states for the character, responding to both movement and ground detection events
/// </summary>
[RequireComponent(typeof(CharacterMovementController), typeof(GroundDetector))]
public class CharacterAnimatorController : MonoBehaviour
{
    // Animation parameter hashes for better performance
    private readonly int k_SpeedHash = Animator.StringToHash("Speed");
    private readonly int k_IsGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int k_JumpHash = Animator.StringToHash("Jump");

    // Component references
    private Animator m_Animator;
    private CharacterMovementController m_MovementController;
    private GroundDetector m_GroundDetector;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Get required components
        m_Animator = GetComponentInChildren<Animator>();
        m_MovementController = GetComponent<CharacterMovementController>();
        m_GroundDetector = GetComponent<GroundDetector>();

        if (m_Animator == null || m_MovementController == null || m_GroundDetector == null)
        {
            Debug.LogError($"[{GetType().Name}] Required components are missing!");
            enabled = false;
            return;
        }

        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (m_MovementController != null)
        {
            m_MovementController.OnMoveSpeedChanged += HandleMovementChanged;
            m_MovementController.OnJumpInitiated += HandleJumpInitiated;
        }

        if (m_GroundDetector != null)
        {
            m_GroundDetector.OnGroundStateChanged += HandleGroundStateChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (m_MovementController != null)
        {
            m_MovementController.OnMoveSpeedChanged -= HandleMovementChanged;
            m_MovementController.OnJumpInitiated -= HandleJumpInitiated;
        }

        if (m_GroundDetector != null)
        {
            m_GroundDetector.OnGroundStateChanged -= HandleGroundStateChanged;
        }
    }

    private void HandleMovementChanged(float normalizedSpeed)
    {
        m_Animator.SetFloat(k_SpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
    }

    private void HandleGroundStateChanged(bool isGrounded)
    {
        m_Animator.SetBool(k_IsGroundedHash, isGrounded);
    }

    private void HandleJumpInitiated()
    {
        m_Animator.SetTrigger(k_JumpHash);
    }
}