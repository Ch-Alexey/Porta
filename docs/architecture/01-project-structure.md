# Структура решения

Итог Этапа 0. Решение — `Porta.slnx` в корне (новый XML-формат solution из .NET 10).

```
Porta/
├── Porta.slnx                    — solution (собираемые проекты)
├── Directory.Packages.props      — центральное управление версиями пакетов (CPM)
├── README.md
├── docs/                         — вся документация проекта
├── src/
│   ├── Porta.Core/               — ЯДРО: платформонезависимая логика (net10.0)
│   │                               identity, discovery, transport, indexer, sync,
│   │                               storage, media. Зависимость: MessagePack.
│   ├── Porta.App/                — общий UI на Avalonia (shared), ссылается на Core
│   ├── Porta.App.Desktop/        — голова: Windows/macOS/Linux desktop
│   ├── Porta.App.Android/        — голова: Android   (вне solution до workload'ов)
│   ├── Porta.App.iOS/            — голова: iOS       (вне solution до workload'ов)
│   └── Porta.App.Browser/        — голова: WebAssembly (вне solution до workload'ов)
└── tests/
    └── Porta.Core.Tests/         — xunit-тесты ядра
```

## Что в solution сейчас

Собираются без дополнительных SDK-workload'ов:
`Porta.Core`, `Porta.App` (shared), `Porta.App.Desktop`, `Porta.Core.Tests`.

**Мобильные и браузерная головы** (`Android`, `iOS`, `Browser`) сгенерированы и лежат
на диске, но **не добавлены в solution**, потому что требуют установки workload'ов
(`android`, `ios`, `wasm-tools`). Их подключим на этапе, когда займёмся этими
платформами. Ядро уже платформонезависимо, поэтому подключение голов не потребует
переделки логики.

## Central Package Management (CPM)

Версии всех NuGet-пакетов заданы централизованно в `Directory.Packages.props`.
В `.csproj` у `PackageReference` **не указывается** `Version` — только имя пакета.
Новую зависимость добавляем так: `PackageReference` в нужный `.csproj` + `PackageVersion`
в `Directory.Packages.props`.

## Команды

Сборка всего решения:

```bash
dotnet build Porta.slnx
```

Тесты:

```bash
dotnet test Porta.slnx
```

Запуск десктоп-приложения:

```bash
dotnet run --project src/Porta.App.Desktop
```

## Замечания

- **MessagePack:** используем версию 3.1.8 (в 3.1.4 были известные уязвимости, включая
  высокого уровня — не откатываться ниже 3.1.8 без проверки advisories).
- **Avalonia 12.1.1** — версия зафиксирована в CPM; головы держим на одной версии Avalonia.
