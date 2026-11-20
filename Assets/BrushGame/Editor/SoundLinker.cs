using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   음성(Sound/Voice)과 효과음(Sound/Sfx) 파일을 SoundManager 목록에 연결하고, 모바일용 압축 설정을 맞춘다.
  ///   파일 이름 = 코드에서 부르는 ID (Docs/tts_script.csv, Docs/sfx_list.csv). 파일을 더하거나 바꾼 뒤 다시 실행하면 된다.
  ///   버튼 소리: 기본은 Sfx/touch, 환자·로봇·액자 버튼은 ButtonSound로 각자 소리를 낸다.
  /// </summary>
  public static class SoundLinker
  {
    private const string VoiceDir = "Assets/BrushGame/Sound/Voice";
    private const string SfxDir = "Assets/BrushGame/Sound/Sfx";

    [MenuItem("BrushGame/Link Sounds")]
    public static void LinkMenu()
    {
      var sound = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
      if (sound == null)
      {
        Debug.LogWarning("[BrushGame] Main 씬을 열고 실행하세요 (SoundManager가 없으면 BrushGame/Add Sound To Scene 먼저).");
        return;
      }
      Link(sound);
      var scene = sound.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
    }

    public static void Link(SoundManager sound)
    {
      // 음성: 모노, 메모리에 압축한 채로 (90개라 다 풀어 두면 메모리를 많이 먹는다)
      var voices = Import(VoiceDir, AudioClipLoadType.CompressedInMemory, 0.5f);
      // 효과음: 모노, 짧으니 풀어 둔다 (바로 나와야 함)
      var sfx = Import(SfxDir, AudioClipLoadType.DecompressOnLoad, 0.7f);

      var so = new SerializedObject(sound);
      SetArray(so.FindProperty("_voices"), voices);
      SetArray(so.FindProperty("_sfx"), sfx);
      var music = LinkBrushingMusic();
      SetArray(so.FindProperty("_gameplayMusicOptions"), music);
      var touch = sfx.FirstOrDefault(c => c.name == "touch");
      if (touch != null)
      {
        so.FindProperty("_touch").objectReferenceValue = touch;
      }
      so.ApplyModifiedProperties();

      var custom = 0;
      var lobby = Object.FindFirstObjectByType<LobbyScreen>(FindObjectsInactive.Include);
      if (lobby != null)
      {
        custom += SetButtonSound(new SerializedObject(lobby).FindProperty("_patientButton").objectReferenceValue as Button, "patient_tap");
      }
      var robot = Object.FindFirstObjectByType<PartnerRobot>(FindObjectsInactive.Include);
      if (robot != null)
      {
        custom += SetButtonSound(new SerializedObject(robot).FindProperty("_touch").objectReferenceValue as Button, "robot_beep");
      }
      var frame = Object.FindFirstObjectByType<PhotoFrameScreen>(FindObjectsInactive.Include);
      if (frame != null)
      {
        var buttons = new SerializedObject(frame).FindProperty("_frameButtons");
        for (var i = 0; buttons != null && i < buttons.arraySize; i++)
        {
          custom += SetButtonSound(buttons.GetArrayElementAtIndex(i).objectReferenceValue as Button, "frame_pick");
        }
      }
      Debug.Log($"[BrushGame] 음성 {voices.Length}개, 효과음 {sfx.Length}개 연결, 버튼 소리 {custom}곳 지정");
    }

    // ---------- 양치 화면 배경음 후보 (설정 "양치 음악"에서 아이가 고른다) ----------

    /// <summary>순서 = 설정 목록 순서 (통통 우주 산책, 포근한 별빛, 신나는 모험). 곡을 늘리면 MainSceneBuilder.MusicNames도 같이</summary>
    private static readonly string[] BrushingMusic =
    {
      "Assets/BrushGame/Sound/Bgm/Brushing_A_Marimba.wav",
      "Assets/BrushGame/Sound/Bgm/Brushing_B_WarmPad.wav",
      "Assets/BrushGame/Sound/Game_play_Sound.wav",
    };

    private static AudioClip[] LinkBrushingMusic()
    {
      var clips = new AudioClip[BrushingMusic.Length];
      for (var i = 0; i < BrushingMusic.Length; i++)
      {
        var path = BrushingMusic[i];
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
          continue;
        }
        // 긴 배경음: 스트리밍 + 압축 (메모리에 다 올리지 않는다)
        var settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis)
        {
          settings.loadType = AudioClipLoadType.Streaming;
          settings.compressionFormat = AudioCompressionFormat.Vorbis;
          settings.quality = 0.7f;
          importer.defaultSampleSettings = settings;
          importer.SaveAndReimport();
        }
        clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
      }
      return clips;
    }

    private static AudioClip[] Import(string dir, AudioClipLoadType loadType, float quality)
    {
      if (!AssetDatabase.IsValidFolder(dir))
      {
        return new AudioClip[0];
      }
      var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { dir })
        .Select(AssetDatabase.GUIDToAssetPath)
        .OrderBy(p => p)
        .ToArray();
      foreach (var path in paths)
      {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        var changed = !importer.forceToMono || settings.loadType != loadType ||
                      settings.compressionFormat != AudioCompressionFormat.Vorbis || !Mathf.Approximately(settings.quality, quality);
        if (!changed)
        {
          continue;
        }
        importer.forceToMono = true;
        settings.loadType = loadType;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = quality;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
      }
      return paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
    }

    private static void SetArray(SerializedProperty prop, AudioClip[] clips)
    {
      prop.arraySize = clips.Length;
      for (var i = 0; i < clips.Length; i++)
      {
        prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
      }
    }

    private static int SetButtonSound(Button button, string sfxId)
    {
      if (button == null)
      {
        return 0;
      }
      var marker = button.GetComponent<ButtonSound>();
      if (marker == null)
      {
        marker = Undo.AddComponent<ButtonSound>(button.gameObject);
      }
      Undo.RecordObject(marker, "Button Sound");
      marker.Configure(sfxId);
      EditorUtility.SetDirty(marker);
      return 1;
    }
  }
}
