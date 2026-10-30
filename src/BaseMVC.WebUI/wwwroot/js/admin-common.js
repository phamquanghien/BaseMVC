(() => {
  const getAntiforgeryToken = (selector) => {
    const input = document.querySelector(`${selector} input[name="__RequestVerificationToken"]`);
    if (!(input instanceof HTMLInputElement)) {
      throw new Error("Không tìm thấy mã xác thực biểu mẫu.");
    }
    return input.value;
  };

  const getErrorMessage = async (response) => {
    if ((response.headers.get("content-type") ?? "").includes("application/json")) {
      const payload = await response.json().catch(() => null);
      if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
        return payload.errors.join(" ");
      }
      if (payload?.errors && typeof payload.errors === "object") {
        const messages = Object.values(payload.errors).flatMap((value) =>
          Array.isArray(value) ? value : [],
        );
        if (messages.length > 0) return messages.join(" ");
      }
      if (typeof payload?.title === "string") return payload.title;
    }
    return `Yêu cầu không thành công (HTTP ${response.status}).`;
  };

  const request = async (url, options = {}, tokenSelector = null) => {
    const method = (options.method ?? "GET").toUpperCase();
    const response = await fetch(url, {
      ...options,
      headers: {
        Accept: "application/json",
        ...(options.body && !(options.body instanceof FormData)
          ? { "Content-Type": "application/json" }
          : {}),
        ...(method !== "GET" && tokenSelector
          ? { RequestVerificationToken: getAntiforgeryToken(tokenSelector) }
          : {}),
        ...options.headers,
      },
      credentials: "same-origin",
    });

    if (response.redirected) {
      throw new Error("Phiên đăng nhập không còn hợp lệ hoặc bạn không đủ quyền thao tác.");
    }
    if (!response.ok) throw new Error(await getErrorMessage(response));
    return response.status === 204 ? null : response.json();
  };

  const showError = (element, message) => {
    element.textContent = message;
    element.classList.remove("hidden");
  };

  const hideError = (element) => {
    element.textContent = "";
    element.classList.add("hidden");
  };

  window.BaseAdmin = Object.freeze({ request, showError, hideError });
})();
