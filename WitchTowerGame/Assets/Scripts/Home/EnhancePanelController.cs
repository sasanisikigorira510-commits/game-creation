using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;
using TMPro;
using WitchTower.Battle;

namespace WitchTower.Home
{
    public sealed class EnhancePanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private UpgradeStatusView attackUpgradeView;
        [SerializeField] private UpgradeStatusView defenseUpgradeView;
        [SerializeField] private UpgradeStatusView hpUpgradeView;
        [SerializeField] private int baseUpgradeCost = 10;
        [SerializeField] private TMP_Text ctaText;

        public int BaseUpgradeCost => baseUpgradeCost;

        private void OnEnable()
        {
            Refresh();
        }

        public void UpgradeAttack()
        {
            TryUpgrade(UpgradeType.Attack);
        }

        public void UpgradeDefense()
        {
            TryUpgrade(UpgradeType.Defense);
        }

        public void UpgradeHp()
        {
            TryUpgrade(UpgradeType.Hp);
        }

        public void Refresh()
        {
            var gameManager = GameManager.Instance;
            var profile = gameManager != null ? gameManager.PlayerProfile : null;
            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (profile == null)
            {
                return;
            }

            if (attackUpgradeView != null)
            {
                attackUpgradeView.Bind(
                    "Attack",
                    profile.AttackUpgradeLevel,
                    GetUpgradeCost(profile.AttackUpgradeLevel),
                    profile.GetAttackBonus(),
                    BuildUpgradeImpact(profile, UpgradeType.Attack));
            }

            if (defenseUpgradeView != null)
            {
                defenseUpgradeView.Bind(
                    "Defense",
                    profile.DefenseUpgradeLevel,
                    GetUpgradeCost(profile.DefenseUpgradeLevel),
                    profile.GetDefenseBonus(),
                    BuildUpgradeImpact(profile, UpgradeType.Defense));
            }

            if (hpUpgradeView != null)
            {
                hpUpgradeView.Bind(
                    "HP",
                    profile.HpUpgradeLevel,
                    GetUpgradeCost(profile.HpUpgradeLevel),
                    profile.GetHpBonus(),
                    BuildUpgradeImpact(profile, UpgradeType.Hp));
            }

            if (ctaText != null)
            {
                ctaText.text = HomeActionAdvisor.BuildEnhanceHeadline(profile, baseUpgradeCost);
            }
        }

        private bool TryUpgrade(UpgradeType upgradeType)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null)
            {
                return false;
            }

            var currentLevel = GetCurrentLevel(profile, upgradeType);
            var cost = GetUpgradeCost(currentLevel);
            if (profile.Gold < cost)
            {
                return false;
            }

            profile.Gold -= cost;
            ApplyUpgrade(profile, upgradeType);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
            return true;
        }

        private int GetUpgradeCost(int currentLevel)
        {
            return baseUpgradeCost + currentLevel * 5;
        }

        private static string BuildUpgradeImpact(Data.PlayerProfile profile, UpgradeType upgradeType)
        {
            if (profile == null)
            {
                return "Impact: profile unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats currentStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats upgradedStats = PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, upgradeType);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            string currentThreat = BattleEncounterAdvisor.BuildThreatText(currentStats, enemyStats);
            string upgradedThreat = BattleEncounterAdvisor.BuildThreatText(upgradedStats, enemyStats);

            return upgradeType switch
            {
                UpgradeType.Attack => $"Impact: +3 ATK for floor {nextFloor}, threat {TrimThreat(currentThreat)} -> {TrimThreat(upgradedThreat)}.",
                UpgradeType.Defense => $"Impact: +2 DEF for floor {nextFloor}, threat {TrimThreat(currentThreat)} -> {TrimThreat(upgradedThreat)}.",
                UpgradeType.Hp => $"Impact: +10 HP for floor {nextFloor}, threat {TrimThreat(currentThreat)} -> {TrimThreat(upgradedThreat)}.",
                _ => "Impact: unavailable."
            };
        }

        private static string TrimThreat(string threatLabel)
        {
            return string.IsNullOrEmpty(threatLabel)
                ? "unknown"
                : threatLabel.Replace("Threat: ", string.Empty).Trim().ToLowerInvariant();
        }

        private static int GetCurrentLevel(Data.PlayerProfile profile, UpgradeType upgradeType)
        {
            return upgradeType switch
            {
                UpgradeType.Attack => profile.AttackUpgradeLevel,
                UpgradeType.Defense => profile.DefenseUpgradeLevel,
                UpgradeType.Hp => profile.HpUpgradeLevel,
                _ => 0
            };
        }

        private static void ApplyUpgrade(Data.PlayerProfile profile, UpgradeType upgradeType)
        {
            switch (upgradeType)
            {
                case UpgradeType.Attack:
                    profile.AttackUpgradeLevel += 1;
                    break;
                case UpgradeType.Defense:
                    profile.DefenseUpgradeLevel += 1;
                    break;
                case UpgradeType.Hp:
                    profile.HpUpgradeLevel += 1;
                    break;
            }
        }
    }
}
