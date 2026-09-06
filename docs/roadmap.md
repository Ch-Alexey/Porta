# Дорожная карта Porta

Этапы упорядочены так, чтобы как можно раньше получить работающую передачу файлов
между двумя устройствами в локальной сети, а сложное (другие сети, медиа-поиск)
добавлять поверх готового ядра.

## Этап 0 — Каркас проекта
- [x] Структура решения: `Porta.Core`, `Porta.App` (Avalonia) + головы, `Porta.Core.Tests`. См. [project-structure](architecture/01-project-structure.md).
- [x] Сборка десктоп-головы + тесты (зелёные).
- [ ] Установить workloads и включить мобильные/браузерную головы в solution.
- [x] Базовый CI (GitHub Actions: сборка + юнит-тесты + проверка уязвимостей). `.github/workflows/ci.yml`.

## Этап 1 — Личность и хранилище (локально)
- [x] Генерация ключей устройства (ECDSA P-256), Device ID, персистентность. См. [features/01-identity](features/01-identity.md).
- [ ] Платформенное защищённое хранилище ключа (заменить файловое `FileDeviceIdentityStore`).
- [x] SQLite-хранилище метаданных, миграции. См. [features/02-data-model](features/02-data-model.md), [ADR-0007](adr/0007-data-access.md).
- [x] Сущности `Device`/`Storage` + репозитории (CRUD, связи, каскады).
- [x] Indexer: скан папки, метаданные, хеши, content-defined chunking. См. [features/03-indexer](features/03-indexer.md).

## Этап 2 — Обнаружение и связывание в LAN
- [x] Discovery по mDNS (Makaretu.Dns): чистое ядро + `Porta.Infrastructure`. См. [features/05-discovery](features/05-discovery.md).
- [x] Pairing: формат токена (QR/ссылка) + одноразовый код, доменно-разделённые подписи. См. [features/04-pairing](features/04-pairing.md).
- [x] Доверенные устройства, проверка ключей (логика; сетевой обмен — Этап 3).

## Этап 3 — Передача данных
- [x] Сертификат устройства из ключа + проверка pinning (`DeviceCertificate`). См. [features/06-transport](features/06-transport.md).
- [x] QUIC listener/connector, взаимная аутентификация по Device ID (pinning), обмен данными. Интеграционный тест зелёный.
- [x] Протокол: фрейминг сообщений (MessagePack), рукопожатие Hello, проверка доверия к RemoteDeviceId. См. [features/07-protocol](features/07-protocol.md).
- [x] Обмен индексами хранилищ + вычисление дельты (`IndexComparer`). См. [features/08-index-exchange](features/08-index-exchange.md).
- [x] Запрос и передача недостающих блоков, сборка файла с проверкой хешей. См. [features/09-block-transfer](features/09-block-transfer.md).
- [x] **Первая реальная синхронизация файла между двумя устройствами** (интеграционный тест поверх QUIC). ✅
- [ ] Запись версий при синхронизации — перенесено в Этап 4.

## Этап 4 — Умная синхронизация и история
- [x] Передача только изменившихся блоков (дельты) — `IndexComparer` + дедупликация.
- [x] Движок оркестрации синка хранилища (`SyncProtocol`, pull целого хранилища). См. [features/10-sync-engine](features/10-sync-engine.md).
- [ ] Двусторонний синк (оба узла сходятся) и потоковость (индекс/блоки не одним сообщением).
- [x] Сохранение предыдущих версий перед перезаписью файла (`IVersionStore`). См. [features/11-versioning](features/11-versioning.md).
- [x] Игнор-паттерны в скане/синке (`IgnoreRules`). Служебные папки не синхронизируются. См. [features/12-ignore-patterns](features/12-ignore-patterns.md).
- [ ] Политика хранения версий (сколько/сколько времени) и восстановление из UI.
- [x] Модель version vectors (сравнение/слияние/конфликт). См. [features/13-version-vectors](features/13-version-vectors.md).
- [x] Версионированный индекс (`IndexVersioning`) — инкремент версии при изменении. См. [features/14](features/14-versioned-index.md).
- [x] Решение о конфликте (`ConflictResolver`) + применение к файлам (`SyncApplier`, конфликт → обе версии). См. [15](features/15-conflict-resolution.md), [16](features/16-conflict-apply.md).
- [x] Обмен версионированными индексами + персистентность версий (SQLite) + разрешение конфликтов в синке. См. [features/17-versioned-sync](features/17-versioned-sync.md).
- [ ] Встречный проход (полная двусторонняя конвергенция за сеанс), удаления, сжатие векторов.

## Приложение (UI) — сквозной слой
- [x] Bootstrap `AppEnvironment` (личность + БД + репозитории) и запускаемое десктоп-приложение. См. [features/18-app-shell](features/18-app-shell.md).
- [x] Оболочка: Device ID, вкладки «Хранилища» (список + добавление) и «Устройства».
- [x] Абстракция данных (`IAppData`/интерфейсы репозиториев) → тестируемые VM (`Porta.App.Tests`) + design-time превью.
- [x] Связывание из UI по токену (показать приглашение / принять токен → доверенное устройство).
- [x] `SyncService` (оркестратор синка) + `DeviceRepositoryTrustPolicy` (доверие из БД). См. [features/19-sync-service](features/19-sync-service.md).
- [x] UI-кнопка синка: фоновый QUIC-приём + `QuicSyncController` + «Синхронизировать» по найденным. См. [features/21-sync-from-ui](features/21-sync-from-ui.md).
- [x] Авто-синхронизация: при появлении доверенного устройства синк запускается автоматически (`AutoSyncCoordinator`). См. [features/22-auto-sync](features/22-auto-sync.md).
- [x] Синк по локальным изменениям файлов (`FileSystemChangeNotifier` → авто-синк). См. [features/23-file-change-sync](features/23-file-change-sync.md).
- [x] Выборочный доступ: связывание хранилищ↔устройств в UI, синк только расшаренных (`StorageSharing`). См. [features/24-storage-sharing](features/24-storage-sharing.md).
- [ ] Рендер QR + сканирование камерой; диалог выбора папки.
- [x] Разовая передача: протокол ядра (намерение сессии, потоковая передача, подтверждение приёма). См. [features/26-drop-transfer](features/26-drop-transfer.md).
- [x] Разовая передача: UI (вкладка «Передача», подтверждение приёма, папка «Загрузки»). См. [features/28-drop-ui](features/28-drop-ui.md).
- [x] **Пакетная отдача блоков + приём на диск + таймаут чтения** — закрыт потолок 16 МБ и вечное зависание. См. [features/27-block-streaming](features/27-block-streaming.md).
- [x] Discovery-устройства в UI (mDNS): приложение объявляет себя и показывает найденные в сети. См. [features/20-discovery-ui](features/20-discovery-ui.md).
- [x] Вкладка «Медиа» (срез 1: поиск). См. [features/25-media-scan](features/25-media-scan.md).

## Этап 5 — Вкладка «Медиа»
- [x] Сканер фото/видео по устройству + фильтры (тип, имя, дата, размер) + вкладка «Медиа». См. [features/25-media-scan](features/25-media-scan.md).
- [ ] EXIF: дата съёмки, ориентация, GPS (поле `MediaFile.CapturedAt` уже заложено).
- [ ] Превью и сетка миниатюр вместо списка.
- [ ] Действия над найденным: отправить разово / добавить в хранилище.
- [ ] Доступ к галерее на мобильных с разрешениями.

## Этап 6 — За пределы локальной сети (позже)
- [ ] Работа через интернет: проброс/hole punching, возможно relay.
- [ ] Более строгая модель безопасности для недоверенных сетей.

## Принципы приоритизации
1. Ядро раньше UI — логику можно тестировать без интерфейса.
2. Сначала «работает на двух устройствах в LAN», потом красота и охват.
3. Каждая фича получает свой файл контекста в [`features/`](features/) до начала реализации.
