const state = {
  token: localStorage.getItem("token") || "",
  user: JSON.parse(localStorage.getItem("user") || "null")
};

const authStatus = document.getElementById("authStatus");
const adminFaqCard = document.getElementById("adminFaqCard");
const faqList = document.getElementById("faqList");
const requestsList = document.getElementById("requestsList");

function roleLabel(role) {
  if (role === "admin" || role === "staff") return "Admin";
  if (role === "student") return "Öğrenci";
  return role;
}

function isAdmin(user) {
  return user && (user.role === "admin" || user.role === "staff");
}

function statusLabel(status) {
  const labels = {
    pending: "Beklemede",
    approved: "Onaylandı",
    rejected: "Reddedildi"
  };
  return labels[status] || status;
}

function setSession(token, user) {
  state.token = token;
  state.user = user;
  localStorage.setItem("token", token || "");
  localStorage.setItem("user", JSON.stringify(user || null));
  renderUserState();
}

function clearSession() {
  state.token = "";
  state.user = null;
  localStorage.removeItem("token");
  localStorage.removeItem("user");
  renderUserState();
}

function renderUserState() {
  if (!state.user) {
    authStatus.textContent = "Misafir modundasınız";
    adminFaqCard.classList.add("hidden");
    return;
  }
  authStatus.textContent = `${state.user.fullName} olarak giriş yaptınız (${roleLabel(state.user.role)})`;
  isAdmin(state.user)
    ? adminFaqCard.classList.remove("hidden")
    : adminFaqCard.classList.add("hidden");
}

async function api(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  const response = await fetch(path, { ...options, headers });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || "İstek başarısız");
  return data;
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text) node.textContent = text;
  return node;
}

async function loadFaqs() {
  const q = document.getElementById("searchInput").value.trim();
  const topic = document.getElementById("topicInput").value.trim();
  const query = new URLSearchParams();
  if (q) query.set("q", q);
  if (topic) query.set("topic", topic);
  const data = await api(`/api/faqs?${query.toString()}`);
  faqList.innerHTML = "";
  if (!data.items.length) return faqList.append(el("p", "muted", "Kayıt bulunamadı."));
  data.items.forEach((item) => {
    const box = el("div", "item");
    box.append(el("h4", "", item.question));
    box.append(el("p", "", item.answer));
    box.append(el("span", "badge", item.topic));
    const historyBtn = el("button", "secondary", "Yanıt Geçmişi");
    historyBtn.style.marginLeft = "8px";
    historyBtn.onclick = async () => {
      const history = await api(`/api/faqs/${item.id}/history`);
      alert(history.history.map((h) => `${h.updatedAt} - ${h.answer}`).join("\n\n") || "Geçmiş yok");
    };
    box.append(historyBtn);
    if (isAdmin(state.user)) {
      const deleteBtn = el("button", "secondary", "Sil");
      deleteBtn.style.marginLeft = "8px";
      deleteBtn.onclick = async () => {
        if (!confirm("Bu kaydı silmek istiyor musunuz?")) return;
        await api(`/api/faqs/${item.id}`, { method: "DELETE" });
        await loadFaqs();
      };
      box.append(deleteBtn);
    }
    faqList.append(box);
  });
}

async function loadRequests() {
  requestsList.innerHTML = "";
  if (!state.user) return requestsList.append(el("p", "muted", "Talepleri görmek için giriş yapın."));
  const data = await api("/api/questions/history");
  if (!data.items.length) return requestsList.append(el("p", "muted", "Talep geçmişi boş."));
  data.items.forEach((req) => {
    const box = el("div", "item");
    box.append(el("h4", "", req.question));
    box.append(el("p", "", `Konu: ${req.topic}`));
    box.append(el("p", "", `Durum: ${statusLabel(req.status)}`));
    if (req.reviewNote) box.append(el("p", "", `Not: ${req.reviewNote}`));
    if (isAdmin(state.user) && req.status === "pending") {
      const answer = el("textarea");
      answer.placeholder = "Onaylarsanız cevap yazın";
      box.append(answer);
      const approveBtn = el("button", "", "Onayla ve Yayınla");
      approveBtn.onclick = async () => {
        await api(`/api/requests/${req.id}/moderate`, {
          method: "PATCH",
          body: JSON.stringify({ decision: "approved", answer: answer.value, reviewNote: "Yayınlandı" })
        });
        await loadRequests();
        await loadFaqs();
      };
      const rejectBtn = el("button", "secondary", "Reddet");
      rejectBtn.style.marginLeft = "8px";
      rejectBtn.onclick = async () => {
        await api(`/api/requests/${req.id}/moderate`, {
          method: "PATCH",
          body: JSON.stringify({ decision: "rejected", reviewNote: "Uygun bulunmadı" })
        });
        await loadRequests();
      };
      box.append(approveBtn);
      box.append(rejectBtn);
    }
    requestsList.append(box);
  });
}

document.getElementById("registerBtn").onclick = async () => {
  try {
    const fullName = document.getElementById("fullName").value;
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;
    const data = await api("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ fullName, email, password })
    });
    setSession(data.token, data.user);
    await loadRequests();
  } catch (error) {
    alert(error.message);
  }
};

document.getElementById("loginBtn").onclick = async () => {
  try {
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;
    const data = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password })
    });
    setSession(data.token, data.user);
    await loadRequests();
  } catch (error) {
    alert(error.message);
  }
};

document.getElementById("logoutBtn").onclick = () => {
  clearSession();
  loadRequests().catch(() => undefined);
};

document.getElementById("searchBtn").onclick = () => loadFaqs().catch((e) => alert(e.message));

document.getElementById("sendRequestBtn").onclick = async () => {
  if (!state.user) return alert("Talep göndermek için önce giriş yapın.");
  try {
    const topic = document.getElementById("reqTopic").value;
    const question = document.getElementById("reqQuestion").value;
    const details = document.getElementById("reqDetails").value;
    await api("/api/requests", {
      method: "POST",
      body: JSON.stringify({ topic, question, details })
    });
    alert("Talebiniz alındı.");
    await loadRequests();
  } catch (error) {
    alert(error.message);
  }
};

document.getElementById("addFaqBtn").onclick = async () => {
  try {
    const topic = document.getElementById("adminTopic").value;
    const question = document.getElementById("adminQuestion").value;
    const answer = document.getElementById("adminAnswer").value;
    await api("/api/faqs", {
      method: "POST",
      body: JSON.stringify({ topic, question, answer })
    });
    alert("SSS eklendi.");
    await loadFaqs();
  } catch (error) {
    alert(error.message);
  }
};

document.getElementById("loadRequestsBtn").onclick = () => loadRequests().catch((e) => alert(e.message));

renderUserState();
loadFaqs().catch(() => undefined);
loadRequests().catch(() => undefined);
