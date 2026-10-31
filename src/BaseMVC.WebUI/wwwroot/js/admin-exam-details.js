(() => {
  const root = document.querySelector("[data-exam-details]");
  if (!(root instanceof HTMLElement)) return;

  const adminApi = window.BaseAdmin;
  const $ = (selector) => root.querySelector(selector);
  const config = {
    summaryUrl: root.dataset.summaryUrl,
    candidatesUrl: root.dataset.candidatesUrl,
    resultsUrl: root.dataset.resultsUrl,
    importUrl: root.dataset.importUrl,
    templateUrl: root.dataset.templateUrl,
    statusUrl: root.dataset.statusUrl,
    canImport: root.dataset.canImport === "true",
    canChangeStatus: root.dataset.canChangeStatus === "true",
  };
  const errorBanner = $("[data-details-error]");

  if (!adminApi || !(errorBanner instanceof HTMLElement) || !config.summaryUrl || !config.candidatesUrl || !config.resultsUrl) {
    throw new Error("Exam details configuration is incomplete.");
  }

  const { showError, hideError } = adminApi;
  const request = (url, options = {}) => adminApi.request(url, options, "#exam-details-antiforgery");

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

  const loadSummary = async () => {
    try {
      const summary = await request(config.summaryUrl);
      $("[data-details-name]").textContent = summary.name;
      $("[data-details-type]").textContent = `Loại kỳ thi: ${summary.examTypeName}`;
      $("[data-details-status]").textContent = statusLabels[summary.status] ?? summary.status;
      const statusSelect = $("[data-status-select]");
      if (statusSelect instanceof HTMLSelectElement) statusSelect.value = summary.status;
    } catch (error) {
      showError(errorBanner, error.message || "Không thể tải thông tin kỳ thi.");
    }
  };

  const createList = ({ prefix, url, columns }) => {
    const rows = $(`[data-${prefix}-rows]`);
    const search = $(`[data-${prefix}-search]`);
    const total = $(`[data-${prefix}-total]`);
    const pageLabel = $(`[data-${prefix}-page]`);
    const previous = $(`[data-${prefix}-previous]`);
    const next = $(`[data-${prefix}-next]`);
    let page = 1;
    let totalPages = 0;
    let timer;

    const render = (items) => {
      rows.replaceChildren();
      if (items.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = columns.length;
        cell.className = "py-8 text-center text-base-content/60";
        cell.textContent = "Chưa có dữ liệu.";
        row.append(cell);
        rows.append(row);
        return;
      }
      for (const item of items) {
        const row = document.createElement("tr");
        for (const column of columns) {
          const cell = document.createElement("td");
          cell.textContent = item[column] ?? "";
          row.append(cell);
        }
        rows.append(row);
      }
    };

    const load = async () => {
      hideError(errorBanner);
      const query = new URL(url, window.location.origin);
      query.searchParams.set("page", String(page));
      query.searchParams.set("pageSize", "10");
      if (search.value.trim()) query.searchParams.set("search", search.value.trim());
      try {
        const data = await request(`${query.pathname}${query.search}`);
        page = data.page;
        totalPages = data.totalPages;
        total.textContent = `(${data.totalCount})`;
        pageLabel.textContent = `Trang ${data.page} / ${Math.max(data.totalPages, 1)}`;
        previous.disabled = data.page <= 1;
        next.disabled = data.page >= data.totalPages;
        render(data.items);
      } catch (error) {
        showError(errorBanner, error.message || "Không thể tải dữ liệu.");
      }
    };

    search.addEventListener("input", () => {
      clearTimeout(timer);
      timer = setTimeout(() => {
        page = 1;
        load();
      }, 300);
    });
    previous.addEventListener("click", () => {
      if (page > 1) {
        page -= 1;
        load();
      }
    });
    next.addEventListener("click", () => {
      if (page < totalPages) {
        page += 1;
        load();
      }
    });

    return { load, reset: () => { page = 1; return load(); } };
  };

  const candidates = createList({
    prefix: "candidates",
    url: config.candidatesUrl,
    columns: ["studentCode", "lastName", "firstName", "examDate", "session", "examDuration", "examRoom", "retentionNote", "retainedSection"],
  });
  const results = createList({
    prefix: "results",
    url: config.resultsUrl,
    columns: ["studentCode", "lastName", "firstName", "multipleChoiceScore", "practicalScore", "finalScore", "note"],
  });

  if (config.canImport && config.importUrl && config.templateUrl) {
    const format = $("[data-import-format]");
    const kind = $("[data-import-kind]");
    const mode = $("[data-import-mode]");
    const file = $("[data-import-file]");
    const submit = $("[data-import-submit]");
    const template = $("[data-import-template]");
    const hint = $("[data-import-hint]");
    const importError = $("[data-import-error]");
    const summary = $("[data-import-summary]");
    const errorList = $("[data-import-errors]");
    let isImporting = false;

    const updateHint = () => {
      hint.textContent = kind.value === "Candidates"
        ? "Danh sách thi nhập được khi kỳ thi ở trạng thái “Chưa bắt đầu”, “Mở đăng ký”, “Đã hoàn thành đăng ký” hoặc “Chờ xếp lịch thi”; sau khi nhập kỳ thi chuyển sang “Đã xếp lịch thi”."
        : "Kết quả thi nhập được khi kỳ thi ở trạng thái từ “Chưa bắt đầu” đến “Đã thi (Chờ kết quả)”; sau khi nhập kỳ thi chuyển sang “Đã nhập kết quả”. Mã sinh viên phải có trong danh sách sinh viên; họ tên lấy từ danh sách sinh viên.";
      template.href = `${config.templateUrl}?format=${encodeURIComponent(format.value)}&kind=${encodeURIComponent(kind.value)}`;
    };
    format.addEventListener("change", updateHint);
    kind.addEventListener("change", updateHint);
    updateHint();

    submit.addEventListener("click", async () => {
      if (isImporting) return;
      hideError(importError);
      summary.classList.add("hidden");
      errorList.replaceChildren();

      const selected = file.files?.[0];
      if (!selected) {
        showError(importError, "Vui lòng chọn file Excel (.xlsx).");
        return;
      }

      isImporting = true;
      submit.disabled = true;
      try {
        const body = new FormData();
        body.append("file", selected);
        body.append("format", format.value);
        body.append("kind", kind.value);
        body.append("mode", mode.value);
        const result = await request(config.importUrl, { method: "POST", body });
        summary.textContent = `Đã import ${result.importedCount}/${result.totalRows} dòng; ${result.errors.length} dòng lỗi.`;
        summary.classList.remove("hidden");
        for (const rowError of result.errors) {
          const item = document.createElement("li");
          item.textContent = `Dòng ${rowError.row}: ${rowError.message}`;
          errorList.append(item);
        }
        file.value = "";
        if (result.importedCount > 0) {
          await Promise.all([loadSummary(), candidates.reset(), results.reset()]);
        }
      } catch (error) {
        showError(importError, error.message || "Không thể import file.");
      } finally {
        isImporting = false;
        submit.disabled = false;
      }
    });
  }

  const statusSelect = $("[data-status-select]");
  const statusSubmit = $("[data-status-submit]");
  const statusError = $("[data-status-error]");
  if (config.canChangeStatus && config.statusUrl && statusSelect instanceof HTMLSelectElement && statusSubmit instanceof HTMLButtonElement && statusError instanceof HTMLElement) {
    statusSubmit.addEventListener("click", async () => {
      hideError(statusError);
      statusSubmit.disabled = true;
      try {
        await request(config.statusUrl, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ status: statusSelect.value }),
        });
        await loadSummary();
      } catch (error) {
        showError(statusError, error.message || "Không thể chuyển trạng thái kỳ thi.");
      } finally {
        statusSubmit.disabled = false;
      }
    });
  }

  loadSummary();
  candidates.load();
  results.load();
})();
