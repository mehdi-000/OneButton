using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(CinemachineBrain))]
public class closecam : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera closeCamVirtual;

    CinemachineBrain _brain;
    int _overrideId = -1;

    void Awake()
    {
        _brain = GetComponent<CinemachineBrain>();
    }

    void OnEnable()
    {
        if (closeCamVirtual == null || _brain == null)
            return;

        _overrideId = _brain.SetCameraOverride(-1, null, closeCamVirtual, 1f, -1f);
    }

    void OnDisable()
    {
        if (_brain != null && _overrideId >= 0)
        {
            _brain.ReleaseCameraOverride(_overrideId);
            _overrideId = -1;
        }
    }
}
