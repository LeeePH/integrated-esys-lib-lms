(function () {
    const MODAL_ID = 'ChangePasswordModal';

    function modalEl() { return document.getElementById(MODAL_ID); }

    function toggleVisibility(btn) {
        const targetSel = btn.getAttribute('data-toggle-target');
        if (!targetSel) return;
        const input = document.querySelector(targetSel);
        if (!input) return;
        const isPwd = input.type === 'password';
        input.type = isPwd ? 'text' : 'password';
        btn.textContent = isPwd ? 'Hide' : 'Show';
    }

    function policyChecks(pwd) {
        return {
            len: pwd.length >= 8 && pwd.length <= 64,
            upper: /[A-Z]/.test(pwd),
            lower: /[a-z]/.test(pwd),
            digit: /\d/.test(pwd),
            symbol: /[^A-Za-z0-9]/.test(pwd),
            space: !/\s/.test(pwd)
        };
    }

    function updatePolicyUI(pwd) {
        const checks = policyChecks(pwd);
        const list = document.getElementById('cpPolicyList');
        if (list) {
            list.querySelectorAll('li[data-rule]').forEach(li => {
                const key = li.getAttribute('data-rule');
                const ok = !!checks[key];
                li.classList.toggle('text-success', ok);
                li.classList.toggle('text-danger', !ok);
            });
        }
        const bar = document.getElementById('cpStrengthBar');
        if (bar) {
            const total = 6;
            const passed = Object.values(checks).filter(Boolean).length;
            const pct = Math.round((passed / total) * 100);
            bar.style.width = pct + '%';
            bar.classList.remove('bg-danger', 'bg-warning', 'bg-success');
            if (pct < 50) bar.classList.add('bg-danger');
            else if (pct < 84) bar.classList.add('bg-warning');
            else bar.classList.add('bg-success');
        }
    }

    function updateMatchUI() {
        const a = document.getElementById('cpNewPassword');
        const b = document.getElementById('cpConfirmPassword');
        const hint = document.getElementById('cpMatchHint');
        if (!a || !b || !hint) return;
        const mismatch = a.value.length > 0 && b.value.length > 0 && a.value !== b.value;
        hint.classList.toggle('d-none', !mismatch);
    }

    // New: enable/disable submit button based on policy + match
    function updateSubmitState() {
        const m = modalEl();
        if (!m) return;
        const form = m.querySelector('form');
        if (!form) return;
        const btn = form.querySelector('button[type="submit"]');
        if (!btn) return;

        const pwd = (document.getElementById('cpNewPassword')?.value || '');
        const conf = (document.getElementById('cpConfirmPassword')?.value || '');
        const checks = policyChecks(pwd);
        const allOk = Object.values(checks).every(Boolean);
        const match = pwd.length > 0 && pwd === conf;

        btn.disabled = !(allOk && match);
    }

    function resetSubmitState() {
        const m = modalEl();
        if (!m) return;
        const btn = m.querySelector('form button[type="submit"]');
        if (btn) {
            btn.disabled = false;
            if (btn.dataset.originalText) {
                btn.textContent = btn.dataset.originalText;
            } else if (!btn.textContent || btn.textContent.trim() === '') {
                btn.textContent = 'Change Password';
            }
        }
    }

    // Handle show/hide events to reset state
    document.addEventListener('shown.bs.modal', (e) => {
        if (e.target && e.target.id === MODAL_ID) {
            resetSubmitState();
            updateMatchUI();
            const np = document.getElementById('cpNewPassword');
            updatePolicyUI(np ? np.value : '');
            updateSubmitState();
        }
    });
    document.addEventListener('hidden.bs.modal', (e) => {
        if (e.target && e.target.id === MODAL_ID) {
            resetSubmitState();
        }
    });

    // Also reset after navigation from bfcache
    window.addEventListener('pageshow', () => {
        resetSubmitState();
        updateSubmitState();
    });

    // Click handler for show/hide password buttons
    document.addEventListener('click', (e) => {
        const btn = e.target.closest('button[data-toggle-target]');
        if (btn) toggleVisibility(btn);
    });

    // Live policy + match hints
    const np = document.getElementById('cpNewPassword');
    const cp = document.getElementById('cpConfirmPassword');
    np?.addEventListener('input', () => { updatePolicyUI(np.value); updateMatchUI(); updateSubmitState(); });
    cp?.addEventListener('input', () => { updateMatchUI(); updateSubmitState(); });

    // Intercept submit: client-side validation + guard, then let server handle
    document.addEventListener('submit', (e) => {
        const form = e.target;
        const m = modalEl();
        if (!m || !(form instanceof HTMLFormElement) || !m.contains(form)) return;

        const btn = form.querySelector('button[type="submit"]');
        const pwd = (document.getElementById('cpNewPassword')?.value || '');
        const conf = (document.getElementById('cpConfirmPassword')?.value || '');

        // Client validation: block invalid attempts
        const checks = policyChecks(pwd);
        const allOk = Object.values(checks).every(Boolean);
        const match = pwd === conf;

        if (!allOk || !match) {
            e.preventDefault();
            e.stopPropagation();
            updatePolicyUI(pwd);
            updateMatchUI();
            updateSubmitState(); // ensure submit stays disabled when invalid
            return;
        }

        // Submit guard (only for the change password form in this modal)
        if (btn) {
            btn.dataset.originalText = btn.textContent || '';
            btn.disabled = true;
            btn.textContent = 'Changing...';
        }
    });

    // Auto-open if flagged by controller
    const cfg = document.getElementById('cpModalConfig');
    if (cfg?.dataset.open === 'true') {
        const el = modalEl();
        if (el && typeof bootstrap !== 'undefined') {
            const modal = new bootstrap.Modal(el, { backdrop: 'static', keyboard: false });
            modal.show();
        }
    }
})();