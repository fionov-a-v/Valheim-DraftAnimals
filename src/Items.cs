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
    /// Предметы-упряжи: лёгкая (кабан, олень) и тяжёлая (волк). Клоны седла локса (предмет «прочее», не складывается)
    /// со своей моделью и иконкой; рецепты — из настроек, меняются на лету.
    /// </summary>
    internal static class Items
    {
        public const string HarnessPrefab = "DA_Harness";
        public const string HeavyHarnessPrefab = "DA_HeavyHarness";
        private const string BasePrefab = "SaddleLox";

        private static CustomItem s_light;
        private static CustomItem s_heavy;

        public static void Register()
        {
            s_light = Create(HarnessPrefab, "$da_harness", "$da_harness_desc", HarnessKind.Light, DraftConfig.Harness, 5f);
            s_heavy = Create(HeavyHarnessPrefab, "$da_heavyharness", "$da_heavyharness_desc", HarnessKind.Heavy, DraftConfig.HeavyHarness, 8f);
        }

        private static CustomItem Create(string prefabName, string token, string description, HarnessKind kind, ItemRecipeConfig recipe,
            float weight)
        {
            var config = new ItemConfig
            {
                Name = token,
                Description = description,
                CraftingStation = string.IsNullOrWhiteSpace(recipe.Station.Value) ? null : recipe.Station.Value.Trim(),
                Amount = 1,
            };
            foreach (KeyValuePair<string, int> r in DraftRules.ParseRecipe(recipe.Recipe.Value))
            {
                config.AddRequirement(r.Key, r.Value);
            }
            var item = new CustomItem(prefabName, BasePrefab, config);
            if (item.ItemPrefab == null || item.ItemDrop == null)
            {
                Logger.LogError($"Draft Animals: не удалось создать предмет {prefabName} (нет ванильного {BasePrefab})");
                return null;
            }
            BuildModel(item.ItemPrefab, kind);
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_weight = weight;
            Sprite icon = RenderIcon(kind);
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            ItemManager.Instance.AddItem(item);
            return item;
        }

        /// <summary>Префаб предмета для этого вида упряжи (для выпадения при снятии).</summary>
        public static GameObject Prefab(HarnessKind kind)
        {
            CustomItem item = kind == HarnessKind.Heavy ? s_heavy : kind == HarnessKind.Light ? s_light : null;
            return item != null ? item.ItemPrefab : null;
        }

        public static Sprite Icon(HarnessKind kind)
        {
            CustomItem item = kind == HarnessKind.Heavy ? s_heavy : s_light;
            Sprite[] icons = item?.ItemDrop?.m_itemData.m_shared.m_icons;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        /// <summary>Какая это упряжь (или None, если предмет — не упряжь).</summary>
        public static HarnessKind KindOf(ItemDrop.ItemData item)
        {
            string name = item?.m_shared?.m_name;
            if (name == "$da_harness")
            {
                return HarnessKind.Light;
            }
            if (name == "$da_heavyharness")
            {
                return HarnessKind.Heavy;
            }
            return HarnessKind.None;
        }

        /// <summary>Вместо седла — наша модель упряжи; коллайдер — ящик по её размеру.</summary>
        private static void BuildModel(GameObject prefab, HarnessKind kind)
        {
            foreach (LODGroup lod in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(lod);
            }
            foreach (MeshRenderer mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Object.DestroyImmediate(mr.gameObject);
            }
            foreach (Collider c in prefab.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
            var visual = new GameObject("da_visual");
            visual.layer = prefab.layer;
            visual.transform.SetParent(prefab.transform, false);
            AddMeshes(visual.transform, kind);
            BoxCollider box = visual.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.05f, -0.06f);
            box.size = new Vector3(0.66f, 0.1f, 0.84f);
        }

        private static void AddMeshes(Transform parent, HarnessKind kind)
        {
            Mesh[] meshes = Art.ItemMeshes();
            Material[] materials = Art.HarnessMaterials(kind);
            for (int i = 0; i < meshes.Length; i++)
            {
                var go = new GameObject("part" + i);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
            }
        }

        /// <summary>
        /// Иконка: модель упряжи сверху-сбоку. Рисуется временная модель (сам префаб лежит в неактивном контейнере
        /// Jötunn, а RenderManager рисует только активные объекты).
        /// </summary>
        private static Sprite RenderIcon(HarnessKind kind)
        {
            GameObject model = null;
            try
            {
                model = new GameObject("da_render");
                model.transform.position = new Vector3(0f, -5000f, 0f);
                AddMeshes(model.transform, kind);
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(model)
                {
                    Width = 128,
                    Height = 128,
                    Rotation = Quaternion.Euler(48f, -30f, 0f),
                    UseCache = false,
                });
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Draft Animals: иконка упряжи не отрисована, будет иконка седла: {e.Message}");
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

        /// <summary>Рецепты из настроек (после загрузки ObjectDB и при каждой смене настроек).</summary>
        public static void ApplyRecipes()
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
            {
                return;
            }
            bool light = DraftConfig.For(Species.Boar).Enabled.Value || DraftConfig.For(Species.Deer).Enabled.Value;
            Apply(s_light, DraftConfig.Harness, light);
            Apply(s_heavy, DraftConfig.HeavyHarness, DraftConfig.For(Species.Wolf).Enabled.Value);
        }

        private static void Apply(CustomItem item, ItemRecipeConfig cfg, bool enabled)
        {
            Recipe recipe = item?.Recipe?.Recipe;
            if (recipe == null)
            {
                return;
            }
            var requirements = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> r in DraftRules.ParseRecipe(cfg.Recipe.Value))
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(r.Key);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Logger.LogWarning($"Draft Animals: предмет '{r.Key}' не найден (рецепт {item.ItemPrefab.name})");
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = drop, m_amount = r.Value, m_recover = true });
            }
            recipe.m_resources = requirements.ToArray();
            recipe.m_craftingStation = FindStation(cfg.Station.Value);
            recipe.m_enabled = enabled;
        }

        internal static CraftingStation FindStation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            name = CraftingStations.GetInternalName(name.Trim());
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (go == null)
            {
                go = PrefabManager.Instance.GetPrefab(name);
            }
            CraftingStation station = go != null ? go.GetComponent<CraftingStation>() : null;
            if (station == null)
            {
                Logger.LogWarning($"Draft Animals: станок '{name}' не найден");
            }
            return station;
        }
    }
}
