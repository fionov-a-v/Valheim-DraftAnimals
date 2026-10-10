using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace DraftAnimals
{
    /// <summary>
    /// «Живой заяц» — заяц в сумке. Получается из силка (пойманный дикий) или с ручного зайца (E). Проходит через порталы.
    /// Выпускается уже ручным: использовать из инвентаря — перед игроком, выбросить — где упал. Уровень и имя зайца
    /// хранятся в предмете (m_customData), поэтому в стопку живые зайцы не складываются.
    /// </summary>
    internal static class LiveHare
    {
        public const string ItemPrefab = "DA_LiveHare";
        private const string BasePrefab = "TrophyHare";
        private const string Token = "$da_livehare";
        private const string LevelKey = "da_level";
        private const string NameKey = "da_name";

        private static CustomItem s_item;

        public static void Register()
        {
            try
            {
                var config = new ItemConfig { Name = Token, Description = "$da_livehare_desc" };
                var item = new CustomItem(ItemPrefab, BasePrefab, config);
                if (item.ItemPrefab == null || item.ItemDrop == null)
                {
                    Logger.LogError($"Draft Animals: не удалось создать предмет {ItemPrefab} (нет ванильного {BasePrefab})");
                    return;
                }
                ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_maxStackSize = 1;
                shared.m_weight = 2f;
                shared.m_teleportable = true;
                shared.m_value = 0;
                Sprite icon = RenderIcon();
                if (icon != null)
                {
                    shared.m_icons = new[] { icon };
                }
                item.ItemPrefab.AddComponent<LiveHareItem>();
                ItemManager.Instance.AddItem(item);
                s_item = item;
            }
            catch (Exception e)
            {
                Logger.LogError($"Draft Animals: не удалось создать живого зайца: {e}");
            }
        }

        /// <summary>
        /// Иконка — сам заяц из игры (его модель в позе префаба), вид сбоку-спереди. Модель копируется в неактивный
        /// контейнер и без скриптов: CharacterAnimEvent и прочие без зайца-хозяина падали бы в Awake.
        /// </summary>
        private static Sprite RenderIcon()
        {
            GameObject model = null;
            GameObject holder = null;
            try
            {
                GameObject hare = PrefabManager.Instance.GetPrefab(HareAnimal.PrefabName);
                Transform visual = hare != null ? hare.transform.Find("Visual") : null;
                if (visual == null)
                {
                    return null;
                }
                holder = new GameObject("da_livehare_render");
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
                Logger.LogWarning($"Draft Animals: иконка живого зайца не отрисована: {e.Message}");
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

        public static bool Is(ItemDrop.ItemData item)
        {
            return item?.m_shared != null && item.m_shared.m_name == Token;
        }

        /// <summary>Глобальный RPC «отдай игроку зайца»: заяц к этому моменту уже убран из мира, отвечать через него нельзя.</summary>
        public static void RegisterRpc()
        {
            ZRoutedRpc.instance?.Register<int, string>("DA_GiveHare", RPC_GiveHare);
        }

        public static void Give(long peer, int level, string name)
        {
            ZRoutedRpc.instance?.InvokeRoutedRPC(peer, "DA_GiveHare", level, name ?? "");
        }

        /// <summary>Может ли игрок взять зайца (есть место в сумке).</summary>
        public static bool CanTake(Player player)
        {
            ItemDrop.ItemData sample = Create(1, "");
            return player != null && sample != null && player.GetInventory().CanAddItem(sample);
        }

        private static ItemDrop.ItemData Create(int level, string name)
        {
            GameObject prefab = s_item != null ? s_item.ItemPrefab : null;
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

        private static void RPC_GiveHare(long sender, int level, string name)
        {
            Player player = Player.m_localPlayer;
            ItemDrop.ItemData data = Create(level, name);
            if (player == null || data == null)
            {
                return;
            }
            if (player.GetInventory().CanAddItem(data))
            {
                player.GetInventory().AddItem(data);
                player.Message(MessageHud.MessageType.Center, "$da_msg_hare_bagged");
            }
            else
            {
                // Сумка полна — заяц тут же выскакивает на землю (выброшенный предмет снова становится зайцем).
                ItemDrop.DropItem(data, 1, player.transform.position + player.transform.forward + Vector3.up * 0.5f, Quaternion.identity);
                player.Message(MessageHud.MessageType.Center, "$da_msg_bag_full");
            }
        }

        /// <summary>Выпустить из инвентаря: ручной заяц появляется перед игроком.</summary>
        public static void ReleaseFromInventory(Humanoid user, ItemDrop.ItemData item)
        {
            Vector3 forward = user.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 position = user.transform.position + forward * 1.2f;
            if (ZoneSystem.instance != null)
            {
                position.y = Mathf.Max(position.y, ZoneSystem.instance.GetSolidHeight(position));
            }
            if (Release(position, Quaternion.LookRotation(forward), item))
            {
                user.GetInventory().RemoveItem(item);
                user.Message(MessageHud.MessageType.Center, "$da_msg_hare_released");
            }
        }

        /// <summary>Создать ручного зайца с уровнем и именем из предмета.</summary>
        public static bool Release(Vector3 position, Quaternion rotation, ItemDrop.ItemData item)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(HareAnimal.PrefabName) : null;
            if (prefab == null)
            {
                return false;
            }
            int level = 1;
            string name = "";
            if (item?.m_customData != null)
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
            return true;
        }
    }

    /// <summary>Выброшенный «Живой заяц» сразу становится ручным зайцем там, где упал.</summary>
    public sealed class LiveHareItem : MonoBehaviour
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
            if (ZoneSystem.instance != null)
            {
                position.y = Mathf.Max(position.y - 0.5f, ZoneSystem.instance.GetSolidHeight(position));
            }
            if (LiveHare.Release(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), drop.m_itemData))
            {
                view.Destroy();
            }
        }
    }
}
