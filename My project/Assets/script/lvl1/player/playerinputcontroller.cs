using UnityEngine;
using UnityEngine.InputSystem;

public class playerinputcontroller : MonoBehaviour
{
    public Vector2 movementinputvector { get; private set; }
    public Vector2 lookInputVector { get; private set; }

    private void OnMove(InputValue inputValue)
    {
        movementinputvector = inputValue.Get<Vector2>();
        Debug.Log("MOVE = " + movementinputvector);
    }

    private void OnLook(InputValue inputValue)
    {
        lookInputVector = inputValue.Get<Vector2>();
        Debug.Log("LOOK = " + lookInputVector);
    }
}
