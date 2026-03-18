using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Home;
using WitchTower.Managers;

namespace WitchTower.Battle
{
    public sealed class BattleSceneController : MonoBehaviour
    {
        [SerializeField] private string homeSceneName = "HomeScene";
        [SerializeField] private BattleStateMachine stateMachine;

        private int currentFloor;
        private bool resultHandled;
        private BattleRewardResult lastReward;

        private void Start()
        {
            currentFloor = GameManager.Instance.CurrentFloor;
            resultHandled = false;
            lastReward = new BattleRewardResult(0, 0);
            stateMachine.Begin(currentFloor);
        }

        private void Update()
        {
            if (resultHandled)
            {
                return;
            }

            var result = stateMachine.Tick(Time.deltaTime);
            if (result == BattleResult.Win)
            {
                resultHandled = true;
                stateMachine.ShowResult(true);
                OnBattleWin();
            }
            else if (result == BattleResult.Lose)
            {
                resultHandled = true;
                stateMachine.ShowResult(false);
                OnBattleLose();
            }
        }

        public void OnBattleWin()
        {
            ApplyRewards();
            GameManager.Instance.RecordFloorClear(currentFloor);
            MissionService.RecordBattleWin(GameManager.Instance.PlayerProfile);
            MissionService.RecordHighestFloor(GameManager.Instance.PlayerProfile, GameManager.Instance.PlayerProfile.HighestFloor);
            SaveManager.Instance.SaveCurrentGame();
            stateMachine.ShowResultPanel(new BattleResultViewData(true, lastReward.Gold, lastReward.Exp, GameManager.Instance.CurrentFloor));
        }

        public void OnBattleLose()
        {
            SaveManager.Instance.SaveCurrentGame();
            stateMachine.ShowResultPanel(new BattleResultViewData(false, 0, 0, currentFloor));
        }

        public void Retreat()
        {
            SaveManager.Instance.SaveCurrentGame();
            SceneManager.LoadScene(homeSceneName);
        }

        public void GoToNextFloor()
        {
            resultHandled = false;
            currentFloor = GameManager.Instance.CurrentFloor;
            stateMachine.Begin(currentFloor);
        }

        public void ReturnHome()
        {
            SceneManager.LoadScene(homeSceneName);
        }

        public void UseSkillStrike()
        {
            stateMachine.UseSkill(BattleSkillType.Strike);
        }

        public void UseSkillDrain()
        {
            stateMachine.UseSkill(BattleSkillType.Drain);
        }

        public void UseSkillGuard()
        {
            stateMachine.UseSkill(BattleSkillType.Guard);
        }

        private void ApplyRewards()
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null)
            {
                lastReward = new BattleRewardResult(0, 0);
                return;
            }

            var reward = BattleRewardCalculator.Calculate(currentFloor, profile.HighestFloor);
            profile.AddGold(reward.Gold);
            profile.AddExp(reward.Exp);
            lastReward = reward;
        }
    }
}
