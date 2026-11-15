// BombTester.cs - À ajouter à un GameObject vide dans la scène
using UnityEngine;
using UnityEngine.InputSystem;

public class BombTester : MonoBehaviour
{
    public BombController bombToTest;
    public Key testInputKey = Key.B; // Input System (Part 1 : Active Input Handling = Input System)
    
    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[testInputKey].wasPressedThisFrame && bombToTest != null)
        {
            bombToTest.OnTouch();
            Debug.Log($"💣 Bomb touched! (Key {testInputKey} pressed)");
        }
        
        // Reset avec R
        if (keyboard.rKey.wasPressedThisFrame && bombToTest != null)
        {
            // Forcer le reset si besoin
            Debug.Log("🔄 Reset bomb manually");
            // Vous pouvez ajouter une méthode Reset() dans BombController si nécessaire
        }
    }
}