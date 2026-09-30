using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>첫 실행 1단계: 나이 선택</summary>
  public class AgeSelectScreen : ScreenBase
  {
    [SerializeField] private ChoiceCard[] _cards;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _backButton;

    private int[] _ages = Array.Empty<int>();
    private int _selected = -1;

    public event Action<int> Confirmed;
    public event Action BackPressed;

    public void Setup(int[] ages, int currentAge)
    {
      _ages = ages;
      for (var i = 0; i < _cards.Length; i++)
      {
        var used = i < ages.Length;
        _cards[i].gameObject.SetActive(used);
        if (used)
        {
          _cards[i].SetContent($"{ages[i]}세", null);
        }
      }
      Select(Array.IndexOf(ages, currentAge));
    }

    private void Awake()
    {
      for (var i = 0; i < _cards.Length; i++)
      {
        var index = i;
        _cards[i].Button.onClick.AddListener(() => Select(index));
      }
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke(_ages[_selected]));
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Select(int index)
    {
      _selected = index;
      for (var i = 0; i < _cards.Length; i++)
      {
        _cards[i].SetSelected(i == index);
      }
      _nextButton.interactable = index >= 0;
    }
  }
}
