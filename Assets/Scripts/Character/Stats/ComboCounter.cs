using UnityEngine;
using TMPro;

public class ComboCounter : MonoBehaviour
{
    public TextMeshProUGUI comboText;     // Reference to the UI text
    public float resetTime = 2f;          // Time before combo resets
    private int comboCount = 0;           // Current combo
    private float lastClickTime;          // When the last hit occurred

    void Update()
    {
        // If time passed since last hit is too long, reset combo
        if (comboCount > 0 && Time.time - lastClickTime > resetTime)
        {
            ResetCombo();
        }

        // Left click simulates attack
        if (Input.GetMouseButtonDown(0))
        {
            AddCombo();
        }
    }

    void AddCombo()
    {
        comboCount++;
        lastClickTime = Time.time;

        // Get a medieval combo title
        string title = GetComboTitle(comboCount);

        // Format: x1\n<Combo Title>
        comboText.text = $"<b>x{comboCount}</b>\n<size=28><i>{title}</i></size>";
        comboText.gameObject.SetActive(true);
    }

    void ResetCombo()
    {
        comboCount = 0;
        comboText.text = "";
        comboText.gameObject.SetActive(false);
    }

    string GetComboTitle(int count)
    {
        if (count < 3) return "Peasant";
        if (count < 6) return "Squire";
        if (count < 10) return "Savage";
        if (count < 15) return "Relentless";
        if (count < 20) return "Executioner";
        return "Warlord";
    }
}