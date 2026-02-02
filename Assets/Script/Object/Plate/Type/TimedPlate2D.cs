using UnityEngine;
using UnityEngine.UI;

public class TimedPlate2D : PlateBase2D
{
    [Header("Timed")]
    [SerializeField] private float countdownDuration = 10f;

    [Header("Start rule")]
    [Tooltip("Tắt = rời khỏi là đếm luôn. Bật = chỉ đếm nếu đã chìm hết (đứng đủ lâu để press).")]
    [SerializeField] private bool requireFullyPressedToStartCountdown = false;

    [Header("Ring UI (BG xám + FG xanh)")]
    [SerializeField] private GameObject ringRoot;      // parent chứa BG + FG
    [SerializeField] private Canvas ringCanvas;        // Canvas world-space
    [SerializeField] private Image ringFG;             // FG: Image Filled Radial 360

    [Header("Ring Sorting (render đè lên Tilemap)")]
    [SerializeField] private bool applyRingSortingOnAwake = true;
    [SerializeField] private string ringSortingLayer = "UI";
    [SerializeField] private int ringSortingOrder = 5000;
    [SerializeField] private bool forceOverrideSorting = true;

    [Header("Optional: auto-config ring fill")]
    [SerializeField] private bool autoConfigureRingFill = true;
    [Tooltip("CCW = ngược kim đồng hồ (Zelda stamina style)")]
    [SerializeField] private bool ringFillCCW = true;

    // Persisted countdown state (KHÔNG reset khi SHIFT)
    private bool armed;       // đã từng bị đứng lên (trong cycle hiện tại)
    private bool counting;    // đang đếm
    private bool expired;     // hết giờ (ring ẩn) cho tới khi đứng lên lại
    private float endTime;    // Time.time khi countdown kết thúc

    private Vector2 countdownStartPos;

    private float Remaining => Mathf.Max(0f, endTime - Time.time);

    protected override void Awake()
    {
        base.Awake();
        CacheRingRefs();
        if (applyRingSortingOnAwake) ApplyRingSorting();
        RefreshRingVisibilityAndFill(forceFullWhenVisible: false);
    }

    private void Update()
    {
        // Safety net: nếu vì bất kỳ lý do gì countdown đã start mà sau đó latch SwapBlock được xác nhận
        // -> cancel countdown ngay, ring FULL (khi active world), và giữ ON.
        if (counting && IsSwapBlockLatched)
        {
            counting = false;
            expired = false;
            armed = true;

            SetOn(true);
            RecomputePressedPos();
            rb.position = pressedPos;

            if (activeInWorld)
            {
                SetRingVisible(true);
                SetRingFill(1f);
            }
            else
            {
                SetRingVisible(false);
            }
            return;
        }

        if (!counting) return;

        float dur = Mathf.Max(0.001f, countdownDuration);
        float remain = Remaining;
        float norm = Mathf.Clamp01(remain / dur); // 1 -> 0

        // Ring chỉ hiển thị ở OWNER world (activeInWorld).
        if (activeInWorld)
        {
            SetRingVisible(true);
            SetRingFill(norm);
        }
        else
        {
            SetRingVisible(false);
        }

        // Plate trồi lên theo progress (không DOTween)
        float t = 1f - norm; // 0->1
        rb.position = Vector2.Lerp(countdownStartPos, basePos, t);

        // Hết giờ
        if (remain <= 0f)
        {
            counting = false;
            expired = true;
            armed = false;

            SetOn(false);
            SetRingVisible(false);

            rb.position = basePos;
        }
    }

    protected override void OnWorldActiveChanged(bool isActive)
    {
        // TimedPlate: countdown vẫn chạy qua SHIFT, nhưng ring chỉ render ở OWNER world.
        if (!isActive)
        {
            SetRingVisible(false);
            return;
        }

        RefreshRingVisibilityAndFill(forceFullWhenVisible: false);
    }

    protected override void OnBecameInactiveWorld()
    {
        PlateDbg($"OnBecameInactiveWorld: hadPlayer={deactivatedHadPlayer} hadSwap={deactivatedHadSwapBlock} latched={IsSwapBlockLatched} counting={counting}");

        KillAllTweens();

        // Khi inactive world: vẫn update state, nhưng KHÔNG render ring.
        SetRingVisible(false);

        // SwapBlock đè => giữ ON, KHÔNG countdown (ring sẽ FULL khi quay lại đúng world)
        if (IsSwapBlockLatched || deactivatedHadSwapBlock)
        {
            counting = false;
            expired = false;
            armed = true;

            SetOn(true);
            // ring hidden while inactive
            SetRingFill(1f);

            RecomputePressedPos();
            rb.position = pressedPos;

            PlateDbg("InactiveWorld -> SwapBlock HOLD: cancel countdown, ring full.");
            return;
        }

        // Player đè mà SHIFT => coi như rời => start countdown ngay
        if (deactivatedHadPlayer)
        {
            if (!counting && !expired)
            {
                armed = true;
                StartCountdownFromCurrentPos();
                PlateDbg("InactiveWorld -> Player left by SHIFT: start countdown.");
            }
        }
        // ring remains hidden while inactive; will be refreshed on re-activation.
    }

    protected override void OnOccupancyChanged()
    {
        if (!activeInWorld) return;

        PlateDbg($"OnOccupancyChanged: hasOcc={HasOccupant} latched={IsSwapBlockLatched} counting={counting} expired={expired} armed={armed}");

        // Có occupant: FULL
        if (HasOccupant)
        {
            expired = false;
            armed = true;
            counting = false;

            SetOn(true);
            SetRingVisible(true);
            SetRingFill(1f);

            TweenPressToBottom();
            return;
        }

        // Latch SwapBlock: FULL, KHÔNG countdown
        if (IsSwapBlockLatched)
        {
            expired = false;
            armed = true;
            counting = false;

            SetOn(true);
            SetRingVisible(true);
            SetRingFill(1f);

            RecomputePressedPos();
            rb.position = pressedPos;

            PlateDbg("Latch detected while active -> force HOLD (cancel countdown).");
            return;
        }

        if (counting) return;

        if (expired)
        {
            SetOn(false);
            SetRingVisible(false);
            TweenRaiseToTop(raiseDuration);
            return;
        }

        if (!armed)
        {
            SetOn(false);
            SetRingVisible(false);
            TweenRaiseToTop(raiseDuration);
            return;
        }

        // Start countdown when leaving after armed
        RecomputePressedPos();

        if (requireFullyPressedToStartCountdown && !IsFullyPressed())
        {
            armed = false;
            SetOn(false);
            SetRingVisible(false);
            TweenRaiseToTop(raiseDuration);
            return;
        }

        StartCountdownFromCurrentPos();
        PlateDbg("Leave -> start countdown.");
    }

    private void StartCountdownFromCurrentPos()
    {
        counting = true;
        expired = false;

        float dur = Mathf.Max(0.001f, countdownDuration);
        endTime = Time.time + dur;
        countdownStartPos = rb.position;

        // TimedPlate counting cũng coi là ON (để Exit tính)
        SetOn(true);

        SetRingVisible(true);
        SetRingFill(1f);
    }

    private void CacheRingRefs()
    {
        if (ringRoot == null)
        {
            // try auto find
            var t = transform.Find("Ring") ?? transform.Find("RingUI") ?? transform.Find("Canvas");
            if (t != null) ringRoot = t.gameObject;
        }

        if (ringCanvas == null && ringRoot != null)
            ringCanvas = ringRoot.GetComponentInChildren<Canvas>(true);

        if (ringFG == null && ringRoot != null)
        {
            // try find by name contains "FG"
            var imgs = ringRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                if (imgs[i] != null && imgs[i].name.ToLower().Contains("fg"))
                {
                    ringFG = imgs[i];
                    break;
                }
            }
            // fallback: first Image
            if (ringFG == null && imgs.Length > 0) ringFG = imgs[0];
        }

        if (autoConfigureRingFill && ringFG != null)
        {
            ringFG.type = Image.Type.Filled;
            ringFG.fillMethod = Image.FillMethod.Radial360;
            ringFG.fillOrigin = (int)Image.Origin360.Top;
            ringFG.fillClockwise = !ringFillCCW;
        }
    }

    private void ApplyRingSorting()
    {
        if (ringCanvas == null) return;

        if (forceOverrideSorting) ringCanvas.overrideSorting = true;

        ringCanvas.sortingLayerName = ringSortingLayer;
        ringCanvas.sortingOrder = ringSortingOrder;
    }

    private void RefreshRingVisibilityAndFill(bool forceFullWhenVisible)
    {
        if (!activeInWorld)
        {
            SetRingVisible(false);
            return;
        }

        bool shouldShow = false;

        if (counting) shouldShow = true;
        else if (expired) shouldShow = false;
        else if (!armed) shouldShow = false;
        else
        {
            shouldShow = (activeInWorld && HasOccupant) || IsSwapBlockLatched;
        }

        SetRingVisible(shouldShow);
        if (!shouldShow) return;

        if (forceFullWhenVisible)
        {
            SetRingFill(1f);
        }
        else
        {
            if (counting)
            {
                float dur = Mathf.Max(0.001f, countdownDuration);
                SetRingFill(Mathf.Clamp01(Remaining / dur));
            }
            else
            {
                SetRingFill(1f);
            }
        }
    }

    private void SetRingVisible(bool visible)
    {
        if (ringRoot != null && ringRoot.activeSelf != visible)
            ringRoot.SetActive(visible);
    }

    private void SetRingFill(float normalizedRemain)
    {
        if (ringFG != null)
            ringFG.fillAmount = Mathf.Clamp01(normalizedRemain);
    }
}
