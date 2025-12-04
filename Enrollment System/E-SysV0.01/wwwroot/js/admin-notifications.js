// guard to prevent double-init if script included more than once
if (window.__adminNotificationsInitialized) {
    console.warn('admin-notifications: already initialized');
} else {
    window.__adminNotificationsInitialized = true;

    (function () {
        const btn = document.getElementById('notifBtn');
        const panel = document.getElementById('notifPanel');
        const badge = document.getElementById('notifBadge');
        const list = document.getElementById('notifList');
        const emptyEl = document.getElementById('notifEmpty');
        const btnClear = document.getElementById('clearNotif');
        const btnMarkRead = document.getElementById('markAllRead');
        const chkSound = document.getElementById('settingSound');
        const chkBrowser = document.getElementById('settingBrowser');
        const audio = document.getElementById('notifSound'); // fallback only

        const itemsKey = 'adminNotifItems.v1';
        const settingsKey = 'adminNotifSettings.v1';

        let settings = { sound: true, browser: false };
        try { settings = Object.assign(settings, JSON.parse(localStorage.getItem(settingsKey) || '{}')); } catch { /* ignore */ }
        if (chkSound) chkSound.checked = !!settings.sound;
        if (chkBrowser) chkBrowser.checked = !!settings.browser;

        chkSound?.addEventListener('change', () => saveSettings({ sound: chkSound.checked }));
        chkBrowser?.addEventListener('change', () => {
            if (chkBrowser.checked && 'Notification' in window && Notification.permission !== 'granted') {
                Notification.requestPermission().then((p) => {
                    saveSettings({ browser: p === 'granted' });
                    chkBrowser.checked = (p === 'granted');
                }).catch(() => saveSettings({ browser: false }));
            } else {
                saveSettings({ browser: chkBrowser.checked });
            }
        });
        function saveSettings(partial) {
            Object.assign(settings, partial);
            try { localStorage.setItem(settingsKey, JSON.stringify(settings)); } catch { /* ignore storage errors */ }
        }

        // Web Audio beep (reliable after any user interaction)
        let audioCtx = null;
        let audioReady = false;
        function initAudio() {
            if (window.__adminAudioPrimed) return;
            try {
                if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                if (audioCtx.state === 'suspended') audioCtx.resume().catch(() => { });
                audioReady = true;
                window.__adminAudioPrimed = true;
            } catch {
                audioReady = false;
            }
        }
        // prime on first user gesture anywhere (kept as backup)
        window.addEventListener('pointerdown', initAudio, { once: true });

        function playBeep() {
            if (!settings.sound) return;
            if (audioReady && audioCtx) {
                try {
                    const o = audioCtx.createOscillator();
                    const g = audioCtx.createGain();
                    o.type = 'sine';
                    o.frequency.value = 880; // short high beep
                    g.gain.setValueAtTime(0.0001, audioCtx.currentTime);
                    g.gain.exponentialRampToValueAtTime(0.2, audioCtx.currentTime + 0.01);
                    g.gain.exponentialRampToValueAtTime(0.0001, audioCtx.currentTime + 0.15);
                    o.connect(g).connect(audioCtx.destination);
                    o.start();
                    o.stop(audioCtx.currentTime + 0.16);
                    return;
                } catch { /* fallback */ }
            }
            // fallback to <audio> tag if Web Audio not available
            try { if (audio) { audio.currentTime = 0; audio.play().catch(() => { }); } } catch { }
        }

        let items = [];
        function loadItems() {
            try { items = JSON.parse(localStorage.getItem(itemsKey) || '[]') || []; }
            catch { items = []; }
        }
        function saveItems() { try { localStorage.setItem(itemsKey, JSON.stringify(items)); } catch { } }
        function unreadCount() { return items.filter(i => !i.read).length; }
        function updateBadge() {
            if (!badge) return;
            const count = unreadCount();
            if (count > 0) {
                badge.textContent = String(count);
                badge.classList.remove('d-none');
            } else {
                badge.textContent = '0';
                badge.classList.add('d-none');
            }
        }
        function escapeHtml(s) {
            return (s || '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
        }

        function computeDefaultLink(n) {
            if (n.link && typeof n.link === 'string' && n.link.length > 0) return n.link;
            const reqId = (n.rawId && typeof n.rawId === 'string' && n.rawId.length > 0) ? n.rawId : n.id;
            if (reqId && typeof reqId === 'string' && reqId.length > 0) {
                return `/Admin/Admin/RequestDetails?id=${encodeURIComponent(reqId)}`;
            }
            const status = (n.status || '').toLowerCase();
            if (status.includes('1st sem pending')) return '/Admin/Admin/Pending1stSem';
            if (status.includes('2nd sem pending')) return '/Admin/Admin/Pending2ndSem';
            if (status.includes('on hold')) return '/Admin/Admin/OnHold';
            if (status.includes('rejected')) return '/Admin/Admin/Rejected';
            if (status.includes('enrolled - regular')) return '/Admin/Admin/Enrolled2ndSem';
            if (status.startsWith('enrolled')) return '/Admin/Admin/Enrolled';
            return '/Admin/Admin/Dashboard';
        }
        function render() {
            if (!list || !emptyEl) return;
            list.innerHTML = '';
            if (!items.length) {
                emptyEl.classList.remove('d-none');
                updateBadge();
                return;
            }
            emptyEl.classList.add('d-none');
            const frag = document.createDocumentFragment();
            items
                .sort((a, b) => (new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()))
                .forEach(it => {
                    const div = document.createElement('div');
                    div.className = 'notif-item border rounded p-2 mb-2';
                    if (!it.read) div.classList.add('bg-light');
                    const when = new Date(it.timestamp);
                    const icon = it.icon || 'bell';
                    const link = it.link || computeDefaultLink(it);
                    div.innerHTML = `
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <div><i class="fas fa-${icon} me-1"></i> <strong>${escapeHtml(it.title || it.type || 'Notification')}</strong></div>
                                <div class="small text-muted">${when.toLocaleString()}</div>
                                <div class="small mt-1">${escapeHtml(it.message || '')}</div>
                                ${it.program ? `<div class="small text-secondary">Program: ${escapeHtml(it.program)}</div>` : ''}
                            </div>
                            <div class="ms-2 d-flex flex-column gap-1">
                                ${link ? `<a class="btn btn-sm btn-outline-primary" href="${link}">View</a>` : ''}
                                <button class="btn btn-sm btn-outline-secondary" data-action="dismiss" data-id="${it.id}">Dismiss</button>
                            </div>
                        </div>`;
                    frag.appendChild(div);
                });
            list.appendChild(frag);
            updateBadge();
        }

        function addNotification(n) {
            const item = {
                id: `${n.type || 'Note'}:${n.id || crypto.randomUUID()}`,
                rawId: n.id || null,
                type: n.type || 'Notification',
                title: n.title || '',
                message: n.message || '',
                severity: n.severity || 'info',
                icon: n.icon || 'bell',
                link: n.link || '',
                email: n.email || '',
                program: n.program || '',
                status: n.status || '',
                timestamp: n.submittedAt ? n.submittedAt : new Date().toISOString(),
                read: false
            };
            if (items.some(i => i.id === item.id)) {
                item.id = `${item.id}:${Date.now()}`;
            }
            items.unshift(item);
            saveItems();
            render();

            // Notify user
            playBeep();
            if (settings.browser && 'Notification' in window && Notification.permission === 'granted') {
                try {
                    const notif = new Notification(item.title || 'New notification', { body: `${item.message}`, icon: '/images/logo.svg' });
                    setTimeout(() => notif.close(), 5000);
                } catch { }
            }
        }

        list?.addEventListener('click', (e) => {
            const btn = e.target.closest('button[data-action="dismiss"]');
            if (!btn) return;
            const id = btn.getAttribute('data-id');
            items = items.map(i => i.id === id ? { ...i, read: true } : i);
            saveItems();
            render();
        });
        btnClear?.addEventListener('click', () => {
            items = [];
            saveItems();
            render();
        });
        btnMarkRead?.addEventListener('click', () => {
            items = items.map(i => ({ ...i, read: true }));
            saveItems();
            render();
        });

        // call initAudio when user clicks the bell (guaranteed user gesture)
        btn?.addEventListener('click', (e) => {
            initAudio();
            e.preventDefault();
            panel?.classList.toggle('show');
        });

        document.addEventListener('click', (e) => {
            if (!panel?.contains(e.target) && !btn?.contains(e.target)) panel?.classList.remove('show');
        });

        if (window.signalR) {
            const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/admin').withAutomaticReconnect().build();

            connection.on('AdminNotification', (payload) => { try { addNotification(payload || {}); } catch (e) { console.warn('notif error', e); } });

            connection.on('NewEnrollmentRequest', (p) => {
                const payload = p || {};
                addNotification({
                    type: 'PendingSubmitted',
                    title: 'New pending enrollment',
                    message: `${payload.fullName || 'Student'} submitted ${payload.type || ''}.`,
                    severity: 'info',
                    icon: 'hourglass-half',
                    id: payload.id,
                    link: payload.link,
                    email: payload.email,
                    program: payload.program,
                    status: payload.status,
                    submittedAt: payload.submittedAt
                });
            });

            connection.start().catch(err => console.error('SignalR connect failed', err));
        }

        loadItems();
        render();
    })();
}