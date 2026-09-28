using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   카메라 미리보기, 안내 문구, 판정 표시처럼 개발 때만 필요한 것들을 실제 플레이에서 숨긴다.
  ///   토글 키로 테스트 중에만 켤 수 있다.
  /// </summary>
  public class DevOnlyObjects : MonoBehaviour
  {
    [SerializeField] private GameObject[] _objects;
    [SerializeField] private Behaviour[] _behaviours;
    [SerializeField] private bool _visible;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

    private void Awake()
    {
      Apply();
    }

    private void Update()
    {
      if (Input.GetKeyDown(_toggleKey))
      {
        _visible = !_visible;
        Apply();
      }
    }

    private void Apply()
    {
      foreach (var go in _objects)
      {
        go.SetActive(_visible);
      }
      foreach (var b in _behaviours)
      {
        b.enabled = _visible;
      }
    }
  }
}
