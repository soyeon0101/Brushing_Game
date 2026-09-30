using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>첫 실행 3단계: 닉네임 입력. 여기서 완료해야 프로필이 만들어진다</summary>
  public class NicknameScreen : ScreenBase
  {
    public const int MaxLength = 8;

    [SerializeField] private InputField _input;
    [SerializeField] private Image _characterImage;
    [SerializeField] private Button _doneButton;
    [SerializeField] private Button _backButton;

    public event Action<string> Confirmed;
    public event Action BackPressed;

    private string Nickname => _input.text.Trim();

    public void Setup(PlayerCharacter character, string currentNickname)
    {
      ArtSlot.Apply(_characterImage, character?.Portrait);
      _input.text = currentNickname ?? "";
      Refresh();
    }

    private void Awake()
    {
      _input.characterLimit = MaxLength;
      _input.onValueChanged.AddListener(_ => Refresh());
      _doneButton.onClick.AddListener(() => Confirmed?.Invoke(Nickname));
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Refresh()
    {
      _doneButton.interactable = Nickname.Length > 0;
    }
  }
}
