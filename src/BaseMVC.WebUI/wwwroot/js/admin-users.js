(() => {
  const manager = document.querySelector("[data-user-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-user-rows]");
  const searchInput = manager.querySelector("[data-user-search]");
  const pageSizeSelect = manager.querySelector("[data-user-page-size]");
  const countLabel = manager.querySelector("[data-user-count]");
  const pageLabel = manager.querySelector("[data-user-page-label]");
  const previousButton = manager.querySelector("[data-user-previous]");
  const nextButton = manager.querySelector("[data-user-next]");
  const errorBanner = manager.querySelector("[data-user-error]");
  const createButton = manager.querySelector("[data-user-create]");
  const modal = manager.querySelector("[data-user-modal]");
  const modalTitle = manager.querySelector("[data-user-modal-title]");
  const modalDescription = manager.querySelector("[data-user-modal-description]");
  const modalError = manager.querySelector("[data-user-modal-error]");
  const emailInput = manager.querySelector("#user-email");
  const passwordContainer = manager.querySelector("[data-user-password-container]");
  const passwordInput = manager.querySelector("#user-password");
  const lockContainer = manager.querySelector("[data-user-lock-container]");
  const lockedInput = manager.querySelector("[data-user-locked]");
  const roleContainer = manager.querySelector("[data-user-roles]");
  const submitButton = manager.querySelector("[data-user-submit]");
  const cancelButton = manager.querySelector("[data-user-cancel]");
  const deleteModal = manager.querySelector("[data-user-delete-modal]");
  const deleteDescription = manager.querySelector("[data-user-delete-description]");
  const deleteError = manager.querySelector("[data-user-delete-error]");
  const deleteCancel = manager.querySelector("[data-user-delete-cancel]");
  const deleteConfirm = manager.querySelector("[data-user-delete-confirm]");
  const tokenInput = document.querySelector(
    '#user-antiforgery input[name="__RequestVerificationToken"]',
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
    !(emailInput instanceof HTMLInputElement) ||
    !(passwordContainer instanceof HTMLElement) ||
    !(passwordInput instanceof HTMLInputElement) ||
    !(lockContainer instanceof HTMLElement) ||
    !(lockedInput instanceof HTMLInputElement) ||
    !(roleContainer instanceof HTMLElement) ||
    !(submitButton instanceof HTMLButtonElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    !(deleteModal instanceof HTMLDialogElement) ||
    !(deleteDescription instanceof HTMLElement) ||
    !(deleteError instanceof HTMLElement) ||
    !(deleteCancel instanceof HTMLButtonElement) ||
    !(deleteConfirm instanceof HTMLButtonElement) ||
    !(tokenInput instanceof HTMLInputElement)
  ) {
    throw new Error("User manager markup is incomplete.");
  }

  const config = {
    searchUrl: manager.dataset.searchUrl,
    rolesUrl: manager.dataset.rolesUrl,
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
      config.rolesUrl,
      config.createUrl,
      config.editUrlTemplate,
      config.deleteUrlTemplate,
    ].some((value) => !value)
  ) {
    throw new Error("User manager endpoint configuration is incomplete.");
  }

  let page = 1;
  let pageSize = Number(pageSizeSelect.value);
  let totalPages = 0;
  let searchTimer;
  let activeUser = null;
  let modalMode = "create";
  let isSaving = false;
  let assignableRoles = [];

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
  const makeActionButton = (label, action, user) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = action === "delete"
      ? "btn btn-ghost btn-sm text-error"
      : "btn btn-ghost btn-sm";
    button.textContent = label;
    button.dataset.userAction = action;
    button.dataset.userId = user.id;
    button.dataset.userEmail = user.email;
    button.dataset.userLocked = String(user.isLocked);
    button.dataset.userCurrent = String(user.isCurrentUser);
    button.dataset.userRoles = JSON.stringify(user.roleNames);
    return button;
  };
  const renderRows = (items) => {
    rows.replaceChildren();
    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = makeCell("Không tìm thấy người dùng phù hợp.", "py-10 text-center text-base-content/60");
      cell.colSpan = 4;
      row.append(cell);
      rows.append(row);
      return;
    }

    for (const user of items) {
      const row = document.createElement("tr");
      row.append(makeCell(user.email, "font-medium"));
      row.append(makeCell(user.roleNames.join(", ") || "Chưa gán role"));
      const status = document.createElement("td");
      const badge = document.createElement("span");
      badge.className = user.isLocked ? "badge badge-error badge-outline" : "badge badge-success badge-outline";
      badge.textContent = user.isLocked ? "Đã khóa" : "Hoạt động";
      status.append(badge);
      row.append(status);

      const actions = document.createElement("td");
      actions.className = "text-right";
      const actionGroup = document.createElement("div");
      actionGroup.className = "flex justify-end gap-1";
      if (config.canUpdate) actionGroup.append(makeActionButton("Sửa", "edit", user));
      if (config.canDelete && !user.isCurrentUser) {
        actionGroup.append(makeActionButton("Xóa", "delete", user));
      }
      if (!config.canUpdate && !config.canDelete) {
        actionGroup.className = "text-base-content/50";
        actionGroup.textContent = "—";
      }
      actions.append(actionGroup);
      row.append(actions);
      rows.append(row);
    }
  };
  const loadUsers = async () => {
    hideError(errorBanner);
    rows.innerHTML =
      '<tr><td colspan="4" class="py-10 text-center text-base-content/60">Đang tải danh sách người dùng...</td></tr>';
    const url = new URL(config.searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", String(pageSize));
    try {
      const result = await request(url);
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} người dùng`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      renderRows([]);
      countLabel.textContent = "";
      showError(errorBanner, error.message || "Không thể tải danh sách người dùng.");
    }
  };
  const buildRoleChoices = (selectedNames = []) => {
    roleContainer.replaceChildren();
    const selected = new Set(selectedNames.map((name) => name.toLocaleLowerCase()));
    for (const role of assignableRoles) {
      const label = document.createElement("label");
      label.className = "label cursor-pointer justify-start gap-3";
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.className = "checkbox checkbox-primary checkbox-sm";
      checkbox.value = role.id;
      checkbox.checked = selected.has(role.name.toLocaleLowerCase());
      checkbox.disabled = !role.isAssignable;
      checkbox.dataset.userRoleId = role.id;
      const name = document.createElement("span");
      name.className = "label-text";
      name.textContent = role.name;
      const roleLabel = document.createElement("span");
      roleLabel.className = "flex items-center gap-2";
      roleLabel.append(name);
      if (!role.isAssignable) {
        const restriction = document.createElement("span");
        restriction.className = "badge badge-ghost badge-sm";
        restriction.textContent = "Cần quyền quản lý phân quyền";
        roleLabel.append(restriction);
      }
      label.append(checkbox, roleLabel);
      roleContainer.append(label);
    }
    if (assignableRoles.length === 0) {
      const message = document.createElement("p");
      message.className = "text-sm text-base-content/60";
      message.textContent = "Không có role nào có thể gán.";
      roleContainer.append(message);
    }
  };
  const selectedRoleIds = () =>
    [...roleContainer.querySelectorAll('input[type="checkbox"]:checked')].map((input) => input.value);
  const openEditor = (mode, user = null) => {
    modalMode = mode;
    activeUser = user;
    hideError(modalError);
    submitButton.disabled = false;
    passwordContainer.classList.toggle("hidden", mode !== "create");
    passwordInput.value = "";
    passwordInput.required = mode === "create";
    lockContainer.classList.toggle("hidden", mode !== "edit");
    lockedInput.checked = Boolean(user?.isLocked);
    lockedInput.disabled = Boolean(user?.isCurrentUser);

    if (mode === "create") {
      modalTitle.textContent = "Thêm người dùng";
      modalDescription.textContent = "Tạo tài khoản Identity mới với mật khẩu ban đầu.";
      submitButton.textContent = "Tạo tài khoản";
      emailInput.value = "";
      buildRoleChoices();
    } else if (user) {
      modalTitle.textContent = "Chỉnh sửa người dùng";
      modalDescription.textContent = `Cập nhật email, role và trạng thái của ${user.email}.`;
      submitButton.textContent = "Lưu thay đổi";
      emailInput.value = user.email;
      buildRoleChoices(user.roleNames);
    }
    modal.showModal();
    emailInput.focus();
  };
  const userUrl = (template, userId) =>
    template.replace("__userId__", encodeURIComponent(userId));

  const saveUser = async () => {
    if (isSaving) return;
    hideError(modalError);
    if (!emailInput.checkValidity()) {
      emailInput.reportValidity();
      return;
    }
    if (modalMode === "create" && !passwordInput.checkValidity()) {
      passwordInput.reportValidity();
      return;
    }
    isSaving = true;
    submitButton.disabled = true;
    try {
      const payload = {
        email: emailInput.value,
        roleIds: selectedRoleIds(),
      };
      if (modalMode === "create") {
        payload.password = passwordInput.value;
        await request(config.createUrl, { method: "POST", body: JSON.stringify(payload) });
      } else if (activeUser) {
        payload.isLocked = lockedInput.checked;
        await request(userUrl(config.editUrlTemplate, activeUser.id), {
          method: "POST",
          body: JSON.stringify(payload),
        });
      }
      modal.close();
      if (modalMode === "create") page = 1;
      await loadUsers();
    } catch (error) {
      showError(modalError, error.message || "Không thể lưu thay đổi.");
    } finally {
      isSaving = false;
      submitButton.disabled = false;
    }
  };
  const deleteUser = async () => {
    if (!activeUser || isSaving) return;
    hideError(deleteError);
    isSaving = true;
    deleteConfirm.disabled = true;
    try {
      await request(userUrl(config.deleteUrlTemplate, activeUser.id), { method: "POST" });
      deleteModal.close();
      if (page > 1 && rows.children.length === 1) page -= 1;
      await loadUsers();
    } catch (error) {
      showError(deleteError, error.message || "Không thể xóa tài khoản.");
    } finally {
      isSaving = false;
      deleteConfirm.disabled = false;
    }
  };

  createButton?.addEventListener("click", () => {
    if (config.canCreate) openEditor("create");
  });
  submitButton.addEventListener("click", saveUser);
  cancelButton.addEventListener("click", () => modal.close());
  emailInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      saveUser();
    }
  });
  deleteCancel.addEventListener("click", () => deleteModal.close());
  deleteConfirm.addEventListener("click", deleteUser);
  rows.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-user-action]");
    if (!(button instanceof HTMLButtonElement)) return;
    let userRoles;
    try {
      userRoles = JSON.parse(button.dataset.userRoles ?? "[]");
    } catch {
      throw new Error("User role data is invalid.");
    }
    const user = {
      id: button.dataset.userId,
      email: button.dataset.userEmail,
      isLocked: button.dataset.userLocked === "true",
      isCurrentUser: button.dataset.userCurrent === "true",
      roleNames: userRoles,
    };
    if (!user.id || !user.email) return;
    if (button.dataset.userAction === "edit" && config.canUpdate) {
      openEditor("edit", user);
    } else if (button.dataset.userAction === "delete" && config.canDelete && !user.isCurrentUser) {
      activeUser = user;
      hideError(deleteError);
      deleteDescription.textContent = `Bạn có chắc muốn xóa tài khoản ${user.email} không? Thao tác này không thể hoàn tác.`;
      deleteModal.showModal();
    }
  });
  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadUsers();
    }, 250);
  });
  pageSizeSelect.addEventListener("change", () => {
    pageSize = Number(pageSizeSelect.value);
    page = 1;
    loadUsers();
  });
  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadUsers();
    }
  });
  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadUsers();
    }
  });

  const initialize = async () => {
    try {
      assignableRoles = await request(config.rolesUrl);
      await loadUsers();
    } catch (error) {
      showError(errorBanner, error.message || "Không thể tải cấu hình người dùng.");
    }
  };
  void initialize();
})();
