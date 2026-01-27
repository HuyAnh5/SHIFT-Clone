using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// One-finger Jump + slide-to Action/Mark helper.
///
/// Works in two styles:
/// (A) One Zone (recommended): Attach to a parent RectTransform that covers Jump+Action+Mark.
///     - Parent has Image (alpha 0) RaycastTarget ON.
///     - BtnJump/BtnAction/BtnMark are VISUAL ONLY: their Image.raycastTarget OFF (or CanvasGroup BlocksRaycasts OFF).
///     - Assign jumpArea/actionArea/markArea to those button RectTransforms.
///     - Assign jumpVisual/actionVisual/markVisual (CanvasGroup) for highlight feedback.
///
/// (B) Helper on BtnJump: Attach to BtnJump so BtnAction/BtnMark can still be independent.
///     - If cancelWhenExitZone is ON, set dragZoneOverride to a parent that covers Jump+Action+Mark.
///
/// Requires a PlayerController reference to read IsGroundedNow.
/// This script only sets flags in MobileUIInput; PlayerController/PlayerMarkSwapController must consume them.
/// </summary>
public class MobileJumpCombinationZone : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler
{
    private enum Region { None, Jump, Action, Mark }

    [Header("Refs")]
    [SerializeField] private PlayerController player;
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
    [Tooltip("CanvasGroup for Jump button visual. Alpha will be controlled by this script.")]
    [SerializeField] private CanvasGroup jumpVisual;

    [Tooltip("CanvasGroup for Action button visual. Alpha will be controlled by this script.")]
    [SerializeField] private CanvasGroup actionVisual;

    [Tooltip("CanvasGroup for Mark button visual. Alpha will be controlled by this script.")]
    [SerializeField] private CanvasGroup markVisual;

    [SerializeField] private float idleAlpha = 0.25f;
    [SerializeField] private float pressedAlpha = 1f;

    [Tooltip("When an action is actually triggered, do a quick pulse (alpha+scale) for confirmation.")]
    [SerializeField] private bool pulseOnTrigger = true;

    [SerializeField] private float pulseDuration = 0.12f;
    [SerializeField] private float pulseScale = 1.06f;

    // --- runtime ---
    private int activePointerId = int.MinValue;
    private bool jumpTriggeredThisHold;
    private bool secondaryTriggeredThisHold; // Action OR Mark (first one wins)
    private Coroutine waitAirborneCo;

    private Region heldRegion = Region.None;

    private RectTransform ZoneRT => dragZoneOverride != null ? dragZoneOverride : (RectTransform)transform;

    // scale cache for pulses
    private Vector3 jumpBaseScale = Vector3.one;
    private Vector3 actionBaseScale = Vector3.one;
    private Vector3 markBaseScale = Vector3.one;

    private Coroutine jumpPulseCo;
    private Coroutine actionPulseCo;
    private Coroutine markPulseCo;

    [Header("Mark Icon Swap (optional)")]
    [SerializeField] private PlayerMarkSwapController markSwap;
    [SerializeField] private Image markIcon;
    [SerializeField] private Sprite iconMark;
    [SerializeField] private Sprite iconSwap;

    private bool lastShowSwap;


    private void Awake()
    {
        if (jumpVisual != null) jumpBaseScale = jumpVisual.transform.localScale;
        if (actionVisual != null) actionBaseScale = actionVisual.transform.localScale;
        if (markVisual != null) markBaseScale = markVisual.transform.localScale;

        if (markSwap == null) markSwap = FindAnyObjectByType<PlayerMarkSwapController>();

        // start in idle
        ApplyVisuals(Region.None);
    }

    private void LateUpdate()
    {
        RefreshMarkIcon();
    }

    private void RefreshMarkIcon()
    {
        if (markSwap == null || markIcon == null || iconMark == null || iconSwap == null) return;

        bool showSwap = markSwap.UI_ShouldShowSwapIcon();
        if (showSwap == lastShowSwap) return;

        markIcon.sprite = showSwap ? iconSwap : iconMark;
        lastShowSwap = showSwap;
    }


    // Make dragging start immediately (no drag threshold) so sliding feels responsive.
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

        // Update held highlight purely by finger position (not by what got triggered)
        Region r = GetRegionAtPoint(e.position, cam);
        if (r != heldRegion)
        {
            heldRegion = r;
            ApplyVisuals(heldRegion);
        }

        // --- Trigger logic ---

        // 1) Jump: trigger once per hold (only when grounded)
        if (!jumpTriggeredThisHold && r == Region.Jump)
        {
            MobileUIInput.TriggerJump(); // lu�n cho v�o buffer
            jumpTriggeredThisHold = true;
            Pulse(Region.Jump);
            return;
        }


        // 2) Secondary: only allow ONE of {Action, Mark} per hold (first one wins)
        if (secondaryTriggeredThisHold) return;

        if (r == Region.Action)
        {
            secondaryTriggeredThisHold = true;

            if (slideAfterJumpForcesSwap && jumpTriggeredThisHold)
                StartWaitAirborneThenSwap();
            else
                TriggerActionImmediate();

            return;
        }

        if (r == Region.Mark)
        {
            secondaryTriggeredThisHold = true;
            MobileUIInput.TriggerMark();
            Pulse(Region.Mark);
            return;
        }
    }

    private Region GetRegionAtPoint(Vector2 screenPos, Camera cam)
    {
        // Priority: Action > Mark > Jump (in case of overlap)
        if (actionArea != null && RectTransformUtility.RectangleContainsScreenPoint(actionArea, screenPos, cam))
            return Region.Action;

        if (markArea != null && RectTransformUtility.RectangleContainsScreenPoint(markArea, screenPos, cam))
            return Region.Mark;

        if (jumpArea != null && RectTransformUtility.RectangleContainsScreenPoint(jumpArea, screenPos, cam))
            return Region.Jump;

        return Region.None;
    }

    private void TriggerActionImmediate()
    {
        if (player != null && player.IsGroundedNow)
        {
            MobileUIInput.TriggerShift();
            Pulse(Region.Action);
        }
        else
        {
            MobileUIInput.TriggerSwap();
            Pulse(Region.Action);
        }
    }

    private void StartWaitAirborneThenSwap()
    {
        if (waitAirborneCo != null)
            StopCoroutine(waitAirborneCo);

        waitAirborneCo = StartCoroutine(WaitUntilAirborneThenSwap());

        // While waiting, keep Action highlighted (already done by heldRegion) and only pulse when swap actually fires.
    }

    private IEnumerator WaitUntilAirborneThenSwap()
    {
        float t = 0f;
        while (t < waitAirborneTimeout)
        {
            if (player == null)
            {
                MobileUIInput.TriggerSwap();
                Pulse(Region.Action);
                yield break;
            }

            if (!player.IsGroundedNow)
            {
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

        if (waitAirborneCo != null)
        {
            StopCoroutine(waitAirborneCo);
            waitAirborneCo = null;
        }

        heldRegion = Region.None;
        ApplyVisuals(Region.None);
    }

    // ----------------- Visuals -----------------

    private void ApplyVisuals(Region region)
    {
        // exactly ONE highlighted. If None -> all idle
        SetAlpha(jumpVisual, region == Region.Jump ? pressedAlpha : idleAlpha);
        SetAlpha(actionVisual, region == Region.Action ? pressedAlpha : idleAlpha);
        SetAlpha(markVisual, region == Region.Mark ? pressedAlpha : idleAlpha);
    }

    private static void SetAlpha(CanvasGroup cg, float a)
    {
        if (cg == null) return;
        cg.alpha = Mathf.Clamp01(a);
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
        // quick "pop" confirmation, then return to held/idle state
        Transform t = cg.transform;

        float prevAlpha = cg.alpha;
        Vector3 prevScale = t.localScale;

        cg.alpha = pressedAlpha;
        t.localScale = baseScale * pulseScale;

        float time = 0f;
        while (time < pulseDuration)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        // restore scale, and alpha according to current heldRegion
        t.localScale = baseScale;

        // alpha is driven by ApplyVisuals; re-apply to avoid desync
        ApplyVisuals(heldRegion);

        // if someone changed alpha externally, don't leave it in a weird state
        cg.alpha = Mathf.Clamp01(cg.alpha);
    }
}