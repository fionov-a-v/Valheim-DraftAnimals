using System;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace DraftAnimals
{
    /// <summary>
    /// Оленёнок — детёныш ручных оленей. Как поросёнок у кабана: тот же олень вдвое меньше (уменьшена модель, капсула
    /// и точка глаз), без добычи, с ванильным Growup — через FawnGrowTime становится взрослым ручным оленем того же
    /// уровня. Ходит на ванильном ИИ животных (ручной убегает только от врагов), к колу привязывается.
    /// </summary>
    internal static class Fawn
    {
        public const string PrefabName = "DA_Fawn";
        private const string DeerPrefab = "Deer";

        /// <summary>Во сколько раз оленёнок меньше оленя.</summary>
        private const float Scale = 0.5f;

        public static GameObject Prefab { get; private set; }

        public static void Register()
        {
            try
            {
                GameObject deer = PrefabManager.Instance.GetPrefab(DeerPrefab);
                if (deer == null || deer.GetComponent<Character>() == null)
                {
                    Logger.LogError("Draft Animals: ванильный олень не найден — оленят не будет");
                    return;
                }
                GameObject fawn = PrefabManager.Instance.CreateClonedPrefab(PrefabName, deer);
                // Если олень уже дополнен модом (сцена создавалась раньше) — убираем лишнее: упряжь, размножение, добычу.
                foreach (Type type in new[] { typeof(HarnessAnimal), typeof(TameAnimal), typeof(Procreation), typeof(Tameable), typeof(CharacterDrop) })
                {
                    Component c = fawn.GetComponent(type);
                    if (c != null)
                    {
                        Object.DestroyImmediate(c);
                    }
                }

                Character character = fawn.GetComponent<Character>();
                character.m_name = "$da_fawn";
                character.m_runSpeed = 6f;
                // Падающая тушка — того же размера, что и модель оленёнка.
                foreach (EffectList.EffectData effect in character.m_deathEffects.m_effectPrefabs)
                {
                    effect.m_multiplyParentVisualScale = true;
                }

                Transform visual = fawn.transform.Find("Visual");
                if (visual != null)
                {
                    visual.localScale = visual.localScale * Scale;
                    visual.localPosition = visual.localPosition * Scale;
                }
                Transform eye = fawn.transform.Find("EyePos");
                if (eye != null)
                {
                    eye.localPosition = eye.localPosition * Scale;
                }
                CapsuleCollider capsule = fawn.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.radius *= Scale;
                    capsule.height *= Scale;
                    capsule.center *= Scale;
                }
                Rigidbody body = fawn.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.mass = 4f;
                }

                Growup growup = fawn.AddComponent<Growup>();
                growup.m_growTime = DraftConfig.FawnGrowTime.Value;
                growup.m_inheritTame = true;
                growup.m_grownPrefab = deer;

                fawn.AddComponent<TameAnimal>();
                PrefabManager.Instance.AddPrefab(fawn);
                Prefab = fawn;
            }
            catch (Exception e)
            {
                Logger.LogError($"Draft Animals: не удалось создать оленёнка: {e}");
            }
        }
    }
}
