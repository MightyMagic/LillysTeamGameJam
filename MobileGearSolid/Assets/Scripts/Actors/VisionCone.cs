using UnityEngine;

namespace NightShift
{
    /// Readable vision sector clipped by walls/props (raycast fan mesh drawn on the floor).
    public class VisionCone : MonoBehaviour
    {
        const int Rays = 32;
        Mesh mesh;
        MeshRenderer mr;
        readonly Vector3[] verts = new Vector3[Rays + 1];
        readonly Color[] colors = new Color[Rays + 1];
        int[] tris;

        public static VisionCone Create(Transform levelRoot, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(levelRoot, false);
            go.transform.position = Vector3.zero;
            var cone = go.AddComponent<VisionCone>();
            cone.mesh = new Mesh();
            cone.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = cone.mesh;
            cone.mr = go.AddComponent<MeshRenderer>();
            cone.mr.sharedMaterial = Art.Unlit;
            cone.mr.sortingOrder = Art.OrderCones;
            cone.tris = new int[(Rays - 1) * 3];
            for (int i = 0; i < Rays - 1; i++)
            {
                cone.tris[i * 3] = 0;
                cone.tris[i * 3 + 1] = i + 2;
                cone.tris[i * 3 + 2] = i + 1;
            }
            return cone;
        }

        public void Draw(Vector2 origin, float angleDeg, float fov, float range, Color c)
        {
            mr.enabled = true;
            verts[0] = origin;
            colors[0] = c;
            var edge = new Color(c.r, c.g, c.b, c.a * 0.35f);
            for (int i = 0; i < Rays; i++)
            {
                float a = (angleDeg - fov * 0.5f + fov * i / (Rays - 1)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var hit = Physics2D.Raycast(origin, dir, range, Level.ObstacleMask);
                Vector2 p = hit.collider != null ? hit.point : origin + dir * range;
                verts[i + 1] = p;
                colors[i + 1] = edge;
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            if (mesh.triangles.Length != tris.Length) mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        public void Hide()
        {
            if (mr != null) mr.enabled = false;
        }
    }
}
