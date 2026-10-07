using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace DraftAnimals.Visuals
{
    /// <summary>
    /// Процедурные текстуры, материалы и модели мода: упряжь на звере (дуга над холкой с кольцами для постромок
    /// и колокольчиком, подпруга, ремни от дуги к подпруге), упряжь-предмет, привязь, кормушка и доска козел тележки. Материалы — на шейдере построек игры
    /// (клон ванильного материала дерева), поэтому освещаются как всё вокруг.
    /// </summary>
    internal static class Art
    {
        private static Material s_leather;
        private static Material s_darkLeather;
        private static Material s_bronze;
        private static Material s_iron;
        private static Material s_wood;
        private static Material s_fodder;
        private static readonly Dictionary<Species, Mesh[]> s_harness = new Dictionary<Species, Mesh[]>();
        private static Mesh[] s_item;
        private static Mesh s_bench;
        private static Mesh[] s_post;
        private static Mesh[] s_feeder;

        // ================================================================== материалы

        public static Material Leather => Ensure() ? s_leather : null;
        public static Material DarkLeather => Ensure() ? s_darkLeather : null;
        public static Material Bronze => Ensure() ? s_bronze : null;
        public static Material Iron => Ensure() ? s_iron : null;

        /// <summary>Доски с текстурой ванильной деревянной стены (тайлится по метрам — под наши UV в метрах).</summary>
        public static Material Wood => Ensure() ? s_wood : null;

        /// <summary>Корм в кормушке: сено с зеленью.</summary>
        public static Material Fodder => Ensure() ? s_fodder : null;

        /// <summary>
        /// Материалы создаются один раз, когда ванильные префабы уже загружены (до этого — null, и вызывающий берёт
        /// запасной материал; заглушку не запоминаем, чтобы потом создать настоящие).
        /// </summary>
        private static bool Ensure()
        {
            if (s_leather != null)
            {
                return true;
            }
            Material source = FindMaterial("piece_banner01", "woodwall") ?? FindMaterial("wood_wall", "woodwall");
            if (source == null)
            {
                return false;
            }
            Texture2D flat = FlatNormal();
            s_leather = Clone(source, "da_leather", LeatherTexture(3, new Color(0.42f, 0.26f, 0.14f)), flat, 0.18f, 0f);
            s_darkLeather = Clone(source, "da_leather_dark", LeatherTexture(5, new Color(0.24f, 0.15f, 0.09f)), flat, 0.12f, 0f);
            s_bronze = Clone(source, "da_bronze", MetalTexture(7, new Color(0.66f, 0.45f, 0.2f), new Color(0.36f, 0.24f, 0.1f)), flat, 0.45f, 0.6f);
            s_iron = Clone(source, "da_iron", MetalTexture(9, new Color(0.34f, 0.34f, 0.35f), new Color(0.18f, 0.18f, 0.19f)), flat, 0.35f, 0.55f);
            s_wood = Clone(source, "da_wood", source.mainTexture as Texture2D, null, 0.1f, 0f);
            s_fodder = Clone(source, "da_fodder", FodderTexture(13), flat, 0.05f, 0f);
            return true;
        }

        private static Material FindMaterial(string prefabName, string materialName)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                return null;
            }
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer)
                {
                    continue;
                }
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null && m.name.StartsWith(materialName, StringComparison.Ordinal))
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        private static Material Clone(Material source, string name, Texture2D albedo, Texture2D normal, float gloss, float metallic)
        {
            Material m = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            m.name = name;
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            if (albedo != null)
            {
                m.mainTexture = albedo;
            }
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
            }
            m.color = Color.white;
            // Зверь движется: отключаем мировой шум цвета, который «ползал» бы по ремням.
            SetFloat(m, "_MoveableObject", 1f);
            SetFloat(m, "_ValueNoise", 0f);
            SetFloat(m, "_ValueNoiseVertex", 0f);
            SetFloat(m, "_TriplanarMap", 0f);
            SetFloat(m, "_AddRain", 0f);
            SetFloat(m, "_Glossiness", gloss);
            SetFloat(m, "_Metallic", metallic);
            if (m.HasProperty("_AddSnow"))
            {
                m.SetInteger("_AddSnow", 0);
            }
            return m;
        }

        private static void SetFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property))
            {
                m.SetFloat(property, value);
            }
        }

        // ================================================================== текстуры

        /// <summary>Кожа: неровный тон, тёмные края ремня и строчка вдоль краёв (U — поперёк ремня, V — вдоль).</summary>
        private static Texture2D LeatherTexture(int seed, Color baseColor)
        {
            const int size = 128;
            Texture2D tex = NewTexture("da_leather_" + seed, size, size, false);
            var px = new Color32[size * size];
            Color dark = baseColor * 0.6f;
            Color thread = new Color(0.78f, 0.7f, 0.52f);
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float n = ValueNoise(u * 6f, v * 6f, seed) * 0.5f + ValueNoise(u * 24f, v * 24f, seed + 1) * 0.3f
                              + ValueNoise(u * 64f, v * 64f, seed + 2) * 0.2f;
                    Color c = Color.Lerp(dark, baseColor * 1.15f, n);
                    float edge = Mathf.Min(u, 1f - u);
                    if (edge < 0.07f)
                    {
                        c = Color.Lerp(c, dark * 0.8f, 1f - edge / 0.07f);
                    }
                    // Строчка: короткие стежки на расстоянии от краёв.
                    if (Mathf.Abs(edge - 0.14f) < 0.02f && (y / 6) % 2 == 0)
                    {
                        c = Color.Lerp(c, thread, 0.75f);
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Металл: тон с разводами и тёмными пятнами окисла.</summary>
        private static Texture2D MetalTexture(int seed, Color light, Color dark)
        {
            const int size = 64;
            Texture2D tex = NewTexture("da_metal_" + seed, size, size, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n = ValueNoise(u * 8f, v * 8f, seed) * 0.6f + ValueNoise(u * 24f, v * 24f, seed + 1) * 0.4f;
                    Color c = Color.Lerp(dark, light, n);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Сено: соломенные штрихи вдоль V с вкраплениями зелени.</summary>
        private static Texture2D FodderTexture(int seed)
        {
            const int size = 128;
            Texture2D tex = NewTexture("da_fodder", size, size, false);
            var px = new Color32[size * size];
            var straw = new Color(0.72f, 0.6f, 0.3f);
            var dry = new Color(0.45f, 0.36f, 0.16f);
            var green = new Color(0.35f, 0.5f, 0.18f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float strands = ValueNoise(u * 48f, v * 6f, seed);
                    Color c = Color.Lerp(dry, straw, strands);
                    float g = ValueNoise(u * 10f, v * 10f, seed + 1);
                    if (g > 0.62f)
                    {
                        c = Color.Lerp(c, green, (g - 0.62f) * 2.2f);
                    }
                    c *= 0.85f + 0.3f * ValueNoise(u * 96f, v * 96f, seed + 2);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D FlatNormal()
        {
            const int size = 8;
            Texture2D tex = NewTexture("da_flat_n", size, size, true);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(128, 128, 255, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D NewTexture(string name, int w, int h, bool linear)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        // ================================================================== упряжь на звере

        /// <summary>Толщина дуги, м.</summary>
        private const float ArchTube = 0.03f;

        /// <summary>Радиус колец для постромок на концах дуги, м.</summary>
        private const float RingRadius = 0.04f;

        /// <summary>Дуга — полуэллипс поперёк тела: от левого конца через верх к правому.</summary>
        private static List<Vector3> ArchPath(SpeciesInfo info, int segments)
        {
            var points = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * i / segments;
                points.Add(new Vector3(-Mathf.Cos(a) * info.ArchHalfWidth, info.ArchBottom + Mathf.Sin(a) * (info.ArchTop - info.ArchBottom),
                    info.ArchZ));
            }
            return points;
        }

        /// <summary>
        /// Точка, к которой крепится постромка (кольцо на конце дуги), в координатах модели упряжи:
        /// side = +1 — справа, -1 — слева.
        /// </summary>
        public static Vector3 TracePoint(SpeciesInfo info, int side)
        {
            return new Vector3(side * info.ArchHalfWidth, info.ArchBottom - RingRadius, info.ArchZ);
        }

        /// <summary>Колокольчик: несколько коротких цилиндров, сужающихся кверху, и ушко.</summary>
        private static void AddBell(MeshBuilder b, Vector3 top)
        {
            b.AddTube(MeshBuilder.Ellipse(top + Vector3.down * 0.015f, Vector3.forward, Vector3.up, 0.012f, 0.012f, 10), 0.004f, 5, 4f);
            b.AddCylinder(top + Vector3.down * 0.075f, 0.02f, 0.042f, 12, 4f);
            b.AddCylinder(top + Vector3.down * 0.055f, 0.02f, 0.033f, 12, 4f);
            b.AddCylinder(top + Vector3.down * 0.035f, 0.012f, 0.022f, 12, 4f);
        }

        /// <summary>Меши упряжи вида: [0] — кожа (подпруга, ремень по спине), [1] — дерево (дуга), [2] — металл.</summary>
        public static Mesh[] HarnessMeshes(SpeciesInfo info)
        {
            if (s_harness.TryGetValue(info.Species, out Mesh[] cached))
            {
                return cached;
            }
            var leather = new MeshBuilder();
            var wood = new MeshBuilder();
            var metal = new MeshBuilder();

            // Дуга над холкой — не обхватывает шею, поэтому шея сквозь неё не проходит.
            wood.AddTube(ArchPath(info, 20), ArchTube, 8, 2f);

            // Кольца для постромок на концах дуги и колокольчик под верхом.
            for (int side = -1; side <= 1; side += 2)
            {
                metal.AddTube(MeshBuilder.Ellipse(TracePoint(info, side), Vector3.forward, Vector3.up, RingRadius, RingRadius, 12), 0.009f, 6, 4f);
            }
            AddBell(metal, new Vector3(0f, info.ArchTop - ArchTube, info.ArchZ));

            // Подпруга вокруг туловища за передними ногами.
            float hw = info.BodyHalfWidth + 0.015f;
            float hh = info.BodyHalfHeight + 0.015f;
            var girthCenter = new Vector3(0f, info.BodyCenterY, info.GirthZ);
            List<Vector3> girth = MeshBuilder.Ellipse(girthCenter, Vector3.right, Vector3.up, hw, hh, 28);
            var girthUp = new List<Vector3>(girth.Count);
            foreach (Vector3 p in girth)
            {
                girthUp.Add((p - girthCenter).normalized);
            }
            leather.AddStrap(girth, girthUp, 0.07f, 0.012f, 1.5f);

            // Ремни от концов дуги к подпруге (держат дугу).
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 end = new Vector3(side * info.ArchHalfWidth, info.ArchBottom + 0.02f, info.ArchZ);
                Vector3 girthSide = new Vector3(side * hw, info.BodyCenterY, info.GirthZ);
                var outward = new Vector3(side, 0f, 0f);
                leather.AddStrap(new List<Vector3> { end, girthSide }, new List<Vector3> { outward, outward }, 0.04f, 0.01f, 1.5f);
            }
            Vector3 girthTop = new Vector3(0f, info.BackTop + 0.015f, info.GirthZ);

            // Пряжка на спине у подпруги.
            metal.AddBox(girthTop + Vector3.up * 0.008f, new Vector3(0.08f, 0.012f, 0.06f), Quaternion.identity, 4f);

            var meshes = new[]
            {
                leather.ToMesh("da_harness_leather_" + info.Species),
                wood.ToMesh("da_harness_arch_" + info.Species),
                metal.ToMesh("da_harness_metal_" + info.Species),
            };
            s_harness[info.Species] = meshes;
            return meshes;
        }

        /// <summary>Материалы к мешам <see cref="HarnessMeshes"/> (тяжёлая упряжь — с железом вместо бронзы).</summary>
        public static Material[] HarnessMaterials(HarnessKind kind)
        {
            return new[] { Leather, Wood, kind == HarnessKind.Heavy ? Iron : Bronze };
        }

        /// <summary>Модель упряжи: объект с тремя рендерерами (без коллайдеров, не мешает физике).</summary>
        public static GameObject BuildHarness(Transform parent, SpeciesInfo info, HarnessKind kind)
        {
            var root = new GameObject("da_harness_visual");
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            Mesh[] meshes = HarnessMeshes(info);
            Material[] materials = HarnessMaterials(kind);
            for (int i = 0; i < meshes.Length; i++)
            {
                var go = new GameObject("part" + i);
                go.layer = root.layer;
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = materials[i];
            }
            return root;
        }

        // ================================================================== упряжь-предмет

        /// <summary>
        /// Упряжь, лежащая на земле: дуга плашмя и свёрнутая подпруга рядом. [0] — кожа, [1] — дерево, [2] — металл.
        /// Начало координат — посередине, на земле.
        /// </summary>
        public static Mesh[] ItemMeshes()
        {
            if (s_item != null)
            {
                return s_item;
            }
            var leather = new MeshBuilder();
            var wood = new MeshBuilder();
            var metal = new MeshBuilder();
            var arch = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float a = Mathf.PI * i / 16;
                arch.Add(new Vector3(-Mathf.Cos(a) * 0.28f, ArchTube, 0.05f + Mathf.Sin(a) * 0.32f));
            }
            wood.AddTube(arch, ArchTube, 8, 2f);
            for (int side = -1; side <= 1; side += 2)
            {
                var p = new Vector3(side * 0.28f, ArchTube, 0.05f - RingRadius);
                metal.AddTube(MeshBuilder.Ellipse(p, Vector3.forward, Vector3.up, RingRadius, RingRadius, 12), 0.009f, 6, 4f);
            }
            AddBell(metal, new Vector3(0f, 0.09f, 0.4f));
            // Подпруга кольцом позади дуги и ремень, тянущийся к ней.
            var girthCenter = new Vector3(0f, 0.02f, -0.3f);
            List<Vector3> girth = MeshBuilder.Ellipse(girthCenter, Vector3.right, Vector3.forward, 0.18f, 0.12f, 20);
            var up = new List<Vector3>(girth.Count);
            for (int i = 0; i < girth.Count; i++)
            {
                up.Add(Vector3.up);
            }
            leather.AddStrap(girth, up, 0.07f, 0.012f, 1.5f);
            var strap = new List<Vector3> { new Vector3(0f, 0.02f, 0.2f), new Vector3(0.02f, 0.025f, 0.02f), new Vector3(0f, 0.025f, -0.18f) };
            leather.AddStrap(strap, new List<Vector3> { Vector3.up, Vector3.up, Vector3.up }, 0.05f, 0.01f, 1.5f);
            metal.AddBox(new Vector3(0f, 0.035f, -0.18f), new Vector3(0.08f, 0.012f, 0.06f), Quaternion.identity, 4f);
            s_item = new[] { leather.ToMesh("da_item_leather"), wood.ToMesh("da_item_arch"), metal.ToMesh("da_item_metal") };
            return s_item;
        }

        // ================================================================== привязь

        /// <summary>Высота кольца привязи над землёй, м: от него идёт верёвка.</summary>
        public const float PostRingHeight = 1f;

        /// <summary>Радиус кола, м.</summary>
        public const float PostRadius = 0.06f;

        /// <summary>Кол привязи: [0] — дерево (уходит на 0,25 м в землю), [1] — железное кольцо-хомутик у верха.</summary>
        public static Mesh[] PostMeshes()
        {
            if (s_post != null)
            {
                return s_post;
            }
            var wood = new MeshBuilder();
            wood.AddCylinder(new Vector3(0f, -0.25f, 0f), 1.4f, PostRadius, 10, 1f);
            var iron = new MeshBuilder();
            iron.AddTube(MeshBuilder.Ellipse(new Vector3(0f, PostRingHeight, 0f), Vector3.right, Vector3.forward,
                PostRadius + 0.012f, PostRadius + 0.012f, 16), 0.014f, 6, 4f);
            iron.AddTube(MeshBuilder.Ellipse(new Vector3(0f, PostRingHeight - 0.07f, PostRadius + 0.05f), Vector3.forward, Vector3.up,
                0.045f, 0.045f, 14), 0.01f, 6, 4f);
            s_post = new[] { wood.ToMesh("da_post_wood"), iron.ToMesh("da_post_iron") };
            return s_post;
        }

        // ================================================================== кормушка

        /// <summary>Полудлина и полуширина корыта кормушки (по X и Z), м.</summary>
        public const float FeederHalfLength = 0.71f;
        public const float FeederHalfWidth = 0.31f;

        /// <summary>Высота дна корыта и бортов, м.</summary>
        public const float FeederFloor = 0.32f;
        public const float FeederTop = 0.58f;

        /// <summary>Кормушка-корыто на четырёх ножках: [0] — доски, [1] — слой корма (его двигает <see cref="DraftAnimals.Feeder"/>).</summary>
        public static Mesh[] FeederMeshes()
        {
            if (s_feeder != null)
            {
                return s_feeder;
            }
            const float hx = FeederHalfLength;
            const float hz = FeederHalfWidth;
            const float wall = 0.04f;
            float wallHeight = FeederTop - FeederFloor;
            float wallY = FeederFloor + wallHeight * 0.5f;
            var planks = new MeshBuilder();
            planks.AddBox(new Vector3(0f, FeederFloor + 0.02f, 0f), new Vector3(hx * 2f - wall, 0.04f, hz * 2f - wall), Quaternion.identity, 1f);
            planks.AddBox(new Vector3(0f, wallY, hz - wall * 0.5f), new Vector3(hx * 2f, wallHeight, wall), Quaternion.identity, 1f);
            planks.AddBox(new Vector3(0f, wallY, -hz + wall * 0.5f), new Vector3(hx * 2f, wallHeight, wall), Quaternion.identity, 1f);
            planks.AddBox(new Vector3(hx - wall * 0.5f, wallY, 0f), new Vector3(wall, wallHeight, hz * 2f - wall * 2f), Quaternion.identity, 1f);
            planks.AddBox(new Vector3(-hx + wall * 0.5f, wallY, 0f), new Vector3(wall, wallHeight, hz * 2f - wall * 2f), Quaternion.identity, 1f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    planks.AddBox(new Vector3(sx * (hx - 0.1f), FeederFloor * 0.5f, sz * (hz - 0.08f)), new Vector3(0.08f, FeederFloor, 0.08f),
                        Quaternion.identity, 1f);
                }
            }
            var fodder = new MeshBuilder();
            fodder.AddBox(Vector3.zero, new Vector3(hx * 2f - wall * 2.2f, 0.03f, hz * 2f - wall * 2.2f), Quaternion.identity, 1.5f);
            s_feeder = new[] { planks.ToMesh("da_feeder_planks"), fodder.ToMesh("da_feeder_fodder") };
            return s_feeder;
        }

        // ================================================================== козлы

        /// <summary>Доска козел поперёк передней стенки тележки: начало — центр доски, длина вдоль X.</summary>
        public static Mesh BenchMesh()
        {
            if (s_bench != null)
            {
                return s_bench;
            }
            var b = new MeshBuilder();
            b.AddBox(Vector3.zero, new Vector3(1.32f, 0.05f, 0.3f), Quaternion.identity, 1f);
            // Опоры-бруски на боковых стенках.
            b.AddBox(new Vector3(0.6f, -0.045f, 0f), new Vector3(0.1f, 0.04f, 0.26f), Quaternion.identity, 1f);
            b.AddBox(new Vector3(-0.6f, -0.045f, 0f), new Vector3(0.1f, 0.04f, 0.26f), Quaternion.identity, 1f);
            s_bench = b.ToMesh("da_bench");
            return s_bench;
        }
    }
}
