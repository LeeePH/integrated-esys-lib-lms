function bindOther(selectId, inputId) {
    const select = document.getElementById(selectId);
    const input = document.getElementById(inputId);

    select.addEventListener("change", function () {
        if (this.value === "OTHER") {
            input.style.display = "block";
            input.required = true;
        } else {
            input.style.display = "none";
            input.required = false;
            input.value = "";
        }
    });
}

bindOther("BachelorSelect", "BachelorOther");
bindOther("MastersSelect", "MastersOther");
bindOther("PhDSelect", "PhDOther");
bindOther("LicensesSelect", "LicensesOther");