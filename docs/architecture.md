# Архитектура

Проект BYOND 2.0 представляет собой игровой движок с клиент-серверной архитектурой, построенный на .NET 8.0 с использованием C# и Lua для скриптинга.

## Структура проекта

Проект разделен на несколько ключевых компонентов:

*   **`Core`**: Библиотека классов, содержащая основную логику и общие компоненты, которые используются всеми остальными частями движка.
*   **`Server`**: Консольное приложение, которое запускает игровой сервер. Отвечает за управление игровым миром и обработку подключений.
*   **`Editor`**: Графическое приложение для создания и редактирования игровых карт, объектов и скриптов.
*   **`Client`**: Игровой клиент (в разработке).
*   **`scripts`**: Каталог со скриптами на Lua, которые определяют игровую логику.
*   **`tests`**: Проект с модульными тестами.

## Ключевые концепции

### Состояние игры (`GameState`)
Центральный класс, который хранит все текущее состояние игрового мира, включая карту (`Map`) и список всех игровых объектов (`GameObjects`).

### Объектная модель (`GameObject` и `ObjectType`)
Движок использует систему наследования для определения типов объектов.
*   **`ObjectType`**: Определяет шаблон для объектов, включая их имя, родительский тип и свойства по умолчанию.
*   **`GameObject`**: Представляет экземпляр объекта в игровом мире, имеющий свой уникальный `Id`, координаты и экземплярные свойства, которые могут переопределять свойства `ObjectType`.

### Скриптовый API (`GameApi`)
C# класс `GameApi` служит мостом между C# и Lua. Экземпляр этого класса предоставляется в Lua как глобальный объект `Game`, позволяя скриптам безопасно взаимодействовать с игровым миром.

## Диаграмма архитектуры

```mermaid
graph TD
    subgraph "Пользовательские Инструменты"
        Editor[<i class='fa fa-pencil-ruler'></i> Редактор]
    end

    subgraph "Игровые Приложения"
        Client[<i class='fa fa-gamepad'></i> Клиент]
        Server[<i class='fa fa-server'></i> Сервер]
    end

    subgraph "Ядро Движка (Core)"
        CoreLib[Core.dll]
        GameApi[GameApi]
        GameState[GameState]
        ObjectTypeManager[ObjectTypeManager]
        MapLoader[MapLoader]
    end

    subgraph "Скрипты и Данные"
        LuaScripts[<i class='fa fa-file-code'></i> Lua Скрипты]
        ProjectFiles[<i class='fa fa-folder-open'></i> Файлы проекта<br>(карты, типы)]
    end

    Editor -- "Использует" --> CoreLib
    Client -- "Использует" --> CoreLib
    Server -- "Использует" --> CoreLib

    CoreLib --> GameApi
    CoreLib --> GameState
    CoreLib --> ObjectTypeManager
    CoreLib --> MapLoader

    Server -- "Выполняет" --> LuaScripts
    LuaScripts -- "Вызывает" --> GameApi

    GameApi -- "Изменяет" --> GameState
    GameApi -- "Использует" --> ObjectTypeManager
    GameApi -- "Использует" --> MapLoader

    Editor -- "Загружает/Сохраняет" --> ProjectFiles
    MapLoader -- "Читает/Пишет" --> ProjectFiles
    ObjectTypeManager -- "Читает/Пишет" --> ProjectFiles

    style Editor fill:#D8BFD8,stroke:#333,stroke-width:2px
    style Client fill:#ADD8E6,stroke:#333,stroke-width:2px
    style Server fill:#90EE90,stroke:#333,stroke-width:2px
```
