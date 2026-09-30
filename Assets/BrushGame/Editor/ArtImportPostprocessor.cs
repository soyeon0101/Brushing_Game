using UnityEditor;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   Assets/BrushGame/Art 아래에 처음 들어온 그림을 UI용 스프라이트로 가져온다.
  ///   이미 가져온 그림의 설정을 손으로 바꿨다면 건드리지 않는다.
  /// </summary>
  public class ArtImportPostprocessor : AssetPostprocessor
  {
    private const string ArtRoot = "Assets/BrushGame/Art/";

    private void OnPreprocessTexture()
    {
      if (!assetPath.StartsWith(ArtRoot) || !assetImporter.importSettingsMissing)
      {
        return;
      }
      var importer = (TextureImporter)assetImporter;
      importer.textureType = TextureImporterType.Sprite;
      importer.spriteImportMode = SpriteImportMode.Single;
      importer.alphaIsTransparency = true;
      importer.mipmapEnabled = false;
    }
  }
}
