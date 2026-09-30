using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   Art/Models의 3D 모델(한 덩어리)을 팔·날개·귀 같은 부위로 잘라, 관절(어깨, 등, 귀뿌리)을 중심으로 돌 수 있게 만든다.
  ///   뼈대(리깅) 없이 부위를 통째로 돌리는 방식이라 말랑한 인형 같은 캐릭터에 맞다.
  ///   자르는 기준은 모델을 발바닥~머리끝 = 0~1(y), 좌우·앞뒤 끝 = -1~1(x, z)로 맞춘 좌표다. 모델은 +Z(앞)를 보고 있다.
  ///   결과는 열려 있는 Main 씬의 양치 화면 전투(BattleView)에 연결한다. 캐릭터마다 전용 카메라가 UI의 RawImage로 그린다.
  /// </summary>
  public static class PuppetBuilder
  {
    private const string ModelDir = "Assets/BrushGame/Art/Models";
    private const string LayerName = "Battle3D";
    private static readonly Vector3 StageOrigin = new Vector3(0f, -500f, 0f);
    private const float CameraSize = 0.62f;

    private sealed class Part
    {
      public string Name;
      public string Parent;
      /// <summary>관절 위치 (맞춘 좌표)</summary>
      public Vector3 Pivot;
      /// <summary>삼각형 가운데가 이 부위인가 (맞춘 좌표)</summary>
      public Func<Vector3, bool> Match;
    }

    [MenuItem("BrushGame/Build 3D Battle Puppets")]
    public static void BuildMenu()
    {
      var battle = Object.FindFirstObjectByType<BattleView>(FindObjectsInactive.Include);
      if (battle == null)
      {
        Debug.LogWarning("[BrushGame] Main 씬을 열고 실행하세요.");
        return;
      }
      var layer = LayerMask.NameToLayer(LayerName);
      if (layer < 0)
      {
        Debug.LogWarning($"[BrushGame] Tags and Layers에 '{LayerName}' 레이어를 먼저 추가하세요.");
        return;
      }

      var old = GameObject.Find("Battle3D");
      if (old != null)
      {
        Undo.DestroyObjectImmediate(old);
      }
      var root = new GameObject("Battle3D");
      Undo.RegisterCreatedObjectUndo(root, "Build 3D Battle Puppets");
      root.transform.position = StageOrigin;

      var so = new SerializedObject(battle);
      var heroTarget = TargetFor((Image)so.FindProperty("_heroImage").objectReferenceValue, "Hero3D");
      var villainTarget = TargetFor((Image)so.FindProperty("_villainImage").objectReferenceValue, "Villain3D");

      var hero = BuildHero(root.transform, layer, heroTarget);
      var villain = BuildVillain(root.transform, layer, villainTarget);
      so.FindProperty("_heroPuppet").objectReferenceValue = hero;
      so.FindProperty("_villainPuppet").objectReferenceValue = villain;
      so.ApplyModifiedProperties();

      // 메인 카메라는 3D 무대를 그리지 않는다
      foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (!cam.transform.IsChildOf(root.transform))
        {
          Undo.RecordObject(cam, "Build 3D Battle Puppets");
          cam.cullingMask &= ~(1 << layer);
        }
      }

      var scene = battle.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 3D 요정·충치균을 만들어 전투에 연결했습니다.");
    }

    // ---------- 캐릭터별 자르기 ----------

    private static HeroPuppet BuildHero(Transform root, int layer, RawImage target)
    {
      var parts = new[]
      {
        new Part { Name = "EarL", Parent = "Head", Pivot = new Vector3(-0.3f, 0.83f, 0f), Match = p => p.y > 0.83f && p.x < 0f },
        new Part { Name = "EarR", Parent = "Head", Pivot = new Vector3(0.3f, 0.83f, 0f), Match = p => p.y > 0.83f && p.x >= 0f },
        new Part { Name = "WingL", Parent = "Body", Pivot = new Vector3(-0.1f, 0.42f, -0.35f), Match = p => IsHeroWing(p) && p.x < 0f },
        new Part { Name = "WingR", Parent = "Body", Pivot = new Vector3(0.1f, 0.42f, -0.35f), Match = p => IsHeroWing(p) && p.x >= 0f },
        new Part { Name = "Head", Parent = "Body", Pivot = new Vector3(0f, 0.42f, 0f), Match = p => p.y > 0.425f },
        new Part { Name = "ArmR", Parent = "Body", Pivot = new Vector3(-0.63f, 0.36f, 0.15f), Match = p => IsHeroArm(p, -1f) },
        new Part { Name = "ArmL", Parent = "Body", Pivot = new Vector3(0.63f, 0.36f, 0.15f), Match = p => IsHeroArm(p, 1f) },
        new Part { Name = "Body", Parent = null, Pivot = Vector3.zero, Match = p => true },
      };
      var go = BuildPuppet("Hero", root, new Vector3(0f, 0f, 0f), parts, layer, out var bones);
      var puppet = go.AddComponent<HeroPuppet>();

      // 칫솔은 충치균 쪽 손(모델의 -X, 캐릭터의 오른손)에
      var hand = Normalized(new Vector3(-0.86f, 0.25f, 0.2f), bones["__scale"]);
      AttachProp(bones["ArmR"], "Toothbrush", hand, 0.42f, Quaternion.Euler(0f, 0f, 90f), 0.35f, layer);

      Wire(puppet, target, go, -25f, new Dictionary<string, string>
      {
        { "_head", "Head" }, { "_earL", "EarL" }, { "_earR", "EarR" }, { "_wingL", "WingL" }, { "_wingR", "WingR" },
        { "_brushArm", "ArmR" }, { "_otherArm", "ArmL" },
      }, bones);
      return puppet;
    }

    // 등 뒤 날개. 머리 높이(0.42 위)에서는 뒤통수를 피해 더 뒤쪽만
    private static bool IsHeroWing(Vector3 p) =>
      p.y > 0.30f && p.y < 0.54f && (p.z < -0.72f || (p.z < -0.33f && p.y < 0.42f));

    /// <summary>어깨에서 손까지 캡슐 안 (맞춘 좌표 → 모델 단위로 거리를 잰다)</summary>
    private static bool IsHeroArm(Vector3 p, float side)
    {
      if (p.x * side < 0.55f || p.y > 0.40f || p.y < 0.18f || p.z < -0.35f)
      {
        return false;
      }
      var shoulder = new Vector3(0.63f * side, 0.36f, 0.15f);
      var hand = new Vector3(0.87f * side, 0.25f, 0.2f);
      return DistanceToSegment(Scale(p), Scale(shoulder), Scale(hand)) < 0.055f;
    }

    private static VillainPuppet BuildVillain(Transform root, int layer, RawImage target)
    {
      var parts = new[]
      {
        new Part { Name = "WingR", Parent = "Body", Pivot = new Vector3(-0.5f, 0.57f, -0.2f), Match = p => IsVillainWing(p) && p.x < 0f },
        new Part { Name = "WingL", Parent = "Body", Pivot = new Vector3(0.5f, 0.57f, -0.2f), Match = p => IsVillainWing(p) && p.x >= 0f },
        new Part { Name = "ArmR", Parent = "Body", Pivot = new Vector3(-0.5f, 0.5f, 0f), Match = p => IsVillainArm(p) && p.x < 0f },
        new Part { Name = "ArmL", Parent = "Body", Pivot = new Vector3(0.5f, 0.5f, 0f), Match = p => IsVillainArm(p) && p.x >= 0f },
        new Part { Name = "Tail", Parent = "Body", Pivot = new Vector3(0f, 0.27f, -0.72f), Match = p => p.z < -0.76f && p.y < 0.38f },
        new Part { Name = "Body", Parent = null, Pivot = Vector3.zero, Match = p => true },
      };
      var go = BuildPuppet("Villain", root, new Vector3(10f, 0f, 0f), parts, layer, out var bones);
      var puppet = go.AddComponent<VillainPuppet>();

      // 사탕 포크는 영웅 반대쪽 손(모델의 -X)에
      var hand = Normalized(new Vector3(-0.68f, 0.25f, 0.1f), bones["__scale"]);
      AttachProp(bones["ArmR"], "CandyFork", hand, 0.5f, Quaternion.identity, 0.3f, layer);

      Wire(puppet, target, go, 25f, new Dictionary<string, string>
      {
        { "_wingL", "WingL" }, { "_wingR", "WingR" }, { "_forkArm", "ArmR" }, { "_otherArm", "ArmL" }, { "_tail", "Tail" },
      }, bones);
      return puppet;
    }

    // 날개: 몸통 옆으로 뻗은 부분 (팔보다 위)
    private static bool IsVillainWing(Vector3 p) =>
      (Mathf.Abs(p.x) > 0.5f && p.y > 0.51f) || (Mathf.Abs(p.x) > 0.8f && p.y > 0.46f);

    // 팔: 날개 아래, 몸통 옆
    private static bool IsVillainArm(Vector3 p) => Mathf.Abs(p.x) > 0.47f && p.y > 0.2f && p.y <= 0.51f;

    // ---------- 공통 ----------

    /// <summary>현재 캐릭터의 맞춘 좌표 → 모델 단위 변환용 (자를 때 거리 계산에 쓴다)</summary>
    private static Vector3 _unit = Vector3.one;

    private static Vector3 Scale(Vector3 n) => Vector3.Scale(n, _unit);

    private static Vector3 Normalized(Vector3 n, Transform scaleHolder) => Vector3.Scale(n, scaleHolder.localScale);

    private static GameObject BuildPuppet(string name, Transform root, Vector3 stagePos, Part[] parts, int layer, out Dictionary<string, Transform> bones)
    {
      var modelPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Model", new[] { $"{ModelDir}/{name}" })[0]);
      var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
      var material = AssetDatabase.LoadAssetAtPath<Material>($"{ModelDir}/{name}/{name}.mat");

      // 모델 전체를 한 좌표계(모델 루트 기준)로 모은다
      var verts = new List<Vector3>();
      var normals = new List<Vector3>();
      var uvs = new List<Vector2>();
      var tris = new List<int>();
      foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
      {
        var m = mf.sharedMesh;
        // 최상위에도 축 보정 회전이 있을 수 있어서(Meshy) 전체 변환을 쓴다. 에셋은 원점에 있다
        var toRoot = mf.transform.localToWorldMatrix;
        var start = verts.Count;
        foreach (var v in m.vertices)
        {
          verts.Add(toRoot.MultiplyPoint3x4(v));
        }
        foreach (var n in m.normals)
        {
          normals.Add(toRoot.MultiplyVector(n).normalized);
        }
        uvs.AddRange(m.uv);
        foreach (var t in m.triangles)
        {
          tris.Add(start + t);
        }
      }

      // 발바닥 가운데를 원점으로, 키를 1로
      var bounds = new Bounds(verts[0], Vector3.zero);
      foreach (var v in verts)
      {
        bounds.Encapsulate(v);
      }
      var scale = 1f / bounds.size.y;
      var ext = bounds.extents * scale;
      _unit = new Vector3(ext.x, 1f, ext.z);
      for (var i = 0; i < verts.Count; i++)
      {
        var v = verts[i] - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        verts[i] = v * scale;
      }

      // 삼각형 가운데로 부위를 정한다 (위에 있는 규칙이 먼저)
      var partTris = new List<int>[parts.Length];
      for (var i = 0; i < parts.Length; i++)
      {
        partTris[i] = new List<int>();
      }
      for (var t = 0; t < tris.Count; t += 3)
      {
        var c = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f;
        var n = new Vector3(c.x / ext.x, c.y, c.z / ext.z);
        for (var i = 0; i < parts.Length; i++)
        {
          if (parts[i].Match(n))
          {
            partTris[i].Add(tris[t]);
            partTris[i].Add(tris[t + 1]);
            partTris[i].Add(tris[t + 2]);
            break;
          }
        }
      }

      // 부위 메시는 한 에셋 파일에 모아 둔다 (첫 부위가 본체, 나머지는 그 안에)
      var assetPath = $"{ModelDir}/{name}/{name}Parts.asset";
      AssetDatabase.DeleteAsset(assetPath);
      Mesh holder = null;

      var puppet = new GameObject(name);
      puppet.transform.SetParent(root, false);
      puppet.transform.localPosition = stagePos;
      var scaleHolder = new GameObject("__scale").transform;
      scaleHolder.SetParent(puppet.transform, false);
      scaleHolder.localScale = _unit;
      bones = new Dictionary<string, Transform> { { "__scale", scaleHolder } };

      var pivots = new Dictionary<string, Vector3>();
      foreach (var p in parts)
      {
        pivots[p.Name] = Vector3.Scale(p.Pivot, _unit);
      }
      // 부모가 먼저 만들어지도록 부모 없는 것부터
      var ordered = new List<int>();
      var done = new HashSet<string>();
      while (ordered.Count < parts.Length)
      {
        for (var i = 0; i < parts.Length; i++)
        {
          if (!done.Contains(parts[i].Name) && (parts[i].Parent == null || done.Contains(parts[i].Parent)))
          {
            ordered.Add(i);
            done.Add(parts[i].Name);
          }
        }
      }
      foreach (var i in ordered)
      {
        var part = parts[i];
        var pivot = pivots[part.Name];
        var mesh = BuildPartMesh(part.Name, verts, normals, uvs, partTris[i], pivot);
        if (holder == null)
        {
          holder = mesh;
          AssetDatabase.CreateAsset(mesh, assetPath);
        }
        else
        {
          AssetDatabase.AddObjectToAsset(mesh, holder);
        }

        var go = new GameObject(part.Name);
        var parent = part.Parent == null ? puppet.transform : bones[part.Parent];
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pivot - (part.Parent == null ? Vector3.zero : pivots[part.Parent]);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        bones[part.Name] = go.transform;
      }
      AssetDatabase.SaveAssets();
      Debug.Log($"[BrushGame] {name}: " + string.Join(", ", Array.ConvertAll(parts, p => $"{p.Name} {partTris[Array.IndexOf(parts, p)].Count / 3}")));

      SetLayer(puppet, layer);
      return puppet;
    }

    private static Mesh BuildPartMesh(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, Vector3 pivot)
    {
      var map = new Dictionary<int, int>();
      var v = new List<Vector3>();
      var n = new List<Vector3>();
      var uv = new List<Vector2>();
      var t = new List<int>(tris.Count);
      foreach (var index in tris)
      {
        if (!map.TryGetValue(index, out var mapped))
        {
          mapped = v.Count;
          map[index] = mapped;
          v.Add(verts[index] - pivot);
          n.Add(normals.Count > index ? normals[index] : Vector3.up);
          uv.Add(uvs.Count > index ? uvs[index] : Vector2.zero);
        }
        t.Add(mapped);
      }
      var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
      mesh.SetVertices(v);
      mesh.SetNormals(n);
      mesh.SetUVs(0, uv);
      mesh.SetTriangles(t, 0);
      mesh.RecalculateBounds();
      mesh.RecalculateTangents();
      return mesh;
    }

    /// <summary>소품 모델(칫솔, 포크)을 손에 쥐여 준다. grip: 소품 길이 중 손이 쥐는 곳 (0 = 아래 끝)</summary>
    private static void AttachProp(Transform arm, string name, Vector3 handInPuppet, float length, Quaternion rotation, float grip, int layer)
    {
      var modelPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Model", new[] { $"{ModelDir}/{name}" })[0]);
      var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
      var prop = (GameObject)PrefabUtility.InstantiatePrefab(model);
      prop.name = name;
      var material = AssetDatabase.LoadAssetAtPath<Material>($"{ModelDir}/{name}/{name}.mat");
      foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
      {
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
      }

      // 손 자리(holder)를 먼저 잡고, 소품을 세운 뒤(rotation) 가장 긴 쪽을 length로 맞추고, 쥐는 곳이 손에 오게 옮긴다
      var puppet = arm.parent;
      while (puppet.parent != null && puppet.parent.name != "Battle3D")
      {
        puppet = puppet.parent;
      }
      var holder = new GameObject($"{name}Grip").transform;
      holder.SetParent(arm, false);
      holder.position = puppet.TransformPoint(handInPuppet);
      prop.transform.SetParent(holder, false);
      prop.transform.localRotation = rotation * prop.transform.localRotation;
      var b = BoundsOf(prop);
      prop.transform.localScale *= length / Mathf.Max(b.size.x, b.size.y, b.size.z);
      b = BoundsOf(prop);
      var gripPoint = new Vector3(b.center.x, b.min.y + b.size.y * grip, b.center.z);
      prop.transform.position += holder.position - gripPoint;
      SetLayer(prop, layer);
    }

    private static Bounds BoundsOf(GameObject go)
    {
      var rs = go.GetComponentsInChildren<Renderer>(true);
      var b = rs[0].bounds;
      foreach (var r in rs)
      {
        b.Encapsulate(r.bounds);
      }
      return b;
    }

    /// <summary>퍼펫 컴포넌트에 부위, 전용 카메라, 조명, UI 자리를 연결한다</summary>
    private static void Wire(PuppetBase puppet, RawImage target, GameObject go, float yaw, Dictionary<string, string> fields, Dictionary<string, Transform> bones)
    {
      Object.DestroyImmediate(bones["__scale"].gameObject);
      var layer = go.layer;
      var stage = go.transform.parent;

      var camGo = new GameObject($"{go.name}Camera");
      camGo.transform.SetParent(stage, false);
      camGo.transform.localPosition = go.transform.localPosition + new Vector3(0f, CameraSize - 0.02f, 5f);
      camGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
      var cam = camGo.AddComponent<Camera>();
      cam.orthographic = true;
      cam.orthographicSize = CameraSize;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
      cam.cullingMask = 1 << layer;
      cam.nearClipPlane = 0.1f;
      cam.farClipPlane = 20f;
      cam.allowMSAA = false;
      cam.allowHDR = false;
      camGo.layer = layer;

      var key = new GameObject($"{go.name}KeyLight").AddComponent<Light>();
      key.transform.SetParent(camGo.transform, false);
      key.transform.localRotation = Quaternion.Euler(30f, -25f, 0f);
      key.type = LightType.Directional;
      key.intensity = 1.05f;
      key.cullingMask = 1 << layer;
      var fill = new GameObject($"{go.name}FillLight").AddComponent<Light>();
      fill.transform.SetParent(camGo.transform, false);
      fill.transform.localRotation = Quaternion.Euler(10f, 40f, 0f);
      fill.type = LightType.Directional;
      fill.intensity = 0.45f;
      fill.color = new Color(1f, 0.92f, 1f);
      fill.cullingMask = 1 << layer;

      var so = new SerializedObject(puppet);
      so.FindProperty("_camera").objectReferenceValue = cam;
      so.FindProperty("_target").objectReferenceValue = target;
      so.FindProperty("_facingYaw").floatValue = yaw;
      foreach (var f in fields)
      {
        so.FindProperty(f.Key).objectReferenceValue = bones[f.Value];
      }
      so.ApplyModifiedPropertiesWithoutUndo();
      go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
      puppet.gameObject.SetActive(false);
      camGo.SetActive(false);
    }

    /// <summary>2D 그림 자리 안에 3D 캐릭터를 보여줄 RawImage를 (없으면) 만든다</summary>
    private static RawImage TargetFor(Image image, string name)
    {
      var existing = image.transform.Find(name);
      if (existing != null)
      {
        return existing.GetComponent<RawImage>();
      }
      var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
      Undo.RegisterCreatedObjectUndo(go, "Build 3D Battle Puppets");
      var rt = (RectTransform)go.transform;
      rt.SetParent(image.transform, false);
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      var raw = go.GetComponent<RawImage>();
      raw.raycastTarget = false;
      raw.enabled = false;
      return raw;
    }

    private static void SetLayer(GameObject go, int layer)
    {
      foreach (var t in go.GetComponentsInChildren<Transform>(true))
      {
        t.gameObject.layer = layer;
      }
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
      var ab = b - a;
      var k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
      return Vector3.Distance(p, a + ab * k);
    }
  }
}
