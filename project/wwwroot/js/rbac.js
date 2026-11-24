// rbac.js - Ролевая модель доступа (Role-Based Access Control) Elcho's Pizza
const ElchosRBAC = (function () {
    const ROLE_TITLES = {
        'Admin': '👑 Управляющий / Администратор',
        'Cashier': '💳 Кассир (POS)',
        'Chef': '👨‍🍳 Шеф-повар (Кухня)',
        'Courier': '🛵 Курьер доставки',
        'Client': '👤 Гость / Клиент'
    };

    const ROLE_HOME = {
        'Admin': '/admin',
        'Cashier': '/pos',
        'Chef': '/kitchen',
        'Courier': '/courier',
        'Client': '/'
    };

    function getUser() {
        try {
            return JSON.parse(localStorage.getItem('elchos_user') || 'null');
        } catch (e) {
            return null;
        }
    }

    function getRole() {
        const u = getUser();
        return u && u.role ? u.role : 'Guest';
    }

    function hasRole(allowedRoles) {
        const role = getRole();
        if (role === 'Admin') return true; // Администратор имеет полный доступ ко всем модулям
        return allowedRoles.includes(role);
    }

    function getRoleTitle(role) {
        return ROLE_TITLES[role] || role || 'Гость';
    }

    async function loginAs(role) {
        try {
            const res = await fetch('/api/auth/demo-login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ role: role })
            }).then(r => r.json());

            if (res.success && res.user) {
                localStorage.setItem('elchos_user', JSON.stringify(res.user));
                window.location.reload();
            } else {
                alert('Ошибка авторизации: ' + (res.message || 'пользователь не найден'));
            }
        } catch (e) {
            alert('Сбой связи с сервером при входе');
        }
    }

    function logout() {
        localStorage.removeItem('elchos_user');
        window.location.href = '/';
    }

    function enforce(allowedRoles, terminalName) {
        const runCheck = () => {
            const user = getUser();
            const role = getRole();

            // Автоматически патчим fetch для передачи заголовков роли
            setupFetchInterceptor();

            // 1. Не авторизован
            if (!user) {
                renderAuthPrompt(terminalName, allowedRoles);
                return;
            }

            // 2. Роль не разрешена
            if (!hasRole(allowedRoles)) {
                renderAccessDenied(terminalName, allowedRoles, user);
                return;
            }

            // 3. Доступ разрешен: добавляем бейдж пользователя и кнопку выхода в шапку
            injectUserHeaderBadge(user);
        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', runCheck);
        } else {
            runCheck();
        }
    }

    function renderAuthPrompt(terminalName, allowedRoles) {
        if (document.getElementById('rbacAuthOverlay')) return;
        const allowedStr = allowedRoles.map(r => ROLE_TITLES[r] || r).join(', ');
        const overlay = document.createElement('div');
        overlay.id = 'rbacAuthOverlay';
        overlay.style.cssText = `
            position: fixed; top: 0; left: 0; width: 100vw; height: 100vh;
            background: rgba(15, 23, 42, 0.96); z-index: 99999;
            display: flex; align-items: center; justify-content: center;
            backdrop-filter: blur(8px); padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: #F8FAFC;
        `;
        overlay.innerHTML = `
            <div style="background: #1E293B; border: 2px solid #334155; border-radius: 20px; max-width: 480px; width: 100%; padding: 32px; box-shadow: 0 25px 50px rgba(0,0,0,0.5); text-align: center;">
                <div style="font-size: 3.5rem; margin-bottom: 12px;">🔒</div>
                <h2 style="font-size: 1.5rem; font-weight: 900; margin-bottom: 8px;">Требуется служебный вход</h2>
                <div style="color: #94A3B8; font-size: 0.95rem; margin-bottom: 20px;">
                    Модуль <strong>«${terminalName}»</strong> предназначен для сотрудников пиццерии (${allowedStr}).
                </div>

                <div style="display: flex; flex-direction: column; gap: 10px; margin-bottom: 24px;">
                    <button onclick="ElchosRBAC.loginAs('Admin')" style="padding: 12px; background: #FF6900; color: white; border: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem; cursor: pointer; transition: all 0.2s;">
                        👔 Войти как Управляющий (Admin)
                    </button>
                    ${allowedRoles.includes('Cashier') ? `
                        <button onclick="ElchosRBAC.loginAs('Cashier')" style="padding: 12px; background: #10B981; color: white; border: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem; cursor: pointer;">
                            💳 Войти как Кассир (Анна Смирнова)
                        </button>
                    ` : ''}
                    ${allowedRoles.includes('Chef') ? `
                        <button onclick="ElchosRBAC.loginAs('Chef')" style="padding: 12px; background: #3B82F6; color: white; border: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem; cursor: pointer;">
                            👨‍🍳 Войти как Шеф-повар (Иван Ковалев)
                        </button>
                    ` : ''}
                    ${allowedRoles.includes('Courier') ? `
                        <button onclick="ElchosRBAC.loginAs('Courier')" style="padding: 12px; background: #F59E0B; color: black; border: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem; cursor: pointer;">
                            🛵 Войти как Курьер (Алексей Быстров)
                        </button>
                    ` : ''}
                </div>

                <div style="border-top: 1px solid #334155; padding-top: 16px;">
                    <a href="/" style="color: #94A3B8; text-decoration: none; font-size: 0.9rem; font-weight: 700;">
                        ← Вернуться на сайт пиццерии
                    </a>
                </div>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    function renderAccessDenied(terminalName, allowedRoles, user) {
        if (document.getElementById('rbacDeniedOverlay')) return;
        const allowedStr = allowedRoles.map(r => ROLE_TITLES[r] || r).join(', ');
        const userRoleTitle = ROLE_TITLES[user.role] || user.role;
        const myHome = ROLE_HOME[user.role] || '/';

        const overlay = document.createElement('div');
        overlay.id = 'rbacDeniedOverlay';
        overlay.style.cssText = `
            position: fixed; top: 0; left: 0; width: 100vw; height: 100vh;
            background: rgba(15, 23, 42, 0.96); z-index: 99999;
            display: flex; align-items: center; justify-content: center;
            backdrop-filter: blur(10px); padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: #F8FAFC;
        `;
        overlay.innerHTML = `
            <div style="background: #1E293B; border: 2px solid #EF4444; border-radius: 20px; max-width: 520px; width: 100%; padding: 36px; box-shadow: 0 25px 50px rgba(239, 68, 68, 0.2); text-align: center;">
                <div style="font-size: 4rem; margin-bottom: 12px;">⛔</div>
                <div style="display: inline-block; background: rgba(239, 68, 68, 0.2); color: #EF4444; padding: 4px 14px; border-radius: 9999px; font-size: 0.82rem; font-weight: 800; margin-bottom: 12px;">
                    ОШИБКА 403: ДОСТУП ОГРАНИЧЕН
                </div>
                <h2 style="font-size: 1.6rem; font-weight: 900; margin-bottom: 12px;">Недостаточно прав доступа</h2>
                
                <div style="background: #0F172A; border-radius: 12px; padding: 16px; margin-bottom: 20px; text-align: left; font-size: 0.92rem; border: 1px solid #334155;">
                    <div style="margin-bottom: 8px;">
                        <span style="color: #94A3B8;">Вы авторизованы как:</span><br>
                        <strong style="color: #FBBF24; font-size: 1.05rem;">${user.fullName}</strong>
                        <span style="color: #94A3B8; font-size: 0.85rem;">(${userRoleTitle})</span>
                    </div>
                    <div style="border-top: 1px dashed #334155; margin: 10px 0;"></div>
                    <div style="color: #CBD5E1; font-size: 0.88rem; line-height: 1.5;">
                        Терминал <strong>«${terminalName}»</strong> закрыт для вашей роли. Доступ имеют только: <strong>${allowedStr}</strong>.
                    </div>
                </div>

                <div style="display: flex; gap: 10px; margin-bottom: 20px;">
                    <a href="${myHome}" style="flex: 1; text-align: center; padding: 14px; background: #3B82F6; color: white; text-decoration: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem;">
                        В мой рабочий раздел →
                    </a>
                    <button onclick="ElchosRBAC.logout()" style="flex: 1; padding: 14px; background: #334155; color: white; border: none; border-radius: 10px; font-weight: 800; font-size: 0.95rem; cursor: pointer;">
                        Сменить аккаунт 🔄
                    </button>
                </div>

                <a href="/" style="color: #94A3B8; text-decoration: none; font-size: 0.85rem; font-weight: 700;">
                    ← На главную витрину пиццерии
                </a>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    function injectUserHeaderBadge(user) {
        if (document.getElementById('rbacUserBadge')) return;
        const pill = document.createElement('div');
        pill.id = 'rbacUserBadge';
        pill.style.cssText = `
            display: inline-flex; align-items: center; gap: 8px;
            background: rgba(255, 255, 255, 0.1); border: 1px solid rgba(255, 255, 255, 0.2);
            padding: 5px 12px; border-radius: 9999px; font-size: 0.8rem; font-weight: 700; color: white;
        `;
        pill.innerHTML = `
            <span>👤 ${user.fullName.split(' ')[0]} <small style="opacity:0.8;">(${ROLE_TITLES[user.role] || user.role})</small></span>
            <button onclick="ElchosRBAC.logout()" title="Выйти / Сменить пользователя" style="background:none; border:none; color:#EF4444; font-weight:800; cursor:pointer; font-size:0.85rem; padding:0 2px;">✕</button>
        `;

        const headerActions = document.querySelector('.pos-header > div:last-child') ||
                              document.querySelector('.kds-header > div:last-child') ||
                              document.querySelector('.admin-nav > div:last-child') ||
                              document.querySelector('.courier-header > div:last-child');
        if (headerActions) {
            headerActions.insertBefore(pill, headerActions.firstChild);
        }
    }

    function setupFetchInterceptor() {
        if (window._rbacFetchIntercepted) return;
        window._rbacFetchIntercepted = true;
        const originalFetch = window.fetch;
        window.fetch = function (url, options = {}) {
            options = options || {};
            options.headers = options.headers || {};
            const user = getUser();
            if (user && user.role) {
                if (typeof Headers !== 'undefined' && options.headers instanceof Headers) {
                    options.headers.set('X-User-Role', user.role);
                    options.headers.set('X-User-Id', user.id);
                } else {
                    options.headers['X-User-Role'] = user.role;
                    options.headers['X-User-Id'] = user.id;
                }
            }
            return originalFetch(url, options);
        };
    }

    // Автоматическая инициализация перехватчика
    setupFetchInterceptor();

    return {
        getUser,
        getRole,
        hasRole,
        getRoleTitle,
        loginAs,
        logout,
        enforce,
        setupFetchInterceptor
    };
})();
