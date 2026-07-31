# Фича: Protocol — сессия поверх канала

**Статус:** первый срез готов (фрейминг + рукопожатие + доверие) · **Слой:** `Porta.Core/Protocol`

Готово: `MessageChannel` (4 юнит-теста), `HelloMessage`/`ProtocolVersion`, `ITrustPolicy`,
`PeerSession.EstablishAsync` (+ `UntrustedPeerException`/`ProtocolVersionException`).
Проверено поверх реального QUIC: обе стороны устанавливают сессию, недоверенный отклонён.

Опирается на транспорт ([06-transport](06-transport.md)) и формат MessagePack
([ADR-0004](../adr/0004-protocol-format.md)).

## Зачем

Транспорт даёт зашифрованный аутентифицированный канал и знает `RemoteDeviceId`, но:
1. сам по себе принимает соединение от любого устройства (доверие решает слой выше);
2. это «сырые байты» без границ сообщений.

Этот слой добавляет: **фрейминг сообщений**, **рукопожатие** (согласование версии
протокола + имя устройства) и **проверку доверия** (закрывает долг из транспорта —
соединение от недоверенного устройства отклоняется).

## Компоненты

### MessageChannel — фрейминг
Обёртка над `Stream`: сообщение = 4 байта длины (BE) + MessagePack-полезная нагрузка.
- `WriteAsync<T>` / `ReadAsync<T>`.
- Ограничение размера (`MaxMessageSize`) и `MessagePackSecurity.UntrustedData` — данные
  приходят от второй стороны.
- Чистая логика: тестируется на `MemoryStream`.

### Сообщения
- `HelloMessage(ProtocolVersion, DeviceName)` — первым делом при установлении сессии.
- `ProtocolVersion.Current` — версия протокола.

### ITrustPolicy — доверие
`bool IsTrusted(DeviceId)`. Позже реализуется поверх `DeviceRepository`; пока —
абстракция, чтобы сессия не зависела от хранилища.

### PeerSession — установление сессии
`EstablishAsync(connection, trust, localName, isInitiator)`:
1. **Проверка доверия:** если `RemoteDeviceId` не доверен → закрыть соединение,
   `UntrustedPeerException`. (Инициатор открывает контрольный поток, ответчик принимает.)
2. **Hello:** обе стороны шлют и читают `HelloMessage`; несовпадение версии →
   `ProtocolVersionException`.
3. Возвращает `PeerSession` с `RemoteDeviceId`, `RemoteDeviceName` и контрольным каналом.

## Границы среза

- Фрейминг + рукопожатие + проверка доверия — **есть**.
- Прикладные сообщения (обмен индексами хранилищ, запрос/передача блоков) — следующий
  срез, поверх `MessageChannel`.
- Мультиплексирование логических каналов, heartbeat/timeout, версионная совместимость
  «вперёд» — позже.

## Тесты

- `MessageChannel`: round-trip сообщения на `MemoryStream`; отказ на завышенной длине;
  отказ на неполном чтении.
- Интеграционные (поверх реального QUIC): обе стороны устанавливают сессию, обмениваются
  Hello (имена/версии); недоверенное устройство отклоняется (`UntrustedPeerException`).
