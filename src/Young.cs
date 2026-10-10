using System;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace DraftAnimals
{
    /// <summary>
    /// Детёныши, которых нет в игре: оленёнок и зайчонок. Как поросёнок у кабана: тот же зверь вдвое меньше (уменьшены
    /// модель, капсула и точка глаз), без добычи, с ванильным Growup — через время роста становится взрослым ручным зверем
    /// того же уровня. Ходят на ванильном ИИ животных (ручной убегает только от врагов).
    /// </summary>
    internal static class Young
    {
        public const string FawnPrefab = "DA_Fawn";
        public const string HareKitPrefab = "DA_HareKit";

        /// <summary>Во сколько раз детёныш меньше взрослого.</summary>
        private const float Scale = 0.5f;

        public static GameObject Fawn { get; private set; }
        public static GameObject HareKit { get; private set; }

        public static void Register()
        {
            // Оленёнка можно привязать к колу, зайчонка — нет (зайцы не привязываются).
            Fawn = Create(FawnPrefab, "Deer", "$da_fawn", 6f, 4f, DraftConfig.FawnGrowTime.Value, true);
            HareKit = Create(HareKitPrefab, HareAnimal.PrefabName, "$da_harekit", 6f, 3f, DraftConfig.HareKitGrowTime.Value, false);
        }

        private static GameObject Create(string name, string adultName, string token, float runSpeed, float mass, float growTime,
            bool tetherable)
        {
            try
            {
                GameObject adult = PrefabManager.Instance.GetPrefab(adultName);
                if (adult == null || adult.GetComponent<Character>() == null)
                {
                    Logger.LogError($"Draft Animals: ванильный '{adultName}' не найден — детёнышей не будет");
                    return null;
                }
                GameObject young = PrefabManager.Instance.CreateClonedPrefab(name, adult);
                // Если взрослый уже дополнен модом (сцена создавалась раньше) — убираем лишнее: упряжь, размножение, добычу.
                foreach (Type type in new[]
                         {
                             typeof(HarnessAnimal), typeof(HareAnimal), typeof(TameAnimal), typeof(Procreation), typeof(Tameable),
                             typeof(CharacterDrop),
                         })
                {
                    Component c = young.GetComponent(type);
                    if (c != null)
                    {
                        Object.DestroyImmediate(c);
                    }
                }

                Character character = young.GetComponent<Character>();
                character.m_name = token;
                character.m_runSpeed = runSpeed;
                // Падающая тушка — того же размера, что и модель детёныша.
                foreach (EffectList.EffectData effect in character.m_deathEffects.m_effectPrefabs)
                {
                    effect.m_multiplyParentVisualScale = true;
                }

                Transform visual = young.transform.Find("Visual");
                if (visual != null)
                {
                    visual.localScale = visual.localScale * Scale;
                    visual.localPosition = visual.localPosition * Scale;
                }
                Transform eye = young.transform.Find("EyePos");
                if (eye != null)
                {
                    eye.localPosition = eye.localPosition * Scale;
                }
                CapsuleCollider capsule = young.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.radius *= Scale;
                    capsule.height *= Scale;
                    capsule.center *= Scale;
                }
                Rigidbody body = young.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.mass = mass;
                }

                Growup growup = young.AddComponent<Growup>();
                growup.m_growTime = growTime;
                growup.m_inheritTame = true;
                growup.m_grownPrefab = adult;

                young.AddComponent<TameAnimal>().m_tetherable = tetherable;
                PrefabManager.Instance.AddPrefab(young);
                return young;
            }
            catch (Exception e)
            {
                Logger.LogError($"Draft Animals: не удалось создать детёныша {name}: {e}");
                return null;
            }
        }
    }
}
