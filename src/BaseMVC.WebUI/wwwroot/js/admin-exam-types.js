(() => {
  const manager = document.querySelector("[data-exam-type-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-exam-type-rows]");
  const searchInput = manager.querySelector("[data-exam-type-search]");
  const pageSizeSelect = manager.querySelector("[data-exam-type-page-size]");
  const countLabel = manager.querySelector("[data-exam-type-count]");
  const pageLabel = manager.querySelector("[data-exam-type-page-label]");
  const previousButton = manager.querySelector("[data-exam-type-previous]");
  const nextButton = manager.querySelector("[data-exam-type-next]");
  const errorBanner = manager.querySelector("[data-exam-type-error]");
  const modal = manager.querySelector("[data-exam-type-modal]");
  const modalTitle = manager.querySelector("[data-exam-type-modal-title]");
  const modalDescription = manager.querySelector("[data-exam-type-modal-description]");
  const modalError = manager.querySelector("[data-exam-type-modal-error]");
  const fields = manager.querySelector("[data-exam-type-fields]");
  const nameInput = manager.querySelector("#exam-type-name");
  const submitButton = manager.querySelector("[data-exam-type-submit]");
  const cancelButton = manager.querySelector("[data-exam-type-cancel]");
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
    !(nameInput instanceof HTMLInputElement) ||
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    !adminApi
  ) {
    throw new Error("ExamType manager markup is incomplete.");
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
    throw new Error("ExamType manager endpoint configuration is incomplete.");
  }

  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeExamType = null;
  let modalMode = "create";
  let isSaving = false;

  const { showError, hideError } = adminApi;
  const formatLabels = {
    ItStandard: "Chuẩn đầu ra Công nghệ thông tin",
    LanguageStandard: "Chuẩn đầu ra Ngoại ngữ",
    LanguagePlacement: "Kiểm tra trình độ Ngoại ngữ",
    EnglishIntensive: "Tiếng Anh tăng cường",
    ItBasicCertificate: "Chứng chỉ ứng dụng CNTT cơ bản",
  };
  const dataFormatSelect = manager.querySelector("#exam-type-data-format");
  const request = (url, options = {}) => adminApi.request(url, options, "#exam-type-antiforgery");

  const makeCell = (text, className = "") => {
    const cell = document.createElement("td");
    cell.textContent = text;
    if (className) cell.className = className;
    return cell;
  };

  const makeActionButton = (label, action, examType) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      action === "delete" ? "btn btn-ghost btn-sm text-error" : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.examTypeAction = action;
    button.dataset.examTypeId = String(examType.id);
    button.dataset.name = examType.name;
    button.dataset.dataFormat = examType.dataFormat || "";
    return button;
  };

  const renderRows = (items) => {
    rows.replaceChildren();

    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell("Không tìm thấy loại kỳ thi phù hợp.", "py-10 text-center text-base-content/60");
      cell.colSpan = 3;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const examType of items) {
      const row = document.createElement("tr");
      row.append(makeCell(examType.name));
      row.append(
        makeCell(
          formatLabels[examType.dataFormat] || "Chưa gán",
          examType.dataFormat ? "" : "text-warning",
        ),
      );

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", examType));
      if (config.canDelete) actionGroup.append(makeActionButton("Xóa", "delete", examType));
      if (!config.canUpdate && !config.canDelete) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
    }
  };

  const loadExamTypes = async () => {
    hideError(errorBanner);
    rows.innerHTML =
      '<tr><td colspan="3" class="py-10 text-center text-base-content/60">Đang tải danh sách loại kỳ thi...</td></tr>';

    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));

    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} loại kỳ thi`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách loại kỳ thi.");
    }
  };

  const examTypeUrl = (template, examTypeId) =>
    template.replace(/\/0(\/|$)/, `/${encodeURIComponent(examTypeId)}$1`);

  const openModal = (mode, examType = null) => {
    modalMode = mode;
    activeExamType = examType;
    hideError(modalError);
    submitButton.disabled = false;
    fields.classList.toggle("hidden", mode === "delete");
    submitButton.classList.toggle("btn-error", mode === "delete");
    submitButton.classList.toggle("btn-primary", mode !== "delete");

    if (mode === "create") {
      modalTitle.textContent = "Thêm loại kỳ thi";
      modalDescription.textContent = "Nhập tên loại kỳ thi.";
      submitButton.textContent = "Tạo loại kỳ thi";
      nameInput.value = "";
      dataFormatSelect.value = "";
    } else if (mode === "edit") {
      modalTitle.textContent = "Chỉnh sửa loại kỳ thi";
      modalDescription.textContent = `Cập nhật loại kỳ thi “${examType.name}”.`;
      submitButton.textContent = "Lưu thay đổi";
      nameInput.value = examType.name;
      dataFormatSelect.value = examType.dataFormat || "";
    } else {
      modalTitle.textContent = "Xác nhận xóa loại kỳ thi";
      modalDescription.textContent = `Bạn có chắc muốn xóa loại kỳ thi “${examType.name}” không?`;
      submitButton.textContent = "Xóa loại kỳ thi";
      nameInput.value = "";
    }

    modal.showModal();
    if (mode !== "delete") nameInput.focus();
    else cancelButton.focus();
  };

  const saveExamType = async () => {
    if (isSaving) return;
    hideError(modalError);

    if (modalMode !== "delete") {
      if (!nameInput.value.trim()) {
        showError(modalError, "Vui lòng nhập tên loại kỳ thi.");
        nameInput.focus();
        return;
      }
    }

    isSaving = true;
    submitButton.disabled = true;

    try {
      const body = JSON.stringify({
        name: nameInput.value,
        dataFormat: dataFormatSelect.value || null,
      });
      if (modalMode === "create") {
        await request(config.createUrl, { method: "POST", body });
      } else if (modalMode === "edit" && activeExamType) {
        await request(examTypeUrl(config.editUrlTemplate, activeExamType.id), {
          method: "POST",
          body,
        });
      } else if (modalMode === "delete" && activeExamType) {
        await request(examTypeUrl(config.deleteUrlTemplate, activeExamType.id), {
          method: "POST",
        });
      }

      modal.close();
      if (modalMode === "create") page = 1;
      await loadExamTypes();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = false;
    }
  };

  manager.querySelector("[data-exam-type-create]")?.addEventListener("click", () => {
    if (config.canCreate) openModal("create");
  });
  cancelButton.addEventListener("click", () => modal.close());
  submitButton.addEventListener("click", saveExamType);
  for (const input of [nameInput]) {
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        saveExamType();
      }
    });
  }

  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-exam-type-action]");
    if (!(button instanceof HTMLButtonElement)) return;

    const examType = {
      id: button.dataset.examTypeId,
      name: button.dataset.name,
      dataFormat: button.dataset.dataFormat,
    };
    if (!examType.id) return;
    if (button.dataset.examTypeAction === "edit" && config.canUpdate) {
      openModal("edit", examType);
    } else if (button.dataset.examTypeAction === "delete" && config.canDelete) {
      openModal("delete", examType);
    }
  });

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadExamTypes();
    }, 250);
  });

  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    page = 1;
    loadExamTypes();
  });

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadExamTypes();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadExamTypes();
    }
  });

  void loadExamTypes();
})();
