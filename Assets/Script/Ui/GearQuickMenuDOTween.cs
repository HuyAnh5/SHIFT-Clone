using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GearQuickMenuDOTween : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button btnGear;
    [SerializeField] private RectTransform gearIcon;   // IconGear (để xoay)
    [SerializeField] private RectTransform iconsRow;   // IconsRow (trượt)
    [SerializeField] private CanvasGroup iconsGroup;   // CanvasGroup trên IconsRow

    [Header("Anim")]
    [SerializeField] private float duration = 0.22f;
    [SerializeField] private float slideDistance = 260f; // kéo từ phải -> trái
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private bool startClosed = true;

    private bool isOpen;
    private Vector2 openPos;
    private Vector2 closedPos;
    private Sequence seq;

    private void Awake()
    {
        if (btnGear != null) btnGear.onClick.AddListener(Toggle);

        if (iconsGroup == null && iconsRow != null)
            iconsGroup = iconsRow.GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (iconsRow == null) return;

        if (iconsGroup == null)
            iconsGroup = iconsRow.gameObject.AddComponent<CanvasGroup>();

        // openPos = vị trí bạn set sẵn trong Editor (icons hiện ra bên trái gear)
        openPos = iconsRow.anchoredPosition;

        // closedPos = đẩy IconsRow sang phải để "ẩn" (trượt từ phải sang trái khi mở)
        closedPos = openPos + new Vector2(slideDistance, 0f);

        if (startClosed) SetClosedInstant();
        else SetOpenInstant();
    }

    public void Toggle()
    {
        if (seq != null && seq.IsActive()) return;
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        KillSeq();
        isOpen = true;

        // chuẩn bị trạng thái đóng -> mở
        iconsRow.anchoredPosition = closedPos;
        iconsGroup.alpha = 0f;
        iconsGroup.interactable = true;
        iconsGroup.blocksRaycasts = true;

        btnGear.interactable = false;

        seq = DOTween.Sequence().SetUpdate(true);

        // gear xoay + icons trượt + fade in
        if (gearIcon != null)
            seq.Join(gearIcon.DOLocalRotate(new Vector3(0, 0, -360f), duration, RotateMode.FastBeyond360)
                .SetRelative().SetEase(ease));

        seq.Join(iconsRow.DOAnchorPos(openPos, duration).SetEase(ease));
        seq.Join(iconsGroup.DOFade(1f, duration).SetEase(ease));

        seq.OnComplete(() => btnGear.interactable = true);
    }

    public void Close()
    {
        KillSeq();
        isOpen = false;

        btnGear.interactable = false;

        seq = DOTween.Sequence().SetUpdate(true);

        // gear xoay + icons trượt vào + fade out
        if (gearIcon != null)
            seq.Join(gearIcon.DOLocalRotate(new Vector3(0, 0, -360f), duration, RotateMode.FastBeyond360)
                .SetRelative().SetEase(ease));

        seq.Join(iconsRow.DOAnchorPos(closedPos, duration).SetEase(ease));
        seq.Join(iconsGroup.DOFade(0f, duration).SetEase(ease));

        seq.OnComplete(() =>
        {
            iconsGroup.interactable = false;
            iconsGroup.blocksRaycasts = false;
            btnGear.interactable = true;
        });
    }

    private void SetClosedInstant()
    {
        isOpen = false;
        iconsRow.anchoredPosition = closedPos;
        iconsGroup.alpha = 0f;
        iconsGroup.interactable = false;
        iconsGroup.blocksRaycasts = false;
    }

    private void SetOpenInstant()
    {
        isOpen = true;
        iconsRow.anchoredPosition = openPos;
        iconsGroup.alpha = 1f;
        iconsGroup.interactable = true;
        iconsGroup.blocksRaycasts = true;
    }

    private void KillSeq()
    {
        if (seq != null) seq.Kill();
        seq = null;
    }
}
