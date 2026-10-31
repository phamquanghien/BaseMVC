// Collapsible navbar dropdowns: close on outside click, Escape, or navigation; swap the hamburger icon.
(function () {
    const dropdowns = document.querySelectorAll("details[data-nav-dropdown]");

    function syncIcons(details) {
        const open = details.querySelector("[data-nav-icon-open]");
        const close = details.querySelector("[data-nav-icon-close]");
        if (open && close) {
            open.classList.toggle("hidden", details.open);
            close.classList.toggle("hidden", !details.open);
        }
    }

    dropdowns.forEach(function (details) {
        details.addEventListener("toggle", function () {
            if (details.open) {
                dropdowns.forEach(function (other) {
                    if (other !== details) { other.open = false; }
                });
            }
            syncIcons(details);
        });
    });

    document.addEventListener("click", function (event) {
        dropdowns.forEach(function (details) {
            if (details.open && !details.contains(event.target)) { details.open = false; }
        });
    });

    document.addEventListener("keydown", function (event) {
        if (event.key !== "Escape") { return; }
        dropdowns.forEach(function (details) {
            if (details.open) {
                details.open = false;
                const summary = details.querySelector("summary");
                if (summary) { summary.focus(); }
            }
        });
    });
})();
