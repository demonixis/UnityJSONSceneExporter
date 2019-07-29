using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SceneExporter : MonoBehaviour
{
    [Serializable]
    public struct UMeshFilter
    {
        public float[] Positions;
        public float[] Normals;
        public float[] UVs;
        public int[] Indices;
        public int SubMeshCount;
    }

    [Serializable]
    public struct UMaterial
    {
        public float[] Scale;
        public string MainTexture;
    }

    [Serializable]
    public struct URenderer
    {
        public string Name;
        public UMeshFilter MeshFilter;
        public UMaterial Material;
    }

    [Serializable]
    public struct UCollider
    {
        public float[] Min;
        public float[] Max;
        public float Radius;
    }

    [Serializable]
    public struct UGameObject
    {
        public int ID;
        public string Name;
        public int Parent;
        public float[] LocalPosition;
        public float[] LocalRotation;
        public float[] LocalScale;
        public URenderer Renderer;
        public UCollider Collider;
    }

    public bool ExportMeshData = true;

    [ContextMenu("Export")]
    public void Export()
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        var list = new List<UGameObject>();

        foreach (var tr in transforms)
            list.Add(ExportObject(tr));

        var json = JsonConvert.SerializeObject(list.ToArray(), Formatting.Indented);
        var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        path = Path.Combine(path, "map.json");

        File.WriteAllText(path, json);

        Debug.Log($"Exported: {list.Count} objects");
    }

    public UGameObject ExportObject(Transform tr)
    {
        var target = new UGameObject
        {
            ID = tr.GetInstanceID(),
            Name = tr.name,
            Parent = tr.transform.parent?.GetInstanceID() ?? -1,
            LocalPosition = ToFloat3(tr.transform.localPosition),
            LocalRotation = ToFloat3(tr.transform.localRotation.eulerAngles),
            LocalScale = ToFloat3(tr.transform.localScale)
        };

        var collider = tr.GetComponent<Collider>();
        if (collider != null)
        {
            var uc = new UCollider
            {
                Min = ToFloat3(collider.bounds.min),
                Max = ToFloat3(collider.bounds.max)
            };

            var sphere = collider as SphereCollider;
            if (sphere != null)
                uc.Radius = sphere.radius;

            target.Collider = uc;
        }

        var renderer = tr.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var rd = new URenderer
            {
                Name = renderer.name,
                Material = new UMaterial
                {
                    Scale = ToFloat2(renderer.sharedMaterial.mainTextureScale),
                    MainTexture = renderer.sharedMaterial.mainTexture?.name
                }
            };

            if (ExportMeshData)
            {
                var meshFilder = renderer.GetComponent<MeshFilter>();
                var mesh = meshFilder.sharedMesh;

                rd.MeshFilter = new UMeshFilter
                {
                    Positions = ToFloat3(mesh.vertices),
                    Normals = ToFloat3(mesh.normals),
                    UVs = ToFloat2(mesh.uv),
                    Indices = mesh.GetIndices(0),
                    SubMeshCount =  mesh.subMeshCount
                };
            }

            target.Renderer = rd;
        }

        return target;
    }

    public float[] ToFloat2(Vector2 vec) => new[] { vec.x, vec.y };
    public float[] ToFloat3(Vector3 vec) => new[] { vec.x, vec.y, vec.z };

    public float[] ToFloat2(Vector2[] vecs)
    {
        var list = new List<float>();

        foreach (var vec in vecs)
        {
            list.Add(vec.x);
            list.Add(vec.y);
        }

        return list.ToArray();
    }

    public float[] ToFloat3(Vector3[] vecs)
    {
        var list = new List<float>();

        foreach (var vec in vecs)
        {
            list.Add(vec.x);
            list.Add(vec.y);
            list.Add(vec.z);
        }

        return list.ToArray();
    }
}
