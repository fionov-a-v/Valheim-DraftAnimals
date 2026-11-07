using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace DraftAnimals
{
    /// <summary>
    /// Живой зверь в сумке — заяц или курица; предмет называется так же, как сам зверь. Ручного зверя берут E
    /// (<see cref="CarryAnimal"/>), предмет проходит через порталы и выпускается ручным: использовать из инвентаря —
    /// перед игроком, выбросить — где упал. Уровень и имя зверя хранятся в предмете (m_customData), поэтому в стопку
    /// живые звери не складываются; имя видно в подсказке предмета.
    /// </summary>
    internal static class LiveAnimal
    {
        private const string LevelKey = "da_level";
        private const string NameKey = "da_name";

        /// <summary>Вид зверя в сумке: предмет (клон ванильного BasePrefab) и префаб самого зверя.</summary>
        private sealed class Kind
        {
            public string ItemPrefab;
            public string BasePrefab;
            public string AnimalPrefab;
            /// <summary>Имя предмета — ванильное имя зверя (по нему же предмет узнаётся).</summary>
            public string Token;
            public string Description;
            public float Weight;
            public CustomItem Item;
        }

        private static readonly List<Kind> s_kinds = new List<Kind>
        {
            new Kind
            {
                ItemPrefab = "DA_LiveHare", BasePrefab = "TrophyHare", AnimalPrefab = HareAnimal.PrefabName, Token = "$enemy_hare",
                Description = "$da_livehare_desc", Weight = 2f,
            },
            new Kind
            {
                ItemPrefab = "DA_LiveHen", BasePrefab = "Feathers", AnimalPrefab = "Hen", Token = "$enemy_hen",
                Description = "$da_livehen_desc", Weight = 3f,
            },
        };

        /// <summary>Префабы зверей, которых можно взять в сумку.</summary>
        public static IEnumerable<string> AnimalPrefabs
        {
            get
            {
                foreach (Kind kind in s_kinds)
                {
                    yield return kind.AnimalPrefab;
                }
            }
        }

        public static void Register()
        {
            foreach (Kind kind in s_kinds)
            {
                Register(kind);
            }
        }

        private static void Register(Kind kind)
        {
            try
            {
                var config = new ItemConfig { Name = kind.Token, Description = kind.Description };
                var item = new CustomItem(kind.ItemPrefab, kind.BasePrefab, config);
                if (item.ItemPrefab == null || item.ItemDrop == null)
                {
                    Logger.LogError($"Draft Animals: не удалось создать предмет {kind.ItemPrefab} (нет ванильного {kind.BasePrefab})");
                    return;
                }
                ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_maxStackSize = 1;
                shared.m_weight = kind.Weight;
                shared.m_teleportable = true;
                shared.m_value = 0;
                Sprite icon = RenderIcon(kind.AnimalPrefab);
                if (icon != null)
                {
                    shared.m_icons = new[] { icon };
                }
                item.ItemPrefab.AddComponent<LiveAnimalItem>();
                ItemManager.Instance.AddItem(item);
                kind.Item = item;
            }
            catch (Exception e)
            {
                Logger.LogError($"Draft Animals: не удалось создать {kind.ItemPrefab}: {e}");
            }
        }

        /// <summary>
        /// Иконка — сам зверь из игры (его модель в позе префаба), вид сбоку-спереди. Модель копируется в неактивный
        /// контейнер и без скриптов: CharacterAnimEvent и прочие без зверя-хозяина падали бы в Awake.
        /// </summary>
        private static Sprite RenderIcon(string animalPrefab)
        {
            GameObject model = null;
            GameObject holder = null;
            try
            {
                GameObject animal = PrefabManager.Instance.GetPrefab(animalPrefab);
                Transform visual = animal != null ? animal.transform.Find("Visual") : null;
                if (visual == null)
                {
                    return null;
                }
                holder = new GameObject("da_live_render");
                holder.SetActive(false);
                model = Object.Instantiate(visual.gameObject, holder.transform);
                foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Object.DestroyImmediate(script);
                }
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                {
                    animator.enabled = false;
                }
                model.transform.SetParent(null, false);
                model.transform.position = new Vector3(0f, -5000f, 0f);
                model.transform.rotation = Quaternion.identity;
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(model)
                {
                    Width = 128,
                    Height = 128,
                    Rotation = Quaternion.Euler(10f, -140f, 0f),
                    UseCache = false,
                });
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Draft Animals: иконка {animalPrefab} не отрисована: {e.Message}");
                return null;
            }
            finally
            {
                if (model != null)
                {
                    Object.DestroyImmediate(model);
                }
                if (holder != null)
                {
                    Object.DestroyImmediate(holder);
                }
            }
        }

        private static Kind ByItem(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return null;
            }
            foreach (Kind kind in s_kinds)
            {
                if (item.m_shared.m_name == kind.Token)
                {
                    return kind;
                }
            }
            return null;
        }

        private static Kind ByAnimal(string animalPrefab)
        {
            foreach (Kind kind in s_kinds)
            {
                if (kind.AnimalPrefab == animalPrefab)
                {
                    return kind;
                }
            }
            return null;
        }

        public static bool Is(ItemDrop.ItemData item)
        {
            return ByItem(item) != null;
        }

        /// <summary>Имя, данное зверю игроком, — для подсказки предмета (пусто, если не называли).</summary>
        public static string GivenName(ItemDrop.ItemData item)
        {
            return Is(item) && item.m_customData != null && item.m_customData.TryGetValue(NameKey, out string name) ? name : "";
        }

        /// <summary>Глобальный RPC «отдай игроку зверя»: зверь к этому моменту уже убран из мира, отвечать через него нельзя.</summary>
        public static void RegisterRpc()
        {
            ZRoutedRpc.instance?.Register<string, int, string>("DA_GiveAnimal", RPC_GiveAnimal);
        }

        public static void Give(long peer, string animalPrefab, int level, string name)
        {
            ZRoutedRpc.instance?.InvokeRoutedRPC(peer, "DA_GiveAnimal", animalPrefab, level, name ?? "");
        }

        /// <summary>Может ли игрок взять этого зверя (есть место в сумке).</summary>
        public static bool CanTake(Player player, string animalPrefab)
        {
            ItemDrop.ItemData sample = Create(ByAnimal(animalPrefab), 1, "");
            return player != null && sample != null && player.GetInventory().CanAddItem(sample);
        }

        private static ItemDrop.ItemData Create(Kind kind, int level, string name)
        {
            GameObject prefab = kind?.Item != null ? kind.Item.ItemPrefab : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;
            }
            ItemDrop.ItemData data = drop.m_itemData.Clone();
            data.m_dropPrefab = prefab;
            data.m_stack = 1;
            data.m_customData[LevelKey] = Mathf.Max(1, level).ToString();
            if (!string.IsNullOrEmpty(name))
            {
                data.m_customData[NameKey] = name;
            }
            return data;
        }

        private static void RPC_GiveAnimal(long sender, string animalPrefab, int level, string name)
        {
            Player player = Player.m_localPlayer;
            Kind kind = ByAnimal(animalPrefab);
            ItemDrop.ItemData data = Create(kind, level, name);
            if (player == null || data == null)
            {
                return;
            }
            if (player.GetInventory().CanAddItem(data))
            {
                player.GetInventory().AddItem(data);
                player.Message(MessageHud.MessageType.Center, kind.Token + " $da_msg_in_bag");
            }
            else
            {
                // Сумка полна — зверь тут же выскакивает на землю (выброшенный предмет снова становится зверем).
                ItemDrop.DropItem(data, 1, player.transform.position + player.transform.forward + Vector3.up * 0.5f, Quaternion.identity);
                player.Message(MessageHud.MessageType.Center, "$da_msg_bag_full");
            }
        }

        /// <summary>Выпустить из инвентаря: ручной зверь появляется перед игроком на полу (в доме — не на крыше и не за стеной).</summary>
        public static void ReleaseFromInventory(Humanoid user, ItemDrop.ItemData item)
        {
            Vector3 forward = user.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 feet = user.transform.position;
            float distance = Ground.FreeDistance(feet + Vector3.up * 0.5f, forward, 1.2f, 0.35f);
            Vector3 position = feet + forward * distance;
            position.y = Ground.FloorHeight(position, 0.8f, 3f);
            GameObject animal = Release(position, Quaternion.LookRotation(forward), item);
            if (animal != null)
            {
                user.GetInventory().RemoveItem(item);
                Character character = animal.GetComponent<Character>();
                user.Message(MessageHud.MessageType.Center, (character != null ? character.m_name : "") + " $da_msg_released");
            }
        }

        /// <summary>Создать ручного зверя с уровнем и именем из предмета (null — не вышло).</summary>
        public static GameObject Release(Vector3 position, Quaternion rotation, ItemDrop.ItemData item)
        {
            Kind kind = ByItem(item);
            GameObject prefab = kind != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(kind.AnimalPrefab) : null;
            if (prefab == null)
            {
                return null;
            }
            int level = 1;
            string name = "";
            if (item.m_customData != null)
            {
                if (item.m_customData.TryGetValue(LevelKey, out string l))
                {
                    int.TryParse(l, out level);
                }
                item.m_customData.TryGetValue(NameKey, out name);
            }
            GameObject go = Object.Instantiate(prefab, position, rotation);
            Character character = go.GetComponent<Character>();
            if (character != null)
            {
                character.SetTamed(true);
                character.SetLevel(Mathf.Max(1, level));
            }
            ZNetView view = go.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !string.IsNullOrEmpty(name))
            {
                view.GetZDO().Set(ZDOVars.s_tamedName, name);
            }
            // Пасётся там, где выпустили.
            go.GetComponent<BaseAI>()?.SetPatrolPoint();
            return go;
        }
    }

    /// <summary>Выброшенный живой зверь сразу становится ручным зверем там, где упал.</summary>
    public sealed class LiveAnimalItem : MonoBehaviour
    {
        private void Start()
        {
            ZNetView view = GetComponent<ZNetView>();
            ItemDrop drop = GetComponent<ItemDrop>();
            if (view == null || !view.IsValid() || !view.IsOwner() || drop == null)
            {
                return;
            }
            Vector3 position = transform.position;
            position.y = Ground.FloorHeight(position, 0.3f, 5f);
            if (LiveAnimal.Release(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), drop.m_itemData) != null)
            {
                view.Destroy();
            }
        }
    }
}
