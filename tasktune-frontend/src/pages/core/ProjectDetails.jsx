import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, ProgressBar, Button } from '../../components/ui';
import { projectService } from '../../services/projectService';
import { userService } from '../../services/userService';
import { FolderKanban, Calendar, User as UserIcon, ArrowLeft } from 'lucide-react';

const STATUS_MAP = { 0: 'Planning', 1: 'Active', 2: 'Completed' };
const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'Completed', 3: 'In Review' };

export default function ProjectDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [project, setProject] = useState(null);
  const [usersMap, setUsersMap] = useState({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data } = await projectService.getProject(id);
        setProject(data);
        
        try {
          const { data: userRes } = await userService.getUsers(1, 100);
          const userItems = userRes?.items || userRes?.Items || [];
          const map = {};
          userItems.forEach(u => {
            map[u.userId ?? u.id] = u.fullName ?? u.name ?? 'Unknown';
          });
          setUsersMap(map);
        } catch { }
      } catch (err) {
        console.error('Failed to load project details:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [id]);

  if (loading) return <AppLayout title="Project Details"><div className="flex items-center justify-center p-8 text-text-secondary">Loading...</div></AppLayout>;
  if (!project) return <AppLayout title="Project Details"><div className="p-8 text-center text-text-secondary">Project not found</div></AppLayout>;

  const tasks = project.tasks || [];
  const completedTasks = tasks.filter(t => t.status === 2).length;
  const progressPct = tasks.length > 0 ? Math.round((completedTasks / tasks.length) * 100) : 0;

  return (
    <AppLayout title="Project Details">
      <div className="mb-4">
        <Button variant="secondary" onClick={() => navigate('/projects')}><ArrowLeft size={16} className="inline mr-2"/> Back to Projects</Button>
      </div>

      <Card className="mb-6 xl:w-2/3">
        <div className="flex justify-between items-start mb-4">
          <div>
            <h1 className="text-2xl font-bold text-white mb-2 flex items-center gap-2">
              <FolderKanban size={24} className="text-orange" /> {project.projectName}
            </h1>
            <p className="text-text-secondary">{project.description || 'No description provided.'}</p>
          </div>
          <Badge variant={project.status === 2 ? 'success' : project.status === 1 ? 'orange' : 'secondary'}>
            {STATUS_MAP[project.status] || 'Unknown'}
          </Badge>
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <div className="p-3 bg-bg rounded-lg">
            <p className="text-xs text-text-muted flex items-center gap-1"><UserIcon size={12}/> Manager</p>
            <p className="font-medium text-white">{project.managerName || usersMap[project.managerId] || 'Unknown'}</p>
          </div>
          <div className="p-3 bg-bg rounded-lg">
            <p className="text-xs text-text-muted flex items-center gap-1"><Calendar size={12}/> Start Date</p>
            <p className="font-medium text-white">{project.startDate}</p>
          </div>
          <div className="p-3 bg-bg rounded-lg">
            <p className="text-xs text-text-muted flex items-center gap-1"><Calendar size={12}/> Due Date</p>
            <p className="font-medium text-white">{project.endDate}</p>
          </div>
        </div>

        <div>
          <div className="flex justify-between mb-1">
            <span className="text-sm font-medium text-text-secondary">Overall Progress</span>
            <span className="text-sm font-bold text-white">{progressPct}%</span>
          </div>
          <ProgressBar value={progressPct} color="bg-orange" />
          <p className="text-xs text-text-muted mt-2">{completedTasks} of {tasks.length} tasks completed</p>
        </div>
      </Card>

      <h2 className="text-xl font-bold text-white mb-4">Tasks Breakdown</h2>
      
      {tasks.length === 0 ? (
        <Card><p className="text-center text-text-muted py-4">No tasks allocated to this project.</p></Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {tasks.map(task => (
            <Card key={task.taskId || task.id} className="border-l-4" style={{borderLeftColor: task.status === 2 ? '#22C55E' : task.status === 1 ? '#F97316' : '#6B7280'}}>
              <h3 className="font-semibold text-white mb-2">{task.taskName}</h3>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-text-muted">Status</span>
                <span className="font-medium text-white">{TASK_STATUS_MAP[task.status] || 'Unknown'}</span>
              </div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-text-muted">Assigned To</span>
                <span className="font-medium text-text-secondary">{task.assignedToName || usersMap[task.assignedTo] || `User #${task.assignedTo}`}</span>
              </div>
              {task.dueDate && (
                <div className="flex justify-between text-sm">
                  <span className="text-text-muted">Due Date</span>
                  <span className="text-text-secondary">{task.dueDate}</span>
                </div>
              )}
              <Button className="mt-4 w-full" onClick={() => navigate(`/tasks/${task.taskId || task.id}`)}>
                Open Task
              </Button>
            </Card>
          ))}
        </div>
      )}
    </AppLayout>
  );
}
