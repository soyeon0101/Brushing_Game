using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BrushGame
{
  /// <summary>
  ///   양치 한 판 기록. 서버 표(brush_sessions)의 열 이름과 같다.
  ///   이름·이메일은 넣지 않고 기기마다 만든 익명 번호(player_id)만 보낸다.
  /// </summary>
  [Serializable]
  public class BrushRecord
  {
    public string player_id;
    public int age;
    public string character_id;
    public string meal;
    public string played_on;
    public bool completed;
    public float session_sec;
    public float brushing_sec;
    public float idle_sec;
    public float hand_lost_sec;
    public float too_fast_sec;
    public int too_fast_count;
    public int strokes;
    public int zone_seconds;
    public float[] zone_brush_sec = new float[4];
    public float calib_swing;
    public float calib_rate;
    public string input_kind;
    public string app_version;
  }

  /// <summary>
  ///   양치 기록을 Supabase 표에 한 줄씩 보낸다. 게임 키는 "추가만 가능"이라 읽기·수정·삭제는 막혀 있다.
  ///   인터넷이 없거나 서버 오류면 저장 파일에 남겨 두고 다음 판이 끝날 때 다시 보낸다.
  /// </summary>
  public static class BrushRecordUploader
  {
    private const string Url = "https://btnogymghqiyrslxutmo.supabase.co/rest/v1/brush_sessions";
    // 공개용(publishable) 키. 게임에 넣어도 되는 키이고, 표 권한은 서버에서 "추가만"으로 막는다
    private const string Key = "sb_publishable_Qr4c7u1nqcuLUYYlYyr6ig_5tmZwtwo";
    private const int MaxPending = 50;

    private static readonly HashSet<string> Sending = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Sending.Clear();

    /// <summary>이번 판 기록을 보내고, 전에 못 보낸 기록도 같이 다시 보낸다</summary>
    public static void Send(BrushRecord record)
    {
      var pending = SaveStore.Data.pendingRecords;
      pending.Add(JsonUtility.ToJson(record));
      if (pending.Count > MaxPending)
      {
        pending.RemoveRange(0, pending.Count - MaxPending);
      }
      SaveStore.Save();

      foreach (var json in pending.ToArray())
      {
        Post(json);
      }
    }

    private static void Post(string json)
    {
      if (!Sending.Add(json))
      {
        return;
      }
      var request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbPOST)
      {
        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
        downloadHandler = new DownloadHandlerBuffer(),
        timeout = 15,
      };
      request.SetRequestHeader("apikey", Key);
      request.SetRequestHeader("Content-Type", "application/json");
      request.SetRequestHeader("Prefer", "return=minimal");
      request.SendWebRequest().completed += _ =>
      {
        Sending.Remove(json);
        var code = request.responseCode;
        var sent = request.result == UnityWebRequest.Result.Success;
        // 4xx(429 제외)는 다시 보내도 똑같이 거절되므로 버린다. 인터넷 끊김·서버 오류만 남겨 둔다
        var rejected = !sent && code >= 400 && code < 500 && code != 429;
        if (sent || rejected)
        {
          if (rejected)
          {
            Debug.LogWarning($"[양치 기록] 서버가 거절해서 버림 ({code}): {request.downloadHandler.text}");
          }
          SaveStore.Data.pendingRecords.Remove(json);
          SaveStore.Save();
        }
        else
        {
          Debug.Log($"[양치 기록] 못 보냄, 다음에 다시 보냄: {request.error}");
        }
        request.Dispose();
      };
    }
  }
}
