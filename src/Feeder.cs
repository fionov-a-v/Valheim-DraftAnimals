using System.Collections.Generic;
using DraftAnimals.Logic;
using DraftAnimals.Visuals;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Кормушка — корыто с инвентарём (обычный сундук игры: открывается E, в ней ничего не лежит на земле, поэтому
    /// проходящий мимо игрок ничего не подберёт). Голодные звери рядом сами подходят и съедают по одной своей еде.
    /// Убирает еду из инвентаря только владелец кормушки — по просьбе зверя — и отвечает зверю, что тот сыт.
    /// </summary>
    public sealed class Feeder : MonoBehaviour
    {
        /// <summary>Слой корма в корыте: виден, когда в кормушке что-то лежит; чем полнее, тем выше.</summary>
        public GameObject m_fodder;

        private static readonly List<Feeder> s_all = new List<Feeder>();

        private ZNetView m_nview;
        private Container m_container;
        private float m_fillTimer;

        public bool Valid => m_nview != null && m_nview.IsValid() && m_container != null && m_container.GetInventory() != null;

        public Vector3 Center => transform.TransformPoint(new Vector3(0f, (Art.FeederFloor + Art.FeederTop) * 0.5f, 0f));

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_container = GetComponent<Container>();
            s_all.Add(this);
            if (m_nview != null && m_nview.IsValid())
            {
                m_nview.Register<ZDOID, string>("DA_Eat", RPC_Eat);
            }
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
        }

        private void Update()
        {
            m_fillTimer -= Time.deltaTime;
            if (m_fillTimer > 0f || m_fodder == null || !Valid)
            {
                return;
            }
            m_fillTimer = 1f;
            Inventory inventory = m_container.GetInventory();
            bool any = inventory.NrOfItems() > 0;
            if (m_fodder.activeSelf != any)
            {
                m_fodder.SetActive(any);
            }
            if (any)
            {
                float fill = Mathf.Clamp01(inventory.SlotsUsedPercentage() / 100f);
                float y = Art.FeederFloor + 0.06f + fill * (Art.FeederTop - Art.FeederFloor - 0.1f);
                m_fodder.transform.localPosition = new Vector3(0f, y, 0f);
            }
        }

        /// <summary>Что из лежащего в кормушке этот зверь ест (имя предмета) или null.</summary>
        public string FindFood(TameAnimal animal)
        {
            if (!Valid)
            {
                return null;
            }
            foreach (ItemDrop.ItemData item in m_container.GetInventory().GetAllItems())
            {
                if (animal.CanEat(item))
                {
                    return item.m_shared.m_name;
                }
            }
            return null;
        }

        public bool Contains(string sharedName)
        {
            return Valid && !string.IsNullOrEmpty(sharedName) && m_container.GetInventory().GetItem(sharedName) != null;
        }

        /// <summary>Где встать, чтобы есть: у ближайшего к from края корыта, на standOff от него.</summary>
        public Vector3 EatPoint(Vector3 from, float standOff)
        {
            Vector3 local = transform.InverseTransformPoint(from);
            DraftRules.FeederEatPoint(local.x, local.z, Art.FeederHalfLength, Art.FeederHalfWidth, standOff, out float x, out float z);
            return transform.TransformPoint(new Vector3(x, 0f, z));
        }

        /// <summary>
        /// Ближайшая кормушка с едой для зверя (привязанному — только та, до которой он дотянется на верёвке).
        /// </summary>
        public static Feeder FindFor(TameAnimal animal, out string food)
        {
            food = null;
            Feeder best = null;
            float bestDistance = DraftConfig.FeederRange.Value;
            Vector3 position = animal.transform.position;
            bool tied = animal.IsTied;
            Vector3 post = tied ? animal.TiePos : position;
            float reach = TameAnimal.TetherRadius + 0.3f;
            foreach (Feeder feeder in s_all)
            {
                if (feeder == null || !feeder.Valid)
                {
                    continue;
                }
                float d = Vector3.Distance(feeder.transform.position, position);
                if (d >= bestDistance)
                {
                    continue;
                }
                if (tied && Utils.DistanceXZ(feeder.EatPoint(post, animal.Radius + 0.15f), post) > reach)
                {
                    continue;
                }
                string item = feeder.FindFood(animal);
                if (item == null)
                {
                    continue;
                }
                best = feeder;
                bestDistance = d;
                food = item;
            }
            return best;
        }

        public void RequestEat(ZDOID animal, string sharedName)
        {
            if (m_nview != null && m_nview.IsValid())
            {
                m_nview.InvokeRPC("DA_Eat", animal, sharedName);
            }
        }

        private void RPC_Eat(long sender, ZDOID animal, string sharedName)
        {
            if (!m_nview.IsOwner() || !Valid)
            {
                return;
            }
            Inventory inventory = m_container.GetInventory();
            ItemDrop.ItemData item = inventory.GetItem(sharedName);
            if (item == null || !inventory.RemoveOneItem(item))
            {
                return;
            }
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(animal) : null;
            ZNetView view = go != null ? go.GetComponent<ZNetView>() : null;
            if (view != null && view.IsValid())
            {
                view.InvokeRPC("DA_FeederFed", sharedName);
            }
        }
    }
}
