using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Starts a new attempt (reloads the scene) when R is pressed after the landing.</summary>
public class RoundFlow : MonoBehaviour
{
    [SerializeField] private GymnastController gymnast;

    private void Update()
    {
        if (gymnast.HasLanded && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
