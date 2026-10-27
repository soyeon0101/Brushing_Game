using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   첫 실행 2단계 (보호자용): 회원가입. 보호자 이름과 이메일을 받는다.
  ///   서버가 없어서 가입 정보는 이 기기 저장 파일에만 남는다.
  /// </summary>
  public class SignupScreen : ScreenBase
  {
    public const int NameMaxLength = 20;
    public const int EmailMaxLength = 60;

    private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    [SerializeField] private InputField _nameInput;
    [SerializeField] private InputField _emailInput;
    [Tooltip("이메일 형식이 틀렸을 때 안내")]
    [SerializeField] private Text _note;
    [SerializeField] private Button _doneButton;
    [SerializeField] private Button _backButton;

    [SerializeField] private string _defaultNote = "가입 정보는 이 기기에만 저장돼요";
    [SerializeField] private string _badEmailNote = "이메일 주소를 다시 확인해 주세요";

    public event Action<string, string> Confirmed;
    public event Action BackPressed;

    private string GuardianName => _nameInput.text.Trim();
    private string Email => _emailInput.text.Trim();
    private bool EmailOk => EmailPattern.IsMatch(Email);

    public void Setup(GuardianAccount account)
    {
      _nameInput.text = account?.guardianName ?? "";
      _emailInput.text = account?.email ?? "";
      Refresh();
    }

    private void Awake()
    {
      _nameInput.characterLimit = NameMaxLength;
      _emailInput.characterLimit = EmailMaxLength;
      _emailInput.contentType = InputField.ContentType.EmailAddress;
      _nameInput.onValueChanged.AddListener(_ => Refresh());
      _emailInput.onValueChanged.AddListener(_ => Refresh());
      _doneButton.onClick.AddListener(() => Confirmed?.Invoke(GuardianName, Email));
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Refresh()
    {
      var typedEmail = Email.Length > 0;
      _note.text = typedEmail && !EmailOk ? _badEmailNote : _defaultNote;
      _doneButton.interactable = GuardianName.Length > 0 && EmailOk;
    }
  }
}
