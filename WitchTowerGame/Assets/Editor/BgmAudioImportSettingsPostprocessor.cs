using UnityEditor;
using UnityEngine;

namespace WitchTower.EditorTools
{
    public sealed class BgmAudioImportSettingsPostprocessor : AssetPostprocessor
    {
        private const string BgmResourcePathPrefix = "Assets/Resources/Audio/BGM/";
        private const string SeResourcePathPrefix = "Assets/Resources/Audio/SE/";
        private const float BgmVorbisQuality = 0.62f;

        private void OnPreprocessAudio()
        {
            ApplySettings(assetImporter as AudioImporter, assetPath);
        }

        [MenuItem("WitchTower/Audio/Apply BGM Import Settings")]
        private static void ApplyAllBgmSettings()
        {
            ApplyAudioSettings("Assets/Resources/Audio/BGM", "BGM");
        }

        [MenuItem("WitchTower/Audio/Apply SE Import Settings")]
        private static void ApplyAllSeSettings()
        {
            ApplyAudioSettings("Assets/Resources/Audio/SE", "SE");
        }

        private static void ApplyAudioSettings(string folderPath, string label)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
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

            Debug.Log($"[BgmAudioImportSettings] Applied mobile-friendly {label} import settings to {changedCount} clip(s).");
        }

        private static bool ApplySettings(AudioImporter importer, string path)
        {
            if (importer == null || string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.StartsWith(BgmResourcePathPrefix))
            {
                return ApplyBgmSettings(importer);
            }

            if (path.StartsWith(SeResourcePathPrefix))
            {
                return ApplySeSettings(importer);
            }

            return false;
        }

        private static bool ApplyBgmSettings(AudioImporter importer)
        {
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

        private static bool ApplySeSettings(AudioImporter importer)
        {
            bool changed = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            changed |= SetIfDifferent(ref settings.loadType, AudioClipLoadType.DecompressOnLoad);
            changed |= SetIfDifferent(ref settings.compressionFormat, AudioCompressionFormat.ADPCM);
            changed |= SetIfDifferent(ref settings.sampleRateSetting, AudioSampleRateSetting.PreserveSampleRate);
            changed |= SetIfDifferent(ref settings.quality, 1f);
            changed |= SetIfDifferent(ref settings.preloadAudioData, true);

            if (changed)
            {
                importer.defaultSampleSettings = settings;
            }

            if (importer.loadInBackground != false)
            {
                importer.loadInBackground = false;
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
