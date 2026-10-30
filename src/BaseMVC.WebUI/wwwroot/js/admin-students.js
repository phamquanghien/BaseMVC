(() => {
  const manager = document.querySelector("[data-student-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const $ = (selector) => manager.querySelector(selector);
  const rows = $("[data-student-rows]");
  const searchInput = $("[data-student-search]");
  const pageSizeSelect = $("[data-student-page-size]");
  const countLabel = $("[data-student-count]");
  const pageLabel = $("[data-student-page-label]");
  const previousButton = $("[data-student-previous]");
  const nextButton = $("[data-student-next]");
  const errorBanner = $("[data-student-error]");
  const modal = $("[data-student-modal]");
  const modalTitle = $("[data-student-modal-title]");
  const modalDescription = $("[data-student-modal-description]");
  const modalError = $("[data-student-modal-error]");
  const fields = $("[data-student-fields]");
  const submitButton = $("[data-student-submit]");
  const cancelButton = $("[data-student-cancel]");
  const inputs = {
    code: $("#student-code"),
    dob: $("#student-dob"),
    lastName: $("#student-last-name"),
    firstName: $("#student-first-name"),
    classCode: $("#student-class-code"),
    faculty: $("#student-faculty"),
    gender: $("#student-gender"),
    english: $("#student-english"),
    language: $("#student-language"),
    it: $("#student-it"),
  };
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
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    Object.values(inputs).some(
      (input) => !(input instanceof HTMLInputElement || input instanceof HTMLSelectElement),
    ) ||
    !adminApi
  ) {
    throw new Error("Student manager markup is incomplete.");
  }

  const config = {
    searchUrl: manager.dataset.searchUrl,
    facultyOptionsUrl: manager.dataset.facultyOptionsUrl,
    createUrl: manager.dataset.createUrl,
    editUrlTemplate: manager.dataset.editUrlTemplate,
    deleteUrlTemplate: manager.dataset.deleteUrlTemplate,
    canCreate: manager.dataset.canCreate === "true",
    canUpdate: manager.dataset.canUpdate === "true",
    canDelete: manager.dataset.canDelete === "true",
  };

  if (
    [
      config.searchUrl,
      config.facultyOptionsUrl,
      config.createUrl,
      config.editUrlTemplate,
      config.deleteUrlTemplate,
    ].some((value) => !value)
  ) {
    throw new Error("Student manager endpoint configuration is incomplete.");
  }

  const columnCount = 8;
  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeStudent = null;
  let modalMode = "create";
  let isSaving = false;
  let students = new Map();

  const { showError, hideError } = adminApi;
  const request = (url, options = {}) => adminApi.request(url, options, "#student-antiforgery");

  const makeCell = (text, className = "") => {
    const cell = document.createElement("td");
    cell.textContent = text;
    if (className) cell.className = className;
    return cell;
  };

  const formatDate = (value) => {
    if (!value) return "";
    const [year, month, day] = value.split("-");
    return `${day}/${month}/${year}`;
  };

  const makeBadges = (student) => {
    const cell = document.createElement("td");
    const group = document.createElement("div");
    group.className = "flex flex-wrap gap-1";
    const badges = [
      [student.eligibleForEnglish, "Học tiếng Anh"],
      [student.passedLanguageRequirement, "Ngoại ngữ"],
      [student.passedITRequirement, "Tin học"],
    ];
    for (const [active, label] of badges) {
      if (!active) continue;
      const badge = document.createElement("span");
      badge.className = "badge badge-success badge-outline badge-sm";
      badge.textContent = label;
      group.append(badge);
    }
    if (!group.hasChildNodes()) {
      group.className = "text-base-content/50";
      group.textContent = "—";
    }
    cell.append(group);
    return cell;
  };

  const makeActionButton = (label, action, student) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      action === "delete" ? "btn btn-ghost btn-sm text-error" : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.studentAction = action;
    button.dataset.studentId = String(student.id);
    return button;
  };

  const renderRows = (items) => {
    rows.replaceChildren();
    students = new Map(items.map((student) => [String(student.id), student]));

    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell(
        "Không tìm thấy sinh viên phù hợp.",
        "py-10 text-center text-base-content/60",
      );
      cell.colSpan = columnCount;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const student of items) {
      const row = document.createElement("tr");
      row.append(makeCell(student.studentCode, "font-mono font-medium"));
      row.append(makeCell(`${student.lastName} ${student.firstName}`));
      row.append(makeCell(formatDate(student.dateOfBirth)));
      row.append(makeCell(student.gender ? "Nam" : "Nữ"));
      row.append(makeCell(student.classCode));
      row.append(makeCell(student.facultyName ? `${student.facultyCode} – ${student.facultyName}` : student.facultyCode));
      row.append(makeBadges(student));

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", student));
      if (config.canDelete) actionGroup.append(makeActionButton("Xóa", "delete", student));
      if (!config.canUpdate && !config.canDelete) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
    }
  };

  const loadStudents = async () => {
    hideError(errorBanner);
    rows.innerHTML = `<tr><td colspan="${columnCount}" class="py-10 text-center text-base-content/60">Đang tải danh sách sinh viên...</td></tr>`;

    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));

    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} sinh viên`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách sinh viên.");
    }
  };

  const loadFacultyOptions = async (selectedCode) => {
    const options = await request(config.facultyOptionsUrl);
    inputs.faculty.replaceChildren();
    const placeholder = new Option("-- Chọn khoa --", "");
    inputs.faculty.append(placeholder);
    for (const option of options) {
      inputs.faculty.append(new Option(`${option.facultyCode} – ${option.facultyName}`, option.facultyCode));
    }
    inputs.faculty.value = selectedCode ?? "";
  };

  const studentUrl = (template, studentId) =>
    template.replace(/\/0(\/|$)/, `/${encodeURIComponent(studentId)}$1`);

  const fillForm = (student) => {
    inputs.code.value = student?.studentCode ?? "";
    inputs.dob.value = student?.dateOfBirth ?? "";
    inputs.lastName.value = student?.lastName ?? "";
    inputs.firstName.value = student?.firstName ?? "";
    inputs.classCode.value = student?.classCode ?? "";
    inputs.gender.checked = student?.gender ?? false;
    inputs.english.checked = student?.eligibleForEnglish ?? false;
    inputs.language.checked = student?.passedLanguageRequirement ?? false;
    inputs.it.checked = student?.passedITRequirement ?? false;
  };

  const openModal = async (mode, student = null) => {
    modalMode = mode;
    activeStudent = student;
    hideError(modalError);
    submitButton.disabled = false;
    fields.classList.toggle("hidden", mode === "delete");
    submitButton.classList.toggle("btn-error", mode === "delete");
    submitButton.classList.toggle("btn-primary", mode !== "delete");

    if (mode === "create") {
      modalTitle.textContent = "Thêm sinh viên";
      modalDescription.textContent = "Nhập thông tin sinh viên mới.";
      submitButton.textContent = "Tạo sinh viên";
      fillForm(null);
    } else if (mode === "edit") {
      modalTitle.textContent = "Chỉnh sửa sinh viên";
      modalDescription.textContent = `Cập nhật sinh viên “${student.lastName} ${student.firstName}”.`;
      submitButton.textContent = "Lưu thay đổi";
      fillForm(student);
    } else {
      modalTitle.textContent = "Xác nhận xóa sinh viên";
      modalDescription.textContent = `Bạn có chắc muốn xóa sinh viên “${student.lastName} ${student.firstName}” (${student.studentCode}) không?`;
      submitButton.textContent = "Xóa sinh viên";
    }

    modal.showModal();
    if (mode === "delete") {
      cancelButton.focus();
      return;
    }

    try {
      await loadFacultyOptions(student?.facultyCode);
    } catch (error) {
      showError(modalError, error.message || "Không thể tải danh sách khoa.");
    }
    inputs.code.focus();
  };

  const saveStudent = async () => {
    if (isSaving) return;
    hideError(modalError);

    if (modalMode !== "delete") {
      const checks = [
        [inputs.code.value.trim(), "Vui lòng nhập mã sinh viên.", inputs.code],
        [inputs.lastName.value.trim(), "Vui lòng nhập họ.", inputs.lastName],
        [inputs.firstName.value.trim(), "Vui lòng nhập tên.", inputs.firstName],
        [inputs.classCode.value.trim(), "Vui lòng nhập mã lớp.", inputs.classCode],
        [inputs.faculty.value, "Vui lòng chọn khoa.", inputs.faculty],
      ];
      for (const [value, message, input] of checks) {
        if (!value) {
          showError(modalError, message);
          input.focus();
          return;
        }
      }
    }

    isSaving = true;
    submitButton.disabled = true;

    try {
      const body = JSON.stringify({
        studentCode: inputs.code.value,
        lastName: inputs.lastName.value,
        firstName: inputs.firstName.value,
        classCode: inputs.classCode.value,
        facultyCode: inputs.faculty.value,
        gender: inputs.gender.checked,
        eligibleForEnglish: inputs.english.checked,
        passedLanguageRequirement: inputs.language.checked,
        passedITRequirement: inputs.it.checked,
        dateOfBirth: inputs.dob.value || null,
      });
      if (modalMode === "create") {
        await request(config.createUrl, { method: "POST", body });
      } else if (modalMode === "edit" && activeStudent) {
        await request(studentUrl(config.editUrlTemplate, activeStudent.id), {
          method: "POST",
          body,
        });
      } else if (modalMode === "delete" && activeStudent) {
        await request(studentUrl(config.deleteUrlTemplate, activeStudent.id), {
          method: "POST",
        });
      }

      modal.close();
      if (modalMode === "create") page = 1;
      await loadStudents();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = false;
    }
  };

  manager.querySelector("[data-student-create]")?.addEventListener("click", () => {
    if (config.canCreate) openModal("create");
  });
  cancelButton.addEventListener("click", () => modal.close());
  submitButton.addEventListener("click", saveStudent);
  for (const input of [inputs.code, inputs.lastName, inputs.firstName, inputs.classCode]) {
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        saveStudent();
      }
    });
  }

  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-student-action]");
    if (!(button instanceof HTMLButtonElement)) return;

    const student = students.get(button.dataset.studentId ?? "");
    if (!student) return;
    if (button.dataset.studentAction === "edit" && config.canUpdate) {
      openModal("edit", student);
    } else if (button.dataset.studentAction === "delete" && config.canDelete) {
      openModal("delete", student);
    }
  });

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadStudents();
    }, 250);
  });

  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    page = 1;
    loadStudents();
  });

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadStudents();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadStudents();
    }
  });

  const importModal = $("[data-student-import-modal]");
  if (importModal instanceof HTMLDialogElement && config.canCreate && manager.dataset.importUrl) {
    const importFile = $("[data-student-import-file]");
    const importError = $("[data-student-import-error]");
    const importSummary = $("[data-student-import-summary]");
    const importErrors = $("[data-student-import-errors]");
    const importSubmit = $("[data-student-import-submit]");
    let isImporting = false;

    const resetImport = () => {
      importFile.value = "";
      hideError(importError);
      importSummary.classList.add("hidden");
      importErrors.replaceChildren();
    };

    manager.querySelector("[data-student-import]")?.addEventListener("click", () => {
      resetImport();
      importModal.showModal();
    });
    $("[data-student-import-close]").addEventListener("click", () => importModal.close());

    importSubmit.addEventListener("click", async () => {
      if (isImporting) return;
      hideError(importError);
      importSummary.classList.add("hidden");
      importErrors.replaceChildren();

      const file = importFile.files?.[0];
      if (!file) {
        showError(importError, "Vui lòng chọn file Excel (.xlsx).");
        return;
      }

      isImporting = true;
      importSubmit.disabled = true;
      try {
        const formData = new FormData();
        formData.append("file", file);
        const result = await request(manager.dataset.importUrl, { method: "POST", body: formData });
        importSummary.textContent = `Đã import ${result.importedCount}/${result.totalRows} dòng; ${result.errors.length} dòng lỗi.`;
        importSummary.classList.remove("hidden");
        for (const rowError of result.errors) {
          const item = document.createElement("li");
          item.textContent = `Dòng ${rowError.row}: ${rowError.message}`;
          importErrors.append(item);
        }
        importFile.value = "";
        if (result.importedCount > 0) {
          page = 1;
          await loadStudents();
        }
      } catch (error) {
        showError(importError, error.message || "Không thể import file.");
      } finally {
        isImporting = false;
        importSubmit.disabled = false;
      }
    });
  }

  loadStudents();
})();
