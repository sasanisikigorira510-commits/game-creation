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
        [SerializeField] private float textFloatDistance = 28f;
        [SerializeField] private float flashFadeSpeed = 5f;

        private float playerTextRemaining;
        private float enemyTextRemaining;
        private RectTransform playerDamageRect;
        private RectTransform enemyDamageRect;
        private Vector2 playerDamageBasePosition;
        private Vector2 enemyDamageBasePosition;

        private void Awake()
        {
            CacheTextDefaults();
        }

        private void Update()
        {
            TickText(ref playerTextRemaining, playerDamageText, playerDamageRect, playerDamageBasePosition);
            TickText(ref enemyTextRemaining, enemyDamageText, enemyDamageRect, enemyDamageBasePosition);
            TickFlash(playerFlashGroup);
            TickFlash(enemyFlashGroup);
        }

        public void ShowHit(BattleHitInfo hitInfo)
        {
            CacheTextDefaults();

            var targetText = hitInfo.TargetIsPlayer ? playerDamageText : enemyDamageText;
            var flashGroup = hitInfo.TargetIsPlayer ? playerFlashGroup : enemyFlashGroup;
            var targetRect = hitInfo.TargetIsPlayer ? playerDamageRect : enemyDamageRect;
            var basePosition = hitInfo.TargetIsPlayer ? playerDamageBasePosition : enemyDamageBasePosition;

            if (targetText != null)
            {
                targetText.text = hitInfo.IsCritical ? $"CRIT {hitInfo.Damage}" : hitInfo.Damage.ToString();
                targetText.color = hitInfo.IsCritical
                    ? new Color(1f, 0.55f, 0.35f, 1f)
                    : (hitInfo.IsSkill ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 1f, 1f, 1f));
                targetText.gameObject.SetActive(true);
            }

            if (targetRect != null)
            {
                targetRect.anchoredPosition = basePosition;
                targetRect.localScale = hitInfo.IsCritical ? Vector3.one * 1.15f : Vector3.one;
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

        private void TickText(ref float remaining, TMP_Text targetText, RectTransform targetRect, Vector2 basePosition)
        {
            if (targetText == null)
            {
                return;
            }

            if (remaining <= 0f)
            {
                targetText.gameObject.SetActive(false);
                ResetTextVisual(targetText, targetRect, basePosition);
                return;
            }

            float normalized = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, textDuration));
            if (targetRect != null)
            {
                targetRect.anchoredPosition = basePosition + Vector2.up * (textFloatDistance * normalized);
                targetRect.localScale = Vector3.Lerp(targetRect.localScale, Vector3.one, Time.deltaTime * 12f);
            }

            var color = targetText.color;
            color.a = 1f - normalized;
            targetText.color = color;
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

        private void CacheTextDefaults()
        {
            if (playerDamageText != null && playerDamageRect == null)
            {
                playerDamageRect = playerDamageText.rectTransform;
                playerDamageBasePosition = playerDamageRect.anchoredPosition;
            }

            if (enemyDamageText != null && enemyDamageRect == null)
            {
                enemyDamageRect = enemyDamageText.rectTransform;
                enemyDamageBasePosition = enemyDamageRect.anchoredPosition;
            }
        }

        private static void ResetTextVisual(TMP_Text targetText, RectTransform targetRect, Vector2 basePosition)
        {
            if (targetRect != null)
            {
                targetRect.anchoredPosition = basePosition;
                targetRect.localScale = Vector3.one;
            }

            var color = targetText.color;
            color.a = 1f;
            targetText.color = color;
        }
    }
}
