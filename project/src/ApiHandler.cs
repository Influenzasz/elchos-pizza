// ApiHandler.cs - Обработчик всех REST API запросов системы управления пиццерией
using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using PizzaSystem.Data;
using PizzaSystem.Realtime;
using PizzaSystem.Server;

namespace PizzaSystem.Api
{
    public class ApiHandler
    {
        private readonly SQLiteDatabase _db;
        private readonly JavaScriptSerializer _json;

        public ApiHandler(SQLiteDatabase db)
        {
            _db = db;
            _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public object Execute(SimpleHttpRequest req)
        {
            if (req.Path != null && req.Path.Contains("?"))
            {
                int qIdx = req.Path.IndexOf('?');
                string qStr = req.Path.Substring(qIdx + 1);
                req.Path = req.Path.Substring(0, qIdx);
                if (req.Query == null) req.Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] qParts = qStr.Split('&');
                foreach (var qp in qParts)
                {
                    string[] kv = qp.Split('=');
                    if (kv.Length == 2)
                        req.Query[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                    else if (kv.Length == 1)
                        req.Query[Uri.UnescapeDataString(kv[0])] = "";
                }
            }

            string path = req.Path.ToLowerInvariant();
            string method = req.Method.ToUpperInvariant();
            string body = req.Body;

            // 1. АВТОРИЗАЦИЯ
            if (path == "/api/auth/login" && method == "POST")
            {
                return HandleLogin(body);
            }
            if (path == "/api/auth/register" && method == "POST")
            {
                return HandleRegister(body);
            }
            if (path == "/api/auth/demo-login" && method == "POST")
            {
                return HandleDemoLogin(body);
            }
            if (path == "/api/auth/me")
            {
                return HandleGetMe(req);
            }

            // 2. КАТАЛОГ И МЕНЮ
            if (path == "/api/categories")
            {
                return _db.Query("SELECT * FROM Categories ORDER BY DisplayOrder ASC;");
            }
            if (path == "/api/products")
            {
                return HandleGetProducts(req);
            }
            if (path == "/api/sizes")
            {
                return _db.Query("SELECT * FROM PizzaSizes ORDER BY Diameter ASC;");
            }
            if (path == "/api/doughtypes")
            {
                return _db.Query("SELECT * FROM DoughTypes ORDER BY Id ASC;");
            }
            if (path == "/api/toppings")
            {
                return _db.Query("SELECT * FROM Toppings WHERE IsAvailable = 1 ORDER BY Category, Price ASC;");
            }
            if (path == "/api/products/save" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Доступ запрещен (требуется роль Администратора)" };
                return HandleSaveProduct(body);
            }
            if (path == "/api/products/toggle" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Доступ запрещен (требуется роль Администратора)" };
                return HandleToggleProduct(body);
            }
            if (path == "/api/products/delete" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Доступ запрещен (требуется роль Администратора)" };
                return HandleDeleteProduct(body);
            }

            // 3. ЗАКАЗЫ
            if (path == "/api/orders" && method == "POST")
            {
                return HandleCreateOrder(body);
            }
            if (path == "/api/orders" && method == "GET")
            {
                return HandleGetOrders(req);
            }
            if (path == "/api/orders/track" && method == "GET")
            {
                return HandleTrackOrder(req);
            }
            if (path.StartsWith("/api/orders/user/") && method == "GET")
            {
                return HandleGetUserOrders(path);
            }
            if (path.StartsWith("/api/orders/") && path.EndsWith("/status") && method == "POST")
            {
                return HandleUpdateOrderStatus(path, body, req);
            }
            if (path == "/api/orders-board" || path == "/api/orders/board")
            {
                return HandleGetBoardOrders();
            }
            if (path == "/api/orders/kitchen")
            {
                return HandleGetKitchenOrders();
            }
            if (path == "/api/orders/courier")
            {
                return HandleGetCourierOrders();
            }
            if (path.StartsWith("/api/orders/") && method == "GET")
            {
                return HandleGetOrderDetails(path);
            }

            // 4. КАССА POS И КАССОВЫЕ СМЕНЫ (54-ФЗ)
            if (path == "/api/shifts" && method == "GET")
            {
                return HandleGetAllShifts();
            }
            if (path == "/api/shifts/current" && method == "GET")
            {
                return HandleGetCurrentShift();
            }
            if (path == "/api/shifts/open" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Cashier" && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Управление сменами разрешено только Кассиру и Администратору" };
                return HandleOpenShift(body);
            }
            if (path == "/api/shifts/close" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Cashier" && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Управление сменами разрешено только Кассиру и Администратору" };
                return HandleCloseShift(body);
            }
            if (path == "/api/shifts/x-report" && method == "GET")
            {
                return HandleGetXReport();
            }
            if (path == "/api/pos/create-order" && method == "POST")
            {
                return HandlePosCreateOrder(body);
            }
            if (path.StartsWith("/api/pos/receipt/"))
            {
                return HandleGetReceipt(path);
            }

            // 5. ОТЗЫВЫ И КОНТРОЛЬ КАЧЕСТВА
            if (path == "/api/reviews" && method == "GET")
            {
                return HandleGetReviews();
            }
            if (path == "/api/reviews" && method == "POST")
            {
                return HandleAddReview(body);
            }

            // 5. СКЛАД / ИНВЕНТАРЬ
            if (path == "/api/inventory" && method == "GET")
            {
                return HandleGetInventory();
            }
            if (path == "/api/inventory/restock" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Пополнение склада разрешено только Администратору" };
                return HandleRestock(body);
            }
            if (path == "/api/inventory/update" && method == "POST")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { success = false, forbidden = true, message = "Ошибка 403: Изменение складских позиций разрешено только Администратору" };
                return HandleUpdateInventory(body);
            }

            // 6. АНАЛИТИКА
            if (path == "/api/analytics")
            {
                return HandleGetAnalytics();
            }
            if (path == "/api/logs")
            {
                string r = GetCallerRole(req);
                if (r != null && r != "Admin") return new { error = "Ошибка 403: Доступ к журналу аудита разрешен только Администратору" };
                return _db.Query("SELECT * FROM ActivityLogs ORDER BY Id DESC LIMIT 100;");
            }

            // 7. ПРОМОКОДЫ
            if (path == "/api/promocode/check" && method == "POST")
            {
                return HandleCheckPromocode(body);
            }

            return new { error = "API маршрут не найден: " + path };
        }

        #region Auth Handlers
        private object HandleLogin(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string login = GetString(data, "login", GetString(data, "email", "")).Trim();
            string pass = GetString(data, "password", "").Trim();

            var users = _db.Query("SELECT * FROM Users WHERE (Email = ? OR Phone = ?) AND Password = ? AND IsActive = 1 LIMIT 1;", login, login, pass);
            if (users.Count == 0)
            {
                return new { success = false, message = "Неверный логин (Email/Телефон) или пароль" };
            }

            var user = users[0];
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _db.ExecuteNonQuery("UPDATE Users SET LastLogin = ? WHERE Id = ?;", now, user["Id"]);

            _db.ExecuteNonQuery("INSERT INTO ActivityLogs (UserId, UserName, Action, Details, IpAddress, Timestamp) VALUES (?, ?, 'Вход в систему', 'Успешная авторизация', '127.0.0.1', ?);",
                user["Id"], user["FullName"], now);

            return new
            {
                success = true,
                user = new
                {
                    id = user["Id"],
                    email = user["Email"],
                    phone = user["Phone"],
                    fullName = user["FullName"],
                    address = user["Address"],
                    role = user["Role"]
                }
            };
        }

        private object HandleDemoLogin(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string role = data.ContainsKey("role") ? data["role"].ToString() : "Admin";

            var users = _db.Query("SELECT * FROM Users WHERE Role = ? AND IsActive = 1 ORDER BY Id ASC LIMIT 1;", role);
            if (users.Count == 0)
            {
                return new { success = false, message = "Пользователь с ролью " + role + " не найден" };
            }

            var user = users[0];
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _db.ExecuteNonQuery("UPDATE Users SET LastLogin = ? WHERE Id = ?;", now, user["Id"]);

            return new
            {
                success = true,
                user = new
                {
                    id = user["Id"],
                    email = user["Email"],
                    phone = user["Phone"],
                    fullName = user["FullName"],
                    address = user["Address"],
                    role = user["Role"]
                }
            };
        }

        private object HandleRegister(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string email = data.ContainsKey("email") ? data["email"].ToString().Trim() : "";
            string phone = data.ContainsKey("phone") ? data["phone"].ToString().Trim() : "";
            string pass = data.ContainsKey("password") ? data["password"].ToString().Trim() : "";
            string name = data.ContainsKey("fullName") ? data["fullName"].ToString().Trim() : "";
            string address = data.ContainsKey("address") ? data["address"].ToString().Trim() : "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(name))
            {
                return new { success = false, message = "Заполните обязательные поля: ФИО, Email, пароль" };
            }

            var existing = _db.Query("SELECT Id FROM Users WHERE Email = ?;", email);
            if (existing.Count > 0)
            {
                return new { success = false, message = "Пользователь с таким Email уже зарегистрирован" };
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            long id = _db.InsertAndGetId(@"
                INSERT INTO Users (Email, Phone, Password, FullName, Address, Role, CreatedAt, IsActive)
                VALUES (?, ?, ?, ?, ?, 'Client', ?, 1);
            ", email, phone, pass, name, address, now);

            return new
            {
                success = true,
                user = new
                {
                    id = id,
                    email = email,
                    phone = phone,
                    fullName = name,
                    address = address,
                    role = "Client"
                }
            };
        }

        private object HandleGetMe(SimpleHttpRequest req)
        {
            string uid = req.Query.ContainsKey("userId") ? req.Query["userId"] : null;
            if (string.IsNullOrEmpty(uid)) return new { authenticated = false };

            var users = _db.Query("SELECT Id, Email, Phone, FullName, Address, Role FROM Users WHERE Id = ?;", uid);
            if (users.Count == 0) return new { authenticated = false };

            return new { authenticated = true, user = users[0] };
        }
        #endregion

        #region Product & Menu Handlers
        private object HandleGetProducts(SimpleHttpRequest req)
        {
            string cat = req.Query.ContainsKey("category") ? req.Query["category"] : null;
            if (!string.IsNullOrEmpty(cat))
            {
                return _db.Query(@"
                    SELECT p.*, c.Name as CategoryName 
                    FROM Products p 
                    JOIN Categories c ON p.CategoryId = c.Id 
                    WHERE c.Slug = ? OR c.Id = ? 
                    ORDER BY p.IsAvailable DESC, p.Id ASC;", cat, cat);
            }

            return _db.Query(@"
                SELECT p.*, c.Name as CategoryName 
                FROM Products p 
                JOIN Categories c ON p.CategoryId = c.Id 
                ORDER BY c.DisplayOrder ASC, p.IsAvailable DESC, p.Id ASC;");
        }

        private object HandleSaveProduct(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            long id = data.ContainsKey("id") && data["id"] != null ? Convert.ToInt64(data["id"]) : 0;
            string name = data["name"].ToString();
            string desc = data.ContainsKey("description") ? data["description"].ToString() : "";
            int catId = Convert.ToInt32(data["categoryId"]);
            double price = Convert.ToDouble(data["basePrice"]);
            string badge = data.ContainsKey("badge") ? data["badge"].ToString() : "";
            string img = data.ContainsKey("imageUrl") ? data["imageUrl"].ToString() : "";
            int isPizza = data.ContainsKey("isPizza") && Convert.ToBoolean(data["isPizza"]) ? 1 : 0;
            int cal = data.ContainsKey("calories") && data["calories"] != null ? Convert.ToInt32(data["calories"]) : 0;
            int weight = data.ContainsKey("weightGrams") && data["weightGrams"] != null ? Convert.ToInt32(data["weightGrams"]) : 0;

            if (id > 0)
            {
                _db.ExecuteNonQuery(@"
                    UPDATE Products 
                    SET Name = ?, Description = ?, CategoryId = ?, BasePrice = ?, Badge = ?, ImageUrl = ?, IsPizza = ?, Calories = ?, WeightGrams = ?
                    WHERE Id = ?;", name, desc, catId, price, badge, img, isPizza, cal, weight, id);
            }
            else
            {
                id = _db.InsertAndGetId(@"
                    INSERT INTO Products (Name, Description, CategoryId, BasePrice, Badge, ImageUrl, IsPizza, Calories, WeightGrams, IsAvailable)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 1);", name, desc, catId, price, badge, img, isPizza, cal, weight);
            }

            EventBus.Publish("menu_updated", new { productId = id, name = name });
            return new { success = true, id = id };
        }

        private object HandleToggleProduct(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            long id = Convert.ToInt64(data["id"]);
            int isAvail = Convert.ToInt32(data["isAvailable"]);

            _db.ExecuteNonQuery("UPDATE Products SET IsAvailable = ? WHERE Id = ?;", isAvail, id);
            EventBus.Publish("menu_updated", new { productId = id, isAvailable = isAvail });
            return new { success = true };
        }

        private object HandleDeleteProduct(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            long id = Convert.ToInt64(data["id"]);
            _db.ExecuteNonQuery("DELETE FROM Products WHERE Id = ?;", id);
            EventBus.Publish("menu_updated", new { deletedId = id });
            return new { success = true };
        }
        #endregion

        #region Order Handlers
        private object HandleCreateOrder(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            if (data == null)
            {
                return new { success = false, message = "Некорректные данные заказа" };
            }

            string orderType = GetString(data, "orderType", "Delivery");
            long? userId = null;
            if (data.ContainsKey("userId") && data["userId"] != null)
            {
                long uidVal;
                if (long.TryParse(data["userId"].ToString(), out uidVal)) userId = uidVal;
            }

            string name = GetString(data, "customerName", "Гость");
            if (string.IsNullOrEmpty(name)) name = "Гость";

            string phone = GetString(data, "customerPhone", "-");
            string addr = GetString(data, "deliveryAddress", "");
            string comment = GetString(data, "comment", "");
            string payment = GetString(data, "paymentMethod", "CardOnline");
            string promo = GetString(data, "promoCode", "").Trim().ToUpperInvariant();
            double discount = GetDouble(data, "discountAmount", 0);

            int? table = null;
            if (data.ContainsKey("tableNumber") && data["tableNumber"] != null)
            {
                int tblVal;
                if (int.TryParse(data["tableNumber"].ToString(), out tblVal)) table = tblVal;
            }

            var lastIdObj = _db.ExecuteScalar("SELECT MAX(Id) FROM Orders;");
            long nextNum = (lastIdObj != null && lastIdObj != DBNull.Value ? Convert.ToInt64(lastIdObj) : 100) + 1;
            string orderNumber = "#" + nextNum;
            while (_db.Query("SELECT Id FROM Orders WHERE OrderNumber = ? LIMIT 1;", orderNumber).Count > 0)
            {
                nextNum++;
                orderNumber = "#" + nextNum;
            }

            var itemsList = data.ContainsKey("items") ? data["items"] as System.Collections.ArrayList : null;
            if (itemsList == null || itemsList.Count == 0)
            {
                return new { success = false, message = "Корзина пуста" };
            }

            double total = 0;
            var validatedItems = new List<Dictionary<string, object>>();

            foreach (object rawIt in itemsList)
            {
                var it = rawIt as Dictionary<string, object>;
                if (it == null) continue;

                long pid = GetLong(it, "productId", 0);
                int qty = Math.Max(1, GetInt(it, "quantity", 1));
                double unitPrice = GetDouble(it, "unitPrice", 0);
                string pName = GetString(it, "productName", "Пицца");
                string sName = GetNullableString(it, "sizeName");
                string dName = GetNullableString(it, "doughName");
                string toppingsJson = "[]";
                if (it.ContainsKey("selectedToppings") && it["selectedToppings"] != null)
                {
                    try { toppingsJson = _json.Serialize(it["selectedToppings"]); } catch { toppingsJson = "[]"; }
                }

                double itemTotal = unitPrice * qty;
                total += itemTotal;

                validatedItems.Add(new Dictionary<string, object>
                {
                    { "productId", pid },
                    { "productName", pName },
                    { "sizeName", sName },
                    { "doughName", dName },
                    { "toppingsJson", toppingsJson },
                    { "quantity", qty },
                    { "unitPrice", unitPrice },
                    { "totalPrice", itemTotal }
                });
            }

            if (validatedItems.Count == 0)
            {
                return new { success = false, message = "В корзине нет доступных товаров" };
            }

            double finalTotal = Math.Max(0, total - discount);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string paymentStatus = (payment == "CardOnline" || payment == "CardTerminal" || payment == "SBP") ? "Paid" : "Pending";

            long orderId = _db.InsertAndGetId(@"
                INSERT INTO Orders (OrderNumber, OrderType, UserId, CustomerName, CustomerPhone, DeliveryAddress, Comment, PaymentMethod, PaymentStatus, TotalAmount, DiscountAmount, PromoCode, Status, CreatedAt, TableNumber)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Created', ?, ?);",
                orderNumber, orderType, userId, name, phone, addr, comment, payment, paymentStatus, finalTotal, discount, promo, now, table);

            foreach (var vit in validatedItems)
            {
                _db.ExecuteNonQuery(@"
                    INSERT INTO OrderItems (OrderId, ProductId, ProductName, SizeName, DoughName, SelectedToppings, Quantity, UnitPrice, TotalPrice)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);",
                    orderId, vit["productId"], vit["productName"], vit["sizeName"], vit["doughName"], vit["toppingsJson"], vit["quantity"], vit["unitPrice"], vit["totalPrice"]);
            }

            // Автоматическое списание ингредиентов со склада
            DeductInventoryForOrder(validatedItems);

            // Лог создания
            _db.ExecuteNonQuery(@"
                INSERT INTO ActivityLogs (UserId, UserName, Action, Details, IpAddress, Timestamp)
                VALUES (?, ?, 'Новый заказ', ?, '127.0.0.1', ?);",
                userId, name, "Создан заказ " + orderNumber + " на сумму " + finalTotal + " ₽ (" + orderType + ")", now);

            // Real-time оповещение кухни, кассы, табло
            EventBus.Publish("order_created", new
            {
                id = orderId,
                orderNumber = orderNumber,
                orderType = orderType,
                customerName = name,
                customerPhone = phone,
                totalAmount = finalTotal,
                status = "Created",
                createdAt = now,
                itemsCount = validatedItems.Count
            });

            return new
            {
                success = true,
                id = orderId,
                orderId = orderId,
                orderNumber = orderNumber,
                totalAmount = finalTotal,
                status = "Created",
                createdAt = now
            };
        }

        private void DeductInventoryForOrder(List<Dictionary<string, object>> items)
        {
            try
            {
                foreach (var it in items)
                {
                    int qty = Convert.ToInt32(it["quantity"]);
                    bool isPizza = it["sizeName"] != null;

                    if (isPizza)
                    {
                        _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%Мука%';", 0.25 * qty);
                        _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%Моцарелла%';", 0.15 * qty);
                        _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%томатный%';", 0.08 * qty);

                        string size = it["sizeName"].ToString();
                        if (size.Contains("25")) _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%Коробки%25%';", qty);
                        else if (size.Contains("30")) _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%Коробки%30%';", qty);
                        else if (size.Contains("35")) _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = MAX(0, CurrentStock - ?) WHERE IngredientName LIKE '%Коробки%35%';", qty);
                    }
                }
                EventBus.Publish("inventory_updated", new { message = "Остатки склада обновлены" });
            }
            catch { }
        }

        private object HandleGetOrders(SimpleHttpRequest req)
        {
            string status = req.Query.ContainsKey("status") ? req.Query["status"] : null;
            string uid = req.Query.ContainsKey("userId") ? req.Query["userId"] : null;

            string sql = "SELECT * FROM Orders ";
            var conditions = new List<string>();
            var prms = new List<object>();

            if (!string.IsNullOrEmpty(status))
            {
                conditions.Add("Status = ?");
                prms.Add(status);
            }
            if (!string.IsNullOrEmpty(uid))
            {
                conditions.Add("UserId = ?");
                prms.Add(Convert.ToInt64(uid));
            }

            if (conditions.Count > 0)
            {
                sql += "WHERE " + string.Join(" AND ", conditions.ToArray()) + " ";
            }

            sql += "ORDER BY Id DESC LIMIT 100;";
            var orders = _db.Query(sql, prms.ToArray());

            foreach (var ord in orders)
            {
                long oid = Convert.ToInt64(ord["Id"]);
                ord["items"] = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", oid);
            }

            return orders;
        }

        private object HandleGetOrderDetails(string path)
        {
            string idStr = Uri.UnescapeDataString(path.Replace("/api/orders/", "").Trim());
            List<Dictionary<string, object>> list = null;
            long id;
            if (long.TryParse(idStr.TrimStart('#'), out id))
            {
                list = _db.Query("SELECT * FROM Orders WHERE Id = ? OR OrderNumber = ? OR OrderNumber = ? LIMIT 1;", id, "#" + id, id.ToString());
            }
            if (list == null || list.Count == 0)
            {
                list = _db.Query("SELECT * FROM Orders WHERE OrderNumber = ? LIMIT 1;", idStr);
            }

            if (list == null || list.Count == 0)
            {
                // Поиск по номеру телефона (только цифры)
                string cleanDigits = "";
                foreach (char c in idStr) { if (char.IsDigit(c)) cleanDigits += c; }
                if (cleanDigits.Length >= 4)
                {
                    list = _db.Query(@"
                        SELECT * FROM Orders 
                        WHERE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(CustomerPhone, '+', ''), ' ', ''), '(', ''), ')', ''), '-', '') LIKE ? 
                        ORDER BY Id DESC LIMIT 1;", "%" + cleanDigits + "%");
                }
            }

            if (list == null || list.Count == 0) return new { error = "Заказ не найден" };

            var order = list[0];
            long realId = Convert.ToInt64(order["Id"]);
            order["items"] = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", realId);
            order["id"] = realId;
            order["orderId"] = realId;
            return order;
        }

        private object HandleTrackOrder(SimpleHttpRequest req)
        {
            string q = req.Query.ContainsKey("q") ? req.Query["q"].Trim() : "";
            if (string.IsNullOrEmpty(q))
            {
                return new { error = "Укажите номер заказа или телефон" };
            }
            return HandleGetOrderDetails("/api/orders/" + Uri.EscapeDataString(q));
        }

        private object HandleGetUserOrders(string path)
        {
            string uidStr = path.Replace("/api/orders/user/", "").Trim();
            long uid;
            if (!long.TryParse(uidStr, out uid)) return new List<object>();

            var orders = _db.Query("SELECT * FROM Orders WHERE UserId = ? ORDER BY Id DESC LIMIT 20;", uid);
            foreach (var ord in orders)
            {
                long ordId = Convert.ToInt64(ord["Id"]);
                ord["items"] = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", ordId);
                ord["id"] = ordId;
                ord["orderId"] = ordId;
            }
            return orders;
        }

        private object HandleUpdateOrderStatus(string path, string body, SimpleHttpRequest req)
        {
            string[] parts = path.Split('/');
            long orderId = Convert.ToInt64(parts[3]);

            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string newStatus = data["status"].ToString();

            string callerRole = GetCallerRole(req);
            if (callerRole != null)
            {
                if ((newStatus == "Cooking" || newStatus == "Oven" || newStatus == "Ready") && callerRole != "Chef" && callerRole != "Admin")
                {
                    return new { success = false, forbidden = true, message = "Ошибка 403: Изменение статусов приготовления разрешено только Шеф-повару." };
                }
                if ((newStatus == "OnTheWay" || newStatus == "Delivered") && callerRole != "Courier" && callerRole != "Admin")
                {
                    return new { success = false, forbidden = true, message = "Ошибка 403: Изменение статусов доставки разрешено только Курьеру." };
                }
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string timeCol = null;
            if (newStatus == "Cooking") timeCol = "CookingStartedAt";
            else if (newStatus == "Ready") timeCol = "ReadyAt";
            else if (newStatus == "Delivered") timeCol = "DeliveredAt";

            if (timeCol != null)
            {
                _db.ExecuteNonQuery("UPDATE Orders SET Status = ?, " + timeCol + " = ? WHERE Id = ?;", newStatus, now, orderId);
            }
            else
            {
                _db.ExecuteNonQuery("UPDATE Orders SET Status = ? WHERE Id = ?;", newStatus, orderId);
            }

            var ord = _db.Query("SELECT OrderNumber, OrderType, CustomerName FROM Orders WHERE Id = ?;", orderId);
            string ordNum = ord.Count > 0 ? ord[0]["OrderNumber"].ToString() : "#" + orderId;

            EventBus.Publish("order_status_updated", new
            {
                id = orderId,
                orderNumber = ordNum,
                status = newStatus,
                timestamp = now
            });

            return new { success = true, orderId = orderId, status = newStatus };
        }

        private object HandleGetBoardOrders()
        {
            var cooking = _db.Query("SELECT Id, OrderNumber, OrderType, Status, CreatedAt FROM Orders WHERE Status IN ('Created', 'Cooking', 'Oven') ORDER BY Id ASC LIMIT 16;");
            var ready = _db.Query("SELECT Id, OrderNumber, OrderType, Status, ReadyAt FROM Orders WHERE Status = 'Ready' ORDER BY Id DESC LIMIT 16;");

            return new
            {
                cooking = cooking,
                ready = ready,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };
        }

        private object HandleGetKitchenOrders()
        {
            var orders = _db.Query("SELECT * FROM Orders WHERE Status IN ('Created', 'Cooking', 'Oven') ORDER BY Id ASC;");
            foreach (var ord in orders)
            {
                long oid = Convert.ToInt64(ord["Id"]);
                ord["items"] = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", oid);
            }
            return orders;
        }

        private object HandleGetCourierOrders()
        {
            var orders = _db.Query("SELECT * FROM Orders WHERE OrderType = 'Delivery' AND Status IN ('Ready', 'OnTheWay') ORDER BY Id ASC;");
            foreach (var ord in orders)
            {
                long oid = Convert.ToInt64(ord["Id"]);
                ord["items"] = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", oid);
            }
            return orders;
        }
        #endregion

        #region POS & Receipt Handlers
        private object HandlePosCreateOrder(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            data["orderType"] = data.ContainsKey("orderType") ? data["orderType"] : "Takeout";
            data["customerName"] = data.ContainsKey("customerName") && !string.IsNullOrEmpty(data["customerName"].ToString()) ? data["customerName"].ToString() : "Клиент Кассы";
            data["customerPhone"] = data.ContainsKey("customerPhone") ? data["customerPhone"].ToString() : "-";

            return HandleCreateOrder(_json.Serialize(data));
        }

        private object HandleGetReceipt(string path)
        {
            string idStr = path.Replace("/api/pos/receipt/", "").Trim();
            long id = Convert.ToInt64(idStr);

            var orders = _db.Query("SELECT * FROM Orders WHERE Id = ?;", id);
            if (orders.Count == 0) return new { error = "Чек не найден" };

            var order = orders[0];
            var items = _db.Query("SELECT * FROM OrderItems WHERE OrderId = ?;", id);

            return new
            {
                company = "ООО «Элчос Пицца»",
                inn = "7701984520",
                address = "г. Москва, ул. Центральная, д. 10",
                kktNumber = "000458129034",
                orderNumber = order["OrderNumber"],
                orderType = order["OrderType"],
                createdAt = order["CreatedAt"],
                paymentMethod = order["PaymentMethod"],
                totalAmount = order["TotalAmount"],
                discountAmount = order["DiscountAmount"],
                items = items,
                cashier = "Анна Смирнова (Кассир)",
                taxVat20 = Math.Round(Convert.ToDouble(order["TotalAmount"]) * 20.0 / 120.0, 2)
            };
        }
        #endregion

        #region Inventory Handlers
        private object HandleGetInventory()
        {
            var items = _db.Query("SELECT *, (CurrentStock <= MinStock) as IsLowStock FROM Inventory ORDER BY IsLowStock DESC, IngredientName ASC;");
            return items;
        }

        private object HandleRestock(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            long id = Convert.ToInt64(data["ingredientId"]);
            double amount = Convert.ToDouble(data["amount"]);

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _db.ExecuteNonQuery("UPDATE Inventory SET CurrentStock = CurrentStock + ?, LastRestocked = ? WHERE Id = ?;", amount, now, id);

            EventBus.Publish("inventory_updated", new { ingredientId = id, added = amount });
            return new { success = true };
        }

        private object HandleUpdateInventory(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            long id = Convert.ToInt64(data["id"]);
            string name = data["ingredientName"].ToString();
            string unit = data["unit"].ToString();
            double min = Convert.ToDouble(data["minStock"]);
            double cost = Convert.ToDouble(data["costPerUnit"]);

            _db.ExecuteNonQuery("UPDATE Inventory SET IngredientName = ?, Unit = ?, MinStock = ?, CostPerUnit = ? WHERE Id = ?;", name, unit, min, cost, id);
            return new { success = true };
        }
        #endregion

        #region Analytics Handlers
        private object HandleGetAnalytics()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            var revObj = _db.ExecuteScalar("SELECT SUM(TotalAmount) FROM Orders WHERE Status != 'Cancelled' AND CreatedAt LIKE ?;", today + "%");
            double todayRevenue = revObj != null && revObj != DBNull.Value ? Convert.ToDouble(revObj) : 0;

            var countObj = _db.ExecuteScalar("SELECT COUNT(*) FROM Orders WHERE CreatedAt LIKE ?;", today + "%");
            int todayOrders = countObj != null && countObj != DBNull.Value ? Convert.ToInt32(countObj) : 0;

            double avgCheck = todayOrders > 0 ? Math.Round(todayRevenue / todayOrders, 0) : 0;

            var activeObj = _db.ExecuteScalar("SELECT COUNT(*) FROM Orders WHERE Status IN ('Created', 'Cooking', 'Oven', 'Ready', 'OnTheWay');");
            int activeOrders = activeObj != null && activeObj != DBNull.Value ? Convert.ToInt32(activeObj) : 0;

            var topProducts = _db.Query(@"
                SELECT ProductName, SUM(Quantity) as TotalSold, SUM(TotalPrice) as TotalRevenue
                FROM OrderItems
                GROUP BY ProductName
                ORDER BY TotalSold DESC
                LIMIT 5;");

            var statusCounts = _db.Query(@"
                SELECT Status, COUNT(*) as Count 
                FROM Orders 
                GROUP BY Status;");

            var lowStock = _db.Query("SELECT IngredientName, CurrentStock, MinStock, Unit FROM Inventory WHERE CurrentStock <= MinStock;");

            return new
            {
                todayRevenue = todayRevenue,
                todayOrders = todayOrders,
                avgCheck = avgCheck,
                activeOrders = activeOrders,
                topProducts = topProducts,
                statusCounts = statusCounts,
                lowStockWarnings = lowStock
            };
        }

        private object HandleCheckPromocode(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string code = data.ContainsKey("code") ? data["code"].ToString().Trim().ToUpperInvariant() : "";
            double total = data.ContainsKey("orderTotal") ? Convert.ToDouble(data["orderTotal"]) : 0;

            var list = _db.Query("SELECT * FROM Promocodes WHERE Code = ? AND IsActive = 1;", code);
            if (list.Count == 0)
            {
                return new { valid = false, message = "Промокод не существует или неактивен" };
            }

            var promo = list[0];
            double minTotal = Convert.ToDouble(promo["MinOrderAmount"]);
            if (total < minTotal)
            {
                return new { valid = false, message = "Промокод действует от суммы " + minTotal + " ₽" };
            }

            int percent = Convert.ToInt32(promo["DiscountPercent"]);
            double discount = Math.Round(total * percent / 100.0, 0);

            return new
            {
                valid = true,
                code = code,
                discountPercent = percent,
                discountAmount = discount,
                message = "Промокод применен: скидка " + percent + "% (" + discount + " ₽)"
            };
        }
        #endregion

        #region Shift Handlers (54-ФЗ)
        private object HandleGetCurrentShift()
        {
            var openShifts = _db.Query("SELECT * FROM CashierShifts WHERE Status = 'Open' ORDER BY Id DESC LIMIT 1;");
            if (openShifts.Count == 0)
            {
                return new { hasOpenShift = false, isOpen = false, shift = (object)null };
            }
            var shift = openShifts[0];
            long shiftId = Convert.ToInt64(shift["Id"]);
            string openTime = shift["OpenedAt"].ToString();

            var sales = _db.Query(@"
                SELECT 
                    COUNT(*) as TotalOrders,
                    COALESCE(SUM(TotalAmount), 0) as TotalRevenue,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'Cash' THEN TotalAmount ELSE 0 END), 0) as CashTotal,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'CardOnline' OR PaymentMethod = 'CardTerminal' THEN TotalAmount ELSE 0 END), 0) as CardTotal,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'SBP' THEN TotalAmount ELSE 0 END), 0) as SBPTotal
                FROM Orders 
                WHERE CreatedAt >= ? AND Status != 'Cancelled';", openTime);

            var sData = sales.Count > 0 ? sales[0] : new Dictionary<string, object>();
            long totalOrders = sData.ContainsKey("TotalOrders") ? Convert.ToInt64(sData["TotalOrders"]) : 0;
            double totalRevenue = sData.ContainsKey("TotalRevenue") ? Convert.ToDouble(sData["TotalRevenue"]) : 0;
            double cashSales = sData.ContainsKey("CashTotal") ? Convert.ToDouble(sData["CashTotal"]) : 0;
            double cardSales = sData.ContainsKey("CardTotal") ? Convert.ToDouble(sData["CardTotal"]) : 0;
            double sbpSales = sData.ContainsKey("SBPTotal") ? Convert.ToDouble(sData["SBPTotal"]) : 0;
            double initialCash = Convert.ToDouble(shift["InitialCash"]);
            double cashInDrawer = initialCash + cashSales;

            var shiftObj = new
            {
                Id = shiftId,
                CashierName = shift["CashierName"],
                OpenedAt = openTime,
                InitialCash = initialCash,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                CashSales = cashSales,
                CardSales = cardSales,
                SBPSales = sbpSales,
                CashInDrawer = cashInDrawer
            };

            return new
            {
                hasOpenShift = true,
                isOpen = true,
                shift = shiftObj,
                shiftId = shiftId,
                cashierName = shift["CashierName"],
                openedAt = openTime,
                initialCash = initialCash,
                totalOrders = totalOrders,
                totalRevenue = totalRevenue,
                cashSales = cashSales,
                cardSales = cardSales,
                sbpSales = sbpSales,
                cashInDrawer = cashInDrawer
            };
        }

        private object HandleOpenShift(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            double initialCash = GetDouble(data, "initialCash", 3000.0);
            string cashierName = GetString(data, "cashierName", "Анна Смирнова (Кассир)");
            long cashierId = GetLong(data, "cashierId", 2);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _db.ExecuteNonQuery("UPDATE CashierShifts SET Status = 'Closed', ClosedAt = ? WHERE Status = 'Open';", now);

            long shiftId = _db.InsertAndGetId(@"
                INSERT INTO CashierShifts (CashierId, CashierName, OpenedAt, InitialCash, Status)
                VALUES (?, ?, ?, ?, 'Open');", cashierId, cashierName, now, initialCash);

            _db.ExecuteNonQuery(@"
                INSERT INTO ActivityLogs (UserId, UserName, Action, Details, IpAddress, Timestamp)
                VALUES (?, ?, 'Открытие смены', ?, '127.0.0.1', ?);",
                cashierId, cashierName, "Открыта кассовая смена #" + shiftId + " с разменом " + initialCash + " ₽", now);

            return new { success = true, shiftId = shiftId, message = "Кассовая смена успешно открыта" };
        }

        private object HandleCloseShift(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var openShifts = _db.Query("SELECT * FROM CashierShifts WHERE Status = 'Open' ORDER BY Id DESC LIMIT 1;");
            if (openShifts.Count == 0) return new { success = false, message = "Нет открытой смены" };

            var cur = openShifts[0];
            long shiftId = Convert.ToInt64(cur["Id"]);
            string openTime = cur["OpenedAt"].ToString();

            var sales = _db.Query(@"
                SELECT 
                    COUNT(*) as TotalOrders,
                    COALESCE(SUM(TotalAmount), 0) as TotalRevenue,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'Cash' THEN TotalAmount ELSE 0 END), 0) as CashTotal,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'CardOnline' OR PaymentMethod = 'CardTerminal' THEN TotalAmount ELSE 0 END), 0) as CardTotal,
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'SBP' THEN TotalAmount ELSE 0 END), 0) as SBPTotal
                FROM Orders 
                WHERE CreatedAt >= ? AND Status != 'Cancelled';", openTime);

            int totalOrders = Convert.ToInt32(sales[0]["TotalOrders"]);
            double cashSales = Convert.ToDouble(sales[0]["CashTotal"]);
            double cardSales = Convert.ToDouble(sales[0]["CardTotal"]);
            double sbpSales = Convert.ToDouble(sales[0]["SBPTotal"]);

            _db.ExecuteNonQuery(@"
                UPDATE CashierShifts 
                SET Status = 'Closed', ClosedAt = ?, TotalOrders = ?, CashSales = ?, CardSales = ?, SBPSales = ?
                WHERE Id = ?;", now, totalOrders, cashSales, cardSales, sbpSales, shiftId);

            _db.ExecuteNonQuery(@"
                INSERT INTO ActivityLogs (UserId, UserName, Action, Details, IpAddress, Timestamp)
                VALUES (2, 'Анна Смирнова (Кассир)', 'Закрытие смены (Z-отчет)', ?, '127.0.0.1', ?);",
                "Закрыта смена #" + shiftId + ". Заказов: " + totalOrders + ", Наличные: " + cashSales + " ₽, Безнал: " + (cardSales + sbpSales) + " ₽", now);

            return new
            {
                success = true,
                shiftId = shiftId,
                zReport = new
                {
                    company = "ООО «Элчос Пицца»",
                    inn = "7701984520",
                    kktNumber = "000458129034",
                    shiftNumber = shiftId,
                    cashier = cur["CashierName"],
                    openedAt = openTime,
                    closedAt = now,
                    initialCash = Convert.ToDouble(cur["InitialCash"]),
                    cashSales = cashSales,
                    cardSales = cardSales,
                    sbpSales = sbpSales,
                    totalRevenue = cashSales + cardSales + sbpSales,
                    totalOrders = totalOrders,
                    taxVat20 = Math.Round((cashSales + cardSales + sbpSales) * 20.0 / 120.0, 2)
                }
            };
        }

        private object HandleGetXReport()
        {
            return HandleGetCurrentShift();
        }

        private object HandleGetAllShifts()
        {
            var shifts = _db.Query("SELECT * FROM CashierShifts ORDER BY Id DESC LIMIT 50;");
            return shifts;
        }
        #endregion

        #region Reviews Handlers
        private object HandleGetReviews()
        {
            var list = _db.Query("SELECT * FROM CustomerReviews WHERE IsApproved = 1 ORDER BY Id DESC LIMIT 30;");
            return list;
        }

        private object HandleAddReview(string body)
        {
            var data = _json.Deserialize<Dictionary<string, object>>(body);
            string name = GetString(data, "customerName", "Гость");
            int rating = GetInt(data, "rating", 5);
            if (rating < 1) rating = 1; if (rating > 5) rating = 5;
            string comment = GetString(data, "comment", "");
            string orderNumber = GetString(data, "orderNumber", "");
            long? orderId = null;
            if (data.ContainsKey("orderId") && data["orderId"] != null)
            {
                long oid;
                if (long.TryParse(data["orderId"].ToString(), out oid)) orderId = oid;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            long id = _db.InsertAndGetId(@"
                INSERT INTO CustomerReviews (OrderId, OrderNumber, CustomerName, Rating, Comment, CreatedAt, IsApproved)
                VALUES (?, ?, ?, ?, ?, ?, 1);", orderId, orderNumber, name, rating, comment, now);

            return new { success = true, reviewId = id, message = "Спасибо за ваш отзыв! Нам важно ваше мнение." };
        }
        #endregion

        #region Safe Helper Methods
        private string GetCallerRole(SimpleHttpRequest req)
        {
            if (req == null || req.Headers == null) return null;
            if (req.Headers.ContainsKey("X-User-Role") && !string.IsNullOrEmpty(req.Headers["X-User-Role"]))
            {
                return req.Headers["X-User-Role"].Trim();
            }
            if (req.Headers.ContainsKey("X-User-Id") && !string.IsNullOrEmpty(req.Headers["X-User-Id"]))
            {
                long uid;
                if (long.TryParse(req.Headers["X-User-Id"], out uid))
                {
                    var u = _db.Query("SELECT Role FROM Users WHERE Id = ? LIMIT 1;", uid);
                    if (u.Count > 0) return u[0]["Role"].ToString();
                }
            }
            return null;
        }

        private static string GetString(Dictionary<string, object> dict, string key, string defaultValue = "")
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return defaultValue;
            return dict[key].ToString();
        }

        private static string GetNullableString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return null;
            return dict[key].ToString();
        }

        private static double GetDouble(Dictionary<string, object> dict, string key, double defaultValue = 0)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return defaultValue;
            double val;
            if (double.TryParse(dict[key].ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                return val;
            try { return Convert.ToDouble(dict[key]); } catch { return defaultValue; }
        }

        private static long GetLong(Dictionary<string, object> dict, string key, long defaultValue = 0)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return defaultValue;
            try { return Convert.ToInt64(dict[key]); } catch { return defaultValue; }
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return defaultValue;
            try { return Convert.ToInt32(dict[key]); } catch { return defaultValue; }
        }
        #endregion
    }
}
