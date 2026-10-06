using UnityEngine;

namespace BrushGame
{
  /// <summary>우주 항해 지도의 행성 하나. GameCatalog.planets에 순서대로 넣으면 지도가 그만큼 길어진다 (첫 행성이 맨 아래)</summary>
  [CreateAssetMenu(menuName = "BrushGame/Planet", fileName = "Planet")]
  public class PlanetData : ScriptableObject
  {
    public string planetName;
    public Sprite sprite;
    [Tooltip("나중에 우주선 부품과 연결할 때 쓴다. 지금은 비워 둬도 된다")]
    public string requiredPartId;
  }
}
