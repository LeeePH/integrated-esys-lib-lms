(function () {
    'use strict';

    console.log('[admin-enrolled-details] Script starting...');

    const cfgRoot = document.getElementById('requestDetailsConfig');
    if (!cfgRoot) {
        console.log('[admin-enrolled-details] Config element not found');
        return;
    }

    const requestId = cfgRoot.dataset.requestId || '';
    const isSecondSemPending = cfgRoot.dataset.isSecondSemPending === 'true';
    const targetYearLevel = cfgRoot.dataset.targetYearLevel || '1st Year';
    const targetSemester = cfgRoot.dataset.targetSemester || '2nd Semester';
    const calcUrl = cfgRoot.dataset.calcUrl || '';

    let existingEligibility = {};
    let secondSemSubjects = [];
    let prerequisites = {};

    try {
        existingEligibility = JSON.parse(cfgRoot.dataset.existingSecondSemEligibility || '{}');
    } catch (e) {
        console.error('[admin-enrolled-details] Parse error (eligibility):', e);
        existingEligibility = {};
    }

    try {
        secondSemSubjects = JSON.parse(cfgRoot.dataset.secondSemSubjects || '[]');
    } catch (e) {
        console.error('[admin-enrolled-details] Parse error (subjects):', e);
        secondSemSubjects = [];
    }

    try {
        prerequisites = JSON.parse(cfgRoot.dataset.prerequisites || '{}');
    } catch (e) {
        console.error('[admin-enrolled-details] Parse error (prerequisites):', e);
        prerequisites = {};
    }

    console.log('[admin-enrolled-details] Config loaded:', {
        requestId,
        calcUrl,
        subjectsCount: secondSemSubjects.length,
        existingEligibilityCount: Object.keys(existingEligibility).length
    });

    // Render existing eligibility on page load
    if (Object.keys(existingEligibility).length > 0) {
        console.log('[admin-enrolled-details] Rendering existing eligibility');
        renderEligibilityTable(existingEligibility);
        showEligibilityTable();
    }

    // Calculate Eligibility Button Handler
    const calculateBtn = document.getElementById('calculateEligibility');
    if (calculateBtn) {
        console.log('[admin-enrolled-details] Calculate button found');

        calculateBtn.addEventListener('click', async function (e) {
            e.preventDefault();
            console.log('[admin-enrolled-details] Calculate button clicked');

            // Collect subject remarks
            const remarks = collectSubjectRemarks();
            console.log('[admin-enrolled-details] Collected remarks:', remarks);

            if (Object.keys(remarks).length === 0) {
                alert('⚠️ No remarks found.\n\nPlease set remarks for at least one subject before calculating eligibility.\n\nMake sure you select Pass, Fail, or Ongoing for each subject.');
                return;
            }

            // Show loading state
            const originalText = calculateBtn.innerHTML;
            calculateBtn.disabled = true;
            calculateBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Calculating...';

            try {
                // Get antiforgery token
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                if (!tokenInput) {
                    console.error('[admin-enrolled-details] Antiforgery token not found');
                    alert('❌ Security token missing. Please refresh the page.');
                    calculateBtn.innerHTML = originalText;
                    calculateBtn.disabled = false;
                    return;
                }

                console.log('[admin-enrolled-details] Building FormData...');

                // Build FormData
                const formData = new FormData();
                formData.append('__RequestVerificationToken', tokenInput.value);
                formData.append('id', requestId);

                // Add subject remarks
                let remarksAdded = 0;
                for (const [code, remark] of Object.entries(remarks)) {
                    formData.append(`subjectRemarks[${code}]`, remark);
                    remarksAdded++;
                    console.log(`[admin-enrolled-details] Added: subjectRemarks[${code}] = ${remark}`);
                }

                console.log(`[admin-enrolled-details] Total remarks added: ${remarksAdded}`);
                console.log('[admin-enrolled-details] Sending POST to:', calcUrl);

                // Send AJAX request
                const response = await fetch(calcUrl, {
                    method: 'POST',
                    body: formData,
                    credentials: 'include',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                console.log('[admin-enrolled-details] Response status:', response.status);

                if (!response.ok) {
                    const errorText = await response.text();
                    console.error('[admin-enrolled-details] Server error:', errorText);
                    alert('❌ Calculation Failed\n\nServer error (status ' + response.status + '). Check console for details.');
                    calculateBtn.innerHTML = originalText;
                    calculateBtn.disabled = false;
                    return;
                }

                const contentType = response.headers.get('content-type');
                console.log('[admin-enrolled-details] Response content-type:', contentType);

                if (!contentType || !contentType.includes('application/json')) {
                    const htmlText = await response.text();
                    console.error('[admin-enrolled-details] Unexpected response (not JSON):', htmlText.substring(0, 500));
                    alert('❌ Calculation Failed\n\nServer returned HTML instead of JSON. This usually means an error occurred. Check console for details.');
                    calculateBtn.innerHTML = originalText;
                    calculateBtn.disabled = false;
                    return;
                }

                const result = await response.json();
                console.log('[admin-enrolled-details] Calculation result:', result);

                if (result.eligibility) {
                    // Store eligibility
                    existingEligibility = result.eligibility;

                    // Render table
                    renderEligibilityTable(existingEligibility);
                    showEligibilityTable();

                    // Success feedback
                    calculateBtn.classList.remove('btn-info');
                    calculateBtn.classList.add('btn-success');
                    calculateBtn.innerHTML = '<i class="fas fa-check me-2"></i>Calculated Successfully!';

                    // Show alert
                    showAlert('success', '✅ Eligibility calculated successfully! Review the results below and click "Save Subject Remarks & Eligibility" to persist changes.');

                    // Reset button after delay
                    setTimeout(() => {
                        calculateBtn.classList.remove('btn-success');
                        calculateBtn.classList.add('btn-info');
                        calculateBtn.innerHTML = originalText;
                        calculateBtn.disabled = false;
                    }, 3000);
                } else if (result.error) {
                    alert('❌ Calculation Failed\n\n' + result.error);
                    calculateBtn.innerHTML = originalText;
                    calculateBtn.disabled = false;
                } else {
                    alert('❌ Calculation Failed\n\nUnknown error. Check console for details.');
                    console.error('[admin-enrolled-details] Unexpected response format:', result);
                    calculateBtn.innerHTML = originalText;
                    calculateBtn.disabled = false;
                }
            } catch (error) {
                console.error('[admin-enrolled-details] Network error:', error);
                alert('❌ Network Error\n\nFailed to connect to server: ' + error.message);
                calculateBtn.innerHTML = originalText;
                calculateBtn.disabled = false;
            }
        });
    }

    // Save Remarks Form Handler
    const saveRemarksForm = document.getElementById('saveRemarksForm');
    if (saveRemarksForm) {
        console.log('[admin-enrolled-details] Save form found');

        saveRemarksForm.addEventListener('submit', function (e) {
            console.log('[admin-enrolled-details] Form submitting');

            // Inject subject remarks
            const remarksData = document.getElementById('subjectRemarksData');
            if (remarksData) {
                remarksData.innerHTML = '';
                const remarks = collectSubjectRemarks();

                for (const [code, remark] of Object.entries(remarks)) {
                    const input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = `subjectRemarks[${code}]`;
                    input.value = remark;
                    remarksData.appendChild(input);
                }

                console.log('[admin-enrolled-details] Injected', Object.keys(remarks).length, 'remarks');
            }

            // Inject eligibility if calculated
            if (Object.keys(existingEligibility).length > 0) {
                let eligData = document.getElementById('eligibilityData');
                if (!eligData) {
                    eligData = document.createElement('div');
                    eligData.id = 'eligibilityData';
                    eligData.style.display = 'none';
                    saveRemarksForm.appendChild(eligData);
                }

                eligData.innerHTML = '';

                for (const [code, status] of Object.entries(existingEligibility)) {
                    const input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = `secondSemEligibility[${code}]`;
                    input.value = status;
                    eligData.appendChild(input);
                }

                console.log('[admin-enrolled-details] Injected', Object.keys(existingEligibility).length, 'eligibility entries');
            }
        });
    }

    // Helper: Collect Subject Remarks
    function collectSubjectRemarks() {
        const remarks = {};

        console.log('[admin-enrolled-details] collectSubjectRemarks() called');

        // Method 1: Query all radio buttons in the table
        const allRadios = document.querySelectorAll('table tbody input[type="radio"]');
        console.log('[admin-enrolled-details] Found', allRadios.length, 'radio buttons');

        allRadios.forEach((radio, index) => {
            if (radio.checked) {
                // Extract subject code from name attribute: subjectRemarks[CODE]
                const match = radio.name.match(/subjectRemarks\[(.+?)\]/);
                if (match) {
                    const code = match[1];
                    const value = radio.value;
                    remarks[code] = value;
                    console.log(`[admin-enrolled-details] Radio ${index}: ${code} = ${value} (checked)`);
                } else {
                    console.log(`[admin-enrolled-details] Radio ${index}: name="${radio.name}" - no match`);
                }
            }
        });

        // If no remarks found, try alternative method
        if (Object.keys(remarks).length === 0) {
            console.log('[admin-enrolled-details] No remarks found, trying alternative method...');

            // Method 2: Query by name pattern
            const namePattern = document.querySelectorAll('input[type="radio"][name*="subjectRemarks"]');
            console.log('[admin-enrolled-details] Alternative method found', namePattern.length, 'radio buttons');

            namePattern.forEach((radio, index) => {
                if (radio.checked) {
                    const match = radio.name.match(/subjectRemarks\[(.+?)\]/);
                    if (match) {
                        const code = match[1];
                        remarks[code] = radio.value;
                        console.log(`[admin-enrolled-details] Alternative ${index}: ${code} = ${radio.value}`);
                    }
                }
            });
        }

        console.log('[admin-enrolled-details] Final remarks collected:', remarks);
        return remarks;
    }

    // Helper: Render Eligibility Table
    function renderEligibilityTable(eligibility) {
        const tbody = document.getElementById('secondSemSubjectsBody');
        if (!tbody) {
            console.error('[admin-enrolled-details] Table body not found');
            return;
        }

        tbody.innerHTML = '';

        if (secondSemSubjects.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted">No subjects configured</td></tr>';
            return;
        }

        secondSemSubjects.forEach(subject => {
            const code = subject.code || '';
            const title = subject.title || code;
            const units = subject.units || 0;
            const prereqList = subject.prereq || [];

            const eligStatus = eligibility[code] || 'Not calculated';
            const canEnroll = eligStatus.toLowerCase().startsWith('can enroll');

            const badgeClass = canEnroll ? 'bg-success' : 'bg-danger';
            const badgeIcon = canEnroll ? 'fa-check-circle' : 'fa-times-circle';

            const prereqDisplay = prereqList.length > 0
                ? prereqList.join(', ')
                : '<span class="text-muted">None</span>';

            const row = document.createElement('tr');
            row.innerHTML = `
                <td><strong>${code}</strong></td>
                <td>${title}</td>
                <td>${units}</td>
                <td><small class="text-muted">${prereqDisplay}</small></td>
                <td>
                    <span class="badge ${badgeClass}">
                        <i class="fas ${badgeIcon} me-1"></i>${eligStatus}
                    </span>
                </td>
            `;

            tbody.appendChild(row);
        });

        console.log('[admin-enrolled-details] Rendered', secondSemSubjects.length, 'subjects');
    }

    // Helper: Show Eligibility Table
    function showEligibilityTable() {
        const table = document.getElementById('secondSemesterTable');
        if (table) {
            table.classList.remove('d-none');
            setTimeout(() => {
                table.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }, 100);
        }
    }

    // Helper: Show Alert
    function showAlert(type, message) {
        const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
        const icon = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-triangle';

        // Remove existing alerts
        const existingAlerts = document.querySelectorAll('.card > .alert:not(.alert-dismissible)');
        existingAlerts.forEach(alert => alert.remove());

        const alertDiv = document.createElement('div');
        alertDiv.className = `alert ${alertClass} alert-dismissible fade show`;
        alertDiv.innerHTML = `
            <i class="fas ${icon} me-2"></i>
            <span>${message}</span>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        const card = document.querySelector('.card');
        if (card) {
            card.insertBefore(alertDiv, card.firstChild);
        }
    }

    console.log('[admin-enrolled-details] Script initialized successfully');
})();