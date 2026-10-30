using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Card highlight and pointer-hover selection. Deliberately implements only these three handlers: an EventTrigger
    /// implements the scroll and drag handlers too and swallows them, so the mouse wheel stops working over cards
    /// (probe P4, docs/infomation/22 section 6). Callbacks are wrapped so an exception never reaches the EventSystem.
    /// </summary>
    internal sealed class CardHover : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Action OnSelected;
        public Action OnDeselected;
        public Action OnEnter;

        public void OnSelect(BaseEventData eventData) => Run(OnSelected);

        public void OnDeselect(BaseEventData eventData) => Run(OnDeselected);

        public void OnPointerEnter(PointerEventData eventData) => Run(OnEnter);

        private static void Run(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception) { }
        }
    }
}
