#!/bin/bash
set -e

echo "=== Запуск Elcho's Pizza Enterprise Server (Cloud / Docker) ==="

# Создаем каталог данных для постоянного диска (Persistent Volume)
mkdir -p /data

# Если базы данных еще нет на постоянном диске, копируем первичную инициализированную базу
if [ ! -f /data/pizza.db ] && [ -f /app/project/pizza.db ]; then
    echo "[Storage] Первичная инициализация постоянного тома: копирование pizza.db в /data/pizza.db"
    cp /app/project/pizza.db /data/pizza.db
    chmod 666 /data/pizza.db
fi

# Проверяем порт от облачного хостинга (Render=10000, Railway=динамический, по умолчанию 5000)
export PORT="${PORT:-5000}"
export DATABASE_PATH="${DATABASE_PATH:-/data/pizza.db}"

echo "[Network] Сервер запускается на порту: $PORT"
echo "[Database] Путь к базе данных: $DATABASE_PATH"

# Запуск скомпилированного C# сервера через среду выполнения Mono
exec mono /app/Server.exe
