let idleTime = 0;
const maxIdle = 20; // seconds

function timerIncrement() {
    idleTime++;
    if (idleTime >= maxIdle) {
        // silently redirect to logout
        window.location.href = "/SuperAdmin/VerifyOTP";
    }
}

setInterval(timerIncrement, 1000);

document.onmousemove = document.onkeypress = document.onclick = document.onscroll = function () {
    idleTime = 0;
};
