(() => {
  const manager = document.querySelector("[data-permission-manager]");
  if (!(manager instanceof HTMLElement)) return;

  const rolesTable = manager.querySelector("[data-permission-roles]");
  const errorBanner = manager.querySelector("[data-permission-error]");
  const modal = manager.querySelector("[data-permission-modal]");
  const roleName = manager.querySelector("[data-permission-role-name]");
  const modalError = manager.querySelector("[data-permission-modal-error]");
  const groups = manager.querySelector("[data-permission-groups]");
  const cancelButton = manager.querySelector("[data-permission-cancel]");
  const saveButton = manager.querySelector("[data-permission-save]");
  const adminApi = window.BaseAdmin;
  const config = {
    rolesUrl: manager.dataset.rolesUrl,
    permissionUrlTemplate: manager.dataset.permissionUrlTemplate,
    updateUrlTemplate: manager.dataset.updateUrlTemplate,
    canManage: manager.dataset.canManage === "true",
  };

  if (
    !(rolesTable instanceof HTMLElement) ||
    !(errorBanner instanceof HTMLElement) ||
    !(modal instanceof HTMLDialogElement) ||
    !(roleName instanceof HTMLElement) ||
    !(modalError instanceof HTMLElement) ||
    !(groups instanceof HTMLElement) ||
    !(cancelButton instanceof HTMLButtonElement) ||
    (config.canManage && !(saveButton instanceof HTMLButtonElement)) ||
    !adminApi ||
    [config.rolesUrl, config.permissionUrlTemplate, config.updateUrlTemplate].some(
      (value) => !value,
    )
  ) {
    throw new Error("Permission manager markup or endpoint configuration is incomplete.");
  }

  let selectedRoleId = null;
  let isSaving = false;

  const showError = adminApi?.showError;
  const hideError = adminApi?.hideError;
  const request = (url, options = {}) =>
    adminApi.request(url, options, "#permission-antiforgery");

  const permissionUrl = (template, roleId) =>
    template.replace("__roleId__", encodeURIComponent(roleId));

  const renderRoles = (roles) => {
    rolesTable.replaceChildren();
    if (roles.length === 0) {
      const row = document.createElement("tr");
      const cell = document.createElement("td");
      cell.colSpan = 2;
      cell.className = "py-8 text-center text-base-content/60";
      cell.textContent = "Chưa có role nào.";
      row.append(cell);
      rolesTable.append(row);
      return;
    }

    for (const role of roles) {
      const row = document.createElement("tr");
      const nameCell = document.createElement("td");
      nameCell.className = "font-medium";
      nameCell.textContent = role.name;

      const actionCell = document.createElement("td");
      actionCell.className = "text-right";
      const button = document.createElement("button");
      button.type = "button";
      button.className = config.canManage ? "btn btn-primary btn-sm" : "btn btn-outline btn-sm";
      button.textContent = config.canManage ? "Phân quyền" : "Xem quyền";
      button.dataset.permissionRoleId = role.id;
      button.dataset.permissionRoleName = role.name;
      actionCell.append(button);
      row.append(nameCell, actionCell);
      rolesTable.append(row);
    }
  };

  const loadRoles = async () => {
    hideError(errorBanner);
    try {
      renderRoles(await request(config.rolesUrl));
    } catch (error) {
      rolesTable.replaceChildren();
      showError(errorBanner, error.message || "Không thể tải danh sách role.");
    }
  };

  const renderPermissions = (permissions) => {
    groups.replaceChildren();
    const groupedPermissions = new Map();
    for (const permission of permissions) {
      const modulePermissions = groupedPermissions.get(permission.module) ?? [];
      modulePermissions.push(permission);
      groupedPermissions.set(permission.module, modulePermissions);
    }

    for (const [module, modulePermissions] of groupedPermissions) {
      const card = document.createElement("section");
      card.className = "rounded-box border border-base-300";
      const heading = document.createElement("h3");
      heading.className = "border-b border-base-300 px-4 py-3 font-semibold";
      heading.textContent = module;
      const list = document.createElement("div");
      list.className = "grid gap-2 p-4 sm:grid-cols-2";

      for (const permission of modulePermissions) {
        const label = document.createElement("label");
        label.className = "flex items-start gap-3 rounded-box px-2 py-2 hover:bg-base-200";
        const input = document.createElement("input");
        input.type = "checkbox";
        input.className = "checkbox checkbox-primary checkbox-sm mt-0.5";
        input.value = permission.name;
        input.checked = permission.isGranted;
        input.disabled =
          !config.canManage ||
          (roleName.dataset.roleName === "SuperAdmin" &&
            ["Permissions.Access.Read", "Permissions.Access.Manage"].includes(permission.name));

        const text = document.createElement("span");
        text.className = "min-w-0";
        const action = document.createElement("span");
        action.className = "block font-medium";
        action.textContent = permission.action;
        const code = document.createElement("code");
        code.className = "block break-all text-xs text-base-content/60";
        code.textContent = permission.name;
        text.append(action, code);
        label.append(input, text);
        list.append(label);
      }

      card.append(heading, list);
      groups.append(card);
    }
  };

  const openPermissions = async (roleId, name) => {
    selectedRoleId = roleId;
    roleName.textContent = `Quyền của role: ${name}`;
    roleName.dataset.roleName = name;
    groups.innerHTML =
      '<p class="py-8 text-center text-base-content/60">Đang tải catalog quyền...</p>';
    hideError(modalError);
    if (saveButton instanceof HTMLButtonElement) saveButton.disabled = true;
    modal.showModal();

    try {
      const result = await request(permissionUrl(config.permissionUrlTemplate, roleId));
      renderPermissions(result.permissions);
      if (saveButton instanceof HTMLButtonElement) saveButton.disabled = false;
    } catch (error) {
      groups.replaceChildren();
      showError(modalError, error.message || "Không thể tải quyền của role.");
    }
  };

  rolesTable.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest("[data-permission-role-id]");
    if (!(button instanceof HTMLButtonElement)) return;
    const roleId = button.dataset.permissionRoleId;
    const name = button.dataset.permissionRoleName;
    if (roleId && name) void openPermissions(roleId, name);
  });

  cancelButton.addEventListener("click", () => modal.close());

  if (saveButton instanceof HTMLButtonElement) {
    saveButton.addEventListener("click", async () => {
      if (isSaving || !selectedRoleId) return;
      hideError(modalError);
      isSaving = true;
      saveButton.disabled = true;

      try {
        const permissions = Array.from(
          groups.querySelectorAll('input[type="checkbox"]:checked'),
          (checkbox) => checkbox.value,
        );
        await request(permissionUrl(config.updateUrlTemplate, selectedRoleId), {
          method: "POST",
          body: JSON.stringify({ permissions }),
        });
        modal.close();
        await loadRoles();
      } catch (error) {
        showError(modalError, error.message || "Không thể lưu quyền của role.");
      } finally {
        isSaving = false;
        saveButton.disabled = false;
      }
    });
  }

  void loadRoles();
})();
