// app.js - Клиентский интерфейс Elcho's Pizza Enterprise Suite
let state = {
    categories: [],
    products: [],
    sizes: [],
    doughTypes: [],
    toppings: [],
    cart: JSON.parse(localStorage.getItem('elchos_cart') || '[]'),
    currentUser: JSON.parse(localStorage.getItem('elchos_user') || 'null'),
    currentPromocode: null,
    discountAmount: 0,
    activeOrder: JSON.parse(localStorage.getItem('elchos_active_order') || 'null'),
    customizingProduct: null,
    customPizzaSize: 'Средняя 30 см',
    customPizzaDough: 'Традиционное',
    customSelectedToppings: [],
    selectedRating: 5,
    reviews: []
};

// Нормализация активного заказа
function getActiveOrderId() {
    if (!state.activeOrder) return null;
    return state.activeOrder.id || state.activeOrder.orderId || state.activeOrder.Id;
}

function setActiveOrder(ord) {
    if (!ord) {
        state.activeOrder = null;
        localStorage.removeItem('elchos_active_order');
        updateTrackerUI();
        updateFloatingTracker();
        return;
    }
    const id = ord.id || ord.orderId || ord.Id;
    const num = ord.orderNumber || ord.OrderNumber || (id ? `#${id}` : '#---');
    const st = ord.status || ord.Status || 'Created';
    const total = ord.totalAmount !== undefined ? ord.totalAmount : (ord.TotalAmount !== undefined ? ord.TotalAmount : 0);
    const items = ord.items || (state.activeOrder && state.activeOrder.items ? state.activeOrder.items : []);
    const addr = ord.deliveryAddress || ord.DeliveryAddress || '';
    const name = ord.customerName || ord.CustomerName || '';
    const phone = ord.customerPhone || ord.CustomerPhone || '';
    const payment = ord.paymentMethod || ord.PaymentMethod || 'CardOnline';
    const paymentSt = ord.paymentStatus || ord.PaymentStatus || 'Paid';
    const type = ord.orderType || ord.OrderType || 'Delivery';
    const created = ord.createdAt || ord.CreatedAt || '';

    state.activeOrder = {
        ...ord,
        id: id,
        orderId: id,
        Id: id,
        orderNumber: num,
        OrderNumber: num,
        status: st,
        Status: st,
        totalAmount: total,
        TotalAmount: total,
        items: items,
        deliveryAddress: addr,
        DeliveryAddress: addr,
        customerName: name,
        CustomerName: name,
        customerPhone: phone,
        CustomerPhone: phone,
        paymentMethod: payment,
        PaymentMethod: payment,
        paymentStatus: paymentSt,
        PaymentStatus: paymentSt,
        orderType: type,
        OrderType: type,
        createdAt: created,
        CreatedAt: created
    };
    localStorage.setItem('elchos_active_order', JSON.stringify(state.activeOrder));
    updateTrackerUI();
    updateFloatingTracker();
}

// Звуковой синтезатор Web Audio API (работает без внешних MP3 на любом ПК)
const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
function playChime(type = 'success') {
    try {
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.connect(gain);
        gain.connect(audioCtx.destination);
        
        if (type === 'success') {
            osc.frequency.setValueAtTime(587.33, audioCtx.currentTime); // D5
            osc.frequency.setValueAtTime(880, audioCtx.currentTime + 0.1); // A5
            gain.gain.setValueAtTime(0.15, audioCtx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.4);
            osc.start();
            osc.stop(audioCtx.currentTime + 0.4);
        } else if (type === 'ding') {
            osc.frequency.setValueAtTime(659.25, audioCtx.currentTime); // E5
            osc.frequency.setValueAtTime(783.99, audioCtx.currentTime + 0.12); // G5
            osc.frequency.setValueAtTime(1046.50, audioCtx.currentTime + 0.25); // C6
            gain.gain.setValueAtTime(0.2, audioCtx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.6);
            osc.start();
            osc.stop(audioCtx.currentTime + 0.6);
        }
    } catch (e) { }
}

// Инициализация при загрузке
document.addEventListener('DOMContentLoaded', async () => {
    updateUserUI();
    updateCartBadge();
    initEventSource();
    await loadInitialData();
    renderCategories();
    renderProducts();
    loadReviews();

    // Проверяем, есть ли активный заказ для трекера
    if (state.activeOrder) {
        checkActiveOrderStatus();
        updateFloatingTracker();
    }
});

// SSE (Server-Sent Events) Живая шина событий
function initEventSource() {
    try {
        const evtSource = new EventSource('/api/events');
        evtSource.onmessage = (e) => {
            try {
                const msg = JSON.parse(e.data);
                if (msg.type === 'order_status_updated') {
                    const activeId = getActiveOrderId();
                    const activeNum = state.activeOrder ? (state.activeOrder.orderNumber || state.activeOrder.OrderNumber) : null;
                    if ((activeId && activeId == msg.data.id) || (activeNum && activeNum == msg.data.orderNumber)) {
                        if (state.activeOrder) {
                            state.activeOrder.status = msg.data.status;
                            state.activeOrder.Status = msg.data.status;
                            localStorage.setItem('elchos_active_order', JSON.stringify(state.activeOrder));
                        }
                        updateTrackerUI();
                        updateFloatingTracker();
                        playChime('ding');
                        showToast(`Статус заказа ${msg.data.orderNumber}: ${getStatusRu(msg.data.status)}`);
                    }
                }
            } catch (err) { }
        };
    } catch (err) {
        console.warn('SSE не поддерживается или ошибка подключения:', err);
    }
}

// Автоматический фоновый опрос статуса активного заказа (каждые 4 секунды)
setInterval(async () => {
    const id = getActiveOrderId();
    if (!id || !state.activeOrder) return;
    const st = state.activeOrder.status || state.activeOrder.Status;
    if (st === 'Delivered' || st === 'Cancelled') return;

    try {
        const order = await fetch(`/api/orders/${id}`).then(r => r.json());
        if (order && !order.error) {
            const oldStatus = state.activeOrder.status || state.activeOrder.Status;
            const newStatus = order.Status || order.status;
            if (oldStatus !== newStatus) {
                setActiveOrder(order);
                playChime('ding');
                showToast(`Статус заказа обновлен: ${getStatusRu(newStatus)}`);
            } else if (!state.activeOrder.items || state.activeOrder.items.length === 0) {
                setActiveOrder(order);
            }
        }
    } catch (e) { }
}, 4000);

// Загрузка данных с бэкенда
async function loadInitialData() {
    try {
        const [catsRes, prodsRes, sizesRes, doughtypesRes, toppingsRes] = await Promise.all([
            fetch('/api/categories').then(r => r.json()),
            fetch('/api/products').then(r => r.json()),
            fetch('/api/sizes').then(r => r.json()),
            fetch('/api/doughtypes').then(r => r.json()),
            fetch('/api/toppings').then(r => r.json())
        ]);
        state.categories = catsRes;
        state.products = prodsRes;
        state.sizes = sizesRes;
        state.doughTypes = doughtypesRes;
        state.toppings = toppingsRes;
    } catch (err) {
        console.error('Ошибка загрузки меню:', err);
    }
}

// Отрисовка категорий в шапке
function renderCategories() {
    const nav = document.getElementById('categoryNav');
    if (!nav) return;
    nav.innerHTML = state.categories.map(c => `
        <a href="#cat-${c.Slug}" class="category-btn" onclick="setActiveCategory(this)">
            ${c.Icon || ''} ${c.Name}
        </a>
    `).join('');
}

function setActiveCategory(btn) {
    document.querySelectorAll('.category-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

// Отрисовка карточек меню
function renderProducts() {
    const container = document.getElementById('menuContainer');
    if (!container) return;

    let html = '';
    state.categories.forEach(cat => {
        const catProducts = state.products.filter(p => p.CategoryId === cat.Id);
        if (catProducts.length === 0) return;

        html += `
            <section class="menu-section" id="cat-${cat.Slug}">
                <div class="section-header">
                    <h2 class="section-title">${cat.Icon || ''} ${cat.Name}</h2>
                    <span class="section-count">${catProducts.length} позиций</span>
                </div>
                <div class="products-grid">
                    ${catProducts.map(p => renderProductCard(p)).join('')}
                </div>
            </section>
        `;
    });

    container.innerHTML = html;
}

function renderProductCard(product) {
    const isPizza = product.IsPizza === 1;
    const badgeHtml = product.Badge ? `<span class="badge-pill">${product.Badge}</span>` : '';
    const imgUrl = product.ImageUrl || '/img/pizza_default.svg';

    // Для пицц выводим кнопку кастомизации
    if (isPizza) {
        return `
            <div class="product-card" data-id="${product.Id}">
                <div class="product-img-wrap" onclick="openPizzaCustomizer(${product.Id})">
                    ${badgeHtml}
                    <img src="${imgUrl}" alt="${product.Name}" class="product-img" loading="lazy" onerror="this.onerror=null; this.src='/img/pizza_default.svg';">
                </div>
                <div class="product-body">
                    <h3 class="product-title" onclick="openPizzaCustomizer(${product.Id})">${product.Name}</h3>
                    <p class="product-desc">${product.Description || ''}</p>
                    <div class="product-footer">
                        <div class="price-tag">от ${product.BasePrice} ₽</div>
                        <button class="btn-add" onclick="openPizzaCustomizer(${product.Id})">
                            Выбрать ⚙️
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    // Для остальных товаров (закуски, напитки, десерты) — прямое добавление
    return `
        <div class="product-card" data-id="${product.Id}">
            <div class="product-img-wrap" onclick="quickAddToCart(${product.Id})">
                ${badgeHtml}
                <img src="${imgUrl}" alt="${product.Name}" class="product-img" loading="lazy" onerror="this.onerror=null; this.src='/img/pizza_default.svg';">
            </div>
            <div class="product-body">
                <h3 class="product-title">${product.Name}</h3>
                <p class="product-desc">${product.Description || ''}</p>
                <div class="product-footer">
                    <div class="price-tag">${product.BasePrice} ₽</div>
                    <button class="btn-add" onclick="quickAddToCart(${product.Id})">
                        + В корзину
                    </button>
                </div>
            </div>
        </div>
    `;
}

// Быстрое добавление простого товара в корзину
function quickAddToCart(productId) {
    const product = state.products.find(p => p.Id === productId);
    if (!product) return;

    const existing = state.cart.find(it => it.productId === productId && !it.sizeName && !it.selectedToppings?.length);
    if (existing) {
        existing.quantity += 1;
    } else {
        state.cart.push({
            productId: product.Id,
            productName: product.Name,
            imageUrl: product.ImageUrl,
            sizeName: null,
            doughName: null,
            selectedToppings: [],
            unitPrice: product.BasePrice,
            quantity: 1
        });
    }

    saveCart();
    playChime('success');
    showToast(`«${product.Name}» добавлен в корзину`);
}

// Кастомизация пиццы
function openPizzaCustomizer(productId) {
    const product = state.products.find(p => p.Id === productId);
    if (!product) return;

    state.customizingProduct = product;
    state.customPizzaSize = 'Средняя 30 см';
    state.customPizzaDough = 'Традиционное';
    state.customSelectedToppings = [];

    document.getElementById('customPizzaTitle').textContent = product.Name;
    document.getElementById('customPizzaDesc').textContent = product.Description;
    document.getElementById('customPizzaImg').src = product.ImageUrl;

    renderCustomToppings();
    updateCustomModalPrice();

    document.getElementById('customPizzaModal').classList.add('open');
}

function setCustomSize(sizeName) {
    state.customPizzaSize = sizeName;
    document.querySelectorAll('#sizeControl .segment-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.size === sizeName);
    });
    updateCustomModalPrice();
}

function setCustomDough(doughName) {
    state.customPizzaDough = doughName;
    document.querySelectorAll('#doughControl .segment-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.dough === doughName);
    });
    updateCustomModalPrice();
}

function renderCustomToppings() {
    const container = document.getElementById('toppingsContainer');
    if (!container) return;

    container.innerHTML = state.toppings.map(t => {
        const isSelected = state.customSelectedToppings.some(top => top.Id === t.Id);
        return `
            <div class="topping-chip ${isSelected ? 'active' : ''}" onclick="toggleCustomTopping(${t.Id})">
                <span class="name">${t.ImageUrl || '🧀'} ${t.Name}</span>
                <span class="price">+${t.Price} ₽</span>
            </div>
        `;
    }).join('');
}

function toggleCustomTopping(toppingId) {
    const topping = state.toppings.find(t => t.Id === toppingId);
    if (!topping) return;

    const idx = state.customSelectedToppings.findIndex(t => t.Id === toppingId);
    if (idx >= 0) {
        state.customSelectedToppings.splice(idx, 1);
    } else {
        state.customSelectedToppings.push(topping);
    }
    renderCustomToppings();
    updateCustomModalPrice();
}

function calculateCustomPrice() {
    if (!state.customizingProduct) return 0;
    let base = state.customizingProduct.BasePrice;

    // Множитель размера
    if (state.customPizzaSize.includes('30')) base = Math.round(base * 1.38);
    else if (state.customPizzaSize.includes('35')) base = Math.round(base * 1.76);

    // Добавки
    const toppingsTotal = state.customSelectedToppings.reduce((sum, t) => sum + t.Price, 0);
    return base + toppingsTotal;
}

function updateCustomModalPrice() {
    const price = calculateCustomPrice();
    const btn = document.getElementById('btnAddToOrderCustom');
    if (btn) {
        btn.textContent = `Добавить в корзину за ${price} ₽`;
    }

    // Расчет калорий и веса
    let weight = 410;
    if (state.customPizzaSize.includes('30')) weight = 620;
    else if (state.customPizzaSize.includes('35')) weight = 880;
    weight += state.customSelectedToppings.reduce((sum, t) => sum + (t.WeightGrams || 40), 0);

    const meta = document.getElementById('customPizzaMeta');
    if (meta) {
        meta.textContent = `${state.customPizzaSize}, ${state.customPizzaDough} тесто • ${weight} г`;
    }
}

function addCustomizedPizzaToCart() {
    const product = state.customizingProduct;
    if (!product) return;

    const unitPrice = calculateCustomPrice();
    const toppingsCopy = state.customSelectedToppings.map(t => t.Name);

    state.cart.push({
        productId: product.Id,
        productName: product.Name,
        imageUrl: product.ImageUrl,
        sizeName: state.customPizzaSize,
        doughName: state.customPizzaDough,
        selectedToppings: toppingsCopy,
        unitPrice: unitPrice,
        quantity: 1
    });

    saveCart();
    playChime('success');
    closeModal('customPizzaModal');
    showToast(`Пицца «${product.Name}» добавлена в заказ`);
}

// КОРЗИНА
function toggleCart() {
    const drawer = document.getElementById('cartDrawer');
    if (!drawer) return;
    drawer.classList.toggle('open');
    renderCart();
}

function renderCart() {
    const list = document.getElementById('cartItemsList');
    const empty = document.getElementById('cartEmpty');
    const footer = document.getElementById('cartFooter');
    if (!list) return;

    if (state.cart.length === 0) {
        list.innerHTML = '';
        if (empty) empty.style.display = 'block';
        if (footer) footer.style.display = 'none';
        return;
    }

    if (empty) empty.style.display = 'none';
    if (footer) footer.style.display = 'flex';

    list.innerHTML = state.cart.map((it, idx) => {
        const metaStr = it.sizeName ? `${it.sizeName}, ${it.doughName}` : '';
        const toppingsStr = it.selectedToppings?.length ? `+ ${it.selectedToppings.join(', ')}` : '';
        return `
            <div class="cart-item">
                <img src="${it.imageUrl || '/img/pizza_default.svg'}" class="cart-item-img" onerror="this.onerror=null; this.src='/img/pizza_default.svg';">
                <div class="cart-item-info">
                    <div class="cart-item-title">${it.productName}</div>
                    ${metaStr ? `<div class="cart-item-meta">${metaStr}</div>` : ''}
                    ${toppingsStr ? `<div class="cart-item-toppings">${toppingsStr}</div>` : ''}
                </div>
                <div class="cart-item-qty">
                    <button class="qty-btn" onclick="changeQty(${idx}, -1)">−</button>
                    <span class="qty-num">${it.quantity}</span>
                    <button class="qty-btn" onclick="changeQty(${idx}, 1)">+</button>
                </div>
                <div class="cart-item-price">${it.unitPrice * it.quantity} ₽</div>
            </div>
        `;
    }).join('');

    updateCartSummary();
}

function changeQty(idx, delta) {
    if (!state.cart[idx]) return;
    state.cart[idx].quantity += delta;
    if (state.cart[idx].quantity <= 0) {
        state.cart.splice(idx, 1);
    }
    saveCart();
    renderCart();
}

function updateCartSummary() {
    const subtotal = state.cart.reduce((sum, it) => sum + (it.unitPrice * it.quantity), 0);
    const subtotalEl = document.getElementById('cartSubtotal');
    const discountEl = document.getElementById('cartDiscount');
    const totalEl = document.getElementById('cartTotal');

    if (subtotalEl) subtotalEl.textContent = `${subtotal} ₽`;

    let discount = 0;
    if (state.currentPromocode) {
        discount = Math.round(subtotal * state.currentPromocode.discountPercent / 100.0);
    }
    state.discountAmount = discount;

    if (discountEl) discountEl.textContent = `− ${discount} ₽`;
    const finalTotal = Math.max(0, subtotal - discount);
    if (totalEl) totalEl.textContent = `${finalTotal} ₽`;
}

function saveCart() {
    localStorage.setItem('elchos_cart', JSON.stringify(state.cart));
    updateCartBadge();
}

function updateCartBadge() {
    const badge = document.getElementById('cartBadge');
    const totalCount = state.cart.reduce((sum, it) => sum + it.quantity, 0);
    const totalPrice = state.cart.reduce((sum, it) => sum + (it.unitPrice * it.quantity), 0);

    if (badge) {
        badge.textContent = totalCount;
        badge.style.display = totalCount > 0 ? 'inline-block' : 'none';
    }

    const priceSpan = document.getElementById('cartHeaderPrice');
    if (priceSpan) {
        priceSpan.textContent = totalCount > 0 ? `| ${totalPrice} ₽` : '';
    }
}

// Применение промокода
async function applyPromocode() {
    const input = document.getElementById('promoInput');
    if (!input || !input.value.trim()) return;

    const code = input.value.trim().toUpperCase();
    const subtotal = state.cart.reduce((sum, it) => sum + (it.unitPrice * it.quantity), 0);

    try {
        const res = await fetch('/api/promocode/check', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code: code, orderTotal: subtotal })
        }).then(r => r.json());

        if (res.valid) {
            state.currentPromocode = res;
            updateCartSummary();
            playChime('ding');
            showToast(res.message);
        } else {
            showToast(res.message, 'error');
        }
    } catch (err) {
        showToast('Ошибка проверки промокода', 'error');
    }
}

// ОФОРМЛЕНИЕ ЗАКАЗА
function openCheckoutModal() {
    if (state.cart.length === 0) {
        showToast('Ваша корзина пуста', 'error');
        return;
    }

    // Предзаполняем данные пользователя
    if (state.currentUser) {
        document.getElementById('checkoutName').value = state.currentUser.fullName || '';
        document.getElementById('checkoutPhone').value = state.currentUser.phone || '';
        document.getElementById('checkoutAddress').value = state.currentUser.address || '';
    }

    const subtotal = state.cart.reduce((sum, it) => sum + (it.unitPrice * it.quantity), 0);
    const total = Math.max(0, subtotal - (state.discountAmount || 0));
    document.getElementById('checkoutTotalBtn').textContent = `Оплатить ${total} ₽`;

    document.getElementById('checkoutModal').classList.add('open');
}

async function submitOrder() {
    const name = document.getElementById('checkoutName').value.trim();
    const phone = document.getElementById('checkoutPhone').value.trim();
    const orderType = document.querySelector('input[name="orderType"]:checked')?.value || 'Delivery';
    const address = document.getElementById('checkoutAddress').value.trim();
    const comment = document.getElementById('checkoutComment').value.trim();
    const payment = document.querySelector('input[name="paymentMethod"]:checked')?.value || 'CardOnline';

    if (!name || !phone) {
        showToast('Укажите ваше имя и контактный телефон', 'error');
        return;
    }

    if (orderType === 'Delivery' && !address) {
        showToast('Укажите адрес доставки (улица, дом, квартира)', 'error');
        return;
    }

    const payload = {
        orderType: orderType,
        userId: state.currentUser ? state.currentUser.id : null,
        customerName: name,
        customerPhone: phone,
        deliveryAddress: address,
        comment: comment,
        paymentMethod: payment,
        promoCode: state.currentPromocode ? state.currentPromocode.code : null,
        discountAmount: state.discountAmount,
        items: state.cart
    };

    try {
        const btn = document.getElementById('checkoutTotalBtn');
        btn.disabled = true;
        btn.textContent = 'Отправка заказа...';

        const res = await fetch('/api/orders', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        }).then(r => r.json());

        if (res.success) {
            playChime('ding');
            const newOrderId = res.orderId || res.id;

            // Формируем полный объект активного заказа
            const fullActiveOrder = {
                ...res,
                id: newOrderId,
                orderId: newOrderId,
                Id: newOrderId,
                customerName: name,
                customerPhone: phone,
                deliveryAddress: address,
                orderType: orderType,
                paymentMethod: payment,
                paymentStatus: (payment === 'CardOnline' || payment === 'SBP') ? 'Paid' : 'Pending',
                items: [...state.cart]
            };
            setActiveOrder(fullActiveOrder);

            // Очищаем корзину
            state.cart = [];
            state.currentPromocode = null;
            state.discountAmount = 0;
            saveCart();

            closeModal('checkoutModal');
            const cartDrawer = document.getElementById('cartDrawer');
            if (cartDrawer) cartDrawer.classList.remove('open');

            showToast(`Заказ ${res.orderNumber} успешно принят! Готовим для вас.`);

            // Открываем модальное окно живого трекера
            openOrderTracker(newOrderId);
        } else {
            showToast(res.message || 'Ошибка оформления заказа', 'error');
        }
    } catch (err) {
        showToast('Ошибка отправки заказа на сервер', 'error');
    } finally {
        const btn = document.getElementById('checkoutTotalBtn');
        if (btn) btn.disabled = false;
    }
}

// ПЛАВАЮЩИЙ МИНИ-ТРЕКЕР
function updateFloatingTracker() {
    const floatEl = document.getElementById('floatingTracker');
    if (!floatEl) return;
    if (!state.activeOrder) {
        floatEl.style.display = 'none';
        return;
    }

    const st = state.activeOrder.status || state.activeOrder.Status;
    if (st === 'Delivered' || st === 'Cancelled') {
        floatEl.style.display = 'none';
        return;
    }

    floatEl.style.display = 'flex';
    const numEl = document.getElementById('floatOrderNumber');
    if (numEl) numEl.textContent = state.activeOrder.orderNumber || state.activeOrder.OrderNumber || '#---';
    const txtEl = document.getElementById('floatStatusText');
    if (txtEl) txtEl.textContent = getStatusRu(st);
}

// ТРЕКЕР ЗАКАЗА
async function openOrderTracker(orderId) {
    if (!orderId) orderId = getActiveOrderId();

    const modal = document.getElementById('trackerModal');
    if (modal) modal.classList.add('open');

    // Если в памяти уже есть данные, сразу отображаем
    if (state.activeOrder) {
        updateTrackerUI();
    }

    if (!orderId) {
        const searchBox = document.getElementById('trackerSearchBox');
        if (searchBox) searchBox.style.display = 'block';
        return;
    }

    try {
        const order = await fetch(`/api/orders/${orderId}`).then(r => r.json());
        if (order && !order.error) {
            setActiveOrder(order);
        } else if (!state.activeOrder) {
            const searchBox = document.getElementById('trackerSearchBox');
            if (searchBox) searchBox.style.display = 'block';
            showToast(order.error || 'Заказ не найден', 'error');
        }
    } catch (e) {
        console.warn('Ошибка загрузки трекера:', e);
    }
}

function toggleTrackerSearch() {
    const box = document.getElementById('trackerSearchBox');
    if (!box) return;
    box.style.display = box.style.display === 'none' ? 'block' : 'none';
}

async function searchAndTrackOrder() {
    const input = document.getElementById('trackerSearchInput');
    if (!input) return;
    const q = input.value.trim();
    if (!q) {
        showToast('Введите номер заказа (#105) или телефон', 'error');
        return;
    }

    try {
        const order = await fetch(`/api/orders/track?q=${encodeURIComponent(q)}`).then(r => r.json());
        if (order && !order.error) {
            setActiveOrder(order);
            const box = document.getElementById('trackerSearchBox');
            if (box) box.style.display = 'none';
            playChime('success');
            showToast(`Заказ ${order.OrderNumber || order.orderNumber} найден!`);
        } else {
            showToast(order.error || 'Заказ не найден', 'error');
        }
    } catch (e) {
        showToast('Ошибка при поиске заказа', 'error');
    }
}

function updateTrackerUI() {
    if (!state.activeOrder) return;
    const ord = state.activeOrder;

    const numEl = document.getElementById('trackerOrderNumber');
    if (numEl) numEl.textContent = ord.orderNumber || ord.OrderNumber || '#---';

    const typeBadge = document.getElementById('trackerTypeBadge');
    const isDelivery = (ord.orderType || ord.OrderType) === 'Delivery';
    if (typeBadge) {
        typeBadge.textContent = isDelivery ? '🛵 Доставка курьером' : '🛍️ Самовывоз из пиццерии';
    }

    const st = ord.status || ord.Status || 'Created';
    const statusTextEl = document.getElementById('trackerStatusDesc');
    if (statusTextEl) statusTextEl.textContent = getStatusRu(st);

    const subDescEl = document.getElementById('trackerSubDesc');
    if (subDescEl) subDescEl.textContent = getStatusSubText(st);

    const timerEta = document.getElementById('trackerTimerEta');
    if (timerEta) {
        if (st === 'Delivered') timerEta.textContent = 'Доставлен!';
        else if (st === 'OnTheWay') timerEta.textContent = '~10-15 мин';
        else if (st === 'Ready') timerEta.textContent = isDelivery ? '~15-20 мин' : 'Готов к выдаче!';
        else if (st === 'Oven') timerEta.textContent = '~20-25 мин';
        else timerEta.textContent = '~30-35 мин';
    }

    const transitLabel = document.getElementById('stepLabelTransit');
    if (transitLabel) {
        transitLabel.textContent = isDelivery ? 'В пути' : 'Выдача';
    }

    // Подсветка шагов
    const steps = ['Created', 'Cooking', 'Oven', 'Ready', 'OnTheWay', 'Delivered'];
    const curIdx = steps.indexOf(st);

    steps.forEach((s, idx) => {
        const el = document.getElementById(`step-${s}`);
        if (!el) return;
        el.classList.remove('completed', 'active');
        if (idx < curIdx || (st === 'Delivered' && idx <= curIdx)) {
            el.classList.add('completed');
        } else if (idx === curIdx) {
            el.classList.add('active');
        }
    });

    // Детали заказа
    const itemsList = document.getElementById('trackerItemsList');
    const items = ord.items || [];
    if (itemsList) {
        if (items.length === 0) {
            itemsList.innerHTML = '<div style="color:#94A3B8; font-size:0.85rem;">Товары в заказе загружаются...</div>';
        } else {
            itemsList.innerHTML = items.map(it => {
                let toppingsArr = [];
                try { toppingsArr = JSON.parse(it.SelectedToppings || it.selectedToppings || '[]'); } catch(e){}
                const toppingsStr = toppingsArr.length ? ` (+ ${toppingsArr.join(', ')})` : '';
                const sizeStr = it.SizeName || it.sizeName ? ` (${it.SizeName || it.sizeName}${it.DoughName || it.doughName ? ', ' + (it.DoughName || it.doughName) : ''})` : '';
                const name = it.ProductName || it.productName || 'Пицца';
                const qty = it.Quantity || it.quantity || 1;
                const total = it.TotalPrice || it.totalPrice || (it.UnitPrice || it.unitPrice || 0) * qty;

                return `
                    <div style="display:flex; justify-content:space-between; align-items:center; font-size:0.88rem; background:white; padding:8px 12px; border-radius:8px; border:1px solid #E2E8F0;">
                        <div>
                            <span style="font-weight:700; color:#1E293B;">${name}</span>
                            <span style="color:#64748B; font-size:0.8rem;">${sizeStr}${toppingsStr}</span>
                            <span style="color:var(--primary); font-weight:800; margin-left:6px;">× ${qty}</span>
                        </div>
                        <span style="font-weight:800; color:#1E293B;">${total} ₽</span>
                    </div>
                `;
            }).join('');
        }
    }

    const itemsCountEl = document.getElementById('trackerItemsCount');
    if (itemsCountEl) itemsCountEl.textContent = `${items.length} ${getPluralItems(items.length)}`;

    const addrEl = document.getElementById('trackerDeliveryAddress');
    if (addrEl) addrEl.textContent = isDelivery ? (ord.deliveryAddress || ord.DeliveryAddress || 'Адрес не указан') : 'Самовывоз: г. Москва, ул. Центральная, д. 10';

    const custEl = document.getElementById('trackerCustomerName');
    if (custEl) custEl.textContent = `${ord.customerName || ord.CustomerName || 'Гость'} (${ord.customerPhone || ord.CustomerPhone || ''})`;

    const payEl = document.getElementById('trackerPaymentMethod');
    if (payEl) {
        const pm = ord.paymentMethod || ord.PaymentMethod;
        const ps = ord.paymentStatus || ord.PaymentStatus;
        let pmRu = pm === 'Cash' ? 'Наличными при получении' : (pm === 'SBP' ? 'СБП по QR-коду' : 'Банковской картой онлайн');
        let psRu = ps === 'Paid' ? ' (Оплачено ✅)' : ' (Ожидает оплаты ⏳)';
        payEl.textContent = pmRu + psRu;
    }

    const totalEl = document.getElementById('trackerTotalAmount');
    if (totalEl) totalEl.textContent = `${ord.totalAmount || ord.TotalAmount || 0} ₽`;

    // Кнопка отзыва
    const reviewBox = document.getElementById('trackerReviewAction');
    if (reviewBox) {
        reviewBox.style.display = st === 'Delivered' ? 'block' : 'none';
    }
}

function getStatusSubText(status) {
    switch (status) {
        case 'Created': return 'Заказ принят системой, передан на кухню и встал в очередь выпекания.';
        case 'Cooking': return 'Шеф-повар раскатывает свежее тесто и щедро укладывает ингредиенты.';
        case 'Oven': return 'Пицца выпекается при 300°C на подовом конвейере до идеальной хрустящей корочки.';
        case 'Ready': return 'Заказ бережно проверен шеф-контролером и упакован в термосумку.';
        case 'OnTheWay': return 'Курьер уже мчит по указанному адресу. Пицца приедет обжигающе горячей!';
        case 'Delivered': return 'Заказ успешно вручен! Желаем вам приятного аппетита и отличного дня.';
        case 'Cancelled': return 'Заказ был отменен. Приносим свои извинения за неудобства.';
        default: return 'Мы готовим ваш заказ со всей заботой и соблюдением стандартов.';
    }
}

function getPluralItems(n) {
    if (n % 10 === 1 && n % 100 !== 11) return 'позиция';
    if ([2, 3, 4].includes(n % 10) && ![12, 13, 14].includes(n % 100)) return 'позиции';
    return 'позиций';
}

async function checkActiveOrderStatus() {
    const id = getActiveOrderId();
    if (!id || !state.activeOrder) return;

    try {
        const order = await fetch(`/api/orders/${id}`).then(r => r.json());
        if (order && !order.error) {
            setActiveOrder(order);
        }
    } catch (e) { }
}

function getStatusRu(status) {
    switch (status) {
        case 'Created': return 'Заказ принят и передан на кухню';
        case 'Cooking': return 'Шеф-повар раскатывает тесто и собирает пиццу';
        case 'Oven': return 'Пицца выпекается в печи при 300°C';
        case 'Ready': return 'Заказ собран и упакован в термосумку';
        case 'OnTheWay': return 'Курьер мчит к вам с горячим заказом!';
        case 'Delivered': return 'Заказ успешно доставлен. Приятного аппетита!';
        case 'Cancelled': return 'Заказ отменен';
        default: return status;
    }
}

// АВТОРИЗАЦИЯ И КОРПОРАТИВНЫЙ ВХОД В СИСТЕМУ
function openAuthModal() {
    document.getElementById('authModal').classList.add('open');
}

function switchAuthTab(tab) {
    document.querySelectorAll('.auth-tab').forEach(t => t.classList.remove('active'));
    document.getElementById(`tab-${tab}`).classList.add('active');
    document.getElementById('loginForm').style.display = tab === 'login' ? 'block' : 'none';
    document.getElementById('registerForm').style.display = tab === 'register' ? 'block' : 'none';
}

async function handleLoginSubmit() {
    const login = document.getElementById('authLogin').value.trim();
    const pass = document.getElementById('authPass').value.trim();

    if (!login || !pass) {
        showToast('Введите логин и пароль', 'error');
        return;
    }

    try {
        const res = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ login: login, password: pass })
        }).then(r => r.json());

        if (res.success) {
            state.currentUser = res.user;
            localStorage.setItem('elchos_user', JSON.stringify(res.user));
            updateUserUI();
            closeModal('authModal');
            playChime('success');
            showToast(`Добро пожаловать, ${res.user.fullName}!`);
        } else {
            showToast(res.message, 'error');
        }
    } catch (e) {
        showToast('Ошибка подключения к серверу', 'error');
    }
}

async function handleRegisterSubmit() {
    const name = document.getElementById('regName').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const phone = document.getElementById('regPhone').value.trim();
    const pass = document.getElementById('regPass').value.trim();

    if (!name || !email || !pass) {
        showToast('Заполните ФИО, Email и пароль', 'error');
        return;
    }

    try {
        const res = await fetch('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fullName: name, email: email, phone: phone, password: pass })
        }).then(r => r.json());

        if (res.success) {
            state.currentUser = res.user;
            localStorage.setItem('elchos_user', JSON.stringify(res.user));
            updateUserUI();
            closeModal('authModal');
            playChime('success');
            showToast(`Регистрация успешна! Добро пожаловать.`);
        } else {
            showToast(res.message, 'error');
        }
    } catch (e) {
        showToast('Ошибка регистрации', 'error');
    }
}

// Быстрый переключатель учетной записи сотрудника
async function demoLogin(role) {
    try {
        const res = await fetch('/api/auth/demo-login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ role: role })
        }).then(r => r.json());

        if (res.success) {
            state.currentUser = res.user;
            localStorage.setItem('elchos_user', JSON.stringify(res.user));
            updateUserUI();
            closeModal('authModal');
            closeModal('profileModal');
            playChime('ding');
            showToast(`Вход выполнен: ${res.user.fullName} (${res.user.role})`);
        }
    } catch (e) { }
}

function logoutUser() {
    state.currentUser = null;
    localStorage.removeItem('elchos_user');
    updateUserUI();
    closeModal('profileModal');
    showToast('Вы вышли из учетной записи');
}

function updateUserUI() {
    const userBtn = document.getElementById('userBtn');
    if (!userBtn) return;

    if (state.currentUser) {
        userBtn.innerHTML = `👤 ${state.currentUser.fullName.split(' ')[0]} (${state.currentUser.role})`;
        userBtn.onclick = openProfileModal;
    } else {
        userBtn.innerHTML = `👤 Войти`;
        userBtn.onclick = openAuthModal;
    }

    updateRbacNavigation();
}

function updateRbacNavigation() {
    const user = state.currentUser;
    const role = user ? user.role : 'Guest';

    // 1. Быстрая кнопка рабочего модуля в шапке сайта
    let roleActionBtn = document.getElementById('roleActionBtn');
    if (!roleActionBtn) {
        roleActionBtn = document.createElement('a');
        roleActionBtn.id = 'roleActionBtn';
        roleActionBtn.className = 'history-btn';
        roleActionBtn.style.display = 'none';
        roleActionBtn.style.textDecoration = 'none';
        roleActionBtn.target = '_blank';
        const actionsBox = document.querySelector('.header-actions');
        if (actionsBox) actionsBox.insertBefore(roleActionBtn, actionsBox.firstChild);
    }

    if (role === 'Admin') {
        roleActionBtn.style.display = 'inline-flex';
        roleActionBtn.href = '/admin';
        roleActionBtn.innerHTML = '📊 Панель управления';
        roleActionBtn.style.borderColor = '#FF6900';
        roleActionBtn.style.color = '#FF6900';
    } else if (role === 'Cashier') {
        roleActionBtn.style.display = 'inline-flex';
        roleActionBtn.href = '/pos';
        roleActionBtn.innerHTML = '🖥️ Касса POS';
        roleActionBtn.style.borderColor = '#10B981';
        roleActionBtn.style.color = '#10B981';
    } else if (role === 'Chef') {
        roleActionBtn.style.display = 'inline-flex';
        roleActionBtn.href = '/kitchen';
        roleActionBtn.innerHTML = '👨‍🍳 Кухня KDS';
        roleActionBtn.style.borderColor = '#3B82F6';
        roleActionBtn.style.color = '#3B82F6';
    } else if (role === 'Courier') {
        roleActionBtn.style.display = 'inline-flex';
        roleActionBtn.href = '/courier';
        roleActionBtn.innerHTML = '🛵 Доставка';
        roleActionBtn.style.borderColor = '#F59E0B';
        roleActionBtn.style.color = '#F59E0B';
    } else {
        roleActionBtn.style.display = 'none';
    }

    // 2. Служебные полосы вверху сайта
    const navLinks = document.querySelectorAll('#rbacNavLinks a[data-allowed]');
    navLinks.forEach(link => {
        const allowed = link.getAttribute('data-allowed').split(',');
        const isAllowed = role === 'Admin' || allowed.includes(role);
        if (isAllowed) {
            link.style.opacity = '1';
            link.style.filter = 'none';
            link.style.pointerEvents = 'auto';
            link.onclick = null;
        } else {
            link.style.opacity = '0.45';
            link.style.filter = 'grayscale(80%)';
            link.onclick = (e) => {
                e.preventDefault();
                showToast(`Доступ закрыт: модуль доступен только для ${allowed.join(', ')}`, 'error');
            };
        }
    });
}

async function openProfileModal() {
    if (!state.currentUser) {
        openAuthModal();
        return;
    }

    const modal = document.getElementById('profileModal');
    if (!modal) return;

    document.getElementById('profileName').textContent = state.currentUser.fullName;
    document.getElementById('profilePhone').textContent = state.currentUser.phone || 'Телефон не указан';
    document.getElementById('profileEmail').textContent = state.currentUser.email || '';

    const roleBadge = document.getElementById('profileRoleBadge');
    if (roleBadge) {
        roleBadge.textContent = state.currentUser.role;
        roleBadge.style.background = state.currentUser.role === 'Admin' ? '#FF6900' :
            (state.currentUser.role === 'Cashier' ? '#10B981' :
            (state.currentUser.role === 'Chef' ? '#3B82F6' :
            (state.currentUser.role === 'Courier' ? '#F59E0B' : '#64748B')));
    }

    modal.classList.add('open');
    loadUserOrderHistory();
}

async function loadUserOrderHistory() {
    const listEl = document.getElementById('profileOrdersList');
    if (!listEl) return;
    if (!state.currentUser) return;

    try {
        const orders = await fetch(`/api/orders/user/${state.currentUser.id}`).then(r => r.json());
        const countEl = document.getElementById('profileOrdersCount');
        if (countEl) countEl.textContent = `${orders.length} ${getPluralItems(orders.length)}`;

        if (!orders || orders.length === 0) {
            listEl.innerHTML = `
                <div style="text-align:center; padding:30px; color:#94A3B8;">
                    <div style="font-size:2rem; margin-bottom:8px;">🍕</div>
                    У вас пока нет оформленных заказов.<br>Сделайте ваш первый заказ прямо сейчас!
                </div>
            `;
            return;
        }

        listEl.innerHTML = orders.map(ord => {
            const items = ord.items || [];
            const itemsSummary = items.map(it => `${it.ProductName} × ${it.Quantity}`).join(', ');
            const st = ord.Status;
            const isCompleted = st === 'Delivered';

            return `
                <div class="history-order-card">
                    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;">
                        <div>
                            <span style="font-weight:800; font-size:1rem; color:#1E293B;">Заказ ${ord.OrderNumber}</span>
                            <span style="font-size:0.8rem; color:#64748B; margin-left:8px;">${ord.CreatedAt}</span>
                        </div>
                        <span class="badge" style="background:${isCompleted ? '#ECFDF5' : '#FFEDD5'}; color:${isCompleted ? '#10B981' : '#EA580C'}; font-size:0.8rem;">
                            ${getStatusRu(st)}
                        </span>
                    </div>
                    <div style="font-size:0.85rem; color:#475569; margin-bottom:10px; line-height:1.4;">
                        ${itemsSummary}
                    </div>
                    <div style="display:flex; justify-content:space-between; align-items:center; border-top:1px dashed #CBD5E1; padding-top:8px;">
                        <span style="font-weight:900; color:var(--primary); font-size:1rem;">${ord.TotalAmount} ₽</span>
                        <div style="display:flex; gap:8px;">
                            <button class="defense-pill" onclick="openOrderTracker(${ord.Id})" style="padding:6px 12px; font-size:0.8rem;">
                                📦 Трекер
                            </button>
                            <button class="defense-pill" onclick="repeatOrder(${ord.Id})" style="padding:6px 12px; font-size:0.8rem; background:var(--primary); color:white;">
                                🔁 Повторить
                            </button>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    } catch (e) {
        listEl.innerHTML = '<div style="color:red; text-align:center;">Ошибка загрузки истории</div>';
    }
}

async function repeatOrder(orderId) {
    try {
        const order = await fetch(`/api/orders/${orderId}`).then(r => r.json());
        if (!order || !order.items || order.items.length === 0) {
            showToast('Не удалось загрузить состав заказа', 'error');
            return;
        }

        order.items.forEach(it => {
            let toppingsArr = [];
            try { toppingsArr = JSON.parse(it.SelectedToppings || '[]'); } catch(e){}

            state.cart.push({
                cartItemId: 'item_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4),
                productId: it.ProductId,
                productName: it.ProductName,
                sizeName: it.SizeName || null,
                doughName: it.DoughName || null,
                selectedToppings: toppingsArr,
                unitPrice: it.UnitPrice,
                quantity: it.Quantity
            });
        });

        saveCart();
        closeModal('profileModal');
        toggleCart();
        playChime('success');
        showToast('Блюда добавлены в корзину! Проверьте состав перед оформлением.');
    } catch (e) {
        showToast('Ошибка повторения заказа', 'error');
    }
}

// ОТЗЫВЫ И КОНТРОЛЬ КАЧЕСТВА
async function loadReviews() {
    try {
        const reviews = await fetch('/api/reviews').then(r => r.json());
        state.reviews = reviews || [];
        renderReviews();
    } catch (e) { }
}

function renderReviews() {
    const grid = document.getElementById('reviewsGrid');
    if (!grid) return;

    if (!state.reviews || state.reviews.length === 0) {
        grid.innerHTML = '<div style="color:#94A3B8; grid-column: 1/-1; text-align:center; padding:30px;">Пока нет отзывов. Будьте первыми!</div>';
        return;
    }

    grid.innerHTML = state.reviews.map(rev => {
        const stars = '⭐'.repeat(rev.Rating || 5);
        return `
            <div class="review-card">
                <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;">
                    <div style="font-weight:800; color:#1E293B;">${rev.CustomerName || 'Гость'}</div>
                    <div style="font-size:0.8rem; color:#94A3B8;">${rev.CreatedAt ? rev.CreatedAt.split(' ')[0] : ''}</div>
                </div>
                <div style="margin-bottom:8px; font-size:1rem;">${stars}</div>
                <p style="font-size:0.88rem; color:#475569; line-height:1.5;">${rev.Comment || 'Отличная пицца, очень вкусно!'}</p>
                ${rev.OrderNumber ? `<div style="font-size:0.75rem; color:#94A3B8; margin-top:8px;">Заказ ${rev.OrderNumber} • Проверенный отзыв ✅</div>` : ''}
            </div>
        `;
    }).join('');
}

function setRating(r) {
    state.selectedRating = r;
    const labels = [
        '',
        'Плохо (1 из 5)',
        'Так себе (2 из 5)',
        'Нормально (3 из 5)',
        'Хорошо, понравилось (4 из 5)',
        'Отлично! Очень вкусно (5 из 5) ⭐'
    ];
    const lbl = document.getElementById('ratingLabel');
    if (lbl) lbl.textContent = labels[r];

    const starBtns = document.querySelectorAll('.star-btn');
    starBtns.forEach((btn, idx) => {
        btn.style.opacity = idx < r ? '1' : '0.25';
        btn.style.filter = idx < r ? 'none' : 'grayscale(100%)';
    });
}

function openReviewModalDirect() {
    state.selectedRating = 5;
    setRating(5);
    const m = document.getElementById('reviewModal');
    if (m) m.classList.add('open');
}

function openReviewModalFromTracker() {
    openReviewModalDirect();
    if (state.activeOrder) {
        const num = state.activeOrder.orderNumber || state.activeOrder.OrderNumber;
        const name = state.activeOrder.customerName || state.activeOrder.CustomerName;
        const nameInput = document.getElementById('reviewCustomerName');
        if (nameInput && name) nameInput.value = name;
    }
}

async function submitCustomerReview() {
    const comment = document.getElementById('reviewComment').value.trim();
    const name = document.getElementById('reviewCustomerName').value.trim() || (state.currentUser ? state.currentUser.fullName : 'Гость');
    const orderNum = state.activeOrder ? (state.activeOrder.orderNumber || state.activeOrder.OrderNumber) : '';
    const orderId = getActiveOrderId();

    try {
        const res = await fetch('/api/reviews', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                customerName: name,
                rating: state.selectedRating || 5,
                comment: comment,
                orderNumber: orderNum,
                orderId: orderId
            })
        }).then(r => r.json());

        if (res.success) {
            closeModal('reviewModal');
            playChime('success');
            showToast('Спасибо за ваш отзыв! Он опубликован на сайте.');
            loadReviews();
            document.getElementById('reviewComment').value = '';
        }
    } catch (e) {
        showToast('Ошибка отправки отзыва', 'error');
    }
}

// УТИЛИТЫ МОДАЛОК И TOAST
function closeModal(modalId) {
    const m = document.getElementById(modalId);
    if (m) m.classList.remove('open');
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer') || createToastContainer();
    const toast = document.createElement('div');
    toast.className = `toast-msg ${type}`;
    toast.style.cssText = `
        background: ${type === 'error' ? '#EF4444' : '#18181B'};
        color: white;
        padding: 12px 20px;
        border-radius: 10px;
        margin-top: 8px;
        font-size: 0.92rem;
        font-weight: 600;
        box-shadow: 0 10px 25px rgba(0,0,0,0.25);
        animation: slideIn 0.3s ease;
        display: flex;
        align-items: center;
        gap: 10px;
    `;
    toast.innerHTML = `<span>${type === 'error' ? '⚠️' : '🍕'}</span> ${message}`;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(-10px)';
        toast.style.transition = 'all 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

function createToastContainer() {
    const c = document.createElement('div');
    c.id = 'toastContainer';
    c.style.cssText = 'position: fixed; bottom: 24px; right: 24px; z-index: 9999; display: flex; flex-direction: column-reverse;';
    document.body.appendChild(c);
    return c;
}
