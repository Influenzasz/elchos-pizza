# ☁️ Руководство по облачному деплою: Render.com и Railway.app

Пошаговая инструкция по круглосуточному запуску проекта **Elcho's Pizza** в облаке с сохранением базы данных на постоянном диске (Persistent Volume).

---

## 1. Как это работает в облаке (Архитектура контейнера)

1. **Контейнер Docker (`Dockerfile`):**  
   Использует официальный образ среды выполнения **Mono 6.12** на базе Debian Linux. Скомпилированный C# сервер `Server.exe` запускается в Linux без перекомпиляции.
2. **Кросс-платформенная связка с SQLite (`Server.exe.config`):**  
   Файл конфигурации автоматически транслирует Windows-вызовы `winsqlite3.dll` в стандартную системную библиотеку Linux `libsqlite3.so.0`.
3. **Постоянный диск (Persistent Volume):**  
   Диск монтируется в каталог `/data`. При первом запуске скрипт `entrypoint.sh` автоматически копирует каталог блюд и рецептур в `/data/pizza.db`. Все последующие заказы, отзывы и смены сохраняются навсегда, даже при перезагрузке контейнера!

---

## 2. Развертывание на Render.com (Самый простой способ)

Render.com автоматически подхватывает файл конфигурации `render.yaml`.

### Шаг 1. Загрузка кода на GitHub
1. Создайте репозиторий на GitHub (публичный или приватный).
2. Загрузите файлы проекта в репозиторий:
   ```bash
   git init
   git add .
   git commit -m "feat: cloud deployment setup for Render and Railway"
   git branch -M main
   git remote add origin https://github.com/ВАШ_ЛОГИН/elchos-pizza.git
   git push -u origin main
   ```

### Шаг 2. Подключение репозитория к Render
1. Перейдите на сайт [render.com](https://render.com/) и войдите через свой GitHub-аккаунт.
2. В панели управления нажмите **«New +»** $\rightarrow$ выберите **«Web Service»**.
3. Выберите ваш репозиторий с проектом Elcho's Pizza.
4. Render автоматически определит `Dockerfile`! Задайте базовые параметры:
   * **Name:** `elchos-pizza`
   * **Region:** `Frankfurt (EU)` (ближе всего к РФ)
   * **Instance Type:** Starter (или Free)
5. **Подключение постоянного диска (Критично для сохранения базы):**
   * Прокрутите страницу настроек вниз до раздела **«Disks»** (или вкладки *Disks* в меню сервиса).
   * Нажмите **«Add Disk»**:
     * **Name:** `pizza-data`
     * **Mount Path:** `/data`
     * **Size:** `1 GB` (этого хватит на сотни тысяч заказов)
6. В разделе **Environment Variables** (переменные окружения) убедитесь, что указано:
   * `DATABASE_PATH` = `/data/pizza.db`
   * `PORT` = `10000` (Render устанавливает автоматически)
7. Нажмите **«Create Web Service»**.

Через 2–3 минуты сборка завершится, и вы получите персональный HTTPS-адрес:  
`https://elchos-pizza-xxxx.onrender.com`

---

## 3. Развертывание на Railway.app

Платформа Railway отлично подходит для деплоя через веб-интерфейс или через Railway CLI.

### Вариант через веб-интерфейс Railway:
1. Перейдите на [railway.app](https://railway.app/) и авторизуйтесь через GitHub.
2. Нажмите **«New Project»** $\rightarrow$ **«Deploy from GitHub repo»**.
3. Выберите ваш репозиторий.
4. После создания сервиса нажмите на него и перейдите во вкладку **«Settings»**:
   * В разделе **Volumes** нажмите **«Add Volume»**:
     * **Mount Path:** укажите `/data`
5. Во вкладке **Variables** добавьте:
   * `DATABASE_PATH` = `/data/pizza.db`
6. Во вкладке **Networking** нажмите **«Generate Domain»** — Railway мгновенно выдаст публичный URL вида `https://elchos-pizza.up.railway.app`.

### Вариант через консоль (Railway CLI):
Если у вас установлен Railway CLI:
```bash
npm install -g @railway/cli
railway login
railway init
railway volume add --mount-path /data
railway up
```

---

## 4. Проверка работы сайта в облаке

После завершения деплоя откройте ваш облачный адрес в браузере:
* `https://ВАШ-ДОМЕН/` — клиентская витрина онлайн-заказа;
* `https://ВАШ-ДОМЕН/pos` — сенсорная касса кассира;
* `https://ВАШ-ДОМЕН/kitchen` — кухонный монитор KDS поваров;
* `https://ВАШ-ДОМЕН/board` — табло очереди в зале;
* `https://ВАШ-ДОМЕН/admin` — панель управления, склад WMS и выручка.

> [!TIP]
> Все экраны мгновенно синхронизируются через Server-Sent Events (SSE). Если вы откроете сайт на телефоне и сделаете заказ, а на компьютере откроете `/kitchen`, заказ появится на экране повара мгновенно!
