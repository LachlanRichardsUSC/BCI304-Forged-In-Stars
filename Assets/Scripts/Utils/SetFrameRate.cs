using UnityEngine;

public class SetFrameRate : MonoBehaviour
{
    [Header("Frame Rate Settings")]
    [SerializeField] private int targetFPS = 60; // Adjustable FPS cap
    [SerializeField] private bool enableVSync = false; // Toggle VSync

    private void Awake()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        QualitySettings.vSyncCount = enableVSync ? 1 : 0; // Enable or disable VSync
        Application.targetFrameRate = enableVSync ? -1 : targetFPS; // Disable FPS cap if VSync is on
    }

    private void OnValidate()
    {
        ApplySettings(); // Auto-apply changes when modified in the Inspector
    }
}
