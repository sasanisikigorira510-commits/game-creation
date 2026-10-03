using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchTower.UI
{
    /// <summary>Requires a new press after the screen-opening touch has ended.</summary>
    public sealed class FreshPressButton : Button
    {
        private bool readyForPress;
        private int? pressedPointer;

        protected override void OnEnable()
        {
            base.OnEnable();
            readyForPress = false;
            pressedPointer = null;
        }

        protected override void OnDisable()
        {
            readyForPress = false;
            pressedPointer = null;
            base.OnDisable();
        }

        private void LateUpdate()
        {
            // Wait for a neutral input frame, including when returning from
            // results. A held touch must not become a press on a new button.
            if (!Input.GetMouseButton(0) && Input.touchCount == 0)
            {
                readyForPress = true;
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            pressedPointer = null;
            if (!readyForPress || !IsActive() || !IsInteractable() ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            pressedPointer = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            bool accepted = pressedPointer == eventData.pointerId;
            pressedPointer = null;
            if (accepted)
            {
                base.OnPointerClick(eventData);
            }
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (readyForPress)
            {
                base.OnSubmit(eventData);
            }
        }
    }
}
