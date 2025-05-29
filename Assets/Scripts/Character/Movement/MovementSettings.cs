using UnityEngine;

/// <summary>
/// Contains all settings related to character movement mechanics.
/// Can be used to create different movement presets for different character types.
/// </summary>
public class MovementSettings : MonoBehaviour
{
    [System.Serializable]
    public class BasicMovementSettings
    {
        [Tooltip("Base walking speed")]
        public float walkSpeed = 5f;

        [Tooltip("Running speed when sprint is active")]
        public float runSpeed = 10f;

        [Tooltip("How quickly the character rotates to face movement direction")]
        public float turnSpeed = 10f;

        [Tooltip("Stamina cost per second while sprinting")]
        public float sprintStaminaCost = 10f;
    }

    [System.Serializable]
    public class JumpSettings
    {
        [Tooltip("Force applied when jumping")]
        public float jumpForce = 10f;

        [Tooltip("How long to buffer a jump input")]
        public float bufferDuration = 0.2f;

        [Tooltip("Multiplier for gravity when falling")]
        public float fallMultiplier = 2.5f;

        [Tooltip("Minimum time that must pass between jumps")]
        public float jumpCooldown = 0.1f;
    }

    [System.Serializable]
    public class JumpJetSettings
    {
        [Tooltip("Upward force applied by jump jets")]
        public float thrustForce = 15f;

        [Tooltip("Stamina cost per second while jump jets are active")]
        public float staminaCostPerSecond = 20f;

        [Tooltip("Delay before jump jets can activate after jumping (in seconds)")]
        public float postJumpDelay = 0.25f;

        //[Tooltip("Maximum duration jump jets can be active")]
        //public float maxDuration = 2f;
    }

    [System.Serializable]
    public class DashSettings
    {
        [Tooltip("Force applied during dash")]
        public float dashForce = 50f;

        [Tooltip("Duration of the dash in seconds")]
        public float dashDuration = 0.2f;

        [Tooltip("Cooldown before dash can be used again")]
        public float dashCooldown = 1.0f;

        [Tooltip("Stamina cost for each dash")]
        public float dashStaminaCost = 25f;

        [Tooltip("Whether dash should align with camera forward instead of character forward")]
        public bool useCameraForward = true;
    }

    [System.Serializable]
    public class GroundHandlingSettings
    {
        [Tooltip("Force applied to keep character grounded (higher for larger characters)")]
        public float groundSnapForce = 15f;
    }

    [Header("Basic Movement")]
    [SerializeField] private BasicMovementSettings basicMovement = new();

    [Header("Jump Configuration")]
    [SerializeField] private JumpSettings jumpSettings = new();

    [Header("Jump Jet Configuration")]
    [SerializeField] private JumpJetSettings jumpJetSettings = new();

    [Header("Dash Configuration")]
    [SerializeField] private DashSettings dashSettings = new();

    [Header("Ground Handling")]
    [SerializeField] private GroundHandlingSettings groundHandling = new();

    // Public getters for settings
    public BasicMovementSettings BasicMovement => basicMovement;
    public JumpSettings Jump => jumpSettings;
    public JumpJetSettings JumpJet => jumpJetSettings;
    public DashSettings Dash => dashSettings;
    public GroundHandlingSettings GroundHandling => groundHandling;

    /// <summary>
    /// Optional: Method to apply a complete settings preset
    /// </summary>
    public void ApplyPreset(MovementSettings preset)
    {
        if (preset == null) return;

        basicMovement = preset.basicMovement;
        jumpSettings = preset.jumpSettings;
        jumpJetSettings = preset.jumpJetSettings;
        dashSettings = preset.dashSettings;
        groundHandling = preset.groundHandling;
    }

    /// <summary>
    /// Applies settings optimized for large characters (5-10m tall)
    /// </summary>
    public void ApplyLargeCharacterPreset()
    {
        // Scale movement speeds
        basicMovement.walkSpeed = 8f;
        basicMovement.runSpeed = 15f;

        // Stronger jump for larger mass
        jumpSettings.jumpForce = 15f;

        // Stronger ground handling
        groundHandling.groundSnapForce = 20f;

        // Adjust jump jets for larger character
        jumpJetSettings.thrustForce = 20f;
    }

    private void OnValidate()
    {
        // Ensure minimum values are respected
        basicMovement.walkSpeed = Mathf.Max(0.1f, basicMovement.walkSpeed);
        basicMovement.runSpeed = Mathf.Max(basicMovement.walkSpeed, basicMovement.runSpeed);
        basicMovement.turnSpeed = Mathf.Max(0.1f, basicMovement.turnSpeed);

        jumpSettings.jumpForce = Mathf.Max(0, jumpSettings.jumpForce);
        jumpSettings.bufferDuration = Mathf.Max(0, jumpSettings.bufferDuration);
        jumpSettings.fallMultiplier = Mathf.Max(1, jumpSettings.fallMultiplier);

        jumpJetSettings.thrustForce = Mathf.Max(0, jumpJetSettings.thrustForce);
        //jumpJetSettings.maxDuration = Mathf.Max(0.1f, jumpJetSettings.maxDuration);

        dashSettings.dashForce = Mathf.Max(0, dashSettings.dashForce);
        dashSettings.dashDuration = Mathf.Max(0.01f, dashSettings.dashDuration);
        dashSettings.dashCooldown = Mathf.Max(0, dashSettings.dashCooldown);

        groundHandling.groundSnapForce = Mathf.Max(0, groundHandling.groundSnapForce);
    }
}