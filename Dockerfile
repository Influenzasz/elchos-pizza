# ==============================================================================
# Dockerfile для облачного развертывания Elcho's Pizza (Render.com / Railway.app)
# Базовый образ: Актуальный Debian 12 (Bookworm) с официальным Mono runtime и SQLite
# ==============================================================================
FROM debian:bookworm-slim

# Установка актуального Mono и SQLite3 из официальных репозиториев Debian 12
RUN apt-get update && apt-get install -y --no-install-recommends \
    mono-runtime \
    mono-complete \
    sqlite3 \
    libsqlite3-0 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Копирование файлов проекта
COPY . /app/

# Настройка dllmap для связи Windows P/Invoke winsqlite3.dll -> Linux libsqlite3.so.0
RUN echo '<configuration><dllmap dll="winsqlite3.dll" target="libsqlite3.so.0" /></configuration>' > /app/Server.exe.config && \
    cp /app/Server.exe.config /app/project/Server.exe.config

# Точка монтирования для диска (если подключен)
VOLUME ["/data"]

ENV PORT=10000
ENV AUTO_OPEN_BROWSER=false

EXPOSE 10000

# Запуск с поддержкой постоянного диска и любых динамических портов Render / Railway
CMD ["/bin/bash", "-c", "mkdir -p /data; if [ -d /data ] && [ ! -f /data/pizza.db ] && [ -f /app/project/pizza.db ]; then cp /app/project/pizza.db /data/pizza.db 2>/dev/null || true; chmod 666 /data/pizza.db 2>/dev/null || true; fi; export DATABASE_PATH=${DATABASE_PATH:-/data/pizza.db}; export PORT=${PORT:-10000}; echo '[Cloud] Starting Elcho Pizza Server on port '$PORT; exec mono /app/Server.exe"]
