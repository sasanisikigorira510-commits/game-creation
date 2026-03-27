using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchTower.Battle
{
    public static class BattleSceneRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Scenes/BattleScene.unity")
            {
                return;
            }

            var controller = Object.FindObjectOfType<BattleSceneController>(true);
            if (controller == null)
            {
                Debug.LogWarning("[BattleSceneRuntimeBootstrap] BattleSceneController not found after scene load.");
                return;
            }

            controller.InitializeForSceneLoad();
        }
    }
}
