(function () {
    const cfgRoot = document.getElementById('requestDetailsTransfereeConfig');
    if (!cfgRoot) return;

    const requestId = cfgRoot.dataset.requestId || '';
    const calcUrl = cfgRoot.dataset.calcUrl || '';
    const isProgramFull = cfgRoot.dataset.programFull === 'true';

    // parse prerequisites provided by server
    let prerequisitesMap = {};
    try {
        const prereqJson = cfgRoot.dataset.prerequisites || '{}';
        const parsed = JSON.parse(prereqJson);
        // normalize to arrays
        Object.keys(parsed || {}).forEach(k => {
            const v = parsed[k];
            prerequisitesMap[k] = Array.isArray(v) ? v : (v ? [v] : []);
        });
    } catch (ex) { prerequisitesMap = {}; }

    // ✅ Store latest eligibility in memory
    let latestEligibility = {};

    // render any existing computed eligibility if server supplied it
    try {
        const existingEligJson = cfgRoot.dataset.existingEligibility || '{}';
        const existingElig = existingEligJson ? JSON.parse(existingEligJson) : {};
        if (existingElig && Object.keys(existingElig).length > 0) {
            latestEligibility = existingElig;
            renderEligibilityMap(existingElig);
            appendEligibilityHiddenInputs(existingElig);
        }
    } catch (ex) {
        /* ignore parse errors */
    }

    const SOFT_DOCS = new Set(['GoodMoral', 'Diploma']);
    const progressEl = document.getElementById('flagProgressTransferee');
    const reviewChecks = [
        document.getElementById('chkReviewConfirm'),
        document.getElementById('chkReviewConfirmAction')
    ].filter(Boolean);

    // Helpers
    function selectsAll() { return Array.from(document.querySelectorAll('select.transferee-flag')); }
    function extractKeyFromName(name) {
        if (!name) return '';
        const m = name.match(/\[([^\]]+)\]/);
        return m ? m[1] : name;
    }
    function getValuesMap() {
        const map = {};
        selectsAll().forEach(s => {
            const key = s.getAttribute('data-key') || extractKeyFromName(s.name);
            map[key] = (s.value || '').trim();
        });
        return map;
    }
    function flagsCount() { return selectsAll().filter(s => (s.value || '').trim().length > 0).length; }
    function anyIneligible(values) { return Object.values(values).some(v => v === 'Ineligible'); }
    function requiredHasToBeFollowed(values) { return Object.entries(values).some(([k, v]) => !SOFT_DOCS.has(k) && v === 'To be followed'); }
    function allFlagsPresent(values) { const total = selectsAll().length; return total > 0 && flagsCount() === total; }

    // Action rules
    function acceptAllowed(values) {
        if (!allFlagsPresent(values)) return false;
        if (anyIneligible(values)) return false;
        for (const [k, vRaw] of Object.entries(values)) {
            const v = vRaw || '';
            if (SOFT_DOCS.has(k)) {
                if (!(v === 'Submitted' || v === 'To be followed')) return false;
            } else {
                if (v !== 'Submitted') return false;
            }
        }
        return true;
    }
    function holdAllowed(values) { return isProgramFull || requiredHasToBeFollowed(values); }
    function rejectAllowed(values) { return anyIneligible(values); }

    // Reviewed hidden sync
    function setReviewedHidden() {
        const reviewed = reviewChecks.some(c => c && c.checked);
        document.querySelectorAll('.js-action-form input[name="reviewed"]').forEach(inp => inp.value = reviewed ? 'true' : 'false');
    }

    // Append flags to form before submit
    function appendFlagsToForm(form) {
        form.querySelectorAll('input[data-dynamic-flag="true"]').forEach(x => x.remove());
        const values = getValuesMap();
        Object.entries(values).forEach(([k, v]) => {
            const inp = document.createElement('input');
            inp.type = 'hidden';
            inp.name = `flags[${k}]`;
            inp.value = v;
            inp.setAttribute('data-dynamic-flag', 'true');
            form.appendChild(inp);
        });
    }

    // ✅ Append eligibility to Accept form before submit
    function appendEligibilityToForm(form) {
        // Remove old eligibility inputs
        form.querySelectorAll('input[data-dynamic-elig="true"]').forEach(x => x.remove());

        // If no eligibility calculated yet, skip
        if (!latestEligibility || Object.keys(latestEligibility).length === 0) {
            console.warn('[Transferee] No eligibility data to append to Accept form');
            return;
        }

        // Append eligibility as hidden inputs
        Object.entries(latestEligibility).forEach(([subjectCode, reason]) => {
            const inp = document.createElement('input');
            inp.type = 'hidden';
            inp.name = `eligibility[${subjectCode}]`;
            inp.value = reason;
            inp.setAttribute('data-dynamic-elig', 'true');
            form.appendChild(inp);
        });

        console.log('[Transferee] ✅ Appended eligibility to Accept form:', latestEligibility);
    }

    // Subject remarks collection
    function collectSubjectRemarksEntries() {
        const entries = [];

        document.querySelectorAll('input[type="radio"][name^="subjectRemarks"]').forEach(r => {
            if (!r.checked) return;
            const name = r.getAttribute('name');
            if (!name) return;
            entries.push({ name, value: (r.value || '').trim() });
        });

        document.querySelectorAll('select[name^="subjectRemarks"]').forEach(sel => {
            const name = sel.getAttribute('name');
            if (!name) return;
            if (!entries.some(e => e.name === name)) entries.push({ name, value: (sel.value || '').trim() });
        });

        return entries;
    }

    // Eligibility rendering & hidden inputs
    function htmlEscape(s) {
        return (s || '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function renderEligibilityMap(map) {
        if (!map) return;
        Object.keys(map).forEach(code => {
            const reason = map[code] || '';
            const tr = document.querySelector(`#secondSemEligibilityTable tbody tr[data-code="${code}"]`);
            if (!tr) return;
            const eligCell = tr.querySelector('.eligibility-text');
            if (!eligCell) return;
            const can = !reason.toLowerCase().startsWith('cannot');
            const badgeClass = can ? 'bg-success' : 'bg-danger';
            eligCell.innerHTML = `<span class="badge ${badgeClass}">${htmlEscape(reason)}</span>`;
        });
    }

    function appendEligibilityHiddenInputs(map) {
        if (!map) return;
        const flagsForm = document.getElementById('frmUpdateFlags');
        if (!flagsForm) return;
        flagsForm.querySelectorAll('input[data-dynamic-elig]').forEach(x => x.remove());
        Object.entries(map).forEach(([k, v]) => {
            const inp = document.createElement('input');
            inp.type = 'hidden';
            inp.name = `secondSemEligibility[${k}]`;
            inp.value = v;
            inp.setAttribute('data-dynamic-elig', '1');
            flagsForm.appendChild(inp);
        });
        if (!flagsForm.querySelector('input[name="eligibilityCalculated"]')) {
            const m = document.createElement('input');
            m.type = 'hidden';
            m.name = 'eligibilityCalculated';
            m.value = 'true';
            flagsForm.appendChild(m);
        }
    }

    // ✅ Calculate & Save (MODIFIED)
    async function calculateAndSaveEligibility() {
        if (!calcUrl) {
            alert('Calculation URL not configured.');
            return;
        }

        const tokenInput = document.querySelector('#frmUpdateFlags input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        const entries = collectSubjectRemarksEntries();
        const anySet = entries.some(e => e.value === 'pass' || e.value === 'fail' || e.value === 'ongoing');
        if (!anySet && !confirm('No subject remarks set. Continue and calculate assuming missing remarks as not passed?')) return;

        const fd = new FormData();
        if (token) fd.append('__RequestVerificationToken', token);
        fd.append('id', requestId);

        entries.forEach(e => fd.append(e.name, e.value));

        const btn = document.getElementById('btnCalculateAndSave');
        const origHtml = btn ? btn.innerHTML : null;
        try {
            if (btn) { btn.disabled = true; btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Saving...'; }

            const resp = await fetch(calcUrl, {
                method: 'POST',
                body: fd,
                credentials: 'include',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!resp.ok) {
                const txt = await resp.text();
                alert(txt || 'Failed to calculate eligibility.');
                return;
            }

            const json = await resp.json();
            if (json?.success && json?.eligibility) {
                // ✅ Store eligibility in memory
                latestEligibility = json.eligibility;

                // ✅ Update UI
                renderEligibilityMap(json.eligibility);
                appendEligibilityHiddenInputs(json.eligibility);

                // ✅ CRITICAL: Populate Accept form
                const acceptForm = document.querySelector('form[data-action="accept"]');
                if (acceptForm) {
                    acceptForm.querySelectorAll('input[name^="eligibility["]').forEach(inp => inp.remove());
                    Object.entries(json.eligibility).forEach(([code, status]) => {
                        const inp = document.createElement('input');
                        inp.type = 'hidden';
                        inp.name = `eligibility[${code}]`;
                        inp.value = status;
                        acceptForm.appendChild(inp);
                    });
                    console.log('[Calculate] ✅ Populated Accept form with eligibility:', json.eligibility);
                } else {
                    console.error('[Calculate] ❌ Accept form not found!');
                }

                alert('✅ Eligibility calculated and saved successfully! You can now Accept the request.');

                if (btn) {
                    btn.classList.remove('btn-outline-primary');
                    btn.classList.add('btn-success');
                    btn.innerHTML = '<i class="fas fa-check me-1"></i> Saved';
                    setTimeout(() => {
                        btn.classList.remove('btn-success');
                        btn.classList.add('btn-outline-primary');
                        if (origHtml) btn.innerHTML = origHtml;
                        btn.disabled = false;
                        updateCalculateButton();
                        updateActionButtons(); // ✅ Re-enable Accept button
                    }, 1400);
                }
            } else {
                alert('Calculation failed.');
            }
        } catch (ex) {
            console.error(ex);
            alert('Network or server error while calculating eligibility.');
        } finally {
            if (btn && !btn.disabled) btn.disabled = false;
        }
    }

    // Action enabling
    function computeEnabling(form) {
        const action = form.getAttribute('data-action') || '';
        const reviewed = reviewChecks.some(c => c && c.checked);
        const values = getValuesMap();

        if (action === 'accept') {
            const ok = acceptAllowed(values);
            const hasEligibility = latestEligibility && Object.keys(latestEligibility).length > 0;
            const acceptEnabled = reviewed && ok && !isProgramFull && hasEligibility;

            let title = '';
            if (!ok) {
                title = 'Accept requires all required documents Submitted; Good Moral/Diploma may be To be followed. No Ineligible allowed.';
            } else if (!reviewed) {
                title = 'Review first';
            } else if (!hasEligibility) {
                title = 'Click "Calculate & Save Eligibility" first';
            }

            return { enabled: acceptEnabled, title };
        }
        if (action === 'hold') {
            const ok = holdAllowed(values);
            return { enabled: reviewed && ok, title: ok ? (reviewed ? '' : 'Review first') : 'On Hold requires program capacity reached or a required document (except Good Moral/Diploma) marked To be followed.' };
        }
        if (action === 'reject') {
            const ok = rejectAllowed(values);
            return { enabled: reviewed && ok, title: ok ? (reviewed ? '' : 'Review first') : 'Reject requires at least one document flagged Ineligible.' };
        }
        return { enabled: true, title: '' };
    }

    function updateActionButtons() {
        document.querySelectorAll('.js-action-form').forEach(f => {
            const btn = f.querySelector('button[type="submit"]');
            if (!btn) return;
            const { enabled, title } = computeEnabling(f);
            btn.disabled = !enabled;
            btn.title = title || '';
        });
    }

    function updateCalculateButton() {
        const btn = document.getElementById('btnCalculateAndSave');
        if (!btn) return;
        const reviewed = reviewChecks.some(c => c && c.checked);
        btn.disabled = !reviewed;
        btn.title = reviewed ? '' : 'Confirm review before calculating eligibility';
    }

    // ✅ Wiring
    selectsAll().forEach(s => s.addEventListener('change', () => { updateProgress(); updateActionButtons(); updateCalculateButton(); }));
    reviewChecks.forEach(c => c.addEventListener('change', () => { setReviewedHidden(); updateActionButtons(); updateCalculateButton(); }));

    // ✅ Wire submit handlers for action forms
    document.querySelectorAll('.js-action-form').forEach(f => {
        f.addEventListener('submit', function (e) {
            setReviewedHidden();
            const { enabled, title } = computeEnabling(f);
            if (!enabled) {
                e.preventDefault();
                alert(title || 'Action requirements are not met.');
                return;
            }

            const action = f.getAttribute('data-action') || '';

            // ✅ CRITICAL: Append eligibility to Accept form on submit
            if (action === 'accept') {
                appendEligibilityToForm(f);
            }

            appendFlagsToForm(f);
        });
    });

    // ✅ Wire Calculate & Save button
    const calcBtn = document.getElementById('btnCalculateAndSave');
    if (calcBtn) calcBtn.addEventListener('click', (ev) => {
        ev.preventDefault();
        calculateAndSaveEligibility();
    });

    const frmUpdate = document.getElementById('frmUpdateFlags');
    if (frmUpdate) frmUpdate.addEventListener('submit', function () { setReviewedHidden(); });

    function updateProgress() {
        const total = selectsAll().length;
        const flagged = flagsCount();
        if (progressEl) {
            if (total === 0) {
                progressEl.textContent = 'Flags not required';
                progressEl.className = 'badge bg-secondary';
            } else {
                progressEl.textContent = `${flagged}/${total} flagged`;
                progressEl.className = 'badge ' + (flagged === total ? 'bg-success' : 'bg-secondary');
            }
        }
    }

    // initial sync
    setTimeout(() => { updateProgress(); updateActionButtons(); setReviewedHidden(); updateCalculateButton(); }, 40);
})();