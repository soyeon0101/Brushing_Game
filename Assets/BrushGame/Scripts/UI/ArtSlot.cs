using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   그림이 들어갈 자리. 그림이 아직 없으면 임시 색 도형을 그대로 두고,
  ///   그림이 있으면 넣으면서 임시 색을 흰색으로 돌려 그림 색이 그대로 나오게 한다.
  /// </summary>
  public static class ArtSlot
  {
    public static void Apply(Image image, Sprite sprite)
    {
      if (image == null || sprite == null)
      {
        return;
      }
      image.sprite = sprite;
      image.type = Image.Type.Simple;
      image.preserveAspect = true;
      image.color = new Color(1f, 1f, 1f, image.color.a);
    }
  }
}
