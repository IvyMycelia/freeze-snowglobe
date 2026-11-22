
using UnityEngine;

public class CameraXTarget : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Camera Height")]
    [SerializeField] private float fixedY = 2f;

    private void LateUpdate()
    {
        if (player == null)
            return;

        transform.position = new Vector3(
            player.position.x,
            fixedY,
            0f
        );
    }
}
