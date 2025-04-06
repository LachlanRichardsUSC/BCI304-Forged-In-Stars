using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pickup : MonoBehaviour {
    private void OnTriggerEnter(Collider other) {
        CharacterStats characterStats = other.GetComponent<CharacterStats>();
        if (characterStats != null) {
            characterStats.Strength.AddModifier(new StatModifier(10, StatModType.Flat, this));
            Debug.Log(characterStats.Strength.Value);
            gameObject.SetActive(false);
        }
    }
}
