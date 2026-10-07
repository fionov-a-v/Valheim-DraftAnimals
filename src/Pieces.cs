using System;
using System.Collections.Generic;
using DraftAnimals.Logic;
using DraftAnimals.Visuals;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace DraftAnimals
{
    /// <summary>
    /// Постройки молотом (вкладка «Обстановка»): привязь — клон деревянного столба со своим колом и кольцом,
    /// кормушка — клон деревянного сундука (инвентарь, защита, звуки) с корытом вместо сундука.
    /// </summary>
    internal static class Pieces
    {
        public const string PostPrefab = "piece_da_tetherpost";
        public const string FeederPrefab = "piece_da_feeder";

        private static GameObject s_post;
        private static GameObject s_feeder;

        public static void Register()
        {
            s_post = Add(PostPrefab, "wood_pole", BuildPost, "$da_post", "$da_post_desc", DraftConfig.PostRecipe, Art.PostMeshes(),
                new[] { Art.Wood, Art.Iron }, Quaternion.Euler(10f, -30f, 0f));
            s_feeder = Add(FeederPrefab, "piece_chest_wood", BuildFeeder, "$da_feeder", "$da_feeder_desc", DraftConfig.FeederRecipe,
                Art.FeederMeshes(), new[] { Art.Wood, Art.Fodder }, Quaternion.Euler(25f, -30f, 0f));
        }

        private static GameObject Add(string name, string basePrefab, Action<GameObject> build, string token, string description,
            ItemRecipeConfig recipe, Mesh[] iconMeshes, Material[] iconMaterials, Quaternion iconRotation)
        {
            try
            {
                GameObject baseGo = PrefabManager.Instance.GetPrefab(basePrefab);
                if (baseGo == null || baseGo.GetComponent<Piece>() == null || baseGo.GetComponent<WearNTear>() == null)
                {
                    Logger.LogError($"Draft Animals: ванильный '{basePrefab}' не найден — {name} не добавлен");
                    return null;
                }
                GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(name, baseGo);
                build(prefab);
                var config = new PieceConfig
                {
                    Name = token,
                    Description = description,
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Furniture,
                    CraftingStation = string.IsNullOrWhiteSpace(recipe.Station.Value) ? null : recipe.Station.Value.Trim(),
                    Icon = RenderIcon(iconMeshes, iconMaterials, iconRotation),
                    Requirements = DraftRules.ParseRecipe(recipe.Recipe.Value)
                        .ConvertAll(r => new RequirementConfig(r.Key, r.Value, 0, true)).ToArray(),
                };
                PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, config));
                return prefab;
            }
            catch (Exception e)
            {
                Logger.LogError($"Draft Animals: не удалось создать {name}: {e}");
                return null;
            }
        }

        // ================================================================== привязь

        private static void BuildPost(GameObject prefab)
        {
            WearNTear wear = prefab.GetComponent<WearNTear>();
            // Привязь — не строительный столб: ни к чему не прищёлкивается и ничего на себе не держит.
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (child != prefab.transform && (child.name.StartsWith("$hud_snappoint", StringComparison.Ordinal) || child.CompareTag("snappoint")))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            DestroyChild(prefab, "pole_top_snow");
            DestroyChild(prefab, "WornBroken");
            wear.m_snow = null;
            wear.m_supports = false;

            Transform visual = prefab.transform.Find("New");
            if (visual == null)
            {
                visual = new GameObject("New").transform;
                visual.SetParent(prefab.transform, false);
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            SetMesh(visual.gameObject, Art.PostMeshes()[0], Art.Wood);
            AddPart(visual, "ring", Art.PostMeshes()[1], Art.Iron);
            wear.m_new = visual.gameObject;
            wear.m_worn = visual.gameObject;
            wear.m_broken = visual.gameObject;

            Transform colliderGo = prefab.transform.Find("collider");
            BoxCollider box = colliderGo != null ? colliderGo.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                var go = new GameObject("collider") { layer = prefab.layer };
                go.transform.SetParent(prefab.transform, false);
                box = go.AddComponent<BoxCollider>();
                colliderGo = go.transform;
            }
            colliderGo.localPosition = Vector3.zero;
            colliderGo.localRotation = Quaternion.identity;
            colliderGo.localScale = Vector3.one;
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(0.16f, 1.2f, 0.16f);

            // Верёвка — та же, что у ванильной тележки, только тоньше.
            LineRenderer rope = prefab.AddComponent<LineRenderer>();
            GameObject cart = PrefabManager.Instance.GetPrefab("Cart");
            LineRenderer cartLine = cart != null ? cart.GetComponent<LineRenderer>() : null;
            if (cartLine != null)
            {
                rope.sharedMaterials = cartLine.sharedMaterials;
                rope.textureMode = cartLine.textureMode;
                rope.widthMultiplier = cartLine.widthMultiplier * 0.6f;
                rope.widthCurve = cartLine.widthCurve;
                rope.colorGradient = cartLine.colorGradient;
            }
            else
            {
                rope.sharedMaterial = Art.DarkLeather;
                rope.widthMultiplier = 0.03f;
            }
            rope.useWorldSpace = true;
            rope.positionCount = 0;
            rope.enabled = false;

            prefab.AddComponent<TetherPost>();
        }

        // ================================================================== кормушка

        private static void BuildFeeder(GameObject prefab)
        {
            WearNTear wear = prefab.GetComponent<WearNTear>();
            Container container = prefab.GetComponent<Container>();
            DestroyChild(prefab, "floor_2x2_snow");
            wear.m_snow = null;

            Transform visual = prefab.transform.Find("New");
            if (visual == null)
            {
                visual = new GameObject("New").transform;
                visual.SetParent(prefab.transform, false);
            }
            foreach (LODGroup lod in visual.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(lod);
            }
            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(visual.GetChild(i).gameObject);
            }
            Mesh[] meshes = Art.FeederMeshes();
            AddPart(visual, "trough", meshes[0], Art.Wood);
            GameObject fodder = AddPart(visual, "fodder", meshes[1], Art.Fodder);
            fodder.SetActive(false);
            wear.m_new = visual.gameObject;
            wear.m_worn = visual.gameObject;
            wear.m_broken = visual.gameObject;

            var colliderGo = new GameObject("collider") { layer = prefab.layer };
            colliderGo.transform.SetParent(prefab.transform, false);
            BoxCollider box = colliderGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Art.FeederTop * 0.5f, 0f);
            box.size = new Vector3(Art.FeederHalfLength * 2f + 0.02f, Art.FeederTop, Art.FeederHalfWidth * 2f + 0.02f);

            container.m_name = "$da_feeder";
            container.m_width = 4;
            container.m_height = 2;
            container.m_open = null;
            container.m_closed = null;

            prefab.AddComponent<Feeder>().m_fodder = fodder;
        }

        // ================================================================== общее

        private static void DestroyChild(GameObject prefab, string name)
        {
            Transform t = prefab.transform.Find(name);
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        private static void SetMesh(GameObject go, Mesh mesh, Material material)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = go.AddComponent<MeshFilter>();
            }
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = material;
        }

        private static GameObject AddPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            SetMesh(go, mesh, material);
            return go;
        }

        /// <summary>Иконка для меню молота: временная модель из тех же мешей (RenderManager рисует только активное).</summary>
        private static Sprite RenderIcon(Mesh[] meshes, Material[] materials, Quaternion rotation)
        {
            GameObject model = null;
            try
            {
                model = new GameObject("da_piece_render");
                model.transform.position = new Vector3(0f, -5000f, 0f);
                for (int i = 0; i < meshes.Length; i++)
                {
                    AddPart(model.transform, "part" + i, meshes[i], materials[i]);
                }
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(model)
                {
                    Width = 128,
                    Height = 128,
                    Rotation = rotation,
                    UseCache = false,
                });
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Draft Animals: иконка постройки не отрисована: {e.Message}");
                return null;
            }
            finally
            {
                if (model != null)
                {
                    Object.DestroyImmediate(model);
                }
            }
        }

        /// <summary>Рецепты из настроек (после регистрации префабов и при каждой смене настроек).</summary>
        public static void ApplyRecipes()
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
            {
                return;
            }
            Apply(s_post, DraftConfig.PostRecipe);
            Apply(s_feeder, DraftConfig.FeederRecipe);
        }

        private static void Apply(GameObject prefab, ItemRecipeConfig cfg)
        {
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (piece == null)
            {
                return;
            }
            var requirements = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> r in DraftRules.ParseRecipe(cfg.Recipe.Value))
            {
                GameObject item = ObjectDB.instance.GetItemPrefab(r.Key);
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Logger.LogWarning($"Draft Animals: предмет '{r.Key}' не найден (рецепт {prefab.name})");
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = drop, m_amount = r.Value, m_recover = true });
            }
            piece.m_resources = requirements.ToArray();
            piece.m_craftingStation = Items.FindStation(cfg.Station.Value);
        }
    }
}
