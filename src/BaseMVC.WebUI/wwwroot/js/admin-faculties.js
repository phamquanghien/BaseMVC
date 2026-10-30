(() => {
  const manager = document.querySelector("[data-faculty-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-faculty-rows]");
  const searchInput = manager.querySelector("[data-faculty-search]");
  const pageSizeSelect = manager.querySelector("[data-faculty-page-size]");
  const countLabel = manager.querySelector("[data-faculty-count]");
  const pageLabel = manager.querySelector("[data-faculty-page-label]");
  const previousButton = manager.querySelector("[data-faculty-previous]");
  const nextButton = manager.querySelector("[data-faculty-next]");
  const errorBanner = manager.querySelector("[data-faculty-error]");
  const modal = manager.querySelector("[data-faculty-modal]");
  const modalTitle = manager.querySelector("[data-faculty-modal-title]");
  const modalDescription = manager.querySelector("[data-faculty-modal-description]");
  const modalError = manager.querySelector("[data-faculty-modal-error]");
  const fields = manager.querySelector("[data-faculty-fields]");
  const codeInput = manager.querySelector("#faculty-code");
  const nameInput = manager.querySelector("#faculty-name");
  const submitButton = manager.querySelector("[data-faculty-submit]");
  const cancelButton = manager.querySelector("[data-faculty-cancel]");
  const adminApi = window.BaseAdmin;

  if (
    !(rows instanceof HTMLElement) ||
    !(searchInput instanceof HTMLInputElement) ||
    !(pageSizeSelect instanceof HTMLSelectElement) ||
    !(countLabel instanceof HTMLElement) ||
    !(pageLabel instanceof HTMLElement) ||
    !(previousButton instanceof HTMLButtonElement) ||
    !(nextButton instanceof HTMLButtonElement) ||
    !(errorBanner instanceof HTMLElement) ||
    !(modal instanceof HTMLDialogElement) ||
    !(modalTitle instanceof HTMLElement) ||
    !(modalDescription instanceof HTMLElement) ||
    !(modalError instanceof HTMLElement) ||
    !(fields instanceof HTMLElement) ||
    !(codeInput instanceof HTMLInputElement) ||
    !(nameInput instanceof HTMLInputElement) ||
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    !adminApi
  ) {
    throw new Error("Faculty manager markup is incomplete.");
  }

  const config = {
    searchUrl: manager.dataset.searchUrl,
    createUrl: manager.dataset.createUrl,
    editUrlTemplate: manager.dataset.editUrlTemplate,
    deleteUrlTemplate: manager.dataset.deleteUrlTemplate,
    canCreate: manager.dataset.canCreate === "true",
    canUpdate: manager.dataset.canUpdate === "true",
    canDelete: manager.dataset.canDelete === "true",
  };

  if (
    [config.searchUrl, config.createUrl, config.editUrlTemplate, config.deleteUrlTemplate].some(
      (value) => !value,
    )
  ) {
    throw new Error("Faculty manager endpoint configuration is incomplete.");
  }

  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeFaculty = null;
  let modalMode = "create";
  let isSaving = false;

  const { showError, hideError } = adminApi;
  const request = (url, options = {}) => adminApi.request(url, options, "#faculty-antiforgery");

  const makeCell = (text, className = "") => {
    const cell = document.createElement("td");
    cell.textContent = text;
    if (className) cell.className = className;
    return cell;
  };

  const makeActionButton = (label, action, faculty) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      action === "delete" ? "btn btn-ghost btn-sm text-error" : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.facultyAction = action;
    button.dataset.facultyId = String(faculty.id);
    button.dataset.facultyCode = faculty.facultyCode;
    button.dataset.facultyName = faculty.facultyName;
    return button;
  };

  const renderRows = (items) => {
    rows.replaceChildren();

    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell("Không tìm thấy khoa phù hợp.", "py-10 text-center text-base-content/60");
      cell.colSpan = 3;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const faculty of items) {
      const row = document.createElement("tr");
      row.append(makeCell(faculty.facultyCode, "font-mono font-medium"));
      row.append(makeCell(faculty.facultyName));

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", faculty));
      if (config.canDelete) actionGroup.append(makeActionButton("Xóa", "delete", faculty));
      if (!config.canUpdate && !config.canDelete) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
    }
  };

  const loadFaculties = async () => {
    hideError(errorBanner);
    rows.innerHTML =
      '<tr><td colspan="3" class="py-10 text-center text-base-content/60">Đang tải danh sách khoa...</td></tr>';

    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));

    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} khoa`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách khoa.");
    }
  };

  const facultyUrl = (template, facultyId) =>
    template.replace(/\/0(\/|$)/, `/${encodeURIComponent(facultyId)}$1`);

  const openModal = (mode, faculty = null) => {
    modalMode = mode;
    activeFaculty = faculty;
    hideError(modalError);
    submitButton.disabled = false;
    fields.classList.toggle("hidden", mode === "delete");
    submitButton.classList.toggle("btn-error", mode === "delete");
    submitButton.classList.toggle("btn-primary", mode !== "delete");

    if (mode === "create") {
      modalTitle.textContent = "Thêm khoa";
      modalDescription.textContent = "Nhập mã khoa (2 ký tự) và tên khoa.";
      submitButton.textContent = "Tạo khoa";
      codeInput.value = "";
      nameInput.value = "";
    } else if (mode === "edit") {
      modalTitle.textContent = "Chỉnh sửa khoa";
      modalDescription.textContent = `Cập nhật khoa “${faculty.facultyName}”.`;
      submitButton.textContent = "Lưu thay đổi";
      codeInput.value = faculty.facultyCode;
      nameInput.value = faculty.facultyName;
    } else {
      modalTitle.textContent = "Xác nhận xóa khoa";
      modalDescription.textContent = `Bạn có chắc muốn xóa khoa “${faculty.facultyName}” (${faculty.facultyCode}) không?`;
      submitButton.textContent = "Xóa khoa";
      codeInput.value = "";
      nameInput.value = "";
    }

    modal.showModal();
    if (mode !== "delete") codeInput.focus();
    else cancelButton.focus();
  };

  const saveFaculty = async () => {
    if (isSaving) return;
    hideError(modalError);

    if (modalMode !== "delete") {
      if (codeInput.value.trim().length !== 2) {
        showError(modalError, "Mã khoa phải gồm đúng 2 ký tự.");
        codeInput.focus();
        return;
      }
      if (!nameInput.value.trim()) {
        showError(modalError, "Vui lòng nhập tên khoa.");
        nameInput.focus();
        return;
      }
    }

    isSaving = true;
    submitButton.disabled = true;

    try {
      const body = JSON.stringify({
        facultyCode: codeInput.value,
        facultyName: nameInput.value,
      });
      if (modalMode === "create") {
        await request(config.createUrl, { method: "POST", body });
      } else if (modalMode === "edit" && activeFaculty) {
        await request(facultyUrl(config.editUrlTemplate, activeFaculty.id), {
          method: "POST",
          body,
        });
      } else if (modalMode === "delete" && activeFaculty) {
        await request(facultyUrl(config.deleteUrlTemplate, activeFaculty.id), {
          method: "POST",
        });
      }

      modal.close();
      if (modalMode === "create") page = 1;
      await loadFaculties();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = false;
    }
  };

  manager.querySelector("[data-faculty-create]")?.addEventListener("click", () => {
    if (config.canCreate) openModal("create");
  });
  cancelButton.addEventListener("click", () => modal.close());
  submitButton.addEventListener("click", saveFaculty);
  for (const input of [codeInput, nameInput]) {
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        saveFaculty();
      }
    });
  }

  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-faculty-action]");
    if (!(button instanceof HTMLButtonElement)) return;

    const faculty = {
      id: button.dataset.facultyId,
      facultyCode: button.dataset.facultyCode,
      facultyName: button.dataset.facultyName,
    };
    if (!faculty.id) return;
    if (button.dataset.facultyAction === "edit" && config.canUpdate) {
      openModal("edit", faculty);
    } else if (button.dataset.facultyAction === "delete" && config.canDelete) {
      openModal("delete", faculty);
    }
  });

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadFaculties();
    }, 250);
  });

  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    page = 1;
    loadFaculties();
  });

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadFaculties();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadFaculties();
    }
  });

  void loadFaculties();
})();
