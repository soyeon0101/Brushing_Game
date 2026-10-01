using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   배경음(반복)과 효과음. 소리 파일은 인스펙터에서 바꿔 끼우면 되고, 코드는 어느 소리를 언제 트는지만 안다.
  ///   전체 소리 켬/끔은 GuardianSettings.ApplyAudio(AudioListener 음량)가, 배경음/효과음 켬/끔은 여기서 따로 처리한다.
  ///   씬에 하나만 둔다. 없어도 게임은 소리 없이 그대로 돌아간다.
  /// </summary>
  public class SoundManager : MonoBehaviour
  {
    [Header("배경음 (반복)")]
    [Tooltip("양치 화면이 아닌 모든 화면")]
    [SerializeField] private AudioClip _menuMusic;
    [Tooltip("양치 화면")]
    [SerializeField] private AudioClip _gameplayMusic;

    [Header("효과음")]
    [Tooltip("모든 버튼을 누를 때")]
    [SerializeField] private AudioClip _touch;
    [Tooltip("치료 완료 화면이 뜰 때")]
    [SerializeField] private AudioClip _clear;
    [Tooltip("4구역을 다 닦고 마지막으로 줌아웃할 때")]
    [SerializeField] private AudioClip _gargle;

    [Header("크기")]
    [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _effectVolume = 1f;

    private static SoundManager _instance;
    private AudioSource _music;
    private AudioSource _effect;

    private static GuardianSettings Settings => SaveStore.Data.settings;

    private void Awake()
    {
      _instance = this;
      _music = gameObject.AddComponent<AudioSource>();
      _music.playOnAwake = false;
      _music.loop = true;
      _effect = gameObject.AddComponent<AudioSource>();
      _effect.playOnAwake = false;
      ApplySettings();
    }

    private void Start()
    {
      // 꺼져 있는 화면의 버튼까지 전부 (화면은 GameFlow가 켜고 끈다)
      foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        button.onClick.AddListener(PlayTouch);
      }
    }

    private void OnDestroy()
    {
      if (_instance == this)
      {
        _instance = null;
      }
    }

    /// <summary>설정의 배경음/효과음 켬/끔과 볼륨을 반영한다 (인스펙터 크기 × 보호자 설정 볼륨)</summary>
    public static void ApplySettings()
    {
      if (_instance == null)
      {
        return;
      }
      _instance._music.volume = _instance._musicVolume * Mathf.Clamp01(Settings.musicVolume);
      _instance._music.mute = !Settings.music;
      _instance._effect.volume = _instance._effectVolume * Mathf.Clamp01(Settings.effectVolume);
      _instance._effect.mute = !Settings.effects;
    }

    public static void PlayMenuMusic() => PlayMusic(_instance != null ? _instance._menuMusic : null);

    public static void PlayGameplayMusic() => PlayMusic(_instance != null ? _instance._gameplayMusic : null);

    public static void StopMusic()
    {
      if (_instance != null)
      {
        _instance._music.Stop();
        _instance._music.clip = null;
      }
    }

    /// <summary>일시정지: 멈춘 곳에서 다시 이어서 튼다</summary>
    public static void SetMusicPaused(bool paused)
    {
      if (_instance == null)
      {
        return;
      }
      if (paused)
      {
        _instance._music.Pause();
      }
      else
      {
        _instance._music.UnPause();
      }
    }

    public static void PlayTouch() => PlayEffect(_instance != null ? _instance._touch : null);

    public static void PlayClear() => PlayEffect(_instance != null ? _instance._clear : null);

    public static void PlayGargle() => PlayEffect(_instance != null ? _instance._gargle : null);

    private static void PlayMusic(AudioClip clip)
    {
      if (_instance == null)
      {
        return;
      }
      var music = _instance._music;
      // 같은 곡이면 처음부터 다시 틀지 않는다 (로비 ↔ 도감처럼 화면만 바뀔 때)
      if (music.clip == clip && music.isPlaying)
      {
        return;
      }
      music.clip = clip;
      if (clip != null)
      {
        music.Play();
      }
      else
      {
        music.Stop();
      }
    }

    private static void PlayEffect(AudioClip clip)
    {
      if (_instance != null && clip != null)
      {
        _instance._effect.PlayOneShot(clip);
      }
    }
  }
}
