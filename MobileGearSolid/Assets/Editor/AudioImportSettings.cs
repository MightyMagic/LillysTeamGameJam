using UnityEditor;
using UnityEngine;

namespace MobileGearSolid.EditorTools
{
    /// Sensible mobile import settings for files dropped into Assets/Resources/Audio:
    /// music streams from disk (low memory), short effects are decompressed for instant playback.
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (path.Contains("Resources/Audio/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.8f;
            }
            ai.defaultSampleSettings = s;
        }
    }
}
