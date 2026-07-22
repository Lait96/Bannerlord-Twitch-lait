(() => {
    const copyText = async text => {
        if (navigator.clipboard && window.isSecureContext) return navigator.clipboard.writeText(text);
        const input = Object.assign(document.createElement("textarea"), { value: text, className: "copy-fallback" });
        document.body.appendChild(input);
        input.select();
        document.execCommand("copy");
        input.remove();
    };

    const toast = document.createElement("div");
    toast.className = "copy-toast";
    toast.setAttribute("role", "status");
    document.body.appendChild(toast);
    let toastTimer;
    const showToast = text => {
        toast.textContent = text;
        toast.classList.add("is-visible");
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 1600);
    };

    const setupPage = page => {
        const russian = page.dataset.uiLanguage === "ru";
        const search = page.querySelector("[data-guide-search]");
        const status = page.querySelector("[data-guide-search-status]");
        const rows = Array.from(page.querySelectorAll("tbody tr"));
        const filters = Array.from(page.querySelectorAll("[data-guide-filter]"));
        const commands = page.querySelector(".guide-section.commands");
        const rewards = page.querySelector(".guide-section.rewards");
        const globalConfigs = page.querySelector(".guide-section.global-configs");
        const classConfig = globalConfigs?.querySelector(".class-config");
        const commonConfig = globalConfigs?.querySelector(".common-config");
        const settingsConfigs = Array.from(globalConfigs?.querySelectorAll(":scope > .settings-config") || []);
        const commonGroups = [];

        if (commonConfig) {
            let group = { category: "settings", elements: [] };
            Array.from(commonConfig.children).forEach(element => {
                if (element.matches("h2")) {
                    if (group.elements.length) commonGroups.push(group);
                    group = { category: "settings", elements: [] };
                }
                group.elements.push(element);
                if (element.matches(".kill-streaks")) group.category = "streaks";
                if (element.matches(".achievements")) group.category = "achievements";
                if (element.matches(".legend")) group.category = "map";
                if (element.matches("h2") && commonGroups.some(item => item.category === "map")) group.category = "map";
            });
            if (group.elements.length) commonGroups.push(group);
        }

        const availableFilters = new Set(["all"]);
        if (commands) availableFilters.add("commands");
        if (rewards) availableFilters.add("rewards");
        if (classConfig) availableFilters.add("classes");
        if (settingsConfigs.length) availableFilters.add("settings");
        commonGroups.forEach(group => availableFilters.add(group.category));
        filters.forEach(button => button.hidden = !availableFilters.has(button.dataset.guideFilter));

        const setHidden = (element, hidden) => element?.classList.toggle("is-filter-hidden", hidden);
        const filterGuide = () => {
            const query = (search?.value || "").toLocaleLowerCase().trim();
            let visible = 0;
            rows.forEach(row => {
                const matches = !query || row.textContent.toLocaleLowerCase().includes(query);
                row.classList.toggle("is-search-hidden", !matches);
                if (matches && !row.closest(".is-filter-hidden")) visible += 1;
            });
            if (status) status.textContent = query
                ? (russian ? `Найдено записей: ${visible}` : `${visible} matching entries`)
                : (russian ? `Записей в разделе: ${visible}` : `${visible} entries in this section`);
        };

        const selectFilter = filter => {
            setHidden(commands, filter !== "all" && filter !== "commands");
            setHidden(rewards, filter !== "all" && filter !== "rewards");
            const globalFilter = ["classes", "settings", "streaks", "achievements", "map"].includes(filter);
            setHidden(globalConfigs, filter === "commands" || filter === "rewards");
            setHidden(classConfig, globalFilter && filter !== "classes");
            setHidden(commonConfig, filter === "classes");
            settingsConfigs.forEach(section => setHidden(section, globalFilter && filter !== "settings"));
            commonGroups.forEach(group => group.elements.forEach(element =>
                setHidden(element, globalFilter && filter !== "classes" && group.category !== filter)));
            filters.forEach(button => button.classList.toggle("is-active", button.dataset.guideFilter === filter));
            filterGuide();
        };

        page.querySelectorAll(".commands tbody tr").forEach(row => {
            row.querySelectorAll(":scope > td").forEach((cell, index) => {
                if (index > 1) return;
                const values = index === 0 ? [cell.textContent.trim()] : cell.textContent.split(",").map(x => x.trim());
                const buttons = values.filter(value => value && value !== "—").map(value => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "copy-chip";
                    button.textContent = value;
                    button.title = russian ? `Копировать ${value}` : `Copy ${value}`;
                    button.addEventListener("click", async () => {
                        try { await copyText(value); showToast(russian ? `Скопировано: ${value}` : `Copied: ${value}`); }
                        catch { showToast(russian ? "Не удалось скопировать команду" : "Could not copy the command"); }
                    });
                    return button;
                });
                if (buttons.length) cell.replaceChildren(...buttons);
            });
        });

        page.querySelectorAll(".starter-command").forEach(button => button.addEventListener("click", async () => {
            const value = button.textContent.trim();
            try { await copyText(value); showToast(russian ? `Скопировано: ${value}` : `Copied: ${value}`); }
            catch { showToast(russian ? "Не удалось скопировать команду" : "Could not copy the command"); }
        }));

        if (classConfig) {
            const cards = Array.from(classConfig.querySelectorAll(":scope > .class-card"));
            if (cards.length) {
                const grid = document.createElement("div");
                grid.className = "class-grid";
                const details = document.createElement("div");
                details.className = "class-details";
                const selectClass = (button, content) => {
                    grid.querySelectorAll("button").forEach(item => item.classList.toggle("is-selected", item === button));
                    details.replaceChildren(content);
                };
                cards.forEach((card, index) => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.textContent = card.querySelector("summary")?.textContent || "Class";
                    const content = card.querySelector(".class-card__content");
                    button.addEventListener("click", () => selectClass(button, content));
                    grid.appendChild(button);
                    card.remove();
                    if (index === 0) selectClass(button, content);
                });
                classConfig.append(grid, details);
            }
        }

        filters.forEach(button => button.addEventListener("click", () => selectFilter(button.dataset.guideFilter)));
        search?.addEventListener("input", filterGuide);
        selectFilter("all");
    };

    document.querySelectorAll("[data-guide-language]").forEach(setupPage);
    document.querySelectorAll("[data-language-select]").forEach(button => button.addEventListener("click", () => {
        document.querySelectorAll("[data-guide-language]").forEach(page => page.classList.toggle("is-language-hidden", page.dataset.guideLanguage !== button.dataset.languageSelect));
        document.querySelectorAll("[data-language-select]").forEach(item => item.classList.toggle("is-active", item === button));
        window.scrollTo({ top: 0, behavior: "smooth" });
    }));
})();
