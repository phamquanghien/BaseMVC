(() => {
  const root = document.querySelector("[data-exam-details]");
  if (!(root instanceof HTMLElement)) return;

  const adminApi = window.BaseAdmin;
  const $ = (selector) => root.querySelector(selector);
  const config = {
    summaryUrl: root.dataset.summaryUrl,
    dataUrl: root.dataset.dataUrl,
    importUrl: root.dataset.importUrl,
    templateUrl: root.dataset.templateUrl,
    statusUrl: root.dataset.statusUrl,
    canImport: root.dataset.canImport === "true",
    canChangeStatus: root.dataset.canChangeStatus === "true",
  };
  const errorBanner = $("[data-details-error]");

  if (!adminApi || !(errorBanner instanceof HTMLElement) || !config.summaryUrl || !config.dataUrl) {
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

  const formatName = $("[data-format-name]");
  const formatHint = $("[data-format-hint]");
  const candidatesCard = $("[data-candidates-card]");
  if (!(formatName instanceof HTMLElement) || !(formatHint instanceof HTMLElement)) {
    throw new Error("Exam details configuration is incomplete.");
  }

  const studentNote = "Mã sinh viên phải có trong danh sách sinh viên; họ tên lấy từ danh sách sinh viên.";
  const formatInfo = {
    ItStandard: { label: "Chuẩn đầu ra Công nghệ thông tin", note: studentNote },
    LanguageStandard: { label: "Chuẩn đầu ra Ngoại ngữ", note: studentNote },
    LanguagePlacement: { label: "Kiểm tra trình độ Ngoại ngữ", note: studentNote },
    EnglishIntensive: { label: "Tiếng Anh tăng cường", note: `Chỉ hỗ trợ nhập kết quả thi (chưa có danh sách thi). ${studentNote}` },
    ItBasicCertificate: { label: "Chứng chỉ ứng dụng CNTT cơ bản", note: "Thí sinh thi độc lập, không liên kết danh sách sinh viên. Số CCCD và Số báo danh không được trùng nhau trong một kỳ thi." },
  };
  const noFormatMessage = "Loại kỳ thi này chưa được gán định dạng dữ liệu. Vui lòng gán ở mục Loại kỳ thi để nhập và xem danh sách thi/kết quả thi.";
  // Định dạng được suy ra từ loại kỳ thi của kỳ thi; cập nhật mỗi lần tải summary.
  const state = { format: undefined, candidates: false };
  const formatListeners = [];

  const loadSummary = async () => {
    try {
      const summary = await request(config.summaryUrl);
      $("[data-details-name]").textContent = summary.name;
      $("[data-details-type]").textContent = `Loại kỳ thi: ${summary.examTypeName}`;
      $("[data-details-status]").textContent = statusLabels[summary.status] ?? summary.status;
      const statusSelect = $("[data-status-select]");
      if (statusSelect instanceof HTMLSelectElement) statusSelect.value = summary.status;
      const changed = state.format !== summary.dataFormat || state.candidates !== summary.supportsCandidates;
      state.format = summary.dataFormat;
      state.candidates = summary.supportsCandidates;
      const info = formatInfo[state.format];
      formatName.textContent = info ? info.label : "Chưa gán định dạng";
      formatHint.textContent = info ? info.note : noFormatMessage;
      if (candidatesCard instanceof HTMLElement) candidatesCard.classList.toggle("hidden", !state.candidates);
      if (changed) for (const listener of formatListeners) listener();
      return summary;
    } catch (error) {
      showError(errorBanner, error.message || "Không thể tải thông tin kỳ thi.");
      return null;
    }
  };

  const createList = ({ prefix, kind }) => {
    const rows = $(`[data-${prefix}-rows]`);
    const head = $(`[data-${prefix}-head]`);
    const search = $(`[data-${prefix}-search]`);
    const total = $(`[data-${prefix}-total]`);
    const pageLabel = $(`[data-${prefix}-page]`);
    const previous = $(`[data-${prefix}-previous]`);
    const next = $(`[data-${prefix}-next]`);
    let page = 1;
    let totalPages = 0;
    let timer;
    let loadToken = 0;

    const render = (columns, items) => {
      head.replaceChildren(...columns.map((column) => {
        const cell = document.createElement("th");
        cell.textContent = column.label;
        return cell;
      }));
      rows.replaceChildren();
      if (items.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = Math.max(columns.length, 1);
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
          cell.textContent = item[column.key] ?? "";
          row.append(cell);
        }
        rows.append(row);
      }
    };

    const load = async () => {
      if (!state.format || (kind === "Candidates" && !state.candidates)) {
        rows.replaceChildren();
        head.replaceChildren();
        total.textContent = "";
        return;
      }
      hideError(errorBanner);
      const token = ++loadToken;
      const query = new URL(config.dataUrl, window.location.origin);
      query.searchParams.set("kind", kind);
      query.searchParams.set("page", String(page));
      query.searchParams.set("pageSize", "10");
      if (search.value.trim()) query.searchParams.set("search", search.value.trim());
      try {
        const data = await request(`${query.pathname}${query.search}`);
        if (token !== loadToken) return;
        page = data.page;
        totalPages = data.totalPages;
        total.textContent = `(${data.totalCount})`;
        pageLabel.textContent = `Trang ${data.page} / ${Math.max(data.totalPages, 1)}`;
        previous.disabled = data.page <= 1;
        next.disabled = data.page >= data.totalPages;
        render(data.columns, data.items);
      } catch (error) {
        if (token === loadToken) showError(errorBanner, error.message || "Không thể tải dữ liệu.");
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

  const candidates = createList({ prefix: "candidates", kind: "Candidates" });
  const results = createList({ prefix: "results", kind: "Results" });

  if (config.canImport && config.importUrl && config.templateUrl) {
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
        : "Kết quả thi nhập được khi kỳ thi ở trạng thái từ “Chưa bắt đầu” đến “Đã thi (Chờ kết quả)”; sau khi nhập kỳ thi chuyển sang “Đã nhập kết quả”.";
      template.href = `${config.templateUrl}?kind=${encodeURIComponent(kind.value)}`;
      template.classList.toggle("hidden", !state.format);
      submit.disabled = isImporting || !state.format;
    };
    kind.addEventListener("change", updateHint);
    formatListeners.push(() => {
      const option = kind.querySelector('option[value="Candidates"]');
      if (option instanceof HTMLOptionElement) option.disabled = !state.candidates;
      if (!state.candidates) kind.value = "Results";
      updateHint();
    });
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
        submit.disabled = !state.format;
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

  formatListeners.push(() => {
    candidates.reset();
    results.reset();
  });

  loadSummary();
})();
