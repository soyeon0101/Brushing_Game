using UnityEditor;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   Assets/BrushGame/Art 아래에 처음 들어온 그림을 UI용 스프라이트로 가져온다.
  ///   단, Art/Models 아래는 3D 모델 텍스처라서 스프라이트로 바꾸지 않고, 노멀맵만 알아보고 크기를 모바일에 맞게 줄인다.
  ///   이미 가져온 그림의 설정을 손으로 바꿨다면 건드리지 않는다.
  /// </summary>
  public class ArtImportPostprocessor : AssetPostprocessor
  {
    private const string ArtRoot = "Assets/BrushGame/Art/";
    private const string ModelRoot = "Assets/BrushGame/Art/Models/";
    private const int ModelTextureMaxSize = 2048;

    private void OnPreprocessTexture()
    {
      if (!assetPath.StartsWith(ArtRoot) || !assetImporter.importSettingsMissing)
      {
        return;
      }
      var importer = (TextureImporter)assetImporter;
      if (assetPath.StartsWith(ModelRoot))
      {
        // Meshy는 "_normal.png", Tripo는 "_Normal_Bake.png"처럼 이름이 제각각이다
        if (System.IO.Path.GetFileName(assetPath).ToLowerInvariant().Contains("normal"))
        {
          importer.textureType = TextureImporterType.NormalMap;
        }
        importer.maxTextureSize = ModelTextureMaxSize;
        return;
      }
      importer.textureType = TextureImporterType.Sprite;
      importer.spriteImportMode = SpriteImportMode.Single;
      importer.alphaIsTransparency = true;
      importer.mipmapEnabled = false;
    }
  }
}
