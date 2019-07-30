using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Demonixis.UnityJSONSceneExporter
{
#if UNITY_EDITOR
    [CustomEditor(typeof(SceneExporter))]
    public class SceneExporterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var script = (SceneExporter)target;

            if (GUILayout.Button("Export"))
                script.Export();
        }
    }
#endif

    public class SceneExporter : MonoBehaviour
    {
        [SerializeField]
        private bool m_LogEnabled = true;
        [SerializeField]
        public bool m_ExportMeshData = true;
        [SerializeField]
        private Formatting m_JSONFormat = Formatting.Indented;
        [SerializeField]
        private string m_ExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        [SerializeField]
        private string m_ExportFilename = Application.productName;

        [ContextMenu("Export")]
        public void Export()
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            var list = new List<UGameObject>();

            foreach (var tr in transforms)
            {
                list.Add(ExportObject(tr));

                if (m_LogEnabled)
                    Debug.Log($"Exporter: {tr.name}");
            }

            var json = JsonConvert.SerializeObject(list.ToArray(), m_JSONFormat);
            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path = Path.Combine(m_ExportPath, $"{m_ExportFilename}.json");

            File.WriteAllText(path, json);

            if (m_LogEnabled)
                Debug.Log($"Exported: {list.Count} objects");
        }

        public UGameObject ExportObject(Transform tr)
        {
            var uGameObject = new UGameObject
            {
                ID = tr.GetInstanceID(),
                Name = tr.name,
                Parent = tr.transform.parent?.GetInstanceID() ?? -1,
                IsStatic = tr.gameObject.isStatic,
                IsActive = tr.gameObject.activeSelf,
                LocalPosition = ToFloat3(tr.transform.localPosition),
                LocalRotation = ToFloat3(tr.transform.localRotation.eulerAngles),
                LocalScale = ToFloat3(tr.transform.localScale)
            };

            var collider = tr.GetComponent<Collider>();
            if (collider != null)
            {
                var uCollider = new UCollider
                {
                    Min = ToFloat3(collider.bounds.min),
                    Max = ToFloat3(collider.bounds.max),
                    Enabled = collider.enabled
                };

                var sphere = collider as SphereCollider;
                if (sphere != null)
                    uCollider.Radius = sphere.radius;

                uGameObject.Collider = uCollider;
            }

            var light = tr.GetComponent<Light>();
            if (light != null)
            {
                var uLight = new ULight
                {
                    Intensity = light.intensity,
                    Radius = light.range,
                    Color = ToFloat4(light.color),
                    Angle = light.spotAngle,
                    ShadowsEnabled = light.shadows != LightShadows.None,
                    Enabled = light.enabled,
                    Type = (int)light.type
                };

                uGameObject.Light = uLight;
            }

            var reflectionProbe = tr.GetComponent<ReflectionProbe>();
            if (reflectionProbe != null)
            {
                var uReflectionProbe = new UReflectionProbe
                {
                    BoxSize = ToFloat3(reflectionProbe.size),
                    BoxMin = ToFloat3(reflectionProbe.bounds.min),
                    BoxMax = ToFloat3(reflectionProbe.bounds.max),
                    Intensity = reflectionProbe.intensity,
                    ClipPlanes = new []
                    {
                        reflectionProbe.nearClipPlane,
                        reflectionProbe.farClipPlane
                    },
                    Enabled = reflectionProbe.enabled,
                    IsBacked = reflectionProbe.refreshMode != ReflectionProbeRefreshMode.EveryFrame,
                    Resolution = reflectionProbe.resolution
                };

                uGameObject.ReflectionProbe = uReflectionProbe;
            }

            var renderer = tr.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var uRenderer = new UMeshRenderer
                {
                    Name = renderer.name,
                    Materials = new UMaterial[renderer.sharedMaterials.Length],
                    Enabled = renderer.enabled
                };

                for (var i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    uRenderer.Materials[i] = new UMaterial
                    {
                        Scale = ToFloat2(renderer.sharedMaterials[i].mainTextureScale),
                        Offset = ToFloat2(renderer.sharedMaterials[i].mainTextureOffset),
                        MainTexture = renderer.sharedMaterials[i].mainTexture?.name
                    };
                }

                if (m_ExportMeshData)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    var mesh = meshFilter.sharedMesh;

                    var uMeshFilter = new UMeshFilter
                    {
                        Positions = ToFloat3(mesh.vertices),
                        Normals = ToFloat3(mesh.normals),
                        UVs = ToFloat2(mesh.uv),
                        Indices = mesh.GetIndices(0),
                        SubMeshCount = mesh.subMeshCount,
                        Triangles = new int[mesh.subMeshCount][],
                        MeshFormat = (int)mesh.indexFormat
                    };

                    for (var i = 0; i < mesh.subMeshCount; i++)
                        uMeshFilter.Triangles[i] = mesh.GetTriangles(i);

                    uRenderer.MeshFilter = uMeshFilter;
                }

                uGameObject.Renderer = uRenderer;
            }

            return uGameObject;
        }

        public float[] ToFloat2(Vector2 vec) => new[] { vec.x, vec.y };
        public float[] ToFloat3(Vector3 vec) => new[] { vec.x, vec.y, vec.z };
        public float[] ToFloat4(Color c) => new[] { c.r, c.g, c.b, c.a };

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
}