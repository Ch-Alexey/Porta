# Фича: обнаружение устройств в UI (mDNS)

**Статус:** готово · **Слой:** `Porta.App`, `Porta.App.Desktop` (композиция), `Porta.Core/Discovery`

Показывает найденные в сети устройства во вкладке «Устройства». Первый шаг к
«Синхронизировать» из UI (нужен адрес пира — его даёт discovery).

## Композиция

`MdnsDeviceDiscovery` живёт в `Porta.Infrastructure`, а общий `Porta.App` её не видит.
Поэтому **десктоп-голова** ([`Program.cs`](../../src/Porta.App.Desktop/Program.cs)):
создаёт `AppEnvironment` + `MdnsDeviceDiscovery`, объявляет это устройство
(`Advertise`) и запускает поиск (`StartBrowsing`), затем внедряет их в приложение через
`App.InjectedData`/`App.InjectedDiscovery`. Если mDNS недоступен (нет multicast) —
приложение всё равно запускается (обёрнуто в try/catch).

Ядро от инфраструктуры не зависит: VM работает с абстракцией `IDeviceDiscovery`.

## View-модель

`DevicesViewModel(IAppData, IDeviceDiscovery?, IUiDispatcher?)`:
- подписывается на `PeerDiscovered`/`PeerLost`, ведёт `DiscoveredPeers`;
- события mDNS приходят из фоновых потоков → маршалинг в UI-поток через `IUiDispatcher`
  (`AvaloniaUiDispatcher` в рантайме, `ImmediateDispatcher` в тестах — абстракция ради
  тестируемости).

## UI

Во вкладке «Устройства» два списка: «Доверенные» (из БД) и «Найдены в сети» (mDNS).

## Границы среза

- Показ найденных устройств — **есть** (VM с тестами; приложение проверено запуском).
- Пока нет: кнопка «Синхронизировать» по найденному устройству (нужен слушающий
  QUIC-сервис + подключение по адресу пира), «добавить в доверенные» из найденных,
  фактический порт синка (объявляем 47100, но ещё не слушаем).

## Тесты

- `DevicesViewModel` + фейковое `IDeviceDiscovery`: найденное устройство появляется,
  дубликат игнорируется, потерянное убирается, без discovery список пуст.
