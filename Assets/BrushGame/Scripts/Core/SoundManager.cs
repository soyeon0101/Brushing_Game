using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   배경음(반복), 효과음, 음성 안내(TTS). 소리 파일은 인스펙터에서 바꿔 끼우면 되고, 코드는 어느 소리를 언제 트는지만 안다.
  ///   전체 소리 켬/끔은 GuardianSettings.ApplyAudio(AudioListener 음량)가, 배경음/효과음/음성 켬/끔은 여기서 따로 처리한다.
  ///   음성·효과음은 이름(ID)으로 부른다. 파일 이름 = ID (Docs/tts_script.csv, Docs/sfx_list.csv). 목록은 BrushGame/Link Sounds가 채운다.
  ///   음성이 나오는 동안 배경음을 낮춘다. 화면이 바뀌면 GameFlow가 하던 말을 끊는다.
  ///   씬에 하나만 둔다. 없거나 파일이 빠져도 게임은 소리 없이 그대로 돌아간다.
  /// </summary>
  public class SoundManager : MonoBehaviour
  {
    /// <summary>다른 말이 나오는 중일 때 어떻게 할지</summary>
    public enum Say
    {
      /// <summary>하던 말을 끊고 바로 (구역 시작·완료처럼 꼭 들려야 하는 말)</summary>
      Interrupt,
      /// <summary>하던 말이 끝난 뒤에 (최대 2개까지 기다린다)</summary>
      Queue,
      /// <summary>아무 말도 안 하고 있을 때만 (응원처럼 놓쳐도 되는 말)</summary>
      IfFree,
    }

    [Header("배경음 (반복)")]
    [Tooltip("양치 화면이 아닌 모든 화면")]
    [SerializeField] private AudioClip _menuMusic;
    [Tooltip("양치 화면 (후보 목록이 비었을 때)")]
    [SerializeField] private AudioClip _gameplayMusic;
    [Tooltip("설정에서 고르는 양치 화면 배경음 후보 (순서 = 설정 버튼 순서)")]
    [SerializeField] private AudioClip[] _gameplayMusicOptions;

    [Header("효과음")]
    [Tooltip("모든 버튼을 누를 때 (ButtonSound가 붙은 버튼은 그 소리를 낸다)")]
    [SerializeField] private AudioClip _touch;
    [Tooltip("치료 완료 화면이 뜰 때")]
    [SerializeField] private AudioClip _clear;
    [Tooltip("4구역을 다 닦고 마지막으로 줌아웃할 때")]
    [SerializeField] private AudioClip _gargle;

    [Header("음성·효과음 목록 (BrushGame/Link Sounds가 Sound/Voice, Sound/Sfx에서 채운다)")]
    [SerializeField] private AudioClip[] _voices;
    [SerializeField] private AudioClip[] _sfx;

    [Header("크기")]
    [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _effectVolume = 1f;
    [Tooltip("음성 기준 크기. 보호자 설정의 안내 음성 게이지(0~100%)가 여기에 곱해진다")]
    [SerializeField, Range(0f, 1f)] private float _voiceVolume = 0.9f;
    [Tooltip("음성이 나오는 동안 배경음 크기 (원래 크기 대비)")]
    [SerializeField, Range(0f, 1f)] private float _duckRatio = 0.3f;
    [Tooltip("양치 중 슥삭 반복 소리 크기")]
    [SerializeField, Range(0f, 1f)] private float _brushLoopVolume = 0.35f;

    private const int MaxQueue = 2;

    private static SoundManager _instance;
    private AudioSource _music;
    private AudioSource _effect;
    private AudioSource _pitched;
    private AudioSource _voice;
    private AudioSource _loop;
    private readonly Dictionary<string, AudioClip> _voiceMap = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> _sfxMap = new Dictionary<string, AudioClip>();
    private readonly List<AudioClip> _queue = new List<AudioClip>();
    private readonly Dictionary<string, float> _cooldownUntil = new Dictionary<string, float>();
    private float _musicBase;
    private float _duck = 1f;
    private bool _loopOn;

    private static GuardianSettings Settings => SaveStore.Data.settings;

    /// <summary>음성이 나오고 있거나 기다리는 말이 있는지 (동화가 말이 끝날 때까지 다음 장으로 안 넘어가게)</summary>
    public static bool VoiceBusy => _instance != null && (_instance._voice.isPlaying || _instance._queue.Count > 0);

    private void Awake()
    {
      _instance = this;
      _music = NewSource(true);
      _effect = NewSource(false);
      _pitched = NewSource(false);
      _voice = NewSource(false);
      _loop = NewSource(true);
      Fill(_voiceMap, _voices);
      Fill(_sfxMap, _sfx);
      ApplySettings();
    }

    private void Start()
    {
      // 꺼져 있는 화면의 버튼까지 전부 (화면은 GameFlow가 켜고 끈다)
      foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        var custom = button.GetComponent<ButtonSound>();
        if (custom != null)
        {
          var id = custom.SfxId;
          if (!string.IsNullOrEmpty(id))
          {
            button.onClick.AddListener(() => Sfx(id));
          }
          continue;
        }
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

    private void Update()
    {
      // 말이 끝나면 기다리던 말을 잇는다
      if (!_voice.isPlaying && _queue.Count > 0)
      {
        var next = _queue[0];
        _queue.RemoveAt(0);
        _voice.clip = next;
        _voice.Play();
      }
      // 말하는 동안 배경음을 낮춘다 (부드럽게)
      var duckTarget = _voice.isPlaying ? _duckRatio : 1f;
      _duck = Mathf.MoveTowards(_duck, duckTarget, Time.unscaledDeltaTime * 3f);
      _music.volume = _musicBase * _duck;
      // 슥삭 소리는 켜고 끌 때 0.2초에 걸쳐
      var loopTarget = _loopOn && Settings.effects ? _brushLoopVolume * Mathf.Clamp01(Settings.effectVolume) : 0f;
      _loop.volume = Mathf.MoveTowards(_loop.volume, loopTarget, Time.unscaledDeltaTime * 5f);
      if (_loop.volume <= 0f && !_loopOn && _loop.isPlaying)
      {
        _loop.Stop();
      }
    }

    /// <summary>설정의 배경음/효과음 켬/끔과 볼륨을 반영한다 (인스펙터 크기 × 보호자 설정 볼륨)</summary>
    public static void ApplySettings()
    {
      if (_instance == null)
      {
        return;
      }
      var i = _instance;
      i._musicBase = i._musicVolume * Mathf.Clamp01(Settings.musicVolume);
      i._music.volume = i._musicBase * i._duck;
      i._music.mute = !Settings.music;
      var effect = i._effectVolume * Mathf.Clamp01(Settings.effectVolume);
      i._effect.volume = effect;
      i._effect.mute = !Settings.effects;
      i._pitched.volume = effect;
      i._pitched.mute = !Settings.effects;
      i._voice.volume = i._voiceVolume * Mathf.Clamp01(Settings.voiceVolume);
      i._voice.mute = !Settings.voiceGuide;
      if (!Settings.voiceGuide)
      {
        StopVoice();
      }
    }

    // ---------- 배경음 ----------

    public static void PlayMenuMusic() => PlayMusic(_instance != null ? _instance._menuMusic : null);

    /// <summary>양치 화면 배경음: 설정에서 고른 곡</summary>
    public static void PlayGameplayMusic() => PlayMusic(_instance != null ? _instance.GameplayMusic(Settings.brushingMusic) : null);

    /// <summary>설정에서 양치 음악을 고를 때 미리 들려준다 (설정 창을 닫으면 PlayMenuMusic으로 돌아간다)</summary>
    public static void PreviewGameplayMusic(int index) => PlayMusic(_instance != null ? _instance.GameplayMusic(index) : null);

    private AudioClip GameplayMusic(int index)
    {
      if (_gameplayMusicOptions == null || _gameplayMusicOptions.Length == 0)
      {
        return _gameplayMusic;
      }
      var clip = _gameplayMusicOptions[Mathf.Clamp(index, 0, _gameplayMusicOptions.Length - 1)];
      return clip != null ? clip : _gameplayMusic;
    }

    public static void StopMusic()
    {
      if (_instance != null)
      {
        _instance._music.Stop();
        _instance._music.clip = null;
      }
    }

    /// <summary>일시정지: 멈춘 곳에서 다시 이어서 튼다 (슥삭 소리도 같이 멈춘다)</summary>
    public static void SetMusicPaused(bool paused)
    {
      if (_instance == null)
      {
        return;
      }
      if (paused)
      {
        _instance._music.Pause();
        SetBrushLoop(false);
      }
      else
      {
        _instance._music.UnPause();
      }
    }

    // ---------- 효과음 ----------

    public static void PlayTouch() => PlayEffect(_instance != null ? _instance._touch : null);

    public static void PlayClear() => PlayEffect(_instance != null ? _instance._clear : null);

    public static void PlayGargle() => PlayEffect(_instance != null ? _instance._gargle : null);

    /// <summary>효과음 하나 (ID = Sound/Sfx 파일 이름). 없으면 조용히 넘어간다</summary>
    public static void Sfx(string id, float volume = 1f)
    {
      if (_instance != null && _instance._sfxMap.TryGetValue(id, out var clip))
      {
        _instance._effect.PlayOneShot(clip, volume);
      }
    }

    /// <summary>높낮이를 바꾼 효과음 (지도 발판처럼 한 칸씩 음을 올리거나, 자주 나는 소리를 조금씩 다르게)</summary>
    public static void SfxPitch(string id, float pitch, float volume = 1f)
    {
      if (_instance != null && _instance._sfxMap.TryGetValue(id, out var clip))
      {
        _instance._pitched.pitch = pitch;
        _instance._pitched.PlayOneShot(clip, volume);
      }
    }

    /// <summary>쿨타임 동안 다시 안 나는 효과음</summary>
    public static void SfxOnce(string id, float cooldown, float volume = 1f)
    {
      if (_instance != null && _instance.TryCooldown("sfx:" + id, cooldown))
      {
        Sfx(id, volume);
      }
    }

    /// <summary>양치 중 슥삭 반복 소리. 닦는 동안만 켠다</summary>
    public static void SetBrushLoop(bool on)
    {
      if (_instance == null || !_instance._sfxMap.TryGetValue("brush_loop", out var clip))
      {
        return;
      }
      var i = _instance;
      i._loopOn = on;
      if (on && !i._loop.isPlaying)
      {
        i._loop.clip = clip;
        i._loop.volume = 0f;
        i._loop.Play();
      }
    }

    // ---------- 음성 안내 ----------

    /// <summary>
    ///   음성 하나 (ID = Sound/Voice 파일 이름). 보호자 설정에서 음성 안내를 끄면 안 나온다.
    ///   cooldown 초 동안은 같은 key(없으면 ID)로 다시 말하지 않는다.
    /// </summary>
    /// <returns>말했거나 줄을 섰으면 true</returns>
    public static bool Voice(string id, Say mode = Say.Queue, float cooldown = 0f, string key = null)
    {
      var i = _instance;
      if (i == null || string.IsNullOrEmpty(id) || !Settings.voiceGuide || !i._voiceMap.TryGetValue(id, out var clip))
      {
        return false;
      }
      var busy = i._voice.isPlaying || i._queue.Count > 0;
      if (mode == Say.IfFree && busy)
      {
        return false;
      }
      if (cooldown > 0f && !i.TryCooldown("voice:" + (key ?? id), cooldown))
      {
        return false;
      }
      if (mode == Say.Interrupt || !busy)
      {
        i._queue.Clear();
        i._voice.Stop();
        i._voice.clip = clip;
        i._voice.Play();
        return true;
      }
      if (i._queue.Count >= MaxQueue)
      {
        i._queue.RemoveAt(i._queue.Count - 1);
      }
      i._queue.Add(clip);
      return true;
    }

    /// <summary>하던 말과 기다리던 말을 모두 멈춘다 (화면이 바뀔 때)</summary>
    public static void StopVoice()
    {
      if (_instance == null)
      {
        return;
      }
      _instance._queue.Clear();
      _instance._voice.Stop();
    }

    // ---------- 내부 ----------

    private AudioSource NewSource(bool loop)
    {
      var source = gameObject.AddComponent<AudioSource>();
      source.playOnAwake = false;
      source.loop = loop;
      return source;
    }

    private static void Fill(Dictionary<string, AudioClip> map, AudioClip[] clips)
    {
      map.Clear();
      if (clips == null)
      {
        return;
      }
      foreach (var clip in clips)
      {
        if (clip != null)
        {
          map[clip.name] = clip;
        }
      }
    }

    private bool TryCooldown(string key, float cooldown)
    {
      var now = Time.unscaledTime;
      if (_cooldownUntil.TryGetValue(key, out var until) && now < until)
      {
        return false;
      }
      _cooldownUntil[key] = now + cooldown;
      return true;
    }

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
