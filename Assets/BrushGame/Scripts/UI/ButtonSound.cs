using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   이 버튼은 기본 버튼 소리 대신 다른 효과음을 낸다 (SoundManager가 게임 시작 때 읽는다).
  ///   ID는 Sound/Sfx 파일 이름. 비우면 소리를 내지 않는다.
  /// </summary>
  public class ButtonSound : MonoBehaviour
  {
    [SerializeField] private string _sfxId = "";

    public string SfxId => _sfxId;

    public void Configure(string sfxId)
    {
      _sfxId = sfxId;
    }
  }
}
