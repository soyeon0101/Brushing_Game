using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   화면 전환의 유일한 기준. 화면들은 버튼 이벤트만 알리고, 어디로 갈지는 여기서 정한다.
  ///   첫 실행: 타이틀 → 나이 → 캐릭터 → 닉네임(프로필 생성) → 로비
  ///   이후:   타이틀 → 로비
  ///   치료:   로비 → 환자 확인 → 카메라 준비 → 로딩 → 4구역 양치 → 완료 → 스티커 → (새 배지가 있으면 배지함) → 로비
  /// </summary>
  public class GameFlow : MonoBehaviour
  {
    [SerializeField] private GameCatalog _catalog;
    [Tooltip("카메라 준비·양치 화면에서만 보이는 카메라 영상")]
    [SerializeField] private GameObject _cameraFeed;

    [Header("화면")]
    [SerializeField] private TitleScreen _title;
    [SerializeField] private AgeSelectScreen _ageSelect;
    [SerializeField] private CharacterSelectScreen _characterSelect;
    [SerializeField] private NicknameScreen _nickname;
    [SerializeField] private LobbyScreen _lobby;
    [SerializeField] private PatientBookScreen _patientBook;
    [SerializeField] private PatientIntroScreen _patientIntro;
    [SerializeField] private CameraReadyScreen _cameraReady;
    [SerializeField] private LoadingScreen _loading;
    [SerializeField] private BrushingScreen _brushing;
    [SerializeField] private CompleteScreen _complete;
    [SerializeField] private StickerScreen _stickers;
    [SerializeField] private BadgeScreen _badges;
    [SerializeField] private GuardianSettingsPanel _settings;

    private ScreenBase _current;
    private PlayerProfile _draft = new PlayerProfile();
    private PatientInfo _patient;
    private StickerRecord _newSticker;
    private List<BadgeInfo> _newBadges = new List<BadgeInfo>();

    private static SaveData Data => SaveStore.Data;

    private void Awake()
    {
      foreach (var screen in AllScreens())
      {
        screen.gameObject.SetActive(false);
      }
      _settings.gameObject.SetActive(false);
      _cameraFeed.SetActive(false);
    }

    private void Start()
    {
      Data.settings.ApplyAudio();

      _title.StartPressed += () =>
      {
        if (Data.HasProfile)
        {
          ShowLobby();
        }
        else
        {
          ShowAgeSelect();
        }
      };

      _ageSelect.BackPressed += () => Show(_title);
      _ageSelect.Confirmed += age =>
      {
        _draft.age = age;
        _characterSelect.Setup(_catalog.characters, _draft.characterId);
        Show(_characterSelect);
      };
      _characterSelect.BackPressed += ShowAgeSelect;
      _characterSelect.Confirmed += id =>
      {
        _draft.characterId = id;
        _nickname.Setup(_catalog.FindCharacter(id), _draft.nickname);
        Show(_nickname);
      };
      _nickname.BackPressed += () => Show(_characterSelect);
      _nickname.Confirmed += nickname =>
      {
        _draft.nickname = nickname;
        Data.profile = _draft;
        _draft = new PlayerProfile();
        SaveStore.Save();
        ShowLobby();
      };

      _lobby.TreatPressed += () =>
      {
        var now = DateTime.Now;
        _patient = _catalog.PatientFor(Data, now);
        _patientIntro.Setup(_patient, SaveData.SlotAt(now));
        Show(_patientIntro);
      };
      _lobby.BookPressed += () =>
      {
        _patientBook.Setup(_catalog, Data);
        Show(_patientBook);
      };
      _lobby.StickersPressed += () =>
      {
        _stickers.Setup(Data, null);
        Show(_stickers);
      };
      _lobby.SettingsPressed += _settings.Open;
      _settings.ResetConfirmed += () =>
      {
        SaveStore.ResetAll();
        Data.settings.ApplyAudio();
        _draft = new PlayerProfile();
        Show(_title);
      };
      _patientBook.BackPressed += ShowLobby;

      _patientIntro.BackPressed += ShowLobby;
      _patientIntro.Confirmed += () => Show(_cameraReady);
      _cameraReady.BackPressed += ShowLobby;
      _cameraReady.Finished += () => Show(_loading);
      _loading.Finished += () =>
      {
        _brushing.Setup(_patient, Data.settings.zoneSeconds);
        Show(_brushing);
      };
      _brushing.QuitRequested += ShowLobby;
      _brushing.Completed += OnTreatmentComplete;
      _complete.Confirmed += () =>
      {
        _stickers.Setup(Data, _newSticker);
        Show(_stickers);
      };
      _stickers.BackPressed += () =>
      {
        if (_newBadges.Count == 0)
        {
          ShowLobby();
          return;
        }
        _badges.Setup(_catalog, Data, _newBadges);
        _newBadges = new List<BadgeInfo>();
        Show(_badges);
      };
      _lobby.BadgesPressed += () =>
      {
        _badges.Setup(_catalog, Data, null);
        Show(_badges);
      };
      _badges.BackPressed += ShowLobby;

      Show(_title);
    }

    private ScreenBase[] AllScreens() => new ScreenBase[]
    {
      _title, _ageSelect, _characterSelect, _nickname, _lobby, _patientBook, _patientIntro,
      _cameraReady, _loading, _brushing, _complete, _stickers, _badges,
    };

    private void Show(ScreenBase screen)
    {
      if (_current != null)
      {
        _current.OnHide();
        _current.gameObject.SetActive(false);
      }
      _current = screen;
      _cameraFeed.SetActive(screen == _cameraReady || screen == _brushing);
      screen.gameObject.SetActive(true);
      screen.OnShow();
    }

    private void ShowAgeSelect()
    {
      _ageSelect.Setup(_catalog.ages, _draft.age);
      Show(_ageSelect);
    }

    private void ShowLobby()
    {
      var character = _catalog.FindCharacter(Data.profile.characterId);
      var now = DateTime.Now;
      _lobby.Setup(Data, character, _catalog.PatientFor(Data, now), SaveData.SlotAt(now));
      Show(_lobby);
    }

    /// <summary>치료 기록: 스티커(같은 날 같은 끼니는 한 장), 도감, 치료 횟수, 새 배지</summary>
    private void OnTreatmentComplete()
    {
      var now = DateTime.Now;
      var date = SaveData.DateKey(now);
      var slot = SaveData.SlotAt(now);
      _newSticker = null;
      if (!Data.HasSticker(date, slot))
      {
        _newSticker = new StickerRecord { date = date, slot = slot, patientId = _patient.id };
        Data.stickers.Add(_newSticker);
      }
      if (!Data.curedPatientIds.Contains(_patient.id))
      {
        Data.curedPatientIds.Add(_patient.id);
      }
      Data.treatmentCount++;
      _newBadges = BadgeRules.Evaluate(_catalog, Data);
      SaveStore.Save();

      _complete.Setup(_patient);
      Show(_complete);
    }
  }
}
