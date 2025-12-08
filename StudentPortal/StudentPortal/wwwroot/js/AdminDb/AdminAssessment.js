// --- ELEMENTS ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
const toast = document.getElementById('toast');
const backButton = document.querySelector('.back-button');

const makeChangesButton = document.getElementById('makeChangesButton');
const adminActions = document.getElementById('adminActions');
const deleteModal = document.getElementById('deleteModal');
const confirmDelete = document.getElementById('confirmDelete');
const cancelDelete = document.getElementById('cancelDelete');

const assessmentTitle = document.getElementById('assessmentTitle');
const assessmentDescription = document.getElementById('assessmentDescription');
const editControls = document.getElementById('editControls');
const saveEditBtn = document.getElementById('saveEditBtn');
const cancelEditBtn = document.getElementById('cancelEditBtn');
const dateInfo = document.getElementById('dateInfo');
const checkSubmissionBtn = document.getElementById('checkSubmissionBtn');

// --- INITIAL STATE ---
if (editControls) {
    editControls.style.display = 'none';
    editControls.classList.remove('show');
}

// --- TOAST ---
function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// Bottom bar interactions are managed by AdminBottomBar.js

// back button -> AdminClass (by classCode) or dashboard
backButton?.addEventListener('click', () => {
    const classCode = document.body?.dataset?.classCode || '';
    showToast('Returning...');
    const target = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb';
    setTimeout(() => { window.location.href = target; }, 800);
});

// --- CHECK SUBMISSION BUTTON ---
checkSubmissionBtn?.addEventListener('click', () => {
    showToast('Opening submissions...');
    setTimeout(() => (window.location.href = 'adminchecksubmissions.html'), 800);
});

// --- ADMIN ACTIONS MENU ---
let adminMenuOpen = false;
makeChangesButton?.addEventListener('click', (e) => {
    adminMenuOpen = !adminMenuOpen;
    adminActions.classList.toggle('show', adminMenuOpen);
    e.stopPropagation();
});

// Close adminActions when clicking outside
document.addEventListener('click', (e) => {
    if (!adminActions.contains(e.target) && !makeChangesButton.contains(e.target)) {
        adminActions.classList.remove('show');
        adminMenuOpen = false;
    }
});

// --- DELETE MODAL HANDLING ---
adminActions?.addEventListener('click', (e) => {
    const action = e.target.closest('.admin-action');
    if (!action) return;

    const type = action.dataset.action;
    adminActions.classList.remove('show');
    adminMenuOpen = false;

    if (type === 'edit') enterEditMode();
    else if (type === 'delete') showDeleteModal();
});

function showDeleteModal() {
    if (!deleteModal) return;
    deleteModal.classList.add('show');
}

confirmDelete?.addEventListener('click', () => {
    if (!deleteModal) return;
    deleteModal.classList.remove('show');
    showToast('🗑️ Assessment deleted.');
});

cancelDelete?.addEventListener('click', () => {
    if (!deleteModal) return;
    deleteModal.classList.remove('show');
});

// Close modal when clicking outside
document.addEventListener('click', (e) => {
    if (deleteModal && deleteModal.classList.contains('show') && e.target === deleteModal) {
        deleteModal.classList.remove('show');
    }
});

// --- EDIT MODE ---
let inEditMode = false;
let originalDateText = '';

function enterEditMode() {
    if (inEditMode) return;
    inEditMode = true;

    showToast('Editing assessment...');

    if (assessmentTitle) {
        assessmentTitle.contentEditable = 'true';
        assessmentTitle.focus();
        placeCaretAtEnd(assessmentTitle);
    }
    if (assessmentDescription) {
        assessmentDescription.contentEditable = 'true';
    }

    // Handle date info (replace Deadline with input)
    originalDateText = dateInfo.textContent;
    const parts = dateInfo.textContent.split('|').map((p) => p.trim());
    const postedText = parts.find((p) => p.startsWith('Posted:')) || '';
    const deadlineText = parts.find((p) => p.startsWith('Deadline:'));
    const currentDeadline = deadlineText ? deadlineText.replace('Deadline:', '').trim() : '';

    const deadlineInput = document.createElement('input');
    deadlineInput.type = 'date';
    deadlineInput.id = 'deadlineInput';
    deadlineInput.value = currentDeadline
        ? new Date(currentDeadline).toISOString().split('T')[0]
        : '';

    dateInfo.innerHTML = `${postedText} | Deadline: `;
    dateInfo.appendChild(deadlineInput);

    revealEditControls();
}

function revealEditControls() {
    editControls.style.display = 'flex';
    requestAnimationFrame(() => editControls.classList.add('show'));
}

function hideEditControls() {
    editControls.classList.remove('show');
    setTimeout(() => (editControls.style.display = 'none'), 320);
}

function placeCaretAtEnd(el) {
    try {
        const range = document.createRange();
        const sel = window.getSelection();
        range.selectNodeContents(el);
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
    } catch (err) { }
}

// --- EXIT EDIT MODE ---
function exitEditMode(saveChanges = false) {
    if (!inEditMode) return;
    inEditMode = false;

    if (assessmentTitle) assessmentTitle.contentEditable = 'false';
    if (assessmentDescription) assessmentDescription.contentEditable = 'false';

    const deadlineInput = document.getElementById('deadlineInput');
    const newDeadline = deadlineInput ? deadlineInput.value : '';
    if (deadlineInput) deadlineInput.remove();

    if (saveChanges) {
        const today = new Date().toLocaleDateString('en-US', {
            month: 'short',
            day: 'numeric',
            year: 'numeric',
        });
        const formattedDeadline = newDeadline
            ? new Date(newDeadline).toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            })
            : 'N/A';
        dateInfo.textContent = `Posted: Oct 12, 2025 | Edited: ${today} | Deadline: ${formattedDeadline}`;
        showToast('✅ Changes saved.');
    } else {
        dateInfo.textContent = originalDateText;
        showToast('✖️ Edit cancelled.');
    }

    hideEditControls();
}

// --- SAVE / CANCEL BUTTONS ---
saveEditBtn?.addEventListener('click', () => exitEditMode(true));
cancelEditBtn?.addEventListener('click', () => exitEditMode(false));

// --- ESC KEY CANCEL ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && inEditMode) {
        exitEditMode(false);
    }
});

// ------------------ REPLACE ATTACHMENT (upload + link) -----
const replaceFileBtn = document.getElementById('replaceFileBtn');
const replaceFileInput = document.getElementById('replaceFileInput');

replaceFileBtn?.addEventListener('click', (e) => {
    e.preventDefault();
    if (!inEditMode) { showToast('Enable edit mode first'); return; }
    replaceFileInput?.click();
});

replaceFileInput?.addEventListener('change', async () => {
    const file = replaceFileInput.files && replaceFileInput.files[0];
    if (!file) return;
    try {
        const classCode = document.body?.dataset?.classCode || '';
        const fd = new FormData();
        fd.append('file', file);
        fd.append('classCode', classCode);
        fd.append('type', 'assessment');
        const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
        if (!up.ok) throw new Error('Upload failed');
        const upRes = await up.json();
        if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

        const assessmentContainer = document.getElementById('assessmentContainer');
        const assessmentId = assessmentContainer?.dataset?.assessmentId || '';
        const linkRes = await fetch('/AdminAssessment/ReplaceAttachment', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ assessmentId, fileName: file.name, fileUrl: upRes.fileUrl })
        });
        if (!linkRes.ok) throw new Error('Replace failed');
        const linkJson = await linkRes.json();
        if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

        const attList = document.getElementById('attachmentList');
        if (attList) {
            attList.innerHTML = '';
            const box = document.createElement('div');
            box.className = 'attachment-box';
            box.innerHTML = `<i class=\"fa-solid fa-file\"></i> ${file.name}`;
            attList.appendChild(box);
        }
        showToast('✅ File replaced');
    } catch (err) {
        console.error(err);
        showToast('❌ ' + (err.message || 'Could not replace file'));
    } finally {
        try { replaceFileInput.value = ''; } catch { }
    }
});

class AdminAssessmentManager {
    constructor() {
        this.currentAssessment = null;
        this.isEditing = false;
        this.classId = this.getClassIdFromUrl();
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadAssessmentData();
        this.refreshLogCounters();
    }

    getClassIdFromUrl() {
        const urlParams = new URLSearchParams(window.location.search);
        return urlParams.get('classId') || '';
    }

    bindEvents() {
        try {
            // Make Changes button
            const makeChangesBtn = document.getElementById('makeChangesButton');
            if (makeChangesBtn) {
                makeChangesBtn.addEventListener('click', () => {
                    this.toggleAdminActions();
                });
            }

            // Admin actions
            document.querySelectorAll('.admin-action button').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const action = e.target.closest('.admin-action')?.dataset?.action;
                    if (action) this.handleAdminAction(action);
                });
            });

            // Edit controls
            const saveEditBtn = document.getElementById('saveEditBtn');
            const cancelEditBtn = document.getElementById('cancelEditBtn');
            if (saveEditBtn) saveEditBtn.addEventListener('click', () => this.saveAssessment());
            if (cancelEditBtn) cancelEditBtn.addEventListener('click', () => this.cancelEdit());

            // Delete modal
            const confirmDelete = document.getElementById('confirmDelete');
            const cancelDelete = document.getElementById('cancelDelete');
            if (confirmDelete) confirmDelete.addEventListener('click', () => this.deleteAssessment());
            if (cancelDelete) cancelDelete.addEventListener('click', () => this.hideDeleteModal());

            // Search functionality (optional element)
            const search = document.getElementById('searchStudents');
            if (search) {
                search.addEventListener('input', (e) => {
                    this.filterStudents(e.target.value);
                });
            }

            // Anti-cheat log boxes -> open modal
            document.querySelectorAll('.log-box').forEach(box => {
                box.addEventListener('click', () => {
                    const type = box.dataset.logType;
                    if (type) this.openLogModal(type);
                });
            });

            // Modal close buttons
            document.querySelectorAll('.loglist-modal-close').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const t = e.target.dataset.close;
                    if (t) this.closeLogModal(t);
                });
            });

            // Close when clicking outside modal content
            document.querySelectorAll('.loglist-modal').forEach(modal => {
                modal.addEventListener('click', (e) => {
                    if (e.target.classList.contains('loglist-modal')) {
                        const id = modal.id.replace('modal-', '');
                        this.closeLogModal(id);
                    }
                });
            });
        } catch (err) {
            console.error('Error binding events:', err);
        }
    }

    async loadAssessmentData() {
        try {
            const response = await fetch(`/AdminAssessment/Index?classId=${this.classId}`);
            if (response.ok) {
                // Page loads with server-side data
                this.currentAssessment = window.assessmentData; // Set from server
            }
        } catch (error) {
            console.error('Error loading assessment data:', error);
            this.showToast('Error loading assessment data', 'error');
        }
    }

    toggleAdminActions() {
        const actions = document.getElementById('adminActions');
        if (!actions) return;
        const current = actions.getAttribute('aria-hidden');
        actions.setAttribute('aria-hidden', current === 'true' ? 'false' : 'true');
    }

    handleAdminAction(action) {
        this.toggleAdminActions();

        switch (action) {
            case 'edit':
                this.startEditing();
                break;
            case 'delete':
                this.showDeleteModal();
                break;
        }
    }

    startEditing() {
        this.isEditing = true;

        // Make content editable
        document.getElementById('assessmentTitle').contentEditable = 'true';
        document.getElementById('assessmentDescription').contentEditable = 'true';

        // Show edit controls
        document.getElementById('editControls').style.display = 'flex';
        document.getElementById('dateInfo').style.display = 'none';

        this.showToast('Editing mode enabled', 'info');
    }

    async saveAssessment() {
        try {
            const title = document.getElementById('assessmentTitle').textContent.trim();
            const description = document.getElementById('assessmentDescription').textContent.trim();
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            const deadlineInput = document.getElementById('deadlineInput');
            const deadline = deadlineInput ? new Date(deadlineInput.value).toISOString() : null;

            const response = await fetch('/AdminAssessment/UpdateAssessment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId, title, description, deadline })
            });

            const result = await response.json();

            if (result.success) {
                this.cancelEdit();
                this.showToast('Assessment saved successfully', 'success');
            } else {
                throw new Error(result.message);
            }

        } catch (error) {
            console.error('Error saving assessment:', error);
            this.showToast('Error saving assessment', 'error');
        }
    }

    cancelEdit() {
        this.isEditing = false;

        // Make content non-editable
        document.getElementById('assessmentTitle').contentEditable = 'false';
        document.getElementById('assessmentDescription').contentEditable = 'false';

        // Hide edit controls
        document.getElementById('editControls').style.display = 'none';
        document.getElementById('dateInfo').style.display = 'block';

        // Reset content (you might want to reload from server)
        if (this.currentAssessment) {
            document.getElementById('assessmentTitle').textContent = this.currentAssessment.title;
            document.getElementById('assessmentDescription').textContent = this.currentAssessment.description;
        }
    }

    showDeleteModal() {
        document.getElementById('deleteModal').style.display = 'flex';
    }

    hideDeleteModal() {
        document.getElementById('deleteModal').style.display = 'none';
    }

    async deleteAssessment() {
        try {
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            const response = await fetch('/AdminAssessment/DeleteAssessment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId })
            });

            const result = await response.json();

            if (result.success) {
                this.showToast('Assessment deleted successfully', 'success');
                const classCode = document.body?.dataset?.classCode || '';
                setTimeout(() => {
                    window.location.href = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb';
                }, 1200);
            } else {
                throw new Error(result.message);
            }

        } catch (error) {
            console.error('Error deleting assessment:', error);
            this.showToast('Error deleting assessment', 'error');
        } finally {
            this.hideDeleteModal();
        }
    }

    filterStudents(searchTerm) {
        const rows = document.querySelectorAll('.submitted-row:not(.header)');
        const lowerSearchTerm = searchTerm.toLowerCase();

        rows.forEach(row => {
            const studentName = row.querySelector('.name-col').textContent.toLowerCase();
            if (studentName.includes(lowerSearchTerm)) {
                row.style.display = 'flex';
            } else {
                row.style.display = 'none';
            }
        });
    }

    showToast(message, type = 'info') {
        const toast = document.getElementById('toast');
        toast.textContent = message;
        toast.className = `toast ${type}`;
        toast.style.display = 'block';

        setTimeout(() => {
            toast.style.display = 'none';
        }, 3000);
    }

    async openLogModal(type) {
        try {
            const modal = document.getElementById(`modal-${type}`);
            const title = document.getElementById(`title-${type}`);
            const list = document.getElementById(`list-${type}`);
            if (!modal || !title || !list) return;

            title.textContent = this.getLogTitle(type);
            list.innerHTML = '<div class="loading">Loading logs...</div>';
            modal.style.display = 'flex';

            const logs = await this.fetchLogs(type);
            this.renderLogs(list, logs);
        } catch (err) {
            console.error('Error opening log modal:', err);
            this.showToast('Error loading logs', 'error');
        }
    }

    closeLogModal(type) {
        const modal = document.getElementById(`modal-${type}`);
        if (modal) modal.style.display = 'none';
    }

    getLogTitle(type) {
        switch (type) {
            case 'copy': return 'Copy Logs';
            case 'paste': return 'Paste Logs';
            case 'inspect': return 'Inspect Logs';
            case 'tabswitch': return 'Tab Switch Logs';
            case 'openprograms': return 'Open Programs Logs';
            case 'screenshare': return 'Screen Share Logs';
            default: return 'Logs';
        }
    }

    async fetchLogs(type) {
        const { classCode: routeClass, contentId: routeContent } = this.getRouteParams();
        const classCode = routeClass || document.body?.dataset?.classCode || '';
        const assessmentId = routeContent || document.getElementById('assessmentContainer')?.dataset?.assessmentId || '';
        if (!classCode || !assessmentId) throw new Error('Missing identifiers');
        const url = type ?
            `/AdminAssessment/${encodeURIComponent(classCode)}/${encodeURIComponent(assessmentId)}/Logs?type=${encodeURIComponent(type)}` :
            `/AdminAssessment/${encodeURIComponent(classCode)}/${encodeURIComponent(assessmentId)}/Logs`;
        const res = await fetch(url);
        if (!res.ok) throw new Error('Failed to load logs');
        const json = await res.json();
        if (!json.success) throw new Error(json.message || 'Failed to load logs');
        return Array.isArray(json.logs) ? json.logs : [];
    }

    getRouteParams() {
        try {
            const parts = window.location.pathname.split('/').filter(Boolean);
            const idx = parts.findIndex(p => p.toLowerCase() === 'adminassessment');
            if (idx >= 0 && parts.length >= idx + 3) {
                return { classCode: parts[idx + 1], contentId: parts[idx + 2] };
            }
        } catch { }
        return { classCode: '', contentId: '' };
    }

    async refreshLogCounters() {
        try {
            const logs = await this.fetchLogs();
            const counts = { copy: 0, paste: 0, inspect: 0, tabswitch: 0, openprograms: 0, screenshare: 0 };
            logs.forEach(l => {
                const type = (l.type || '').toLowerCase();
                const c = l.count || 1;
                if (counts.hasOwnProperty(type)) counts[type] += c;
            });
            const apply = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = String(val); };
            apply('logCopy', counts.copy);
            apply('logPaste', counts.paste);
            apply('logInspect', counts.inspect);
            apply('logTabSwitch', counts.tabswitch);
            apply('logOpenPrograms', counts.openprograms);
            apply('logScreenShare', counts.screenshare);
        } catch (err) {
            console.warn('Could not refresh log counters:', err);
        }
    }

    renderLogs(container, logs) {
        if (!logs.length) {
            container.innerHTML = '<div class="empty">No logs found for this category.</div>';
            return;
        }
        const frag = document.createDocumentFragment();
        logs.forEach(l => {
            const row = document.createElement('div');
            row.className = 'loglist-row';
            const time = new Date(l.time).toLocaleString();
            const details = (l.details || '').slice(0, 200);
            row.innerHTML = `
                <div class="loglist-left">
                    <div class="loglist-name">${l.student || 'Unknown'}</div>
                    <div class="loglist-email">${l.email || ''}</div>
                </div>
                <div class="loglist-right">
                    <div class="loglist-meta">
                        <span class="loglist-type">${l.type}</span>
                        <span class="loglist-count">x${l.count || 1}</span>
                        <span class="loglist-time">${time}</span>
                    </div>
                    <div class="loglist-details">${details}</div>
                </div>`;
            frag.appendChild(row);
        });
        container.innerHTML = '';
        container.appendChild(frag);
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    new AdminAssessmentManager();
});
