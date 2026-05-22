using UnityEngine;

[RequireComponent(typeof(Camera))]
public class closecam : MonoBehaviour
{
    [SerializeField] Transform followTarget;
    [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);

    void LateUpdate()
    {
        if (followTarget == null) return;
        transform.position = followTarget.position + offset;
        transform.rotation = Quaternion.identity;
    }
}
