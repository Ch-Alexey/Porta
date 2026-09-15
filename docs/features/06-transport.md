# Фича: Transport — защищённый канал (QUIC)

**Статус:** срез 1 (сертификат) + срез 2 (QUIC-канал) готовы · **Слой:**
`Porta.Core/Transport` (сертификат, абстракция), `Porta.Infrastructure` (QUIC)

Готово: `DeviceCertificate` (pinning), `ITransport`/`IPeerConnection`/`ITransportListener`,
`QuicTransport` (взаимная аутентификация по Device ID, обмен данными). 80 юнит-тестов +
3 интеграционных (mDNS, QUIC-обмен, отклонение неверного pin).

Транспорт — [ADR-0006](../adr/0006-transport-quic.md) (QUIC + TLS 1.3, доверие по pinning).

## Зачем

Дать доверенным устройствам зашифрованный канал. QUIC несёт встроенный TLS 1.3;
доверие — не по цепочке CA, а по **закреплённому ключу** устройства (Device ID).

## Срез 1: сертификат устройства и проверка pinning (этот документ)

Это фундамент, и он чисто-тестируемый. Закрывает два долга предыдущих срезов:
- **Identity:** сгенерировать TLS-сертификат из ключа устройства.
- **Pairing:** сеть обязана доказать, что владеет закреплённым ключом — проверкой
  предъявленного сертификата.

### Сертификат

`DeviceIdentity.CreateSelfSignedCertificate(notBefore, notAfter)` строит самоподписанный
X.509 из ключа устройства (ECDSA P-256 — выбран в Identity именно ради совместимости с
TLS 1.3, см. [01-identity](01-identity.md)). Subject — `CN=<DeviceId>`; расширения:
BasicConstraints (не CA), KeyUsage (DigitalSignature).

Ключевое свойство: публичный ключ сертификата **тот же**, что у устройства, поэтому
`DeviceId.FromPublicKey(cert.SPKI) == устройство.Id`. Значит сертификат сам по себе
привязан к Device ID — отдельного «имени» доверять не нужно.

### Проверка (pinning)

`DeviceCertificate`:
- `FromCertificate(cert)` → `DeviceId` (хеш публичного ключа сертификата).
- `Matches(cert, expectedDeviceId)` → совпадает ли предъявленный сертификат с
  закреплённым Device ID.

Это и есть обязательная сетевая проверка: при QUIC-хендшейке принимаем соединение,
только если `Matches(peerCert, доверенный Device ID)`.

## Срез 2: QUIC-транспорт (готово, интеграционный тест)

`ITransport` / `IPeerConnection` / `ITransportListener` в ядре (без зависимости от QUIC);
реализация `QuicTransport` в `Porta.Infrastructure` на `System.Net.Quic`:

- ALPN `porta`, TLS 1.3 внутри QUIC.
- Обе стороны предъявляют сертификат устройства; **клиент** проверяет сервер по
  закреплённому Device ID (pinning в `RemoteCertificateValidationCallback`); **сервер**
  требует клиентский сертификат и вычисляет `RemoteDeviceId` из него (решение о доверии —
  слой выше).
- PFX round-trip сертификата — чтобы приватный ключ был пригоден для TLS-стека.
- `QuicTransport.IsSupported` — гейт по наличию libmsquic.
- QUIC-классы помечены `[SupportedOSPlatform]` (linux/macos/windows).

**Требование окружения:** QUIC требует libmsquic. На macOS (Homebrew) запуск с
`DYLD_LIBRARY_PATH=/opt/homebrew/opt/libmsquic/lib`. Без библиотеки QUIC-тесты тихо
пропускаются.

## Границы среза

- Установление аутентифицированного соединения + обмен байтами по потоку — **есть**
  (интеграционный тест: взаимный pinning, обмен данными, отклонение неверного pin).
- Протокол поверх канала (обмен индексами, запрос/передача блоков) — следующий срез.
- Сервер принимает любой корректный сертификат и отдаёт `RemoteDeviceId`; сверку с
  доверенными устройствами делает слой выше — пока не подключено.
- Ротация/срок сертификата, множественные потоки, backpressure — позже.

## Тесты (срез 1)

- Сгенерированный сертификат содержит приватный ключ; CN содержит Device ID.
- Публичный ключ сертификата (SPKI) равен публичному ключу устройства.
- `DeviceId.FromPublicKey(cert)` равен `identity.Id` (привязка).
- `DeviceCertificate.Matches` = true для своего Device ID, false для чужого.
- Даты действия сертификата выставлены как заданы.
