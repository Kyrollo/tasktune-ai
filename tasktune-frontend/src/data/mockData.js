// ── Users ──
export const users = [
  { id: 1, name: 'Ahmed Hassan', email: 'ahmed@tasktune.ai', role: 'Employee', department: 'Engineering', position: 'Frontend Developer', status: 'Active', avatar: null },
  { id: 2, name: 'Sara Mohamed', email: 'sara@tasktune.ai', role: 'Supervisor', department: 'Engineering', position: 'Tech Lead', status: 'Active', avatar: null },
  { id: 3, name: 'Omar Ali', email: 'omar@tasktune.ai', role: 'Manager', department: 'Engineering', position: 'Engineering Manager', status: 'Active', avatar: null },
  { id: 4, name: 'Layla Ibrahim', email: 'layla@tasktune.ai', role: 'CEO', department: 'Executive', position: 'Chief Executive Officer', status: 'Active', avatar: null },
  { id: 5, name: 'Nour Khalil', email: 'nour@tasktune.ai', role: 'HR', department: 'Human Resources', position: 'HR Manager', status: 'Active', avatar: null },
  { id: 6, name: 'Youssef Farid', email: 'youssef@tasktune.ai', role: 'Admin', department: 'IT', position: 'System Admin', status: 'Active', avatar: null },
  { id: 7, name: 'Mona Adel', email: 'mona@tasktune.ai', role: 'Employee', department: 'Design', position: 'UI/UX Designer', status: 'Active', avatar: null },
  { id: 8, name: 'Khaled Nasser', email: 'khaled@tasktune.ai', role: 'Employee', department: 'Engineering', position: 'Backend Developer', status: 'Active', avatar: null },
  { id: 9, name: 'Fatima Zayed', email: 'fatima@tasktune.ai', role: 'Supervisor', department: 'Design', position: 'Design Lead', status: 'Active', avatar: null },
  { id: 10, name: 'Amir Rashid', email: 'amir@tasktune.ai', role: 'Employee', department: 'Marketing', position: 'Content Writer', status: 'Inactive', avatar: null },
];

// ── Projects ──
export const projects = [
  { id: 1, name: 'TaskTune Platform', description: 'Main product development for the TaskTune AI platform.', managerId: 3, status: 'Active', startDate: '2025-01-15', endDate: '2026-06-30', taskCount: 24, completionPct: 62 },
  { id: 2, name: 'Mobile App v2', description: 'Redesign and rebuild the mobile experience.', managerId: 3, status: 'Active', startDate: '2025-06-01', endDate: '2026-03-31', taskCount: 18, completionPct: 35 },
  { id: 3, name: 'HR Portal', description: 'Internal HR management portal with self-service features.', managerId: 3, status: 'Planning', startDate: '2026-02-01', endDate: '2026-09-30', taskCount: 8, completionPct: 5 },
  { id: 4, name: 'AI Agent Integration', description: 'Integrate LangChain-based AI agents into the core platform.', managerId: 3, status: 'Active', startDate: '2025-09-01', endDate: '2026-04-30', taskCount: 12, completionPct: 78 },
  { id: 5, name: 'Client Dashboard', description: 'Customer-facing analytics and reporting dashboard.', managerId: 3, status: 'Completed', startDate: '2025-03-01', endDate: '2025-12-31', taskCount: 15, completionPct: 100 },
];

// ── Tasks ──
export const tasks = [
  { id: 1, name: 'Implement Login Page', projectId: 1, assigneeIds: [1], supervisorId: 2, priority: 'High', status: 'Completed', progress: 100, startDate: '2025-12-01', dueDate: '2025-12-15', submitMethod: 'File Upload', definitionOfDone: 'Login form with validation, JWT auth integration, responsive design.', subtasks: [{ id: 1, text: 'Design login form', done: true }, { id: 2, text: 'Implement validation', done: true }, { id: 3, text: 'Connect to auth API', done: true }] },
  { id: 2, name: 'Design Dashboard Layout', projectId: 1, assigneeIds: [7], supervisorId: 9, priority: 'High', status: 'In Progress', progress: 65, startDate: '2026-01-10', dueDate: '2026-02-28', submitMethod: 'Link', definitionOfDone: 'Figma mockup approved, component library started.', subtasks: [{ id: 1, text: 'Create wireframes', done: true }, { id: 2, text: 'Build component library', done: true }, { id: 3, text: 'Final review with team', done: false }] },
  { id: 3, name: 'Build REST API endpoints', projectId: 1, assigneeIds: [8], supervisorId: 2, priority: 'High', status: 'In Progress', progress: 45, startDate: '2026-01-15', dueDate: '2026-03-15', submitMethod: 'File Upload', definitionOfDone: 'All CRUD endpoints functional with tests.', subtasks: [{ id: 1, text: 'User endpoints', done: true }, { id: 2, text: 'Project endpoints', done: false }, { id: 3, text: 'Task endpoints', done: false }, { id: 4, text: 'Write unit tests', done: false }] },
  { id: 4, name: 'Setup CI/CD Pipeline', projectId: 1, assigneeIds: [6], supervisorId: 2, priority: 'Medium', status: 'Not Started', progress: 0, startDate: '2026-03-01', dueDate: '2026-03-20', submitMethod: 'Link', definitionOfDone: 'GitHub Actions pipeline running build + test + deploy.', subtasks: [] },
  { id: 5, name: 'Integrate AI Chatbot', projectId: 4, assigneeIds: [1, 8], supervisorId: 2, priority: 'High', status: 'In Progress', progress: 55, startDate: '2026-01-20', dueDate: '2026-03-10', submitMethod: 'File Upload', definitionOfDone: 'Chatbot widget embedded, LangChain agent responds contextually.', subtasks: [{ id: 1, text: 'Setup LangChain agent', done: true }, { id: 2, text: 'Build chat UI widget', done: true }, { id: 3, text: 'Connect to backend', done: false }, { id: 4, text: 'Test response quality', done: false }] },
  { id: 6, name: 'Write User Documentation', projectId: 1, assigneeIds: [10], supervisorId: 9, priority: 'Low', status: 'Not Started', progress: 0, startDate: '2026-03-15', dueDate: '2026-04-30', submitMethod: 'File Upload', definitionOfDone: 'Complete user guide for all roles.', subtasks: [] },
  { id: 7, name: 'Performance Optimization', projectId: 1, assigneeIds: [8], supervisorId: 2, priority: 'Medium', status: 'In Review', progress: 90, startDate: '2026-01-05', dueDate: '2026-02-20', submitMethod: 'Link', definitionOfDone: 'Page load under 2s, Lighthouse score > 90.', subtasks: [{ id: 1, text: 'Audit current performance', done: true }, { id: 2, text: 'Optimize images', done: true }, { id: 3, text: 'Code splitting', done: true }, { id: 4, text: 'Cache strategy', done: false }] },
  { id: 8, name: 'Mobile Responsive Design', projectId: 2, assigneeIds: [7], supervisorId: 9, priority: 'High', status: 'In Progress', progress: 30, startDate: '2026-02-10', dueDate: '2026-04-15', submitMethod: 'File Upload', definitionOfDone: 'All pages responsive on mobile/tablet breakpoints.', subtasks: [{ id: 1, text: 'Audit all pages', done: true }, { id: 2, text: 'Implement mobile nav', done: false }, { id: 3, text: 'Test on devices', done: false }] },
];

// ── Evaluations ──
export const evaluations = [
  { id: 1, taskId: 1, employeeId: 1, evaluatorId: 2, date: '2025-12-16', overall: 92, quality: 90, timeliness: 95, collaboration: 88, feedback: 'Excellent work on the login page. Clean code, well tested.', aiReport: 'This employee demonstrated strong frontend skills and delivered ahead of schedule. Quality metrics are above team average.' },
  { id: 2, taskId: 7, employeeId: 8, evaluatorId: 2, date: '2026-02-21', overall: 85, quality: 88, timeliness: 78, collaboration: 90, feedback: 'Good optimization work. Minor delay but solid results.', aiReport: 'Performance improvements are significant. Recommended for more complex tasks.' },
  { id: 3, taskId: 2, employeeId: 7, evaluatorId: 9, date: '2026-01-30', overall: 78, quality: 82, timeliness: 70, collaboration: 85, feedback: 'Design quality is great but timeline needs improvement.', aiReport: 'Creative output is strong. Time management coaching suggested.' },
];

// ── Notifications ──
export const notifications = [
  { id: 1, type: 'task', title: 'New Task Assigned', message: 'You have been assigned "Integrate AI Chatbot"', date: '2026-03-02T09:00:00', read: false, link: '/tasks/5' },
  { id: 2, type: 'evaluation', title: 'Evaluation Received', message: 'Your work on "Login Page" has been evaluated', date: '2026-03-01T14:30:00', read: false, link: '/evaluations' },
  { id: 3, type: 'reminder', title: 'Deadline Approaching', message: '"Build REST API endpoints" is due in 13 days', date: '2026-03-01T08:00:00', read: true, link: '/tasks/3' },
  { id: 4, type: 'system', title: 'System Update', message: 'TaskTune AI v2.1 has been deployed', date: '2026-02-28T16:00:00', read: true, link: null },
  { id: 5, type: 'overdue', title: 'Task Overdue', message: '"Design Dashboard Layout" is past its deadline', date: '2026-03-01T00:00:00', read: false, link: '/tasks/2' },
  { id: 6, type: 'task', title: 'Progress Submitted', message: 'Ahmed submitted progress on "Integrate AI Chatbot"', date: '2026-02-27T11:00:00', read: true, link: '/supervisor/review/5' },
];

// ── Audit Logs ──
export const auditLogs = [
  { id: 1, timestamp: '2026-03-02T10:15:00', userId: 6, action: 'CREATE', resourceType: 'User', resourceId: 10, ip: '192.168.1.15', details: { before: null, after: { name: 'Amir Rashid', role: 'Employee' } } },
  { id: 2, timestamp: '2026-03-02T09:30:00', userId: 4, action: 'UPDATE', resourceType: 'Role_Permission', resourceId: 3, ip: '192.168.1.10', details: { before: { access: 'Partial' }, after: { access: 'Full' } } },
  { id: 3, timestamp: '2026-03-01T16:45:00', userId: 2, action: 'UPDATE', resourceType: 'Task', resourceId: 7, ip: '192.168.1.22', details: { before: { status: 'In Progress' }, after: { status: 'In Review' } } },
  { id: 4, timestamp: '2026-03-01T14:20:00', userId: 1, action: 'CREATE', resourceType: 'Task_Progress', resourceId: 12, ip: '192.168.1.30', details: { before: null, after: { progress: 55, taskId: 5 } } },
  { id: 5, timestamp: '2026-02-28T11:00:00', userId: 5, action: 'CREATE', resourceType: 'Communication', resourceId: 8, ip: '192.168.1.18', details: { before: null, after: { type: 'Email', recipients: 'Engineering Dept' } } },
  { id: 6, timestamp: '2026-02-28T09:00:00', userId: 6, action: 'UPDATE', resourceType: 'AI_Config', resourceId: 1, ip: '192.168.1.15', details: { before: { model: 'v1.2' }, after: { model: 'v1.3' } } },
  { id: 7, timestamp: '2026-02-27T15:30:00', userId: 3, action: 'DELETE', resourceType: 'Project', resourceId: 6, ip: '192.168.1.12', details: { before: { name: 'Legacy Migration' }, after: null } },
];

// ── Communications ──
export const communications = [
  { id: 1, senderId: 5, recipientType: 'Department', recipient: 'Engineering', type: 'Email', subject: 'Q1 Performance Review Schedule', message: 'Dear Engineering team, performance reviews will begin next week...', date: '2026-02-28T09:00:00', status: 'Sent' },
  { id: 2, senderId: 5, recipientType: 'Individual', recipient: 'Ahmed Hassan', type: 'Warning', subject: 'Attendance Notice', message: 'This is a formal notice regarding recent attendance patterns...', date: '2026-02-25T14:00:00', status: 'Sent' },
  { id: 3, senderId: 5, recipientType: 'Company', recipient: 'All Employees', type: 'Announcement', subject: 'Company Town Hall - March 2026', message: 'Join us for the quarterly town hall meeting...', date: '2026-03-01T10:00:00', status: 'Scheduled' },
];

// ── Departments ──
export const departments = [
  { id: 1, name: 'Engineering', headId: 3, employeeCount: 12 },
  { id: 2, name: 'Design', headId: 9, employeeCount: 5 },
  { id: 3, name: 'Marketing', headId: null, employeeCount: 4 },
  { id: 4, name: 'Human Resources', headId: 5, employeeCount: 3 },
  { id: 5, name: 'Executive', headId: 4, employeeCount: 2 },
  { id: 6, name: 'IT', headId: 6, employeeCount: 3 },
];

// ── Chart Data ──
export const chartData = {
  taskCompletionTrend: [
    { month: 'Sep', completed: 8 }, { month: 'Oct', completed: 12 }, { month: 'Nov', completed: 15 },
    { month: 'Dec', completed: 10 }, { month: 'Jan', completed: 18 }, { month: 'Feb', completed: 14 },
  ],
  departmentPerformance: [
    { dept: 'Engineering', score: 87 }, { dept: 'Design', score: 82 },
    { dept: 'Marketing', score: 75 }, { dept: 'HR', score: 90 }, { dept: 'IT', score: 85 },
  ],
  taskStatusDistribution: [
    { name: 'Completed', value: 35, color: '#22C55E' }, { name: 'In Progress', value: 28, color: '#F97316' },
    { name: 'In Review', value: 12, color: '#3B82F6' }, { name: 'Not Started', value: 15, color: '#6B7280' },
    { name: 'Overdue', value: 5, color: '#EF4444' },
  ],
  weeklyWorkload: [
    { day: 'Mon', tasks: 5 }, { day: 'Tue', tasks: 8 }, { day: 'Wed', tasks: 6 },
    { day: 'Thu', tasks: 9 }, { day: 'Fri', tasks: 4 },
  ],
  performanceTrend: [
    { month: 'Sep', overall: 80, quality: 82, timeliness: 78 },
    { month: 'Oct', overall: 83, quality: 85, timeliness: 80 },
    { month: 'Nov', overall: 85, quality: 84, timeliness: 86 },
    { month: 'Dec', overall: 82, quality: 80, timeliness: 84 },
    { month: 'Jan', overall: 88, quality: 90, timeliness: 85 },
    { month: 'Feb', overall: 90, quality: 92, timeliness: 88 },
  ],
  engagementHeatmap: [
    { dept: 'Engineering', week1: 92, week2: 88, week3: 95, week4: 90 },
    { dept: 'Design', week1: 85, week2: 90, week3: 82, week4: 88 },
    { dept: 'Marketing', week1: 78, week2: 75, week3: 80, week4: 72 },
    { dept: 'HR', week1: 95, week2: 93, week3: 90, week4: 96 },
    { dept: 'IT', week1: 88, week2: 85, week3: 90, week4: 87 },
  ],
};

// ── Role Matrix ──
export const roleMatrix = {
  pages: ['Dashboard', 'Task Details', 'Submit Progress', 'My Evaluations', 'Supervisor Dashboard', 'Task Review', 'Executive Dashboard', 'Reports', 'HR Dashboard', 'Communication Center', 'Projects', 'Tasks Management', 'Users Management', 'AI Control Panel', 'Role Matrix', 'Org Directory', 'Employee Profile', 'Notifications', 'Audit Logs'],
  roles: ['Employee', 'Supervisor', 'Manager', 'CEO', 'HR', 'Admin'],
  access: [
    ['Full', 'Full', 'Full', 'Full', '—', '—'],
    ['Full', 'Full', 'Full', 'Full', '—', '—'],
    ['Full', 'Full', 'Full', 'Full', '—', '—'],
    ['Full', 'Full', '—', '—', '—', '—'],
    ['—', '—', '—', '—', '—', '—'],
    ['—', 'Full', '—', '—', '—', '—'],
    ['—', '—', 'Full', 'Full', '—', '—'],
    ['—', 'Partial', 'Full', 'Full', 'Partial', '—'],
    ['—', '—', '—', '—', 'Full', '—'],
    ['—', '—', '—', '—', 'Full', '—'],
    ['—', 'Partial', 'Full', 'Full', '—', 'Partial'],
    ['—', 'Full', 'Full', 'Full', '—', 'Full'],
    ['—', '—', '—', '—', '—', 'Full'],
    ['—', '—', '—', 'Full', '—', 'Partial'],
    ['—', '—', '—', 'Full', '—', 'Partial'],
    ['Partial', 'Partial', 'Full', 'Full', 'Full', 'Full'],
    ['Partial', 'Partial', 'Full', 'Full', 'Full', 'Full'],
    ['Full', 'Full', 'Full', 'Full', 'Full', 'Full'],
    ['—', '—', 'Partial', 'Full', '—', 'Full'],
  ],
};

// ── AI Config ──
export const aiConfig = {
  features: [
    { id: 'guidance', name: 'AI Task Guidance', enabled: true, description: 'Provides real-time suggestions for task completion' },
    { id: 'evaluation', name: 'AI Evaluation', enabled: true, description: 'Generates automated performance evaluations' },
    { id: 'summaries', name: 'Executive Summaries', enabled: true, description: 'Creates AI-powered executive reports' },
    { id: 'insights', name: 'Predictive Insights', enabled: false, description: 'Forecasts project risks and delays' },
    { id: 'chatbot', name: 'AI Chatbot', enabled: true, description: 'Real-time AI assistant for employees' },
  ],
  model: { version: 'v1.3', temperature: 0.7, maxTokens: 2048 },
  actionLogs: [
    { id: 1, timestamp: '2026-03-02T08:00:00', agent: 'Task Guidance', input: 'Task #5: Integrate AI Chatbot', output: 'Suggested 4 implementation steps with code examples.', confidence: 0.92 },
    { id: 2, timestamp: '2026-03-01T15:00:00', agent: 'Feedback & Reports', input: 'Evaluation for Task #7', output: 'Generated detailed performance report with 85% overall score.', confidence: 0.88 },
    { id: 3, timestamp: '2026-02-28T12:00:00', agent: 'Email & Logs', input: 'Draft Q1 review email for Engineering dept', output: 'Generated professional email template with key performance data.', confidence: 0.95 },
  ],
};

// helper to get user by id
export const getUserById = (id) => users.find(u => u.id === id);
export const getProjectById = (id) => projects.find(p => p.id === id);
export const getTaskById = (id) => tasks.find(t => t.id === id);
