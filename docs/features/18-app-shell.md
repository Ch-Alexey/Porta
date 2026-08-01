# Фича: приложение и UI-оболочка

**Статус:** первый срез готов (запускается) · **Слой:** `Porta.Core/App`, `Porta.App` (Avalonia)

Первый **запускаемый** продукт: десктоп-приложение поверх готового ядра.

## Bootstrap (`Porta.Core/App/AppEnvironment`)

Единая точка инициализации (платформонезависимая):
- папка данных (`~/Library/Application Support/Porta` на macOS и аналоги);
- личность устройства (`FileDeviceIdentityStore.LoadOrCreate`);
- БД (`PortaDatabase` + миграции) и репозитории (`Device`/`Storage`/`FileIndex`).

`AppEnvironment.Create()` вызывается один раз при старте приложения (App.axaml.cs).

## UI (Avalonia, MVVM)

- **Шапка:** имя устройства + его **Device ID** (реальный, из личности).
- **Вкладка «Хранилища»:** список папок синхронизации из БД; форма добавления
  (имя + путь → `StorageRepository.Add`).
- **Вкладка «Устройства»:** список доверенных устройств + **связывание по токену**
  («Показать приглашение» → токен `porta:…` для копирования; вставить токен + имя →
  «Подключиться» → устройство добавляется в доверенные). Через `IAppData.CreateInvitation`
  / `AcceptInvitation` поверх [`PairingService`](04-pairing.md).

VM: `MainViewModel(AppEnvironment)` → `StoragesViewModel` / `DevicesViewModel`
(CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`).

## Запуск

```bash
dotnet run --project src/Porta.App.Desktop
```

Проверено: приложение стартует, создаёт данные (`device.key` 0600, `porta.db` v2),
показывает Device ID и вкладки.

## Границы среза

- Отображение личности + хранилища (список/добавление) + устройства (список +
  связывание по токену) — **есть**.
- Пока нет: **рендер QR** (показываем токен строкой) и сканирование камерой; выбор
  папки диалогом; запуск синка из UI; discovery устройств; вкладка «Медиа»; прогресс.
- Связывание из UI закрывает только сторону joiner'а (принять токен → доверять
  инициатору). Обратная проверка (инициатор проверяет joiner'а) идёт по сети — при
  подключении транспорта к UI.
- Design-time previewer: `Design.DataContext` убран (VM требует `AppEnvironment`), данные
  видны только в рантайме.

## Тестируемость (абстракция данных)

Репозитории вынесены за интерфейсы `IDeviceRepository`/`IStorageRepository`; UI получает
данные через `IAppData`. View-модели зависят от интерфейсов, а не от `AppEnvironment`,
поэтому тестируются с in-memory фейками (`Porta.App/Design/InMemory*`), без файловой
системы. Design-time превью вернулось через `DesignAppData`/`DesignMainViewModel`.

## Тесты

- Ядро (`AppEnvironment`, репозитории) покрыто юнит-тестами.
- View-модели (`Porta.App.Tests`): пустой список, добавление хранилища (персист + очистка
  полей), игнор пустого ввода, список устройств, личность в оболочке.
