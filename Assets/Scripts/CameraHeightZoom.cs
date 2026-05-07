using UnityEngine;
using Cinemachine;

public class CameraHeightZoom : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CinemachineVirtualCamera vcam;

    [Header("Ortho Size")]
    [SerializeField] float baseOrthoSize = 12.12f;
    [SerializeField] float orthoSizePerUnit = 0.3f;

    [Header("Follow Offset")]
    [SerializeField] Vector3 baseFollowOffset = new Vector3(0f, 2.6f, -10f);
    [SerializeField] float followOffsetYPerUnit = 0.4f;

    [Header("Smoothing")]
    [SerializeField] float smoothSpeed = 3f;

    CinemachineTransposer _transposer;
    float _targetOrtho;
    Vector3 _targetOffset;

    void Start()
    {
        if (vcam == null)
            vcam = GetComponent<CinemachineVirtualCamera>();

        _transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        _targetOrtho = baseOrthoSize;
        _targetOffset = baseFollowOffset;
    }

    void LateUpdate()
    {
        float height = Mathf.Max(0f, GameplayEventBus.HeightAbovePlaySurface);

        _targetOrtho = baseOrthoSize + height * orthoSizePerUnit;
        _targetOffset = baseFollowOffset + Vector3.up * (height * followOffsetYPerUnit);

        float dt = Time.deltaTime * smoothSpeed;
        vcam.m_Lens.OrthographicSize = Mathf.Lerp(vcam.m_Lens.OrthographicSize, _targetOrtho, dt);
        _transposer.m_FollowOffset = Vector3.Lerp(_transposer.m_FollowOffset, _targetOffset, dt);
    }
}
