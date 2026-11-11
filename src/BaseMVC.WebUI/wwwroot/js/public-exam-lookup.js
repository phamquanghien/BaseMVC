(() => {
  const root = document.querySelector("[data-public-exam-lookup]");
  if (!(root instanceof HTMLElement)) return;

  const kind = root.querySelector("[data-lookup-kind]");
  const typeField = root.querySelector("[data-exam-type-field]");
  const examType = root.querySelector("[data-exam-type]");
  const form = root.querySelector("[data-lookup-form]");
  const error = root.querySelector("[data-lookup-error]");
  const message = root.querySelector("[data-lookup-message]");
  const results = root.querySelector("[data-lookup-results]");
  const head = root.querySelector("[data-lookup-results-head]");
  const rows = root.querySelector("[data-lookup-results-rows]");
  const submit = root.querySelector("[data-lookup-submit]");
  const siteKey = root.dataset.siteKey;
  const tokenInput = root.querySelector("[data-recaptcha-token]");
  const hiddenKind = root.querySelector("[data-hidden-kind]");
  const hiddenType = root.querySelector("[data-hidden-exam-type]");
  const config = { typesUrl: root.dataset.typesUrl, lookupUrl: root.dataset.lookupUrl };

  if (!(kind instanceof HTMLSelectElement) || !(typeField instanceof HTMLElement) ||
      !(examType instanceof HTMLSelectElement) || !(form instanceof HTMLFormElement) ||
      !(error instanceof HTMLElement) || !(message instanceof HTMLElement) ||
      !(results instanceof HTMLElement) || !(head instanceof HTMLElement) ||
      !(rows instanceof HTMLElement) || !(submit instanceof HTMLButtonElement) ||
      !(tokenInput instanceof HTMLInputElement) || !(hiddenKind instanceof HTMLInputElement) ||
      !(hiddenType instanceof HTMLInputElement) || !config.typesUrl || !config.lookupUrl) return;

  let typeRequest = 0;
  const showError = (text) => {
    error.textContent = text;
    error.classList.remove("hidden");
  };
  const clearOutput = () => {
    error.classList.add("hidden");
    message.classList.add("hidden");
    results.classList.add("hidden");
    head.replaceChildren();
    rows.replaceChildren();
  };

  const loadTypes = async () => {
    clearOutput();
    typeField.classList.toggle("hidden", !kind.value);
    form.classList.add("hidden");
    examType.disabled = true;
    examType.replaceChildren(new Option("Đang tải loại kỳ thi...", ""));
    const request = ++typeRequest;
    if (!kind.value) return;

    try {
      const url = new URL(config.typesUrl, window.location.origin);
      url.searchParams.set("kind", kind.value);
      const response = await fetch(url, { headers: { Accept: "application/json" } });
      const data = await response.json().catch(() => null);
      if (request !== typeRequest) return;
      if (!response.ok) throw new Error(data?.error || "Không thể tải loại kỳ thi.");

      examType.replaceChildren(new Option("Chọn loại kỳ thi", ""));
      for (const item of data.items) examType.add(new Option(item.name, String(item.id)));
      examType.disabled = data.items.length === 0;
      if (data.items.length === 0) {
        message.textContent = "Hiện chưa có loại kỳ thi phù hợp để tra cứu.";
        message.classList.remove("hidden");
      }
    } catch (requestError) {
      if (request === typeRequest) showError(requestError.message || "Không thể tải loại kỳ thi.");
    }
  };

  kind.addEventListener("change", loadTypes);
  examType.addEventListener("change", () => {
    clearOutput();
    const selected = examType.value.length > 0;
    form.classList.toggle("hidden", !selected);
    hiddenKind.value = kind.value;
    hiddenType.value = examType.value;
  });

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearOutput();
    if (!form.reportValidity()) return;
    if (!siteKey || !window.grecaptcha) {
      showError("Không thể tải reCAPTCHA. Vui lòng thử lại sau.");
      return;
    }

    submit.disabled = true;
    submit.textContent = "Đang xác minh...";
    try {
      const token = await new Promise((resolve, reject) => {
        window.grecaptcha.ready(() => {
          window.grecaptcha.execute(siteKey, { action: "exam_lookup" }).then(resolve, reject);
        });
      });
      tokenInput.value = token;
      const response = await fetch(config.lookupUrl, {
        method: "POST",
        body: new FormData(form),
        headers: { Accept: "application/json" },
      });
      const data = await response.json().catch(() => null);
      if (!response.ok) throw new Error(data?.error || "Bạn thao tác quá nhanh. Vui lòng thử lại sau.");
      if (!data) throw new Error("Không thể đọc phản hồi tra cứu. Vui lòng thử lại.");
      if (data.message) {
        message.textContent = data.message;
        message.classList.remove("hidden");
        return;
      }

      for (const column of data.columns) {
        const cell = document.createElement("th");
        cell.textContent = column.label;
        head.append(cell);
      }
      for (const item of data.items) {
        const row = document.createElement("tr");
        for (const column of data.columns) {
          const cell = document.createElement("td");
          cell.textContent = column.key === "examName" ? item.examName : (item.values[column.key] ?? "");
          row.append(cell);
        }
        rows.append(row);
      }
      results.classList.remove("hidden");
    } catch (requestError) {
      showError(requestError.message || "Không thể thực hiện tra cứu.");
    } finally {
      tokenInput.value = "";
      submit.disabled = false;
      submit.textContent = "Tra cứu";
    }
  });
})();
