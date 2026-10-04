using UnityEngine;
using UnityEngine.EventSystems;

namespace HaoxiKaiyan
{
    /// <summary>One touch/mouse area that drives one virtual stick. Each side owns its own pointer.</summary>
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        [SerializeField] private RectTransform knob;
        [SerializeField, Min(20f)] private float travel = 62f;

        private RectTransform pad;
        private int activePointer = int.MinValue;
        private Vector2 value;
        private Vector2 feedback, tapStart;
        private RectTransform fingerMarker;
        private bool physicalFeedback, tapMoved, doubleTapped;
        private float downAt, lastTapAt = -10f;
        private bool releaseAfterFrame;

        public Vector2 Value => value;

        private void Awake()
        {
            pad = transform as RectTransform;
            if (knob == null && transform.childCount > 0)
                knob = transform.GetChild(0) as RectTransform;
        }

        public void Configure(RectTransform knobTransform, float travelDistance)
        {
            knob = knobTransform;
            travel = travelDistance;
            pad = transform as RectTransform;
        }

        public void UsePhysicalFeedback(RectTransform marker)
        {
            physicalFeedback = true;
            fingerMarker = marker;
            if (fingerMarker != null) fingerMarker.gameObject.SetActive(false);
        }

        public void SetPhysicalFeedback(Vector2 progress) => feedback = Vector2.ClampMagnitude(progress, 1f);

        public bool ConsumeDoubleTap()
        {
            bool result = doubleTapped;
            doubleTapped = false;
            return result;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != int.MinValue)
                return;
            activePointer = eventData.pointerId;
            releaseAfterFrame = false;
            tapStart = eventData.position;
            downAt = Time.unscaledTime;
            tapMoved = false;
            if (fingerMarker != null) fingerMarker.gameObject.SetActive(true);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer || pad == null || knob == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, eventData.position,
                    eventData.pressEventCamera, out Vector2 local))
                return;

            // RectTransform local coordinates start at the pivot, which differs for the two pads.
            Vector2 offset = Vector2.ClampMagnitude(local - pad.rect.center, travel);
            value = offset.magnitude < travel * 0.09f ? Vector2.zero : offset / travel;
            if (Vector2.Distance(eventData.position, tapStart) > travel * .18f ||
                offset.magnitude > travel * .27f) tapMoved = true;
            if (physicalFeedback)
            {
                if (fingerMarker != null) fingerMarker.anchoredPosition = offset;
            }
            else knob.anchoredPosition = offset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer)
                return;

            bool tap = !tapMoved && Time.unscaledTime - downAt < .19f;
            if (tap)
            {
                if (Time.unscaledTime - lastTapAt <= .30f)
                {
                    doubleTapped = true;
                    lastTapAt = -10f;
                }
                else lastTapAt = Time.unscaledTime;
            }
            else lastTapAt = -10f;
            activePointer = int.MinValue;
            releaseAfterFrame = true;
            if (fingerMarker != null) fingerMarker.gameObject.SetActive(false);
            if (!physicalFeedback && knob != null)
                knob.anchoredPosition = Vector2.zero;
        }

        public void OnCancel(BaseEventData eventData) => Release();

        private void OnDisable() => Release();

        private void LateUpdate()
        {
            if (physicalFeedback && knob != null)
            {
                Vector2 destination = activePointer == int.MinValue ? Vector2.zero : feedback * travel;
                knob.anchoredPosition = Vector2.Lerp(knob.anchoredPosition, destination,
                    1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            }
            if (!releaseAfterFrame)
                return;
            value = Vector2.zero;
            releaseAfterFrame = false;
        }

        private void Release()
        {
            activePointer = int.MinValue;
            value = Vector2.zero;
            releaseAfterFrame = false;
            doubleTapped = false;
            lastTapAt = -10f;
            if (fingerMarker != null) fingerMarker.gameObject.SetActive(false);
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
        }
    }

}
