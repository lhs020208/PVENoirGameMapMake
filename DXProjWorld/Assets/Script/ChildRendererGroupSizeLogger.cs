using System.Text;
using UnityEngine;

[ExecuteAlways]
public class ChildRendererGroupSizeLogger : MonoBehaviour
{
    [ContextMenu("Log Combined Child Mesh Size XZ")]
    public void LogCombinedChildMeshSizeXZ()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

        bool hasAny = false;
        Bounds combinedBounds = default;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r.gameObject == gameObject) continue; // 루트 본인 제외. 포함하려면 제거

            if (!hasAny)
            {
                combinedBounds = r.bounds;
                hasAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasAny)
        {
            Debug.LogWarning($"[{name}] 자식 MeshRenderer를 찾지 못했습니다.", this);
            return;
        }

        float sizeX = combinedBounds.size.x;
        float sizeZ = combinedBounds.size.z;

        var sb = new StringBuilder();
        sb.AppendLine($"[ChildRendererGroupSizeLogger] Root: {name}");
        sb.AppendLine("=== Combined Child Mesh Bounds ===");
        sb.AppendLine($"Center : {combinedBounds.center}");
        sb.AppendLine($"Size   : {combinedBounds.size}");
        sb.AppendLine($"X Length: {sizeX:F4}");
        sb.AppendLine($"Z Length: {sizeZ:F4}");
        sb.AppendLine($"Min    : {combinedBounds.min}");
        sb.AppendLine($"Max    : {combinedBounds.max}");

        Debug.Log(sb.ToString(), this);
    }
}