using System;
using UnityEngine;
namespace WitchTower.Home
{
    // Compatibility component for scenes authored before the soul tree was removed.
    public sealed class RebirthPanelController : MonoBehaviour
    {
        private void Awake() { gameObject.SetActive(false); }
        public void Show(Action onClose = null) { gameObject.SetActive(false); onClose?.Invoke(); }
        public void RefreshView() { }
    }
}
