(() => {
  const initializeToc = () => {
    const content = document.querySelector(".main-content");
    const toc = document.querySelector("[data-doc-toc]");
    const details = document.querySelector(".doc-toc__details");

    if (!content || !toc || !details) return;

    if (window.matchMedia("(max-width: 720px)").matches) {
      details.open = false;
    }

    const headings = Array.from(content.querySelectorAll("h2, h3"));
    if (!headings.length) {
      details.closest(".doc-toc")?.remove();
      return;
    }

    const usedIds = new Set();
    const slugify = (text) => text
      .toLocaleLowerCase("ru-RU")
      .trim()
      .replace(/[^\p{L}\p{N}\s-]/gu, "")
      .replace(/\s+/g, "-")
      .replace(/-+/g, "-");

    headings.forEach((heading, index) => {
      let id = heading.id || slugify(heading.textContent) || `section-${index + 1}`;
      const baseId = id;
      let suffix = 2;

      while (usedIds.has(id) || (document.getElementById(id) && document.getElementById(id) !== heading)) {
        id = `${baseId}-${suffix++}`;
      }

      heading.id = id;
      usedIds.add(id);

      const link = document.createElement("a");
      link.className = `doc-toc__link doc-toc__link--${heading.tagName.toLowerCase()}`;
      link.href = `#${encodeURIComponent(id)}`;
      link.textContent = heading.textContent;
      link.dataset.targetId = id;
      toc.appendChild(link);

      link.addEventListener("click", () => {
        if (window.matchMedia("(max-width: 1679px)").matches) {
          details.open = false;
        }
      });
    });

    const links = Array.from(toc.querySelectorAll(".doc-toc__link"));
    const activate = (id) => {
      links.forEach((link) => {
        const isActive = link.dataset.targetId === id;
        link.classList.toggle("is-active", isActive);
        if (isActive) link.setAttribute("aria-current", "location");
        else link.removeAttribute("aria-current");
      });
    };

    const updateActiveHeading = () => {
      const offset = 150;
      let active = headings[0];

      for (const heading of headings) {
        if (heading.getBoundingClientRect().top <= offset) active = heading;
        else break;
      }

      activate(active.id);
    };

    let scheduled = false;
    const scheduleUpdate = () => {
      if (scheduled) return;
      scheduled = true;
      window.requestAnimationFrame(() => {
        updateActiveHeading();
        scheduled = false;
      });
    };

    window.addEventListener("scroll", scheduleUpdate, { passive: true });
    window.addEventListener("resize", scheduleUpdate, { passive: true });
    updateActiveHeading();
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeToc, { once: true });
  } else {
    initializeToc();
  }
})();
