using System.Collections.Generic;
using UnityEngine;

namespace DraftAnimals.Visuals
{
    /// <summary>Сборщик процедурных мешей: ящики (доски, пряжки), ремни-трубы вдоль ломаной и кольца.</summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> m_verts = new List<Vector3>();
        private readonly List<Vector3> m_normals = new List<Vector3>();
        private readonly List<Vector2> m_uvs = new List<Vector2>();
        private readonly List<int> m_tris = new List<int>();

        public bool IsEmpty => m_verts.Count == 0;

        /// <summary>Четырёхугольник; порядок обхода подбирается так, чтобы лицевая сторона смотрела по normal.</summary>
        public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int i = m_verts.Count;
            m_verts.Add(p0);
            m_verts.Add(p1);
            m_verts.Add(p2);
            m_verts.Add(p3);
            for (int k = 0; k < 4; k++)
            {
                m_normals.Add(normal);
            }
            m_uvs.Add(uv0);
            m_uvs.Add(uv1);
            m_uvs.Add(uv2);
            m_uvs.Add(uv3);
            AddTri(i, i + 1, i + 2, normal);
            AddTri(i, i + 2, i + 3, normal);
        }

        private void AddTri(int a, int b, int c, Vector3 normal)
        {
            // В Unity лицевая грань — обход по часовой стрелке; Cross(b-a, c-a) смотрит наружу лицевой стороны.
            Vector3 n = Vector3.Cross(m_verts[b] - m_verts[a], m_verts[c] - m_verts[a]);
            if (Vector3.Dot(n, normal) >= 0f)
            {
                m_tris.Add(a);
                m_tris.Add(b);
                m_tris.Add(c);
            }
            else
            {
                m_tris.Add(a);
                m_tris.Add(c);
                m_tris.Add(b);
            }
        }

        /// <summary>Ящик с UV в метрах * uvScale, чтобы текстура не растягивалась на длинных досках.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3 ax = rotation * Vector3.right;
            Vector3 ay = rotation * Vector3.up;
            Vector3 az = rotation * Vector3.forward;

            AddFace(center, ax, ay, az, h.x, h.y, h.z, size.z, size.y, uvScale);
            AddFace(center, -ax, ay, -az, h.x, h.y, h.z, size.z, size.y, uvScale);
            AddFace(center, ay, az, ax, h.y, h.z, h.x, size.x, size.z, uvScale);
            AddFace(center, -ay, az, -ax, h.y, h.z, h.x, size.x, size.z, uvScale);
            AddFace(center, az, ay, -ax, h.z, h.y, h.x, size.x, size.y, uvScale);
            AddFace(center, -az, ay, ax, h.z, h.y, h.x, size.x, size.y, uvScale);
        }

        /// <summary>Грань ящика: n — нормаль, v и u — оси грани, hn/hv/hu — полуразмеры.</summary>
        private void AddFace(Vector3 c, Vector3 n, Vector3 v, Vector3 u, float hn, float hv, float hu, float lenU, float lenV, float uvScale)
        {
            Vector3 fc = c + n * hn;
            Vector3 du = u * hu;
            Vector3 dv = v * hv;
            float su = lenU * uvScale;
            float sv = lenV * uvScale;
            AddQuad(fc - du - dv, fc + du - dv, fc + du + dv, fc - du + dv, n,
                new Vector2(0f, 0f), new Vector2(su, 0f), new Vector2(su, sv), new Vector2(0f, sv));
        }

        /// <summary>
        /// Плоский ремень вдоль ломаной: ширина width лежит вдоль side (поперёк пути), толщина — по нормали «наружу».
        /// up задаёт, куда смотрит лицевая сторона ремня в каждой точке.
        /// </summary>
        public void AddStrap(IList<Vector3> path, IList<Vector3> up, float width, float thickness, float vPerMeter)
        {
            if (path.Count < 2)
            {
                return;
            }
            float length = 0f;
            var rings = new List<Vector3[]>();
            var vs = new List<float>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent = i == 0 ? path[1] - path[0]
                    : i == path.Count - 1 ? path[i] - path[i - 1]
                    : (path[i + 1] - path[i - 1]);
                tangent.Normalize();
                if (i > 0)
                {
                    length += Vector3.Distance(path[i], path[i - 1]);
                }
                Vector3 n = up[i] - tangent * Vector3.Dot(up[i], tangent);
                n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
                Vector3 side = Vector3.Cross(tangent, n).normalized;
                Vector3 hw = side * (width * 0.5f);
                Vector3 ht = n * (thickness * 0.5f);
                rings.Add(new[] { path[i] - hw + ht, path[i] + hw + ht, path[i] + hw - ht, path[i] - hw - ht, n, side });
                vs.Add(length * vPerMeter);
            }
            for (int i = 0; i < rings.Count - 1; i++)
            {
                Vector3[] a = rings[i];
                Vector3[] b = rings[i + 1];
                Vector3 n = (a[4] + b[4]).normalized;
                Vector3 s = (a[5] + b[5]).normalized;
                float v0 = vs[i];
                float v1 = vs[i + 1];
                // Лицо, изнанка и два ребра.
                AddQuad(a[0], a[1], b[1], b[0], n, new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));
                AddQuad(a[3], a[2], b[2], b[3], -n, new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));
                AddQuad(a[1], a[2], b[2], b[1], s, new Vector2(0f, v0), new Vector2(0.1f, v0), new Vector2(0.1f, v1), new Vector2(0f, v1));
                AddQuad(a[0], a[3], b[3], b[0], -s, new Vector2(0f, v0), new Vector2(0.1f, v0), new Vector2(0.1f, v1), new Vector2(0f, v1));
            }
        }

        /// <summary>Труба вдоль ломаной (кольцо, если первая точка совпадает с последней).</summary>
        public void AddTube(IList<Vector3> path, float radius, int sides, float vPerMeter)
        {
            if (path.Count < 2)
            {
                return;
            }
            bool closed = Vector3.Distance(path[0], path[path.Count - 1]) < 1e-4f;
            Vector3 t0 = (path[1] - path[0]).normalized;
            Vector3 normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            int start = m_verts.Count;
            float length = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent;
                if (i == 0 || i == path.Count - 1)
                {
                    tangent = closed
                        ? ((path[1] - path[0]).normalized + (path[path.Count - 1] - path[path.Count - 2]).normalized).normalized
                        : i == 0 ? (path[1] - path[0]).normalized : (path[i] - path[i - 1]).normalized;
                }
                else
                {
                    tangent = ((path[i + 1] - path[i]).normalized + (path[i] - path[i - 1]).normalized).normalized;
                }
                if (i > 0)
                {
                    length += Vector3.Distance(path[i], path[i - 1]);
                }
                // Перенос рамки: убираем из нормали составляющую вдоль новой касательной.
                normal = (normal - tangent * Vector3.Dot(normal, tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                for (int j = 0; j <= sides; j++)
                {
                    float a = (float)j / sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    m_verts.Add(path[i] + dir * radius);
                    m_normals.Add(dir);
                    m_uvs.Add(new Vector2((float)j / sides, length * vPerMeter));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * row + j;
                    int b = a + 1;
                    int c = a + row + 1;
                    int d = a + row;
                    Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                    AddTri(a, b, c, outward);
                    AddTri(a, c, d, outward);
                }
            }
        }

        /// <summary>Вертикальное бревно: боковая поверхность и верхний торец (UV в метрах * uvScale).</summary>
        public void AddCylinder(Vector3 bottom, float height, float radius, int sides, float uvScale = 1f)
        {
            int start = m_verts.Count;
            float circumference = Mathf.PI * 2f * radius;
            for (int ring = 0; ring < 2; ring++)
            {
                float y = ring * height;
                for (int j = 0; j <= sides; j++)
                {
                    float a = (float)j / sides * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    m_verts.Add(bottom + dir * radius + Vector3.up * y);
                    m_normals.Add(dir);
                    m_uvs.Add(new Vector2((float)j / sides * circumference * uvScale, y * uvScale));
                }
            }
            int row = sides + 1;
            for (int j = 0; j < sides; j++)
            {
                int a = start + j;
                int b = a + 1;
                int c = a + row + 1;
                int d = a + row;
                Vector3 outward = (m_normals[a] + m_normals[b]).normalized;
                AddTri(a, b, c, outward);
                AddTri(a, c, d, outward);
            }
            // Торец сверху: веер вокруг центра.
            int center = m_verts.Count;
            Vector3 top = bottom + Vector3.up * height;
            m_verts.Add(top);
            m_normals.Add(Vector3.up);
            m_uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= sides; j++)
            {
                float a = (float)j / sides * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                m_verts.Add(top + dir * radius);
                m_normals.Add(Vector3.up);
                m_uvs.Add(new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
            }
            for (int j = 0; j < sides; j++)
            {
                AddTri(center, center + 1 + j, center + 2 + j, Vector3.up);
            }
        }

        /// <summary>Точки эллипса (замкнутые: последняя = первой) в плоскости осей ax, ay вокруг center.</summary>
        public static List<Vector3> Ellipse(Vector3 center, Vector3 ax, Vector3 ay, float rx, float ry, int segments)
        {
            var points = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)(i % segments) / segments * Mathf.PI * 2f;
                points.Add(center + ax * (Mathf.Cos(a) * rx) + ay * (Mathf.Sin(a) * ry));
            }
            return points;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(m_verts);
            mesh.SetNormals(m_normals);
            mesh.SetUVs(0, m_uvs);
            mesh.SetTriangles(m_tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
