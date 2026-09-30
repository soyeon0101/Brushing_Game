using System;
using UnityEngine;

namespace BrushGame
{
  [Serializable]
  public class PlayerCharacter
  {
    public string id;
    public string displayName;
    public Sprite image;
    [Tooltip("얼굴~쇄골까지 자른 그림. 선택 카드, 프로필에 쓴다. 비우면 image")]
    public Sprite portrait;

    public Sprite Portrait => portrait != null ? portrait : image;
  }

  [Serializable]
  public class PatientInfo
  {
    public string id;
    public string displayName;
    [Tooltip("오늘의 환자 카드와 치료 전 모습")]
    public Sprite before;
    [Tooltip("치료 완료 후 모습")]
    public Sprite after;
    [Tooltip("치료 전 모습을 얼굴~쇄골까지 자른 그림. 로비, 환자 소개, 양치 안내에 쓴다. 비우면 before")]
    public Sprite portrait;
    [Tooltip("양치 화면 배경: 입을 크게 벌린 얼굴 (세로 화면을 꽉 채운다)")]
    public Sprite face;
    [Tooltip("양치 화면의 윗잇몸/입 모양. 비우면 기본 잇몸")]
    public Sprite mouthTop;
    [Tooltip("양치 화면의 아랫잇몸/입 모양. 비우면 기본 잇몸")]
    public Sprite mouthBottom;
    [TextArea] public string requestLine;

    public Sprite Portrait => portrait != null ? portrait : before;
    [TextArea] public string thanksLine;
  }

  /// <summary>
  ///   캐릭터·환자처럼 늘어날 수 있는 콘텐츠 목록. 코드 수정 없이 여기에 항목을 추가하면 된다.
  /// </summary>
  [CreateAssetMenu(menuName = "BrushGame/Game Catalog", fileName = "GameCatalog")]
  public class GameCatalog : ScriptableObject
  {
    public int[] ages = { 4, 5, 6, 7 };
    public PlayerCharacter[] characters;
    public PatientInfo[] patients;

    public PlayerCharacter FindCharacter(string id) => Array.Find(characters, c => c.id == id);

    /// <summary>치료할 때마다 다음 환자가 온다</summary>
    public PatientInfo PatientForVisit(int visitCount) => patients[visitCount % patients.Length];
  }
}
