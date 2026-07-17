# UiKit API — шпаргалка

Файл сгенерирован автоматически (UiKitGenerator) и обновляется при каждой генерации — не редактируйте вручную.

Namespace сгенерированного кода: `Game.Ui`.

## Быстрый старт

```csharp
UiKit.Pages.Show(UiIds.Gameplay.Id);                 // показать страницу
var page = UiKit.Get<GameplayView>();          // типизированная вьюшка страницы
UiKit.Get<GameplayView>().PopupPause.Open();  // открыть попап
var result = await UiKit.Popups.OpenAsync(UiIds.Gameplay.PopupPause.Path); // модальный попап
UiKit.Money.Set(1500);                                 // счётчик денег (алиас в конфиге)
UiKit.OnClick("pageId/button_x", () => { });           // клик по пути (переживает reload)
```

## Страницы

### `gameplay` — GameplayView

UXML: `Assets/MY_GAME_DESIGN/UXML/gameplay.uxml`

| Поле | Тип | Путь |
| --- | --- | --- |
| `GameplayView.PanelMerges` | `CounterView` | `gameplay/panel_merges` |
| `GameplayView.ImagePanelMerges0001` | `ImageView` | `gameplay/image_panel_merges_0001` |
| `GameplayView.LabelPanelMerges0001` | `LabelView` | `gameplay/label_panel_merges_0001` |
| `GameplayView.LabelPanelMerges0002` | `LabelView` | `gameplay/label_panel_merges_0002` |
| `GameplayView.LabelPanelMerges0003` | `LabelView` | `gameplay/label_panel_merges_0003` |
| `GameplayView.PanelTimer` | `TimerView` | `gameplay/panel_timer` |
| `GameplayView.ImagePanelTimer0001` | `ImageView` | `gameplay/image_panel_timer_0001` |
| `GameplayView.PanelPanelTimer0001` | `VisualElement` | `gameplay/panel_panel_timer_0001` |
| `GameplayView.ImagePanelTimer0002` | `ImageView` | `gameplay/image_panel_timer_0002` |
| `GameplayView.LabelPanelTimer0001` | `LabelView` | `gameplay/label_panel_timer_0001` |
| `GameplayView.PanelCoin` | `CounterView` | `gameplay/panel_coin` |
| `GameplayView.ImagePanelCoin0001` | `ImageView` | `gameplay/image_panel_coin_0001` |
| `GameplayView.ImagePanelCoin0002` | `ImageView` | `gameplay/image_panel_coin_0002` |
| `GameplayView.LabelPanelCoin0001` | `LabelView` | `gameplay/label_panel_coin_0001` |
| `GameplayView.ButtonPause` | `ButtonView` | `gameplay/button_pause` |
| `GameplayView.ImageButtonPause0001` | `ImageView` | `gameplay/image_button_pause_0001` |

#### Попап `popup_pause` — поле `PopupPause` (PopupPauseView)

Путь: `gameplay/popup_pause` — `UiKit.Popups.Open(...)` / `OpenAsync(...)`.

| Поле | Тип | Путь |
| --- | --- | --- |
| `GameplayView.PopupPause.Background` | `ImageView` | `gameplay/popup_pause/background` |
| `GameplayView.PopupPause.PanelPause` | `VisualElement` | `gameplay/popup_pause/panel_pause` |
| `GameplayView.PopupPause.PanelPanelPause0001` | `VisualElement` | `gameplay/popup_pause/panel_panel_pause_0001` |
| `GameplayView.PopupPause.ImagePanelPause0001` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0001` |
| `GameplayView.PopupPause.ImagePanelPause0002` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0002` |
| `GameplayView.PopupPause.ImagePanelPause0003` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0003` |
| `GameplayView.PopupPause.ImagePanelPause0004` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0004` |
| `GameplayView.PopupPause.ImagePanelPause0005` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0005` |
| `GameplayView.PopupPause.ImagePanelPause0006` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0006` |
| `GameplayView.PopupPause.ImagePanelPause0007` | `ImageView` | `gameplay/popup_pause/image_panel_pause_0007` |
| `GameplayView.PopupPause.ButtonRestart` | `ButtonView` | `gameplay/popup_pause/button_restart` |
| `GameplayView.PopupPause.ImageButtonRestart0001` | `ImageView` | `gameplay/popup_pause/image_button_restart_0001` |
| `GameplayView.PopupPause.LabelButtonRestart0001` | `LabelView` | `gameplay/popup_pause/label_button_restart_0001` |
| `GameplayView.PopupPause.ButtonClose` | `ButtonView` | `gameplay/popup_pause/button_close` |
| `GameplayView.PopupPause.ImageButtonClose0001` | `ImageView` | `gameplay/popup_pause/image_button_close_0001` |
| `GameplayView.PopupPause.LabelButtonClose0001` | `LabelView` | `gameplay/popup_pause/label_button_close_0001` |
| `GameplayView.PopupPause.LabelPanelPause0001` | `LabelView` | `gameplay/popup_pause/label_panel_pause_0001` |
| `GameplayView.PopupPause.LabelPanelPause0002` | `LabelView` | `gameplay/popup_pause/label_panel_pause_0002` |
| `GameplayView.PopupPause.LabelPanelPause0003` | `LabelView` | `gameplay/popup_pause/label_panel_pause_0003` |
| `GameplayView.PopupPause.LabelPanelPause0004` | `LabelView` | `gameplay/popup_pause/label_panel_pause_0004` |
| `GameplayView.PopupPause.PanelHeader` | `VisualElement` | `gameplay/popup_pause/panel_header` |
| `GameplayView.PopupPause.PanelPanelHeader0001` | `VisualElement` | `gameplay/popup_pause/panel_panel_header_0001` |
| `GameplayView.PopupPause.ImagePanelHeader0001` | `ImageView` | `gameplay/popup_pause/image_panel_header_0001` |
| `GameplayView.PopupPause.LabelPanelHeader0001` | `LabelView` | `gameplay/popup_pause/label_panel_header_0001` |

#### Попап `popup_endgame` — поле `PopupEndgame` (PopupEndgameView)

Путь: `gameplay/popup_endgame` — `UiKit.Popups.Open(...)` / `OpenAsync(...)`.

| Поле | Тип | Путь |
| --- | --- | --- |
| `GameplayView.PopupEndgame.Background` | `ImageView` | `gameplay/popup_endgame/background` |
| `GameplayView.PopupEndgame.PanelEndGame` | `VisualElement` | `gameplay/popup_endgame/panel_end_game` |
| `GameplayView.PopupEndgame.ImagePanelEndGame0001` | `ImageView` | `gameplay/popup_endgame/image_panel_end_game_0001` |
| `GameplayView.PopupEndgame.PanelReward` | `CounterView` | `gameplay/popup_endgame/panel_reward` |
| `GameplayView.PopupEndgame.ImagePanelReward0001` | `ImageView` | `gameplay/popup_endgame/image_panel_reward_0001` |
| `GameplayView.PopupEndgame.PanelPanelReward0001` | `VisualElement` | `gameplay/popup_endgame/panel_panel_reward_0001` |
| `GameplayView.PopupEndgame.ImagePanelReward0002` | `ImageView` | `gameplay/popup_endgame/image_panel_reward_0002` |
| `GameplayView.PopupEndgame.LabelPanelReward0001` | `LabelView` | `gameplay/popup_endgame/label_panel_reward_0001` |
| `GameplayView.PopupEndgame.LabelPanelReward0002` | `LabelView` | `gameplay/popup_endgame/label_panel_reward_0002` |
| `GameplayView.PopupEndgame.LabelPanelReward0003` | `LabelView` | `gameplay/popup_endgame/label_panel_reward_0003` |
| `GameplayView.PopupEndgame.PanelMergesCount` | `CounterView` | `gameplay/popup_endgame/panel_merges_count` |
| `GameplayView.PopupEndgame.ImagePanelMergesCount0001` | `ImageView` | `gameplay/popup_endgame/image_panel_merges_count_0001` |
| `GameplayView.PopupEndgame.PanelPanelMergesCount0001` | `VisualElement` | `gameplay/popup_endgame/panel_panel_merges_count_0001` |
| `GameplayView.PopupEndgame.ImagePanelMergesCount0002` | `ImageView` | `gameplay/popup_endgame/image_panel_merges_count_0002` |
| `GameplayView.PopupEndgame.LabelPanelMergesCount0001` | `LabelView` | `gameplay/popup_endgame/label_panel_merges_count_0001` |
| `GameplayView.PopupEndgame.LabelPanelMergesCount0002` | `LabelView` | `gameplay/popup_endgame/label_panel_merges_count_0002` |
| `GameplayView.PopupEndgame.LabelPanelMergesCount0003` | `LabelView` | `gameplay/popup_endgame/label_panel_merges_count_0003` |
| `GameplayView.PopupEndgame.ButtonRestart` | `ButtonView` | `gameplay/popup_endgame/button_restart` |
| `GameplayView.PopupEndgame.ImageButtonRestart0001` | `ImageView` | `gameplay/popup_endgame/image_button_restart_0001` |
| `GameplayView.PopupEndgame.LabelButtonRestart0001` | `LabelView` | `gameplay/popup_endgame/label_button_restart_0001` |
| `GameplayView.PopupEndgame.ButtonMainMenu` | `ButtonView` | `gameplay/popup_endgame/button_main_menu` |
| `GameplayView.PopupEndgame.PanelButtonMainMenu0001` | `VisualElement` | `gameplay/popup_endgame/panel_button_main_menu_0001` |
| `GameplayView.PopupEndgame.ImageButtonMainMenu0001` | `ImageView` | `gameplay/popup_endgame/image_button_main_menu_0001` |
| `GameplayView.PopupEndgame.LabelButtonMainMenu0001` | `LabelView` | `gameplay/popup_endgame/label_button_main_menu_0001` |
| `GameplayView.PopupEndgame.PanelHeader` | `VisualElement` | `gameplay/popup_endgame/panel_header` |
| `GameplayView.PopupEndgame.PanelPanelHeader0001` | `VisualElement` | `gameplay/popup_endgame/panel_panel_header_0001` |
| `GameplayView.PopupEndgame.ImagePanelHeader0001` | `ImageView` | `gameplay/popup_endgame/image_panel_header_0001` |
| `GameplayView.PopupEndgame.LabelPanelHeader0001` | `LabelView` | `gameplay/popup_endgame/label_panel_header_0001` |

### `loading` — LoadingView

UXML: `Assets/MY_GAME_DESIGN/UXML/loading.uxml`

| Поле | Тип | Путь |
| --- | --- | --- |
| `LoadingView.Background` | `ImageView` | `loading/background` |
| `LoadingView.Image0001` | `ImageView` | `loading/image_0001` |
| `LoadingView.Progressbar` | `BarView` | `loading/progressbar` |
| `LoadingView.ImageProgressbarBack` | `ImageView` | `loading/image_progressbar_back` |
| `LoadingView.ImageProgressbarFill` | `ImageView` | `loading/image_progressbar_fill` |

### `mainmenu` — MainmenuView

UXML: `Assets/MY_GAME_DESIGN/UXML/mainmenu.uxml`

| Поле | Тип | Путь |
| --- | --- | --- |
| `MainmenuView.PanelCoin` | `CounterView` | `mainmenu/panel_coin` |
| `MainmenuView.ImagePanelCoin0001` | `ImageView` | `mainmenu/image_panel_coin_0001` |
| `MainmenuView.ImagePanelCoin0002` | `ImageView` | `mainmenu/image_panel_coin_0002` |
| `MainmenuView.LabelPanelCoin0001` | `LabelView` | `mainmenu/label_panel_coin_0001` |
| `MainmenuView.ButtonSetting` | `ButtonView` | `mainmenu/button_setting` |
| `MainmenuView.ImageButtonSetting0001` | `ImageView` | `mainmenu/image_button_setting_0001` |
| `MainmenuView.Label0001` | `LabelView` | `mainmenu/label_0001` |
| `MainmenuView.Label00012` | `LabelView` | `mainmenu/label_0001_2` |
| `MainmenuView.Label00013` | `LabelView` | `mainmenu/label_0001_3` |
| `MainmenuView.Label00014` | `LabelView` | `mainmenu/label_0001_4` |
| `MainmenuView.ButtonPlay` | `ButtonView` | `mainmenu/button_play` |
| `MainmenuView.ImageButtonPlay0001` | `ImageView` | `mainmenu/image_button_play_0001` |
| `MainmenuView.LabelButtonPlay0001` | `LabelView` | `mainmenu/label_button_play_0001` |
| `MainmenuView.Panel0001` | `VisualElement` | `mainmenu/panel_0001` |
| `MainmenuView.PanelUpgrade` | `ShopItemView` | `mainmenu/panel_upgrade` |
| `MainmenuView.ImagePanelUpgrade0001` | `ImageView` | `mainmenu/image_panel_upgrade_0001` |
| `MainmenuView.ImagePanelUpgrade0002` | `ImageView` | `mainmenu/image_panel_upgrade_0002` |
| `MainmenuView.ButtonBuy` | `ButtonView` | `mainmenu/button_buy` |
| `MainmenuView.ImageButtonBuy0001` | `ImageView` | `mainmenu/image_button_buy_0001` |
| `MainmenuView.PanelButtonBuy0001` | `VisualElement` | `mainmenu/panel_button_buy_0001` |
| `MainmenuView.ImageButtonBuy0002` | `ImageView` | `mainmenu/image_button_buy_0002` |
| `MainmenuView.LabelButtonBuy0001` | `LabelView` | `mainmenu/label_button_buy_0001` |
| `MainmenuView.ButtonLeft` | `ButtonView` | `mainmenu/button_left` |
| `MainmenuView.ButtonRight` | `ButtonView` | `mainmenu/button_right` |

#### Попап `popup_setting` — поле `PopupSetting` (PopupSettingView)

Путь: `mainmenu/popup_setting` — `UiKit.Popups.Open(...)` / `OpenAsync(...)`.

| Поле | Тип | Путь |
| --- | --- | --- |
| `MainmenuView.PopupSetting.Background` | `ImageView` | `mainmenu/popup_setting/background` |
| `MainmenuView.PopupSetting.PanelSetting` | `VisualElement` | `mainmenu/popup_setting/panel_setting` |
| `MainmenuView.PopupSetting.ImagePanelSetting0001` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0001` |
| `MainmenuView.PopupSetting.ImagePanelSetting0002` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0002` |
| `MainmenuView.PopupSetting.ImagePanelSetting0003` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0003` |
| `MainmenuView.PopupSetting.ImagePanelSetting0004` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0004` |
| `MainmenuView.PopupSetting.ImagePanelSetting0005` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0005` |
| `MainmenuView.PopupSetting.ImagePanelSetting0006` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0006` |
| `MainmenuView.PopupSetting.ImagePanelSetting0007` | `ImageView` | `mainmenu/popup_setting/image_panel_setting_0007` |
| `MainmenuView.PopupSetting.ButtonClose` | `ButtonView` | `mainmenu/popup_setting/button_close` |
| `MainmenuView.PopupSetting.ImageButtonClose0001` | `ImageView` | `mainmenu/popup_setting/image_button_close_0001` |
| `MainmenuView.PopupSetting.LabelButtonClose0001` | `LabelView` | `mainmenu/popup_setting/label_button_close_0001` |
| `MainmenuView.PopupSetting.LabelPanelSetting0001` | `LabelView` | `mainmenu/popup_setting/label_panel_setting_0001` |
| `MainmenuView.PopupSetting.LabelPanelSetting0002` | `LabelView` | `mainmenu/popup_setting/label_panel_setting_0002` |
| `MainmenuView.PopupSetting.LabelPanelSetting0003` | `LabelView` | `mainmenu/popup_setting/label_panel_setting_0003` |
| `MainmenuView.PopupSetting.LabelPanelSetting0004` | `LabelView` | `mainmenu/popup_setting/label_panel_setting_0004` |
| `MainmenuView.PopupSetting.PanelHeader` | `VisualElement` | `mainmenu/popup_setting/panel_header` |
| `MainmenuView.PopupSetting.ImagePanelHeader0001` | `ImageView` | `mainmenu/popup_setting/image_panel_header_0001` |
| `MainmenuView.PopupSetting.LabelPanelHeader0001` | `LabelView` | `mainmenu/popup_setting/label_panel_header_0001` |

## Счётчики

| Id | Константа | Пример |
| --- | --- | --- |
| `coin` | `UiIds.Counters.Coin` | `UiKit.Counters[UiIds.Counters.Coin].Set(10);` |
| `merges` | `UiIds.Counters.Merges` | `UiKit.Counters[UiIds.Counters.Merges].Set(10);` |
| `merges_count` | `UiIds.Counters.MergesCount` | `UiKit.Counters[UiIds.Counters.MergesCount].Set(10);` |
| `reward` | `UiIds.Counters.Reward` | `UiKit.Counters[UiIds.Counters.Reward].Set(10);` |

## Правила

- Поля страниц перепривязываются при каждом reload панели: подписки на события делайте в `OnBind()` partial-класса страницы.
- Кнопочные поля (`ButtonView`) переиспользуют автоматическую вьюшку страницы (анимация нажатия, звук, троттлинг) и валидны только пока страница привязана.
- Пользовательский код пишите в partial-файле `<Page>View.cs` — он создаётся один раз и не перезаписывается генератором.
- Сырые строки путей — fallback; используйте константы `UiIds`, тогда переименование в дизайне превратится в ошибку компиляции.
