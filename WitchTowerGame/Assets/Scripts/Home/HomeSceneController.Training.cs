using UnityEngine;
using UnityEngine.UI;
using WitchTower.Managers;

namespace WitchTower.Home
{
    public sealed partial class HomeSceneController
    {
        private HomeMonsterTrainingPanel monsterTrainingPanel;
        private bool IsMonsterTrainingPageOpen => monsterTrainingPanel != null && monsterTrainingPanel.IsVisible;

        private void EnsureMonsterTrainingShortcut(Transform parent)
        {
            if (parent == null || parent.Find("TrainingButton") != null) return;
            var button = CreatePlainButton("TrainingButton", parent, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(-330, HomeFooterContentInset + 505f), new Vector2(350, 112), Color.white, OpenMonsterTrainingPage);
            var image = button.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/HomeMenu/TrainingHomeButtonImage2");
            image.preserveAspect = true;
            CreateUiText("Label", button.transform, "修練", 34, FontStyle.Bold, Vector2.one * .5f,
                Vector2.one * .5f, new Vector2(28, 0), new Vector2(220, 76), new Color(1, .91f, .65f), TextAnchor.MiddleCenter);
        }

        public void OpenMonsterTrainingPage()
        {
            if (!Application.isPlaying || unifiedMenuRoot == null || dailyChallengePanel?.IsVisible == true || SaveManager.Instance?.StorageAccessAvailable == false ||
                GameManager.Instance?.PlayerProfile == null) return;
            if (monsterTrainingPanel == null)
            {
                var root = new GameObject("HomeMonsterTrainingPanel", typeof(RectTransform));
                root.transform.SetParent(unifiedMenuRoot.transform.parent, false);
                monsterTrainingPanel = root.AddComponent<HomeMonsterTrainingPanel>();
            }
            SetHomeGuidePanelVisible(false);
            HideHomeTutorialFocus();
            HideUnifiedMenu();
            monsterTrainingPanel.Show(GetRuntimeFont(), () =>
            {
                if (unifiedMenuRoot != null) unifiedMenuRoot.SetActive(true);
                RefreshAllPanels();
                RefreshHomeGuidance();
            });
        }
    }
}
