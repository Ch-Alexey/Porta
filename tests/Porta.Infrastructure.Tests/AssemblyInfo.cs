// Интеграционные тесты используют QUIC (порты) и файловый SQLite с глобальным
// ClearAllPools() — параллельный запуск создаёт гонки. Выполняем последовательно.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
