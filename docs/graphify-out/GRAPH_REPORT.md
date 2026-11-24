# Graph Report - gemini  (2026-11-21)

## Corpus Check
- 14 files · ~55,114 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 31 file(s) not represented in the graph (top: .exe 26, .db 1, .db-shm 1)
- WARNING: 31 of 110 files seen (28%) have no supported extractor and are missing from this graph entirely. It may look complete while representing only part of the codebase.

## Summary
- 221 nodes · 595 edges · 14 communities (10 shown, 4 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 62 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- ApiHandler
- SQLiteDatabase
- HttpServer
- HttpServer.cs
- app.js
- showToast
- initEventSource
- saveCart
- Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)
- Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)
- LauncherApp
- loadUserOrderHistory
- rbac.js

## God Nodes (most connected - your core abstractions)
1. `ApiHandler` - 46 edges
2. `SQLiteDatabase` - 35 edges
3. `SimpleHttpRequest` - 20 edges
4. `showToast()` - 17 edges
5. `HttpServer` - 15 edges
6. `playChime()` - 12 edges
7. `EventBus` - 10 edges
8. `closeModal()` - 9 edges
9. `setActiveOrder()` - 7 edges
10. `initEventSource()` - 7 edges

## Surprising Connections (you probably didn't know these)
- `ApiHandler` --references--> `SQLiteDatabase`  [EXTRACTED]
  project/src/ApiHandler.cs → project/src/Database.cs
- `HttpServer` --references--> `ApiHandler`  [EXTRACTED]
  project/src/HttpServer.cs → project/src/ApiHandler.cs

## Import Cycles
- None detected.

## Communities (14 total, 4 thin omitted)

### Community 0 - "ApiHandler"
Cohesion: 0.13
Nodes (8): ApiHandler, SimpleHttpRequest, Body, Headers, Method, Path, Query, RawUrl

### Community 2 - "HttpServer"
Cohesion: 0.11
Nodes (4): EventBus, HttpServer, Program, Program

### Community 3 - "HttpServer.cs"
Cohesion: 0.18
Nodes (8): PizzaSystem.Realtime, PizzaSystem, PizzaSystem.Api, PizzaSystem.Data, PizzaSystem.Launchers, PizzaSystem.Tests, PizzaSystem.Server, DbInitializer

### Community 4 - "app.js"
Cohesion: 0.12
Nodes (18): audioCtx, calculateCustomPrice(), createToastContainer(), loadReviews(), openCheckoutModal(), openPizzaCustomizer(), openReviewModalDirect(), openReviewModalFromTracker() (+10 more)

### Community 5 - "showToast"
Cohesion: 0.36
Nodes (13): addCustomizedPizzaToCart(), closeModal(), demoLogin(), handleLoginSubmit(), handleRegisterSubmit(), logoutUser(), playChime(), repeatOrder() (+5 more)

### Community 6 - "initEventSource"
Cohesion: 0.36
Nodes (10): checkActiveOrderStatus(), getActiveOrderId(), getStatusRu(), getStatusSubText(), initEventSource(), openOrderTracker(), setActiveOrder(), submitOrder() (+2 more)

### Community 7 - "saveCart"
Cohesion: 0.25
Nodes (8): applyPromocode(), changeQty(), quickAddToCart(), renderCart(), saveCart(), toggleCart(), updateCartBadge(), updateCartSummary()

### Community 8 - "Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)"
Cohesion: 0.29
Nodes (6): 1. Архитектура и технологии, 2. Структура каталогов, 3. Рабочие модули системы, 4. Корпоративные учетные записи, 5. Действующие промокоды, Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)

### Community 9 - "Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)"
Cohesion: 0.29
Nodes (6): 1. Архитектура и технологии, 2. Структура каталогов, 3. Рабочие модули системы, 4. Корпоративные учетные записи, 5. Действующие промокоды, Elcho's Pizza — Автоматизированная система управления пиццерией (Enterprise Suite)

### Community 11 - "loadUserOrderHistory"
Cohesion: 0.50
Nodes (4): getPluralItems(), loadUserOrderHistory(), openAuthModal(), openProfileModal()

## Knowledge Gaps
- **6 isolated node(s):** `Method`, `RawUrl`, `Path`, `Query`, `Headers` (+1 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 39 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SQLiteDatabase` connect `SQLiteDatabase` to `ApiHandler`, `HttpServer`, `HttpServer.cs`?**
  _High betweenness centrality (0.114) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `ApiHandler` (e.g. with `HttpServer.HttpServer()` and `Program.Main()`) actually correct?**
  _`ApiHandler` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Method`, `RawUrl`, `Path` to the rest of the system?**
  _6 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `ApiHandler` be split into smaller, more focused modules?**
  _Cohesion score 0.12895927601809956 - nodes in this community are weakly interconnected._
- **Why does `ApiHandler` connect `ApiHandler` to `SQLiteDatabase`, `HttpServer`, `HttpServer.cs`?**
  _High betweenness centrality (0.111) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `SQLiteDatabase` (e.g. with `Program.Main()` and `Program.Main()`) actually correct?**
  _`SQLiteDatabase` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Should `HttpServer` be split into smaller, more focused modules?**
  _Cohesion score 0.11264367816091954 - nodes in this community are weakly interconnected._