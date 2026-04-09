# Реализация требований: профилирование GraphQL в Hot Chocolate

Документ фиксирует, какая работа была выполнена по каждому пункту и подпункту требований, и где именно это реализовано в коде.

## 1. Сбор метрик выполнения

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 1.1 Измерение времени каждого resolver (sync/async) | Добавлен замер времени на `ResolveFieldValue` через диагностический listener; время пишется в коллектор на каждый field-path независимо от sync/async резолвера. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDiagnosticEventListener.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs`<br>`HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |
| 1.2 Глубина дерева выполнения | Для каждого пути вычисляется depth по `Path` сегментам и сохраняется в профиле поля. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 1.3 Количество вызовов DataLoader по полям | Через listener DataLoader событий собираются batch-вызовы и раскладываются в метрики по текущему path поля. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDataLoaderDiagnosticEventListener.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerScopeContext.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 1.4 Cache hits/misses | Cache hit фиксируется отдельным событием; misses учитываются по размеру batch-ключей. Есть и request-level, и field-level счетчики. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDataLoaderDiagnosticEventListener.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 1.5 Время сериализации результата по типам | Добавлен замер времени сериализации leaf-значений вокруг `CoerceOutputValue`, агрегирование по имени GraphQL-типа (`serializationByType`). | `HotChocolate/Core/src/Types/Execution/Processing/ValueCompletion.Leaf.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs`<br>`HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |

## 2. Выявление N+1 проблем

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 2.1 Автоматическое обнаружение N+1 без batching | Реализован анализ повторяющихся list-path паттернов; issue поднимается, если повторяемость выше порога и нет признаков DataLoader batching/cache hit. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 2.2 Анализ паттернов повторяющихся запросов | Путь нормализуется (`users[0].x` -> `users[].x`), после чего ведется подсчет occurrences по паттерну. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 2.3 Предупреждения с указанием проблемных полей | В extension добавляется секция `nPlusOne` c `issueCount` и списком issues (`pathPattern`, `examplePath`, `message`). | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 2.4 Рекомендации по DataLoader | Для каждой N+1 issue формируется рекомендация использовать DataLoader batching. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |

## 3. Статистика и агрегация

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 3.1 min/max/avg | В sliding-window aggregator добавлен аккумулятор длительностей с расчетом min/max/avg. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerSlidingWindowAggregator.cs` |
| 3.2 p50/p95/p99 | Реализован расчет перцентилей на упорядоченных сэмплах (`p50Ns`, `p95Ns`, `p99Ns`). | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerSlidingWindowAggregator.cs` |
| 3.3 Агрегация по типам полей и объектам | Добавлены группировки `byField` (coordinate/objectType/fieldName) и `byObjectType`. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerSlidingWindowAggregator.cs` |
| 3.4 Скользящее окно (N запросов/интервал) | Сэмплы хранятся в очереди и обрезаются по `SlidingWindowMaxRequests` и `SlidingWindowDuration`. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerSlidingWindowAggregator.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOptions.cs` |
| 3.5 Группировка по операциям (query/mutation/subscription) | Сохраняется `operationType`/`operationName`, формируются `byOperationType` и `byOperation`. | `HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerRequestSample.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerSlidingWindowAggregator.cs` |

## 4. Интеграция с конвейером выполнения

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 4.1 Middleware в execution pipeline | Добавлен `ExecutionProfilerMiddleware`, который создает collector, завершает профилирование, пишет extension/агрегаты/экспорт. | `HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs`<br>`HotChocolate/Core/src/Types/Execution/DependencyInjection/RequestExecutorBuilderExtensions.Profiling.cs` |
| 4.2 Использование диагностики (`IExecutionDiagnosticEvents`) | Подключен диагностический listener уровня выполнения поля (`ExecutionDiagnosticEventListener`) + listener DataLoader событий. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDiagnosticEventListener.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDataLoaderDiagnosticEventListener.cs` |
| 4.3 OpenTelemetry экспорт | Реализован экспорт метрик через `Meter` (counters/histograms) и trace-интеграция через `ActivitySource`. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOpenTelemetryExporter.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerTelemetry.cs` |
| 4.4 Минимальный overhead (<5%) | Добавлены fast-path ветки при отключенном профайлере/фильтрах; реализован benchmark A/B (`off` vs `on`) для проверки накладных расходов. | `HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs`<br>`HotChocolate/Core/benchmarks/Execution.Profiling.Benchmarks/ExecutionProfilerOverheadBenchmark.cs` |
| 4.5 Динамическое включение/отключение | Реализовано runtime-state переключение (`IExecutionProfilerState`) и request-level overrides через `OperationRequestBuilder`/`RequestContext`. | `HotChocolate/Core/src/Types/Execution/Profiling/IExecutionProfilerState.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerState.cs`<br>`HotChocolate/Core/src/Execution.Abstractions/Execution/Features/ExecutionProfilerRequestOverrides.cs`<br>`HotChocolate/Core/src/Execution.Abstractions/Execution/Features/ExecutionProfilerRequestOverridesExtensions.cs`<br>`HotChocolate/Core/src/Types/Execution/Extensions/ExecutionProfilerRequestContextExtensions.cs` |

## 5. Конфигурация

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 5.1 Пороговые значения алертов | Добавлены и применяются пороги: `SlowRequestThreshold`, `SlowRequestFieldLimit`, `NPlusOneListPatternThreshold`, `SlowFieldThreshold`. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOptions.cs`<br>`HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 5.2 Исключение типов/полей | Реализованы `ExcludedObjectTypes`, `ExcludedFieldCoordinates`, `ExcludedFieldNames` с проверкой на этапе записи field-метрик. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOptions.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 5.3 Уровни детализации | Поддержаны `SlowFields`, `Full`, `NPlusOneOnly` с разной формой профиля и фильтрацией полей по `SlowFieldThreshold`. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerDetailLevel.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs` |
| 5.4 IConfiguration + fluent API | Поддержано конфигурирование через `IConfiguration` и через fluent (`AddExecutionProfiler`, `ModifyExecutionProfilerOptions`). | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerServiceCollectionExtensions.cs`<br>`HotChocolate/Core/src/Types/Execution/DependencyInjection/RequestExecutorBuilderExtensions.Profiling.cs` |
| 5.5 Фильтрация по пути запроса | Реализованы path-prefix фильтры (`IncludedPathPrefixes`) и нормализация индексных путей для matching. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOptions.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs`<br>`HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs` |

## 6. Экспорт результатов

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 6.1 Экспорт профиля в JSON | Профиль формируется как словарь и добавляется в GraphQL `extensions.profiling` (далее сериализуется в JSON стандартным ответом GraphQL). | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfileCollector.cs`<br>`HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs` |
| 6.2 Интеграция с внешними APM | Реализованы OpenTelemetry metrics и tracing; через OTel exporter-пайплайн поддерживаются Jaeger/Zipkin/Application Insights. | `HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOpenTelemetryExporter.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerTelemetry.cs` |
| 6.3 Endpoint текущей статистики (dev only) | Добавлен `MapGraphQLProfiler` endpoint (`/graphql/profiler`), который отдает snapshot статистики и отключается вне Development (`404`). | `HotChocolate/AspNetCore/src/AspNetCore/Extensions/HotChocolateAspNetCoreEndpointRouteBuilderExtensions.ExecutionProfiler.cs`<br>`HotChocolate/Core/src/Types/Execution/Extensions/ExecutionProfilerRequestContextExtensions.cs` |
| 6.4 Логирование медленных запросов | В middleware добавлено warning-логирование медленных запросов c operation info и топом медленных полей. | `HotChocolate/Core/src/Types/Execution/Pipeline/ExecutionProfilerMiddleware.cs`<br>`HotChocolate/Core/src/Types/Execution/Profiling/ExecutionProfilerOptions.cs` |

## 7. Тестирование

| Подпункт | Что сделано (в общих чертах) | Ключевые файлы |
|---|---|---|
| 7.1 Unit-тесты компонентов профайлера | Добавлены/расширены тесты на опции, конфигурацию, агрегатор, формирование профиля и сервисную регистрацию. | `HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |
| 7.2 Интеграционные тесты N+1 | Есть интеграционные сценарии с list-полями и проверкой `nPlusOne.issueCount/issues`. | `HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |
| 7.3 Benchmark tests | Добавлен benchmark `ExecutionProfilerOverheadBenchmark` (A/B off vs on) на BenchmarkDotNet. | `HotChocolate/Core/benchmarks/Execution.Profiling.Benchmarks/ExecutionProfilerOverheadBenchmark.cs`<br>`HotChocolate/Core/benchmarks/Execution.Profiling.Benchmarks/Program.cs` |
| 7.4 Поведение при высокой нагрузке | Есть concurrent-тест на пачку параллельных запросов и проверку целостности профиля/агрегатов. | `HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |
| 7.5 Edge cases (исключения, отмена) | Добавлены проверки поведения при исключении резолвера и `OperationCanceledException` с сохранением корректного profiling extension. | `HotChocolate/Core/test/Execution.Tests/DependencyInjection/RequestExecutorBuilderExtensions_ExecutionProfiler.Tests.cs` |

## Ключевой итог по ожидаемому результату

- Профайлер встроен в execution pipeline Hot Chocolate и доступен через расширения для `IServiceCollection` и `IRequestExecutorBuilder`.
- Профиль доступен в `extensions.profiling`, поддерживает N+1 анализ, агрегаты, фильтрацию и уровни детализации.
- Реализована интеграция с OpenTelemetry (метрики + tracing), dev endpoint статистики и slow-query логирование.
- Есть тестовое покрытие (unit/integration/concurrency/edge cases) и benchmark-проверка накладных расходов.
