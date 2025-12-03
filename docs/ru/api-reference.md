# Справочник по API

Этот документ представляет собой подробный справочник по API для написания скриптов, доступному в BYOND 2.0. Доступ к API осуществляется через объект `GameApi`, который доступен глобально в Lua и передается в качестве аргумента в точку входа в C#-скриптах.

## IGameApi

Корневой интерфейс `IGameApi` предоставляет доступ ко всем вложенным API.

*   `Map`: Доступ к `IMapApi`.
*   `Objects`: Доступ к `IObjectApi`.
*   `Scripts`: Доступ к `IScriptApi`.
*   `StdLib`: Доступ к `IStandardLibraryApi`.

---

## IMapApi

`IMapApi` предоставляет функции для взаимодействия с игровой картой.

*   **`GetMap()`**: Возвращает текущий объект `Map`.
*   **`GetTurf(int x, int y, int z)`**: Возвращает `Turf` по указанным координатам.
*   **`SetTurf(int x, int y, int z, int turfId)`**: Устанавливает тайл по указанным координатам в новый ID тайла.
*   **`LoadMapAsync(string filePath)`**: Асинхронно загружает карту из файла `.dmm`.
*   **`SetMap(Map map)`**: Заменяет текущую карту новым объектом `Map`.
*   **`SaveMapAsync(string filePath)`**: Асинхронно сохраняет текущую карту в файл `.dmm`.

---

## IObjectApi

`IObjectApi` предоставляет функции для создания, уничтожения и управления игровыми объектами.

*   **`CreateObject(string typeName, int x, int y, int z)`**: Создает новый `GameObject` указанного типа по заданным координатам.
*   **`GetObject(int id)`**: Возвращает `GameObject` с указанным ID.
*   **`DestroyObject(int id)`**: Уничтожает `GameObject` с указанным ID.
*   **`MoveObject(int id, int x, int y, int z)`**: Перемещает `GameObject` с указанным ID в новые координаты.

---

## IScriptApi

`IScriptApi` предоставляет функции для взаимодействия с файлами скриптов.

*   **`ListScriptFiles()`**: Возвращает список имен всех файлов скриптов.
*   **`ScriptFileExists(string filename)`**: Проверяет, существует ли файл скрипта.
*   **`ReadScriptFile(string filename)`**: Читает содержимое файла скрипта.
*   **`WriteScriptFile(string filename, string content)`**: Записывает содержимое в файл скрипта.
*   **`DeleteScriptFile(string filename)`**: Удаляет файл скрипта.

---

## IStandardLibraryApi

`IStandardLibraryApi` предоставляет реализации общих функций стандартной библиотеки DM.

*   **`Locate(string typePath, List<GameObject> container)`**: Находит объект определенного типа в списке игровых объектов.
*   **`Sleep(int milliseconds)`**: Приостанавливает выполнение текущего скрипта на указанное время.
*   **`Range(int distance, int centerX, int centerY, int centerZ)`**: Возвращает список `GameObject` в пределах определенного диапазона от центральной точки.
*   **`View(int distance, GameObject viewer)`**: Возвращает список `GameObject`, видимых для определенного объекта-наблюдателя.
