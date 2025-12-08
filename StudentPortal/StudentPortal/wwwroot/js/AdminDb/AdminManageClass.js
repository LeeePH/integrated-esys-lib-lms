// Verify script is loaded
console.log('AdminManageClass.js loaded');

const studentListContainer = document.getElementById("student-list");
const studentCountEl = document.getElementById("studentCount");
const classSearchInput = document.getElementById("classSearch");
let studentsData = [];
const joinRequestContainer = document.getElementById("join-request-list") || document.getElementById("requestList");
const exportBtn = document.getElementById("export-btn");
const focusModeBtn = document.getElementById("focus-mode-btn");
const toast = document.getElementById("toast");
const backButton = document.querySelector('.back-button');

// Dummy data removed - now using real data from database via ExportData endpoint

// Back button - go back to class page
backButton?.addEventListener('click', () => { 
    const classCode = document.body.dataset.classCode || '';
    if (classCode) {
        if (typeof window.showToast === 'function') {
            window.showToast('Returning to class...');
        }
        setTimeout(() => window.location.href = `/AdminClass/${classCode}`, 600);
    } else {
        if (typeof window.showToast === 'function') {
            window.showToast('Returning to dashboard...');
        }
        setTimeout(() => window.location.href = '/admindb/AdminDb', 600);
    }
});

function renderStudents(list) {
    if (!studentListContainer) return;
    studentListContainer.innerHTML = "";
    const arr = Array.isArray(list) ? list : [];

    if (arr.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'no-students';
        empty.textContent = 'No approved students yet.';
        studentListContainer.appendChild(empty);
    } else {
        arr.forEach(student => {
            const statusLower = (student.status || '').toLowerCase();
            const icon = statusLower === 'present' ? 'fa-circle-check' : statusLower === 'late' ? 'fa-clock' : 'fa-circle-xmark';
            const name = student.studentName || '';
            const parts = name.split(/\s+/).filter(Boolean);
            const initials = parts.length ? (parts[0].slice(0,1) + (parts.length > 1 ? parts[parts.length-1].slice(0,1) : '')) .toUpperCase() : '';

            const card = document.createElement('div');
            card.classList.add('student-card');
            card.innerHTML = `
                <div class="student-photo avatar">${initials}</div>
                <div class="student-info">
                    <div class="student-name">${name}</div>
                    <div class="student-email">${student.studentEmail || ''}</div>
                </div>
                <span class="student-status status-${statusLower}"><i class="fa-solid ${icon}"></i><span class="att-text">${student.status || ''}</span></span>
                <div class="actions">
                    <button class="btn small success mark-present" data-id="${student.id}" data-status="Present"><i class="fa-solid fa-check"></i> </button>
                    <button class="btn small warning mark-late" data-id="${student.id}" data-status="Late"><i class="fa-solid fa-clock"></i> </button>
                    <button class="btn small danger mark-absent" data-id="${student.id}" data-status="Absent"><i class="fa-solid fa-xmark"></i> </button>
                    <button class="unenroll" title="Un-enroll" data-id="${student.id}" data-email="${student.studentEmail || ''}" data-name="${name}">x</button>
                </div>`;
            studentListContainer.appendChild(card);
        });
    }

    if (studentCountEl) {
        studentCountEl.textContent = `Students: ${arr.length}`;
    }
}

async function loadStudents(classCode) {
    try {
        const res = await fetch(`/AdminManageClass/GetStudentsByClassCode/${encodeURIComponent(classCode)}`);
        if (!res.ok) throw new Error(`Failed to fetch students (${res.status})`);
        const data = await res.json();
        studentsData = Array.isArray(data) ? data : [];
        renderStudents(studentsData);
    } catch (err) {
        console.error('Error loading students:', err);
        if (typeof window.showToast === 'function') window.showToast("⚠️ Could not load students.");
    }
}
 
async function loadJoinRequests(classCode) { 
     try { 
        const url = `/AdminManageClass/GetJoinRequests/${encodeURIComponent(classCode)}`;
        const res = await fetch(url); 
        if (!res.ok) throw new Error(`Failed to fetch join requests (${res.status})`); 
        const data = await res.json(); 
        console.debug('JoinRequests fetched:', { url, count: Array.isArray(data) ? data.length : 'n/a', data });
        if (!joinRequestContainer) return;
        joinRequestContainer.innerHTML = ""; 
        if (!Array.isArray(data) || data.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'no-requests';
            empty.textContent = 'No pending join requests.';
            joinRequestContainer.appendChild(empty);
            return;
        }
        data.forEach(req => { 
            const row = document.createElement("div"); 
            row.classList.add("join-request"); 
            const dateStr = req.requestedAt ? new Date(req.requestedAt).toLocaleString() : ""; 
            const sectionLabel = req.sectionDisplay || req.classCode || "";
            
            // Create elements manually to attach event listeners
            const joinInfo = document.createElement("div");
            joinInfo.className = "join-info";
            joinInfo.innerHTML = `<span class="join-name">${req.studentName || ''}</span> 
                                  <span class="join-class">(${sectionLabel})</span> 
                                  <span class="join-time">${dateStr}</span>`;
            
            const joinActions = document.createElement("div");
            joinActions.className = "join-actions";
            
            const approveBtn = document.createElement("button");
            approveBtn.type = "button";
            approveBtn.className = "approve-btn";
            approveBtn.setAttribute("data-id", req.id || '');
            approveBtn.setAttribute("data-name", req.studentName || '');
            approveBtn.setAttribute("data-class-code", req.classCode || '');
            approveBtn.setAttribute("data-student-email", req.studentEmail || '');
            approveBtn.setAttribute("data-section-label", sectionLabel);
            approveBtn.textContent = "✅ Approve";
            approveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const requestId = approveBtn.dataset.id;
                const classCode = approveBtn.dataset.classCode;
                const studentEmail = approveBtn.dataset.studentEmail;
                console.log('Approve button clicked (direct):', { requestId, classCode, studentEmail });
                if (requestId) {
                    await handleJoinAction(requestId, classCode, studentEmail, true);
                } else {
                    console.error('Approve button missing requestId');
                    if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                }
            });
            
            const rejectBtn = document.createElement("button");
            rejectBtn.type = "button";
            rejectBtn.className = "reject-btn";
            rejectBtn.setAttribute("data-id", req.id || '');
            rejectBtn.textContent = "❌ Cancel";
            rejectBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const requestId = rejectBtn.dataset.id;
                console.log('Reject button clicked (direct):', { requestId });
                if (requestId) {
                    await handleJoinAction(requestId, null, null, false);
                } else {
                    console.error('Reject button missing requestId');
                    if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                }
            });
            
            joinActions.appendChild(approveBtn);
            joinActions.appendChild(rejectBtn);
            
            row.appendChild(joinInfo);
            row.appendChild(joinActions);
            joinRequestContainer.appendChild(row);
            
            // Attach listeners to the newly created buttons
            attachButtonListeners(); 
        }); 
     } catch (err) { 
        console.error('Error loading join requests:', err); 
        if (typeof window.showToast === 'function') window.showToast("⚠️ Could not load join requests."); 
     } 
}

async function handleJoinAction(requestId, classCode, studentEmail, approve = true) {
    const endpoint = approve ? "/AdminManageClass/ApproveJoin" : "/AdminManageClass/RejectJoin";
    console.log('handleJoinAction called:', { requestId, classCode, studentEmail, approve, endpoint });
    
    if (!requestId) {
        console.error('Missing requestId');
        if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
        return;
    }

    try {
        const requestBody = { 
            RequestId: requestId, 
            ClassCode: classCode || '', 
            StudentEmail: studentEmail || '' 
        };
        console.log('Sending request:', { endpoint, body: requestBody });

        const res = await fetch(endpoint, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestBody),
        });

        console.log('Response status:', res.status, res.statusText);
        const data = await res.json();
        console.log('Response data:', data);

        if (!res.ok) {
            const errorMsg = data.message || data.Message || "Request failed";
            console.error('Request failed:', errorMsg);
            throw new Error(errorMsg);
        }

        const successMsg = data.message || data.Message || (approve ? "Request approved." : "Request rejected.");
        if (typeof window.showToast === 'function') window.showToast(successMsg);
        
        // Reload data - reload join requests first (to remove approved/rejected from pending list)
        // then reload students (to show newly approved students in class list)
        if (currentClassCode) {
            // Small delay to ensure database updates are complete
            await new Promise(resolve => setTimeout(resolve, 300));
            await loadJoinRequests(currentClassCode);
            await loadStudents(currentClassCode);
        }
    } catch (err) {
        console.error('Error in handleJoinAction:', err);
        const errorMsg = err.message || "An error occurred";
        if (typeof window.showToast === 'function') window.showToast(`⚠️ ${errorMsg}`);
    }
}

async function markAttendance(studentId, status) {
    try {
        const res = await fetch("/AdminManageClass/MarkAttendance", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ studentId, status, classCode: currentClassCode }),
            credentials: 'same-origin',
        });
        const data = await res.json();
        if (typeof window.showToast === 'function') window.showToast(data.message || 'Attendance updated');
        // Optimistically update the row's status label
        const row = document.querySelector(`.student-card .actions [data-id='${studentId}']`)?.closest('.student-card');
        if (row) {
            const label = row.querySelector('.student-status');
            if (label) {
                const statusLower = (status || '').toLowerCase();
                // update class to match server-rendered style: status-<lower>
                label.className = `student-status status-${statusLower}`;

                // update inner att-text if present
                const att = label.querySelector('.att-text');
                if (att) att.textContent = status;

                // update icon if present
                const iconEl = label.querySelector('i');
                if (iconEl) {
                    iconEl.className = `fa-solid ${statusLower === 'present' ? 'fa-circle-check' : statusLower === 'late' ? 'fa-clock' : 'fa-circle-xmark'}`;
                }
            }
        }
        // Do not reload students; attendance is stored separately from StudentRecord list
    } catch (err) { console.error(err); if (typeof window.showToast === 'function') window.showToast("⚠️ Could not update attendance."); }
}

function computeStanding(attStatusList, taskAttained, taskTotal) {
    // Attendance grade: Present = 1, Late = 0.5, Absent = 0 (averaged then scaled to 100)
    let attPct = 0;
    if (Array.isArray(attStatusList) && attStatusList.length > 0) {
        let score = 0;
        attStatusList.forEach(st => {
            const s = (st || "").toLowerCase();
            if (s === "present") score += 1;
            else if (s === "late") score += 0.5;
        });
        attPct = (score / attStatusList.length) * 100;
    }

    // Task grade out of 100
    let taskPct = 0;
    if (taskTotal > 0) {
        taskPct = (taskAttained / taskTotal) * 100;
    }

    // Assessment left blank (manual input), treat as 0 for now
    const assessPct = 0;

    // Overall grade: Attendance 20%, Task 50%, Assessment 30%
    const finalScore = attPct * 0.20 + taskPct * 0.50 + assessPct * 0.30;
    const standing = finalScore >= 75 ? "Passed" : "Failed";
    return { finalScore, standing };
}

async function exportData() {
    try {
        const classCode = document.body?.dataset?.classCode || "CLASSCODE";

        if (typeof window.showToast === 'function') {
            window.showToast("📥 Loading data from database...");
        }

        // Fetch real data from the server
        const response = await fetch(`/AdminManageClass/ExportData/${encodeURIComponent(classCode)}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(`Failed to fetch export data: ${response.status} ${response.statusText}`);
        }

        const data = await response.json();

        if (!data.success || !data.students || !Array.isArray(data.students)) {
            throw new Error(data.message || "Invalid data received from server");
        }

        // Debug: Log the data structure to help diagnose issues
        console.log("Export data received:", {
            hasTasks: !!data.tasks,
            tasksCount: data.tasks?.length || 0,
            tasks: data.tasks,
            studentsCount: data.students?.length || 0,
            firstStudent: data.students?.[0]
        });

        // Build header row matching the template format
        // Student_ID | Full_Name | Attendance columns | NameOfTask | TaskGrade pairs | Assessment | Standing
        const header = ["Student_ID", "Full_Name"];
        
        // Add attendance columns
        if (data.attendanceLabels && Array.isArray(data.attendanceLabels)) {
            data.attendanceLabels.forEach(label => {
                header.push(label);
            });
        }
        
        // Add dynamic task columns (NameOfTask and TaskGrade pairs side by side)
        if (data.tasks && Array.isArray(data.tasks) && data.tasks.length > 0) {
            console.log("Adding task columns:", data.tasks.length);
            data.tasks.forEach(task => {
                const taskTitle = task.taskTitle || task.title || "NameOfTask";
                header.push(taskTitle); // Task title as NameOfTask column
                header.push("TaskGrade"); // TaskGrade column right after
            });
        } else {
            console.warn("No tasks found in export data");
        }
        
        // Add Assessment and Standing columns
        header.push("Assessment", "Standing");

        // Build data rows from real database data
        const rows = [header];
        
        data.students.forEach(student => {
            const attStatuses = [];
            if (data.attendanceLabels && student.attendanceStatuses) {
                data.attendanceLabels.forEach(label => {
                    attStatuses.push(student.attendanceStatuses[label] || "Absent");
                });
            }

            // Assessment left blank for manual input after export
            const assessmentDisplay = student.assessment || "";

            const row = [
                (student.studentId || student.username || ""),
                student.fullName || ""
            ];

            // Add attendance statuses
            attStatuses.forEach(st => row.push(st));
            
            // Add task data for each task (in the same order as header)
            // Each task gets two columns: NameOfTask (shows task title) and TaskGrade (shows grade)
            if (data.tasks && Array.isArray(data.tasks) && data.tasks.length > 0) {
                const taskGradesByTaskId = student.taskGradesByTaskId || {};
                data.tasks.forEach((task, index) => {
                    const taskId = task.taskId || task.id || "";
                    const taskTitle = task.taskTitle || task.title || "";
                    
                    // NameOfTask column - shows task title in data row
                    row.push(taskTitle);
                    
                    // TaskGrade column - shows grade value (empty string if no grade)
                    const grade = (taskGradesByTaskId && typeof taskGradesByTaskId === 'object') 
                        ? (taskGradesByTaskId[taskId] || "") 
                        : "";
                    row.push(grade);
                });
            } else if (data.tasks && Array.isArray(data.tasks) && data.tasks.length === 0) {
                // Tasks array exists but is empty - no task columns to add
                console.log("Tasks array is empty - no task columns added");
            }
            
            // Add Assessment and Standing
            row.push(assessmentDisplay, student.standing || "");

            rows.push(row);
        });

        // Use SheetJS (XLSX) to create an Excel file from the rows
        if (typeof XLSX === "undefined" || !XLSX.utils || !XLSX.writeFile) {
            console.error("XLSX library is not available. Ensure it is loaded in the view.");
            if (typeof window.showToast === 'function') window.showToast("⚠️ Excel library (XLSX) not loaded.");
            return;
        }

        const wb = XLSX.utils.book_new();
        const ws = XLSX.utils.aoa_to_sheet(rows);
        XLSX.utils.book_append_sheet(wb, ws, "Attendance");

        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
        const fileName = `Class_${classCode}_export_${timestamp}.xlsx`;
        XLSX.writeFile(wb, fileName);

        if (typeof window.showToast === 'function') {
            window.showToast(`✅ Exported ${data.students.length} student(s) to Excel.`);
        }
    } catch (err) {
        console.error("Export error:", err);
        if (typeof window.showToast === 'function') {
            window.showToast(`⚠️ Export failed: ${err.message || "Unknown error"}`);
        }
    }
}

let currentClassCode = ''; // set this on page load from view

// Function to attach event listeners to buttons
function attachButtonListeners() {
    // Attach to approve buttons
    const approveButtons = document.querySelectorAll('.approve-btn');
    console.log(`Found ${approveButtons.length} approve buttons`);
    
    approveButtons.forEach((btn, index) => {
        if (btn.hasAttribute('data-listener-attached')) {
            console.log(`Approve button ${index} already has listener`);
            return;
        }
        
        btn.setAttribute('data-listener-attached', 'true');
        console.log(`Attaching listener to approve button ${index}:`, btn);
        
        btn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('✅ APPROVE BUTTON CLICKED!', this);
            const requestId = this.dataset.id || this.getAttribute('data-id');
            const classCode = this.dataset.classCode || this.getAttribute('data-class-code');
            const studentEmail = this.dataset.studentEmail || this.getAttribute('data-student-email');
            console.log('Approve button data:', { requestId, classCode, studentEmail, allAttributes: Array.from(this.attributes).map(a => `${a.name}=${a.value}`) });
            
            if (!requestId) {
                console.error('❌ Approve button missing requestId');
                alert('Missing request ID! Check console for details.');
                if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                return;
            }
            
            await handleJoinAction(requestId, classCode, studentEmail, true);
        });
    });
    
    // Attach to reject buttons
    const rejectButtons = document.querySelectorAll('.reject-btn');
    console.log(`Found ${rejectButtons.length} reject buttons`);
    
    rejectButtons.forEach((btn, index) => {
        if (btn.hasAttribute('data-listener-attached')) {
            console.log(`Reject button ${index} already has listener`);
            return;
        }
        
        btn.setAttribute('data-listener-attached', 'true');
        console.log(`Attaching listener to reject button ${index}:`, btn);
        
        btn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('✅ REJECT BUTTON CLICKED!', this);
            const requestId = this.dataset.id || this.getAttribute('data-id');
            console.log('Reject button data:', { requestId, allAttributes: Array.from(this.attributes).map(a => `${a.name}=${a.value}`) });
            
            if (!requestId) {
                console.error('❌ Reject button missing requestId');
                alert('Missing request ID! Check console for details.');
                if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                return;
            }
            
            await handleJoinAction(requestId, null, null, false);
        });
    });
}

// Event delegation for dynamically added buttons
document.addEventListener("click", async (e) => {
    // Handle approve button clicks
    if (e.target.closest && e.target.closest(".approve-btn")) {
        const approveBtn = e.target.closest(".approve-btn");
        if (!approveBtn.hasAttribute('data-listener-attached')) {
            e.preventDefault();
            e.stopPropagation();
            const requestId = approveBtn.dataset.id || approveBtn.getAttribute('data-id');
            const classCode = approveBtn.dataset.classCode || approveBtn.getAttribute('data-class-code');
            const studentEmail = approveBtn.dataset.studentEmail || approveBtn.getAttribute('data-student-email');
            console.log('Approve button clicked (delegation fallback):', { requestId, classCode, studentEmail });
            
            if (requestId) {
                await handleJoinAction(requestId, classCode, studentEmail, true);
            }
            return;
        }
    }

    // Handle reject button clicks
    if (e.target.closest && e.target.closest(".reject-btn")) {
        const rejectBtn = e.target.closest(".reject-btn");
        if (!rejectBtn.hasAttribute('data-listener-attached')) {
            e.preventDefault();
            e.stopPropagation();
            const requestId = rejectBtn.dataset.id || rejectBtn.getAttribute('data-id');
            console.log('Reject button clicked (delegation fallback):', { requestId });
            
            if (requestId) {
                await handleJoinAction(requestId, null, null, false);
            }
            return;
        }
    }

    // Handle attendance buttons robustly
    const attBtn = e.target.closest && e.target.closest('button.mark-present, button.mark-absent, button.mark-late');
    if (attBtn) {
        const id = attBtn.dataset.id;
        const status = attBtn.dataset.status;
        if (id && status) await markAttendance(id, status);
        return;
    }
    const unenrollBtn = e.target.closest && e.target.closest('button.unenroll');
    if (unenrollBtn) {
        const id = unenrollBtn.dataset.id;
        const email = unenrollBtn.dataset.email || '';
        const name = unenrollBtn.dataset.name || '';
        openUnenrollModal(id, email, name);
        return;
    }
    if (e.target === exportBtn) await exportData();
    if (e.target === focusModeBtn) {
        if (typeof window.showToast === 'function') window.showToast("Focus mode feature coming soon...");
    }
});

// Initialize on page load
document.addEventListener("DOMContentLoaded", async () => {
    currentClassCode = document.body.dataset.classCode || '';
    console.log('AdminManageClass initialized with classCode:', currentClassCode);
    
    attachButtonListeners();
    wireInlineButtons(document);
    
    if (currentClassCode) {
        await loadStudents(currentClassCode);
        await loadJoinRequests(currentClassCode);
        
        setTimeout(() => {
            attachButtonListeners();
            wireInlineButtons(document);
        }, 100);
    }

    // Wire search filtering
    if (classSearchInput) {
        classSearchInput.addEventListener('input', () => {
            const q = classSearchInput.value.trim().toLowerCase();
            const filtered = studentsData.filter(s =>
                (s.studentName || '').toLowerCase().includes(q) ||
                (s.studentEmail || '').toLowerCase().includes(q)
            );
            renderStudents(filtered);
        });
    }

    const list = document.getElementById('join-request-list');
    if (list && 'MutationObserver' in window) {
        new MutationObserver(function () { wireInlineButtons(list); }).observe(list, { childList: true, subtree: true });
    }
});

function wireInlineButtons(root) {
    (root || document).querySelectorAll('.approve-btn').forEach(function (btn) {
        if (btn.hasAttribute('data-listener-attached')) return;
        btn.setAttribute('data-listener-attached', 'true');
        btn.addEventListener('click', async function (e) {
            e.preventDefault();
            e.stopPropagation();
            const id = btn.getAttribute('data-id');
            const code = btn.getAttribute('data-class-code');
            const email = btn.getAttribute('data-student-email');
            if (!id) return;
            await handleJoinAction(id, code, email, true);
        });
    });
    (root || document).querySelectorAll('.reject-btn').forEach(function (btn) {
        if (btn.hasAttribute('data-listener-attached')) return;
        btn.setAttribute('data-listener-attached', 'true');
        btn.addEventListener('click', async function (e) {
            e.preventDefault();
            e.stopPropagation();
            const id = btn.getAttribute('data-id');
            if (!id) return;
            await handleJoinAction(id, null, null, false);
        });
    });
}

// removed grade chip behavior

async function unenrollStudent(studentId, studentEmail) {
    try {
        const body = { studentId: studentId || '', classCode: currentClassCode, studentEmail: studentEmail || '' };
        const res = await fetch('/AdminManageClass/Unenroll', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
            credentials: 'same-origin'
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Failed to un-enroll');
        if (typeof window.showToast === 'function') window.showToast(data.message || 'Student un-enrolled');
        if (currentClassCode) {
            await loadStudents(currentClassCode);
        }
    } catch (err) {
        console.error('Unenroll error:', err);
        if (typeof window.showToast === 'function') window.showToast(`⚠️ ${err.message || 'Could not un-enroll student'}`);
    }
}

let currentUnenroll = null;
const unenrollModal = document.getElementById('unenroll-modal');
const unenrollNameEl = document.getElementById('unenroll-name');
const unenrollEmailEl = document.getElementById('unenroll-email');
const confirmUnenrollBtn = document.getElementById('confirm-unenroll');
const cancelUnenrollBtn = document.getElementById('cancel-unenroll');
const closeUnenrollModalBtn = document.getElementById('close-unenroll-modal');

function openUnenrollModal(studentId, studentEmail, studentName) {
    currentUnenroll = { id: studentId || '', email: studentEmail || '', name: studentName || '' };
    if (unenrollNameEl) unenrollNameEl.textContent = currentUnenroll.name;
    if (unenrollEmailEl) unenrollEmailEl.textContent = currentUnenroll.email;
    if (unenrollModal) unenrollModal.style.display = 'flex';
}

function closeUnenrollModal() {
    if (unenrollModal) unenrollModal.style.display = 'none';
    currentUnenroll = null;
}

confirmUnenrollBtn?.addEventListener('click', async () => {
    if (!currentUnenroll) return;
    await unenrollStudent(currentUnenroll.id, currentUnenroll.email);
    closeUnenrollModal();
});

cancelUnenrollBtn?.addEventListener('click', () => closeUnenrollModal());
closeUnenrollModalBtn?.addEventListener('click', () => closeUnenrollModal());
