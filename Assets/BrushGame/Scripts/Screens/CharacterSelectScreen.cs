using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>첫 실행 2단계: 캐릭터 선택 (공주/왕자). 항목은 GameCatalog.characters에서 온다</summary>
  public class CharacterSelectScreen : ScreenBase
  {
    [SerializeField] private ChoiceCard[] _cards;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _backButton;

    private PlayerCharacter[] _characters = Array.Empty<PlayerCharacter>();
    private int _selected = -1;

    public event Action<string> Confirmed;
    public event Action BackPressed;

    public void Setup(PlayerCharacter[] characters, string currentId)
    {
      _characters = characters;
      for (var i = 0; i < _cards.Length; i++)
      {
        var used = i < characters.Length;
        _cards[i].gameObject.SetActive(used);
        if (used)
        {
          _cards[i].SetContent(characters[i].displayName, characters[i].Portrait);
        }
      }
      Select(Array.FindIndex(characters, c => c.id == currentId));
    }

    private void Awake()
    {
      for (var i = 0; i < _cards.Length; i++)
      {
        var index = i;
        _cards[i].Button.onClick.AddListener(() => Select(index));
      }
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke(_characters[_selected].id));
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
