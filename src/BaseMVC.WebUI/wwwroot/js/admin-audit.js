(() => {
  const manager = document.querySelector("[data-audit-log]");
  if (!(manager instanceof HTMLElement)) return;

  const rows = manager.querySelector("[data-audit-rows]");
  const searchInput = manager.querySelector("[data-audit-search]");
  const eventSelect = manager.querySelector("[data-audit-event]");
  const outcomeSelect = manager.querySelector("[data-audit-outcome]");
  const pageSizeSelect = manager.querySelector("[data-audit-page-size]");
  const countLabel = manager.querySelector("[data-audit-count]");
  const pageLabel = manager.querySelector("[data-audit-page-label]");
  const previousButton = manager.querySelector("[data-audit-previous]");
  const nextButton = manager.querySelector("[data-audit-next]");
  const errorBanner = manager.querySelector("[data-audit-error]");
  const searchUrl = manager.dataset.searchUrl;

  if (
    !(rows instanceof HTMLElement) ||
    !(searchInput instanceof HTMLInputElement) ||
    !(eventSelect instanceof HTMLSelectElement) ||
    !(outcomeSelect instanceof HTMLSelectElement) ||
    !(pageSizeSelect instanceof HTMLSelectElement) ||
    !(countLabel instanceof HTMLElement) ||
    !(pageLabel instanceof HTMLElement) ||
    !(previousButton instanceof HTMLButtonElement) ||
    !(nextButton instanceof HTMLButtonElement) ||
    !(errorBanner instanceof HTMLElement) ||
    !searchUrl
  ) {
    throw new Error("Audit log markup or endpoint configuration is incomplete.");
  }

  const eventLabels = {
    Login: "Đăng nhập",
    PasswordResetRequested: "Yêu cầu đặt lại mật khẩu",
    PasswordResetEmailDelivery: "Gửi email đặt lại mật khẩu",
    PasswordResetCompleted: "Hoàn tất đặt lại mật khẩu",
    PasswordChange: "Đổi mật khẩu",
    Logout: "Đăng xuất",
    UserCreated: "Tạo người dùng",
    UserUpdated: "Cập nhật người dùng",
    UserDeleted: "Xóa người dùng",
    RoleCreated: "Tạo role",
    RoleUpdated: "Cập nhật role",
    RoleDeleted: "Xóa role",
    RolePermissionsUpdated: "Cập nhật quyền role",
  };
  const outcomeLabels = {
    Succeeded: "Thành công",
    Failed: "Thất bại",
    Accepted: "Đã tiếp nhận",
    LockedOut: "Đã khóa",
  };

  let page = 1;
  let totalPages = 0;
  let searchTimer;
  let activeRequest;

  const showError = (message) => {
    errorBanner.textContent = message;
    errorBanner.classList.remove("hidden");
  };

  const hideError = () => {
    errorBanner.textContent = "";
    errorBanner.classList.add("hidden");
  };

  const renderRows = (items) => {
    rows.replaceChildren();
    if (items.length === 0) {
      const row = document.createElement("tr");
      const cell = document.createElement("td");
      cell.colSpan = 6;
      cell.className = "py-10 text-center text-base-content/60";
      cell.textContent = "Không tìm thấy bản ghi phù hợp.";
      row.append(cell);
      rows.append(row);
      return;
    }

    const formatter = new Intl.DateTimeFormat("vi-VN", {
      dateStyle: "short",
      timeStyle: "medium",
      timeZone: "UTC",
    });
    for (const item of items) {
      const row = document.createElement("tr");
      const timestamp = new Date(item.occurredAtUtc);
      const cells = [
        Number.isNaN(timestamp.getTime()) ? item.occurredAtUtc : formatter.format(timestamp),
        eventLabels[item.eventType] ?? item.eventType,
        outcomeLabels[item.outcome] ?? item.outcome,
        item.actorUserId ?? "—",
        item.targetUserId ?? "—",
        item.targetRoleId ?? "—",
      ];
      for (const value of cells) {
        const cell = document.createElement("td");
        cell.className = "max-w-72 break-all";
        cell.textContent = value;
        row.append(cell);
      }
      rows.append(row);
    }
  };

  const loadLogs = async () => {
    hideError();
    activeRequest?.abort();
    activeRequest = new AbortController();
    const url = new URL(searchUrl, window.location.origin);
    url.searchParams.set("search", searchInput.value.trim());
    url.searchParams.set("eventType", eventSelect.value);
    url.searchParams.set("outcome", outcomeSelect.value);
    url.searchParams.set("page", String(page));
    url.searchParams.set("pageSize", pageSizeSelect.value);

    try {
      const response = await fetch(url, {
        headers: { Accept: "application/json" },
        credentials: "same-origin",
        signal: activeRequest.signal,
      });
      if (response.redirected) {
        throw new Error("Phiên đăng nhập không còn hợp lệ hoặc bạn không đủ quyền xem nhật ký.");
      }
      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        const message = Array.isArray(payload?.errors)
          ? payload.errors.join(" ")
          : `Yêu cầu không thành công (HTTP ${response.status}).`;
        throw new Error(message);
      }

      const result = await response.json();
      page = result.page;
      totalPages = result.totalPages;
      renderRows(result.items);
      countLabel.textContent = `Tổng cộng ${result.totalCount} bản ghi`;
      pageLabel.textContent = `Trang ${page} / ${Math.max(totalPages, 1)}`;
      previousButton.disabled = page <= 1;
      nextButton.disabled = totalPages === 0 || page >= totalPages;
    } catch (error) {
      if (error.name === "AbortError") return;
      renderRows([]);
      countLabel.textContent = "";
      showError(error.message || "Không thể tải nhật ký hệ thống.");
    }
  };

  searchInput.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => {
      page = 1;
      loadLogs();
    }, 250);
  });

  for (const filter of [eventSelect, outcomeSelect, pageSizeSelect]) {
    filter.addEventListener("change", () => {
      page = 1;
      loadLogs();
    });
  }

  previousButton.addEventListener("click", () => {
    if (page > 1) {
      page -= 1;
      loadLogs();
    }
  });

  nextButton.addEventListener("click", () => {
    if (page < totalPages) {
      page += 1;
      loadLogs();
    }
  });

  loadLogs();
})();
