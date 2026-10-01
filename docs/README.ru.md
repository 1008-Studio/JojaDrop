🇺🇸 [English](README.md) | 🇷🇺 Русский

# JojaDrop

Обновляйте выбранную пачку предметов до одного целевого предмета с вероятностью `q * sourcePoints / targetPoints`.

Версия v0.3 поддерживает выбор исходного и целевого предметов, расчёт вероятности и безопасное выполнение улучшения. Успешная попытка расходует один исходный предмет и создаёт один целевой; неудачная попытка также расходует один исходный предмет.

## Требования

- Stardew Valley **1.6.14+** на Windows, Linux, macOS или Steam Deck.
- [SMAPI **4.5.2+**](https://smapi.io/).
- Для разработки: **.NET 8 SDK (8.0.200+)** или новее, способный собирать `net6.0`, а также локальная установка игры с SMAPI. Новый SDK нужен для актуального C# analyzer build-пакета; сам мод по-прежнему использует .NET 6 runtime игры.

Проект следует [руководству SMAPI по настройке модов](https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started): это SDK-style C# class library для `net6.0`. [Pathoschild.Stardew.ModBuildConfig 4.4.0](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig/4.4.0) предоставляет ссылки на игру и SMAPI, deployment и сборку релизного пакета. Требования и версия пакета проверены 2026-09-29.

## Разработка

Из корня репозитория:

```sh
dotnet build JojaDrop.csproj -c Release
```

Build-пакет находит стандартные установки игры, копирует мод в `Mods/JojaDrop` и создаёт release ZIP в `bin/Release`. Скомпилированная сборка находится в `bin/Release/net6.0`.

Для нестандартной установки игры передайте каталог с исполняемым файлом Stardew Valley и установленным SMAPI:

```sh
dotnet build JojaDrop.csproj -c Release -p:GamePath="/absolute/path/to/Stardew Valley"
```

В Windows замените путь на каталог Windows-версии игры. Не сохраняйте локальные пути в project file. Также можно указать `GamePath` в файле build-пакета `stardewvalley.targets` в домашнем каталоге, как описано в его документации.

Чтобы собрать ZIP без автоматической установки мода:

```sh
dotnet build JojaDrop.csproj -c Release -p:GamePath="/absolute/path/to/Stardew Valley" -p:EnableModDeploy=false
```

Запуск .NET 8 calculator checks не зависит от игры, SMAPI и сторонних test packages:

```sh
dotnet run --project Tests/JojaDrop.CalculatorChecks.csproj -c Release
```

Runner проверяет корректные, ошибочные, рекурсивные, cache- и арифметические граничные случаи. При успехе он завершится с кодом 0, а при провале проверки выбросит исключение.

Проверка 2026-09-29: Release был собран SDK 8.0.408 с фактическими assembly Stardew Valley 1.6.15 и SMAPI 4.5.2, без ошибок и предупреждений, с включённым SMAPI analyzer. Все 15 calculator checks прошли. Также были проверены согласованность manifest, содержимое release ZIP, пробелы, ignore rules и геометрия layout при пяти размерах UI от 1920×1080 до 640×360. Игра здесь не запускалась; визуальное поведение, звуки и controller input всё ещё требуют ручной проверки в игре ниже.

### Структура проекта

```text
JojaDrop.csproj
manifest.json
ModEntry.cs
Integration/
    InventoryIntegration.cs
UI/
    UpgradeButton.cs
    UpgradeMenu.cs
    SourceItemMenu.cs
    TargetItemMenu.cs
    MenuDrawing.cs
Services/
    ItemValueService.cs
    UpgradeCalculator.cs
    UpgradeRoller.cs
    UpgradeTransactionService.cs
    UpgradeTransactionResult.cs
Tests/
    JojaDrop.CalculatorChecks.csproj
    Program.cs
README.md
.gitignore
```

`ModEntry` подключает мод. `InventoryIntegration` отвечает за SMAPI rendering и input events. Классы UI рисуют кнопку, меню улучшения, выбор исходного и целевого предметов, используя текстуры игры и drawing helpers. Правила points, поиска целей и вероятностей находятся в `Services`.

### Правила стоимости и вероятности

`ItemValueService.GetValue(Item)` возвращает points JojaDrop **за один предмет**, а не за весь stack. Поддерживаются обычные shipppable `StardewValley.Object`; recipes, quest items, big craftables, non-shippable objects и specialized subclasses возвращают `null`.

Points состоят из двух явных уровней. `U(x)` — уже существующий результат intrinsic acquisition-points, полученный из resolved Stardew data. `P(x)` — стоимость, используемая JojaDrop:

```text
P(x) = max(U(x), min(recipe cost))
recipe cost = 0.90 * sum(quantity × P(ingredient)) / output quantity
```

`P` рекурсивно использует final points ингредиентов, вычисляется в `double`, округляется только при получении целого числа points и выбирает самый дешёвый valid deterministic recipe. Связи crafting, cooking и machine читаются из resolved `Data/CraftingRecipes`, `Data/CookingRecipes` и `Data/Machines`, поэтому обычные data-driven mod entries и string IDs подхватываются автоматически. Category ingredients, random/custom outputs, malformed rules, missing inputs и cyclic branches консервативно пропускаются либо используют `U(x)`; menu из-за них не падает. Final points cache хранится для одного resolved-data snapshot и сбрасывается при изменении соответствующих content assets.

`TargetEconomics` изолирован от Stardew и применяет правило одного output: кандидат должен отличаться от source и иметь `T > q * S`; сравнение выполняется через `long`. Для eligible target вероятность строго равна `p = q * S / T`, а multiplier — `T / (q * S)`. Режим **All** в target picker показывает все и только eligible upgrades; x2/x3/x5/x10 — отфильтрованные подмножества по той же batch chance. Выбор target никогда не меняет `q`; увеличение `q` может лишь очистить target, который перестал быть eligible. Random roll здесь не выполняется.

## Установка

1. Установите SMAPI в Stardew Valley.
2. Соберите проект. По умолчанию build-пакет автоматически установит мод.
3. Или распакуйте generated release ZIP в каталог `Mods` игры, чтобы мод находился по путям `Mods/JojaDrop/manifest.json` и `Mods/JojaDrop/JojaDrop.dll`.
4. Запустите игру через SMAPI. В консоли должно появиться `[JojaDrop] JojaDrop loaded successfully.`

Устанавливайте только файлы packaged мода, а не репозиторий или DLL игры/SMAPI. Другие моды не требуются.

### Как попробовать прототип

1. Загрузите сохранение, откройте стандартное меню игрока (`E` по умолчанию) и выберите вкладку **Inventory**.
2. Нажмите кнопку со стрелкой вверх справа. В hover tooltip указано **JojaDrop Upgrader**. Также можно нажать **U** или **правый стик** контроллера на вкладке Inventory.
3. Откроется панель JojaDrop с Your Item, Target Item, вероятностью/multiplier `--` и неактивной кнопкой Upgrade. Target Item недоступен, пока не выбран source.
4. Нажмите **Your Item** и выберите поддерживаемый предмет в инвентаре. В source slot появятся его icon, display name и points за предмет. Исходный предмет и его stack останутся в рюкзаке.
5. Задайте source quantity, затем нажмите **Target Item** и выберите eligible candidate. Панель покажет icon, name, value, batch probability и batch multiplier.
6. Нажмите **UPGRADE**, чтобы сделать одну попытку. При успехе расходуется выбранная source batch и создаётся один target item; при неудаче batch расходуется, но предмет не создаётся. После завершённой попытки выбор очищается.
7. Закройте любой picker крестиком, Esc или кнопкой B контроллера, чтобы отменить его и вернуться в JojaDrop. Таким же способом закройте JojaDrop для возврата в игру.

Направленная controller navigation использует clickable components в обоих custom menus; A активирует сфокусированный компонент. Picker также поддерживает shoulder buttons, колёсико мыши и кнопки Previous/Next для пагинации.

Кнопка inventory рисуется через `RenderedActiveMenu` и активируется через `ButtonPressed`; consumed input подавляется. Vanilla inventory components и их navigation не изменяются. Открытие блокируется, если inventory нельзя безопасно закрыть, например когда предмет удерживается курсором. Layout использует UI-scaled viewport coordinates и пересчитывается при изменении меню или viewport. На узких экранах кнопка перемещается под inventory; если свободного места снаружи нет, используйте U/правый стик. JojaDrop центрируется при изменении размера, а picker адаптирует grid и включает пагинацию. На очень маленьких UI viewport может потребоваться уменьшить UI scale игры.

### Ручная проверка

- Убедитесь, что появляется сообщение о загрузке и в консоли SMAPI нет ошибок JojaDrop.
- Убедитесь, что кнопка показывается только на Inventory и vanilla tabs, equipment, trash can и item drag/drop продолжают работать.
- Проверьте hover кнопки, tooltip и звук открытия; попробуйте открыть меню, удерживая предмет inventory.
- Выберите предмет, проверьте его name и points за предмет и убедитесь, что stack не меняется после закрытия/повторного открытия menu.
- Убедитесь, что без source нельзя выбрать target, предлагаются только items строго дороже `q * S`, source item исключён, а candidates отсортированы по value.
- Убедитесь, что выбор source/target показывает ожидаемые batch probability и multiplier; увеличение q выше eligibility target очищает этот target, не меняя q.
- Убедитесь, что успешный upgrade удаляет ровно q source items и добавляет один target item, а failure удаляет ровно q source items и ничего не добавляет.
- При полном inventory убедитесь, что success target, который не может stack, оставляет source без изменений и сообщает статус inventory-full.
- Дважды нажмите Upgrade и убедитесь, что выполняется только одна попытка, пока source или target не будут выбраны заново.
- Проверьте отмену picker, крестик закрытия, Esc и controller navigation/A/B.
- Проверьте изменение размера окна и разные UI scales, включая узкий viewport и paginated inventory.
