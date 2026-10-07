# Тягловые звери (Draft Animals) — мод для Valheim

Автор: Fionov Alexander · исходники: https://github.com/fionov-a-v/Valheim-DraftAnimals · [English below](#english)

Ручного кабана, оленя или волка можно запрячь в ванильную повозку. Зверь везёт её за вами сам или под управлением
возницы на козлах — хоть устраивайте гонки в кооперативе. Оленя теперь можно приручить: на приманку, тихо и терпеливо,
и ручные олени приносят оленят. Для хозяйства — привязь (кол с верёвкой) и кормушка, из которой звери едят сами.
Сделано для Valheim 1.0.17 на BepInEx 5 и Jötunn 2.30. Тексты в игре — на русском и английском (по языку игры).

## Установка

Нужны [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) и
[Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Через r2modman/Thunderstore Mod Manager —
установить DraftAnimals, зависимости поставятся сами. Вручную — положить `DraftAnimals.dll` в `BepInEx/plugins`.

Мод нужен **всем** игрокам и серверу (у кого его нет — Jötunn не пустит на сервер). Настройки игры
(грузоподъёмность, скорости, приручение, рецепты) приходят с сервера, менять их может только администратор.

Linux (родная версия): в Steam впишите в параметры запуска Valheim `./start_game_bepinex.sh %command%`.

## Звери

| | Кабан | Олень | Волк |
|---|---|---|---|
| Где взять | Луга, приручается как обычно | Луга, приручается на приманку (см. ниже) | Горы, приручается как обычно |
| Упряжь | Упряжь (верстак) | Упряжь (верстак) | Тяжёлая упряжь (кузница) |
| Везёт без замедления | 900 | 450 | 1200 |
| При двойном грузе | 40% скорости | стоит | 40% скорости |
| Шагом / бегом, м/с | 3 / 5,5 | 3,5 / 8,5 | 3,5 / 7,5 |
| Бег (выносливость 100) | ~20 с, отдых 5/с | ~10 с, отдых 8/с | ~25 с, отдых 6/с |
| Норов | спокойный, в упряжи не дерётся | пугливый: при враге упирается, от удара рвётся прочь | хищник: без возницы бросается в драку, волоча повозку |

Стопка руды весит около 300. Груз — содержимое повозки плюс возница (80 и вес его инвентаря). За каждую звезду зверь
тянет на 25% больше. Голодный запряжённый зверь идёт вдвое медленнее и медленнее восстанавливает выносливость.
Повозка со зверем, как и без него, не проходит через порталы.

Упряжь на звере — деревянная дуга над холкой (у тяжёлой — окованная) с колокольчиком и подпруга; постромки идут от
концов оглобель к кольцам на концах дуги.

## Как запрячь

1. Сделайте упряжь у станка: **Упряжь** — Кожаные обрывки ×10, Шкура оленя ×4, Бронзовые гвозди ×5 (Верстак);
   **Тяжёлая упряжь** — Шкура волка ×4, Кожаные обрывки ×10, Серебро ×2 (Кузница).
2. Глядя на ручного зверя, используйте упряжь с панели быстрого доступа (как седло на быкоящере).
3. Зверь в упряжи слушается: **E** — «за мной / стой» (кабан и олень без упряжи команд не слушаются),
   **Shift+E** — снять упряжь. Еду с панели быстрого доступа зверь ест с руки.
4. Подведите зверя к оглоблям повозки и нажмите у оглобель **Shift+E** — зверь встанет в оглобли. Ещё раз **Shift+E** — распрячь.

Запряжённый зверь идёт за тем, за кем шёл, если тот ближе 40 м; иначе стоит и ждёт. Пока зверь запряжён, повозку
нельзя взять за оглобли, а её ящик открывается как обычно.

## Возница

На передней стенке повозки — **козлы**. Сядьте (**E**) — и правьте зверем, как быкоящером в седле:

- **W** — шагом в сторону взгляда (зверь идёт и без удержания), **Shift** — бегом, **S** — стоп;
- **ПКМ** — развернуться на месте;
- прыжок или **E** — встать.

Пока вы на козлах, у зверя внизу экрана видны здоровье и выносливость, а рядом с именем — груз.
Бег тратит выносливость; с высоким навыком «Верховая езда» — до вдвое меньше, и навык растёт, пока зверь бежит.
Возница с волком не даёт ему драться. Испуганный олень вожжей не слушается.

## Приручение оленя

Олень убегает от всего, что заметит, поэтому с руки он не ест:

1. Положите на землю **Одуванчик** или **Морковь** (морковь приручает в 1,5 раза быстрее и дольше держит сытость).
2. Тихо отойдите. Спокойный голодный олень сам подойдёт к корму, если тот ближе 10 м.
3. Пока олень сыт и спокоен, приручение идёт: всего 15 минут спокойного сытого времени, с морковью — 10 (кабану нужно 30).
4. Каждый испуг откатывает приручение на 10% (не чаще раза в 30 с). Подкрадывайтесь — крадущегося олень замечает позже.

Ручной олень пасётся там, где его приручили, и убегает от врагов ближе 15 м. Консольная команда `tame` приручает и оленей.

**Оленята.** Ручные олени размножаются, как кабаны: сытый спокойный олень, рядом с которым (ближе 4 м) есть другой
сытый ручной олень, набирает симпатию раз в 30 с; на 3 — беременность, через минуту — оленёнок. В радиусе 10 м должно быть
меньше 5 оленей с оленятами. Оленёнок вдвое меньше оленя и через 50 минут вырастает во взрослого ручного оленя того же
уровня. Запрячь его нельзя, привязать к колу — можно. Выключается настройкой `Breeding` в разделе `2 - Deer taming`.

## Привязь и кормушка

Обе строятся молотом у верстака, вкладка «Мебель».

- **Привязь** (Древесина ×3, Кожаные обрывки ×2) — кол с железным кольцом. **E** на колу привязывает ближайшего
  ручного зверя (не дальше 6 м): он пасётся вокруг кола и не уходит дальше длины верёвки (3 м), а за хозяином больше
  не идёт. Ещё раз **E** — отвязать. На кол — один зверь; привязать можно любого ручного зверя игры (и быкоящера, и курицу),
  кроме запряжённого в повозку и того, на ком сидят. Сломали кол — зверь через несколько секунд свободен.
- **Кормушка** (Древесина ×10) — корыто с ящиком на 8 ячеек. Положите в неё еду (**E** — открыть): голодные ручные звери
  в радиусе 10 м сами подходят и съедают по одной своей еде — той же, которой их приручают (олень — одуванчики
  и морковь). Еда лежит в ящике, а не на земле, поэтому проходящий мимо игрок её не подберёт.
  Привязанный зверь идёт к кормушке, только если дотянется на верёвке, — ставьте кормушку рядом с колом.
  По умолчанию дикие звери из кормушек не едят; с `FeedWildAnimals = true` через кормушку можно и приручать.
- **Потомство.** Над ручным зверем, который размножается, видна строка «Потомство»: что мешает (голоден, напуган,
  нет готовой пары рядом, тесно — у кабана не больше 5 своих с детёнышами в радиусе 10 м) или «симпатия 2/3».
  Условия ванильные: раз в 30 с сытый спокойный зверь с готовой парой рядом набирает симпатию, на 3 — беременность,
  через минуту — детёныш. Кормушка их не меняет: звери из неё сыты так же, как от еды на земле.

## Настройки

Файл `BepInEx/config/draftanimals.cfg` (или F1 — Configuration Manager), описания — на английском и русском.
Правка файла во время игры применяется сразу. Разделы 1–8 синхронизируются с сервера, раздел 9 — у каждого свой.

| Раздел | Что в нём |
|---|---|
| 1 - General | радиус сцепки, дальность ведущего, скорость голодного, прибавка за звезду, вес возницы, отдых после изнеможения |
| 2 - Deer taming | время приручения, сытость, корм и множители, откат от испуга, дальность приманки, паники и бегства, размножение оленей (`Breeding`, `MaxDeerNearby`, `FawnGrowTime`) |
| 3 - Boar, 4 - Deer, 5 - Wolf | можно ли запрягать, грузоподъёмность, скорость при перегрузе, скорости, выносливость, точка сцепки (`HitchDistance`, `HitchHeight`), подгонка модели упряжи (`HarnessVisualOffset`, `HarnessVisualScale`) |
| 6 - Harness, 7 - Heavy harness | станок и рецепт упряжи |
| 8 - Tether post and feeder | длина верёвки, радиус привязывания, дальность кормушки, дикие у кормушки, станки и рецепты |
| 9 - Client | панель зверя у возницы (`ShowMountHud`), строка о потомстве (`ShowBreedingInfo`), поза возницы (`DriverSeatOffset`) |

## Сборка из исходников

Нужен .NET SDK 8. Путь к игре задан в `DraftAnimals.csproj` (`ValheimDir`, по умолчанию `~/ValheimWin` — ссылка
на папку игры на Windows-машине, открытую по SMB). Другой путь: `dotnet build -c Release -p:ValheimDir=/путь/к/Valheim`.

```
dotnet build -c Release
```

После сборки DLL сама копируется в `BepInEx/plugins/DraftAnimals.dll` (собрать без копирования:
`-p:SkipDeploy=true`). Если в этот момент игра запущена, Windows держит файл занятым — будет только предупреждение,
скопируйте после выхода из игры. Архив для Thunderstore — `./package.sh` (появится в `dist/`).

Тесты правил (груз, выносливость, вожжи, приручение, привязь, кормушка):

```
dotnet run --project tests/LogicTests
```

Иконка (`package/icon.png`, 256×256) и баннер для страницы мода (`media/banner.png`, 1300×372) рисуются кодом:
`python3 tests/icon.py package/icon.png` и `python3 tests/icon.py media/banner.png --banner`.

## Лицензия

[PolyForm Noncommercial License 1.0.0](LICENSE.md). Кратко (полный текст — в `LICENSE.md`):

- мод можно бесплатно использовать, изменять и распространять, в том числе переделки;
- продавать мод или использовать его в коммерческих целях нельзя;
- при любой передаче мода или его переделки нужно приложить текст лицензии (или ссылку на него) и сохранить строку:

```
Required Notice: Copyright Fionov Alexander (https://github.com/fionov-a-v/Valheim-DraftAnimals)
```

---

<a id="english"></a>
# Draft Animals — mod for Valheim

Hitch a tame boar, deer or wolf to the vanilla cart. The animal pulls it behind you, or a driver steers it from the
cart's bench — race your friends in co-op. Deer become tameable with food lures, and tame deer have fawns. For the farm
there is a tether post and a feeder the animals eat from on their own. Made for Valheim 1.0.17 with BepInEx 5 and
Jötunn 2.30. English and Russian in-game text.

**Install:** needs BepInExPack Valheim and Jötunn. With r2modman/Thunderstore Mod Manager install DraftAnimals and the
dependencies come along; by hand, put `DraftAnimals.dll` into `BepInEx/plugins`. Every player and the server must have
the mod; game settings are synced from the server and only admins can change them.

**Animals:** boar — steady, pulls 900 at full speed, walks 3 / runs 5.5 m/s, doesn't fight in harness; deer — fastest
(3.5 / 8.5 m/s) but pulls 450 and stops at double load, balks while an enemy is within 10 m and bolts for 3 s when hit;
wolf — pulls 1200, 3.5 / 7.5 m/s, fights while not driven, dragging the cart. Load is the cart contents plus the driver
(80 + inventory); an ore stack weighs about 300. Each star adds 25% capacity; a hungry animal walks at half speed.
The cart still can't go through portals.

**Harnessing:** craft a **Harness** at the Workbench (Leather Scraps ×10, Deer Hide ×4, Bronze Nails ×5) for a boar or
deer, or a **Heavy harness** at the Forge (Wolf Pelt ×4, Leather Scraps ×10, Silver ×2) for a wolf. Use it from the
hotbar while looking at a tame animal — it gets a wooden shaft bow with a bell and a girth. A harnessed animal obeys
**E** (follow / stay); **Shift+E** takes the harness off; food from the hotbar feeds it. At a cart's shafts,
**Shift+E** hitches the nearest harnessed animal; press it again to unhitch. A hitched animal follows its leader within 40 m.

**Driving:** sit on the **driver's bench** at the front of the cart (E) and drive like riding a lox: **W** walk toward
where you look (it keeps going), **Shift** run, **S** stop, **RMB** turn in place, jump to get off. The mount panel shows
the animal's health and stamina, and the load next to its name. Running costs stamina; a high Riding skill halves it,
and the skill grows while the animal runs.

**Deer taming:** drop Dandelions or Carrots on the ground and step away quietly; a calm hungry deer comes to food within
10 m. Taming takes 15 minutes of calm, fed time (10 with carrots). Every scare rolls taming back by 10% (once per 30 s).
The `tame` console command tames deer too. Tame deer breed like boars (a fed partner within 4 m, fewer than 5 deer and
fawns within 10 m) and have fawns that grow into tame deer in 50 minutes.

**Tether post and feeder** (Hammer → Furniture, near a Workbench): the **tether post** (Wood ×3, Leather Scraps ×2) ties
the nearest tame animal of any kind with E — it grazes within 3 m of the post; E again unties. The **feeder** (Wood ×10)
is a trough with 8 slots: hungry tame animals within 10 m come and eat their usual food from it, and nobody picks it up
by walking past. Over a breeding animal a "Young" line shows what stops it (hungry, no partner close enough, too crowded)
or how close it is.

**Config** (`BepInEx/config/draftanimals.cfg`, or F1 Configuration Manager): sections 1–8 (hitching, deer taming and
breeding, each animal's capacity, speeds, stamina and harness fit, recipes, tether and feeder) are synced from the
server; section 9 (mount panel, breeding line, driver pose) is per client. All descriptions are in English and Russian.

**Source:** https://github.com/fionov-a-v/Valheim-DraftAnimals

**License:** [PolyForm Noncommercial License 1.0.0](LICENSE.md) — free to use, modify and share, no commercial use;
keep the line `Required Notice: Copyright Fionov Alexander (https://github.com/fionov-a-v/Valheim-DraftAnimals)` when redistributing.

## Changelog

- **1.0.0** — first release: harnessing boars, deer and wolves to the cart, the driver's bench, deer taming and
  breeding, the tether post and the feeder.
