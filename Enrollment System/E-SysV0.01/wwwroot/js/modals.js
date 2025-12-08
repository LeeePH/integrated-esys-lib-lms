/**
 * ============================================
 * ENHANCED MODAL FUNCTIONALITY
 * ============================================
 * Provides optimized modal interactions with:
 * - Form validation
 * - Loading states
 * - Accessibility improvements
 * - Performance optimizations
 * ============================================
 */

class ModalManager {
    constructor() {
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.setupFormValidation();
        this.setupPasswordToggle();
        this.setupOTPInputs();
        this.setupPasswordRequirements();
    }

    setupEventListeners() {
        // Login form submission
        document.getElementById('loginForm')?.addEventListener('submit', this.handleLogin.bind(this));
        
        // Forgot password form
        document.getElementById('forgotPasswordForm')?.addEventListener('submit', this.handleForgotPassword.bind(this));
        
        // OTP form
        document.getElementById('otpForm')?.addEventListener('submit', this.handleOTP.bind(this));
        
        // Create password form
        document.getElementById('createPasswordForm')?.addEventListener('submit', this.handleCreatePassword.bind(this));
        
        // Application card hover effects
        document.querySelectorAll('.application-card').forEach(card => {
            card.addEventListener('mouseenter', this.handleCardHover.bind(this));
            card.addEventListener('mouseleave', this.handleCardLeave.bind(this));
        });

        // Modal show/hide events
        document.querySelectorAll('.modal').forEach(modal => {
            modal.addEventListener('show.bs.modal', this.handleModalShow.bind(this));
            modal.addEventListener('hide.bs.modal', this.handleModalHide.bind(this));
        });
    }

    setupFormValidation() {
        // Real-time email validation
        document.getElementById('loginEmail')?.addEventListener('blur', this.validateEmail.bind(this));
        document.getElementById('resetEmail')?.addEventListener('blur', this.validateEmail.bind(this));
        
        // Real-time password validation
        document.getElementById('newPassword')?.addEventListener('input', this.validatePasswordRequirements.bind(this));
        document.getElementById('confirmNewPassword')?.addEventListener('input', this.validatePasswordMatch.bind(this));
    }

    setupPasswordToggle() {
        // Login password toggle
        document.getElementById('togglePassword')?.addEventListener('click', () => {
            this.togglePasswordVisibility('loginPassword', 'togglePasswordIcon');
        });

        // New password toggle
        document.getElementById('toggleNewPassword')?.addEventListener('click', () => {
            this.togglePasswordVisibility('newPassword', 'toggleNewPasswordIcon');
        });
    }

    setupOTPInputs() {
        const otpInputs = document.querySelectorAll('.otp-input');
        
        otpInputs.forEach((input, index) => {
            input.addEventListener('input', (e) => {
                const value = e.target.value;
                
                // Move to next input if current is filled
                if (value && index < otpInputs.length - 1) {
                    otpInputs[index + 1].focus();
                }
                
                // Auto-submit if all inputs are filled
                if (Array.from(otpInputs).every(input => input.value)) {
                    document.getElementById('verifyOtpBtn').click();
                }
            });

            input.addEventListener('keydown', (e) => {
                // Move to previous input on backspace if current is empty
                if (e.key === 'Backspace' && !e.target.value && index > 0) {
                    otpInputs[index - 1].focus();
                }
            });

            input.addEventListener('paste', (e) => {
                e.preventDefault();
                const pastedData = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 4);
                pastedData.split('').forEach((char, i) => {
                    if (otpInputs[i]) {
                        otpInputs[i].value = char;
                    }
                });
                if (pastedData.length === 4) {
                    document.getElementById('verifyOtpBtn').click();
                }
            });
        });
    }

    setupPasswordRequirements() {
        const passwordInput = document.getElementById('newPassword');
        if (!passwordInput) return;

        passwordInput.addEventListener('input', () => {
            const password = passwordInput.value;
            this.updatePasswordRequirements(password);
        });
    }

    // Event Handlers
    handleLogin(e) {
        e.preventDefault();
        
        const form = e.target;
        const submitBtn = document.getElementById('loginSubmitBtn');
        const spinner = document.getElementById('loginSpinner');
        
        if (!this.validateLoginForm(form)) {
            return;
        }

        this.showLoading(submitBtn, spinner);
        
        // Simulate API call (replace with actual implementation)
        setTimeout(() => {
            this.hideLoading(submitBtn, spinner);
            form.submit();
        }, 1000);
    }

    handleForgotPassword(e) {
        e.preventDefault();
        
        const email = document.getElementById('resetEmail').value;
        const submitBtn = document.getElementById('sendCodeBtn');
        const spinner = document.getElementById('sendCodeSpinner');
        
        if (!this.validateEmail(document.getElementById('resetEmail'))) {
            return;
        }

        this.showLoading(submitBtn, spinner);
        
        // Simulate API call
        setTimeout(() => {
            this.hideLoading(submitBtn, spinner);
            this.showSuccess('Reset code sent to your email!');
            
            // Show OTP modal
            document.getElementById('otpEmail').textContent = email;
            const forgotModal = bootstrap.Modal.getInstance(document.getElementById('ForgotPasswordModal'));
            const otpModal = new bootstrap.Modal(document.getElementById('OTPModal'));
            forgotModal.hide();
            otpModal.show();
        }, 1500);
    }

    handleOTP(e) {
        e.preventDefault();
        
        const otpInputs = document.querySelectorAll('.otp-input');
        const otp = Array.from(otpInputs).map(input => input.value).join('');
        const submitBtn = document.getElementById('verifyOtpBtn');
        const spinner = document.getElementById('verifyOtpSpinner');
        
        if (otp.length !== 4) {
            this.showError('Please enter a valid 4-digit code.');
            return;
        }

        this.showLoading(submitBtn, spinner);
        
        // Simulate API call
        setTimeout(() => {
            this.hideLoading(submitBtn, spinner);
            this.showSuccess('OTP verified successfully!');
            
            // Show create password modal
            const otpModal = bootstrap.Modal.getInstance(document.getElementById('OTPModal'));
            const createPasswordModal = new bootstrap.Modal(document.getElementById('CreatePasswordModal'));
            otpModal.hide();
            createPasswordModal.show();
        }, 1000);
    }

    handleCreatePassword(e) {
        e.preventDefault();
        
        const form = e.target;
        const submitBtn = document.getElementById('createPasswordBtn');
        const spinner = document.getElementById('createPasswordSpinner');
        
        if (!this.validateCreatePasswordForm(form)) {
            return;
        }

        this.showLoading(submitBtn, spinner);
        
        // Simulate API call
        setTimeout(() => {
            this.hideLoading(submitBtn, spinner);
            this.showSuccess('Password created successfully! You can now login.');
            
            // Close modal and show login
            const createPasswordModal = bootstrap.Modal.getInstance(document.getElementById('CreatePasswordModal'));
            const loginModal = new bootstrap.Modal(document.getElementById('LoginModal'));
            createPasswordModal.hide();
            loginModal.show();
        }, 1500);
    }

    handleCardHover(e) {
        const card = e.currentTarget;
        card.style.transform = 'translateY(-5px)';
        card.style.transition = 'transform 0.3s ease';
    }

    handleCardLeave(e) {
        const card = e.currentTarget;
        card.style.transform = 'translateY(0)';
    }

    handleModalShow(e) {
        const modal = e.target;
        document.body.classList.add('modal-open');
        
        // Add fade-in animation
        modal.style.opacity = '0';
        setTimeout(() => {
            modal.style.opacity = '1';
            modal.style.transition = 'opacity 0.3s ease';
        }, 10);
    }

    handleModalHide(e) {
        const modal = e.target;
        modal.style.transition = 'opacity 0.3s ease';
        modal.style.opacity = '0';
    }

    // Validation Methods
    validateLoginForm(form) {
        const email = form.querySelector('#loginEmail');
        const password = form.querySelector('#loginPassword');
        let isValid = true;

        if (!this.validateEmail(email)) {
            isValid = false;
        }

        if (!this.validatePassword(password)) {
            isValid = false;
        }

        return isValid;
    }

    validateEmail(input) {
        const email = input.value.trim();
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        
        if (!email) {
            this.showFieldError(input, 'Email is required');
            return false;
        }
        
        if (!emailRegex.test(email)) {
            this.showFieldError(input, 'Please enter a valid email address');
            return false;
        }
        
        this.clearFieldError(input);
        return true;
    }

    validatePassword(input) {
        const password = input.value;
        
        if (!password) {
            this.showFieldError(input, 'Password is required');
            return false;
        }
        
        if (password.length < 6) {
            this.showFieldError(input, 'Password must be at least 6 characters');
            return false;
        }
        
        this.clearFieldError(input);
        return true;
    }

    validateCreatePasswordForm(form) {
        const newPassword = form.querySelector('#newPassword');
        const confirmPassword = form.querySelector('#confirmNewPassword');
        let isValid = true;

        if (!this.validatePasswordRequirements(newPassword.value)) {
            isValid = false;
        }

        if (!this.validatePasswordMatch(confirmPassword)) {
            isValid = false;
        }

        return isValid;
    }

    validatePasswordRequirements(password) {
        const requirements = {
            length: password.length >= 8,
            uppercase: /[A-Z]/.test(password),
            lowercase: /[a-z]/.test(password),
            number: /\d/.test(password),
            special: /[!@#$%^&*(),.?":{}|<>]/.test(password)
        };

        this.updatePasswordRequirements(password);
        
        return Object.values(requirements).every(req => req);
    }

    validatePasswordMatch(confirmInput) {
        const newPassword = document.getElementById('newPassword').value;
        const confirmPassword = confirmInput.value;
        
        if (!confirmPassword) {
            this.showFieldError(confirmInput, 'Please confirm your password');
            return false;
        }
        
        if (newPassword !== confirmPassword) {
            this.showFieldError(confirmInput, 'Passwords do not match');
            return false;
        }
        
        this.clearFieldError(confirmInput);
        return true;
    }

    // Utility Methods
    togglePasswordVisibility(inputId, iconId) {
        const input = document.getElementById(inputId);
        const icon = document.getElementById(iconId);
        
        if (input.type === 'password') {
            input.type = 'text';
            icon.className = 'bi bi-eye-slash';
        } else {
            input.type = 'password';
            icon.className = 'bi bi-eye';
        }
    }

    updatePasswordRequirements(password) {
        const requirements = {
            'req-length': password.length >= 8,
            'req-uppercase': /[A-Z]/.test(password),
            'req-lowercase': /[a-z]/.test(password),
            'req-number': /\d/.test(password),
            'req-special': /[!@#$%^&*(),.?":{}|<>]/.test(password)
        };

        Object.entries(requirements).forEach(([id, isValid]) => {
            const element = document.getElementById(id);
            if (element) {
                const icon = element.querySelector('i');
                if (isValid) {
                    icon.className = 'bi bi-check-circle-fill text-success me-1';
                } else {
                    icon.className = 'bi bi-circle me-1';
                }
            }
        });
    }

    showLoading(button, spinner) {
        button.disabled = true;
        spinner.classList.remove('d-none');
    }

    hideLoading(button, spinner) {
        button.disabled = false;
        spinner.classList.add('d-none');
    }

    showFieldError(input, message) {
        input.classList.add('is-invalid');
        const errorElement = document.getElementById(input.id + 'Error');
        if (errorElement) {
            errorElement.textContent = message;
        }
    }

    clearFieldError(input) {
        input.classList.remove('is-invalid');
        const errorElement = document.getElementById(input.id + 'Error');
        if (errorElement) {
            errorElement.textContent = '';
        }
    }

    showSuccess(message) {
        this.showAlert('success', message);
    }

    showError(message) {
        this.showAlert('danger', message);
    }

    showAlert(type, message) {
        const alertsContainer = document.getElementById('loginAlerts') || document.body;
        const alertId = 'alert-' + Date.now();
        
        const alertHTML = `
            <div id="${alertId}" class="alert alert-${type} alert-dismissible fade show" role="alert">
                <i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;
        
        alertsContainer.insertAdjacentHTML('afterbegin', alertHTML);
        
        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            const alert = document.getElementById(alertId);
            if (alert) {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        }, 5000);
    }
}

// Utility Functions
function printInstructions(studentType) {
    const modal = document.querySelector(`#${studentType.toLowerCase()}Modal`);
    if (modal) {
        const printContent = modal.querySelector('.modal-body').innerHTML;
        const printWindow = window.open('', '_blank');
        printWindow.document.write(`
            <html>
                <head>
                    <title>${studentType} Enrollment Instructions</title>
                    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
                    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css" rel="stylesheet">
                </head>
                <body>
                    <div class="container mt-4">
                        <h2 class="text-primary mb-4">${studentType} Enrollment Instructions</h2>
                        ${printContent}
                    </div>
                </body>
            </html>
        `);
        printWindow.document.close();
        printWindow.print();
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    new ModalManager();
});

// Export for use in other scripts
window.ModalManager = ModalManager;
