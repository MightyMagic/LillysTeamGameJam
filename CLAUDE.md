# MOBILE GEAR SOLID — заметки для Claude

Мобильный (Android, альбомная ориентация) 2D top-down стелс-экшен для геймджема в духе ранних Metal Gear.
Герой — «Биг Босс», куратор по рации — «Майор». Исходный GDD: `C:\Users\emily\Downloads\MetalGear-GameJam.pdf`
(PDF защищён паролем, читается через `pdftotext -enc UTF-8`).

## Договорённости с пользователем
- Общение на русском. Все тексты игры на русском.
- **Коммиты делает пользователь сам.** Не коммитить без явной просьбы.
- **Все тексты игры — только в `Assets/Resources/Texts/texts.txt`** (никаких строк в коде; в коде `Loc.T("key")` / `Loc.F("key", args)`).
- **Уровни — только в текстовых файлах** `Assets/Resources/Levels/*.txt`, редактируются вручную или в Level Editor.
- **Android-билды — по команде пользователя** («собери андроид билд»): меню `Mobile Gear Solid/Build Android APK`
  → `MobileGearSolid/Builds/AndroidBuilds/MobileGearSolid_<дата>.apk`. Папка `Builds/` в .gitignore.
- Арт — процедурный пиксель-арт в стиле тёмных шпионских референсов (32 px на тайл).

## Где что
Unity-проект: `MobileGearSolid/` (Unity 6000.6.5f1, URP 2D, новый Input System, uGUI + legacy Text).

| Что | Где |
|---|---|
| Единственная сцена | `Assets/Scenes/Game.unity` (Main Camera, Global Light 2D, объект `Game`). Всё остальное строится кодом |
| Тексты | `Assets/Resources/Texts/texts.txt` (`key = value`, `\n`, `{0}`) |
| Уровни и порядок миссий | `Assets/Resources/Levels/<id>.txt`, `campaign.txt` (tutorial, mission_01..03) |
| Формат уровней/сценариев | `MobileGearSolid/LEVELS.md` (держать в актуальном состоянии) |
| Спрайты | `Assets/Resources/Sprites/*.png`, генерируются `MobileGearSolid/Tools/gen_art.py` (Python + Pillow, запускать **без** `-I`) |
| Свои звуки и музыка | `Assets/Resources/Audio/audio.txt` (`sfx.<id>` / `music.menu` / `.level` / `.base` = путь без расширения \| громкость), файлы в `Audio/Sfx`, `Audio/Music`. Нет файла → встроенный процедурный звук / тишина |
| Импорт спрайтов | `Assets/Editor/SpriteImportSettings.cs` (PPU 32, Point, пивоты по префиксу имени) |

### Код (`Assets/Scripts`, namespace `MobileGearSolid`)
- `Core/Game.cs` — точка входа и фазы (Menu, Briefing, Playing, Dialog, CardPick, Paused, Result), кампания, **ночной рейд** (`StartRaid/ContinueRaid/RetreatRaid`, `Run`), карточки (`OfferCards/PickCard`, `ActiveCards`), `ApplyStats` (корабль + карточки → статы игрока), тревога, шум, свет, диалоги, чекпоинты туториала. Игра стартует с главного меню.
- `Core/ScriptRunner.cs` — исполняет `[script]` уровня (`say/objective/toast/highlight/checkpoint/wait .../end`).
- `Core/Loc.cs` — тексты. `Core/Art.cs` — кэш спрайтов, sorting orders, `SortY`. `Core/Sfx.cs` — процедурные звуки (в т.ч. выстрел с глушителем), но сначала ищет файл через `AudioConfig`. `Core/Music.cs` — зацикленная музыка с кроссфейдом (menu в меню, base на корабле, level в миссиях/рейде). `Editor/AudioImportSettings.cs` — музыка Streaming, звуки DecompressOnLoad. `Core/GameInput.cs` — клавиатура + тач.
- `Missions/LevelDef.cs` — парсер файла уровня (`[mission] [map] [guards] [script]`, `tier`, guard `hp=`), `EstimateDifficulty()`, enum’ы. `MissionState.cs` — состояние попытки. `Progress.cs` — PlayerPrefs (кредиты, пройденные уровни, рекорд рейда).
- `Missions/Meta.cs` — прокачка на корабле (6 статов, уровни и цены, база: 2 HP, урон 1, 4 патрона, 1 ловушка; у охраны 2 HP). `Cards.cs` — 10 карточек рейда. `RunState.cs` — забег: 5 комнат по ступеням 1-1-2-2-3 из `Levels/raid`, перенос HP/патронов/карточек, награды.
- `World/Level.cs` — сборка карты (Tilemap + пропы), **легенда символов `Level.Legend`** (её же использует редактор), A*, питание. Интерактивные объекты: `Interactable` (база) → `Pickup`, `PowerPanel`, `DataTerminal`, `LockedDoor`, `ExitDoor`, `HideBox`, `Chest` (`$`, выбор карточки).
- `Actors/Guard.cs` — ИИ: Patrol → Suspicious → Search → Alert, CheckPanel; HP (2, бронированные 3), ранение = оглушение 1.3 с, потом тревога (`reason.wounded`); подозрение копится постепенно; находит тела; пинает коробку в тревоге; после прихода на последнюю позицию осматривается и обыскивает точки рядом (не крутится).
- `Actors/Player.cs` — движение, пистолет (авто-цель, **только вблизи: 4.5**), коробка (камеры не видят; охрана не видит неподвижную), шумовая ловушка ставится под ноги (`NoiseTrap`).
- `Actors/SecurityCamera.cs`, `VisionCone.cs` (меш-конус с рейкастами), `NoiseRipple.cs`, `CharAnim.cs`.
- `UI/GameUI.cs` — все экраны (меню с кнопками «Ночной рейд» и «Корабль», экран корабля на палубе `bg_deck`, выбор карточек, итоги комнаты/рейда) + окно диалогов (визуальная новелла: портрет `portrait_<speaker>`, печать текста), `TouchButton` (подсветка для туториала), `VirtualJoystick`, `UIKit`.

### Editor (`Assets/Editor`)
- `LevelEditorWindow.cs` — меню **Mobile Gear Solid → Level Editor** (кисть/пипетка, маршруты, проверка достижимости, «Сохранить и играть» через `SessionState "ns_test_level"`).
- `AndroidBuilder.cs` — сборка APK. `ProjectSetup.cs` — пересоздаёт сцену Game. `SmokeTest.cs` — автопилот проходит весь туториал и открывает все миссии, скриншоты в `Logs/SmokeTest`, прогресс игрока восстанавливает.

## Что уже сделано
- MVP по GDD: 3 миссии + туториал, охрана с 4 состояниями, камеры, электрощит (гасит свет и камеры, охрану отправляют чинить), терминал данных, пропуск/запертые двери, лут, ранги Ghost / Silent Operator / Compromised, экран итогов, кредиты и 3 улучшения, тач-управление.
- Коробка для пряток, шумовая ловушка, Биг Босс + портреты, VN-диалоги, FTUE-туториал (устранение → коробка → ловушка), в туториале поимка = откат к чекпоинту.
- Тексты и уровни вынесены в файлы, редактор уровней, Android-сборка.
- Исправлено: охрана крутилась на месте в тревоге; Level Editor падал после domain reload.
- Игра называется **Mobile Gear Solid** (namespace `MobileGearSolid`, меню редактора `Mobile Gear Solid/...`, Android id `com.DefaultCompany.MobileGearSolid`). PlayerPrefs-ключи остались с префиксом `ns_` — не менять, иначе слетит прогресс.
- Игра стартует с главного меню (туториал — пункт 00 «Новичок? Начни отсюда»).
- Мета-слой как в Archero: корабль «Мать-база» с прокачкой статов за кредиты; режим «Ночной рейд» из 12 ручных комнат (`Levels/raid`, по 4 на ступень) с сундуками и 10 карточками; Level Editor показывает автооценку сложности. Игрок скользит вдоль стен (коллайдер без трения).
- HUD v2 (`GameUI.BuildHUD/UpdateHUD`): панель текущей цели (крупно, `1/2 Найти пропуск`) + доп. контракты; справа пиксельные сердца и счётчики патронов/ловушек/разведданных/пропуска (иконки `ui_heart`, `ui_ammo` и др. из `gen_art.py`); указатель цели (`Game.ObjectiveTarget()`, в сценариях — `ScriptRunner.Target`): маркер над целью или стрелка у края экрана с расстоянием; красная виньетка при попадании (`UI.OnPlayerHurt`) и пульс + сердцебиение при 1 HP.

## Как проверять изменения
- Пользователь обычно держит проект открытым в Unity → batch-режим Unity недоступен. Управлять живым редактором через Unity CLI (`unity status`, `unity command ...`):
  - перекомпиляция: `unity command eval --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";'`, затем `recompile` / `recompile_status`; ошибки: `unity command console --level error`.
  - автотест: `unity command menu --path "Mobile Gear Solid/Run Smoke Test"`, ждать `[SmokeTest] finished` в консоли, смотреть PNG в `MobileGearSolid/Logs/SmokeTest`.
  - сборка: `unity command menu --path "Mobile Gear Solid/Build Android APK"` (CLI отвалится по таймауту — это нормально; результат `[MGS] Android build ...` в `%LOCALAPPDATA%/Unity/Editor/Editor.log`; первая сборка ~16 мин, IL2CPP ARM64).
- В проекте выключено `runInBackground`, поэтому в несфокусированном редакторе Play почти не тикает (SmokeTest включает его на время прогона). Domain reload при входе в Play отключён (Enter Play Mode Options). **Поэтому статические поля живут между запусками Play**: всё, что создаётся в рантайме (AudioClip.Create, материалы, текстуры) и кэшируется в static, нужно пересоздавать в Init — иначе во втором Play будут «мёртвые» ссылки (так ломались встроенные звуки `Sfx`).
- Если Unity закрыт: `"/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe" -batchmode -projectPath <path> -executeMethod ...` (без `-quit` для SmokeTest; `unity run` всегда добавляет `-quit`).

## Возможные следующие шаги
- Свой applicationIdentifier/компания вместо `com.DefaultCompany.MobileGearSolid`, иконка, release-подпись.
- Баланс миссий 02/03 по итогам плейтестов (GDD §11), больше гаджетов, музыка.
