using TMPro;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed class BattleFeedbackController : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerDamageText;
        [SerializeField] private TMP_Text enemyDamageText;
        [SerializeField] private CanvasGroup playerFlashGroup;
        [SerializeField] private CanvasGroup enemyFlashGroup;
        [SerializeField] private float textDuration = 0.45f;
        [SerializeField] private float flashFadeSpeed = 5f;

        private float playerTextRemaining;
        private float enemyTextRemaining;

        private void Update()
        {
            TickText(ref playerTextRemaining, playerDamageText);
            TickText(ref enemyTextRemaining, enemyDamageText);
            TickFlash(playerFlashGroup);
            TickFlash(enemyFlashGroup);
        }

        public void ShowHit(BattleHitInfo hitInfo)
        {
            var targetText = hitInfo.TargetIsPlayer ? playerDamageText : enemyDamageText;
            var flashGroup = hitInfo.TargetIsPlayer ? playerFlashGroup : enemyFlashGroup;

            if (targetText != null)
            {
                targetText.text = hitInfo.IsCritical ? $"CRIT {hitInfo.Damage}" : hitInfo.Damage.ToString();
                targetText.color = hitInfo.IsSkill ? new Color(1f, 0.85f, 0.35f) : Color.white;
                targetText.gameObject.SetActive(true);
            }

            if (hitInfo.TargetIsPlayer)
            {
                playerTextRemaining = textDuration;
            }
            else
            {
                enemyTextRemaining = textDuration;
            }

            if (flashGroup != null)
            {
                flashGroup.alpha = 0.8f;
            }
        }

        private void TickText(ref float remaining, TMP_Text targetText)
        {
            if (remaining <= 0f)
            {
                if (targetText != null)
                {
                    targetText.gameObject.SetActive(false);
                }

                return;
            }

            remaining -= Time.deltaTime;
        }

        private void TickFlash(CanvasGroup flashGroup)
        {
            if (flashGroup == null || flashGroup.alpha <= 0f)
            {
                return;
            }

            flashGroup.alpha = Mathf.Max(0f, flashGroup.alpha - Time.deltaTime * flashFadeSpeed);
        }
    }
}
