using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [SerializeField] private CursorLockMode defaultLockMode = CursorLockMode.Confined;
    [SerializeField] private bool hideByDefault = false;

    void Start()
    {
        SetCursorState(defaultLockMode, !hideByDefault);
    }

    void Update()
    {
        // Optional: Add controls to toggle cursor state
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorLock();
        }
    }

    public void SetCursorState(CursorLockMode lockMode, bool visible)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = visible;
    }

    public void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}