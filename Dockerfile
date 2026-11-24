# ==============================================================================
# Dockerfile для облачного развертывания Elcho's Pizza (Render.com / Railway.app)
# Базовый образ: Облегченная среда выполнения Mono на базе Debian
# ==============================================================================
FROM mono:6.12-slim

# Установка нативной библиотеки SQLite3 и системных зависимостей
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    libsqlite3-0 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Копирование исходных файлов, скомпилированного сервера и веб-ресурсов
COPY . /app/

# Настройка прав на запуск entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Точка монтирования для постоянного диска (Persistent Volume)
VOLUME ["/data"]

# Порт по умолчанию (облачные провайдеры переопределяют через ENV PORT)
ENV PORT=5000
ENV DATABASE_PATH=/data/pizza.db
ENV AUTO_OPEN_BROWSER=false

EXPOSE 5000

# Запуск через стартовый скрипт инициализации тома
ENTRYPOINT ["/app/entrypoint.sh"]
