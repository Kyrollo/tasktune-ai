import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Card, ProgressBar, Modal, Button } from '../../components/ui';
import { evaluationService } from '../../services/evaluationService';
import { projectService } from '../../services/projectService';
import { taskService } from '../../services/taskService';
import { userService } from '../../services/userService';
import { reportingLineService } from '../../services/reportingLineService';
import { useAuth } from '../../contexts/AuthContext';
import { ArrowLeft, Award, Building2, Calendar, Mail, Network, UserRoundPlus, X } from 'lucide-react';

const ROLE_MAP = {
  0: 'Employee',
  1: 'Supervisor',
  2: 'Manager',
  3: 'HR',
  4: 'CEO',
  5: 'Admin',
};

const TASK_STATUS_MAP = {
  0: 'Not Started',
  1: 'In Progress',
  2: 'Completed',
  3: 'In Review',
};

function formatDate(value) {
  if (!value) return 'N/A';
  return new Date(value).toLocaleDateString();
}

export default function EmployeeProfile() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { user: currentUser } = useAuth();
  const canManageReporting = ['HR', 'Manager', 'CEO', 'Admin'].includes(currentUser?.role);
  const [user, setUser] = useState(null);
  const [evaluations, setEvaluations] = useState([]);
  const [tasks, setTasks] = useState([]);
  const [projects, setProjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [managerOptions, setManagerOptions] = useState([]);
  const [showReportingModal, setShowReportingModal] = useState(false);
  const [selectedManagerId, setSelectedManagerId] = useState('');
  const [reportingEffectiveFrom, setReportingEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [reportingSaving, setReportingSaving] = useState(false);
  const [reportingError, setReportingError] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      if (!id) return;

      try {
        const [userRes, evalsRes, tasksRes, projectsRes, directoryRes] = await Promise.all([
          userService.getUser(id),
          evaluationService.getUserEvaluations(id),
          taskService.getTasks(1, 200),
          projectService.getProjects(1, 100),
          canManageReporting ? userService.getUsers(1, 100) : Promise.resolve({ data: { items: [] } }),
        ]);

        const userData = userRes.data || {};
        setUser({
          id: userData.userId,
          fullName: userData.fullName || [userData.firstName, userData.lastName].filter(Boolean).join(' '),
          email: userData.email || '',
          department: userData.department || 'Unassigned',
          position: userData.position || 'N/A',
          role: ROLE_MAP[userData.role] || userData.role || 'Employee',
          status: userData.status || (userData.isActive ? 'Active' : 'Inactive'),
          manager: userData.manager || null,
          directReports: userData.directReports || [],
        });

        const directoryItems = Array.isArray(directoryRes.data) ? directoryRes.data : directoryRes.data?.items || [];
        setManagerOptions(directoryItems.filter((person) => Number(person.userId ?? person.id) !== Number(id)).map((person) => ({
          id: person.userId ?? person.id,
          name: person.fullName || [person.firstName, person.lastName].filter(Boolean).join(' '),
          position: person.positionTitle || person.position?.positionTitle || '',
        })));

        const evaluationItems = Array.isArray(evalsRes.data) ? evalsRes.data : evalsRes.data?.items || [];
        setEvaluations(evaluationItems.map(item => ({
          id: item.evaluationId,
          taskName: item.taskName,
          overallScore: Number(item.overallScore || 0),
          qualityScore: Number(item.qualityScore || 0),
          timelinessScore: Number(item.timelinessScore || 0),
          collaborationScore: Number(item.collaborationScore || 0),
          createdAt: item.createdAt,
          feedback: item.aiGeneratedFeedback || item.manualFeedback || 'No feedback recorded.',
        })));

        const taskItems = Array.isArray(tasksRes.data) ? tasksRes.data : tasksRes.data?.items || [];
        setTasks(taskItems.map(item => ({
          id: item.taskId,
          name: item.taskName,
          assignedTo: item.assignedTo,
          projectId: item.projectId,
          dueDate: item.dueDate,
          progress: item.completionPercentage ?? 0,
          status: TASK_STATUS_MAP[item.status] || 'Not Started',
        })));

        const projectItems = Array.isArray(projectsRes.data) ? projectsRes.data : projectsRes.data?.items || [];
        setProjects(projectItems.map(item => ({
          id: item.projectId,
          name: item.projectName,
        })));
      } catch (error) {
        console.error('Failed to load employee profile:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [id, canManageReporting]);


  const handleAssignManager = async () => {
    if (!selectedManagerId) {
      setReportingError('Choose a manager first.');
      return;
    }
    setReportingSaving(true);
    setReportingError('');
    try {
      const existingLine = user?.manager?.reportingLineId;
      if (existingLine) {
        const start = new Date(`${reportingEffectiveFrom}T00:00:00`);
        start.setDate(start.getDate() - 1);
        const closeDate = `${start.getFullYear()}-${String(start.getMonth() + 1).padStart(2, '0')}-${String(start.getDate()).padStart(2, '0')}`;
        await reportingLineService.close(existingLine, closeDate);
      }
      await reportingLineService.create({
        employeeUserId: Number(id),
        managerUserId: Number(selectedManagerId),
        relationshipType: 'line',
        isPrimary: true,
        effectiveFrom: reportingEffectiveFrom,
        effectiveTo: null,
      });
      const { data } = await userService.getUser(id);
      setUser((previous) => ({ ...previous, manager: data.manager || null }));
      setShowReportingModal(false);
      setSelectedManagerId('');
    } catch (err) {
      setReportingError(err.response?.data?.message || 'Unable to assign the reporting manager. Check for an existing or circular relationship.');
    } finally {
      setReportingSaving(false);
    }
  };

  const userTasks = useMemo(
    () => tasks.filter(task => String(task.assignedTo) === String(id)),
    [id, tasks],
  );

  const completedTasks = userTasks.filter(task => task.status === 'Completed').length;
  const inProgressTasks = userTasks.filter(task => task.status === 'In Progress').length;
  const averageScore = evaluations.length > 0
    ? Math.round(evaluations.reduce((sum, evaluation) => sum + evaluation.overallScore, 0) / evaluations.length)
    : 0;

  if (loading) {
    return (
      <AppLayout title="Employee Profile">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading employee profile...</div>
        </div>
      </AppLayout>
    );
  }

  if (!user) {
    return (
      <AppLayout title="Profile Not Found">
        <Card>
          <p className="text-text-secondary">Employee not found.</p>
        </Card>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Employee Profile">
      <button onClick={() => navigate(-1)} className="flex items-center gap-2 text-sm text-text-secondary hover:text-white mb-4 transition-colors">
        <ArrowLeft size={16} />
        Back
      </button>

      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 space-y-4">
          <Card>
            <div className="text-center">
              <div className="w-20 h-20 rounded-full bg-orange/20 flex items-center justify-center text-orange text-2xl font-bold mx-auto mb-3">
                {(user.fullName || '?').charAt(0)}
              </div>
              <h2 className="text-lg font-semibold text-white">{user.fullName}</h2>
              <p className="text-sm text-text-secondary">{user.position}</p>
              <div className="mt-2">
                <Badge variant={user.status === 'Active' ? 'success' : 'danger'}>{user.status}</Badge>
              </div>
            </div>

            <div className="mt-4 space-y-3 border-t border-border/50 pt-4">
              <div className="flex items-center gap-3">
                <Mail size={14} className="text-text-muted" />
                <span className="text-sm text-text-secondary">{user.email || 'No email available'}</span>
              </div>
              <div className="flex items-center gap-3">
                <Building2 size={14} className="text-text-muted" />
                <span className="text-sm text-text-secondary">{user.department}</span>
              </div>
              <div className="flex items-center gap-3">
                <Award size={14} className="text-text-muted" />
                <span className="text-sm text-text-secondary">{user.role}</span>
              </div>
              <div className="flex items-center gap-3">
                <Calendar size={14} className="text-text-muted" />
                <span className="text-sm text-text-secondary">Joined {formatDate(user.createdAt)}</span>
              </div>
              <div className="flex items-start gap-3">
                <Network size={14} className="text-text-muted mt-1" />
                <div className="text-sm text-text-secondary">
                  <span className="text-text-muted">Reports to: </span>
                  {user.manager ? (
                    <button onClick={() => navigate(`/profile/${user.manager.userId}`)} className="text-orange hover:underline">
                      {user.manager.fullName}
                    </button>
                  ) : 'No primary manager assigned'}
                </div>
              </div>
              {canManageReporting && (
                <Button variant="secondary" className="w-full mt-2" onClick={() => { setReportingError(''); setShowReportingModal(true); }}>
                  <span className="inline-flex items-center gap-2"><UserRoundPlus size={15} /> {user.manager ? 'Change manager' : 'Assign manager'}</span>
                </Button>
              )}
            </div>
          </Card>

          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Performance Summary</h3>
            <div className="grid grid-cols-2 gap-3">
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-orange">{userTasks.length}</p>
                <p className="text-xs text-text-muted">Total Tasks</p>
              </div>
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-success">{completedTasks}</p>
                <p className="text-xs text-text-muted">Completed</p>
              </div>
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-info">{averageScore || '—'}</p>
                <p className="text-xs text-text-muted">Avg Score</p>
              </div>
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-warning">{inProgressTasks}</p>
                <p className="text-xs text-text-muted">In Progress</p>
              </div>
            </div>
          </Card>
        </div>

        <div className="lg:col-span-2 space-y-4">
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Assigned Tasks</h3>
            {userTasks.length === 0 ? (
              <p className="text-sm text-text-muted py-4 text-center">No tasks assigned to this employee.</p>
            ) : (
              <div className="space-y-3">
                {userTasks.map(task => {
                  const project = projects.find(item => item.id === task.projectId);
                  const statusVariant = {
                    'Not Started': 'default',
                    'In Progress': 'warning',
                    'In Review': 'info',
                    Completed: 'success',
                  };

                  return (
                    <div key={task.id} onClick={() => navigate(`/tasks/${task.id}`)} className="flex items-center justify-between p-3 bg-bg rounded-lg border border-border/50 hover:border-orange/30 cursor-pointer transition-colors">
                      <div className="flex-1">
                        <p className="text-sm font-medium text-white">{task.name}</p>
                        <p className="text-xs text-text-muted">{project?.name || 'Unknown Project'} • Due {formatDate(task.dueDate)}</p>
                      </div>
                      <div className="flex items-center gap-3 ml-4">
                        <div className="w-24">
                          <ProgressBar value={task.progress} size="sm" />
                        </div>
                        <Badge variant={statusVariant[task.status] || 'default'}>{task.status}</Badge>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>

          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Evaluations</h3>
            {evaluations.length === 0 ? (
              <p className="text-sm text-text-muted py-4 text-center">No evaluations recorded yet.</p>
            ) : (
              <div className="space-y-3">
                {evaluations.map(evaluation => (
                  <div key={evaluation.id} className="p-4 bg-bg rounded-lg border border-border/50">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-sm text-white font-medium">
                        {evaluation.taskName || 'Evaluation'} • {formatDate(evaluation.createdAt)}
                      </span>
                      <Badge variant={evaluation.overallScore >= 85 ? 'success' : evaluation.overallScore >= 70 ? 'warning' : 'danger'}>
                        {evaluation.overallScore}
                      </Badge>
                    </div>
                    <div className="grid grid-cols-4 gap-3">
                      {[
                        ['Quality', evaluation.qualityScore],
                        ['Timeliness', evaluation.timelinessScore],
                        ['Collaboration', evaluation.collaborationScore],
                        ['Overall', evaluation.overallScore],
                      ].map(([label, value]) => (
                        <div key={label} className="text-center">
                          <p className="text-lg font-bold text-white">{value}</p>
                          <p className="text-xs text-text-muted">{label}</p>
                        </div>
                      ))}
                    </div>
                    <p className="text-xs text-text-secondary mt-3 italic">"{evaluation.feedback}"</p>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {user.directReports?.length > 0 && (
            <Card>
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-semibold text-white">Direct Reports</h3>
                <Badge>{user.directReports.length}</Badge>
              </div>
              <div className="space-y-2">
                {user.directReports.map((person) => (
                  <button key={person.userId} onClick={() => navigate(`/profile/${person.userId}`)}
                    className="w-full flex items-center justify-between gap-3 p-3 bg-bg rounded-lg border border-border/50 hover:border-orange/30 text-left">
                    <span>
                      <span className="block text-sm text-text-primary font-medium">{person.fullName}</span>
                      <span className="block text-xs text-text-muted">{person.position || 'Position not assigned'} · {person.department || 'Unassigned'}</span>
                    </span>
                    <span className="text-xs text-orange">View profile</span>
                  </button>
                ))}
              </div>
            </Card>
          )}
        </div>
      </div>

      <Modal isOpen={showReportingModal} onClose={() => setShowReportingModal(false)} title="Assign primary manager">
        <div className="space-y-4">
          <p className="text-sm text-text-secondary">
            This creates an effective-dated line-management relationship. It is separate from the employee's role title and department.
          </p>
          {reportingError && <div role="alert" className="p-3 bg-danger/10 border border-danger/20 rounded-lg text-sm text-danger">{reportingError}</div>}
          <div>
            <label className="block text-sm text-text-secondary mb-1.5">Manager</label>
            <select value={selectedManagerId} onChange={(e) => setSelectedManagerId(e.target.value)}
              className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm">
              <option value="">Select manager…</option>
              {managerOptions.map((person) => <option key={person.id} value={person.id}>{person.name}{person.position ? ` — ${person.position}` : ''}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm text-text-secondary mb-1.5">Effective from</label>
            <input type="date" value={reportingEffectiveFrom} onChange={(e) => setReportingEffectiveFrom(e.target.value)}
              className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm" />
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setShowReportingModal(false)} disabled={reportingSaving}>Cancel</Button>
            <Button onClick={handleAssignManager} disabled={reportingSaving}>{reportingSaving ? 'Saving…' : 'Save manager'}</Button>
          </div>
        </div>
      </Modal>
    </AppLayout>
  );
}
