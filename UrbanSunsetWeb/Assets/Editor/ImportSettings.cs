using UnityEditor;
using UnityEngine;

namespace UrbanSunset.EditorTools
{
    /// <summary>
    /// Import rules for everything under Assets/Resources so the project needs no hand-tuned .meta files:
    ///   Resources/Pixel  -> crisp point-filtered pixel-art sprites
    ///   Resources/Smooth -> bilinear sprites (sky, logo, shield bubble, UI words)
    ///   Resources/Audio  -> mono SFX, compressed music, tuned for small WebGL downloads
    /// </summary>
    public class ImportSettings : AssetPostprocessor
    {
        const int Version = 3;
        public override uint GetVersion() { return Version; }

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool pixel = path.Contains("/Resources/Pixel/");
            bool smooth = path.Contains("/Resources/Smooth/");
            if (!pixel && !smooth) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = 100f;
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for tiled scrolling layers
            settings.spriteExtrude = 0;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            bool feet = file.StartsWith("player_");
            settings.spriteAlignment = (int)(feet ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            settings.spritePivot = feet ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
        }

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Audio/")) return;

            var importer = (AudioImporter)assetImporter;
            bool music = path.EndsWith("music.mp3") || path.EndsWith("title.mp3");
            importer.forceToMono = !music;
            importer.loadInBackground = false;
            var s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.45f : 0.7f;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }
    }
}
