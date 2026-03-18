using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Managers;

namespace WitchTower.Core
{
    public sealed class TitleSceneController : MonoBehaviour
    {
        [SerializeField] private string homeSceneName = "HomeScene";

        public void StartNewGame()
        {
            var defaultSave = Save.PlayerSaveData.CreateDefault();
            SaveManager.Instance.Save(defaultSave);
            GameManager.Instance.InitializeFromSave(defaultSave);
            SceneManager.LoadScene(homeSceneName);
        }

        public void ContinueGame()
        {
            SaveManager.Instance.LoadOrCreate();
            GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            SceneManager.LoadScene(homeSceneName);
        }
    }
}
