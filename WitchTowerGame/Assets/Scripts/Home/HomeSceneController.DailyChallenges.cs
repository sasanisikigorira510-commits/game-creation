using UnityEngine;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed partial class HomeSceneController
    {
        private DailyChallengePanel dailyChallengePanel;
        private void EnsureDailyChallengeShortcut(Transform parent)
        {
            if (parent == null || parent.Find("DailyChallengeButton") != null) return;
            var button = CreatePlainButton("DailyChallengeButton", parent, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(330f, HomeFooterContentInset + 505f), new Vector2(330, 84), new Color(.16f, .22f, .34f, .97f), OpenDailyChallengePanel);
            CreateUiText("Label", button.transform, "デイリー試練", 29, FontStyle.Bold, new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(28, 0), new Vector2(260, 72), new Color(1f, .88f, .48f), TextAnchor.MiddleCenter);
            Sprite sprite = Resources.Load<Sprite>("UI/DailyChallenge/TrialStarCore");
            if (sprite != null) CreateMenuImage("TrialStarCore", button.transform, sprite, new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(-116, 0), new Vector2(64, 64), true);
        }

        public void OpenDailyChallengePanel()
        {
            if (!Application.isPlaying || unifiedMenuRoot == null || IsMonsterTrainingPageOpen || SaveManager.Instance?.StorageAccessAvailable == false) return;
            if (dailyChallengePanel == null)
            {
                var root = new GameObject("DailyChallengePanel", typeof(RectTransform));
                root.transform.SetParent(unifiedMenuRoot.transform.parent, false);
                dailyChallengePanel = root.AddComponent<DailyChallengePanel>();
            }
            DailyChallengeSession.ReopenPanel = false;
            SetHomeGuidePanelVisible(false);
            HideHomeTutorialFocus();
            HideUnifiedMenu();
            dailyChallengePanel.Show(OnlinePlayerData.DailyChallenges, GetRuntimeFont(), StartDailyChallenge, ResumeDailyChallenge,
                () =>
                {
                    if (unifiedMenuRoot != null) unifiedMenuRoot.SetActive(true);
                    RefreshHomeGuidance();
                });
            dailyChallengePanel.SetBusy(true);
            OnlinePlayerData.RefreshDailyChallenges((state, error) =>
            {
                if (this == null || dailyChallengePanel == null) return;
                dailyChallengePanel.Refresh(state);
                dailyChallengePanel.SetBusy(false, error);
            });
        }

        private void StartDailyChallenge(string mode)
        {
            if (dailyChallengePanel == null || OnlinePlayerData.Busy) return;
            dailyChallengePanel.SetBusy(true, "入場回数と編成を確認しています…");
            OnlinePlayerData.DailyChallengeStart(mode, (run, error) =>
            {
                if (this == null || dailyChallengePanel == null) return;
                if (error != null || !DailyChallengeSession.Begin(run))
                {
                    dailyChallengePanel.Refresh(OnlinePlayerData.DailyChallenges);
                    dailyChallengePanel.SetBusy(false, error ?? "挑戦記録を開始できません。もう一度確認してください。");
                    return;
                }
                SceneTransitionGuard.LoadScene(battleSceneName);
            });
        }

        private void ResumeDailyChallenge()
        {
            if (dailyChallengePanel == null || OnlinePlayerData.Busy) return;
            dailyChallengePanel.SetBusy(true, "挑戦記録を確認しています…");
            OnlinePlayerData.RefreshDailyChallenges((state, error) =>
            {
                if (this == null || dailyChallengePanel == null) return;
                if (error != null || !DailyChallengeSession.Begin(state?.ActiveRun))
                {
                    dailyChallengePanel.Refresh(state);
                    dailyChallengePanel.SetBusy(false, error ?? "再開できる挑戦はありません。");
                    return;
                }
                SceneTransitionGuard.LoadScene(battleSceneName);
            });
        }
    }
}
