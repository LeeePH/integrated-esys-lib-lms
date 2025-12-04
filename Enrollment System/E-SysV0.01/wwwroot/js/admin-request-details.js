(() => {
    // ============================================
    // GLOBAL STATE
    // ============================================
    let isSecondSemPending = false;
    let isFirstSemPending = false;
    let studentType = '';
    let isProgramFull = false;
    let requiresFlags = false;

    // Soft documents (can be "To be followed")
    const SOFT_DOCS = new Set(['GoodMoral', 'Diploma']);

    // Hard documents (must be "Submitted" for Accept)
    const HARD_DOCS = new Set(['Form138', 'MedicalCertificate', 'CertificateOfIndigency', 'BirthCertificate']);

    // ============================================
    // INITIALIZATION
    // ============================================
    function init() {
        const cfg = document.getElementById('requestDetailsConfig');
        if (cfg) {
            parseServerConfig(cfg);
            determineIfFlagsRequired();
        }

        initFlagProgress();
        initActionButtons();
    }

    // ============================================
    // SERVER CONFIG PARSING
    // ============================================
    function parseServerConfig(cfg) {
        isSecondSemPending = cfg.dataset.isSecondSemPending === 'true';
        isFirstSemPending = cfg.dataset.isFirstSemPending === 'true';
        studentType = (cfg.dataset.studentType || '').trim();
        isProgramFull = cfg.dataset.isProgramFull === 'true';

        console.log('[RequestDetails] Config:', {
            isSecondSemPending,
            isFirstSemPending,
            studentType,
            isProgramFull
        });
    }

    // ============================================
    // DETERMINE IF FLAGS ARE REQUIRED
    // ============================================
    function determineIfFlagsRequired() {
        // ✅ RULE: Only Freshmen 1st Semester Pending requires document flags
        requiresFlags =
            isFirstSemPending &&
            (studentType === 'Freshmen' || studentType.startsWith('Freshmen'));

        console.log('[RequestDetails] requiresFlags:', requiresFlags);
    }

    // ============================================
    // DOCUMENT FLAGS PROGRESS TRACKING
    // ============================================
    function initFlagProgress() {
        const selects = document.querySelectorAll('select.flag-select');
        const progressEl = document.getElementById('flagProgress');

        if (!progressEl) return;

        function updateProgress() {
            if (!requiresFlags) {
                progressEl.textContent = 'Flags not required';
                progressEl.className = 'badge bg-success';
                return;
            }

            const total = selects.length;
            const flagged = Array.from(selects).filter(s => (s.value || '').trim().length > 0).length;

            if (total === 0) {
                progressEl.textContent = 'No flags';
                progressEl.className = 'badge bg-secondary';
            } else {
                progressEl.textContent = `${flagged}/${total} flagged`;
                progressEl.className = 'badge ' + (flagged === total ? 'bg-success' : 'bg-secondary');
            }
        }

        if (requiresFlags && selects.length > 0) {
            selects.forEach(s => s.addEventListener('change', updateProgress));
        }

        updateProgress();
    }

    // ============================================
    // GET CURRENT FLAG VALUES
    // ============================================
    function getFlagValues() {
        const values = {};
        document.querySelectorAll('select.flag-select').forEach(select => {
            const key = select.getAttribute('data-key');
            const value = (select.value || '').trim();
            if (key && value) {
                values[key] = value;
            }
        });

        console.log('[RequestDetails] Flag values:', values);
        return values;
    }

    // ============================================
    // ACTION BUTTON LOGIC
    // ============================================
    function initActionButtons() {
        const reviewCheckbox = document.getElementById('chkReviewConfirm');
        const actionForms = document.querySelectorAll('form.js-action-form');

        if (!reviewCheckbox || actionForms.length === 0) return;

        function updateActionButtons() {
            const reviewed = reviewCheckbox.checked;
            const flagValues = getFlagValues();
            const flagCount = Object.keys(flagValues).length;
            const allFlagged = !requiresFlags || flagCount === 6;

            console.log('[RequestDetails] Update:', {
                reviewed,
                requiresFlags,
                flagCount,
                allFlagged,
                isProgramFull
            });

            actionForms.forEach(form => {
                const action = form.dataset.action;
                const button = form.querySelector('button[type="submit"]');
                const reviewedInput = form.querySelector('input[name="reviewed"]');

                if (!button) return;

                if (reviewedInput) {
                    reviewedInput.value = reviewed ? 'true' : 'false';
                }

                const { enabled, title } = getActionState(action, reviewed, flagValues, allFlagged);
                button.disabled = !enabled;
                button.title = title || '';

                console.log(`[${action}] enabled=${enabled}, title="${title}"`);
            });
        }

        reviewCheckbox.addEventListener('change', updateActionButtons);

        if (requiresFlags) {
            document.querySelectorAll('select.flag-select').forEach(s => {
                s.addEventListener('change', updateActionButtons);
            });
        }

        updateActionButtons();

        actionForms.forEach(form => {
            form.addEventListener('submit', function (e) {
                const action = form.dataset.action;
                if (requiresFlags && (action === 'accept' || action === 'hold' || action === 'reject')) {
                    appendFlagsToForm(form);
                }
            });
        });

        setupHoldReasonHandlers();
    }

    // ============================================
    // ACTION STATE LOGIC
    // ============================================
    function getActionState(action, reviewed, flagValues, allFlagged) {
        // ✅ 2ND SEMESTER PENDING: Only review required
        if (!requiresFlags) {
            if (action === 'accept' || action === 'hold' || action === 'reject') {
                return {
                    enabled: reviewed,
                    title: reviewed ? '' : 'Check review box first'
                };
            }
            return { enabled: true, title: '' };
        }

        // ✅ FRESHMEN 1ST SEMESTER: Apply flagging rules
        if (action === 'accept') {
            return getAcceptState(reviewed, flagValues, allFlagged);
        }

        if (action === 'hold') {
            return getHoldState(reviewed, flagValues, allFlagged);
        }

        if (action === 'reject') {
            return getRejectState(reviewed, flagValues, allFlagged);
        }

        if (action === 'allow') {
            return {
                enabled: allFlagged,
                title: allFlagged ? '' : 'Flag all documents first'
            };
        }

        return { enabled: true, title: '' };
    }

    // ============================================
    // ACCEPT: All hard docs "Submitted", soft docs "Submitted" or "To be followed"
    // ============================================
    function getAcceptState(reviewed, flagValues, allFlagged) {
        if (!reviewed) {
            return { enabled: false, title: 'Check review box first' };
        }

        if (!allFlagged) {
            return { enabled: false, title: 'All 6 documents must be flagged' };
        }

        // ❌ BLOCK: Any document "Ineligible"
        const ineligibleDocs = Object.entries(flagValues)
            .filter(([key, val]) => val === 'Ineligible')
            .map(([key]) => key);

        if (ineligibleDocs.length > 0) {
            return {
                enabled: false,
                title: `Cannot accept: ${ineligibleDocs.join(', ')} marked Ineligible. Use Reject instead.`
            };
        }

        // ❌ BLOCK: Hard docs must be "Submitted"
        const invalidHardDocs = [];
        for (const [key, val] of Object.entries(flagValues)) {
            if (HARD_DOCS.has(key) && val !== 'Submitted') {
                invalidHardDocs.push(key);
            }
        }

        if (invalidHardDocs.length > 0) {
            return {
                enabled: false,
                title: `Cannot accept: ${invalidHardDocs.join(', ')} must be "Submitted"`
            };
        }

        // ❌ BLOCK: Soft docs must be "Submitted" or "To be followed"
        const invalidSoftDocs = [];
        for (const [key, val] of Object.entries(flagValues)) {
            if (SOFT_DOCS.has(key) && val !== 'Submitted' && val !== 'To be followed') {
                invalidSoftDocs.push(key);
            }
        }

        if (invalidSoftDocs.length > 0) {
            return {
                enabled: false,
                title: `Cannot accept: ${invalidSoftDocs.join(', ')} invalid status`
            };
        }

        return { enabled: true, title: 'All requirements met' };
    }

    // ============================================
    // HOLD: Hard docs "To be followed" OR program full
    // ============================================
    function getHoldState(reviewed, flagValues, allFlagged) {
        if (!reviewed) {
            return { enabled: false, title: 'Check review box first' };
        }

        if (!allFlagged) {
            return { enabled: false, title: 'All 6 documents must be flagged' };
        }

        // ✅ ALLOW: Program is full
        if (isProgramFull) {
            return { enabled: true, title: 'Program capacity full' };
        }

        // ✅ ALLOW: At least one hard doc "To be followed"
        const hardDocsToFollow = Object.entries(flagValues)
            .filter(([key, val]) => HARD_DOCS.has(key) && val === 'To be followed')
            .map(([key]) => key);

        if (hardDocsToFollow.length > 0) {
            return { enabled: true, title: `Hard docs need follow-up: ${hardDocsToFollow.join(', ')}` };
        }

        return {
            enabled: false,
            title: 'Hold requires: Program full OR hard docs (Form138, MedCert, Indigency, BirthCert) "To be followed"'
        };
    }

    // ============================================
    // REJECT: At least one doc "Ineligible"
    // ============================================
    function getRejectState(reviewed, flagValues, allFlagged) {
        if (!reviewed) {
            return { enabled: false, title: 'Check review box first' };
        }

        if (!allFlagged) {
            return { enabled: false, title: 'All 6 documents must be flagged' };
        }

        // ✅ ALLOW: At least one document "Ineligible"
        const ineligibleDocs = Object.entries(flagValues)
            .filter(([key, val]) => val === 'Ineligible')
            .map(([key]) => key);

        if (ineligibleDocs.length > 0) {
            return { enabled: true, title: `Ineligible: ${ineligibleDocs.join(', ')}` };
        }

        return {
            enabled: false,
            title: 'Reject requires: At least one document marked "Ineligible"'
        };
    }

    // ============================================
    // HOLD REASON HANDLERS
    // ============================================
    function setupHoldReasonHandlers() {
        const holdReasonSelect = document.getElementById('holdReasonSelect');
        const holdReasonValue = document.getElementById('holdReasonValue');
        const holdReasonOtherInput = document.getElementById('holdReasonOtherInput');

        if (holdReasonSelect && holdReasonValue && holdReasonOtherInput) {
            holdReasonSelect.addEventListener('change', function () {
                if (this.value === 'Other') {
                    holdReasonOtherInput.classList.remove('d-none');
                    holdReasonValue.value = '';
                } else {
                    holdReasonOtherInput.classList.add('d-none');
                    holdReasonValue.value = this.value;
                }
            });

            holdReasonOtherInput.addEventListener('input', function () {
                if (holdReasonSelect.value === 'Other') {
                    holdReasonValue.value = this.value;
                }
            });
        }
    }

    // ============================================
    // APPEND FLAGS TO FORM
    // ============================================
    function appendFlagsToForm(form) {
        form.querySelectorAll('input[data-dynamic-flag="true"]').forEach(x => x.remove());

        const flagSelects = document.querySelectorAll('select.flag-select');
        flagSelects.forEach(select => {
            const key = select.getAttribute('data-key');
            const value = select.value || '';

            if (key && value) {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = `flags[${key}]`;
                input.value = value;
                input.setAttribute('data-dynamic-flag', 'true');
                form.appendChild(input);
            }
        });
    }

    // ============================================
    // BOOTSTRAP
    // ============================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Diagnostic: list all selects and action state
    (function () {
        console.log('--- admin-request-details diagnostic start ---');

        console.log('Script tag present?',
            !!document.querySelector('script[src*="admin-request-details.js"]'),
            document.querySelector('script[src*="admin-request-details.js"]')?.src);

        console.log('Console errors (last 5):');
        console.log(window.__lastConsoleErrors || 'no capture'); // optional if not set

        const selectors = [
            'select.flag-select',
            'select.transferee-flag',
            'select.form-select[name^="flags"]',
            'select[name^="flags"]',
            'select'
        ];

        selectors.forEach(s => {
            const els = Array.from(document.querySelectorAll(s));
            console.log(`Selector "${s}" -> count: ${els.length}`);
        });

        const found = Array.from(document.querySelectorAll('select.flag-select, select.transferee-flag, select.form-select[name^="flags"], select[name^="flags"]'));
        console.log('Found selects (detailed):', found.length);
        found.forEach((el, i) => {
            console.log(i + 1, {
                outerHTML: el.outerHTML.slice(0, 400),
                name: el.getAttribute('name'),
                dataKey: el.dataset.key,
                class: el.className,
                value: el.value,
                disabled: el.disabled
            });
        });

        // If no matches, show first 6 <select> elements so we can inspect markup
        if (found.length === 0) {
            const some = Array.from(document.querySelectorAll('select')).slice(0, 6);
            console.log('No flagged selects found — first <select> elements on page (up to 6):');
            some.forEach((el, i) => console.log(i + 1, el.outerHTML.slice(0, 400)));
        }

        const chk = document.getElementById('chkReviewConfirm');
        console.log('Review checkbox:', !!chk, chk ? { checked: chk.checked, id: chk.id } : null);

        const actionForms = Array.from(document.querySelectorAll('form.js-action-form'));
        console.log('action forms count:', actionForms.length);
        actionForms.forEach((f, i) => {
            const btn = f.querySelector('button[type="submit"]');
            console.log(`actionForm[${i}] data-action=${f.dataset.action}, button disabled=${btn ? btn.disabled : 'no-button'}`);
        });

        console.log('--- admin-request-details diagnostic end ---');
    })();
})();