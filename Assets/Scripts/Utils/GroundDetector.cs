using UnityEngine;

/// <summary>
/// Multi-point ground detection system optimized for large characters.
/// Simply detects if character is grounded for jump/stamina mechanics.
/// </summary>
public class GroundDetector : MonoBehaviour
{
    [Header("Ground Check Configuration")]
    [Tooltip("Height offset from character base to start ground checks")]
    [SerializeField] private float m_GroundCheckStartHeight = 0.5f;

    [Tooltip("Maximum distance to check for ground")]
    [SerializeField] private float m_GroundCheckDistance = 1.5f;

    [Tooltip("Radius for the detection pattern (scales with character size)")]
    [SerializeField] private float m_DetectionRadius = 1.0f;

    [Tooltip("Layers considered as ground")]
    [SerializeField] private LayerMask m_GroundLayers;

    [Header("Debug")]
    [SerializeField] private bool m_ShowDebugRays = true;

    // Detection state
    private bool m_IsGrounded;
    private float m_DistanceToGround;

    // Performance optimization - preallocate arrays
    private readonly Vector3[] m_RaycastOffsets = new Vector3[5];
    private readonly RaycastHit[] m_HitResults = new RaycastHit[5];

    // Events
    public event System.Action<bool> OnGroundStateChanged;

    // Public properties
    public bool IsGrounded => m_IsGrounded;
    public float DistanceToGround => m_DistanceToGround;

    private void Awake()
    {
        InitializeRaycastPattern();
    }

    private void InitializeRaycastPattern()
    {
        // Create an X pattern of raycast points
        // Center point
        m_RaycastOffsets[0] = Vector3.zero;

        // Four corner points (scaled to character size)
        float offset = m_DetectionRadius * 0.7f; // 70% of radius for good coverage
        m_RaycastOffsets[1] = new Vector3(offset, 0, offset);    // Front-right
        m_RaycastOffsets[2] = new Vector3(-offset, 0, offset);   // Front-left
        m_RaycastOffsets[3] = new Vector3(offset, 0, -offset);   // Back-right
        m_RaycastOffsets[4] = new Vector3(-offset, 0, -offset);  // Back-left
    }

    /// <summary>
    /// Performs multi-point ground detection
    /// </summary>
    public bool CheckGround()
    {
        bool wasGrounded = m_IsGrounded;

        // Reset detection state
        m_IsGrounded = false;
        m_DistanceToGround = float.MaxValue;

        float closestDistance = float.MaxValue;

        // Perform raycasts from each point
        for (int i = 0; i < m_RaycastOffsets.Length; i++)
        {
            Vector3 rayOrigin = transform.position + transform.TransformDirection(m_RaycastOffsets[i]);
            rayOrigin.y += m_GroundCheckStartHeight;

            if (Physics.Raycast(rayOrigin, Vector3.down, out m_HitResults[i],
                m_GroundCheckDistance + m_GroundCheckStartHeight, m_GroundLayers))
            {
                float distance = m_HitResults[i].distance - m_GroundCheckStartHeight;
                closestDistance = Mathf.Min(closestDistance, distance);
            }
        }

        // Consider grounded if any ray hit within threshold
        if (closestDistance < float.MaxValue)
        {
            m_IsGrounded = closestDistance <= 0.1f; // Small threshold for ground contact
            m_DistanceToGround = closestDistance;
        }

        // Fire event if state changed
        if (wasGrounded != m_IsGrounded)
        {
            OnGroundStateChanged?.Invoke(m_IsGrounded);
        }

        return m_IsGrounded;
    }

    private void OnDrawGizmos()
    {
        if (!m_ShowDebugRays || !enabled) return;

        // Initialize pattern if needed
        if (m_RaycastOffsets[0] == Vector3.zero && m_RaycastOffsets[1] == Vector3.zero)
        {
            InitializeRaycastPattern();
        }

        // Draw rays
        for (int i = 0; i < m_RaycastOffsets.Length; i++)
        {
            Vector3 rayOrigin = transform.position + transform.TransformDirection(m_RaycastOffsets[i]);
            rayOrigin.y += m_GroundCheckStartHeight;

            // Color based on hit status
            if (Application.isPlaying && m_HitResults[i].collider != null)
            {
                // Green for grounded, yellow for detected but not grounded
                float distance = m_HitResults[i].distance - m_GroundCheckStartHeight;
                Gizmos.color = distance <= 0.1f ? Color.green : Color.yellow;
                Gizmos.DrawLine(rayOrigin, m_HitResults[i].point);

                // Draw hit point sphere
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(m_HitResults[i].point, 0.1f);
            }
            else
            {
                // Red ray for no hit
                Gizmos.color = Color.red;
                Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * (m_GroundCheckDistance + m_GroundCheckStartHeight));
            }
        }

        // Draw detection radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, m_DetectionRadius);
    }
}