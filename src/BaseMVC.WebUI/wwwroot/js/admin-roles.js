(() => {
  const manager = document.querySelector("[data-role-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-role-rows]");
  const searchInput = manager.querySelector("[data-role-search]");
  const pageSizeSelect = manager.querySelector("[data-role-page-size]");
  const countLabel = manager.querySelector("[data-role-count]");
  const pageLabel = manager.querySelector("[data-role-page-label]");
  const previousButton = manager.querySelector("[data-role-previous]");
  const nextButton = manager.querySelector("[data-role-next]");
  const errorBanner = manager.querySelector("[data-role-error]");
  const modal = manager.querySelector("[data-role-modal]");
  const modalTitle = manager.querySelector("[data-role-modal-title]");
  const modalDescription = manager.querySelector("[data-role-modal-description]");
  const modalError = manager.querySelector("[data-role-modal-error]");
  const nameContainer = manager.querySelector("[data-role-name-container]");
  const nameInput = manager.querySelector("#role-name");
  const submitButton = manager.querySelector("[data-role-submit]");
  const cancelButton = manager.querySelector("[data-role-cancel]");
  const createButton = manager.querySelector("[data-role-create]");
  const tokenInput = document.querySelector(
    '#role-antiforgery input[name="__RequestVerificationToken"]',
  );

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
    !(nameContainer instanceof HTMLElement) ||
    !(nameInput instanceof HTMLInputElement) ||
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    !(tokenInput instanceof HTMLInputElement)
  ) {
    throw new Error("Role manager markup is incomplete.");
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
    throw new Error("Role manager endpoint configuration is incomplete.");
  }

  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeRole = null;
  let modalMode = "create";
  let isSaving = false;

  const showError = (element, message) => {
    element.textContent = message;
    element.classList.remove("hidden");
  };

  const hideError = (element) => {
    element.textContent = "";
    element.classList.add("hidden");
  };

  const errorMessage = async (response) => {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) {
      const payload = await response.json();
      if (Array.isArray(payload.errors) && payload.errors.length > 0) {
        return payload.errors.join(" ");
      }
      if (payload.errors && typeof payload.errors === "object") {
        const messages = Object.values(payload.errors).flatMap((value) =>
          Array.isArray(value) ? value : [],
        );
        if (messages.length > 0) return messages.join(" ");
      }
      if (typeof payload.title === "string") return payload.title;
    }
    return `Yêu cầu không thành công (HTTP ${response.status}).`;
  };

  const request = async (url, options = {}) => {
    const response = await fetch(url, {
      ...options,
      headers: {
        Accept: "application/json",
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...(options.method && options.method !== "GET"
          ? { RequestVerificationToken: tokenInput.value }
          : {}),
        ...options.headers,
      },
      credentials: "same-origin",
    });
    if (response.redirected) {
      throw new Error("Phiên đăng nhập không còn hợp lệ hoặc bạn không đủ quyền thao tác.");
    }
    if (!response.ok) throw new Error(await errorMessage(response));
    return response.status === 204 ? null : response.json();
  };

  const makeCell = (text, className = "") => {
    const cell = document.createElement("td");
    cell.textContent = text;
    if (className) cell.className = className;
    return cell;
  };

  const makeActionButton = (label, action, role) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      action === "delete"
        ? "btn btn-ghost btn-sm text-error"
        : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.roleAction = action;
    button.dataset.roleId = role.id;
    button.dataset.roleName = role.name;
    button.dataset.userCount = String(role.userCount);
    return button;
  };

  const renderRows = (items) => {
    rows.replaceChildren();

    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell("Không tìm thấy role phù hợp.", "py-10 text-center text-base-content/60");
      cell.colSpan = 3;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const role of items) {
      const row = document.createElement("tr");
      row.append(makeCell(role.name, "font-medium"));
      row.append(makeCell(String(role.userCount)));

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", role));
      if (config.canDelete) actionGroup.append(makeActionButton("Xóa", "delete", role));
      if (!config.canUpdate && !config.canDelete) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
    }
  };

  const loadRoles = async () => {
    hideError(errorBanner);
    rows.innerHTML =
      '<tr><td colspan="3" class="py-10 text-center text-base-content/60">Đang tải danh sách role...</td></tr>';

    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));

    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} role`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách role.");
    }
  };

  const roleUrl = (template, roleId) =>
    template.replace("__roleId__", encodeURIComponent(roleId));

  const openModal = (mode, role = null) => {
    modalMode = mode;
    activeRole = role;
    hideError(modalError);
    submitButton.disabled = false;
    nameContainer.classList.toggle("hidden", mode === "delete");
    submitButton.classList.toggle("btn-error", mode === "delete");
    submitButton.classList.toggle("btn-primary", mode !== "delete");

    if (mode === "create") {
      modalTitle.textContent = "Thêm role";
      modalDescription.textContent = "Nhập tên role mới.";
      submitButton.textContent = "Tạo role";
      nameInput.value = "";
    } else if (mode === "edit") {
      modalTitle.textContent = "Chỉnh sửa role";
      modalDescription.textContent = `Đổi tên role “${role.name}”.`;
      submitButton.textContent = "Lưu thay đổi";
      nameInput.value = role.name;
    } else {
      modalTitle.textContent = "Xác nhận xóa role";
      modalDescription.textContent =
        role.userCount > 0
          ? `Role “${role.name}” đang được gán cho ${role.userCount} tài khoản nên không thể xóa.`
          : `Bạn có chắc muốn xóa role “${role.name}” không?`;
      submitButton.textContent = "Xóa role";
      submitButton.disabled = role.userCount > 0;
      nameInput.value = "";
    }

    modal.showModal();
    if (mode !== "delete") nameInput.focus();
    else cancelButton.focus();
  };

  const saveRole = async () => {
    if (isSaving) return;
    hideError(modalError);

    if (modalMode !== "delete" && !nameInput.value.trim()) {
      showError(modalError, "Vui lòng nhập tên role.");
      nameInput.focus();
      return;
    }
    if (modalMode === "delete" && activeRole?.userCount > 0) return;

    isSaving = true;
    submitButton.disabled = true;

    try {
      if (modalMode === "create") {
        await request(config.createUrl, {
          method: "POST",
          body: JSON.stringify({ name: nameInput.value }),
        });
      } else if (modalMode === "edit" && activeRole) {
        await request(roleUrl(config.editUrlTemplate, activeRole.id), {
          method: "POST",
          body: JSON.stringify({ name: nameInput.value }),
        });
      } else if (modalMode === "delete" && activeRole) {
        await request(roleUrl(config.deleteUrlTemplate, activeRole.id), {
          method: "POST",
        });
      }

      modal.close();
      if (modalMode === "create") page = 1;
      await loadRoles();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = modalMode === "delete" && activeRole?.userCount > 0;
    }
  };

  manager.querySelector("[data-role-create]")?.addEventListener("click", () => {
    if (config.canCreate) openModal("create");
  });
  cancelButton.addEventListener("click", () => modal.close());
  submitButton.addEventListener("click", saveRole);
  nameInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      saveRole();
    }
  });

  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-role-action]");
    if (!(button instanceof HTMLButtonElement)) return;

    const role = {
      id: button.dataset.roleId,
      name: button.dataset.roleName,
      userCount: Number(button.dataset.userCount),
    };
    if (!role.id || !role.name) return;
    if (button.dataset.roleAction === "edit" && config.canUpdate) {
      openModal("edit", role);
    } else if (button.dataset.roleAction === "delete" && config.canDelete) {
      openModal("delete", role);
    }
  });

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadRoles();
    }, 250);
  });

  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    page = 1;
    loadRoles();
  });

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadRoles();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadRoles();
    }
  });

  void loadRoles();
})();
