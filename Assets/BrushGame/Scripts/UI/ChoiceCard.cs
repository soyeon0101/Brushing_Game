using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>여러 개 중 하나를 고르는 카드 (나이, 캐릭터, 설정값)</summary>
  public class ChoiceCard : MonoBehaviour
  {
    [SerializeField] private Button _button;
    [Tooltip("선택됐을 때만 켜지는 테두리")]
    [SerializeField] private GameObject _selectedMark;
    [Tooltip("그림 자리 (없어도 됨)")]
    [SerializeField] private Image _image;
    [SerializeField] private Text _label;

    public Button Button => _button;

    public void SetSelected(bool selected)
    {
      _selectedMark.SetActive(selected);
    }

    public void SetContent(string label, Sprite sprite)
    {
      if (_label != null)
      {
        _label.text = label;
      }
      ArtSlot.Apply(_image, sprite);
    }
  }
}
