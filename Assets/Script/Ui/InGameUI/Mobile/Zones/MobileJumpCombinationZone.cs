using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileJumpCombinationZone : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler
{
    private enum Region { None, Jump, Action, Mark }

    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private MobileAbilityGate gate;

    [SerializeField] private RectTransform jumpArea;
    [SerializeField] private RectTransform actionArea;
    [SerializeField] private RectTransform markArea;

    [Tooltip("Optional: a bigger rect to use for 'exit cancel' (e.g., a parent that covers Jump+Action+Mark). If null, uses this object's RectTransform.")]
    [SerializeField] private RectTransform dragZoneOverride;

    [Header("Behavior")]
    [Tooltip("If you started the hold by triggering Jump, then sliding into Action will force SWAP (after airborne).")]
    [SerializeField] private bool slideAfterJumpForcesSwap = true;

    [Tooltip("Max time to wait for player to become airborne before firing Swap (prevents fail spam).")]
    [SerializeField] private float waitAirborneTimeout = 0.25f;

    [Tooltip("If finger leaves DragZone (override or this RectTransform), stop outputs.")]
    [SerializeField] private bool cancelWhenExitZone = true;

    [Header("Visual Feedback (optional)")]
    [SerializeField] private CanvasGroup jumpVisual;
    [SerializeField] private CanvasGroup actionVisual;
    [SerializeField] private CanvasGroup markVisual;

    [SerializeField] private float idleAlpha = 0.25f;
    [SerializeField] private float pressedAlpha = 1f;

    [Header("Pulse (optional)")]
    [SerializeField] private bool pulseOnTrigger = true;
    [SerializeField] private float pulseDuration = 0.12f;
    [SerializeField] private float pulseScale = 1.06f;

    [Header("Locked Button Visibility")]
    [Tooltip("If ON: locked buttons are hidden/disabled (SetActive(false)). If OFF: locked buttons stay visible and are only dimmed.")]
    [SerializeField] private bool hideLockedButtonsCompletely = true;

    [Tooltip("Optional: the SWAP button UI rect. If not assigned, can fall back to Mark button UI (see below).")]
    [SerializeField] private RectTransform swapArea;

    [Tooltip("Optional: the SWAP button visual. If not assigned, can fall back to Mark button visual (see below).")]
    [SerializeField] private CanvasGroup swapVisual;

    [Tooltip("If swapArea/swapVisual are not set, treat Mark button as the Swap button for visibility locking.")]
    [SerializeField] private bool swapButtonFallbackToMark = true;

    [Tooltip("Dim locked buttons by overriding alpha (only used when hideLockedButtonsCompletely = false).")]
    [SerializeField] private bool dimLockedButtons = true;
    [SerializeField] private float lockedAlpha = 0.08f;

    [Tooltip("When pressing a locked button, do a Fail Shake (if CameraShake2D is available).")]
    [SerializeField] private bool failShakeOnLockedPress = true;

    // --- runtime ---
    private int activePointerId = int.MinValue;
    private bool jumpTriggeredThisHold;
    private bool secondaryTriggeredThisHold; // Action OR Mark (first one wins)
    private Coroutine waitAirborneCo;

    private Region heldRegion = Region.None;

    private RectTransform ZoneRT => dragZoneOverride != null ? dragZoneOverride : (RectTransform)transform;

    private RectTransform SwapAreaRT => swapArea != null ? swapArea : (swapButtonFallbackToMark ? markArea : null);
    private CanvasGroup SwapVisualCG => swapVisual != null ? swapVisual : (swapButtonFallbackToMark ? markVisual : null);

    private Vector3 jumpBaseScale = Vector3.one;
    private Vector3 actionBaseScale = Vector3.one;
    private Vector3 markBaseScale = Vector3.one;

    private Coroutine jumpPulseCo;
    private Coroutine actionPulseCo;
    private Coroutine markPulseCo;

    private bool lockedFeedbackThisHold;

    private void Awake()
    {
        if (jumpVisual != null) jumpBaseScale = jumpVisual.transform.localScale;
        if (actionVisual != null) actionBaseScale = actionVisual.transform.localScale;
        if (markVisual != null) markBaseScale = markVisual.transform.localScale;

        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (gate == null) gate = FindAnyObjectByType<MobileAbilityGate>();

        ApplyLockedVisibility();
        ApplyVisuals(Region.None);
    }

    private void LateUpdate()
    {
        ApplyLockedVisibility();
        // Icon Mark↔Swap giờ do MobileMarkSwapIcon tự xử lý (không còn ở đây)
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointerId != int.MinValue) return;
        activePointerId = e.pointerId;

        jumpTriggeredThisHold = false;
        secondaryTriggeredThisHold = false;
        lockedFeedbackThisHold = false;

        HandleAtPosition(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;
        HandleAtPosition(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;
        ResetHold();
    }

    private void HandleAtPosition(PointerEventData e)
    {
        if (cancelWhenExitZone && !RectTransformUtility.RectangleContainsScreenPoint(ZoneRT, e.position, e.pressEventCamera))
        {
            ResetHold();
            return;
        }

        var cam = e.pressEventCamera;

        Region r = GetRegionAtPoint(e.position, cam);
        if (r != heldRegion)
        {
            heldRegion = r;
            ApplyVisuals(heldRegion);
        }

        // 1) Jump: once per hold (only when grounded)
        if (!jumpTriggeredThisHold && r == Region.Jump)
        {
            if (!IsJumpUnlocked())
            {
                LockedFeedback();
                return;
            }

            MobileUIInput.TriggerJump();
            jumpTriggeredThisHold = true;
            Pulse(Region.Jump);
            return;
        }

        // 2) Secondary: only allow ONE of {Action, Mark} per hold
        if (secondaryTriggeredThisHold) return;

        if (r == Region.Action)
        {
            secondaryTriggeredThisHold = true;

            if (!IsActionUnlocked())
            {
                LockedFeedback();
                return;
            }

            if (slideAfterJumpForcesSwap && jumpTriggeredThisHold)
                StartWaitAirborneThenSwap();
            else
                TriggerActionImmediate();

            return;
        }

        if (r == Region.Mark)
        {
            secondaryTriggeredThisHold = true;

            // Nếu Mark button chính là Mark/Swap ability thì phải check SwapUnlocked
            if (swapButtonFallbackToMark && !IsSwapUnlocked())
            {
                LockedFeedback();
                return;
            }

            MobileUIInput.TriggerMark();
            Pulse(Region.Mark);
            return;
        }
    }

    private Region GetRegionAtPoint(Vector2 screenPos, Camera cam)
    {
        if (actionArea != null && actionArea.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(actionArea, screenPos, cam))
            return Region.Action;

        if (markArea != null && markArea.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(markArea, screenPos, cam))
            return Region.Mark;

        if (jumpArea != null && jumpArea.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(jumpArea, screenPos, cam))
            return Region.Jump;

        return Region.None;
    }

    private void TriggerActionImmediate()
    {
        // Ưu tiên GravityFlipTrigger trước (không shift world)
        if (GravityFlipTrigger.PlayerInsideAny && GravityFlipTrigger.TryActivateFromUI())
        {
            Pulse(Region.Action);
            return;
        }

        if (player != null && player.IsGroundedNow)
        {
            MobileUIInput.TriggerShift();
            Pulse(Region.Action);
        }
        else
        {
            if (!IsSwapUnlocked())
            {
                LockedFeedback();
                return;
            }

            MobileUIInput.TriggerSwap();
            Pulse(Region.Action);
        }
    }

    private void StartWaitAirborneThenSwap()
    {
        if (!IsSwapUnlocked())
        {
            LockedFeedback();
            return;
        }

        if (waitAirborneCo != null) StopCoroutine(waitAirborneCo);
        waitAirborneCo = StartCoroutine(WaitUntilAirborneThenSwap());
    }

    private IEnumerator WaitUntilAirborneThenSwap()
    {
        float t = 0f;
        while (t < waitAirborneTimeout)
        {
            if (player == null || !player.IsGroundedNow)
            {
                if (!IsSwapUnlocked())
                {
                    LockedFeedback();
                    yield break;
                }

                MobileUIInput.TriggerSwap();
                Pulse(Region.Action);
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        // timeout and still grounded -> do nothing
    }

    private void ResetHold()
    {
        activePointerId = int.MinValue;
        jumpTriggeredThisHold = false;
        secondaryTriggeredThisHold = false;
        lockedFeedbackThisHold = false;

        if (waitAirborneCo != null)
        {
            StopCoroutine(waitAirborneCo);
            waitAirborneCo = null;
        }

        heldRegion = Region.None;
        ApplyVisuals(Region.None);
    }

    // ----------------- Visuals -----------------

    private void ApplyLockedVisibility()
    {
        if (!hideLockedButtonsCompletely)
        {
            SetActiveSafe(jumpArea, true);
            SetActiveSafe(actionArea, true);
            SetActiveSafe(markArea, true);
            SetActiveSafe(jumpVisual, true);
            SetActiveSafe(actionVisual, true);
            SetActiveSafe(markVisual, true);
            SetActiveSafe(swapArea, true);
            SetActiveSafe(swapVisual, true);
            return;
        }

        bool jumpOn = IsJumpUnlocked();
        bool actionOn = IsActionUnlocked();
        bool swapOn = IsSwapUnlocked();

        SetActiveSafe(jumpArea, jumpOn);
        SetActiveSafe(jumpVisual, jumpOn);

        SetActiveSafe(actionArea, actionOn);
        SetActiveSafe(actionVisual, actionOn);

        var swapRT = SwapAreaRT;
        var swapCG = SwapVisualCG;
        if (swapRT != null) SetActiveSafe(swapRT, swapOn);
        if (swapCG != null) SetActiveSafe(swapCG, swapOn);

        if (!swapButtonFallbackToMark)
        {
            SetActiveSafe(markArea, true);
            SetActiveSafe(markVisual, true);
        }
    }

    private void ApplyVisuals(Region region)
    {
        float jumpA = region == Region.Jump ? pressedAlpha : idleAlpha;
        float actionA = region == Region.Action ? pressedAlpha : idleAlpha;
        float markA = region == Region.Mark ? pressedAlpha : idleAlpha;

        if (!hideLockedButtonsCompletely && dimLockedButtons)
        {
            if (!IsJumpUnlocked()) jumpA = lockedAlpha;
            if (!IsActionUnlocked()) actionA = lockedAlpha;
            if (swapButtonFallbackToMark && !IsSwapUnlocked()) markA = lockedAlpha;
        }

        SetAlpha(jumpVisual, jumpA);
        SetAlpha(actionVisual, actionA);
        SetAlpha(markVisual, markA);
    }

    private void Pulse(Region region)
    {
        if (!pulseOnTrigger) return;

        switch (region)
        {
            case Region.Jump:
                if (jumpVisual != null)
                {
                    if (jumpPulseCo != null) StopCoroutine(jumpPulseCo);
                    jumpPulseCo = StartCoroutine(PulseRoutine(jumpVisual, jumpBaseScale));
                }
                break;
            case Region.Action:
                if (actionVisual != null)
                {
                    if (actionPulseCo != null) StopCoroutine(actionPulseCo);
                    actionPulseCo = StartCoroutine(PulseRoutine(actionVisual, actionBaseScale));
                }
                break;
            case Region.Mark:
                if (markVisual != null)
                {
                    if (markPulseCo != null) StopCoroutine(markPulseCo);
                    markPulseCo = StartCoroutine(PulseRoutine(markVisual, markBaseScale));
                }
                break;
        }
    }

    private IEnumerator PulseRoutine(CanvasGroup cg, Vector3 baseScale)
    {
        Transform t = cg.transform;

        cg.alpha = pressedAlpha;
        t.localScale = baseScale * pulseScale;

        float time = 0f;
        while (time < pulseDuration)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        t.localScale = baseScale;
        ApplyVisuals(heldRegion);
        cg.alpha = Mathf.Clamp01(cg.alpha);
    }

    private static void SetAlpha(CanvasGroup cg, float a)
    {
        if (cg == null) return;
        cg.alpha = Mathf.Clamp01(a);
    }

    private static void SetActiveSafe(RectTransform rt, bool on)
    {
        if (rt == null) return;
        if (rt.gameObject.activeSelf == on) return;
        rt.gameObject.SetActive(on);
    }

    private static void SetActiveSafe(CanvasGroup cg, bool on)
    {
        if (cg == null) return;
        if (cg.gameObject.activeSelf == on) return;
        cg.gameObject.SetActive(on);
    }

    // ----------------- Unlock Gate -----------------

    private bool IsJumpUnlocked() => gate == null || gate.JumpUnlocked;
    private bool IsActionUnlocked() => gate == null || gate.ActionUnlocked;
    private bool IsSwapUnlocked() => gate == null || gate.SwapUnlocked;

    private void LockedFeedback()
    {
        if (lockedFeedbackThisHold) return;
        lockedFeedbackThisHold = true;

        if (failShakeOnLockedPress && CameraShake2D.I != null)
            CameraShake2D.I.ShakeFail();
    }
}
