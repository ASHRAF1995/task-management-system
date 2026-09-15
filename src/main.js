import { api, session, setUnauthorizedHandler } from './api.js';

const statusMeta = {
  todo: { label: 'لم تبدأ', className: 'todo' },
  progress: { label: 'قيد التنفيذ', className: 'progress' },
  review: { label: 'بانتظار المراجعة', className: 'review' },
  done: { label: 'مكتملة', className: 'done' }
};
const priorityMeta = {
  high: { label: 'عالية', className: 'high' },
  medium: { label: 'متوسطة', className: 'medium' },
  low: { label: 'منخفضة', className: 'low' }
};

// ---------------- الحالة ----------------
let state = {
  currentUser: session.user,
  activePage: 'dashboard',
  selectedTaskId: null,
  filter: 'all',
  search: '',
  modal: null,
  editingUserId: null,
  loading: false
};
let users = [];
let tasks = [];            // ملخصات المهام المتاحة للمستخدم
let activity = [];         // آخر النشاطات (للمدير)
let notificationItems = [];
let currentTask = null;    // تفاصيل المهمة المفتوحة

const app = document.querySelector('#app');
const currentUser = () => state.currentUser;
const isSuper = () => currentUser()?.role === 'super';
const isManager = () => ['manager', 'super'].includes(currentUser()?.role); // صلاحيات إدارة المهام
const roleLabels = { super: 'مدير النظام', manager: 'مدير', employee: 'موظف' };

// ---------------- أدوات مساعدة ----------------
const esc = (v = '') => String(v).replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c]));
const date = value => new Intl.DateTimeFormat('ar-EG', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(value));
const time = value => new Intl.DateTimeFormat('ar-EG', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' }).format(new Date(value));
const todayStr = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };
const isOverdue = t => t.dueDate < todayStr() && t.status !== 'done';
const fileSize = bytes => bytes < 1024 * 1024 ? `${Math.max(1, Math.ceil(bytes / 1024))} ك.ب` : `${(bytes / 1024 / 1024).toFixed(1)} م.ب`;
const hours = n => Number(n).toLocaleString('ar-EG', { maximumFractionDigits: 2 });
const user = (id, fallbackName) => users.find(u => u.id === id) || { id, name: fallbackName || 'حساب محذوف', initials: '—', color: '#94a3b8', title: '', email: '' };
const initials = (id, fallbackName) => { const u = user(id, fallbackName); return `<span class="avatar" style="--avatar:${esc(u.color)}" title="${esc(u.name)}">${esc(u.initials)}</span>`; };
const taskStatus = key => `<span class="badge status ${statusMeta[key].className}"><i></i>${statusMeta[key].label}</span>`;
const priority = key => `<span class="priority ${priorityMeta[key].className}">${priorityMeta[key].label}</span>`;
const notify = text => { document.querySelectorAll('.toast').forEach(t => t.remove()); const el = document.createElement('div'); el.className = 'toast'; el.textContent = text; document.body.append(el); setTimeout(() => el.remove(), 2600); };

/** ينفّذ طلباً للخادم مع إظهار مؤشر التحميل وعرض رسالة الخطأ إن وجدت */
async function run(action, { successMessage } = {}) {
  setLoading(true);
  try {
    const result = await action();
    if (successMessage) notify(successMessage);
    return result;
  } catch (err) {
    notify(err.message || 'حدث خطأ غير متوقع.');
    return undefined;
  } finally {
    setLoading(false);
  }
}
function setLoading(value) {
  state.loading = value;
  document.body.classList.toggle('is-loading', value);
  document.querySelectorAll('form button.primary').forEach(b => { b.disabled = value; });
}

// ---------------- تحميل البيانات ----------------
async function loadData() {
  const [u, t, n, a] = await Promise.all([
    api.users(),
    api.tasks(),
    api.notifications(),
    isManager() ? api.activity(5) : Promise.resolve([])
  ]);
  users = u; tasks = t; notificationItems = n; activity = a;
}
const refresh = () => run(loadData);

async function openTask(id) {
  const task = await run(() => api.task(id));
  if (!task) return;
  currentTask = task;
  state.selectedTaskId = id;
  state.activePage = 'detail';
  render();
}

async function afterTaskChange(updatedTask) {
  if (updatedTask) currentTask = updatedTask;
  else if (state.selectedTaskId) currentTask = await api.task(state.selectedTaskId);
  await loadData();
}

// ---------------- القوالب ----------------
function navItem(page, icon, label) { return `<button class="nav-item ${state.activePage === page ? 'active' : ''}" data-page="${page}"><span>${icon}</span>${label}</button>`; }

function shell(content) {
  const me = currentUser();
  const unread = unreadNotifications().length;
  return `<div class="layout"><aside class="sidebar"><div class="brand"><span class="brand-mark">✓</span><span>إنجاز</span></div><div class="workspace">مساحة العمل</div><nav>${navItem('dashboard', '▦', 'لوحة التحكم')}${navItem('tasks', '☷', 'المهام')}${isManager() ? navItem('users', '♙', 'الفريق') : ''}${isSuper() ? navItem('accounts', '⊕', 'إدارة الحسابات') : ''}</nav><div class="sidebar-bottom"><div class="user-mini">${initials(me.id, me.name)}<div><strong>${esc(me.name)}</strong><small>${esc(me.title)}</small></div><button id="logout" title="تسجيل الخروج">↪</button></div></div></aside><main><header><button class="mobile-menu" id="mobile-menu">☰</button><div><h1>${pageTitle()}</h1><p>${isManager() ? 'تابع العمل ونظّم مهام فريقك بسهولة' : 'أهلاً بك، إليك المهام التي تنتظرك اليوم'}</p></div><div class="header-actions"><button class="icon-btn" id="notifications" aria-label="الإشعارات">♧${unread ? `<b>${unread}</b>` : ''}</button>${isManager() ? '<button class="primary" data-action="new-task">+ مهمة جديدة</button>' : ''}</div></header><section class="content">${content}</section></main></div>${state.modal ? modal() : ''}`;
}

function pageTitle() {
  return ({
    dashboard: isManager() ? 'لوحة التحكم' : 'مهامي',
    tasks: 'كل المهام',
    users: 'الفريق',
    accounts: 'إدارة الحسابات',
    notifications: 'الإشعارات',
    'edit-task': 'تعديل المهمة'
  })[state.activePage] || 'تفاصيل المهمة';
}

function taskCard(t, compact = false) {
  const overdue = isOverdue(t);
  return `<article class="task-card ${compact ? 'compact' : ''}" data-task="${t.id}"><div class="task-top">${priority(t.priority)}${taskStatus(t.status)}</div><h3>${esc(t.title)}</h3><p>${esc(t.description)}</p><div class="task-footer"><div class="avatars">${t.assignees.slice(0, 3).map(id => initials(id)).join('')}${t.assignees.length > 3 ? `<span class="avatar more">+${t.assignees.length - 3}</span>` : ''}</div><span class="due ${overdue ? 'overdue' : ''}">◷ ${overdue ? 'متأخرة · ' : ''}${date(t.dueDate)}</span></div></article>`;
}

function dashboard() {
  const total = tasks.length;
  const done = tasks.filter(t => t.status === 'done').length;
  const progress = tasks.filter(t => t.status === 'progress').length;
  const overdue = tasks.filter(isOverdue).length;
  const recent = tasks.filter(t => t.status !== 'done').slice(0, 4);

  const teamProgress = users.filter(u => u.role === 'employee').map(u => {
    const assigned = tasks.filter(t => t.assignees.includes(u.id));
    const completed = assigned.filter(t => t.status === 'done').length;
    const pct = assigned.length ? Math.round(completed / assigned.length * 100) : 0;
    return `<div class="member-progress">${initials(u.id)}<div class="grow"><div><strong>${esc(u.name)}</strong><span>${completed} من ${assigned.length} مهام مكتملة</span></div><div class="progress-bar"><i style="width:${pct}%"></i></div></div><b>${pct}%</b></div>`;
  }).join('');

  const activityRows = activity.map(h => `<div class="activity-row">${initials(h.userId, h.userName)}<div><strong>${esc(h.userName)}</strong> <span>${esc(h.text)} — «${esc(h.taskTitle)}»</span><small>${time(h.createdAt)}</small></div></div>`).join('');

  const managementInsights = isManager()
    ? `<div class="dashboard-bottom"><div class="panel"><div class="panel-head"><h2>تقدم الفريق</h2><span>كل المهام</span></div>${teamProgress || '<div class="empty">لا يوجد موظفون بعد</div>'}</div><div class="panel activity"><div class="panel-head"><h2>آخر النشاطات</h2></div>${activityRows || '<div class="empty">لا توجد نشاطات بعد</div>'}</div></div>`
    : '';

  return shell(`<div class="kpis"><div class="kpi blue"><span>☷</span><div><small>إجمالي المهام</small><strong>${total}</strong><em>مهمة</em></div></div><div class="kpi orange"><span>◷</span><div><small>قيد التنفيذ</small><strong>${progress}</strong><em>تحتاج متابعة</em></div></div><div class="kpi green"><span>✓</span><div><small>المهام المكتملة</small><strong>${done}</strong><em>${total ? Math.round(done / total * 100) : 0}% من الإجمالي</em></div></div><div class="kpi red"><span>!</span><div><small>مهام متأخرة</small><strong>${overdue}</strong><em>${overdue ? 'تحتاج اهتماماً' : 'كل شيء على ما يرام'}</em></div></div></div><div class="section-head"><div><h2>${isManager() ? 'نظرة على المهام' : 'المهام المفتوحة'}</h2><p>آخر المهام التي تحتاج إلى عمل</p></div><button class="link-btn" data-page="tasks">عرض الكل ←</button></div><div class="task-grid">${recent.length ? recent.map(t => taskCard(t)).join('') : '<div class="empty">لا توجد مهام مفتوحة الآن. عمل رائع!</div>'}</div>${managementInsights}`);
}

function tasksPage() {
  const term = state.search.trim().toLowerCase();
  const list = tasks.filter(t => (state.filter === 'all' || t.status === state.filter) && `${t.title} ${t.description}`.toLowerCase().includes(term));
  return shell(`<div class="toolbar"><div class="search">⌕<input id="search" placeholder="ابحث عن مهمة..." value="${esc(state.search)}"></div><div class="filters">${[['all', 'الكل'], ['todo', 'لم تبدأ'], ['progress', 'قيد التنفيذ'], ['review', 'مراجعة'], ['done', 'مكتملة']].map(([k, l]) => `<button class="filter ${state.filter === k ? 'active' : ''}" data-filter="${k}">${l}</button>`).join('')}</div></div><div class="tasks-list">${list.length ? list.map(t => taskCard(t)).join('') : '<div class="empty">لا توجد مهام مطابقة لهذا البحث.</div>'}</div>`);
}

function usersPage() {
  return shell(`<div class="team-grid">${users.map(u => {
    const count = tasks.filter(t => t.assignees.includes(u.id)).length;
    return `<div class="member-card">${initials(u.id)}<div><h3>${esc(u.name)}</h3><p>${esc(u.title)}</p><span class="role ${u.role}">${roleLabels[u.role]}</span></div><strong>${count}<small>مهام</small></strong></div>`;
  }).join('')}</div>`);
}

function loginPage() {
  return `<main class="login-page"><section class="login-brand"><div class="brand"><span class="brand-mark">✓</span><span>إنجاز</span></div><h1>منظّم العمل لفريقك</h1><p>تابع المهام، سجّل الإنجاز، واجعل كل الفريق على المسار نفسه.</p></section><form class="login-card" id="login-form"><h2>تسجيل الدخول</h2><p>أدخل بيانات حسابك للمتابعة.</p><label>البريد الإلكتروني<input name="email" type="email" required placeholder="name@company.com"></label><label>كلمة المرور<input name="password" type="password" required placeholder="••••••••"></label><button class="primary">دخول إلى النظام</button></form></main>`;
}

function roleSelect(selected = 'employee', disabled = false) {
  if (disabled) return `<label>الدور<select name="role" disabled><option>${roleLabels.super}</option></select></label>`;
  return `<label>الدور<select name="role">${['employee', 'manager'].map(r => `<option value="${r}" ${selected === r ? 'selected' : ''}>${roleLabels[r]}</option>`).join('')}</select></label>`;
}

function accountsPage() {
  const accounts = users;
  const editing = state.editingUserId ? users.find(u => u.id === state.editingUserId) : null;
  const form = editing
    ? `<form id="edit-account-form" class="account-form"><label>الاسم الكامل<input name="name" required value="${esc(editing.name)}"></label><label>المسمى الوظيفي<input name="title" required value="${esc(editing.title)}"></label><label>البريد الإلكتروني<input name="email" type="email" required value="${esc(editing.email)}"></label>${roleSelect(editing.role, editing.role === 'super')}<label>كلمة المرور الجديدة<input name="password" type="password" minlength="6" placeholder="اتركها فارغة"></label><button type="button" class="secondary" id="cancel-edit">إلغاء</button><button class="primary">حفظ التعديلات</button></form>`
    : `<form id="account-form" class="account-form"><label>الاسم الكامل<input name="name" required placeholder="مثال: أحمد سامي"></label><label>المسمى الوظيفي<input name="title" required placeholder="مثال: مطور واجهات"></label><label>البريد الإلكتروني<input name="email" type="email" required placeholder="ahmed@company.com"></label>${roleSelect()}<label>كلمة المرور<input name="password" type="password" minlength="6" required placeholder="6 أحرف على الأقل"></label><button class="primary">إنشاء الحساب</button></form>`;
  const rows = accounts.map(u => {
    const canDelete = u.role !== 'super' && u.id !== currentUser().id;
    return `<div class="account-row">${initials(u.id)}<div><strong>${esc(u.name)}</strong><small>${esc(u.email)}</small></div><span>${esc(u.title)} · <span class="role ${u.role}">${roleLabels[u.role]}</span></span><div class="account-actions"><button class="edit-account" data-edit-account="${u.id}">تعديل</button>${canDelete ? `<button class="delete-account" data-delete-account="${u.id}">حذف</button>` : ''}</div></div>`;
  }).join('');
  return shell(`<div class="accounts-layout"><section class="panel"><div class="panel-head"><div><h2>${editing ? 'تعديل الحساب' : 'إنشاء حساب جديد'}</h2><p>${editing ? 'اترك كلمة المرور فارغة إذا لم ترغب بتغييرها.' : 'أضف مديراً أو موظفاً ليتمكن من تسجيل الدخول.'}</p></div></div>${form}</section><section class="panel"><div class="panel-head"><div><h2>كل الحسابات</h2><p>${accounts.filter(u => u.role === 'manager').length} مديرين · ${accounts.filter(u => u.role === 'employee').length} موظفين</p></div></div><div class="account-table">${rows}</div></section></div>`);
}

function taskDetails() {
  const t = currentTask;
  if (!t) { state.activePage = 'tasks'; return tasksPage(); }
  const canEdit = isManager() || t.assignees.includes(currentUser().id);
  const totalHours = t.logs.reduce((sum, l) => sum + Number(l.hours), 0);
  const managerActions = isManager() ? `<button class="secondary edit-task-button" data-action="edit-task">تعديل المهمة</button><button class="secondary delete-task-button" data-action="delete-task">حذف</button>` : '';
  const statusSelect = canEdit ? `<select class="status-select" data-status-task="${t.id}">${Object.entries(statusMeta).map(([k, v]) => `<option value="${k}" ${t.status === k ? 'selected' : ''}>${v.label}</option>`).join('')}</select>` : '';

  return shell(`<button class="back" data-page="tasks">→ العودة إلى المهام</button><div class="detail-layout"><article class="detail-main"><div class="detail-title"><div><div class="inline-badges">${priority(t.priority)}${taskStatus(t.status)}</div><h2>${esc(t.title)}</h2></div>${statusSelect}${managerActions}</div><section><h3>الوصف</h3><p class="description">${esc(t.description)}</p></section><section class="detail-info"><div><small>تاريخ البدء</small><strong>${date(t.startDate)}</strong></div><div><small>تاريخ الاستحقاق</small><strong>${date(t.dueDate)}</strong></div><div><small>المسند إليهم</small><span class="avatars">${t.assignees.map(id => initials(id)).join('') || '<span class="muted">لا أحد</span>'}</span></div></section><section><div class="section-title"><h3>سجل العمل</h3><span>${hours(totalHours)} ساعة مسجلة</span></div>${canEdit ? `<form id="log-form" class="log-form"><input name="hours" type="number" min="0.25" max="24" step="0.25" placeholder="الساعات" required><input name="text" placeholder="ما الذي أنجزته؟" maxlength="1000" required><button class="primary">إضافة سجل</button></form>` : ''}<div class="log-list">${t.logs.length ? t.logs.map(l => `<div class="log-item">${initials(l.userId, l.userName)}<div><strong>${esc(l.userName)}</strong><p>${esc(l.text)}</p><small>${time(l.createdAt)}</small></div><b>${hours(l.hours)} س</b></div>`).join('') : '<div class="muted">لم يتم تسجيل أي وقت بعد.</div>'}</div></section><section><div class="section-title"><h3>الملفات والمرفقات</h3></div>${canEdit ? '<label class="upload"><input id="file-input" type="file" multiple>⇧ أضف ملفات أو صوراً</label>' : ''}<div class="attachments">${t.attachments.map(a => `<a class="attachment" href="#" data-download="${a.id}">${a.type.startsWith('image/') ? '▧' : '▤'}<span>${esc(a.name)}</span><small>${fileSize(a.size)}</small></a>`).join('') || '<div class="muted">لا توجد مرفقات.</div>'}</div></section></article><aside class="activity-panel"><h3>سجل النشاط</h3>${t.history.length ? t.history.map(h => `<div class="timeline"><i></i><div><strong>${esc(h.userName)}</strong><p>${esc(h.text)}</p><small>${time(h.createdAt)}</small></div></div>`).join('') : '<div class="muted">سيظهر سجل تغييرات المهمة هنا.</div>'}</aside></div>`);
}

function assigneesField(selected = []) {
  return `<fieldset><legend>إسناد المهمة إلى</legend><div class="assignees">${users.filter(u => u.role === 'employee').map(u => `<label class="assign"><input type="checkbox" name="assignees" value="${u.id}" ${selected.includes(u.id) ? 'checked' : ''}>${initials(u.id)}<span>${esc(u.name)}</span></label>`).join('') || '<span class="muted">أنشئ حساب موظف أولاً.</span>'}</div></fieldset>`;
}

function taskEditorPage() {
  const t = currentTask;
  if (!t || !isManager()) { state.activePage = 'tasks'; return tasksPage(); }
  return shell(`<button class="back" data-page="detail">→ العودة إلى تفاصيل المهمة</button><form id="edit-task-form" class="panel task-editor"><div class="panel-head"><div><h2>تعديل المهمة</h2><p>عدّل تفاصيل المهمة والإسناد حسب احتياج الفريق.</p></div></div><label>عنوان المهمة<input name="title" required maxlength="200" value="${esc(t.title)}"></label><label>الوصف<textarea name="description" required maxlength="4000">${esc(t.description)}</textarea></label><div class="form-grid"><label>الأولوية<select name="priority">${Object.entries(priorityMeta).map(([k, v]) => `<option value="${k}" ${t.priority === k ? 'selected' : ''}>${v.label}</option>`).join('')}</select></label><label>الحالة<select name="status">${Object.entries(statusMeta).map(([k, v]) => `<option value="${k}" ${t.status === k ? 'selected' : ''}>${v.label}</option>`).join('')}</select></label><label>تاريخ البدء<input name="startDate" type="date" required value="${t.startDate}"></label><label>تاريخ الاستحقاق<input name="dueDate" type="date" required value="${t.dueDate}"></label></div>${assigneesField(t.assignees)}<div class="modal-actions"><button type="button" class="secondary" data-page="detail">إلغاء</button><button class="primary">حفظ التعديلات</button></div></form>`);
}

const unreadNotifications = () => notificationItems.filter(n => !n.isRead);

function notificationsPage() {
  const items = notificationItems;
  return shell(`<div class="notifications-page"><div class="panel"><div class="panel-head"><div><h2>الإشعارات</h2><p>المهام التي تحتاج إلى متابعتك الآن.</p></div><span class="notification-count">${unreadNotifications().length} جديدة</span></div>${items.length ? `<div class="notification-list">${items.map(n => `<button class="notification-item" data-task="${n.taskId}"><span class="notice-icon ${n.overdue ? 'danger' : 'warning'}">${n.overdue ? '!' : '◷'}</span><span><strong>${n.overdue ? 'مهمة متأخرة' : 'موعد المهمة قريب'}</strong><p>${esc(n.title)}</p><small>${n.overdue ? `تجاوزت تاريخ الاستحقاق ${date(n.dueDate)}` : `تاريخ الاستحقاق: ${date(n.dueDate)}`}</small></span><b>عرض ←</b></button>`).join('')}</div>` : '<div class="empty">لا توجد إشعارات جديدة. كل شيء تحت السيطرة.</div>'}</div></div>`);
}

function modal() {
  return `<div class="modal-wrap"><div class="modal-backdrop" data-action="close-modal"></div><form class="modal" id="task-form"><div class="modal-head"><div><h2>إنشاء مهمة جديدة</h2><p>أضف التفاصيل ثم حدّد أعضاء الفريق المسؤولين عنها.</p></div><button type="button" class="close" data-action="close-modal">×</button></div><label>عنوان المهمة<input name="title" required maxlength="200" placeholder="مثال: إعداد خطة الإطلاق"></label><label>الوصف<textarea name="description" required maxlength="4000" placeholder="اكتب وصفاً واضحاً لما يجب إنجازه..."></textarea></label><div class="form-grid"><label>الأولوية<select name="priority"><option value="high">عالية</option><option value="medium" selected>متوسطة</option><option value="low">منخفضة</option></select></label><label>الحالة<select name="status">${Object.entries(statusMeta).map(([k, v]) => `<option value="${k}">${v.label}</option>`).join('')}</select></label><label>تاريخ البدء<input name="startDate" type="date" value="${todayStr()}" required></label><label>تاريخ الاستحقاق<input name="dueDate" type="date" required></label></div>${assigneesField()}<div class="modal-actions"><button type="button" class="secondary" data-action="close-modal">إلغاء</button><button class="primary">إنشاء المهمة</button></div></form></div>`;
}

// ---------------- العرض والربط ----------------
function render() {
  if (!currentUser()) { app.innerHTML = loginPage(); bind(); return; }
  if ((state.activePage === 'users' && !isManager()) || (state.activePage === 'accounts' && !isSuper())) state.activePage = 'dashboard';
  const pages = {
    dashboard,
    tasks: tasksPage,
    users: usersPage,
    accounts: accountsPage,
    notifications: notificationsPage,
    'edit-task': taskEditorPage,
    detail: taskDetails
  };
  app.innerHTML = (pages[state.activePage] || dashboard)();
  bind();
}

function bind() {
  const $ = s => document.querySelector(s);
  const $$ = s => document.querySelectorAll(s);

  $('#login-form')?.addEventListener('submit', login);

  $$('[data-page]').forEach(e => e.onclick = () => {
    const page = e.dataset.page;
    state.activePage = page;
    if (page !== 'detail') { state.selectedTaskId = null; currentTask = null; }
    render();
  });
  $$('[data-task]').forEach(e => e.onclick = () => openTask(e.dataset.task));
  $$('[data-filter]').forEach(e => e.onclick = () => { state.filter = e.dataset.filter; render(); });
  $('[data-action="new-task"]')?.addEventListener('click', () => { state.modal = 'new'; render(); });
  $$('[data-action="close-modal"]').forEach(e => e.onclick = () => { state.modal = null; render(); });
  $('[data-action="edit-task"]')?.addEventListener('click', () => { state.activePage = 'edit-task'; render(); });
  $('[data-action="delete-task"]')?.addEventListener('click', deleteTask);

  $('#search')?.addEventListener('input', e => {
    state.search = e.target.value;
    render();
    const input = $('#search');
    input.focus();
    input.setSelectionRange(state.search.length, state.search.length);
  });

  $('#task-form')?.addEventListener('submit', createTask);
  $('#edit-task-form')?.addEventListener('submit', updateTask);
  $('#account-form')?.addEventListener('submit', createAccount);
  $('#edit-account-form')?.addEventListener('submit', updateAccount);
  $('#cancel-edit')?.addEventListener('click', () => { state.editingUserId = null; render(); });
  $$('[data-edit-account]').forEach(e => e.onclick = () => { state.editingUserId = e.dataset.editAccount; render(); });
  $$('[data-delete-account]').forEach(e => e.onclick = () => deleteAccount(e.dataset.deleteAccount));

  $('[data-status-task]')?.addEventListener('change', updateStatus);
  $('#log-form')?.addEventListener('submit', addLog);
  $('#file-input')?.addEventListener('change', addFiles);
  $$('[data-download]').forEach(e => e.onclick = ev => {
    ev.preventDefault();
    const a = currentTask?.attachments.find(x => x.id === e.dataset.download);
    if (a) run(() => api.download(a.url, a.name));
  });

  $('#logout')?.addEventListener('click', logout);
  $('#notifications')?.addEventListener('click', openNotifications);
  $('#mobile-menu')?.addEventListener('click', () => $('.sidebar').classList.toggle('show'));
}

// ---------------- الإجراءات ----------------
function taskPayload(form) {
  const f = new FormData(form);
  return {
    title: f.get('title').trim(),
    description: f.get('description').trim(),
    priority: f.get('priority'),
    status: f.get('status'),
    startDate: f.get('startDate'),
    dueDate: f.get('dueDate'),
    assigneeIds: f.getAll('assignees')
  };
}

function validateTask(data) {
  if (!data.assigneeIds.length) { notify('اختر موظفاً واحداً على الأقل.'); return false; }
  if (data.dueDate < data.startDate) { notify('تاريخ الاستحقاق يجب أن يكون بعد تاريخ البدء.'); return false; }
  return true;
}

async function login(e) {
  e.preventDefault();
  const f = new FormData(e.target);
  const result = await run(() => api.login(f.get('email').trim(), f.get('password')));
  if (!result) return;
  session.save(result.token, result.user);
  state.currentUser = result.user;
  state.activePage = 'dashboard';
  await refresh();
  notify(`مرحباً بك، ${result.user.name}`);
  render();
}

function logout() {
  session.clear();
  Object.assign(state, { currentUser: null, activePage: 'dashboard', selectedTaskId: null, modal: null, editingUserId: null });
  users = []; tasks = []; activity = []; notificationItems = []; currentTask = null;
  render();
}

async function openNotifications() {
  state.activePage = 'notifications';
  state.selectedTaskId = null;
  render(); // نعرض الإشعارات أولاً بحالتها (الجديدة مميزة) ثم نعلّمها كمقروءة
  if (unreadNotifications().length) {
    await run(() => api.readAllNotifications());
    notificationItems = notificationItems.map(n => ({ ...n, isRead: true }));
    const badge = document.querySelector('#notifications b');
    badge?.remove();
  }
}

async function createTask(e) {
  e.preventDefault();
  const data = taskPayload(e.target);
  if (!validateTask(data)) return;
  const task = await run(() => api.createTask(data), { successMessage: 'تم إنشاء المهمة وإسنادها بنجاح.' });
  if (!task) return;
  state.modal = null;
  await run(() => afterTaskChange(task));
  state.selectedTaskId = task.id;
  state.activePage = 'detail';
  render();
}

async function updateTask(e) {
  e.preventDefault();
  const data = taskPayload(e.target);
  if (!validateTask(data)) return;
  const task = await run(() => api.updateTask(state.selectedTaskId, data), { successMessage: 'تم حفظ تعديلات المهمة.' });
  if (!task) return;
  await run(() => afterTaskChange(task));
  state.activePage = 'detail';
  render();
}

async function deleteTask() {
  if (!currentTask || !confirm(`حذف المهمة «${currentTask.title}» نهائياً مع سجلاتها ومرفقاتها؟`)) return;
  const ok = await run(async () => { await api.deleteTask(currentTask.id); return true; }, { successMessage: 'تم حذف المهمة.' });
  if (!ok) return;
  currentTask = null;
  state.selectedTaskId = null;
  state.activePage = 'tasks';
  await refresh();
  render();
}

async function updateStatus(e) {
  const task = await run(() => api.updateStatus(e.target.dataset.statusTask, e.target.value), { successMessage: 'تم تحديث حالة المهمة.' });
  if (task) await run(() => afterTaskChange(task));
  render();
}

async function addLog(e) {
  e.preventDefault();
  const f = new FormData(e.target);
  const log = await run(() => api.addLog(state.selectedTaskId, { hours: Number(f.get('hours')), text: f.get('text').trim() }), { successMessage: 'تمت إضافة سجل العمل.' });
  if (!log) return;
  await run(() => afterTaskChange());
  render();
}

async function addFiles(e) {
  const files = e.target.files;
  if (!files.length) return;
  const saved = await run(() => api.uploadFiles(state.selectedTaskId, files), { successMessage: 'تم رفع الملفات.' });
  if (saved) await run(() => afterTaskChange());
  render();
}

async function createAccount(e) {
  e.preventDefault();
  const f = new FormData(e.target);
  const created = await run(() => api.createUser({
    name: f.get('name').trim(),
    title: f.get('title').trim(),
    email: f.get('email').trim(),
    password: f.get('password'),
    role: f.get('role')
  }), { successMessage: 'تم إنشاء الحساب بنجاح.' });
  if (!created) return;
  await refresh();
  render();
}

async function updateAccount(e) {
  e.preventDefault();
  const f = new FormData(e.target);
  const updated = await run(() => api.updateUser(state.editingUserId, {
    name: f.get('name').trim(),
    title: f.get('title').trim(),
    email: f.get('email').trim(),
    password: f.get('password') || null,
    role: f.get('role') || null
  }), { successMessage: 'تم حفظ تعديلات الحساب.' });
  if (!updated) return;
  state.editingUserId = null;
  await refresh();
  render();
}

async function deleteAccount(id) {
  const target = users.find(u => u.id === id);
  if (!target || !confirm(`حذف حساب ${target.name}؟ سيتم إلغاء إسناد مهامه إليه.`)) return;
  const ok = await run(async () => { await api.deleteUser(id); return true; }, { successMessage: 'تم حذف الحساب وإلغاء إسناد مهامه.' });
  if (!ok) return;
  state.editingUserId = null;
  await refresh();
  render();
}

// ---------------- بدء التشغيل ----------------
setUnauthorizedHandler(() => {
  if (currentUser()) { logout(); notify('انتهت الجلسة، سجّل الدخول مرة أخرى.'); }
});

async function start() {
  if (currentUser()) {
    app.innerHTML = '<div class="app-loading">جارٍ التحميل…</div>';
    const me = await run(() => api.me());
    if (me) {
      state.currentUser = me;
      session.save(session.token, me);
      await refresh();
    }
  }
  render();
}

start();
