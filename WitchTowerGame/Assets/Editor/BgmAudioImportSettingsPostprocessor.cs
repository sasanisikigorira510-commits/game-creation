using UnityEditor;
using UnityEngine;

namespace WitchTower.EditorTools
{
    public sealed class BgmAudioImportSettingsPostprocessor : AssetPostprocessor
    {
        private const string BgmResourcePathPrefix = "Assets/Resources/Audio/BGM/";
        private const float BgmVorbisQuality = 0.62f;

        private void OnPreprocessAudio()
        {
            ApplySettings(assetImporter as AudioImporter, assetPath);
        }

        [MenuItem("WitchTower/Audio/Apply BGM Import Settings")]
        private static void ApplyAllBgmSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio/BGM" });
            int changedCount = 0;
            for (int i = 0; i < guids.Length; i += 1)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!ApplySettings(importer, path))
                {
                    continue;
                }

                changedCount += 1;
                importer.SaveAndReimport();
            }

            Debug.Log($"[BgmAudioImportSettings] Applied mobile-friendly BGM import settings to {changedCount} clip(s).");
        }

        private static bool ApplySettings(AudioImporter importer, string path)
        {
            if (importer == null || string.IsNullOrEmpty(path) || !path.StartsWith(BgmResourcePathPrefix))
            {
                return false;
            }

            bool changed = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            changed |= SetIfDifferent(ref settings.loadType, AudioClipLoadType.Streaming);
            changed |= SetIfDifferent(ref settings.compressionFormat, AudioCompressionFormat.Vorbis);
            changed |= SetIfDifferent(ref settings.sampleRateSetting, AudioSampleRateSetting.PreserveSampleRate);
            changed |= SetIfDifferent(ref settings.quality, BgmVorbisQuality);
            changed |= SetIfDifferent(ref settings.preloadAudioData, false);

            if (changed)
            {
                importer.defaultSampleSettings = settings;
            }

            if (importer.loadInBackground != true)
            {
                importer.loadInBackground = true;
                changed = true;
            }

            if (importer.forceToMono != false)
            {
                importer.forceToMono = false;
                changed = true;
            }

            return changed;
        }

        private static bool SetIfDifferent<T>(ref T value, T next)
        {
            if (Equals(value, next))
            {
                return false;
            }

            value = next;
            return true;
        }

        private static bool SetIfDifferent(ref float value, float next)
        {
            if (Mathf.Approximately(value, next))
            {
                return false;
            }

            value = next;
            return true;
        }
    }
}
