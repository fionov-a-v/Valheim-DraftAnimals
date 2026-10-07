using System.Text;
using HarmonyLib;
using UnityEngine;

namespace DraftAnimals
{
    [HarmonyPatch]
    internal static class Patches
    {
        // ================================================================== префабы

        /// <summary>Свои компоненты на ванильные префабы — до того, как зверь или тележка появятся в мире.</summary>
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        [HarmonyPostfix]
        private static void ZNetScene_Awake(ZNetScene __instance)
        {
            PrefabSetup.Apply(__instance);
        }

        // ================================================================== ИИ

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        [HarmonyPrefix]
        private static bool MonsterAI_UpdateAI(MonsterAI __instance, float dt, ref bool __result)
        {
            return !HarnessAnimal.TryGet(__instance, out HarnessAnimal animal) || animal.PreUpdateAI(dt, ref __result);
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        [HarmonyPostfix]
        private static void MonsterAI_UpdateAI_Post(MonsterAI __instance, float dt)
        {
            if (HarnessAnimal.TryGet(__instance, out HarnessAnimal animal))
            {
                animal.PostUpdateAI(dt);
            }
        }

        [HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
        [HarmonyPrefix]
        private static bool AnimalAI_UpdateAI(AnimalAI __instance, float dt, ref bool __result)
        {
            return !HarnessAnimal.TryGet(__instance, out HarnessAnimal animal) || animal.PreUpdateAI(dt, ref __result);
        }

        [HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
        [HarmonyPostfix]
        private static void AnimalAI_UpdateAI_Post(AnimalAI __instance, float dt)
        {
            if (HarnessAnimal.TryGet(__instance, out HarnessAnimal animal))
            {
                animal.PostUpdateAI(dt);
            }
        }

        /// <summary>Голодный ручной зверь сначала ищет кормушку со своей едой, потом — еду на земле (ванильный поиск).</summary>
        [HarmonyPatch(typeof(MonsterAI), "UpdateConsumeItem")]
        [HarmonyPrefix]
        private static bool MonsterAI_UpdateConsumeItem(MonsterAI __instance, float dt, ref bool __result)
        {
            if (TameAnimal.TryGet(__instance, out TameAnimal animal) && animal.UpdateFeeder(dt))
            {
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>Привязанный: цель за пределами верёвки — на её край.</summary>
        [HarmonyPatch(typeof(BaseAI), "MoveTo")]
        [HarmonyPrefix]
        private static void BaseAI_MoveTo(BaseAI __instance, ref Vector3 point)
        {
            if (TameAnimal.TryGet(__instance, out TameAnimal animal))
            {
                animal.ClampTarget(ref point);
            }
        }

        /// <summary>Привязанный у края верёвки идёт вдоль круга, а не упирается.</summary>
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
        [HarmonyPrefix]
        private static void BaseAI_MoveTowards(BaseAI __instance, ref Vector3 dir)
        {
            if (TameAnimal.TryGet(__instance, out TameAnimal animal))
            {
                animal.SteerDirection(ref dir);
            }
        }

        /// <summary>Привязанный не замечает еду на земле, до которой не дотянется.</summary>
        [HarmonyPatch(typeof(MonsterAI), "FindClosestConsumableItem")]
        [HarmonyPostfix]
        private static void MonsterAI_FindClosestConsumableItem(MonsterAI __instance, ref ItemDrop __result)
        {
            if (__result != null && TameAnimal.TryGet(__instance, out TameAnimal animal) && !animal.CanReach(__result.transform.position))
            {
                __result = null;
            }
        }

        // ================================================================== ручные звери

        /// <summary>Предмет с панели быстрого доступа, глядя на зверя: упряжь надевается, корм скармливается.</summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
        [HarmonyPrefix]
        private static bool Tameable_UseItem(Tameable __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || user == null || item == null)
            {
                return true;
            }
            HarnessKind kind = Items.KindOf(item);
            if (kind != HarnessKind.None)
            {
                __result = animal.TryPutHarness(user, item, kind);
                return false;
            }
            float food = animal.FoodMultiplier(item);
            if (food > 0f && animal.TryFeed(user, item, food))
            {
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Зверь в упряжи: E — «за мной / стой» (кабан и олень без упряжи команд не слушаются), Shift+E — снять упряжь
        /// (вместо переименования).
        /// </summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        [HarmonyPrefix]
        private static bool Tameable_Interact(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || hold || !animal.IsTamed || !animal.IsHarnessed)
            {
                return true;
            }
            if (alt)
            {
                __result = animal.TryRemoveHarness(user);
                return false;
            }
            if (animal.Species == Species.Wolf)
            {
                return true;
            }
            __instance.Command(user);
            __instance.m_petEffect.Create(__instance.transform.position, __instance.transform.rotation);
            __result = true;
            return false;
        }

        /// <summary>Команда оленю: у него нет MonsterAI, ванильный обработчик её бы молча проглотил.</summary>
        [HarmonyPatch(typeof(Tameable), "RPC_Command")]
        [HarmonyPrefix]
        private static bool Tameable_RPC_Command(Tameable __instance, ZDOID characterID, bool message)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || animal.Species != Species.Deer)
            {
                return true;
            }
            animal.DeerCommand(characterID, message);
            return false;
        }

        /// <summary>Подсказка над зверем в упряжи: Shift+E снимает упряжь, а не переименовывает.</summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        [HarmonyPrefix]
        private static bool Tameable_GetHoverText(Tameable __instance, ref string __result)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || !animal.Valid || !animal.IsTamed || !animal.IsHarnessed)
            {
                return true;
            }
            var text = new StringBuilder();
            text.Append(__instance.GetHoverName());
            text.Append(" ( $hud_tame, ").Append(__instance.GetStatusString()).Append(" )");
            text.Append("\n$da_hover_harness");
            if (animal.IsHitched)
            {
                text.Append(", $da_hover_hitched");
            }
            text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] ").Append(animal.Species == Species.Wolf ? "$hud_pet" : "$da_follow_toggle");
            if (!animal.IsHitched)
            {
                text.Append("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $da_remove_harness");
            }
            __result = Localization.instance.Localize(text.ToString());
            return false;
        }

        /// <summary>
        /// Подсказка над ручным зверем (и ванильная, и наша): «на привязи» и строка о потомстве — почему его нет или
        /// сколько осталось.
        /// </summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        [HarmonyPostfix]
        private static void Tameable_GetHoverText_Post(Tameable __instance, ref string __result)
        {
            TameAnimal animal = __instance.GetComponent<TameAnimal>();
            if (animal == null || !animal.Valid || string.IsNullOrEmpty(__result))
            {
                return;
            }
            int line = __result.IndexOf('\n');
            if (line < 0)
            {
                line = __result.Length;
            }
            string extra = animal.IsTied ? " · $da_hover_tied" : "";
            string breeding = DraftConfig.ShowBreedingInfo.Value ? animal.BreedingStatus() : null;
            if (breeding != null)
            {
                extra += "\n<color=#c8c8c8>$da_breed: " + breeding + "</color>";
            }
            if (extra.Length > 0)
            {
                __result = __result.Insert(line, Localization.instance.Localize(extra));
            }
        }

        /// <summary>Консольная «tame» приручает и оленей: ванильный Tame без MonsterAI ничего не делает.</summary>
        [HarmonyPatch(typeof(Tameable), "Tame")]
        [HarmonyPrefix]
        private static bool Tameable_Tame(Tameable __instance)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || animal.Species != Species.Deer)
            {
                return true;
            }
            animal.TameByCommand();
            return false;
        }

        /// <summary>У оленя нет MonsterAI, и ваниль не видит, что он напуган.</summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
        [HarmonyPostfix]
        private static void Tameable_GetStatusString(Tameable __instance, ref string __result)
        {
            HarnessAnimal animal = __instance.GetComponent<HarnessAnimal>();
            if (animal == null || animal.Species != Species.Deer)
            {
                return;
            }
            BaseAI ai = animal.Character != null ? animal.Character.GetBaseAI() : null;
            if (ai != null && ai.IsAlerted())
            {
                __result = "$hud_tamefrightened";
            }
        }

        // ================================================================== тележка

        /// <summary>Shift+E у оглобель — запрячь / распрячь; пока зверь запряжён, за оглобли тележку не взять.</summary>
        [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact))]
        [HarmonyPrefix]
        private static bool Vagon_Interact(Vagon __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold)
            {
                return true;
            }
            CartHitch hitch = __instance.GetComponent<CartHitch>();
            if (hitch == null)
            {
                return true;
            }
            if (alt)
            {
                hitch.ToggleHitch(character);
                __result = false;
                return false;
            }
            if (hitch.IsHitched)
            {
                character.Message(MessageHud.MessageType.Center, "$da_msg_cart_hitched");
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Vagon), nameof(Vagon.GetHoverText))]
        [HarmonyPrefix]
        private static bool Vagon_GetHoverText(Vagon __instance, ref string __result)
        {
            CartHitch hitch = __instance.GetComponent<CartHitch>();
            if (hitch == null || !hitch.Valid)
            {
                return true;
            }
            __result = hitch.GetHoverText();
            return false;
        }

        [HarmonyPatch(typeof(Vagon), "LateUpdate")]
        [HarmonyPostfix]
        private static void Vagon_LateUpdate(Vagon __instance)
        {
            CartHitch hitch = __instance.GetComponent<CartHitch>();
            if (hitch != null)
            {
                hitch.DrawTraces();
            }
        }

        // ================================================================== HUD возницы

        /// <summary>Панель скакуна (как у локса): здоровье и выносливость запряжённого зверя, груз.</summary>
        [HarmonyPatch(typeof(Hud), "UpdateMount")]
        [HarmonyPrefix]
        private static bool Hud_UpdateMount(Hud __instance, Player player)
        {
            if (player == null || !(player.GetDoodadController() is CartSeat seat))
            {
                return true;
            }
            HarnessAnimal animal = seat.Animal;
            if (animal == null || !DraftConfig.ShowMountHud.Value)
            {
                __instance.m_mountPanel.SetActive(false);
                return false;
            }
            Character character = animal.Character;
            __instance.m_mountPanel.SetActive(true);
            __instance.m_mountIcon.overrideSprite = Items.Icon(animal.Info.Harness);
            float health = character.GetHealthPercentage();
            __instance.m_mountHealthBarSlow.SetValue(health);
            __instance.m_mountHealthBarFast.SetValue(health);
            __instance.m_mountHealthText.text = Mathf.CeilToInt(character.GetHealth()).ToString();
            float max = Mathf.Max(1f, animal.MaxStamina);
            float stamina = animal.Stamina;
            __instance.m_mountStaminaBar.SetValue(stamina / max);
            __instance.m_mountStaminaText.text = Mathf.CeilToInt(stamina).ToString();
            CartHitch cart = seat.Hitch;
            string status = animal.Tameable != null ? animal.Tameable.GetStatusString() : "";
            string text = animal.DisplayName + " (" + status + " )";
            if (cart != null)
            {
                float load = cart.GetLoad();
                float capacity = animal.GetCapacity();
                text += "  $da_hover_load " + Mathf.RoundToInt(load) + "/" + Mathf.RoundToInt(capacity);
                if (load > capacity)
                {
                    text += " <color=#ffa040>$da_hover_overload</color>";
                }
            }
            __instance.m_mountNameText.text = Localization.instance.Localize(text);
            return false;
        }
    }
}
