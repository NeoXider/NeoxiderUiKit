# Quickstart: от .fui до работающего интерфейса

## Шаг 0. Установка

1. Установите импортёр «MTK Figma UI Import» и сам kit по git URL (`https://github.com/NeoXider/NeoxiderUiKit.git`, лучше с тегом `#vX.Y.Z`).
2. Импортируйте `.fui`-файл — появится `Assets/<PROJECT>/` с `UXML/`, `USS/`, `Textures/`, `PanelSettings/`.

## Шаг 1. Генерация вьюшек

Откройте **Neoxider → UI Kit**. Окно найдёт или создаст `Assets/UiKit/UiKitConfig.asset`, определит папку FUI-проекта и покажет дерево «страницы → попапы → элементы». Проверьте настройки (namespace — по умолчанию `Game.Ui`, папка вывода — `Assets/UiKit`), при желании исключите элементы или переопределите тип виджета прямо в дереве.

Нажмите **«Сгенерировать вьюшки»**. Результат:

```
Assets/UiKit/
  UiKitConfig.asset          страницы, счётчики, навигация, стили, звук
  UiKitApi.md                живая шпаргалка по всем полям и путям
  Generated/
    LoadingView.g.cs         перегенерируются каждый раз
    MainmenuView.g.cs
    GameplayView.g.cs
    UiIds.g.cs               константы путей
    UiKitModel.g.txt         снапшот модели для diff-отчёта
  Views/
    LoadingView.cs           ВАШИ partial-классы — создаются один раз,
    MainmenuView.cs          при перегенерации не трогаются
    GameplayView.cs
```

## Шаг 2. Объекты на сцене

Нажмите **«Создать объекты на сцене»** (после компиляции скриптов). Появится:

```
UI                       ← UiKitBootstrap (+ AudioSource для кликов)
├── loading              ← LoadingView + PanelRenderer + UiFakeLoading (стартовая, активна)
├── mainmenu             ← MainmenuView + PanelRenderer (выключена)
└── gameplay             ← GameplayView + PanelRenderer (выключена)
```

Кнопка идемпотентна: повторный запуск обновляет только ссылки uxml/PanelSettings у существующих страниц (ключ — pageId в компоненте), не трогая transform и ваши компоненты.

Нажмите Play: `UiFakeLoading` заполнит прогресс-бар за случайные 1–3 секунды «рваными» шагами и покажет `mainmenu`. Типовая навигация уже развешана из конфига: `button_setting` откроет `popup_setting`, `button_close` его закроет, `button_play` покажет `gameplay`. Кнопки анимируются и (если назначен `clickSound` в конфиге) кликают.

Рекомендуемые настройки анимаций в окне: страницам `loading` и `mainmenu` поставьте пресет `none` (или режим `None`) — стартовые экраны должны появляться мгновенно; `fade`/`slide` оставьте для `gameplay` и попапов.

## Шаг 3. Подключение к игре

### Деньги и счётчики

```csharp
UiKit.Money.Set(PlayerData.Coins);      // обновит panel_coin на всех страницах сразу
UiKit.Counters["merges"].Add(1);        // любой счётчик по id
```

### Клики по пути (можно до создания страниц, переживают reload)

```csharp
using Game.Ui; // namespace сгенерированного кода

UiKit.OnClick(UiIds.Mainmenu.ButtonPlay, () => Game.StartLevel());
UiKit.OnClick(UiIds.Gameplay.ButtonPause, () => Debug.Log("pause pressed"));
```

### Модальный попап

```csharp
string result = await UiKit.Popups.OpenAsync(UiIds.Gameplay.PopupEndgame.Path);
switch (result)              // имя кнопки без "button_" или override из конфига
{
    case "restart":   Game.Restart(); break;
    case "main_menu": UiKit.Pages.Show(UiIds.Mainmenu.Id); break;
}
```

### Partial-класс страницы (`Assets/UiKit/Views/MainmenuView.cs`)

`OnBind` вызывается после каждого bind (в т.ч. после reload дерева), подписки внутри него живут ровно до следующего `Unwire` — отписываться вручную не нужно. `OnShow`/`OnHide` — после завершения анимации показа/скрытия.

```csharp
namespace Game.Ui
{
    public partial class MainmenuView
    {
        protected override void OnBind()
        {
            ButtonBuy.Clicked += OnBuy;                 // ButtonView пересоздаётся на каждый bind
            PanelUpgrade.SetPrice("25 000");            // gradient-обёртка текста сохранится
            PanelUpgrade.SetAvailable(Game.Coins >= 25000);
        }

        protected override void OnShow()
        {
            PanelCoin.SetValue(Game.Coins);             // или UiKit.Money.Set из game-кода
        }

        private void OnBuy()
        {
            if (Game.TrySpend(25000))
                PanelUpgrade.SetPurchased(true);
        }
    }
}
```

### Фейковая загрузка (`UiFakeLoading`)

Компонент уже стоит на стартовой странице. В инспекторе: `minSeconds`/`maxSeconds` (1–3 по умолчанию), `nextPageId` (пусто = `mainmenu`), `progressBarPath` (пусто = первый `fui_type_progressbar`), размеры шага. Чтобы встроить в свой флоу:

```csharp
var fake = FindFirstObjectByType<Neo.UIKit.UiFakeLoading>();
fake.Completed += () => Analytics.Log("loading_done");
```

Для настоящей загрузки удалите компонент и двигайте бар сами через `BarView.SetProgress(0..1)` (или партиал `LoadingView` c `ResolveElement`).

## Шаг 4. Свой адаптер игры (~30 строк)

Kit не знает про ваши игровые системы — он объявляет узкие интерфейсы. Один объект может реализовать любые из них; `UiKit.Flow.Connect` подключит всё сразу:

```csharp
using System;
using Neo.UIKit;
using UnityEngine;

public sealed class MyGameUiAdapter : MonoBehaviour, IUiFlowSource, IUiCounterSource
{
    public event Action Win;
    public event Action Lose;
    public event Action Pause;
    public event Action Resume;
    public event Action Menu;
    public event Action GameStart;
    public event Action GameEnd;

    public event Action<string, long> CounterChanged;

    private void Start()
    {
        UiKit.Flow.Connect(this);

        // игра → UI: события маппятся на действия страниц в конфиге
        // (например Win → OpenPopup "gameplay/popup_endgame")
        MyGame.Instance.LevelWon  += () => Win?.Invoke();
        MyGame.Instance.LevelLost += () => Lose?.Invoke();

        // счётчики: UI обновится сам на всех страницах
        MyGame.Instance.CoinsChanged += coins => CounterChanged?.Invoke("coin", coins);
    }

    // UI → игра: kit вызывает при открытии/закрытии попапа паузы
    public void SetPaused(bool paused) => MyGame.Instance.SetPaused(paused);
}
```

Маппинг «момент → действие» редактируется в конфиге (`flow`): `Win → OpenPopup gameplay/popup_endgame`, `Pause → OpenPopup gameplay/popup_pause` и т.д. Без адаптера пауза работает через `Time.timeScale` (`manageTimeScale` в конфиге), а счётчики — ручными `UiKit.Money.Set(...)`.

Для NeoxiderTools адаптер генерируется кнопкой окна — см. `adapters.md`.

## Шаг 5. Свой пресет анимации

```csharp
using System;
using Neo.UIKit;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class RotatePopAnimator : IUiAnimator
{
    public void Show(VisualElement element, Action onDone)
    {
        element.style.rotate = new Rotate(new Angle(-8f));
        element.AddToClassList(UiAnimations.OpenClass);
        element.schedule.Execute(() =>
        {
            element.style.rotate = new Rotate(new Angle(0f));
            onDone?.Invoke();
        }).StartingIn(250);
    }

    public void Hide(VisualElement element, Action onDone)
    {
        element.RemoveFromClassList(UiAnimations.OpenClass);
        onDone?.Invoke();
    }
}
```

Регистрация (например, в `Awake` вашего адаптера или бутстрап-скрипта):

```csharp
UiAnimations.Register("rotate-pop", new RotatePopAnimator());
```

После регистрации `rotate-pop` можно вписать как пресет страницы/попапа в конфиге (и он появится в выпадашках окна). Точечная альтернатива — override `PlayShowAnimation`/`PlayHideAnimation` в partial-классе страницы или `ButtonView.SetPressAnimation(...)` для конкретной кнопки. Простейшие правки (тайминги, дистанции) кода не требуют — задайте `projectOverrideStyleSheet` в конфиге и переопределите правила `uikit.uss` в своём USS.

## Шаг 6. Переимпорт дизайна

Новый `.fui` → **«Сгенерировать вьюшки»** → diff-отчёт (добавлено / удалено / изменено, сломанные пути конфига) → `.g.cs` и конфиг обновлены, `Assets/UiKit/Views/*.cs` не тронуты. Переименованные элементы всплывут ошибками компиляции по константам `UiIds`.

## Новые страницы (уровень, магазин, win/lose и любые другие)

Весь интерфейс собирается из импортированных UXML/USS — новая страница добавляется **без правки пакета**:

1. Дизайнер добавляет экран в `.fui` (корень с классами `fui_type_screen fui_screen_<name>`, например `level`, `shop`, `win`, `lose`) и переимпортирует.
2. **«Сгенерировать вьюшки»** — появляется `LevelView`/`ShopView`/… c типизированными полями под все кнопки/лейблы/картинки, константы в `UiIds.<Page>`, запись в `UiKitConfig` (uxml + PanelSettings, дефолт `fade`/`ForwardAndBackward`), а по именам — навигация и flow-моменты (`win`/`lose` подхватятся автоматически).
3. **«Создать объекты на сцене»** — на сцене появляется GameObject страницы (идемпотентно, существующие не трогаются).
4. Настройте при желании анимацию страницы/попапов в конфиге и подключите к игре: `UiKit.Pages.Show(UiIds.Shop.Id)`, `UiKit.Get<ShopView>().ButtonBuy.Clicked += ...`, `UiKit.Flow.Raise(UiGameMoment.Win)` и т.п. Магазинные карточки — `ShopItemView` (кнопка + цена + иконка + состояние «куплено/недоступно»).

Ничего в самом пакете менять не нужно — только настройки в конфиге и подписки в своём коде.

## Генерация без namespace

Если проект не использует namespace, очистите поле **Namespace** в окне (или `generatorNamespace` в конфиге) — сгенерированные классы (`LoadingView`, `UiIds`, …) окажутся в глобальной области. Сборщик сцены находит компоненты страниц и по namespace, и по имени класса, поэтому работает в обоих режимах.
