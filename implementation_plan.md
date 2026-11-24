# 📐 Архитектурный план реализации (implementation_plan.md) — Elcho's Pizza

Стандарт: **Google DeepMind Agentic Blueprint (master_project_blueprint.html)**  
Система: **Комплексная автоматизация ресторанного бизнеса и сервиса экспресс-доставки Elcho's Pizza**

---

## 1. Концептуальная архитектура и стратификация слоев

Система построена по принципу **Clean Three-Tier Architecture** с реактивной событийно-ориентированной шиной реального времени:

```mermaid
flowchart TD
    subgraph Presentation ["Уровень представления (Presentation Layer)"]
        UI_Web["Клиентский сайт (index.html)"]
        UI_POS["Кассовый POS-терминал (pos.html)"]
        UI_KDS["Кухонный монитор KDS (kitchen.html)"]
        UI_Board["Электронное табло очереди (board.html)"]
        UI_Kiosk["Киоск самообслуживания (kiosk.html)"]
        UI_Courier["Мобильный экран курьера (courier.html)"]
        UI_Admin["Панель управления ERP/WMS (admin.html)"]
    end

    subgraph Launchers ["Десктопные лаунчеры (Desktop Shell)"]
        L_Apps["C# LauncherApp.exe (--app kiosk-mode)"]
    end

    subgraph Application ["Серверное ядро (Application & Domain Layer)"]
        Server_Core["HttpServer.cs (Асинхронный TCP HTTP/1.1 сокет-сервер)"]
        Dispatcher["ApiHandler.cs (REST API Router & Controllers)"]
        EventBus["EventBus.cs (Server-Sent Events / SSE Broker)"]
        WMS_Engine["WMS Engine (Калькулятор списания ингредиентов по граммам)"]
        Fiscal_Engine["Fiscal Core (Модуль кассовых смен и чеков по 54-ФЗ)"]
        Auth_RBAC["RBAC Security Guard (Admin, Cashier, Chef, Courier, Client)"]
    end

    subgraph Data ["Уровень хранения данных (Data Layer)"]
        DB_Wrapper["Database.cs (P/Invoke обертка над winsqlite3.dll)"]
        DB_File[("pizza.db (SQLite Реляционная база, WAL Mode)")]
        DB_WAL[("pizza.db-wal (Журнал опережающей записи)")]
    end

    UI_Web --> Server_Core
    UI_POS --> Server_Core
    UI_KDS --> Server_Core
    UI_Board --> Server_Core
    UI_Kiosk --> Server_Core
    UI_Courier --> Server_Core
    UI_Admin --> Server_Core

    L_Apps -.-> UI_POS
    L_Apps -.-> UI_KDS
    L_Apps -.-> UI_Board
    L_Apps -.-> UI_Kiosk
    L_Apps -.-> UI_Admin

    Server_Core --> Dispatcher
    Dispatcher --> Auth_RBAC
    Dispatcher --> WMS_Engine
    Dispatcher --> Fiscal_Engine
    Dispatcher --> EventBus

    EventBus -.->|SSE Real-time Push| UI_KDS
    EventBus -.->|SSE Real-time Push| UI_Board
    EventBus -.->|SSE Real-time Push| UI_Web
    EventBus -.->|SSE Real-time Push| UI_POS

    WMS_Engine --> DB_Wrapper
    Fiscal_Engine --> DB_Wrapper
    Dispatcher --> DB_Wrapper

    DB_Wrapper --> DB_File
    DB_Wrapper --> DB_WAL
```

---

## 2. Схема базы данных (Entity-Relationship Diagram)

Схема нормализована до 3NF и полностью отражает бизнес-логику пиццерии, складского учета (WMS) и фискальной кассовой дисциплины (54-ФЗ):

```mermaid
erDiagram
    USERS ||--o{ ORDERS : "оформляет"
    USERS ||--o{ CASH_SHIFTS : "открывает/закрывает"
    USERS {
        int id PK
        string email UK
        string passwordHash
        string fullName
        string phone
        string role "admin|cashier|chef|courier|client"
        datetime createdAt
    }

    CATEGORIES ||--o{ PRODUCTS : "содержит"
    CATEGORIES {
        int id PK
        string name
        int sortOrder
        string icon
    }

    PRODUCTS ||--o{ PRODUCT_RECIPES : "имеет техкарту"
    PRODUCTS ||--o{ ORDER_ITEMS : "включается в"
    PRODUCTS {
        int id PK
        int categoryId FK
        string name
        string description
        double basePrice
        string imageUrl
        bool isAvailable
        bool isStopList
    }

    INVENTORY ||--o{ PRODUCT_RECIPES : "расходуется по"
    INVENTORY {
        int id PK
        string ingredientName
        string unit "г|кг|мл|шт"
        double currentStock
        double minStock
        double costPerUnit
    }

    PRODUCT_RECIPES {
        int id PK
        int productId FK
        int ingredientId FK
        string sizeName "25 см|30 см|35 см"
        double requiredQuantity
    }

    ORDERS ||--o{ ORDER_ITEMS : "состоит из"
    ORDERS ||--o| FISCAL_RECEIPTS : "фискализируется"
    ORDERS {
        int id PK
        string orderNumber UK
        int userId FK
        string customerName
        string customerPhone
        string deliveryAddress
        string status "New|Cooking|Oven|Ready|OnTheWay|Delivered|Cancelled"
        string paymentMethod "Cash|Card|QR|CardOnline"
        double totalAmount
        double discountAmount
        string promoCode
        datetime createdAt
    }

    ORDER_ITEMS {
        int id PK
        int orderId FK
        int productId FK
        string productName
        string sizeName
        string doughName
        string selectedToppings
        double unitPrice
        int quantity
    }

    CASH_SHIFTS ||--o{ FISCAL_RECEIPTS : "регистрирует"
    CASH_SHIFTS {
        int id PK
        int cashierId FK
        datetime openedAt
        datetime closedAt
        double initialCash
        double cashTotal
        double cardTotal
        double sbpTotal
        double totalRevenue
        string status "Open|Closed"
        string zReportNumber
    }

    FISCAL_RECEIPTS {
        int id PK
        int shiftId FK
        int orderId FK
        string receiptNumber
        string fiscalSign
        double totalAmount
        double vat20Amount
        string paymentType
        datetime createdAt
    }
```

---

## 3. Сквозной поток данных (Data Flow Lifecycle)

```mermaid
sequenceDiagram
    autonumber
    actor Guest as Клиент / Киоск
    participant API as ApiHandler.cs
    participant WMS as WMS Inventory
    participant DB as SQLite (pizza.db WAL)
    participant Bus as EventBus (SSE)
    actor Chef as Повар (KDS)
    actor Hall as Табло зала (Board)
    actor Courier as Курьер (PWA)

    Guest->>API: POST /api/orders (Состав, размер 35 см, бортик, ELCHOS20)
    activate API
    API->>WMS: Расчет списания граммов сырья по рецептуре
    WMS->>DB: UPDATE Inventory SET currentStock = currentStock - requiredQty
    API->>DB: INSERT INTO Orders, OrderItems (Status = 'New')
    API->>Bus: Broadcast Event "NEW_ORDER" (#1042)
    API-->>Guest: 200 OK (orderId: 42, orderNumber: "#1042")
    deactivate API

    Bus-->>Chef: Push-уведомление на экран кухни KDS + Звуковой сигнал
    Chef->>API: POST /api/orders/42/status {"status": "Cooking"}
    API->>Bus: Broadcast Event "STATUS_CHANGED" (Cooking)
    Bus-->>Guest: Трекер заказа: "Повар собирает вашу пиццу"

    Chef->>API: POST /api/orders/42/status {"status": "Oven"}
    API->>Bus: Broadcast Event "STATUS_CHANGED" (Oven)
    Bus-->>Guest: Трекер заказа: "Выпекается в печи при 300°C"

    Chef->>API: POST /api/orders/42/status {"status": "Ready"}
    API->>Bus: Broadcast Event "STATUS_CHANGED" (Ready)
    Bus-->>Hall: Номер #1042 в колонку «Готовы к выдаче» + ГОНГ
    Bus-->>Courier: Заказ доступен для доставки

    Courier->>API: POST /api/orders/42/status {"status": "OnTheWay"}
    Bus-->>Guest: Трекер заказа: "Курьер в пути к вам"

    Courier->>API: POST /api/orders/42/status {"status": "Delivered"}
    API->>DB: UPDATE Orders SET status = 'Delivered'
    Bus-->>Guest: Трекер: "Заказ доставлен! Оцените качество"
```

---

## 4. Карта модулей системы

### Серверная часть (`project/src/`):
1. **`HttpServer.cs`**: Многопоточный сокетный HTTP/1.1 сервер, обработчик MIME-типов, парсер потоков и заголовков безопасности.
2. **`ApiHandler.cs`**: Контроллеры аутентификации, каталога меню, заказов, WMS-склада, кассовых смен 54-ФЗ, отзывов и аналитики.
3. **`EventBus.cs`**: Брокер Server-Sent Events (SSE) с потокобезопасной коллекцией подписчиков.
4. **`Database.cs`**: Низкоуровневый C# клиент к `winsqlite3.dll` с подготовленными выражениями (`sqlite3_bind_*`).
5. **`DbInitializer.cs`**: Инициализатор базы данных, структуры таблиц и начальных фикстур (44 товара, рецептуры, пользователи).
6. **`TestSuite.cs`**: Комплекс из 40 сквозных интеграционных тестов (`Тест_Системы.exe`).
7. **`LauncherApp.cs`**: Загрузчик рабочих мест Windows в Kiosk-режиме (`--app`).

### Клиентская часть (`project/wwwroot/`):
1. **`index.html`** + **`app.js`**: Витрина онлайн-заказа, конструктор пиццы, корзина, промокоды и онлайн-трекер.
2. **`pos.html`**: Сенсорный кассовый терминал, смены по 54-ФЗ, печать чеков.
3. **`kitchen.html`**: Экран повара KDS со стадиями и интерактивными техкартами.
4. **`board.html`**: Табло очереди зала с акустическим гонгом Web Audio.
5. **`kiosk.html`**: Киоск самообслуживания гостей торгового зала.
6. **`courier.html`**: Мобильный экран службы доставки.
7. **`admin.html`**: Панель управления, склад WMS, стоп-листы, аналитика выручки и журнал аудита.
8. **`css/styles.css`**: Единая дизайн-система, цветовая палитра HSL, адаптивность и скелетоны загрузки.
