# Адаптеры: интеграция kit'а с игрой

Ядро kit'а не зависит ни от NeoxiderTools, ни от каких-либо других игровых систем. Вся интеграция — через четыре узких интерфейса из `Runtime/Flow/`, которые подключаются одним вызовом:

```csharp
UiKit.Flow.Connect(adapter); // подключает ВСЕ интерфейсы, которые объект реализует
```

| Интерфейс | Контракт | Зачем |
|---|---|---|
| `IUiFlowSource` | события `Win`, `Lose`, `Pause`, `Resume`, `Menu`, `GameStart`, `GameEnd`; метод `SetPaused(bool)` | игровые моменты → декларативные действия страниц (конфиг `flow`); kit вызывает `SetPaused` при открытии/закрытии попапа паузы |
| `IUiCounterSource` | событие `CounterChanged(string counterId, long value)` | деньги/энергия/счёт из игры сами обновляют `CounterRegistry` (и все страницы) |
| `IUiClickSound` | `PlayClick()` | звук клика кнопок через вашу аудиосистему вместо AudioClip конфига |
| `IUiAudioSettings` | свойства `SoundOn` / `MusicOn` (get/set) | ваша аудиосистема — источник истины для переключателей настроек; персистентность на её стороне (PlayerPrefs kit'а обходится) |

Маппинг «момент → действие» редактируется в `UiKitConfig.flow`: например `Win → OpenPopup "gameplay/popup_endgame"`, `Pause → OpenPopup "gameplay/popup_pause"`. Действия — `Show / Push / Pop / OpenPopup / ClosePopup` + targetId.

## NeoxiderTools: кнопка «Создать адаптер Neoxider»

В окне **Neoxider → UI Kit** есть кнопка **«Создать адаптер Neoxider»** — она генерирует файл `Assets/UiKit/NeoxiderUiAdapter.cs` (одноразово; дальше файл ваш, правьте свободно).

Важно про define: **NeoxiderTools не поставляет собственный scripting define**, поэтому файл обёрнут в `#if NEOXIDER_TOOLS`:

- NeoxiderTools (`com.neoxider.tools`) установлен → добавьте `NEOXIDER_TOOLS` в **Project Settings → Player → Scripting Define Symbols** — скомпилируется полный мост:
  - `EM` (`OnWin/OnLose/OnPause/OnResume/OnMenu/OnGameStart/OnEnd`) → события `IUiFlowSource`;
  - `Money.CurrentMoney` → `IUiCounterSource` (id счётчика настраивается в инспекторе, по умолчанию `coin`);
  - `AM` → `IUiClickSound` (клип задаётся в инспекторе, пусто = AudioClip из UiKitConfig);
  - `AMSettings` (`SetEfx/SetMusic`) → `IUiAudioSettings`;
  - `SetPaused` → `GM.I.Pause()/Resume()` (с защитой от эхо-цикла Pause↔попап).
- Без define компилируется **шаблонный** адаптер с теми же интерфейсами и TODO-местами — файл валиден в любом проекте, его же удобно взять как заготовку под свои системы.

Использование: положите компонент `NeoxiderUiAdapter` в сцену (например, на корень `UI`) — он сам вызывает `UiKit.Flow.Connect(this)` в `Start`.

## Свой адаптер (любая игра, ~30 строк)

```csharp
using System;
using Neo.UIKit;
using UnityEngine;

public sealed class MyGameUiAdapter : MonoBehaviour,
    IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings
{
    public event Action Win;
    public event Action Lose;
    public event Action Pause;
    public event Action Resume;
    public event Action Menu;
    public event Action GameStart;
    public event Action GameEnd;
    public event Action<string, long> CounterChanged;

    public bool SoundOn
    {
        get => MyAudio.SfxEnabled;
        set => MyAudio.SfxEnabled = value;
    }

    public bool MusicOn
    {
        get => MyAudio.MusicEnabled;
        set => MyAudio.MusicEnabled = value;
    }

    private void Start()
    {
        UiKit.Flow.Connect(this);
        MyGame.LevelWon    += () => Win?.Invoke();
        MyGame.LevelLost   += () => Lose?.Invoke();
        MyGame.CoinsChanged += c => CounterChanged?.Invoke("coin", c);
    }

    public void SetPaused(bool paused) => MyGame.SetPaused(paused);
    public void PlayClick() => MyAudio.PlayOneShot("ui_click");
}
```

Реализовывать все четыре интерфейса не обязательно — подключается ровно то, что реализовано. Объект вообще без интерфейсов даст понятную ошибку в консоль.

Частичные варианты без адаптера:

- счётчики — ручные вызовы `UiKit.Money.Set(...)` / `UiKit.Counters["id"].Set(...)`;
- пауза — при `manageTimeScale = true` в конфиге kit сам ставит `Time.timeScale` 0/1 на попапе паузы (попап определяется по `pausePopupPath`, иначе по «pause» в пути);
- клик всей UI — глобальный хук `UiKit.AnyButtonClicked += name => ...`.

## Аудио-режимы

Три способа, работают и по отдельности, и вместе:

### 1. AudioMixer из коробки (ноль кода)

В `UiKitConfig` (секция Audio): назначьте `audioMixer` и имена exposed-параметров — `soundParam`, `musicParam`, опционально `masterParam`. Тогда:

- `UiKit.Audio.SoundOn / MusicOn` мьютят соответствующие параметры до −80 dB (master глушится, только когда выключено и то и другое);
- состояние сохраняется в PlayerPrefs (`Neo.UIKit.SoundOn` / `Neo.UIKit.MusicOn`) и переживает перезапуск;
- `clickSound` (AudioClip в конфиге) играется kit'ом самостоятельно через 2D `AudioSource` бутстрапа.

### 2. Адаптер (`IUiAudioSettings` + `IUiClickSound`)

Если в проекте своя аудиосистема — подключите адаптер (см. выше). Он становится источником истины: `UiKit.Audio.SoundOn` читает/пишет через адаптер, PlayerPrefs kit'а не используется. `PlayClick()` адаптера имеет приоритет над AudioClip конфига.

### 3. События

Независимо от режимов 1–2, любые изменения переключателей поднимают события:

```csharp
UiKit.Audio.SoundChanged += on => MySfx.SetEnabled(on);
UiKit.Audio.MusicChanged += on => MyMusic.SetEnabled(on);
```

Подключение переключателей из попапа настроек (тумблеры `toggle_*` → `ToggleView`, см. `naming-conventions.md`):

```csharp
protected override void OnBind()
{
    // в partial-классе страницы с popup_setting
    var sound = PopupSetting.ToggleSound; // сгенерированное поле ToggleView
    sound.SetWithoutNotify(UiKit.Audio.SoundOn);
    sound.Changed += on => UiKit.Audio.SoundOn = on;
}
```

Если в дизайне тумблеры не размечены (голые image/label), состояние переключается вручную: `UiKit.OnClick(path, ...)` + `ImageView.SetTint(...)` / `SetImage(...)` по `UiKit.Audio.SoundOn`.
