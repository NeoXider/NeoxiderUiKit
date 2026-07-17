# Neoxider UI Kit

Универсальный UI Kit поверх вывода импортёра «MTK Figma UI Import» (`.fui` → UI Toolkit, Unity 6000.5+, runtime через `PanelRenderer`).

Дизайнер импортирует `.fui`-файл → вы нажимаете две кнопки в окне **Neoxider → UI Kit** → получаете страницы с роутером, попапы, типизированные вьюшки, анимации и звук кликов. Подключение к игре — это подписки и вызовы `Set*`, без развески компонентов по кнопкам.

- Namespace: `Neo.UIKit` (фасад — статический класс `UiKit`).
- Пакет ни от чего не зависит, кроме модулей `uielements` и `audio`; от `com.mtk.fui-import` зависимости нет (сканер читает UXML как файлы).
- Сгенерированный код и конфиг живут в проекте игры (`Assets/UiKit/`), пакет неизменяем.

Подробная документация — в `Docs~/`:

| Файл | О чём |
|---|---|
| [Docs~/quickstart.md](Docs~/quickstart.md) | Пошаговый сценарий с нуля + подключение к игре |
| [Docs~/architecture.md](Docs~/architecture.md) | Слои, жизненный цикл страницы, роутер, счётчики, анимации, звук |
| [Docs~/naming-conventions.md](Docs~/naming-conventions.md) | Контракт дизайнера Figma: правила имён и что деградирует без них |
| [Docs~/adapters.md](Docs~/adapters.md) | Интеграция с игрой: NeoxiderTools, свой адаптер, аудио-режимы |

## Установка

Package Manager → **Add package from git URL**:

```
https://github.com/NeoXider/NeoxiderUiKit.git
```

Рекомендуется пиновать тег — обновление по git URL происходит только вручную, а тег гарантирует воспроизводимую версию:

```
https://github.com/NeoXider/NeoxiderUiKit.git#v0.1.0
```

Чтобы тесты пакета были видны в Test Runner проекта, добавьте в `Packages/manifest.json`:

```json
"testables": ["com.neoxider.uikit"]
```

## Quickstart — «пара кнопок»

1. Импортируйте `.fui`-файл штатным импортёром (появится `Assets/<PROJECT>/{UXML,USS,...}`).
2. Откройте окно **Neoxider → UI Kit**. Окно найдёт (или создаст) `Assets/UiKit/UiKitConfig.asset` и просканирует UXML.
3. Нажмите **«Сгенерировать вьюшки»** — сканер строит модель по классам `fui_type_*`, генератор пишет:
   - `Assets/UiKit/Generated/<Page>View.g.cs` — типизированная вьюшка страницы (перегенерируется);
   - `Assets/UiKit/Views/<Page>View.cs` — ваш partial (создаётся один раз, никогда не перезаписывается);
   - `Assets/UiKit/Generated/UiIds.g.cs` — константы путей;
   - `Assets/UiKit/UiKitApi.md` — живая шпаргалка по всем полям/путям интерфейса.
4. Нажмите **«Создать объекты на сцене»** — появится корень `UI` с единственным `UiKitBootstrap` и по GameObject на страницу (`PanelRenderer` + сгенерированный компонент). Нестартовые страницы выключены; на стартовую страницу автоматически добавляется `UiFakeLoading` — фейковая загрузка, которая заполняет прогресс-бар 1–3 секунды «рваными» шагами и показывает `mainmenu`. Дополнительно создаётся `UI_Background` (`UiBackgroundLayer`) — постоянный фон: Canvas в режиме Screen Space – Camera с `sortingOrder -1000`, спрайт берётся автоматически из фоновой картинки экрана дизайна; страницы прозрачные, поэтому фон виден и в меню, и в игре, а игровой мир рисуется поверх него.
5. Нажмите Play — интерфейс уже работает: загрузка → главное меню, кнопки с анимацией нажатия и звуком клика (если задан `clickSound` в конфиге), типовая навигация (`button_setting` → попап настроек, `button_close` → закрыть, `button_restart` → пере-показ своей страницы с закрытием попапов) развешана декларативно, без единой строки кода.

Пресет анимации нажатия кнопок выбирается в конфиге (`buttonPressPreset`): `scale` (по умолчанию), `sink` (кнопка «проседает»), `pop` (увеличивается), `none` — все сделаны на USS-переходах, свои добавляются в проектный override-USS.

## Обзор API

```csharp
using Neo.UIKit;
using Game.Ui; // namespace сгенерированного кода (настраивается в окне)

// Страницы: реестр + стек
UiKit.Pages.Show(UiIds.Gameplay.Id);   // переключить (скрывает текущий стек)
UiKit.Pages.Push("mainmenu");          // поверх, с историей
UiKit.Pages.Pop();                     // снять верхнюю
UiKit.Pages.Back();                    // Escape: попап → Pop → событие BackOnRoot
UiKit.Pages.PageShown += id => Debug.Log($"shown {id}");

// Попапы: путь "pageId/popup_name"
UiKit.Popups.Open(UiIds.Gameplay.PopupPause.Path);
string result = await UiKit.Popups.OpenAsync("gameplay/popup_endgame"); // "restart", "main_menu", ...

// Счётчики: глобальная модель, броадкаст во все вьюшки на всех страницах
UiKit.Money.Set(1500);                 // алиас счётчика (по умолчанию "coin")
UiKit.Counters["merges"].Add(1);

// Клики по пути: переживают reload, можно подписаться до создания страниц
UiKit.OnClick(UiIds.Mainmenu.ButtonPlay, () => StartGame());
UiKit.AnyButtonClicked += name => Debug.Log($"click {name}");

// Типизированный доступ к сгенерированной вьюшке
var menu = UiKit.Get<MainmenuView>();
menu.LabelButtonPlay0001.SetText("PLAY");   // gradient-обёртка текста сохраняется
menu.PanelUpgrade.SetPrice("25 000");
menu.PopupSetting.Open();

// Звук: три режима (mixer / адаптер / события) — см. Docs~/adapters.md
UiKit.Audio.SoundChanged += on => Debug.Log($"sound {on}");
UiKit.Audio.MusicOn = false;

// Интеграция с игрой: один вызов подключает все реализованные интерфейсы
UiKit.Flow.Connect(myAdapter); // IUiFlowSource / IUiCounterSource / IUiClickSound / IUiAudioSettings
```

Три уровня доступа работают одновременно:

1. **Фасад** `UiKit.Pages / Popups / Counters / Money / OnClick / Audio / Flow` — для game-flow кода.
2. **Типизированные вьюшки**: у страницы есть поле под **каждый** именованный элемент (`ButtonView`, `LabelView`, `ImageView`, `CounterView`, `TimerView`, `BarView`, `ToggleView`, `ShopItemView`, попапы — вложенные вьюшки). Переименование в дизайне → compile error, а не тихий отказ.
3. **Сырой доступ**: `view.Root.Q<VisualElement>(...)` — root каждой вьюшки публичен.

## Конвенции имён Figma (контракт дизайнера, кратко)

Сканер строится на классах `fui_type_*`, которые ставит импортёр; имена (`name`) — идентификаторы. Полные правила — [Docs~/naming-conventions.md](Docs~/naming-conventions.md).

| Правило | Что даёт |
|---|---|
| Корень экрана: `fui_type_screen`, name = `loading` / `mainmenu` / `gameplay`... | pageId, страница в роутере |
| Попап: `fui_type_popup`, name `popup_*`, внутри полноэкранный `background` | `PopupView`, dim-подложка, скрытие до первого кадра |
| Кнопки: `fui_type_button`, name `button_*` | `ButtonView` + декларативная навигация по имени (`button_close`, `button_play`, `button_<popup>`) |
| Панель-счётчик: `panel_*` с Label-числом в `<gradient>`-обёртке | `CounterView`; id = имя без `panel_` (в id со `score`/`level` — `ScoreView`/`LevelView`) |
| `panel_timer` | `TimerView` |
| Прогресс-бар: `fui_type_progressbar` | `BarView` (пиксельная ширина fill от фактической ширины трека) |
| Переключатели: name `toggle_*` (рекомендация: `toggle_sound`, `toggle_music`) | `ToggleView` (класс `is-off`, событие `Changed`) |
| Числа — в rich-text-обёртке `<color=...><gradient=...>512</gradient></color>` | `SetText`/счётчики подменяют только число, сохраняя градиент |

Без конвенций генератор деградирует мягко: экраны и кнопки останутся, но счётчики/таймеры/попапы придётся размечать вручную (override типа виджета в окне).

## Переимпорт дизайна

Дизайн изменился (страниц может стать больше или меньше):

1. Импортируйте новый `.fui` поверх.
2. Снова нажмите **«Сгенерировать вьюшки»**. Генератор сравнит модель с прошлой (снапшот `UiKitModel.g.txt`) и покажет **diff-отчёт**: добавленные / удалённые / изменённые элементы; сломанные пути конфига (счётчики, навигация) попадут в отчёт.
3. `.g.cs`-файлы и конфиг обновятся, **ваши partial-файлы в `Assets/UiKit/Views/` не трогаются никогда**. Ручные правки конфига (override'ы сканера, навигация, id счётчиков) переживают переимпорт.
4. Снова **«Создать объекты на сцене»** при необходимости — сборка идемпотентна (ключ — pageId в компоненте, не имя GameObject): существующим объектам обновляются только ссылки uxml/PanelSettings, transform и добавленные компоненты не трогаются, исчезнувшие страницы не удаляются, а попадают в отчёт.

Переименованная кнопка → изменившаяся константа `UiIds` → ошибка компиляции в вашем коде — ничего не умирает молча.

## Миграция с NeoxiderPages

Старая Canvas-система `Neo.Pages` и новый kit не конфликтуют (разные namespace: `Neo.Pages` против `Neo.UIKit`). Соответствие понятий:

| NeoxiderPages (Canvas) | Новый kit (UI Toolkit) | Что лучше |
|---|---|---|
| `PM` (singleton, exclusive/popup, startup page) | `PageRouter` + `UiKitBootstrap` | стек Push/Pop с историей, Back/Escape, события Shown/Hidden, без singleton-гонок |
| `UIPage` + DOTween (`ForwardOnly/BackwardOnly/...`) | `UiPageBase`/`PopupView` + USS-пресеты + `IUiAnimator` | анимации без DOTween-зависимости, режим анимации сохранён (`UiAnimationMode`), таймаут-гарантия завершения |
| `PageId` (ScriptableObject на страницу) | pageId-строки + сгенерированные константы `UiIds` | не нужно руками создавать ассеты; compile error при переименовании |
| `BtnChangePage` (вешать на каждую кнопку) | декларативный маппинг «кнопка → действие» в конфиге | ноль компонентов, генератор предлагает маппинг сам по именам |
| `G`/GM-интеграция (Win/Lose → страницы, хардкод на GM) | `UiKit.Flow` + `IUiFlowSource` | подключаемый адаптер: NeoxiderTools — один сгенерированный адаптер, чужие системы — свой; ядро ни от чего не зависит |
| `Wallet`/`Score` (хардкод на Money/ScoreManager) | `CounterRegistry` + `IUiCounterSource` | те же счётчики для любых систем, мультиэкранный броадкаст |
| `Audio.PlayUI()` | `UiKit.AnyButtonClicked` + `IUiClickSound` | не привязано к AM |

Для проектов на NeoxiderTools в окне есть кнопка **«Создать адаптер Neoxider»** — генерирует готовый файл-мост (см. [Docs~/adapters.md](Docs~/adapters.md)).

## Лицензия

MIT.
