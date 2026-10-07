using System;
using HarmonyLib;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Доступ к защищённым методам ИИ игры: ходьба по пути, бегство, блуждание, тревога.
    /// Делегаты строятся один раз; «базовое» обновление ИИ — обратный патч Harmony (копия BaseAI.UpdateAI без
    /// виртуального вызова), чтобы, заменив ИИ зверя своим, не потерять реген здоровья и таймеры.
    /// </summary>
    [HarmonyPatch]
    internal static class AiAccess
    {
        private static readonly Func<BaseAI, float, Vector3, float, bool, bool> s_moveTo =
            AccessTools.MethodDelegate<Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));

        private static readonly Func<BaseAI, float, Vector3, bool> s_flee =
            AccessTools.MethodDelegate<Func<BaseAI, float, Vector3, bool>>(AccessTools.Method(typeof(BaseAI), "Flee"));

        private static readonly Action<BaseAI, float> s_idle =
            AccessTools.MethodDelegate<Action<BaseAI, float>>(AccessTools.Method(typeof(BaseAI), "IdleMovement"));

        private static readonly Action<BaseAI, bool> s_setAlerted =
            AccessTools.MethodDelegate<Action<BaseAI, bool>>(AccessTools.Method(typeof(BaseAI), "SetAlerted"));

        private static readonly Action<BaseAI, Vector3> s_lookAt =
            AccessTools.MethodDelegate<Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt"));

        private static readonly Action<BaseAI, Vector3> s_setPatrolPoint =
            AccessTools.MethodDelegate<Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "SetPatrolPoint", new[] { typeof(Vector3) }));

        private static readonly AccessTools.FieldRef<AnimalAI, Character> s_animalTarget =
            AccessTools.FieldRefAccess<AnimalAI, Character>("m_target");

        /// <summary>Идти к точке по пути; true — пришли (или пути нет).</summary>
        public static bool MoveTo(BaseAI ai, float dt, Vector3 point, float distance, bool run) => s_moveTo(ai, dt, point, distance, run);

        public static bool Flee(BaseAI ai, float dt, Vector3 from) => s_flee(ai, dt, from);

        public static void IdleMovement(BaseAI ai, float dt) => s_idle(ai, dt);

        public static void SetAlerted(BaseAI ai, bool alerted) => s_setAlerted(ai, alerted);

        public static void LookAt(BaseAI ai, Vector3 point) => s_lookAt(ai, point);

        /// <summary>Центр, вокруг которого ручной зверь пасётся (ванильный «стой здесь»).</summary>
        public static void SetPatrolPoint(BaseAI ai, Vector3 point) => s_setPatrolPoint(ai, point);

        /// <summary>От кого сейчас убегает дикий олень (ванильный ИИ животных), или null.</summary>
        public static Character AnimalTarget(AnimalAI ai) => ai != null ? s_animalTarget(ai) : null;

        /// <summary>Тело BaseAI.UpdateAI: проверка владельца, реген, таймеры. false — не владелец, ИИ не крутим.</summary>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        public static bool BaseUpdateAI(BaseAI instance, float dt)
        {
            throw new NotImplementedException("Harmony reverse patch");
        }
    }
}
