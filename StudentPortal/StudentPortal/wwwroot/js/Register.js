document.addEventListener("DOMContentLoaded", () => {
    const steps = document.querySelectorAll(".form-step");
    let currentStep = 0;

    const showStep = (index) => {
        steps.forEach((step, i) => step.classList.toggle("active", i === index));
    };

    // Step 1 → Confirm Modal
    const continueEmail = document.getElementById("continueEmail");
    const modal = document.getElementById("confirmModal");
    const modalEmail = document.getElementById("confirmEmailText");
    const modalContinue = document.getElementById("modalContinue");
    const modalBack = document.getElementById("modalBack");

    continueEmail.addEventListener("click", () => {
        const email = document.getElementById("email").value.trim();
        if (!email) return alert("Please enter your email.");
        modalEmail.textContent = email;
        modal.classList.add("active");
    });

    modalBack.addEventListener("click", () => {
        modal.classList.remove("active");
    });

    modalContinue.addEventListener("click", () => {
        modal.classList.remove("active");
        currentStep = 1;
        showStep(currentStep);
    });

    // Step 2: Password creation
    const togglePassword = document.getElementById("togglePassword");
    const passwordInput = document.getElementById("password");

    togglePassword.addEventListener("click", () => {
        const isHidden = passwordInput.type === "password";
        passwordInput.type = isHidden ? "text" : "password";
        togglePassword.className = isHidden
            ? "fa-regular fa-eye-slash"
            : "fa-regular fa-eye";
    });

    const continuePassword = document.getElementById("continuePassword");
    const backToEmail = document.getElementById("backToEmail");

    continuePassword.addEventListener("click", () => {
        const password = passwordInput.value.trim();
        if (password.length < 6)
            return alert("Password must be at least 6 characters.");
        currentStep = 2;
        showStep(currentStep);
    });

    backToEmail.addEventListener("click", () => {
        currentStep = 0;
        showStep(currentStep);
    });

    // Step 3: Verify OTP
    const verifyOtpBtn = document.getElementById("verifyOtp");
    const backToPassword = document.getElementById("backToPassword");

    verifyOtpBtn.addEventListener("click", (e) => {
        e.preventDefault();

        const otp = document.getElementById("otp").value.trim();
        const email = document.getElementById("email").value.trim();

        if (otp.length !== 6) {
            alert("Please enter a valid 6-digit OTP.");
            return;
        }

        fetch("/Account/VerifyOtp", {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: `EmailAddress=${encodeURIComponent(email)}&OTP=${encodeURIComponent(otp)}`
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    alert(data.message);
                    currentStep = 3; // move to Step 4 (account created)
                    showStep(currentStep);
                } else {
                    alert("Invalid OTP. Please try again.");
                }
            })
            .catch(() => {
                alert("Error verifying OTP. Please try again later.");
            });
    });

    backToPassword.addEventListener("click", () => {
        currentStep = 1;
        showStep(currentStep);
    });

    // Resend OTP
    document.getElementById("resendOtp").addEventListener("click", function () {
        const email = document.getElementById("email").value.trim();

        fetch("/Account/ResendOtp", {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded"
            },
            body: "email=" + encodeURIComponent(email)
        })
            .then(res => res.json())
            .then(data => {
                const status = document.getElementById("resendStatus");

                if (data.success) {
                    status.textContent = "A new OTP has been sent to your email. Please check your inbox.";
                    status.style.color = "green";

                    // ✅ Clear the previous OTP input
                    document.getElementById("otp").value = "";

                    // ✅ Inform user they need to enter the new OTP
                    alert("A new OTP has been sent. Please check your email and enter the new code.");
                } else {
                    status.textContent = data.message;
                    status.style.color = "red";
                }
            })
            .catch(() => {
                const status = document.getElementById("resendStatus");
                status.textContent = "Error resending OTP.";
                status.style.color = "red";
            });
    });


    // Send OTP (after password)
    $(document).on("click", "#continuePassword", function (e) {
        e.preventDefault();

        var form = $("#SendOtpForm");
        var formData = form.serialize();

        $.ajax({
            type: "POST",
            url: form.attr("action"),
            data: formData,
            success: function (response) {
                if (response.success) {
                    alert(response.message);
                } else {
                    alert(response.message || "Failed to send OTP.");
                }
            },
            error: function () {
                alert("Error: Unable to connect to the server.");
            }
        });
    });

    // Show first step
    showStep(currentStep);
});
