using UnityEngine;

/// <summary>
/// Handles ground detection for any character or entity that needs it.
/// Can be used by both player characters and AI.
/// </summary>
public class GroundDetector : MonoBehaviour
{
    [Header("Ground Check Settings")]
    [Tooltip("Radius of the sphere used for ground detection")]
    [SerializeField] private float m_SphereRadius = 0.25f;

    [Tooltip("How far down to check for ground")]
    [SerializeField] private float m_GroundCheckDistance = 0.4f;

    [Tooltip("Offset from character's base for starting the ground check")]
    [SerializeField] private float m_GroundCheckOffset = 0.3f;

    [Tooltip("Which layers should be considered as ground")]
    [SerializeField] private LayerMask m_GroundLayers;

    // Cached result of ground check
    private bool m_IsGrounded;

    // Store the hit info for debug visualization
    private RaycastHit m_LastGroundHit;

    // Event that fires when grounded state changes
    public event System.Action<bool> OnGroundStateChanged;

    // Public property to check if grounded
    public bool IsGrounded => m_IsGrounded;

    /// <summary>
    /// Performs the ground check using SphereCast
    /// </summary>
    /// <returns>True if ground is detected, false otherwise</returns>
    public bool CheckGround()
    {
        // Calculate start position of the ground check
        Vector3 origin = transform.position + Vector3.up * m_GroundCheckOffset;
        bool wasGrounded = m_IsGrounded;

        // Perform the sphere cast
        m_IsGrounded = Physics.SphereCast(
            origin,                     // Start position
            m_SphereRadius,            // Radius of the sphere
            Vector3.down,              // Direction to check
            out m_LastGroundHit,       // Store hit information
            m_GroundCheckDistance,     // How far to check
            m_GroundLayers            // Which layers to check against
        );

        // Notify listeners if grounded state changed
        if (wasGrounded != m_IsGrounded)
        {
            OnGroundStateChanged?.Invoke(m_IsGrounded);
        }

        return m_IsGrounded;
    }

    /// <summary>
    /// Draws debug visualization of the ground check in the Scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!enabled) return;

        // Calculate start and end positions
        Vector3 origin = transform.position + Vector3.up * m_GroundCheckOffset;
        Vector3 end = origin + Vector3.down * m_GroundCheckDistance;

        // Draw the sphere cast path
        Gizmos.color = m_IsGrounded ? Color.green : Color.red;

        // Draw start sphere
        Gizmos.DrawWireSphere(origin, m_SphereRadius);

        // Draw line to show cast direction
        Gizmos.DrawLine(origin, end);

        // If grounded, show where we hit
        if (m_IsGrounded && m_LastGroundHit.collider != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_LastGroundHit.point, 0.1f);
        }
    }
}