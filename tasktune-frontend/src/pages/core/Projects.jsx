import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button, ProgressBar, Modal } from '../../components/ui';
import { projectService } from '../../services/projectService';
import { userService } from '../../services/userService';
import { Plus, FolderKanban, Search } from 'lucide-react';

const STATUS_MAP = { 0: 'Planning', 1: 'Active', 2: 'Completed' };

export default function Projects() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState([]);
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('All');
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState('');
  const [newProject, setNewProject] = useState({
    projectName: '', description: '', managerId: '', startDate: '', endDate: '', status: 1
  });
  const statuses = ['All', 'Active', 'Planning', 'Completed'];

  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data: projRes } = await projectService.getProjects(1, 100);
        const items = (projRes?.items || projRes?.Items || [])?.map(p => ({
          id: p.projectId ?? p.id,
          name: p.projectName ?? p.name,
          description: p.description ?? '',
          managerId: p.managerId,
          status: STATUS_MAP[p.status] ?? 'Active',
          startDate: p.startDate ? (typeof p.startDate === 'string' ? p.startDate.slice(0, 10) : p.startDate) : '',
          taskCount: p.taskCount ?? 0,
          completionPct: (p.taskCount && p.taskCount > 0) ? Math.round(((p.completedTaskCount ?? p.completedTasks ?? 0) / p.taskCount) * 100) : (p.completionPct ?? p.completionPercentage ?? 0)
        }));
        setProjects(items);
        try {
          const { data: userRes } = await userService.getUsers(1, 100);
          const userItems = (userRes?.items || userRes?.Items || [])?.map(u => ({
            id: u.userId ?? u.id,
            name: u.fullName ?? (`${u.firstName || ''} ${u.lastName || ''}`.trim() || u.name),
            role: u.role,
            position: u.positionTitle ?? u.position ?? u.role
          }));
          setUsers(userItems || []);
        } catch {
          console.warn('Could not load users');
        }
      } catch (err) {
        console.error('Failed to load projects:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const handleCreateProject = async () => {
    setError('');
    if (!newProject.projectName || !newProject.managerId) return;
    if (!newProject.endDate) {
      setError('End date is required');
      return;
    }
    setCreating(true);
    try {
      await projectService.createProject({
        projectName: newProject.projectName,
        description: newProject.description || null,
        managerId: Number(newProject.managerId),
        status: Number(newProject.status),
        startDate: newProject.startDate || new Date().toISOString().slice(0, 10),
        endDate: newProject.endDate,
      });
      const { data: projRes } = await projectService.getProjects(1, 100);
      const items = (projRes?.items || projRes?.Items || [])?.map(p => ({
        id: p.projectId ?? p.id,
        name: p.projectName ?? p.name,
        description: p.description ?? '',
        managerId: p.managerId,
        status: STATUS_MAP[p.status] ?? 'Active',
        startDate: p.startDate ? (typeof p.startDate === 'string' ? p.startDate.slice(0, 10) : p.startDate) : '',
        taskCount: p.taskCount ?? 0,
        completionPct: (p.taskCount && p.taskCount > 0) ? Math.round(((p.completedTaskCount ?? p.completedTasks ?? 0) / p.taskCount) * 100) : (p.completionPct ?? p.completionPercentage ?? 0)
      }));
      setProjects(items);
      setShowCreate(false);
      setNewProject({ projectName: '', description: '', managerId: '', startDate: '', endDate: '', status: 1 });
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create project');
    } finally {
      setCreating(false);
    }
  };

  const filtered = projects.filter(p =>
    (filter === 'All' || p.status === filter) &&
    p.name.toLowerCase().includes(search.toLowerCase())
  );

  if (loading) return (
    <AppLayout title="Projects">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  return (
    <AppLayout title="Projects">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          {statuses.map(s => (
            <button key={s} onClick={() => setFilter(s)} className={`px-3 py-1.5 rounded-lg text-sm ${filter === s ? 'bg-orange text-white' : 'bg-surface border border-border text-text-secondary hover:text-white'}`}>{s}</button>
          ))}
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search projects..." className="pl-9 pr-4 py-2 bg-surface border border-border rounded-lg text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange" />
          </div>
          <Button onClick={() => { setError(''); setShowCreate(true); }}><span className="flex items-center gap-2"><Plus size={16} /> New Project</span></Button>
        </div>
      </div>

      {/* Project Grid */}
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
        {filtered.map(p => {
          const manager = users.find(u => u.id === p.managerId);
          const statusVar = { Active: 'orange', Planning: 'info', Completed: 'success' };
          return (
              <Card key={p.id} className="hover:border-orange/30 cursor-pointer transition-colors" onClick={() => navigate(`/projects/${p.id}`)}>
              <div className="flex items-center justify-between mb-3">
                <div className="p-2 bg-orange/10 rounded-lg"><FolderKanban size={18} className="text-orange" /></div>
                <Badge variant={statusVar[p.status]}>{p.status}</Badge>
              </div>
              <h3 className="text-white font-semibold mb-1">{p.name}</h3>
              <p className="text-xs text-text-muted mb-3">{p.description}</p>
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs text-text-secondary">{p.completionPct}% complete</span>
                <span className="text-xs text-text-muted">{p.taskCount} tasks</span>
              </div>
              <ProgressBar value={p.completionPct} size="sm" />
              <div className="flex items-center justify-between mt-3 pt-3 border-t border-border/50">
                <span className="text-xs text-text-muted">Manager: {manager?.name}</span>
                <span className="text-xs text-text-muted">{p.startDate}</span>
              </div>
            </Card>
          );
        })}
      </div>

      {filtered.length === 0 && (
        <div className="text-center py-12">
          <FolderKanban size={40} className="text-text-muted mx-auto mb-3" />
          <p className="text-text-secondary">No projects found matching your criteria.</p>
        </div>
      )}

      {/* Create Modal */}
      <Modal isOpen={showCreate} onClose={() => setShowCreate(false)} title="Create Project">
        <div className="space-y-4">
          <div>
            <label className="block text-sm text-text-secondary mb-1.5">Project Name *</label>
            <input value={newProject.projectName} onChange={e => setNewProject(prev => ({ ...prev, projectName: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange" placeholder="Enter project name..." />
          </div>
          <div>
            <label className="block text-sm text-text-secondary mb-1.5">Description</label>
            <textarea rows={3} value={newProject.description} onChange={e => setNewProject(prev => ({ ...prev, description: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange resize-none" placeholder="Describe the project..." />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Start Date</label>
              <input type="date" value={newProject.startDate} onChange={e => setNewProject(prev => ({ ...prev, startDate: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange" />
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">End Date</label>
              <input type="date" value={newProject.endDate} onChange={e => setNewProject(prev => ({ ...prev, endDate: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange" />
            </div>
          </div>
          <div>
            <label className="block text-sm text-text-secondary mb-1.5">Project Manager *</label>
            <select value={newProject.managerId} onChange={e => setNewProject(prev => ({ ...prev, managerId: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange appearance-none">
              <option value="">Select manager...</option>
              {users.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex justify-end gap-3">
            <Button variant="secondary" onClick={() => setShowCreate(false)} disabled={creating}>Cancel</Button>
            <Button onClick={handleCreateProject} disabled={creating}>{creating ? 'Creating...' : 'Create Project'}</Button>
          </div>
        </div>
      </Modal>
    </AppLayout>
  );
}
