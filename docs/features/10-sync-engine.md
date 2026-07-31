# Фича: движок синхронизации (оркестрация)

**Статус:** первый срез готов (pull целого хранилища) · **Слой:** `Porta.Core/Sync`, `Porta.Core/Protocol`

Готово: `FolderIndexRequest`, `SyncProtocol.ServeAsync`/`PullAsync`, `SyncResult`.
3 функциональных теста in-memory (связанные каналы, без QUIC) + интеграционный поверх
QUIC. «Ручная» оркестрация из тестов [09] вынесена в переиспользуемый движок.

Превращает «ручную» цепочку из [09](09-block-transfer.md) в переиспользуемый диалог для
целого хранилища. Работает поверх `MessageChannel` ([07](07-protocol.md)), поэтому
тестируется на связанных in-memory каналах — без транспорта.

## Протокол «pull» (одна сторона тянет у другой)

```
Receiver → Sender : FolderIndexRequest(storageId)
Sender   → Receiver: FolderIndexMessage(storageId, entries)      // скан папки
Receiver          : diff = IndexComparer(local, remote)
Receiver → Sender : BlockRequestMessage(missing hashes)
Sender   → Receiver: BlockResponseMessage(blocks)                // из FolderBlockReader
Receiver          : собрать все FilesToUpdate (FileAssembler)
```

## API

`SyncProtocol`:
- `ServeAsync(channel, folder, storageId)` — отдающая сторона: отвечает на запрос индекса
  и на запрос блоков.
- `PullAsync(channel, folder, storageId) → SyncResult` — принимающая: ведёт весь диалог
  и собирает файлы. `SyncResult(FilesUpdated, BlocksReceived, BytesReceived)`.

Обе стороны запускаются одновременно на двух концах канала.

## Границы среза

- Односторонний pull целого хранилища за один проход — **с тестами** (in-memory + QUIC).
- Двусторонний синк (оба тянут и сходятся), version vectors/конфликты, версионирование
  при перезаписи — следующие срезы Этапа 4.
- Потоковость (индекс/блоки не одним сообщением), прогресс, отмена на середине,
  параллельные запросы — позже.

## Тесты

- In-memory (связанные каналы, без QUIC): синк нескольких файлов во вложенных папках →
  у принимающего появляются идентичные файлы; уже совпадающие файлы пропускаются;
  `SyncResult` отражает число обновлённых.
- Интеграционно поверх QUIC: `SyncProtocol` синхронизирует хранилище между двумя узлами.
