# Фича: модель данных и персистентность (SQLite)

**Статус:** первый срез готов (39 тестов зелёные, всего в ядре) · **Слой:** `Porta.Core/Data`, `Porta.Core/Model`

Реализовано: `PortaDatabase` (WAL + foreign_keys), `Migrations` (001), модели
`TrustedDevice`/`Storage`/`StorageDeviceLink` + enum'ы, `DeviceRepository`,
`StorageRepository` (CRUD, привязка устройств, каскады, FK).

Доступ к данным — см. [ADR-0007](../adr/0007-data-access.md) (Microsoft.Data.Sqlite,
самодельные миграции).

## Зачем

Локальная БД метаданных устройства. Первый срез — фундамент персистентности и две
базовые сущности: **доверенные устройства** и **хранилища** (папки синхронизации).
Позже поверх лягут индекс файлов/блоков, история версий, индекс медиа.

## Схема (миграция 001)

### `devices` — доверенные (удалённые) устройства
| Поле | Тип | Описание |
|---|---|---|
| `device_id` | TEXT PK | Device ID (каноническая Base32-форма) |
| `public_key` | BLOB | Публичный ключ (SubjectPublicKeyInfo, DER) — для pinning |
| `name` | TEXT | Отображаемое имя (задаёт пользователь) |
| `added_at` | INTEGER | Когда добавлено (Unix-время, сек) |
| `last_seen_at` | INTEGER NULL | Когда последний раз были на связи |

> Локальное устройство здесь **не** хранится — его личность лежит в файле ключа
> (см. [01-identity](01-identity.md)); имя локального устройства — в `settings`.

### `storages` — хранилища (папки синхронизации)
| Поле | Тип | Описание |
|---|---|---|
| `storage_id` | TEXT PK | ID хранилища (общий на всех устройствах, где оно есть) |
| `name` | TEXT | Имя хранилища |
| `local_path` | TEXT | Путь на этом устройстве (может отличаться между устройствами) |
| `exchange_mode` | INTEGER | Режим обмена: 0=двусторонний, 1=только приём, 2=только отдача |
| `sync_mode` | INTEGER | 0=авто, 1=ручной |
| `paused` | INTEGER | 0/1 — приостановлено |
| `created_at` | INTEGER | Unix-время создания |

### `storage_devices` — связь «хранилище ↔ устройство»
| Поле | Тип | Описание |
|---|---|---|
| `storage_id` | TEXT | FK → `storages.storage_id` (ON DELETE CASCADE) |
| `device_id` | TEXT | FK → `devices.device_id` (ON DELETE CASCADE) |
| `auto_sync` | INTEGER | 0/1 — авто-синхро пара для этого хранилища |
| PK | (`storage_id`, `device_id`) | |

### `settings` — простое key-value
| Поле | Тип | Описание |
|---|---|---|
| `key` | TEXT PK | Имя настройки (напр. `local_device_name`, `downloads_path`) |
| `value` | TEXT | Значение |

## Компоненты (первый срез)

- `PortaDatabase` — открытие соединения по пути к файлу БД; применяет `PRAGMA`
  (WAL, foreign_keys) и прогоняет миграции.
- `Migrations` — упорядоченный список SQL-шагов; версия в `PRAGMA user_version`.
- Модели: `TrustedDevice`, `Storage`, `StorageExchangeMode`, `SyncMode`.
- Репозитории: `DeviceRepository` (add/get/list/remove/update-last-seen),
  `StorageRepository` (add/get/list/remove, привязка устройств).

## Границы среза

- Только структура + CRUD по устройствам и хранилищам. Индекс файлов/блоков, версии,
  медиа — отдельные будущие миграции и фичи.
- Многопоточный доступ: пока простые короткоживущие соединения; политику пула/локов
  уточним, когда появится фоновый индексатор.

## Тесты

- Миграции: свежая БД поднимается до актуальной версии; повторное открытие идемпотентно.
- `DeviceRepository`: add→get round-trip (включая BLOB ключа), list, remove, update
  last_seen; уникальность PK.
- `StorageRepository`: add→get, list, remove; привязка устройств и каскадное удаление;
  сохранение enum-полей.
- FK: нельзя привязать устройство к несуществующему хранилищу (при `foreign_keys=ON`).
