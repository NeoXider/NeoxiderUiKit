# Архитектура Neoxider UI Kit

## Слои

```
Runtime/
  Core/       UiKit (фасад), UiKitConfig, PageRouter, PopupService, CounterRegistry,
              UiAudio, UiClickRegistry, UiElementResolver, UiActions, UiKitBootstrap
  Views/      UiPageBase (MonoBehaviour), UiSubView (чистый C#), PopupView
  Widgets/    ButtonView, LabelView, ImageView, CounterView (+ ScoreView, LevelView),
              TimerView, BarView, ToggleView, ShopItemView, UiRichText, UiFakeLoading
  Animation/  IUiAnimator, UiAnimations (реестр пресетов)
  Flow/       UiFlow + интерфейсы IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings
  Styles/     uikit.uss (USS-пресеты анимаций, dim, pressed, пульс счётчика)
Editor/
  FuiUxmlScanner (UXML → модель), ViewCodeGenerator + UiIdsGenerator + ApiDocGenerator,
  UiKitGenerator (оркестратор генерации + diff), UiKitConfigUpdater, UiNavigationProposer,
  SceneObjectBuilder, NeoxiderAdapterGenerator, UiKitGeneratorWindow (Neoxider → UI Kit)
```

Принцип разделения:

- **Runtime core** ничего не знает ни про Figma, ни про генератор, ни про чьи-либо игровые системы. Все связи с игрой — через узкие интерфейсы `Flow/`.
- **Widgets** — чистые C#-обёртки (`UiSubView`) над ветками визуального дерева; каждый кэширует последнее значение и повторно применяет его при каждом bind, поэтому неактивные страницы «догоняют» состояние при следующем показе.
- **Генератор** — только Editor, запускается **только по кнопкам** (никакой автогенерации на импорте). Сгенерированный код и конфиг живут в проекте игры, пакет неизменяем.
- Два базовых типа вьюшек: страница — всегда `UiPageBase : MonoBehaviour` (владеет `PanelRenderer`, попадает в роутер и на сцену); всё внутри чужого дерева (попапы, виджеты, секции) — `UiSubView` (чистый C#, `Bind(VisualElement)` / `Unwire()`, запросы скоупятся к своему `Root`).

## Жизненный цикл страницы (`UiPageBase`)

```
Awake        → UiKit.Pages.Register(this)
OnEnable     → PanelRenderer.RegisterUIReloadCallback(OnUIReload)
                + StartCoroutine(EnsureBoundNextFrame())
OnUIReload   → Unwire() → BindInternal(root)
OnDisable    → UnregisterUIReloadCallback + Unwire(); Root/ScreenRoot = null
```

`BindInternal` выполняется до первого кадра нового дерева и в строгом порядке:

1. `ScreenRoot` = элемент с классом `fui_type_screen`;
2. подключение `uikit.uss` + проектного override-USS (из конфига);
3. класс `uikit-page` + класс пресета анимации (`uikit-anim-*`); если страница уже показана — сразу `is-open`;
4. intro-контейнер top-бара (`topBarPath` из конфига → класс `uikit-topbar`);
5. **скрытие всех попапов** (`fui_type_popup` → `display:none`), чтобы полноэкранный `background` попапа не перехватывал ввод;
6. автосоздание `ButtonView` на каждой `Button` (троттлинг 0.15 c, press-пульс, звук клика);
7. `BindUi(root)` — сгенерированный код привязывает типизированные поля;
8. применение реестра `UiKit.OnClick` (нерезолвящийся путь — **error в консоль**, переименования не умирают молча), привязка счётчиков из конфига, декларативная навигация кнопок;
9. `OnBind()` (ваш partial) и событие `Bound`.

Все `Q<>` и подписки — только внутри bind-прохода; `Unwire()` идемпотентен и вызывается на каждом reload и disable. Повторный `Show` после `SetActive(false)` — штатный полный rebind.

**Нюанс re-enable:** `PanelRenderer` вызывает reload-колбэк только когда пересоздаёт визуальное дерево; при повторном включении объекта прежнее дерево может выжить, и колбэк не приходит. Поэтому `EnsureBoundNextFrame()` ждёт кадр и, если bind не случился, повторно биндит **закэшированный root последнего reload'а** (если тот всё ещё прикреплён к панели), иначе передёргивает `panelRenderer.enabled`, форсируя свежий reload.

Show/Hide:

- `ShowInternal` — включает GameObject (bind подхватит отложенный показ через `_pendingShow`) → `PlayShowAnimation` → `OnShow()`;
- `HideInternal` — `PlayHideAnimation` → `OnHide()` → `SetActive(false)` (снятие с рендера и hit-test). Завершение скрытия гарантируется `TransitionEndEvent` **плюс** schedule-таймаут (duration + 0.1 c).

Точки для пользователя (partial-класс): `OnBind` / `OnShow` / `OnHide`, `UnwireUi`, событие `Bound`, а также override `PlayShowAnimation` / `PlayHideAnimation`.

## PageRouter: реестр + стек

- `Show(id)` — скрывает весь текущий стек (анимированно только верхнюю страницу) и показывает целевую.
- `Push(id)` / `Pop()` — страница поверх; sorting order панели = `sortingOrderBase` страницы из конфига + позиция в стеке. Корень стека никогда не Pop'ается.
- `Back()` — сначала закрывает верхний открытый попап, затем `Pop()`, на корне поднимает `BackOnRoot` («выйти из игры?»). `UiKitBootstrap` по умолчанию дёргает `Back()` на Escape (legacy input).
- События `PageShown` / `PageHidden` со строковым pageId.
- `UiKitBootstrap` — единственный на корне `UI`: `Awake` (порядок −100) → `UiKit.Initialize(config)`, регистрация всех страниц (включая неактивные), показ стартовой страницы до первого кадра, выделенный 2D `AudioSource` для звука кликов.
- Статика фасада сбрасывается через `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` — kit работает при выключенном domain reload.

## CounterRegistry: счётчик — модель, не элемент

Значение хранится по id в `Counter` (`Set(long)` / `Add(long)` / событие `Changed`). Конфиг маппит `id → список путей` (например, `coin` отображается и в `mainmenu/panel_coin`, и в `gameplay/panel_coin`). При bind страница создаёт `CounterView` на каждый свой путь; вьюшка подтягивает текущее значение и подписывается на `Changed`. `Set` броадкастит во все живые вьюшки; неактивные страницы получают значение при следующем bind. Неизвестный id — понятная ошибка с перечислением известных id (и определение на лету, чтобы код не падал). `UiKit.Money` — алиас счётчика с флагом `moneyAlias` в конфиге (по умолчанию `coin`).

## Пути "pageId/popup/element"

Адресация повсюду (OnClick, счётчики, навигация, попапы) — путями `pageId/element` или `pageId/popup_x/element`. Имена элементов в импортированном UXML **дублируются между попапами** (`button_restart`, `panel_header`...), поэтому глобальный `Q<>()` недопустим: `UiElementResolver` резолвит путь по сегментам, и каждый сегмент обязан быть однозначным в своём скоупе — неоднозначность даёт ошибку со списком кандидатов. Первичный API — сгенерированные константы `UiIds` (`UiIds.Gameplay.PopupPause.ButtonRestart`), сырые строки — fallback.

## Анимации

Три уровня, от простого к гибкому:

1. **USS-пресеты в `uikit.uss`** (класс-driven, только `opacity/translate/scale`, специфичность ≥ 2 классов — правила kit'а выигрывают у импортёра):
   - страницы: `fade`, `slide-up`, `slide-down`, `slide-left`, `slide-right`, `scale`, `none`;
   - попапы: `scale-pop`, `fade`, `slide-up`, `none` + dim-подложка (`background` → `uikit-dim`) + intro заголовка `panel_header`;
   - intro top-бара страницы (`uikit-topbar`, путь per-страница в конфиге);
   - кнопки: press-пульс `uikit-pressed`; счётчик: пульс `uikit-counter-pulse` при изменении значения.
   Механика: `UssTransitionAnimator` пропускает кадр (`schedule`) → добавляет `is-open` → ждёт `TransitionEndEvent` с таймаут-гарантией; на hide добавляется `is-hiding`.
2. **Проектный override-USS** (`projectOverrideStyleSheet` в конфиге) — правка таймингов/кривых/дистанций без кода и без форка пакета; подключается после `uikit.uss`.
3. **Кодовые точки расширения**:
   - `IUiAnimator { void Show(VisualElement, Action onDone); void Hide(VisualElement, Action onDone); }` + `UiAnimations.Register("my-preset", animator)` — свой пресет появляется в выпадашках окна рядом со встроенными;
   - виртуальные `PlayShowAnimation` / `PlayHideAnimation` в `UiPageBase` и `PopupView` — точечное переопределение в partial-классе;
   - `ButtonView.SetPressAnimation(IUiAnimator)` — замена press-пульса per-кнопка.

Per-страница в конфиге: `showPreset`, необязательный `hidePreset` (пусто = тот же, что show), `popupPreset` и **режим** `UiAnimationMode`: `ForwardAndBackward` / `ForwardOnly` (только показ) / `BackwardOnly` (только скрытие) / `None`. `None` и пресет `none` применяют конечное состояние мгновенно (`InstantUiAnimator`).

**Рекомендуемый дефолт проекта:** страницы `loading` и `mainmenu` — **без страничных анимаций** (пресет `none` / `UiAnimationMode.None`). Экран загрузки — первое, что видит игрок, и появляться он должен мгновенно; переход loading → mainmenu тоже чище без двойного fade. Анимации оставляйте на `gameplay` и попапах.

## Звук — три способа, работают вместе

1. **AudioMixer из коробки**: в конфиге `audioMixer` + имена exposed-параметров (`musicParam`/`soundParam` раздельно и/или `masterParam`). Переключатели сами мьютят группы (−80 dB); состояние `SoundOn/MusicOn` персистится в PlayerPrefs. Ни одной строки кода.
2. **Адаптер `IUiAudioSettings`** — своя аудиосистема становится источником истины (персистентность на её стороне, PlayerPrefs kit'а обходится). Звук клика — через `IUiClickSound`.
3. **События**: `UiKit.Audio.SoundChanged` / `MusicChanged` + свойства `SoundOn` / `MusicOn` — подписка своей логики напрямую, независимо от mixer/адаптера.

Клик кнопки: `ButtonView` → `UiAudio.PlayClick()` (сначала адаптер `IUiClickSound`, иначе `clickSound`-AudioClip конфига через 2D `AudioSource` бутстрапа) + глобальный хук `UiKit.AnyButtonClicked(name)`. Отключается per-кнопка (`PlayClickSound = false`).

## Flow-адаптеры

`UiKit.Flow.Connect(adapter)` — один вызов, подключаются **все** интерфейсы, которые объект реализует:

| Интерфейс | Направление | Что делает |
|---|---|---|
| `IUiFlowSource` | игра → UI и UI → игра | события `Win/Lose/Pause/Resume/Menu/GameStart/GameEnd` декларативно маппятся на действия страниц (конфиг `flow`: момент → `Show/Push/Pop/OpenPopup/ClosePopup` + targetId); kit вызывает `SetPaused(bool)` при открытии/закрытии попапа паузы |
| `IUiCounterSource` | игра → UI | событие `CounterChanged(string id, long value)` → `CounterRegistry` обновляется сам |
| `IUiClickSound` | UI → игра | `PlayClick()` вместо встроенного AudioClip |
| `IUiAudioSettings` | двунаправленно | `SoundOn`/`MusicOn` — источник истины для переключателей настроек |

Пауза: попап паузы (явный `pausePopupPath` в конфиге, иначе любой попап с «pause» в пути) при открытии/закрытии дёргает `SetPaused(true/false)` адаптера; без адаптера и при `manageTimeScale = true` kit сам ставит `Time.timeScale` 0/1. Анимации kit'а идут по realtime и от timeScale не зависят.

Готовый адаптер для NeoxiderTools генерируется кнопкой окна — см. `adapters.md`.

## UiFakeLoading

`Neo.UIKit.UiFakeLoading` — драйвер фейковой загрузки, `MonoBehaviour` рядом с компонентом страницы (`[RequireComponent(typeof(UiPageBase))]`). `SceneObjectBuilder` добавляет его на **стартовую** страницу автоматически.

- Общая длительность — случайная в `minSeconds..maxSeconds` (по умолчанию 1–3 c).
- Прогресс растёт «рваными» случайными шагами (`minStep..maxStep`, паузы 0.08–0.35 c по realtime), как настоящая загрузка, и никогда не обгоняет `elapsed/duration`.
- Бар — по `progressBarPath` (путь относительно страницы), иначе первый элемент с классом `fui_type_progressbar`; отрисовка через `BarView` (пиксельная ширина fill).
- По завершении — событие `Completed` и `UiKit.Pages.Show(nextPageId)`; пустой `nextPageId` = `mainmenu`, если такая страница есть в конфиге, иначе первая другая страница.
- Не нужен — просто удалите компонент со страницы (сборщик сцены не возвращает его существующим объектам) или подпишитесь на `Completed`/задайте `nextPageId`, чтобы встроить в свой флоу; для реальной загрузки замените на свой драйвер с `BarView.SetProgress(0..1)`.

## Генератор и окно (Editor)

- `FuiUxmlScanner` — System.Xml по классам `fui_type_*` (см. `naming-conventions.md`); результат — модель «страницы → попапы → элементы» со скоупленными путями.
- `ViewCodeGenerator` — на страницу: `<Page>View.g.cs` (partial, полное покрытие всех именованных элементов, XML-doc на каждом члене, попапы — вложенные `UiSubView`-классы) + user-partial `<Page>View.cs` (создаётся один раз). Плюс `UiIds.g.cs` и живая шпаргалка `UiKitApi.md`. Вывод детерминированный — чистые git-диффы.
- `UiKitConfigUpdater` — синхронизирует конфиг со сканом: страницы (uxml/PanelSettings, стартовая — `loading`, иначе `mainmenu`, иначе первая), счётчики (id → пути), предложения навигации (`UiNavigationProposer`), результаты попапов. Ручные правки не перезаписываются.
- Diff против прошлой генерации хранится в снапшоте `Generated/UiKitModel.g.txt`.
- `SceneObjectBuilder` — идемпотентная сборка сцены по pageId (см. quickstart); на стартовую страницу вешает `UiFakeLoading`.
- Окно `Neoxider → UI Kit`: дерево «страницы → попапы → элементы» с чекбоксами исключения и override'ами типа виджета/id счётчика (хранятся в конфиге, переживают переимпорт); настройки namespace/папок; пресеты и `UiAnimationMode` per-страница; три кнопки — «Сгенерировать вьюшки», «Создать объекты на сцене», «Создать адаптер Neoxider».
