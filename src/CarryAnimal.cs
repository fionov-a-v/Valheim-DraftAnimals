using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Ручного зверя можно взять в сумку (заяц, курица): E на звере — владелец зверя убирает его из мира и отдаёт
    /// игроку предмет «живой зверь» (<see cref="LiveAnimal"/>). Дикого — нельзя.
    /// </summary>
    public sealed class CarryAnimal : MonoBehaviour
    {
        private ZNetView m_nview;
        private Character m_character;

        private bool Valid => m_nview != null && m_nview.IsValid();
        public bool CanCarry => Valid && m_character != null && !m_character.IsDead() && m_character.IsTamed();

        private string PrefabName => Utils.GetPrefabName(gameObject);

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            if (Valid)
            {
                m_nview.Register<long>("DA_Bag", RPC_Bag);
            }
        }

        /// <summary>Взять в сумку (у игрока, который нажал E). false — нельзя (дикий или сумка полна).</summary>
        public bool TryTake(Player player)
        {
            if (player == null || !CanCarry)
            {
                return false;
            }
            if (!LiveAnimal.CanTake(player, PrefabName))
            {
                player.Message(MessageHud.MessageType.Center, "$da_msg_bag_full");
                return false;
            }
            m_nview.InvokeRPC("DA_Bag", ZNet.GetUID());
            return true;
        }

        private void RPC_Bag(long sender, long peer)
        {
            if (!m_nview.IsOwner() || !CanCarry)
            {
                return;
            }
            LiveAnimal.Give(peer, PrefabName, m_character.GetLevel(), m_nview.GetZDO().GetString(ZDOVars.s_tamedName));
            ZNetScene.instance.Destroy(gameObject);
        }
    }
}
