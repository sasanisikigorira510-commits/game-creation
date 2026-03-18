using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class EnhancePanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private UpgradeStatusView attackUpgradeView;
        [SerializeField] private UpgradeStatusView defenseUpgradeView;
        [SerializeField] private UpgradeStatusView hpUpgradeView;
        [SerializeField] private int baseUpgradeCost = 10;

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
            var profile = GameManager.Instance.PlayerProfile;
            resourceView.Bind(profile);

            if (profile == null)
            {
                return;
            }

            attackUpgradeView.Bind("Attack", profile.AttackUpgradeLevel, GetUpgradeCost(profile.AttackUpgradeLevel), profile.GetAttackBonus());
            defenseUpgradeView.Bind("Defense", profile.DefenseUpgradeLevel, GetUpgradeCost(profile.DefenseUpgradeLevel), profile.GetDefenseBonus());
            hpUpgradeView.Bind("HP", profile.HpUpgradeLevel, GetUpgradeCost(profile.HpUpgradeLevel), profile.GetHpBonus());
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
            return true;
        }

        private int GetUpgradeCost(int currentLevel)
        {
            return baseUpgradeCost + currentLevel * 5;
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
