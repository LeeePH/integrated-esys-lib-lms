/* adminmanagegrades.js (full MVC version) */

/* ---------- fixed columns (accepts Full_Name or split names) ---------- */
const FIXED_COLS = ['Student_ID', 'Full_Name', 'Standing'];

/* ---------- DOM refs ---------- */
const importBtn = document.getElementById('importBtn');
const fileInput = document.getElementById('fileInput');
const importCenter = document.getElementById('importCenter');
const fileControls = document.getElementById('fileControls');
const fileNameEl = document.getElementById('fileName');
const tableWrap = document.getElementById('tableWrap');
const tableSearch = document.getElementById('tableSearch');
const clearFileBtn = document.getElementById('clearFileBtn');
const importDbBtn = document.getElementById('importDbBtn');

const confirmModal = document.getElementById('confirmModal');
const confirmCount = document.getElementById('confirmCount');
const confirmYes = document.getElementById('confirmYes');
const confirmNo = document.getElementById('confirmNo');

const toast = document.getElementById('toast');
const backButton = document.querySelector('.back-button');

/* ---------- state ---------- */
let parsedHeaders = [];
let parsedData = [];
let fixedIndices = {};
let filteredData = [];

/* ---------- toast ---------- */
function showToast(msg, timeout = 2600) {
    toast.textContent = msg;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), timeout);
}

/* ---------- back button ---------- */
backButton?.addEventListener('click', () => {
    const classCode = document.body?.dataset?.classCode || '';
    showToast('Returning...');
    const target = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb';
    setTimeout(() => (window.location.href = target), 800);
});

/* ---------- file handlers ---------- */
importBtn.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', e => {
    const f = e.target.files[0];
    if (f) handleFile(f);
});

clearFileBtn.addEventListener('click', () => {
    resetFile();
    showToast("File removed");
});

tableSearch.addEventListener('input', e => {
    applySearch(e.target.value.trim().toLowerCase());
});

/* ---------- modal ---------- */
importDbBtn.addEventListener('click', () => {
    if (!parsedData.length) return showToast("No data to import");
    confirmCount.textContent = parsedData.length;
    openConfirm();
});
confirmNo.addEventListener('click', closeConfirm);
confirmYes.addEventListener('click', () => {
    closeConfirm();
    sendToServer();
});

/* ---------- parse with SheetJS ---------- */
function handleFile(file) {
    const reader = new FileReader();
    fileNameEl.textContent = file.name;

    reader.onload = evt => {
        let wb;
        try {
            wb = XLSX.read(evt.target.result, { type: 'binary' });
        } catch {
            return showToast("Failed to read file");
        }

        const ws = wb.Sheets[wb.SheetNames[0]];
        const raw = XLSX.utils.sheet_to_json(ws, { defval: '' });
        if (!raw.length) return showToast("File contained no data");

        parsedHeaders = Object.keys(raw[0]);
        parsedData = raw.slice();

        const map = {};
        parsedHeaders.forEach(h => {
            map[h.toLowerCase()] = h;
        });

        fixedIndices = {};
        FIXED_COLS.forEach(col => {
            const found = map[col.toLowerCase()];
            if (found) fixedIndices[col] = found;
        });

        importCenter.style.display = "none";
        fileControls.hidden = false;
        filteredData = parsedData.slice();

        renderTable(parsedHeaders, parsedData, fixedIndices);

        const requiredOk = hasRequiredColumns(fixedIndices);
        importDbBtn.disabled = !requiredOk;

        if (!requiredOk) {
            showToast("Missing required columns: Student_ID and Standing; plus Full_Name or Last/First");
        } else {
            showToast("File parsed successfully");
        }
    };

    reader.readAsBinaryString(file);
}

/* ---------- table rendering ---------- */
function renderTable(headers, rows, fixedMap) {
    const table = document.createElement('table');
    table.className = 'import-table';

    const thead = document.createElement('thead');
    const trh = document.createElement('tr');

    headers.forEach(h => {
        const th = document.createElement('th');
        th.textContent = h;
        if (Object.values(fixedMap).includes(h)) th.classList.add('col-fixed');
        trh.appendChild(th);
    });

    thead.appendChild(trh);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    rows.forEach(row => {
        const tr = document.createElement('tr');
        headers.forEach(h => {
            const td = document.createElement('td');
            td.textContent = row[h] ?? "";
            if (Object.values(fixedMap).includes(h)) td.classList.add('col-fixed');
            tr.appendChild(td);
        });
        tbody.appendChild(tr);
    });

    table.appendChild(tbody);

    tableWrap.innerHTML = '';
    tableWrap.appendChild(table);
}

/* ---------- search ---------- */
function applySearch(q) {
    if (!q) {
        filteredData = parsedData.slice();
        renderTable(parsedHeaders, filteredData, fixedIndices);
        return;
    }

    const sidKey = fixedIndices['Student_ID'];
    const fullKey = fixedIndices['Full_Name'];

    filteredData = parsedData.filter(r => {
        if (sidKey && String(r[sidKey]).toLowerCase().includes(q)) return true;
        if (fullKey && String(r[fullKey]).toLowerCase().includes(q)) return true;

        return Object.values(r).some(v => String(v).toLowerCase().includes(q));
    });

    renderTable(parsedHeaders, filteredData, fixedIndices);
}

/* ---------- reset ---------- */
function resetFile() {
    parsedHeaders = [];
    parsedData = [];
    filteredData = [];
    fixedIndices = {};

    fileNameEl.textContent = "";
    tableWrap.innerHTML = "";
    fileControls.hidden = true;
    importCenter.style.display = "";
    fileInput.value = "";
}

/* ---------- modal ---------- */
function openConfirm() {
    confirmModal.style.display = "flex";
    confirmModal.setAttribute("aria-hidden", "false");
    confirmYes.focus();
}

function closeConfirm() {
    confirmModal.style.display = "none";
    confirmModal.setAttribute("aria-hidden", "true");
}

/* ---------- send to server (MVC) ---------- */
async function sendToServer() {
    if (!hasRequiredColumns(fixedIndices)) return showToast("Cannot import: missing required columns");

    const rows = (filteredData.length ? filteredData : parsedData).map(r => {
        const sid = fixedIndices['Student_ID'] ? r[fixedIndices['Student_ID']] : '';
        let fullName = fixedIndices['Full_Name'] ? r[fixedIndices['Full_Name']] : '';
        const standing = fixedIndices['Standing'] ? r[fixedIndices['Standing']] : '';

        if (!fullName) {
            const last = r[findKeyIgnoreCase(parsedHeaders, 'Last_Name')] || '';
            const first = r[findKeyIgnoreCase(parsedHeaders, 'First_Name')] || '';
            const middle = r[findKeyIgnoreCase(parsedHeaders, 'Middle_Name')] || '';
            fullName = [first, middle, last].filter(Boolean).join(' ').trim();
        }

        return {
            Student_ID: sid || '',
            Full_Name: fullName || '',
            Standing: standing || ''
        };
    });

    function findKeyIgnoreCase(headers, target) {
        const t = String(target).toLowerCase();
        for (const h of headers) {
            if (String(h).toLowerCase() === t) return h;
        }
        return null;
    }

    function hasRequiredColumns(map) {
        const hasSid = !!map['Student_ID'];
        const hasStanding = !!map['Standing'];
        const hasFull = !!map['Full_Name'];
        // accept either Full_Name OR Last+First (Middle optional)
        const hasSplit = !!findKeyIgnoreCase(parsedHeaders, 'Last_Name') && !!findKeyIgnoreCase(parsedHeaders, 'First_Name');
        return hasSid && hasStanding && (hasFull || hasSplit);
    }

    showToast(`Importing ${rows.length} records...`);

    try {
        const resp = await fetch('/AdminManageGrades/ImportGrades', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ rows })
        });

        if (!resp.ok) return showToast("Import failed — server error");
        showToast("Import successful");

    } catch (err) {
        showToast("Import failed — network error");
    }
}
