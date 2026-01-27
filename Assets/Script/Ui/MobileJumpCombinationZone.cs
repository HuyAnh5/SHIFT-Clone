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

    [Header("Tutorial Unlocks (by Level)")]
    [Tooltip("If ON: unlocks are based on the maximum level index the player has reached (persisted in PlayerPrefs). If OFF: based only on the current loaded level.")]
    [SerializeField] private bool useMaxLevelReached = false;

    [Tooltip("Jump button is locked until this level index (LV_#). Example: 2 means LV_1 locked, LV_2+ unlocked.")]
    [Min(1)]
    [SerializeField] private int unlockJumpAtLevel = 2;

    [Tooltip("Action button is locked until this level index (LV_#).")]
    [Min(1)]
    [SerializeField] private int unlockActionAtLevel = 3;

    [Tooltip("SWAP (air action / forced swap) is locked until this level index (LV_#). Set 1 to unlock from the start.")]
    [Min(1)]
    [SerializeField] private int unlockSwapAtLevel = 4;

    [Header("Locked Button Visibility")]
    [Tooltip("If ON: locked buttons are hidden/disabled (SetActive(false)). If OFF: locked buttons stay visible and are only dimmed.")]
    [SerializeField] private bool hideLockedButtonsCompletely = true;

    [Tooltip("Optional: the SWAP button UI rect. If not assigned, can fall back to Mark button UI (see below).")]
    [SerializeField] private RectTransform swapArea;

    [Tooltip("Optional: the SWAP button visual. If not assigned, can fall back to Mark button visual (see below).")]
    [SerializeField] private CanvasGroup swapVisual;

    [Tooltip("If swapArea/swapVisual are not set, treat Mark button as the Swap button for visibility locking.")]
    [SerializeField] private bool swapButtonFallbackToMark = true;

    [Tooltip("If swap is locked, force Mark button to always show the Mark icon (never show Swap icon).")]
    [SerializeField] private bool hideSwapIconWhenLocked = true;

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

    // Optional: allow a dedicated Swap button, or reuse Mark button for swap unlocking/visibility.
    private RectTransform SwapAreaRT => swapArea != null ? swapArea : (swapButtonFallbackToMark ? markArea : null);
    private CanvasGroup SwapVisualCG => swapVisual != null ? swapVisual : (swapButtonFallbackToMark ? markVisual : null);

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

    // unlock runtime
    private const string PREF_MAX_LEVEL = "MaxLevelReached";
    private int gateLevelCached = 1;
    private int lastLevelIndexSeen = -1;
    private bool lockedFeedbackThisHold;


    private void Awake()
    {
        if (jumpVisual != null) jumpBaseScale = jumpVisual.transform.localScale;
        if (actionVisual != null) actionBaseScale = actionVisual.transform.localScale;
        if (markVisual != null) markBaseScale = markVisual.transform.localScale;

        if (markSwap == null) markSwap = FindAnyObjectByType<PlayerMarkSwapController>();

        RefreshGateLevel(force: true);

        ApplyLockedVisibility();

        // start in idle
        ApplyVisuals(Region.None);
    }

    private void LateUpdate()
    {
        RefreshGateLevel();
        ApplyLockedVisibility();
        RefreshMarkIcon();
    }

    private void RefreshMarkIcon()
    {
        if (markSwap == null || markIcon == null || iconMark == null || iconSwap == null) return;

        bool showSwap = markSwap.UI_ShouldShowSwapIcon();
        if (hideSwapIconWhenLocked && !IsSwapUnlocked())
            showSwap = false;
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
        lockedFeedbackThisHold = false;

        RefreshGateLevel(force: true);

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
            if (!IsJumpUnlocked())
            {
                LockedFeedback();
                return;
            }

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
            MobileUIInput.TriggerMark();
            Pulse(Region.Mark);
            return;
        }
    }

    private Region GetRegionAtPoint(Vector2 screenPos, Camera cam)
    {
        // Priority: Action > Mark > Jump (in case of overlap)
        if (actionArea != null && actionArea.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(actionArea, screenPos, cam))
            return Region.Action;

        if (markArea != null && markArea.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(markArea, screenPos, cam))
            return Region.Mark;

        if (jumpArea != null && jumpArea.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(jumpArea, screenPos, cam))
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
                if (!IsSwapUnlocked())
                {
                    LockedFeedback();
                    yield break;
                }
                MobileUIInput.TriggerSwap();
                Pulse(Region.Action);
                yield break;
            }

            if (!player.IsGroundedNow)
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
        // If we hide locked buttons completely, also disable their hit regions by disabling their GameObjects.
        if (!hideLockedButtonsCompletely)
        {
            SetActiveSafe(jumpArea, true);
            SetActiveSafe(actionArea, true);
            SetActiveSafe(markArea, true);
            SetActiveSafe(jumpVisual, true);
            SetActiveSafe(actionVisual, true);
            SetActiveSafe(markVisual, true);

            // Dedicated swap button (if any)
            SetActiveSafe(swapArea, true);
            SetActiveSafe(swapVisual, true);
            return;
        }

        bool jumpOn = IsJumpUnlocked();
        bool actionOn = IsActionUnlocked();
        bool swapOn = IsSwapUnlocked();

        // Jump
        SetActiveSafe(jumpArea, jumpOn);
        SetActiveSafe(jumpVisual, jumpOn);

        // Action
        SetActiveSafe(actionArea, actionOn);
        SetActiveSafe(actionVisual, actionOn);

        // Swap button visibility: use dedicated swap UI if assigned; otherwise optionally reuse Mark button.
        var swapRT = SwapAreaRT;
        var swapCG = SwapVisualCG;
        if (swapRT != null) SetActiveSafe(swapRT, swapOn);
        if (swapCG != null) SetActiveSafe(swapCG, swapOn);

        // If we are NOT reusing Mark as Swap, keep Mark always visible.
        if (!swapButtonFallbackToMark)
        {
            SetActiveSafe(markArea, true);
            SetActiveSafe(markVisual, true);
        }
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

    private void ApplyVisuals(Region region)
    {
        // exactly ONE highlighted. If None -> all idle
        float jumpA = region == Region.Jump ? pressedAlpha : idleAlpha;
        float actionA = region == Region.Action ? pressedAlpha : idleAlpha;
        float markA = region == Region.Mark ? pressedAlpha : idleAlpha;

        if (!hideLockedButtonsCompletely && dimLockedButtons)
        {
            if (!IsJumpUnlocked()) jumpA = lockedAlpha;
            if (!IsActionUnlocked()) actionA = lockedAlpha;
            // If Mark button is being used as Swap button, dim it too when Swap is locked.
            if (swapButtonFallbackToMark && !IsSwapUnlocked()) markA = lockedAlpha;
            // Mark itself is not locked; Swap-lock only affects what Action does and the Mark icon.
        }

        SetAlpha(jumpVisual, jumpA);
        SetAlpha(actionVisual, actionA);
        SetAlpha(markVisual, markA);
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

    // ----------------- Unlock gating -----------------

    private void RefreshGateLevel(bool force = false)
    {
        int currentLevel = 1;
        if (LevelManager.I != null)
            currentLevel = Mathf.Max(1, LevelManager.I.CurrentLevelIndex);

        if (!force && currentLevel == lastLevelIndexSeen) return;
        lastLevelIndexSeen = currentLevel;

        if (!useMaxLevelReached)
        {
            gateLevelCached = currentLevel;
            return;
        }

        int max = PlayerPrefs.GetInt(PREF_MAX_LEVEL, currentLevel);
        if (currentLevel > max)
        {
            max = currentLevel;
            PlayerPrefs.SetInt(PREF_MAX_LEVEL, max);
        }
        gateLevelCached = Mathf.Max(1, max);
    }

    private bool IsJumpUnlocked() => gateLevelCached >= unlockJumpAtLevel;
    private bool IsActionUnlocked() => gateLevelCached >= unlockActionAtLevel;
    private bool IsSwapUnlocked() => gateLevelCached >= unlockSwapAtLevel;

    private void LockedFeedback()
    {
        if (lockedFeedbackThisHold) return;
        lockedFeedbackThisHold = true;

        if (failShakeOnLockedPress && CameraShake2D.I != null)
            CameraShake2D.I.ShakeFail();
    }
}