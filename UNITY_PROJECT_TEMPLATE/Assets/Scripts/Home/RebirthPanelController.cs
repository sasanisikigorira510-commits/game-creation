using TMPro;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class RebirthPanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private RebirthSkillStatusView[] skillViews;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (summaryText != null)
            {
                summaryText.text = BuildSummary(profile);
            }

            if (skillViews == null)
            {
                return;
            }

            foreach (var skillView in skillViews)
            {
                if (skillView != null)
                {
                    skillView.Bind(profile);
                }
            }
        }

        public void Rebirth()
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (!RebirthService.TryRebirth(profile, out _))
            {
                Refresh();
                return;
            }

            GameManager.Instance.SetCurrentFloor(1);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
        }

        public void PurchaseSkill(string skillId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (!RebirthService.TryPurchaseSkill(profile, skillId, out _))
            {
                Refresh();
                return;
            }

            SaveManager.Instance.SaveCurrentGame();
            Refresh();
        }

        public void PurchaseAttackPact()
        {
            PurchaseSkill(RebirthSkillCatalog.AttackPactId);
        }

        public void PurchaseHpOath()
        {
            PurchaseSkill(RebirthSkillCatalog.HpOathId);
        }

        public void PurchaseExpMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.ExpMemoryId);
        }

        public void PurchaseCriticalMark()
        {
            PurchaseSkill(RebirthSkillCatalog.CriticalMarkId);
        }

        public void PurchaseDefenseOath()
        {
            PurchaseSkill(RebirthSkillCatalog.DefenseOathId);
        }

        public void PurchaseGoldMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.GoldMemoryId);
        }

        public void PurchaseStrikeMastery()
        {
            PurchaseSkill(RebirthSkillCatalog.StrikeMasteryId);
        }

        public void PurchaseDrainMastery()
        {
            PurchaseSkill(RebirthSkillCatalog.DrainMasteryId);
        }

        public void PurchaseTempoMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.TempoMemoryId);
        }

        private static string BuildSummary(PlayerProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            var reward = profile.GetPendingRebirthPointReward();
            var rebirthStatus = reward > 0
                ? $"Ready: +{reward} Soul"
                : $"Unlocks at Lv. {RebirthService.MinimumLevel}";

            return $"Soul {profile.RebirthPoints} / Total {profile.TotalRebirthPoints} / Rebirths {profile.RebirthCount}\n{rebirthStatus}";
        }
    }
}
