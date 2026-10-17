using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A cached, hollow ellipse drawn on a fighter's physical ground. Its mesh and material are made once per
    /// arena figure, rather than spawned by actions, and keep the body's shader lunge completely separate.</summary>
    internal sealed class DuelGroundCircle : IDisposable
    {
        private const int Segments = 64;
        private const int SortingOrder = -4;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly MeshRenderer renderer;
        private bool disposed;

        public Transform Root { get; }

        public DuelGroundCircle(string name, Transform arena, int layer, int sortingLayer, Color ink)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            var node = new GameObject(name) { layer = layer };
            Root = node.transform;
            Root.SetParent(arena, false);
            mesh = CreateMesh(ink);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = node.AddComponent<MeshRenderer>();
            Shader shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            if (shader != null && shader.isSupported)
            {
                material = new Material(shader) { name = name + " Ink (Runtime)" };
                material.mainTexture = Texture2D.whiteTexture;
                if (material.HasProperty("_Tint")) material.SetColor("_Tint", Color.white);
                renderer.sharedMaterial = material;
            }
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            if (!disposed) renderer.enabled = visible && material != null;
        }

        private static Mesh CreateMesh(Color ink)
        {
            // A restrained main line and a very faint outside rim; there is no filled disc over the ground art.
            const int bands = 2, verticesPerBand = Segments * 2;
            var vertices = new Vector3[verticesPerBand * bands];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6 * bands];
            for (int band = 0; band < bands; band++)
            {
                float radiusX = band == 0 ? 1.30f : 1.43f;
                float radiusY = band == 0 ? .31f : .34f;
                float inner = band == 0 ? .92f : .975f;
                Color color = ink;
                if (band != 0) color.a *= .2f;
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    int vertex = band * verticesPerBand + i * 2;
                    Vector3 outer = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
                    vertices[vertex] = outer * inner;
                    vertices[vertex + 1] = outer;
                    colors[vertex] = colors[vertex + 1] = color;
                    uvs[vertex] = uvs[vertex + 1] = Vector2.one * .5f;
                    int next = band * verticesPerBand + ((i + 1) % Segments) * 2;
                    int triangle = (band * Segments + i) * 6;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 1;
                    triangles[triangle + 2] = next;
                    triangles[triangle + 3] = next;
                    triangles[triangle + 4] = vertex + 1;
                    triangles[triangle + 5] = next + 1;
                }
            }
            var result = new Mesh { name = "Duel Ground Circle" };
            result.vertices = vertices;
            result.colors = colors;
            result.uv = uvs;
            result.triangles = triangles;
            result.RecalculateBounds();
            result.UploadMeshData(true);
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Root != null) Object.Destroy(Root.gameObject);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
