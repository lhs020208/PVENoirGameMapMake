using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class BuildingLayoutPlacer : MonoBehaviour
{
    [Serializable]
    public class BuildingPrefabEntry
    {
        public int buildingNumber;
        public GameObject prefab;
    }

    [Serializable]
    public struct BuildingTemplateInfo
    {
        public int buildingNumber;
        public Vector3 centerOffset; // 루트 기준 "전체 덩어리 center"
    }

    [Serializable]
    public struct PlacementInfo
    {
        public int buildingNumber;
        public Vector2 targetCenterXZ;   // 배치되어야 하는 "정중앙" 좌표
        public float clockwiseYawDeg;    // 시계방향 각도
    }

    [Header("배치할 프리팹 등록 (1~9)")]
    public List<BuildingPrefabEntry> buildingPrefabs = new List<BuildingPrefabEntry>();

    [Header("생성될 부모 (비우면 이 오브젝트 밑에 생성)")]
    public Transform placedRoot;

    [Header("루트 Y 위치")]
    public float rootY = 0f;

    [Header("기존 생성물 삭제 후 다시 생성")]
    public bool clearExistingFirst = true;

    [SerializeField, HideInInspector]
    private List<BuildingTemplateInfo> templateInfos = new List<BuildingTemplateInfo>();

    [SerializeField, HideInInspector]
    private List<PlacementInfo> placements = new List<PlacementInfo>();

    private void Reset()
    {
        LoadDefaultTemplateInfos();
        LoadDefaultPlacements();
    }

#if UNITY_EDITOR
    [ContextMenu("Place Buildings From Table")]
    public void PlaceBuildingsFromTable()
    {
        if (templateInfos == null || templateInfos.Count == 0)
            LoadDefaultTemplateInfos();

        if (placements == null || placements.Count == 0)
            LoadDefaultPlacements();

        Transform parent = placedRoot != null ? placedRoot : transform;

        if (clearExistingFirst)
        {
            var toDelete = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
                toDelete.Add(parent.GetChild(i).gameObject);

            foreach (var go in toDelete)
                Undo.DestroyObjectImmediate(go);
        }

        var prefabMap = new Dictionary<int, GameObject>();
        foreach (var entry in buildingPrefabs)
        {
            if (entry != null && entry.prefab != null)
                prefabMap[entry.buildingNumber] = entry.prefab;
        }

        var infoMap = new Dictionary<int, BuildingTemplateInfo>();
        foreach (var info in templateInfos)
            infoMap[info.buildingNumber] = info;

        foreach (var p in placements)
        {
            if (!prefabMap.TryGetValue(p.buildingNumber, out var prefab) || prefab == null)
            {
                Debug.LogError($"빌딩 {p.buildingNumber} 프리팹이 등록되지 않았습니다.", this);
                continue;
            }

            if (!infoMap.TryGetValue(p.buildingNumber, out var info))
            {
                Debug.LogError($"빌딩 {p.buildingNumber} center 정보가 없습니다.", this);
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                Debug.LogError($"빌딩 {p.buildingNumber} 프리팹 인스턴스화 실패", this);
                continue;
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Place Building {p.buildingNumber}");

            // 표의 d는 "시계방향"이므로 Unity Yaw(반시계 +)로 바꾸기 위해 부호 반전
            Quaternion rot = Quaternion.Euler(0f, -p.clockwiseYawDeg, 0f);

            // 루트 기준 center offset을 회전시켜서,
            // "정중앙이 targetCenterXZ"가 되도록 루트 위치를 역산
            Vector3 rotatedCenterOffset = rot * info.centerOffset;

            Vector3 worldCenterTarget = new Vector3(
                p.targetCenterXZ.x,
                rootY + rotatedCenterOffset.y,
                p.targetCenterXZ.y
            );

            Vector3 rootPosition = worldCenterTarget - rotatedCenterOffset;

            instance.transform.SetPositionAndRotation(rootPosition, rot);
            instance.name = $"building_{p.buildingNumber}_{p.targetCenterXZ.x}_{p.targetCenterXZ.y}";
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("빌딩 배치 완료", this);
    }
#endif

    private void LoadDefaultTemplateInfos()
    {
        templateInfos = new List<BuildingTemplateInfo>
        {
            new BuildingTemplateInfo { buildingNumber = 1, centerOffset = new Vector3(-12.32f,  9.47f, -6.66f) },
            new BuildingTemplateInfo { buildingNumber = 2, centerOffset = new Vector3( -4.50f,  8.60f, -3.34f) },
            new BuildingTemplateInfo { buildingNumber = 3, centerOffset = new Vector3(  3.78f,  9.09f, -7.66f) },
            new BuildingTemplateInfo { buildingNumber = 4, centerOffset = new Vector3(  5.00f,  6.87f, -7.50f) },
            new BuildingTemplateInfo { buildingNumber = 5, centerOffset = new Vector3(  3.58f,  8.75f, -6.56f) },
            new BuildingTemplateInfo { buildingNumber = 6, centerOffset = new Vector3( -5.00f,  9.59f, -6.88f) },
            new BuildingTemplateInfo { buildingNumber = 7, centerOffset = new Vector3( -4.88f, 10.62f, -6.33f) },
            new BuildingTemplateInfo { buildingNumber = 8, centerOffset = new Vector3( -7.50f, 10.96f, 12.31f) },
            new BuildingTemplateInfo { buildingNumber = 9, centerOffset = new Vector3(  0.33f, 10.98f, -4.28f) },
        };
    }

    private void LoadDefaultPlacements()
    {
        placements = new List<PlacementInfo>
        {
            new PlacementInfo { buildingNumber = 5, targetCenterXZ = new Vector2( 11f,   9f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 8, targetCenterXZ = new Vector2( 21f,  39f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 1, targetCenterXZ = new Vector2( 16f,  77f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 3, targetCenterXZ = new Vector2( 15f, 118f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 8, targetCenterXZ = new Vector2( 11f, 185f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 6, targetCenterXZ = new Vector2( 51f,  23f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 7, targetCenterXZ = new Vector2( 52f,  26f), clockwiseYawDeg =  20f },
            new PlacementInfo { buildingNumber = 2, targetCenterXZ = new Vector2( 58f,  99f), clockwiseYawDeg =  21f },
            new PlacementInfo { buildingNumber = 3, targetCenterXZ = new Vector2( 44f, 155f), clockwiseYawDeg = 139f },
            new PlacementInfo { buildingNumber = 9, targetCenterXZ = new Vector2( 46f, 192f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 1, targetCenterXZ = new Vector2( 85f, 189f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 7, targetCenterXZ = new Vector2( 97f, 156f), clockwiseYawDeg = 341f },
            new PlacementInfo { buildingNumber = 9, targetCenterXZ = new Vector2( 78f, 127f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 5, targetCenterXZ = new Vector2( 87f,  63f), clockwiseYawDeg = 107f },
            new PlacementInfo { buildingNumber = 2, targetCenterXZ = new Vector2( 81f,  15f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 3, targetCenterXZ = new Vector2(112f,  11f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 6, targetCenterXZ = new Vector2(113f,  45f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 1, targetCenterXZ = new Vector2(115f,  88f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 8, targetCenterXZ = new Vector2(117f, 122f), clockwiseYawDeg =  97f },
            new PlacementInfo { buildingNumber = 2, targetCenterXZ = new Vector2(126f, 185f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 9, targetCenterXZ = new Vector2(153f, 188f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 3, targetCenterXZ = new Vector2(184f, 188f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 6, targetCenterXZ = new Vector2(150f, 155f), clockwiseYawDeg = 325f },
            new PlacementInfo { buildingNumber = 7, targetCenterXZ = new Vector2(180f, 154f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 8, targetCenterXZ = new Vector2(188f, 121f), clockwiseYawDeg =   0f },
            new PlacementInfo { buildingNumber = 5, targetCenterXZ = new Vector2(154f, 104f), clockwiseYawDeg =  43f },
            new PlacementInfo { buildingNumber = 1, targetCenterXZ = new Vector2(188f,  74f), clockwiseYawDeg =  90f },
            new PlacementInfo { buildingNumber = 8, targetCenterXZ = new Vector2(145f,  51f), clockwiseYawDeg = 288f },
            new PlacementInfo { buildingNumber = 4, targetCenterXZ = new Vector2(183f,  41f), clockwiseYawDeg =  10f },
            new PlacementInfo { buildingNumber = 9, targetCenterXZ = new Vector2(153f,  28f), clockwiseYawDeg =  36f },
            new PlacementInfo { buildingNumber = 2, targetCenterXZ = new Vector2(184f,  11f), clockwiseYawDeg =  90f },
        };
    }
}