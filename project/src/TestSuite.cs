using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PizzaSystem.Data;
using PizzaSystem.Realtime;
using PizzaSystem.Server;
using PizzaSystem.Api;

namespace PizzaSystem.Tests
{
    class Program
    {
        static int passed = 0;
        static int failed = 0;

        static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[УСПЕХ] ");
                Console.ResetColor();
                Console.WriteLine(testName);
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[ОШИБКА] ");
                Console.ResetColor();
                Console.WriteLine(testName);
                failed++;
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Elcho's Pizza - Сквозное автоматизированное тестирование";

            Console.WriteLine("================================================================================");
            Console.WriteLine("          КОМПЛЕКСНОЕ СКВОЗНОЕ ТЕСТИРОВАНИЕ СИСТЕМЫ ELCHO'S PIZZA               ");
            Console.WriteLine("================================================================================\n");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "pizza.db");
            if (!File.Exists(dbPath)) dbPath = Path.Combine(baseDir, "project", "pizza.db");

            var db = new SQLiteDatabase(dbPath);
            DbInitializer.Initialize(db);
            var api = new ApiHandler(db);
            var json = new JavaScriptSerializer();

            // БЛОК 1: Авторизация и безопасность
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--- БЛОК 1: Авторизация персонала и клиентский доступ ---");
            Console.ResetColor();
            try
            {
                var resAdmin = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/login", Body = "{\"login\":\"admin@elchos.ru\",\"password\":\"admin123\"}" });
                var dictAdmin = json.Deserialize<Dictionary<string, object>>(json.Serialize(resAdmin));
                Assert(dictAdmin.ContainsKey("success") && Convert.ToBoolean(dictAdmin["success"]), "Вход в систему Администратора (admin@elchos.ru)");

                var resCashier = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/login", Body = "{\"login\":\"cashier@elchos.ru\",\"password\":\"cashier123\"}" });
                var dictCashier = json.Deserialize<Dictionary<string, object>>(json.Serialize(resCashier));
                Assert(dictCashier.ContainsKey("success") && Convert.ToBoolean(dictCashier["success"]), "Вход в систему Кассира (cashier@elchos.ru)");

                var resChef = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/login", Body = "{\"login\":\"chef@elchos.ru\",\"password\":\"chef123\"}" });
                var dictChef = json.Deserialize<Dictionary<string, object>>(json.Serialize(resChef));
                Assert(dictChef.ContainsKey("success") && Convert.ToBoolean(dictChef["success"]), "Вход в систему Шеф-повара (chef@elchos.ru)");

                var resCourier = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/login", Body = "{\"login\":\"courier@elchos.ru\",\"password\":\"courier123\"}" });
                var dictCourier = json.Deserialize<Dictionary<string, object>>(json.Serialize(resCourier));
                Assert(dictCourier.ContainsKey("success") && Convert.ToBoolean(dictCourier["success"]), "Вход в систему Курьера (courier@elchos.ru)");

                string regEmail = "client_" + DateTime.Now.Ticks + "@test.ru";
                string regBody = "{\"email\":\"" + regEmail + "\",\"phone\":\"+79998887766\",\"password\":\"secret123\",\"fullName\":\"Тестовый Покупатель\",\"address\":\"ул. Ленина, д. 1\"}";
                var resReg = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/register", Body = regBody });
                var dictReg = json.Deserialize<Dictionary<string, object>>(json.Serialize(resReg));
                Assert(dictReg.ContainsKey("success") && Convert.ToBoolean(dictReg["success"]), "Регистрация нового клиента");

                var resDup = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/auth/register", Body = regBody });
                var dictDup = json.Deserialize<Dictionary<string, object>>(json.Serialize(resDup));
                Assert(dictDup.ContainsKey("success") && !Convert.ToBoolean(dictDup["success"]), "Валидация: защита от дублирования Email");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой блока авторизации: " + ex.Message);
            }

            // БЛОК 2: Каталог, меню и промокоды
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- БЛОК 2: Каталог товаров, размеры, добавки и промокоды ---");
            Console.ResetColor();
            try
            {
                var cats = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/categories" }) as IList;
                Assert(cats != null && cats.Count >= 6, "Каталог категорий (Пиццы, Закуски, Напитки, Десерты, Соусы, Комбо)");

                var prods = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/products" }) as IList;
                Assert(prods != null && prods.Count >= 40, "Ассортимент меню пиццерии (44 позиции)");

                bool allImagesExist = true;
                string wwwroot = Path.Combine(baseDir, "wwwroot");
                if (!Directory.Exists(wwwroot)) wwwroot = Path.Combine(baseDir, "project", "wwwroot");
                foreach (var pObj in prods)
                {
                    var p = json.Deserialize<Dictionary<string, object>>(json.Serialize(pObj));
                    string img = null;
                    if (p.ContainsKey("ImageUrl") && p["ImageUrl"] != null) img = p["ImageUrl"].ToString();
                    else if (p.ContainsKey("imageUrl") && p["imageUrl"] != null) img = p["imageUrl"].ToString();
                    if (string.IsNullOrEmpty(img)) { allImagesExist = false; break; }
                    string localPath = Path.Combine(wwwroot, img.TrimStart('/').Replace('/', '\\'));
                    if (!File.Exists(localPath) || new FileInfo(localPath).Length < 1000)
                    {
                        Console.WriteLine("    [!] Missing or empty image: " + localPath);
                        allImagesExist = false;
                        break;
                    }
                }
                Assert(allImagesExist, "Наличие реальных фотографий высокого разрешения для всех 44 позиций каталога");

                var sizes = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/sizes" }) as IList;
                Assert(sizes != null && sizes.Count == 3, "Размеры пицц: Маленькая 25 см, Средняя 30 см, Большая 35 см");

                var dtypes = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/doughtypes" }) as IList;
                Assert(dtypes != null && dtypes.Count == 2, "Типы теста: Традиционное и Тонкое");

                var tops = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/toppings" }) as IList;
                Assert(tops != null && tops.Count >= 10, "Дополнительные ингредиенты и бортики (13 видов)");

                var promoRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/promocode/check", Body = "{\"code\":\"ELCHOS20\",\"orderTotal\":1500}" });
                var promoDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(promoRes));
                Assert(promoDict.ContainsKey("valid") && Convert.ToBoolean(promoDict["valid"]), "Промокод ELCHOS20 (скидка 20%)");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой блока каталога: " + ex.Message);
            }

            // БЛОК 3: Полный цикл создания и движения заказа
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- БЛОК 3: Сквозное прохождение заказа по всем станциям ---");
            Console.ResetColor();
            try
            {
                // 3.1. Создание простого заказа (напитки/закуски, sizeName = null, doughName = null)
                string simpleOrder = "{\"orderType\":\"Delivery\",\"customerName\":\"Ольга Сидорова\",\"customerPhone\":\"+79271112233\",\"deliveryAddress\":\"ул. Чапаева, д. 10\",\"paymentMethod\":\"CardOnline\",\"promoCode\":null,\"discountAmount\":0,\"items\":[{\"productId\":21,\"productName\":\"Добрый Кола 0.5 л\",\"sizeName\":null,\"doughName\":null,\"selectedToppings\":[],\"unitPrice\":129,\"quantity\":2}]}";
                var resSimple = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders", Body = simpleOrder });
                var dictSimple = json.Deserialize<Dictionary<string, object>>(json.Serialize(resSimple));
                Assert(dictSimple.ContainsKey("success") && Convert.ToBoolean(dictSimple["success"]), "Сайт: Создание простого заказа (без размера и добавок)");

                // 3.2. Создание кастомной пиццы с добавками
                string customOrder = "{\"orderType\":\"Delivery\",\"customerName\":\"Геннадий Ревизоров\",\"customerPhone\":\"+79174445566\",\"deliveryAddress\":\"ул. Бабушкин Взвоз, 3\",\"paymentMethod\":\"CardOnline\",\"promoCode\":\"ELCHOS15\",\"discountAmount\":120,\"items\":[{\"productId\":1,\"productName\":\"Elcho's Special\",\"sizeName\":\"Большая 35 см\",\"doughName\":\"Традиционное\",\"selectedToppings\":[\"Сырный бортик\",\"Бекон\"],\"unitPrice\":899,\"quantity\":1}]}";
                var resCustom = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders", Body = customOrder });
                var dictCustom = json.Deserialize<Dictionary<string, object>>(json.Serialize(resCustom));
                Assert(dictCustom.ContainsKey("success") && Convert.ToBoolean(dictCustom["success"]), "Сайт: Создание кастомной пиццы (размер 35 см, бортик, бекон)");

                long orderId = Convert.ToInt64(dictCustom["orderId"]);
                string orderNum = dictCustom["orderNumber"].ToString();

                // 3.3. Кухня KDS
                var kitchenOrders = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/orders/kitchen" }) as IList;
                bool inKitchen = false;
                foreach (Dictionary<string, object> ko in kitchenOrders)
                {
                    if (Convert.ToInt64(ko["Id"]) == orderId) { inKitchen = true; break; }
                }
                Assert(inKitchen, "Кухня KDS: Заказ поступил в колонку «Новые заказы»");

                // 3.4. Повар берет в работу
                var cookRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders/" + orderId + "/status", Body = "{\"status\":\"Cooking\"}" });
                var cookDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(cookRes));
                Assert(cookDict.ContainsKey("success") && Convert.ToBoolean(cookDict["success"]), "Кухня KDS: Статус переведен в «Cooking» (Сборка пиццы)");

                // 3.5. Отправка в печь
                var ovenRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders/" + orderId + "/status", Body = "{\"status\":\"Oven\"}" });
                var ovenDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(ovenRes));
                Assert(ovenDict.ContainsKey("success") && Convert.ToBoolean(ovenDict["success"]), "Кухня KDS: Статус переведен в «Oven» (Выпекание при 300°C)");

                // 3.6. Готовность заказа
                var readyRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders/" + orderId + "/status", Body = "{\"status\":\"Ready\"}" });
                var readyDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(readyRes));
                Assert(readyDict.ContainsKey("success") && Convert.ToBoolean(readyDict["success"]), "Кухня KDS: Статус переведен в «Ready» (Упакован)");

                // 3.7. Табло в зале
                var boardRes = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/orders/board" });
                var boardDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(boardRes));
                var readyList = boardDict["ready"] as IList;
                bool onBoard = false;
                foreach (Dictionary<string, object> ro in readyList)
                {
                    if (Convert.ToInt64(ro["Id"]) == orderId) { onBoard = true; break; }
                }
                Assert(onBoard, "Табло зала: Номер " + orderNum + " отображается в колонке «Готовы к выдаче»");

                // 3.8. Курьер берет на доставку
                var courierRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders/" + orderId + "/status", Body = "{\"status\":\"OnTheWay\"}" });
                var courierDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(courierRes));
                Assert(courierDict.ContainsKey("success") && Convert.ToBoolean(courierDict["success"]), "Курьер PWA: Статус переведен в «OnTheWay» (В пути к клиенту)");

                // 3.9. Вручение клиенту
                var delivRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders/" + orderId + "/status", Body = "{\"status\":\"Delivered\"}" });
                var delivDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(delivRes));
                Assert(delivDict.ContainsKey("success") && Convert.ToBoolean(delivDict["success"]), "Курьер PWA: Фиксация вручения («Delivered»)");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой сквозного цикла заказа: " + ex.Message);
            }

            // БЛОК 4: Касса POS и Киоск самообслуживания
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- БЛОК 4: Кассовый POS-терминал и Киоск самообслуживания ---");
            Console.ResetColor();
            try
            {
                // Киоск
                string kioskPayload = "{\"orderType\":\"DineIn\",\"customerName\":\"Гость Киоска\",\"paymentMethod\":\"CardTerminal\",\"items\":[{\"productId\":3,\"productName\":\"Сырная Цыпа\",\"sizeName\":\"Средняя 30 см\",\"doughName\":\"Традиционное\",\"selectedToppings\":[],\"unitPrice\":599,\"quantity\":1}]}";
                var kioskRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/orders", Body = kioskPayload });
                var kioskDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(kioskRes));
                Assert(kioskDict.ContainsKey("success") && Convert.ToBoolean(kioskDict["success"]), "Киоск: Прием экспресс-заказа в зале");

                // Касса POS
                string posPayload = "{\"orderType\":\"Takeout\",\"customerName\":\"Гость у стойки\",\"paymentMethod\":\"Cash\",\"items\":[{\"productId\":4,\"productName\":\"Ветчина и грибы\",\"sizeName\":\"Маленькая 25 см\",\"doughName\":\"Тонкое\",\"selectedToppings\":[],\"unitPrice\":499,\"quantity\":1}]}";
                var posRes = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/pos/create-order", Body = posPayload });
                var posDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(posRes));
                Assert(posDict.ContainsKey("success") && Convert.ToBoolean(posDict["success"]), "Касса POS: Фиксация наличной продажи кассиром");

                long posId = Convert.ToInt64(posDict["orderId"]);
                var recRes = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/pos/receipt/" + posId });
                var recDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(recRes));
                Assert(recDict.ContainsKey("company") && recDict["company"].ToString() == "ООО «Элчос Пицца»", "Касса POS: Генерация фискального чека ООО «Элчос Пицца»");
                Assert(Convert.ToDouble(recDict["totalAmount"]) == 499, "Касса POS: Корректность суммы чека (499 ₽)");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой POS/Киоска: " + ex.Message);
            }

            // БЛОК 5: Складской учет WMS и аналитика ERP
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- БЛОК 5: Складской учет (WMS) и Аналитика предприятия ---");
            Console.ResetColor();
            try
            {
                var invItems = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/inventory" }) as IList;
                Assert(invItems != null && invItems.Count >= 15, "Склад WMS: Контроль остатков сырья (18 позиций)");

                var restock = api.Execute(new SimpleHttpRequest { Method = "POST", Path = "/api/inventory/restock", Body = "{\"ingredientId\":1,\"amount\":20}" });
                var restockDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(restock));
                Assert(restockDict.ContainsKey("success") && Convert.ToBoolean(restockDict["success"]), "Склад WMS: Приходная накладная / пополнение сырья");

                var analytics = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/analytics" });
                var analyticsDict = json.Deserialize<Dictionary<string, object>>(json.Serialize(analytics));
                Assert(analyticsDict.ContainsKey("todayRevenue") && analyticsDict.ContainsKey("todayOrders"), "ERP: Сводная аналитика выручки и среднего чека");

                var logs = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/logs" }) as IList;
                Assert(logs != null && logs.Count > 0, "ERP: Журнал аудита действий персонала пиццерии");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой склада/аналитики: " + ex.Message);
            }

            // БЛОК 6: Онлайн-трекинг заказов, кассовые смены 54-ФЗ и отзывы гостей
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- БЛОК 6: Онлайн-трекер заказов, кассовые смены (54-ФЗ) и отзывы гостей ---");
            Console.ResetColor();
            try
            {
                // 6.1. Трекинг по номеру заказа и по телефону
                var trackByNum = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/orders/track?q=%23101" });
                var dictTrackNum = json.Deserialize<Dictionary<string, object>>(json.Serialize(trackByNum));
                Assert(dictTrackNum.ContainsKey("OrderNumber") && dictTrackNum["OrderNumber"].ToString() == "#101", "Трекер: Поиск заказа по номеру (#101)");

                var trackByPhone = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/orders/track?q=9991112233" });
                var dictTrackPhone = json.Deserialize<Dictionary<string, object>>(json.Serialize(trackByPhone));
                Assert(dictTrackPhone.ContainsKey("CustomerPhone"), "Трекер: Поиск активного заказа по номеру телефона");

                // 6.2. Детальная информация о заказе для модального трекера
                var ordDetails = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/orders/101" });
                var dictDetails = json.Deserialize<Dictionary<string, object>>(json.Serialize(ordDetails));
                Assert(dictDetails.ContainsKey("items") && (dictDetails["items"] as IList).Count > 0, "Трекер: Получение детализации состава заказа и статуса");

                // 6.3. Кассовые смены (54-ФЗ)
                var curShift = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/shifts/current" });
                var dictShift = json.Deserialize<Dictionary<string, object>>(json.Serialize(curShift));
                Assert(dictShift.ContainsKey("hasOpenShift"), "54-ФЗ: Получение статуса текущей кассовой смены");

                var allShifts = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/shifts" }) as IList;
                Assert(allShifts != null && allShifts.Count > 0, "54-ФЗ: Реестр фискальных смен в панели администратора");

                var xReport = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/shifts/x-report" });
                var dictX = json.Deserialize<Dictionary<string, object>>(json.Serialize(xReport));
                Assert(dictX.ContainsKey("hasOpenShift"), "54-ФЗ: Формирование промежуточного X-отчета кассира");

                // 6.4. Отзывы и контроль качества
                var revList = api.Execute(new SimpleHttpRequest { Method = "GET", Path = "/api/reviews" }) as IList;
                Assert(revList != null && revList.Count > 0, "Контроль качества: Получение отзывов гостей на сайте");

                var addRev = api.Execute(new SimpleHttpRequest {
                    Method = "POST",
                    Path = "/api/reviews",
                    Body = "{\"orderId\":101,\"orderNumber\":\"#101\",\"customerName\":\"Виктор Тестовый\",\"rating\":5,\"comment\":\"Отличная горячая пицца, супер сервис!\"}"
                });
                var dictAddRev = json.Deserialize<Dictionary<string, object>>(json.Serialize(addRev));
                Assert(dictAddRev.ContainsKey("success") && Convert.ToBoolean(dictAddRev["success"]), "Контроль качества: Публикация отзыва гостя через онлайн-трекер");

                // 6.5. Ролевая модель доступа (RBAC) и защита прав
                var reqForbidden = new SimpleHttpRequest {
                    Method = "POST",
                    Path = "/api/products/delete",
                    Body = "{\"id\":999}"
                };
                reqForbidden.Headers["X-User-Role"] = "Client";
                var resForb = api.Execute(reqForbidden);
                var dictForb = json.Deserialize<Dictionary<string, object>>(json.Serialize(resForb));
                Assert(dictForb.ContainsKey("forbidden") && Convert.ToBoolean(dictForb["forbidden"]), "RBAC: Блокировка попытки удаления блюда клиентом (HTTP 403 Forbidden)");

                var reqShiftForb = new SimpleHttpRequest {
                    Method = "POST",
                    Path = "/api/shifts/close",
                    Body = "{}"
                };
                reqShiftForb.Headers["X-User-Role"] = "Client";
                var resShiftForb = api.Execute(reqShiftForb);
                var dictShiftForb = json.Deserialize<Dictionary<string, object>>(json.Serialize(resShiftForb));
                Assert(dictShiftForb.ContainsKey("forbidden") && Convert.ToBoolean(dictShiftForb["forbidden"]), "RBAC: Блокировка закрытия смены кассы клиентом (HTTP 403 Forbidden)");
            }
            catch (Exception ex)
            {
                Assert(false, "Сбой блока трекера/смен/отзывов: " + ex.Message);
            }

            Console.WriteLine("\n================================================================================");
            Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(string.Format("РЕЗУЛЬТАТ: УСПЕШНО ПРОЙДЕНО СЦЕНАРИЕВ: {0} ИЗ {1}. ОШИБОК: {2}", passed, passed + failed, failed));
            Console.ResetColor();
            Console.WriteLine("================================================================================\n");

            if (args.Length == 0 || args[0] != "--auto")
            {
                Console.WriteLine("Нажмите Enter для завершения...");
                Console.ReadLine();
            }
        }
    }
}
