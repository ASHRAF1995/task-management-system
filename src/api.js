// عميل بسيط للتعامل مع Injaz API
// افتراضياً الـ API على نفس الموقع (مسار نسبي)، فالنسخة تشتغل حتى لو اترفعت في فولدر فرعي
const API_BASE = (import.meta.env?.VITE_API_URL || '.').replace(/\/$/, '');
const TOKEN_KEY = 'injaz-token';
const USER_KEY = 'injaz-user';

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

let onUnauthorized = () => {};
export const setUnauthorizedHandler = fn => { onUnauthorized = fn; };

export const session = {
  get token() { return sessionStorage.getItem(TOKEN_KEY); },
  get user() {
    try { return JSON.parse(sessionStorage.getItem(USER_KEY) || 'null'); } catch { return null; }
  },
  save(token, user) {
    sessionStorage.setItem(TOKEN_KEY, token);
    sessionStorage.setItem(USER_KEY, JSON.stringify(user));
  },
  clear() {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
  }
};

async function request(path, { method = 'GET', body, form, raw = false } = {}) {
  const headers = {};
  if (session.token) headers.Authorization = `Bearer ${session.token}`;
  let payload;
  if (form) payload = form;
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let res;
  try {
    res = await fetch(`${API_BASE}${path}`, { method, headers, body: payload });
  } catch {
    throw new ApiError('تعذّر الاتصال بالخادم. تأكد من تشغيل الـ API.', 0);
  }

  if (res.status === 401 && !path.startsWith('/api/auth/login')) {
    onUnauthorized();
    throw new ApiError('انتهت الجلسة، سجّل الدخول مرة أخرى.', 401);
  }

  if (!res.ok) {
    let message = `حدث خطأ غير متوقع (${res.status}).`;
    try {
      const problem = await res.json();
      message = problem.title || problem.detail || message;
    } catch { /* الرد ليس JSON */ }
    if (res.status === 403) message = 'ليست لديك صلاحية لتنفيذ هذا الإجراء.';
    throw new ApiError(message, res.status);
  }

  if (raw) return res;
  if (res.status === 204) return null;
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export const api = {
  login: (email, password) => request('/api/auth/login', { method: 'POST', body: { email, password } }),
  me: () => request('/api/auth/me'),

  users: () => request('/api/users'),
  createUser: data => request('/api/users', { method: 'POST', body: data }),
  updateUser: (id, data) => request(`/api/users/${id}`, { method: 'PUT', body: data }),
  deleteUser: id => request(`/api/users/${id}`, { method: 'DELETE' }),

  tasks: () => request('/api/tasks'),
  task: id => request(`/api/tasks/${id}`),
  createTask: data => request('/api/tasks', { method: 'POST', body: data }),
  updateTask: (id, data) => request(`/api/tasks/${id}`, { method: 'PUT', body: data }),
  deleteTask: id => request(`/api/tasks/${id}`, { method: 'DELETE' }),
  updateStatus: (id, status) => request(`/api/tasks/${id}/status`, { method: 'PATCH', body: { status } }),
  addLog: (id, data) => request(`/api/tasks/${id}/logs`, { method: 'POST', body: data }),
  uploadFiles: (id, files) => {
    const form = new FormData();
    [...files].forEach(f => form.append('files', f));
    return request(`/api/tasks/${id}/attachments`, { method: 'POST', form });
  },
  deleteAttachment: id => request(`/api/attachments/${id}`, { method: 'DELETE' }),

  activity: (take = 10) => request(`/api/activity?take=${take}`),
  notifications: () => request('/api/notifications'),
  readAllNotifications: () => request('/api/notifications/read-all', { method: 'POST' }),

  /** تنزيل ملف مع إرسال رمز الدخول ثم حفظه في جهاز المستخدم */
  async download(url, fileName) {
    const res = await request(url, { raw: true });
    const blob = await res.blob();
    const href = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href, download: fileName });
    document.body.append(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  }
};
