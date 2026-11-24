# Скрипт восстановления навыков Elcho's Pizza
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "Создание каталогов навыков..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\.agents\skills\security-audit" | Out-Null
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\.agents\skills\system-tester" | Out-Null

Write-Host "Запись навыка security-audit..." -ForegroundColor Cyan
$sec = @'
---
name: security-audit
description: Проведение аудита информационной безопасности проекта «Elcho's Pizza», проверка защиты API (RBAC), SQL-инъекций, соответствия 54-ФЗ и 152-ФЗ, а также подготовка материалов для дипломной работы по ИБ.
---

# Навык аудита безопасности (Security Audit)

Используйте этот навык при проверке безопасности кодовой базы или написании разделов диплома по ИБ.

## Области проверки:
1. RBAC & Аутентификация: Проверить фильтрацию маршрутов /api/admin/*, проверку ролей (admin, cashier, chef, courier, client) и возврат HTTP 403.
2. База данных SQLite: Проверить Database.cs на отсутствие конкатенации строк в SQL, корректность транзакций и защиту журнала WAL.
3. Фискальный учет (54-ФЗ): Проверить правильность расчета сумм, НДС, номеров смен и чеков в фискальных операциях.
4. Защита персональных данных (152-ФЗ): Проверить экранирование данных клиентов в логах и клиентских интерфейсах.
5. Материалы для диплома: Сопоставление с моделью угроз ФСТЭК России в Диплом_Пиццерия/ДЗ_Защита_Информации_Элчос_Пицца.docx.
'@
[System.IO.File]::WriteAllText("$PSScriptRoot\.agents\skills\security-audit\SKILL.md", $sec, [System.Text.Encoding]::UTF8)

Write-Host "Запись навыка system-tester..." -ForegroundColor Cyan
$sys = @'
---
name: system-tester
description: Запуск и анализ результатов сквозного валидатора «Тест_Системы.exe» (40 интеграционных сценариев) для предотвращения регрессий в проекте «Elcho's Pizza».
---

# Навык сквозного тестирования системы (System Tester)

Используйте этот навык после любых изменений в коде C#, REST API или структуре базы данных.

## Порядок выполнения:
1. Запустить валидатор: .\Тест_Системы.exe
2. Убедиться, что все 40 сценариев завершились со статусом PASSED (0 FAILED).
3. В случае сбоя сценария: локализовать ошибку по номеру теста в project/src/TestSuite.cs и устранить причину в соответствующем модуле.
'@
[System.IO.File]::WriteAllText("$PSScriptRoot\.agents\skills\system-tester\SKILL.md", $sys, [System.Text.Encoding]::UTF8)

Write-Host "Готово! Все навыки успешно восстановлены в .agents/skills/" -ForegroundColor Green
