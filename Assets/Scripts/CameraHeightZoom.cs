using UnityEngine;
using UnityEngine.Serialization;
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

    [SerializeField] float heightExponent = 1.09f;

    [Header("Smoothing")]
    [SerializeField] float smoothSpeed = 3f;

    [Header("Multi-Player Framing")]
    [FormerlySerializedAs("duoSpreadOrthoPerMeter")]
    [SerializeField] float partnerSpreadOrthoPerMeter = 0.55f;
    [FormerlySerializedAs("duoSpreadOffsetYPerMeter")]
    [SerializeField] float partnerSpreadOffsetYPerMeter = 0.25f;

    CinemachineTransposer _transposer;
    float _targetOrtho;
    Vector3 _targetOffset;

    public float BaseOrthoSize => baseOrthoSize;
    public float CurrentOrthoSize => vcam != null ? vcam.m_Lens.OrthographicSize : baseOrthoSize;

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
        float height = GetCameraReferenceHeight();
        float spread = GameplayEventBus.PartnersActive ? GameplayEventBus.PartnersVerticalSpread : 0f;
        float scaled = Mathf.Pow(1f + height, heightExponent) - 1f;

        _targetOrtho = baseOrthoSize + scaled * orthoSizePerUnit + spread * partnerSpreadOrthoPerMeter;
        _targetOffset = baseFollowOffset + Vector3.up * (scaled * followOffsetYPerUnit + spread * partnerSpreadOffsetYPerMeter);

        float dt = Time.deltaTime * smoothSpeed;
        vcam.m_Lens.OrthographicSize = Mathf.Lerp(vcam.m_Lens.OrthographicSize, _targetOrtho, dt);
        _transposer.m_FollowOffset = Vector3.Lerp(_transposer.m_FollowOffset, _targetOffset, dt);
    }

    static float GetCameraReferenceHeight()
    {
        if (GameplayEventBus.PartnersActive && GameplayEventBus.PartnersVerticalSpread > 0.01f)
            return GameplayEventBus.PartnersMidHeightAboveSurface;

        return Mathf.Max(0f, GameplayEventBus.HeightAbovePlaySurface);
    }
}
