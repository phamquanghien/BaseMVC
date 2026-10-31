(() => {
  const manager = document.querySelector("[data-exam-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-exam-rows]");
  const searchInput = manager.querySelector("[data-exam-search]");
  const typeFilter = manager.querySelector("[data-exam-type-filter]");
  const statusFilter = manager.querySelector("[data-exam-status-filter]");
  const includeDeleted = manager.querySelector("[data-exam-include-deleted]");
  const pageSizeSelect = manager.querySelector("[data-exam-page-size]");
  const countLabel = manager.querySelector("[data-exam-count]");
  const pageLabel = manager.querySelector("[data-exam-page-label]");
  const previousButton = manager.querySelector("[data-exam-previous]");
  const nextButton = manager.querySelector("[data-exam-next]");
  const errorBanner = manager.querySelector("[data-exam-error]");
  const modal = manager.querySelector("[data-exam-modal]");
  const modalTitle = manager.querySelector("[data-exam-modal-title]");
  const modalDescription = manager.querySelector("[data-exam-modal-description]");
  const modalError = manager.querySelector("[data-exam-modal-error]");
  const fields = manager.querySelector("[data-exam-fields]");
  const nameInput = manager.querySelector("#exam-name");
  const typeSelect = manager.querySelector("#exam-type");
  const typeHint = manager.querySelector("[data-exam-type-hint]");
  const submitButton = manager.querySelector("[data-exam-submit]");
  const cancelButton = manager.querySelector("[data-exam-cancel]");
  const adminApi = window.BaseAdmin;

  if (
    !(rows instanceof HTMLElement) ||
    !(searchInput instanceof HTMLInputElement) ||
    !(typeFilter instanceof HTMLSelectElement) ||
    !(statusFilter instanceof HTMLSelectElement) ||
    !(includeDeleted instanceof HTMLInputElement) ||
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
    !(typeSelect instanceof HTMLSelectElement) ||
    !(typeHint instanceof HTMLElement) ||
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    !adminApi
  ) {
    throw new Error("Exam manager markup is incomplete.");
  }

  const config = {
    searchUrl: manager.dataset.searchUrl,
    examTypesUrl: manager.dataset.examTypesUrl,
    createUrl: manager.dataset.createUrl,
    detailUrlTemplate: manager.dataset.detailUrlTemplate,
    editUrlTemplate: manager.dataset.editUrlTemplate,
    deleteUrlTemplate: manager.dataset.deleteUrlTemplate,
    canCreate: manager.dataset.canCreate === "true",
    canUpdate: manager.dataset.canUpdate === "true",
    canDelete: manager.dataset.canDelete === "true",
  };

  if (
    [config.searchUrl, config.examTypesUrl, config.createUrl, config.editUrlTemplate, config.deleteUrlTemplate].some(
      (value) => !value,
    )
  ) {
    throw new Error("Exam manager endpoint configuration is incomplete.");
  }

  const statusLabels = {
    NotStarted: "Chưa bắt đầu",
    RegistrationOpen: "Mở đăng ký",
    RegistrationCompleted: "Đã hoàn thành đăng ký",
    AwaitingSchedule: "Chờ xếp lịch thi",
    ScheduleArranged: "Đã xếp lịch thi",
    Taken: "Đã thi (Chờ kết quả)",
    ResultsEntered: "Đã nhập kết quả",
    Completed: "Hoàn thành",
  };

  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeExam = null;
  let modalMode = "create";
  let isSaving = false;
  let examTypes = [];

  const { showError, hideError } = adminApi;
  const request = (url, options = {}) => adminApi.request(url, options, "#exam-antiforgery");

  const makeCell = (text, className = "") => {
    const cell = document.createElement("td");
    cell.textContent = text;
    if (className) cell.className = className;
    return cell;
  };

  const makeActionButton = (label, action, exam) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      action === "delete" ? "btn btn-ghost btn-sm text-error" : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.examAction = action;
    button.dataset.examId = String(exam.id);
    return button;
  };

  const renderRows = (items) => {
    rows.replaceChildren();

    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell("Không tìm thấy kỳ thi phù hợp.", "py-10 text-center text-base-content/60");
      cell.colSpan = 4;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const exam of items) {
      const row = document.createElement("tr");
      if (exam.isDeleted) row.className = "opacity-60";
      const nameCell = makeCell("");
      if (exam.isDeleted || !config.detailUrlTemplate) {
        nameCell.textContent = exam.name;
      } else {
        const link = document.createElement("a");
        link.className = "link link-hover font-medium";
        link.href = examUrl(config.detailUrlTemplate, exam.id);
        link.textContent = exam.name;
        nameCell.append(link);
      }
      row.append(nameCell);
      row.append(makeCell(exam.examTypeName));

      const statusCell = document.createElement("td");
      statusCell.className = "whitespace-nowrap";
      const badge = document.createElement("span");
      badge.className = exam.isDeleted ? "badge badge-ghost whitespace-nowrap" : "badge badge-outline whitespace-nowrap";
      badge.textContent = exam.isDeleted
        ? `Đã xóa (${statusLabels[exam.status] ?? exam.status})`
        : (statusLabels[exam.status] ?? exam.status);
      statusCell.append(badge);
      row.append(statusCell);

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (exam.isDeleted) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      } else {
        if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", exam));
        if (config.canDelete) actionGroup.append(makeActionButton("Xóa", "delete", exam));
        if (!config.canUpdate && !config.canDelete) {
          actionGroup.className = "text-base-content/50";
          actionGroup.textContent = "—";
        }
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
      row.dataset.examJson = JSON.stringify(exam);
    }
  };

  const fillOptions = (select, items, firstOption) => {
    select.replaceChildren();
    if (firstOption) {
      const option = document.createElement("option");
      option.value = "";
      option.textContent = firstOption;
      select.append(option);
    }
    for (const item of items) {
      const option = document.createElement("option");
      option.value = String(item.value);
      option.textContent = item.label;
      select.append(option);
    }
  };

  const loadExamTypes = async () => {
    try {
      examTypes = await request(config.examTypesUrl);
      const current = typeFilter.value;
      fillOptions(
        typeFilter,
        examTypes.map((item) => ({ value: item.id, label: item.name })),
        "Tất cả loại kỳ thi",
      );
      typeFilter.value = current;
      fillOptions(
        typeSelect,
        examTypes.map((item) => ({ value: item.id, label: item.name })),
      );
    } catch (error) {
      showError(errorBanner, error.message || "Không thể tải danh sách loại kỳ thi.");
    }
  };

  fillOptions(
    statusFilter,
    Object.entries(statusLabels).map(([value, label]) => ({ value, label })),
    "Tất cả trạng thái",
  );

  const loadExams = async () => {
    hideError(errorBanner);
    rows.innerHTML =
      '<tr><td colspan="4" class="py-10 text-center text-base-content/60">Đang tải danh sách kỳ thi...</td></tr>';

    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    if (typeFilter.value) url.searchParams.set("examTypeId", typeFilter.value);
    if (statusFilter.value) url.searchParams.set("status", statusFilter.value);
    url.searchParams.set("includeDeleted", String(includeDeleted.checked));
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));

    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} kỳ thi`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách kỳ thi.");
    }
  };

  const examUrl = (template, examId) =>
    template.replace(/\/0(\/|$)/, `/${encodeURIComponent(examId)}$1`);

  const openModal = (mode, exam = null) => {
    modalMode = mode;
    activeExam = exam;
    hideError(modalError);
    submitButton.disabled = false;
    fields.classList.toggle("hidden", mode === "delete");
    submitButton.classList.toggle("btn-error", mode === "delete");
    submitButton.classList.toggle("btn-primary", mode !== "delete");
    typeHint.classList.add("hidden");
    typeSelect.disabled = false;

    if (mode === "create") {
      modalTitle.textContent = "Thêm kỳ thi";
      modalDescription.textContent = "Nhập tên và chọn loại kỳ thi. Kỳ thi mới có trạng thái “Chưa bắt đầu”.";
      submitButton.textContent = "Tạo kỳ thi";
      nameInput.value = "";
    } else if (mode === "edit") {
      modalTitle.textContent = "Chỉnh sửa kỳ thi";
      modalDescription.textContent = `Cập nhật kỳ thi “${exam.name}”.`;
      submitButton.textContent = "Lưu thay đổi";
      nameInput.value = exam.name;
      typeSelect.value = String(exam.examTypeId);
      if (exam.status !== "NotStarted") {
        typeSelect.disabled = true;
        typeHint.classList.remove("hidden");
      }
    } else {
      const soft = exam.status !== "NotStarted";
      modalTitle.textContent = "Xác nhận xóa kỳ thi";
      modalDescription.textContent = soft
        ? `Kỳ thi “${exam.name}” đã bắt đầu nên sẽ được ẩn khỏi danh sách (xóa mềm), dữ liệu vẫn được giữ lại. Bạn có chắc không?`
        : `Bạn có chắc muốn xóa vĩnh viễn kỳ thi “${exam.name}” không?`;
      submitButton.textContent = "Xóa kỳ thi";
    }

    modal.showModal();
    if (mode !== "delete") nameInput.focus();
    else cancelButton.focus();
  };

  const saveExam = async () => {
    if (isSaving) return;
    hideError(modalError);

    if (modalMode !== "delete") {
      if (!nameInput.value.trim()) {
        showError(modalError, "Vui lòng nhập tên kỳ thi.");
        nameInput.focus();
        return;
      }
      if (!typeSelect.value) {
        showError(modalError, "Vui lòng chọn loại kỳ thi.");
        typeSelect.focus();
        return;
      }
    }

    isSaving = true;
    submitButton.disabled = true;

    try {
      const body = JSON.stringify({
        name: nameInput.value,
        examTypeId: Number(typeSelect.value),
      });
      if (modalMode === "create") {
        await request(config.createUrl, { method: "POST", body });
      } else if (modalMode === "edit" && activeExam) {
        await request(examUrl(config.editUrlTemplate, activeExam.id), { method: "POST", body });
      } else if (modalMode === "delete" && activeExam) {
        await request(examUrl(config.deleteUrlTemplate, activeExam.id), { method: "POST" });
      }

      modal.close();
      if (modalMode === "create") page = 1;
      await loadExams();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = false;
    }
  };

  manager.querySelector("[data-exam-create]")?.addEventListener("click", () => {
    if (config.canCreate) openModal("create");
  });
  cancelButton.addEventListener("click", () => modal.close());
  submitButton.addEventListener("click", saveExam);
  nameInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      saveExam();
    }
  });

  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-exam-action]");
    if (!(button instanceof HTMLButtonElement)) return;
    const json = button.closest("tr")?.dataset.examJson;
    if (!json) return;

    const exam = JSON.parse(json);
    if (button.dataset.examAction === "edit" && config.canUpdate) {
      openModal("edit", exam);
    } else if (button.dataset.examAction === "delete" && config.canDelete) {
      openModal("delete", exam);
    }
  });

  const reloadFromFirstPage = () => {
    page = 1;
    loadExams();
  };

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(reloadFromFirstPage, 250);
  });
  typeFilter.addEventListener("change", reloadFromFirstPage);
  statusFilter.addEventListener("change", reloadFromFirstPage);
  includeDeleted.addEventListener("change", reloadFromFirstPage);

  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    reloadFromFirstPage();
  });

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadExams();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadExams();
    }
  });

  void loadExamTypes();
  void loadExams();
})();
