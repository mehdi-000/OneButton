using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns extra players as peak altitude crosses escalating thresholds (duo, trio, quad…).
/// Flip input stays on the primary; partners mirror hold/release so all spin together.
/// </summary>
public class MultiPlayerJuggleController : MonoBehaviour
{
    [SerializeField] PlayerController primaryPlayer;
    [SerializeField] float unlockPeakHeightMeters = 200f;
    [Tooltip("Peak height for each additional partner after the first. If zero, matches the first unlock height.")]
    [SerializeField] float heightStepPerPartnerMeters;
    [SerializeField] int maxPartnerCount = 2;
    [FormerlySerializedAs("innerPartnerXOffset")]
    [FormerlySerializedAs("partnerXOffset")]
    [FormerlySerializedAs("secondPlayerXOffset")]
    [SerializeField] float partnerXOffset = 1.2f;
    [FormerlySerializedAs("secondPlayerYOffset")]
    [SerializeField] float partnerYOffset = 100f;
    [FormerlySerializedAs("secondPlayerTint")]
    [SerializeField] Color partnerTint = new Color(0.72f, 0.88f, 1f, 1f);
    [SerializeField] Color[] extraPartnerTints;

    [Header("Partner Magnifying Lens")]
    [SerializeField] MagnifiyingLens partnerLensTemplate;
    [SerializeField] RenderTexture partnerCloseViewTexture;
    [SerializeField] Vector3 partnerLensOffset = new Vector3(0f, -10f, 0f);
    [Tooltip("Extra horizontal push away from center so partner lenses sit beside, not on top of, each character.")]
    [SerializeField] float partnerLensOutwardOffset = 2.8f;

    readonly List<PlayerController> _partners = new();
    readonly List<RenderTexture> _partnerRenderTextures = new();

    float HeightStep =>
        heightStepPerPartnerMeters > 0f ? heightStepPerPartnerMeters : unlockPeakHeightMeters;

    bool PartnersActive => _partners.Count > 0;

    void Awake()
    {
        if (primaryPlayer == null)
            primaryPlayer = FindAnyObjectByType<PlayerController>();

        if (partnerLensTemplate == null)
        {
            var lenses = FindObjectsByType<MagnifiyingLens>(FindObjectsSortMode.None);
            for (int i = 0; i < lenses.Length; i++)
            {
                if (lenses[i] != null && !lenses[i].gameObject.activeSelf)
                {
                    partnerLensTemplate = lenses[i];
                    break;
                }
            }
        }
    }

    void OnEnable()
    {
        GameplayEventBus.PlayerFell += OnPlayerFell;
        GameplayEventBus.FlipHoldStarted += MirrorFlipHoldStarted;
        GameplayEventBus.FlipHoldEnded += MirrorFlipHoldEnded;
    }

    void OnDisable()
    {
        GameplayEventBus.PlayerFell -= OnPlayerFell;
        GameplayEventBus.FlipHoldStarted -= MirrorFlipHoldStarted;
        GameplayEventBus.FlipHoldEnded -= MirrorFlipHoldEnded;
        GameplayEventBus.SetPartnersActive(false);
        ReleasePartnerFlips();
    }

    void OnDestroy()
    {
        for (int i = 0; i < _partnerRenderTextures.Count; i++)
        {
            if (_partnerRenderTextures[i] != null)
                _partnerRenderTextures[i].Release();
        }
        _partnerRenderTextures.Clear();
    }

    void FixedUpdate()
    {
        if (!PartnersActive || !CrazyPanDogUIController.GameStarted)
            return;

        float minHeight = float.PositiveInfinity;
        float maxHeight = 0f;
        bool anyAlive = false;

        if (primaryPlayer != null && !primaryPlayer.HasFallen)
        {
            anyAlive = true;
            minHeight = maxHeight = primaryPlayer.HeightAbovePlaySurface;
        }

        for (int i = 0; i < _partners.Count; i++)
        {
            var p = _partners[i];
            if (p == null || p.HasFallen) continue;
            anyAlive = true;
            float h = p.HeightAbovePlaySurface;
            minHeight = Mathf.Min(minHeight, h);
            maxHeight = Mathf.Max(maxHeight, h);
        }

        if (!anyAlive)
            return;

        if (float.IsPositiveInfinity(minHeight))
            minHeight = maxHeight;

        GameplayEventBus.SetPartnersHeights(minHeight, maxHeight);
        GameplayEventBus.SetHeightAbovePlaySurface(maxHeight);
    }

    void Update()
    {
        if (!CrazyPanDogUIController.GameStarted || primaryPlayer == null)
            return;

        if (_partners.Count >= maxPartnerCount)
            return;

        float nextUnlockHeight = UnlockHeightForPartnerIndex(_partners.Count);
        if (GameplayEventBus.PeakHeightAbovePlaySurface >= nextUnlockHeight)
            UnlockNextPartner();
    }

    float UnlockHeightForPartnerIndex(int partnerIndex) =>
        unlockPeakHeightMeters + partnerIndex * HeightStep;

    void UnlockNextPartner()
    {
        int partnerIndex = _partners.Count;
        SpawnPartner(partnerIndex);

        if (partnerIndex == 0)
            GameplayEventBus.SetPartnersActive(true);

        GameplayEventBus.RaisePartnersUnlocked(_partners.Count);
    }

    float PartnerHorizontalOffset(int partnerIndex) =>
        partnerIndex == 0 ? partnerXOffset : -partnerXOffset;

    void SpawnPartner(int partnerIndex)
    {
        Transform playerRoot = primaryPlayer.PlayerRoot;
        float horizontalOffset = PartnerHorizontalOffset(partnerIndex);
        var spawnPos = playerRoot.position + new Vector3(horizontalOffset, partnerYOffset, 0f);
        var cloneRootGo = Instantiate(playerRoot.gameObject, spawnPos, playerRoot.rotation, playerRoot.parent);
        cloneRootGo.name = $"Player Partner {partnerIndex + 1}";

        var attract = cloneRootGo.GetComponentInChildren<TitleAttractController>();
        if (attract != null)
            Destroy(attract);

        var clone = cloneRootGo.GetComponentInChildren<PlayerController>();
        clone.SetFlipInputManaged(true);
        clone.SetPublishesFlipProgress(false);
        TintSprites(cloneRootGo, PartnerTint(partnerIndex));

        var sourceRb = primaryPlayer.GetComponent<Rigidbody2D>();
        var cloneRb = clone.GetComponent<Rigidbody2D>();
        if (sourceRb != null && cloneRb != null)
            cloneRb.linearVelocity = sourceRb.linearVelocity;

        var partnerTexture = CreatePartnerRenderTexture();
        ConfigurePartnerCloseCam(cloneRootGo, partnerTexture);
        SpawnPartnerLens(clone, partnerTexture, partnerIndex);

        _partners.Add(clone);

        if (primaryPlayer.IsFlipHeld)
            clone.SetExternalFlipHeld(true, primaryPlayer.FlipHoldTime);
    }

    Color PartnerTint(int partnerIndex)
    {
        if (extraPartnerTints != null && partnerIndex < extraPartnerTints.Length)
            return extraPartnerTints[partnerIndex];

        float hueShift = partnerIndex * 0.12f;
        Color.RGBToHSV(partnerTint, out float h, out float s, out float v);
        var tint = Color.HSVToRGB((h + hueShift) % 1f, s, v);
        tint.a = partnerTint.a;
        return tint;
    }

    RenderTexture CreatePartnerRenderTexture()
    {
        if (partnerCloseViewTexture == null)
            return null;

        var rt = new RenderTexture(partnerCloseViewTexture.descriptor);
        rt.Create();
        _partnerRenderTextures.Add(rt);
        return rt;
    }

    static void ConfigurePartnerCloseCam(GameObject partnerRoot, RenderTexture texture)
    {
        if (texture == null) return;

        var cameras = partnerRoot.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].targetTexture = texture;

        var listeners = partnerRoot.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            Destroy(listeners[i]);
    }

    void SpawnPartnerLens(PlayerController partner, RenderTexture texture, int partnerIndex)
    {
        if (partnerLensTemplate == null || texture == null)
            return;

        var lensGo = Instantiate(partnerLensTemplate.gameObject);
        lensGo.name = $"PartnerMagnifiyingLens_{partnerIndex + 1}";
        lensGo.SetActive(true);

        var lens = lensGo.GetComponent<MagnifiyingLens>();
        if (lens == null) return;

        float outwardSide = PartnerHorizontalOffset(partnerIndex) >= 0f ? 1f : -1f;
        var lensOffset = partnerLensOffset + new Vector3(outwardSide * partnerLensOutwardOffset, 0f, 0f);
        lens.InitializePartner(partner, texture, lensOffset, minHeightMeters: 0f, PartnerTint(partnerIndex));
    }

    void MirrorFlipHoldStarted()
    {
        if (!PartnersActive) return;
        float holdTime = primaryPlayer != null ? primaryPlayer.FlipHoldTime : 0f;
        for (int i = 0; i < _partners.Count; i++)
        {
            var p = _partners[i];
            if (p != null && !p.HasFallen)
                p.SetExternalFlipHeld(true, holdTime);
        }
    }

    void MirrorFlipHoldEnded()
    {
        if (!PartnersActive) return;
        ReleasePartnerFlips();
    }

    void ReleasePartnerFlips()
    {
        for (int i = 0; i < _partners.Count; i++)
        {
            if (_partners[i] != null)
                _partners[i].SetExternalFlipHeld(false);
        }
    }

    static void TintSprites(GameObject root, Color tint)
    {
        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = tint;
    }

    void OnPlayerFell(PlayerController _)
    {
        if (!PartnersActive || !CrazyPanDogUIController.GameStarted) return;

        ReleasePartnerFlips();
        GameplayEventBus.RaiseFallenOffSurface();
    }
}
