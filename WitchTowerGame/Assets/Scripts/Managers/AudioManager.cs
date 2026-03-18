using UnityEngine;

namespace WitchTower.Managers
{
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void PlaySe(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }
        }

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }
        }
    }
}
