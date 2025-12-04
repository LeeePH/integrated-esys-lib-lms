(function () {
    'use strict';

    console.log('[SubjectSelection2ndYear] Initializing...');

    const form = document.getElementById('subjectSelectionForm');
    const checkboxes = document.querySelectorAll('.subject-checkbox');
    const selectAllBtn = document.getElementById('selectAllEligible');
    const submitBtn = document.getElementById('submitBtn');
    const selectedCountEl = document.getElementById('selectedCount');
    const totalUnitsEl = document.getElementById('totalUnits');
    const warningContainer = document.getElementById('warningMessages');

    if (!form || checkboxes.length === 0) {
        console.error('[SubjectSelection2ndYear] Required elements not found');
        return;
    }

    // Update selection summary
    function updateSummary() {
        let selectedCount = 0;
        let totalUnits = 0;

        checkboxes.forEach(cb => {
            if (cb.checked && cb.dataset.eligible === 'true') {
                selectedCount++;
                totalUnits += parseInt(cb.dataset.units || 0);
            }
        });

        selectedCountEl.textContent = selectedCount;
        totalUnitsEl.textContent = totalUnits;

        // Enable/disable submit button
        submitBtn.disabled = selectedCount === 0;

        // Show warnings
        showWarnings(selectedCount, totalUnits);

        console.log(`[SubjectSelection2ndYear] Selected: ${selectedCount}, Units: ${totalUnits}`);
    }

    // Show warning messages
    function showWarnings(count, units) {
        warningContainer.innerHTML = '';

        if (count === 0) {
            warningContainer.innerHTML = `
                <div class="alert alert-warning">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    Please select at least one subject to submit your enrollment.
                </div>
            `;
        } else if (units < 15) {
            warningContainer.innerHTML = `
                <div class="alert alert-info">
                    <i class="fas fa-info-circle me-2"></i>
                    You have selected fewer than 15 units. Consider selecting more subjects if eligible.
                </div>
            `;
        } else if (units > 24) {
            warningContainer.innerHTML = `
                <div class="alert alert-warning">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    You have selected more than 24 units. This may require special approval.
                </div>
            `;
        }
    }

    // Select all eligible subjects
    selectAllBtn.addEventListener('change', function () {
        const isChecked = this.checked;

        checkboxes.forEach(cb => {
            if (cb.dataset.eligible === 'true') {
                cb.checked = isChecked;
            }
        });

        updateSummary();
    });

    // Update summary when individual checkbox changes
    checkboxes.forEach(cb => {
        cb.addEventListener('change', updateSummary);
    });

    // Form submission validation
    form.addEventListener('submit', function (e) {
        const selected = Array.from(checkboxes).filter(cb => cb.checked);

        if (selected.length === 0) {
            e.preventDefault();
            alert('⚠️ Please select at least one subject before submitting.');
            return false;
        }

        // Confirm submission
        const subjectList = selected.map(cb => cb.value).join(', ');
        const totalUnits = selected.reduce((sum, cb) => sum + parseInt(cb.dataset.units || 0), 0);

        const confirmed = confirm(
            `📋 Confirm 2nd Year Enrollment\n\n` +
            `Selected Subjects: ${selected.length}\n` +
            `Total Units: ${totalUnits}\n\n` +
            `Subjects: ${subjectList}\n\n` +
            `Click OK to submit your enrollment request.`
        );

        if (!confirmed) {
            e.preventDefault();
            return false;
        }

        // Show loading state
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Submitting...';

        console.log('[SubjectSelection2ndYear] Form submitted');
    });

    // Initial summary update
    updateSummary();

    console.log('[SubjectSelection2ndYear] Initialized successfully');
})();