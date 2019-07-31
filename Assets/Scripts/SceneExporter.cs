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
        private bool m_ExportTextures = false;
        [SerializeField]
        private string m_ExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        [SerializeField]
        private string m_ExportFilename = "GameMap";

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
            var path = Path.Combine(m_ExportPath, $"{m_ExportFilename}.json");

            File.WriteAllText(path, json);

            if (m_LogEnabled)
                Debug.Log($"Exported: {list.Count} objects");
        }

        public UGameObject ExportObject(Transform tr)
        {
            var textures = new List<string>();

            var uGameObject = new UGameObject
            {
                Id = tr.GetInstanceID().ToString(),
                Name = tr.name,
                IsStatic = tr.gameObject.isStatic,
                IsActive = tr.gameObject.activeSelf,
                Transform = new UTransform
                {
                    Parent = tr.transform.parent?.GetInstanceID().ToString() ?? null,
                    LocalPosition = ToFloat3(tr.transform.localPosition),
                    LocalRotation = ToFloat3(tr.transform.localRotation.eulerAngles),
                    LocalScale = ToFloat3(tr.transform.localScale)
                }
            };

            var collider = tr.GetComponent<Collider>();
            if (collider != null)
            {
                var type = ColliderType.Box;
                if (collider is SphereCollider)
                    type = ColliderType.Sphere;
                else if (collider is CapsuleCollider)
                    type = ColliderType.Capsule;
                else if (collider is MeshCollider)
                    type = ColliderType.Mesh;

                var radius = 0.0f;
                if (collider is SphereCollider)
                    radius = ((SphereCollider)collider).radius;

                uGameObject.Collider = new UCollider
                {
                    Min = ToFloat3(collider.bounds.min),
                    Max = ToFloat3(collider.bounds.max),
                    Enabled = collider.enabled,
                    Radius = radius,
                    Type = (int)type
                };
            }

            var light = tr.GetComponent<Light>();
            if (light != null)
            {
                var lightType = 0;
                if (light.type == LightType.Point)
                    lightType = 1;
                else if (light.type == LightType.Spot)
                    lightType = 2;
                else
                    lightType = -1;

                uGameObject.Light = new ULight
                {
                    Intensity = light.intensity,
                    Radius = light.range,
                    Color = ToFloat4(light.color),
                    Angle = light.spotAngle,
                    ShadowsEnabled = light.shadows != LightShadows.None,
                    Enabled = light.enabled,
                    Type = lightType
                };
            }

            var reflectionProbe = tr.GetComponent<ReflectionProbe>();
            if (reflectionProbe != null)
            {
                uGameObject.ReflectionProbe = new UReflectionProbe
                {
                    BoxSize = ToFloat3(reflectionProbe.size),
                    BoxMin = ToFloat3(reflectionProbe.bounds.min),
                    BoxMax = ToFloat3(reflectionProbe.bounds.max),
                    Intensity = reflectionProbe.intensity,
                    ClipPlanes = new[]
                    {
                        reflectionProbe.nearClipPlane,
                        reflectionProbe.farClipPlane
                    },
                    Enabled = reflectionProbe.enabled,
                    IsBacked = reflectionProbe.refreshMode != ReflectionProbeRefreshMode.EveryFrame,
                    Resolution = reflectionProbe.resolution
                };
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
                    var sharedMaterial = renderer.sharedMaterials[i];

                    var normalMap = sharedMaterial.TryGetTexture("_NormalMap", "_BumpMap");
                    var emissiveMap = sharedMaterial.TryGetTexture("_EmissionMap");
                    var metallicMap = sharedMaterial.TryGetTexture("_MetallicGlossMap");
                    var occlusionMap = sharedMaterial.TryGetTexture("_OcclusionMap");

                    uRenderer.Materials[i] = new UMaterial
                    {
                        Scale = ToFloat2(sharedMaterial.mainTextureScale),
                        Offset = ToFloat2(sharedMaterial.mainTextureOffset),
                        ShaderName = sharedMaterial.shader?.name,
                        MainTexture = sharedMaterial.mainTexture?.name,
                        NormalMap = normalMap?.name,
                        AOMap = occlusionMap?.name,
                        EmissionMap = emissiveMap?.name,
                        EmissionColor = ToFloat3(sharedMaterial.TryGetColor("_EmissionColor")),
                        MetalicMap = metallicMap?.name,
                        Cutout = sharedMaterial.TryGetFloat("_Cutoff")
                    };

                    if (m_ExportTextures)
                    {
                        ExportTexture(sharedMaterial.mainTexture, renderer.name, textures);
                        ExportTexture(normalMap, renderer.name, textures);
                        ExportTexture(emissiveMap, renderer.name, textures);
                        ExportTexture(metallicMap, renderer.name, textures);
                        ExportTexture(occlusionMap, renderer.name, textures);
                    }
                }

                if (m_ExportMeshData)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    var mesh = meshFilter.sharedMesh;
                    var subMeshCount = mesh.subMeshCount;

                    UMeshFilter[] filters = new UMeshFilter[subMeshCount];

                    for (var i = 0; i < subMeshCount; i++)
                    {
                        var subMesh = mesh.GetSubmesh(i);

                        filters[i] = new UMeshFilter
                        {
                            Positions = ToFloat3(subMesh.vertices),
                            Normals = ToFloat3(subMesh.normals),
                            UVs = ToFloat2(subMesh.uv),
                            Indices = subMesh.GetIndices(0),
                            MeshFormat = (int)subMesh.indexFormat
                        };
                    }

                    uRenderer.MeshFilters = filters;
                }

                uGameObject.Renderer = uRenderer;
            }

            return uGameObject;
        }

        private void ExportTexture(Texture texture, string folder, List<string> exported)
        {
            if (texture == null)
                return;

            // Already exported?
            var index = exported.IndexOf(texture.name);
            if (index > -1)
            {
                Debug.Log($"Alread exported {texture.name}");
                return;
            }

            if (!texture.isReadable)
            {
                Debug.LogWarning($"The texture {texture.name} is not readable so we can't export it.");
                return;
            }

            var tex2D = (Texture2D)texture;
            var bytes = tex2D.EncodeToPNG();
            var texturePath = Path.Combine(m_ExportPath, "Textures", folder);

            if (!Directory.Exists(texturePath))
                Directory.CreateDirectory(texturePath);

            try
            {
                File.WriteAllBytes(Path.Combine(texturePath, $"{texture.name}.png"), bytes);

                exported.Add(texture.name);

                Debug.Log($"Texture {texture.name} was exported in {texturePath}.");
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
        }

        #region Utility Functions

        public static float[] ToFloat2(Vector2 vector) => new[] { vector.x, vector.y };
        public static float[] ToFloat3(Vector3 vector) => new[] { vector.x, vector.y, vector.z };
        public static float[] ToFloat3(Color color) => new[] { color.r, color.g, color.b };
        public static float[] ToFloat4(Color color) => new[] { color.r, color.g, color.b, color.a };

        public static float[] ToFloat2(Vector2[] vecs)
        {
            var list = new List<float>();

            foreach (var vec in vecs)
            {
                list.Add(vec.x);
                list.Add(vec.y);
            }

            return list.ToArray();
        }

        public static float[] ToFloat3(Vector3[] vecs)
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

        #endregion
    }
}