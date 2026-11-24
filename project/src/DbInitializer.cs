// DbInitializer.cs - Создание схемы и инициализация ассортиментом Elcho's Pizza
using System;

namespace PizzaSystem.Data
{
    public static class DbInitializer
    {
        public static void Initialize(SQLiteDatabase db)
        {
            // 1. Создание схемы таблиц
            string schema = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT UNIQUE NOT NULL,
                    Phone TEXT,
                    Password TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    Address TEXT,
                    Role TEXT NOT NULL DEFAULT 'Client',
                    CreatedAt TEXT NOT NULL,
                    LastLogin TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Icon TEXT,
                    Slug TEXT,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS PizzaSizes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Diameter INTEGER NOT NULL,
                    Multiplier REAL NOT NULL,
                    WeightGrams INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS DoughTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    PriceAdd REAL NOT NULL DEFAULT 0.0
                );

                CREATE TABLE IF NOT EXISTS Toppings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    WeightGrams INTEGER NOT NULL,
                    Category TEXT,
                    ImageUrl TEXT,
                    IsAvailable INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryId INTEGER NOT NULL REFERENCES Categories(Id),
                    Name TEXT NOT NULL,
                    Description TEXT,
                    ImageUrl TEXT,
                    BasePrice REAL NOT NULL,
                    Badge TEXT,
                    IsPizza INTEGER NOT NULL DEFAULT 0,
                    Calories INTEGER,
                    WeightGrams INTEGER,
                    IsAvailable INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Inventory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IngredientName TEXT NOT NULL,
                    Unit TEXT NOT NULL,
                    CurrentStock REAL NOT NULL,
                    MinStock REAL NOT NULL,
                    CostPerUnit REAL NOT NULL,
                    LastRestocked TEXT
                );

                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderNumber TEXT UNIQUE NOT NULL,
                    OrderType TEXT NOT NULL DEFAULT 'Delivery',
                    UserId INTEGER REFERENCES Users(Id),
                    CustomerName TEXT NOT NULL,
                    CustomerPhone TEXT NOT NULL,
                    DeliveryAddress TEXT,
                    Comment TEXT,
                    PaymentMethod TEXT NOT NULL,
                    PaymentStatus TEXT NOT NULL DEFAULT 'Pending',
                    TotalAmount REAL NOT NULL,
                    DiscountAmount REAL NOT NULL DEFAULT 0,
                    PromoCode TEXT,
                    Status TEXT NOT NULL DEFAULT 'Created',
                    CreatedAt TEXT NOT NULL,
                    CookingStartedAt TEXT,
                    ReadyAt TEXT,
                    DeliveredAt TEXT,
                    CourierId INTEGER REFERENCES Users(Id),
                    TableNumber INTEGER
                );

                CREATE TABLE IF NOT EXISTS OrderItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
                    ProductId INTEGER NOT NULL REFERENCES Products(Id),
                    ProductName TEXT NOT NULL,
                    SizeName TEXT,
                    DoughName TEXT,
                    SelectedToppings TEXT,
                    Quantity INTEGER NOT NULL DEFAULT 1,
                    UnitPrice REAL NOT NULL,
                    TotalPrice REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Promocodes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT UNIQUE NOT NULL,
                    DiscountPercent INTEGER NOT NULL,
                    MinOrderAmount REAL NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS ActivityLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    UserName TEXT,
                    Action TEXT NOT NULL,
                    Details TEXT,
                    IpAddress TEXT,
                    Timestamp TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CashierShifts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CashierId INTEGER,
                    CashierName TEXT NOT NULL,
                    OpenedAt TEXT NOT NULL,
                    ClosedAt TEXT,
                    InitialCash REAL NOT NULL DEFAULT 0,
                    CashSales REAL NOT NULL DEFAULT 0,
                    CardSales REAL NOT NULL DEFAULT 0,
                    SBPSales REAL NOT NULL DEFAULT 0,
                    TotalOrders INTEGER NOT NULL DEFAULT 0,
                    Status TEXT NOT NULL DEFAULT 'Open'
                );

                CREATE TABLE IF NOT EXISTS CustomerReviews (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER,
                    OrderNumber TEXT,
                    CustomerName TEXT NOT NULL,
                    Rating INTEGER NOT NULL,
                    Comment TEXT,
                    CreatedAt TEXT NOT NULL,
                    IsApproved INTEGER NOT NULL DEFAULT 1
                );
            ";

            db.ExecuteScript(schema);

            // Гарантируем наличие отзывов гостей и открытой смены кассира
            var revCheck = db.ExecuteScalar("SELECT COUNT(*) FROM CustomerReviews;");
            if (revCheck == null || Convert.ToInt64(revCheck) == 0)
            {
                string rNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                db.ExecuteNonQuery(@"
                    INSERT INTO CustomerReviews (OrderId, OrderNumber, CustomerName, Rating, Comment, CreatedAt, IsApproved) VALUES
                    (101, '#101', 'Алексей Миронов', 5, 'Самая вкусная Пепперони в городе! Доставили всего за 24 минуты, пицца была обжигающе горячей с тянущимся сыром.', ?, 1),
                    (102, '#102', 'Елена Васильева', 5, 'Регулярно заказываем в офис комбо-наборы. Тесто воздушное, хрустящее, ингредиентов не жалеют. Отдельное спасибо за соус в подарок!', ?, 1),
                    (103, '#103', 'Максим Романов', 5, 'Очень удобный трекер на сайте! Видно каждую стадию: от раскатки теста до курьера. Сервис 10 из 10.', ?, 1);",
                    rNow, rNow, rNow);
            }

            var shiftCheck = db.ExecuteScalar("SELECT COUNT(*) FROM CashierShifts WHERE Status = 'Open';");
            if (shiftCheck == null || Convert.ToInt64(shiftCheck) == 0)
            {
                string sNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                db.ExecuteNonQuery(@"
                    INSERT INTO CashierShifts (CashierId, CashierName, OpenedAt, InitialCash, Status)
                    VALUES (2, 'Анна Смирнова (Кассир)', ?, 5000.0, 'Open');", sNow);
            }

            var catCheck = db.ExecuteScalar("SELECT COUNT(*) FROM Categories;");
            if (catCheck != null && Convert.ToInt64(catCheck) > 0)
            {
                return; // База уже заполнена
            }

            Console.WriteLine("[DB] Инициализация эталонной базы данных Elcho's Pizza...");
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string seedData = string.Format(@"
                -- Пользователи
                INSERT INTO Users (Email, Phone, Password, FullName, Address, Role, CreatedAt, IsActive) VALUES
                ('admin@elchos.ru', '+7 (999) 000-01-01', 'admin123', 'Управляющий Elcho''s Pizza', 'Офис управления, 1', 'Admin', '{0}', 1),
                ('cashier@elchos.ru', '+7 (999) 000-02-02', 'cashier123', 'Анна Смирнова (Кассир)', 'Кассовый узел пиццерии', 'Cashier', '{0}', 1),
                ('chef@elchos.ru', '+7 (999) 000-03-03', 'chef123', 'Иван Ковалев (Шеф-повар)', 'Горячий цех пиццерии', 'Chef', '{0}', 1),
                ('courier@elchos.ru', '+7 (999) 000-04-04', 'courier123', 'Алексей Быстров (Курьер)', 'Отдел логистики и доставки', 'Courier', '{0}', 1),
                ('client@elchos.ru', '+7 (999) 111-22-33', 'client123', 'Петр Клиентов (Постоянный гость)', 'ул. Центральная, д. 45, кв. 12', 'Client', '{0}', 1);

                -- Категории
                INSERT INTO Categories (Name, Icon, Slug, DisplayOrder) VALUES
                ('Пиццы', '🍕', 'pizzas', 1),
                ('Закуски', '🍟', 'snacks', 2),
                ('Десерты', '🍰', 'desserts', 3),
                ('Напитки', '🥤', 'drinks', 4),
                ('Соусы', '🥫', 'sauces', 5),
                ('Комбо', '🎁', 'combos', 6);

                -- Размеры и тесто
                INSERT INTO PizzaSizes (Name, Diameter, Multiplier, WeightGrams) VALUES
                ('Маленькая 25 см', 25, 1.00, 410),
                ('Средняя 30 см', 30, 1.38, 620),
                ('Большая 35 см', 35, 1.76, 880);

                INSERT INTO DoughTypes (Name, PriceAdd) VALUES
                ('Традиционное', 0.0),
                ('Тонкое', 0.0);

                -- Добавки (Кастомизация)
                INSERT INTO Toppings (Name, Price, WeightGrams, Category, ImageUrl) VALUES
                ('Сырный бортик', 199, 120, 'crust', '🧀'),
                ('Дополнительная Моцарелла', 99, 60, 'cheese', '🧀'),
                ('Острый халапеньо', 59, 25, 'veggie', '🌶️'),
                ('Пикантная пепперони', 89, 45, 'meat', '🥓'),
                ('Хрустящий бекон', 89, 40, 'meat', '🥓'),
                ('Сочная ветчина', 79, 50, 'meat', '🍖'),
                ('Филе цыпленка', 89, 60, 'meat', '🍗'),
                ('Свежие шампиньоны', 59, 45, 'veggie', '🍄'),
                ('Сладкий болгарский перец', 49, 40, 'veggie', '🫑'),
                ('Спелые томаты черри', 59, 50, 'veggie', '🍅'),
                ('Маринованные огурчики', 49, 35, 'veggie', '🥒'),
                ('Красный ялтинский лук', 39, 30, 'veggie', '🧅'),
                ('Чесночный ранч соус', 49, 35, 'sauce', '🧄');

                -- ПИЦЦЫ (CategoryId = 1)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (1, 'Пепперони', 'Пикантная пепперони, увеличенная порция сыра моцарелла, фирменный томатный соус Elcho''s', '/img/pizza_1.png', 499, 'Хит', 1, 290, 420),
                (1, 'Elcho''s Special', 'Фирменная пицца: бекон, митболы из говядины, пепперони, моцарелла, томаты, шампиньоны, сладкий перец, красный лук, чеснок', '/img/pizza_2.png', 589, 'Фирменная', 1, 310, 480),
                (1, 'Мясное Барбекю', 'Нежный цыпленок, ветчина, пикантная пепперони, острая чоризо, моцарелла, фирменный соус', '/img/pizza_3.png', 569, 'Хит', 1, 320, 490),
                (1, 'Четыре сыра', 'Сыр блю чиз с благородной плесенью, смесь сыров чеддер и пармезан, моцарелла, сливочный соус', '/img/pizza_4.png', 579, 'Гурмэ', 1, 305, 410),
                (1, 'Сырная Классика', 'Увеличенная порция моцареллы, благородный чеддер и выдержанный пармезан, нежный соус Альфредо', '/img/pizza_5.png', 429, 'Любимая', 1, 275, 390),
                (1, 'Ветчина и грибы', 'Сочная ветчина, свежие шампиньоны, увеличенная порция моцареллы, томатный соус', '/img/pizza_6.png', 469, 'Традиция', 1, 260, 430),
                (1, 'Двойной цыпленок', 'Двойная щедрая порция нежного филе цыпленка, сыр моцарелла, сливочный соус Альфредо', '/img/pizza_7.png', 519, 'Белок', 1, 280, 450),
                (1, 'Чоризо Фреш', 'Острые колбаски чоризо, сладкий болгарский перец, моцарелла, томатный соус', '/img/pizza_8.png', 449, 'Новинка', 1, 285, 420),
                (1, 'Маргарита Итальяна', 'Сочные томаты, увеличенная порция сыра моцарелла, итальянские пряные травы, томатный соус', '/img/pizza_9.png', 419, 'Италия', 1, 240, 410),
                (1, 'Гавайская', 'Нежное филе цыпленка, сладкие ананасы, сыр моцарелла, фирменный томатный соус', '/img/pizza_10.png', 499, 'Экзотика', 1, 255, 440),
                (1, 'Цыпленок Барбекю', 'Филе цыпленка, хрустящий бекон, моцарелла, красный ялтинский лук, дымный соус барбекю', '/img/pizza_11.png', 549, 'Хит', 1, 295, 460),
                (1, 'Карбонара Премиум', 'Сыры моцарелла, чеддер и пармезан, нежный бекон, томаты черри, красный лук, чеснок, сливочный соус', '/img/pizza_12.png', 579, 'Премиум', 1, 315, 460),
                (1, 'Диабло Спайси', 'Острые колбаски чоризо, перчики халапеньо, соус барбекю, митболы, моцарелла, томаты, перец', '/img/pizza_13.png', 599, 'Острая 🔥', 1, 310, 470),
                (1, 'Сырный цыпленок', 'Нежный цыпленок, сыр моцарелла, насыщенный сырный соус, спелые томаты', '/img/pizza_14.png', 539, 'Хит', 1, 285, 440),
                (1, 'Песто Дженовезе', 'Филе цыпленка, ароматный соус песто, нежная брынза кубиками, томаты черри, моцарелла', '/img/pizza_15.png', 569, 'Шедевр', 1, 275, 430);

                -- ЗАКУСКИ (CategoryId = 2)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (2, 'Ролл Элчос классический', 'Легендарная горячая закуска: сочный цыпленок, томаты, сыр моцарелла в тонкой хрустящей пшеничной лепешке с соусом ранч', '/img/snack_16.png', 199, 'Топ продаж', 0, 410, 210),
                (2, 'Ролл Элчос острый', 'Нежный цыпленок, острый халапеньо, маринованные огурчики, моцарелла и пикантный соус', '/img/snack_17.png', 219, 'Острое 🔥', 0, 420, 215),
                (2, 'Сырные палочки с соусом', 'Запеченное хрустящее тесто с тянущимся сыром моцарелла и чесночным соусом', '/img/snack_18.png', 249, 'Хит', 0, 360, 230),
                (2, 'Куриные наггетсы (8 шт)', 'Нежное филе цыпленка в хрустящей золотистой панировке из печи', '/img/snack_19.png', 219, 'Хит', 0, 320, 180),
                (2, 'Картофель из печи с травами', 'Хрустящие горячие дольки картофеля, запеченные с прованскими пряными травами', '/img/snack_20.png', 169, 'Гарнир', 0, 240, 160),
                (2, 'Куриные крылья Барбекю (7 шт)', 'Сочные запеченные крылышки в пикантном маринаде с ароматом копчения барбекю', '/img/snack_21.png', 349, 'Вкусно', 0, 480, 280),
                (2, 'Сырный стартер', 'Много расплавленной моцареллы, пикантный пармезан и соус ранч в горячей тортилье', '/img/snack_22.png', 189, 'Сыр', 0, 350, 160);

                -- ДЕСЕРТЫ (CategoryId = 3)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (3, 'Чизкейк Нью-Йорк', 'Классический американский нежнейший творожный чизкейк на песочном корже', '/img/dessert_23.png', 189, 'Десерт', 0, 310, 100),
                (3, 'Шоколадный фондан', 'Французский шоколадный десерт с хрустящей корочкой и горячим тающим центром', '/img/dessert_24.png', 199, 'Хит', 0, 380, 85),
                (3, 'Маффин Три шоколада', 'Пышный шоколадный кекс с кусочками белого, молочного и темного шоколада', '/img/dessert_25.png', 149, 'Сладкое', 0, 340, 110),
                (3, 'Рулетики с корицей (16 шт)', 'Горячие мини-рулетики из нежного теста с корицей, сахаром и порцией сгущенки', '/img/dessert_26.png', 229, 'Для компании', 0, 460, 200),
                (3, 'Бруслетики со сгущенкой (16 шт)', 'Сладкие горячие рулетики со спелой лесной брусникой и цельным сгущенным молоком', '/img/dessert_27.png', 239, 'Новинка', 0, 440, 200);

                -- НАПИТКИ (CategoryId = 4)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (4, 'Добрый Кола 0.5 л', 'Освежающий газированный напиток с ярким карамельным вкусом', '/img/drink_28.png', 129, 'Освежает', 0, 210, 500),
                (4, 'Добрый Кола без сахара 0.5 л', 'Освежающий вкус с нулем калорий и сахара', '/img/drink_29.png', 129, 'Zero', 0, 0, 500),
                (4, 'Добрый Апельсин 0.5 л', 'Яркий газированный напиток с бодрящим вкусом сочного апельсина', '/img/drink_30.png', 129, 'Фрукты', 0, 190, 500),
                (4, 'Морс Клюквенный натуральный 0.45 л', 'Свежий натуральный морс из спелой лесной клюквы собственного приготовления', '/img/drink_31.png', 159, 'Эко', 0, 140, 450),
                (4, 'Сок Rich Апельсин 0.3 л', '100% апельсиновый натуральный сок с мякотью', '/img/drink_32.png', 139, 'Rich', 0, 130, 300),
                (4, 'Вода Bonaqua без газа 0.5 л', 'Природная артезианская чистая питьевая вода', '/img/drink_33.png', 89, 'Вода', 0, 0, 500),
                (4, 'Кофе Капучино 0.3 л', 'Зерновой кофе из 100% арабики со вспененным молоком', '/img/drink_34.png', 159, 'Кофе', 0, 110, 300),
                (4, 'Кофе Латте 0.3 л', 'Нежный кофейный напиток с пышной молочной пенкой', '/img/drink_35.png', 169, 'Кофе', 0, 130, 300),
                (4, 'Кофе Американо 0.3 л', 'Классический черный зерновой кофе', '/img/drink_36.png', 129, 'Кофе', 0, 10, 300);

                -- СОУСЫ (CategoryId = 5)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (5, 'Соус Сырный Heinz', 'Классический насыщенный сливочно-сырный соус', '/img/sauce_37.png', 49, 'Heinz', 0, 95, 25),
                (5, 'Соус Чесночный Heinz', 'Ароматный сливочно-чесночный соус с травами', '/img/sauce_38.png', 49, 'Heinz', 0, 90, 25),
                (5, 'Соус Барбекю Heinz', 'Пикантный соус с ароматом копчения', '/img/sauce_39.png', 49, 'Heinz', 0, 45, 25),
                (5, 'Соус Кисло-сладкий Heinz', 'Традиционный кисло-сладкий соус со специями', '/img/sauce_40.png', 49, 'Heinz', 0, 50, 25);

                -- КОМБО (CategoryId = 6)
                INSERT INTO Products (CategoryId, Name, Description, ImageUrl, BasePrice, Badge, IsPizza, Calories, WeightGrams) VALUES
                (6, 'Комбо Elcho''s Дуэт', 'Любые две сочные средние пиццы 30 см со скидкой 20%', '/img/combo_41.png', 1099, 'Скидка 20%', 0, 1500, 1200),
                (6, 'Комбо Трио Пицц', 'Три сытные пиццы 30 см для компании с экономией до 600 рублей', '/img/combo_42.png', 1549, 'Хит', 0, 2200, 1800),
                (6, 'Бизнес-Ланч с Роллом', 'Горячий ролл + напиток 0.5 л + соус на выбор', '/img/combo_43.png', 349, 'Ланч', 0, 620, 735),
                (6, 'Elcho''s Party Box', '3 большие пиццы 35 см + 2 напитка 0.5 л + крылья Барбекю', '/img/combo_44.png', 2690, 'Суперсет', 0, 3600, 3100);

                -- Складской учет (Inventory)
                INSERT INTO Inventory (IngredientName, Unit, CurrentStock, MinStock, CostPerUnit, LastRestocked) VALUES
                ('Мука пшеничная высший сорт', 'кг', 145.0, 30.0, 65.0, '{0}'),
                ('Сыр Моцарелла для пиццы', 'кг', 82.5, 20.0, 520.0, '{0}'),
                ('Соус томатный фирменный', 'л', 58.0, 15.0, 180.0, '{0}'),
                ('Колбаса Пепперони острая', 'кг', 34.0, 10.0, 650.0, '{0}'),
                ('Филе цыпленка су-вид', 'кг', 38.0, 12.0, 420.0, '{0}'),
                ('Ветчина сочная мясная', 'кг', 26.5, 8.0, 480.0, '{0}'),
                ('Шампиньоны свежие', 'кг', 21.0, 6.0, 240.0, '{0}'),
                ('Сыр Чеддер тертый', 'кг', 17.5, 5.0, 680.0, '{0}'),
                ('Сыр Пармезан выдержанный', 'кг', 11.0, 4.0, 950.0, '{0}'),
                ('Перчики халапеньо маринованные', 'кг', 9.5, 3.0, 380.0, '{0}'),
                ('Спелые томаты', 'кг', 29.0, 10.0, 190.0, '{0}'),
                ('Ананасы консервированные кусочками', 'кг', 14.0, 5.0, 210.0, '{0}'),
                ('Лепешки тортилья пшеничные', 'уп', 88.0, 25.0, 85.0, '{0}'),
                ('Коробки для пиццы 25 см', 'шт', 240.0, 50.0, 18.0, '{0}'),
                ('Коробки для пиццы 30 см', 'шт', 310.0, 80.0, 22.0, '{0}'),
                ('Коробки для пиццы 35 см', 'шт', 185.0, 40.0, 26.0, '{0}'),
                ('Зерновой кофе Бразилия Сантос', 'кг', 13.5, 4.0, 1400.0, '{0}'),
                ('Молоко пастеризованное 3.2%', 'л', 42.0, 15.0, 78.0, '{0}');

                -- Промокоды
                INSERT INTO Promocodes (Code, DiscountPercent, MinOrderAmount, IsActive) VALUES
                ('ELCHOS20', 20, 800, 1),
                ('ELCHOS15', 15, 1000, 1),
                ('PIZZA10', 10, 500, 1);

                -- Тестовые заказы
                INSERT INTO Orders (Id, OrderNumber, OrderType, UserId, CustomerName, CustomerPhone, DeliveryAddress, PaymentMethod, PaymentStatus, TotalAmount, Status, CreatedAt, CookingStartedAt)
                VALUES (101, '#101', 'Delivery', 5, 'Петр Клиентов', '+7 (999) 111-22-33', 'ул. Центральная, д. 45, кв. 12', 'CardOnline', 'Paid', 1198.0, 'Cooking', '{0}', '{0}');

                INSERT INTO OrderItems (OrderId, ProductId, ProductName, SizeName, DoughName, SelectedToppings, Quantity, UnitPrice, TotalPrice) VALUES
                (101, 1, 'Пепперони', 'Средняя 30 см', 'Традиционное', '[""Сырный бортик""]', 1, 898.0, 898.0),
                (101, 16, 'Ролл Элчос классический', NULL, NULL, '[]', 1, 199.0, 199.0),
                (101, 28, 'Добрый Кола 0.5 л', NULL, NULL, '[]', 1, 129.0, 129.0);

                INSERT INTO Orders (Id, OrderNumber, OrderType, CustomerName, CustomerPhone, PaymentMethod, PaymentStatus, TotalAmount, Status, CreatedAt, ReadyAt, TableNumber)
                VALUES (102, '#102', 'DineIn', 'Мария Соколова', '+7 (917) 555-44-33', 'CardTerminal', 'Paid', 699.0, 'Ready', '{0}', '{0}', 4);

                INSERT INTO OrderItems (OrderId, ProductId, ProductName, SizeName, DoughName, SelectedToppings, Quantity, UnitPrice, TotalPrice) VALUES
                (102, 5, 'Сырная Классика', 'Средняя 30 см', 'Традиционное', '[]', 1, 570.0, 570.0),
                (102, 34, 'Кофе Капучино 0.3 л', NULL, NULL, '[]', 1, 159.0, 159.0);

                INSERT INTO Orders (Id, OrderNumber, OrderType, UserId, CustomerName, CustomerPhone, DeliveryAddress, Comment, PaymentMethod, PaymentStatus, TotalAmount, Status, CreatedAt, CourierId)
                VALUES (103, '#103', 'Delivery', 5, 'Дмитрий Волков', '+7 (905) 777-88-99', 'пр. Ленина, д. 18, подъезд 2, эт. 5, кв. 34', 'Домофон 34', 'Cash', 'Pending', 1549.0, 'OnTheWay', '{0}', 4);

                INSERT INTO OrderItems (OrderId, ProductId, ProductName, SizeName, DoughName, SelectedToppings, Quantity, UnitPrice, TotalPrice) VALUES
                (103, 42, 'Комбо Трио Пицц', '30 см', 'Традиционное', '[]', 1, 1549.0, 1549.0);

                INSERT INTO ActivityLogs (UserId, UserName, Action, Details, IpAddress, Timestamp) VALUES
                (1, 'Управляющий', 'Инициализация БД', 'Корпоративная база данных Elcho''s Pizza успешно запущена', '127.0.0.1', '{0}');
            ", now);

            db.ExecuteScript(seedData);
            Console.WriteLine("[DB] База данных успешно инициализирована: ассортимент Elcho's Pizza готов к работе.");
        }
    }
}
