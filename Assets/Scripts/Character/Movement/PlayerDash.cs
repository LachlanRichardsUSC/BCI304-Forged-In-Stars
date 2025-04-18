using UnityEngine;

public class PlayerDash : MonoBehaviour
{
    public float dashForce = 20f;            // How strong the dash is
    public float dashCooldown = 5.5f;        // Time before dashes reset
    public int maxDashes = 4;                // Max dashes before cooldown
    private int currentDashes;
    private float lastDashReset;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentDashes = maxDashes;
        lastDashReset = Time.time;
    }

    void Update()
    {
        // Reset dashes after cooldown
        if (Time.time >= lastDashReset + dashCooldown && currentDashes < maxDashes)
        {
            currentDashes = maxDashes;
            Debug.Log("Dashes reset!");
        }

        // Dash when Left Ctrl is pressed
        if (Input.GetKeyDown(KeyCode.LeftControl) && currentDashes > 0)
        {
            Dash();
        }
    }

    void Dash()
    {
        Vector3 dashDirection = transform.forward; // Dash in the forward direction
        rb.linearVelocity = dashDirection * dashForce;   // Directly set the linear velocity

        currentDashes--;
        lastDashReset = Time.time;

        Debug.Log("Dashed! Dashes left: " + currentDashes);
    }
}
