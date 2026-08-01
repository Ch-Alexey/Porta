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
- **Вкладка «Устройства»:** список доверенных устройств из БД.

VM: `MainViewModel(AppEnvironment)` → `StoragesViewModel` / `DevicesViewModel`
(CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`).

## Запуск

```bash
dotnet run --project src/Porta.App.Desktop
```

Проверено: приложение стартует, создаёт данные (`device.key` 0600, `porta.db` v2),
показывает Device ID и вкладки.

## Границы среза

- Отображение личности + списки хранилищ/устройств + добавление хранилища — **есть**.
- Пока нет: выбор папки диалогом, связывание по QR, запуск синка из UI, discovery в
  списке устройств, вкладка «Медиа», прогресс. Это следующие срезы UI.
- Design-time previewer: `Design.DataContext` убран (VM требует `AppEnvironment`), данные
  видны только в рантайме.

## Тесты

- Ядро (`AppEnvironment`, репозитории) покрыто юнит-тестами.
- UI-слой пока проверяется запуском (автоматических UI-тестов нет — задел на будущее).
