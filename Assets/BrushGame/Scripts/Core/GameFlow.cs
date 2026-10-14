using System;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   화면 전환의 유일한 기준. 화면들은 버튼 이벤트만 알리고, 어디로 갈지는 여기서 정한다.
  ///   첫 실행: 타이틀 → 나이 → 캐릭터 → 닉네임(프로필 생성) → 로비
  ///   이후:   타이틀 → 로비
  ///   치료:   로비 → 환자 확인 → 카메라 준비 → 로딩 → 4구역 양치 → 완료 → 스티커 → 우주 항해 지도 → 로비
  /// </summary>
  public class GameFlow : MonoBehaviour
  {
    /// <summary>로비 스티커판 버튼 자리가 여는 화면 (BrushGame/Lobby 메뉴로 바꾸고 되돌린다)</summary>
    public enum StickerSlot { StickerBoard, PartnerRoom }

    /// <summary>로비 반짝 배지 버튼 자리가 여는 화면</summary>
    public enum BadgeSlot { BadgeBox, SpaceMap }

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
    [Tooltip("우주 항해 지도. 로비의 반짝 배지 버튼과 치료 후 스티커판 다음에 열린다")]
    [SerializeField] private SpaceMapScreen _spaceMap;
    [Tooltip("반짝 배지함. 배지 버튼 자리를 지도로 바꿔도 되돌릴 수 있게 씬에 남겨 둔다")]
    [SerializeField] private BadgeScreen _badges;
    [Tooltip("파트너 방 (로봇 친구)")]
    [SerializeField] private PartnerRoomScreen _partnerRoom;
    [SerializeField] private GuardianSettingsPanel _settings;

    [Header("로비 버튼 자리 (BrushGame/Lobby 메뉴가 버튼 그림·글자와 함께 바꾼다)")]
    [SerializeField] private StickerSlot _stickerSlot = StickerSlot.StickerBoard;
    [SerializeField] private BadgeSlot _badgeSlot = BadgeSlot.SpaceMap;

    private ScreenBase _current;
    private PlayerProfile _draft = new PlayerProfile();
    private PatientInfo _patient;
    private StickerRecord _newSticker;
    /// <summary>치료 직후 스티커판이면 다음에 지도를 보여준다 (로비에서 연 스티커판은 로비로)</summary>
    private bool _mapAfterStickers;

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
        if (_stickerSlot == StickerSlot.PartnerRoom && _partnerRoom != null)
        {
          _partnerRoom.Setup(Data);
          Show(_partnerRoom);
          return;
        }
        _mapAfterStickers = false;
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
        _brushing.Setup(_patient, Data.settings.zoneSeconds, _catalog.FindCharacter(Data.profile.characterId));
        Show(_brushing);
      };
      _brushing.QuitRequested += ShowLobby;
      _brushing.Completed += OnTreatmentComplete;
      // 치료 후: 스티커판에서 도장 쾅 → 우주 항해 지도에서 새 발판 켜짐 → 로비
      _complete.Confirmed += () =>
      {
        _mapAfterStickers = true;
        _stickers.Setup(Data, _newSticker);
        Show(_stickers);
      };
      _stickers.BackPressed += () =>
      {
        if (!_mapAfterStickers)
        {
          ShowLobby();
          return;
        }
        _mapAfterStickers = false;
        ShowSpaceMap();
      };
      // 반짝 배지 버튼 자리는 우주 항해 지도가 맡는다 (배지 화면은 되돌릴 수 있게 꺼진 채로 남긴다)
      _lobby.BadgesPressed += () =>
      {
        if (_badgeSlot == BadgeSlot.SpaceMap)
        {
          ShowSpaceMap();
          return;
        }
        _badges.Setup(_catalog, Data, null);
        Show(_badges);
      };
      _spaceMap.BackPressed += ShowLobby;
      _badges.BackPressed += ShowLobby;
      if (_partnerRoom != null)
      {
        _partnerRoom.BackPressed += ShowLobby;
      }

      Show(_title);
    }

    private ScreenBase[] AllScreens() => Array.FindAll(new ScreenBase[]
    {
      _title, _ageSelect, _characterSelect, _nickname, _lobby, _patientBook, _patientIntro,
      _cameraReady, _loading, _brushing, _complete, _stickers, _spaceMap, _badges, _partnerRoom,
    }, s => s != null);

    private void Show(ScreenBase screen)
    {
      if (_current != null)
      {
        _current.OnHide();
        _current.gameObject.SetActive(false);
      }
      _current = screen;
      _cameraFeed.SetActive(screen == _cameraReady || screen == _brushing);
      PlayMusicFor(screen);
      screen.gameObject.SetActive(true);
      screen.OnShow();
    }

    /// <summary>양치 화면은 게임 배경음, 완료 화면은 배경음 없이 클리어 효과음, 나머지는 기본 배경음</summary>
    private void PlayMusicFor(ScreenBase screen)
    {
      if (screen == _brushing)
      {
        SoundManager.PlayGameplayMusic();
      }
      else if (screen == _complete)
      {
        SoundManager.StopMusic();
        SoundManager.PlayClear();
      }
      else
      {
        SoundManager.PlayMenuMusic();
      }
    }

    private void ShowAgeSelect()
    {
      _ageSelect.Setup(_catalog.ages, _draft.age);
      Show(_ageSelect);
    }

    private void ShowSpaceMap()
    {
      _spaceMap.Setup(_catalog, Data);
      Show(_spaceMap);
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
      // 배지 화면은 지금 열지 않지만, 되돌릴 때를 위해 배지 기록은 계속 쌓아 둔다
      BadgeRules.Evaluate(_catalog, Data);
      SaveStore.Save();

      _complete.Setup(_patient);
      Show(_complete);
    }
  }
}
