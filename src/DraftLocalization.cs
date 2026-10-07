using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace DraftAnimals
{
    internal static class DraftLocalization
    {
        public static void Register()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            loc.AddTranslation("English", new Dictionary<string, string>
            {
                { "da_harness", "Harness" },
                { "da_harness_desc", "A leather harness with a wooden shaft bow, a bell and a girth for a tame boar or deer. Use it from the hotbar while looking at the animal, lead the animal to a cart's shafts and press Shift+E at the shafts." },
                { "da_heavyharness", "Heavy harness" },
                { "da_heavyharness_desc", "A wolf-pelt harness with an iron-fitted shaft bow: only it holds a tame wolf. Use it on the wolf, lead it to a cart's shafts and press Shift+E at the shafts." },

                { "da_seat", "Driver's bench" },
                { "da_drive", "Take the reins" },
                { "da_seat_no_animal", "No animal is hitched to this cart" },

                { "da_hitch", "Hitch" },
                { "da_unhitch", "Unhitch" },
                { "da_hover_load", "Load" },
                { "da_hover_overload", "overloaded" },
                { "da_hover_harness", "In harness" },
                { "da_hover_hitched", "hitched to a cart" },
                { "da_follow_toggle", "Follow / stay" },
                { "da_remove_harness", "Take off the harness" },

                { "da_msg_hitched", "is hitched to the cart" },
                { "da_msg_unhitched", "Unhitched" },
                { "da_msg_no_animal", "No animal in harness nearby: put a harness on a tame animal and bring it to the shafts" },
                { "da_msg_cart_busy", "Someone is pulling this cart" },
                { "da_msg_cart_open", "Someone has this cart open" },
                { "da_msg_cart_hitched", "An animal is hitched to this cart — Shift+E to unhitch" },
                { "da_msg_not_tamed", "Only a tame animal lets you put a harness on it" },
                { "da_msg_disabled", "This animal cannot be harnessed on this server" },
                { "da_msg_already", "already wears a harness" },
                { "da_msg_need_heavy", "A wolf needs a heavy harness" },
                { "da_msg_need_light", "The heavy harness is for wolves" },
                { "da_msg_harnessed", "now wears a harness" },
                { "da_msg_unhitch_first", "Unhitch it from the cart first" },
                { "da_msg_wild_food", "A wild deer won't eat from your hand: leave the food on the ground and step away quietly" },
                { "da_msg_not_hungry", "is not hungry" },
                { "da_msg_eats", "eats" },
                { "da_msg_balks", "balks: an enemy is near!" },

                { "da_post", "Tether post" },
                { "da_post_desc", "A stake with an iron ring. E on the post ties the nearest tame animal: it stays within a few metres of the post. E again unties it." },
                { "da_tie", "Tie" },
                { "da_untie", "Untie" },
                { "da_post_no_animal", "No tame animal nearby" },
                { "da_msg_tied", "is tied to the post" },
                { "da_msg_untied", "is untied" },
                { "da_hover_tied", "tied" },
                { "da_feeder", "Feeder" },
                { "da_fawn", "Fawn" },
                { "da_breed", "Young" },
                { "da_breed_pregnant", "expecting, soon" },
                { "da_breed_hungry", "hungry — feed it" },
                { "da_breed_scared", "frightened" },
                { "da_breed_crowded", "too crowded" },
                { "da_breed_nopartner", "no ready partner within" },
                { "da_breed_love", "getting along" },
                { "da_unit_m", "m" },
                { "da_feeder_desc", "A trough with storage for tame animals. Put their food inside: hungry animals nearby come and eat from it, and passers-by won't pick it up." },
            });

            loc.AddTranslation("Russian", new Dictionary<string, string>
            {
                { "da_harness", "Упряжь" },
                { "da_harness_desc", "Кожаная упряжь с деревянной дугой, колокольчиком и подпругой для ручного кабана или оленя. Используйте её с панели быстрого доступа, глядя на зверя, подведите зверя к оглоблям повозки и нажмите у оглобель Shift+E." },
                { "da_heavyharness", "Тяжёлая упряжь" },
                { "da_heavyharness_desc", "Упряжь из шкур волка с окованной дугой: только она удержит ручного волка. Наденьте её на волка, подведите его к оглоблям повозки и нажмите у оглобель Shift+E." },

                { "da_seat", "Козлы" },
                { "da_drive", "Взять вожжи" },
                { "da_seat_no_animal", "В повозку никто не запряжён" },

                { "da_hitch", "Запрячь" },
                { "da_unhitch", "Распрячь" },
                { "da_hover_load", "Груз" },
                { "da_hover_overload", "перегруз" },
                { "da_hover_harness", "В упряжи" },
                { "da_hover_hitched", "запряжён в повозку" },
                { "da_follow_toggle", "За мной / стой" },
                { "da_remove_harness", "Снять упряжь" },

                { "da_msg_hitched", "запряжён в повозку" },
                { "da_msg_unhitched", "Распряжено" },
                { "da_msg_no_animal", "Рядом нет зверя в упряжи: наденьте упряжь на ручного зверя и подведите его к оглоблям" },
                { "da_msg_cart_busy", "Эту повозку кто-то тянет" },
                { "da_msg_cart_open", "Эту повозку кто-то открыл" },
                { "da_msg_cart_hitched", "В повозку запряжён зверь — Shift+E, чтобы распрячь" },
                { "da_msg_not_tamed", "Упряжь можно надеть только на ручного зверя" },
                { "da_msg_disabled", "На этом сервере этого зверя запрягать нельзя" },
                { "da_msg_already", "уже в упряжи" },
                { "da_msg_need_heavy", "Волку нужна тяжёлая упряжь" },
                { "da_msg_need_light", "Тяжёлая упряжь — для волка" },
                { "da_msg_harnessed", "теперь в упряжи" },
                { "da_msg_unhitch_first", "Сначала распрягите его" },
                { "da_msg_wild_food", "Дикий олень с руки не ест: оставьте корм на земле и тихо отойдите" },
                { "da_msg_not_hungry", "не голоден" },
                { "da_msg_eats", "ест" },
                { "da_msg_balks", "упирается: рядом враг!" },

                { "da_post", "Привязь" },
                { "da_post_desc", "Кол с железным кольцом. E на колу привязывает ближайшего ручного зверя: он ходит не дальше нескольких метров от кола. Ещё раз E — отвязать." },
                { "da_tie", "Привязать" },
                { "da_untie", "Отвязать" },
                { "da_post_no_animal", "Рядом нет ручного зверя" },
                { "da_msg_tied", "привязан к колу" },
                { "da_msg_untied", "отвязан" },
                { "da_hover_tied", "на привязи" },
                { "da_feeder", "Кормушка" },
                { "da_fawn", "Оленёнок" },
                { "da_breed", "Потомство" },
                { "da_breed_pregnant", "скоро будет" },
                { "da_breed_hungry", "голоден — покормите" },
                { "da_breed_scared", "напуган" },
                { "da_breed_crowded", "тесно" },
                { "da_breed_nopartner", "нет готовой пары ближе" },
                { "da_breed_love", "симпатия" },
                { "da_unit_m", "м" },
                { "da_feeder_desc", "Корыто с ящиком для ручных зверей. Положите в него их еду: голодные звери рядом сами подойдут и поедят, а проходящий мимо её не подберёт." },
            });
        }
    }
}
