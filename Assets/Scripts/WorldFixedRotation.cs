using UnityEngine;

/// <summary>
/// Use on a child object that should track a parent's position/scale but keep a fixed orientation in world space.
/// Runs in LateUpdate so it applies after the parent's rotation for this frame.
/// </summary>
public class WorldFixedRotation : MonoBehaviour
{
    [SerializeField] Vector3 worldEulerAngles = Vector3.zero;

    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(worldEulerAngles);
    }
}
