import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge } from '../../components/ui';
import { orgChartService } from '../../services/orgChartService';
import { ChevronDown, ChevronRight, Building2 } from 'lucide-react';

// Stable module-level constant — avoids react-hooks/exhaustive-deps warning
const ROLE_MAP = { 0: 'Employee', 1: 'Supervisor', 2: 'Manager', 3: 'HR', 4: 'CEO', 5: 'Admin' };

const normalizeRole = (role) =>
  typeof role === 'number' ? (ROLE_MAP[role] || 'Employee') : (role || 'Employee');

// ── DeptNode ─────────────────────────────────────────────────────────────────
function DeptNode({ dept, members, level = 0 }) {
  const [expanded, setExpanded] = useState(true);
  const navigate = useNavigate();
  const head = members.find(m => m.id === dept.headId);

  return (
    <div className={`${level > 0 ? 'ml-8 border-l border-border/50 pl-4' : ''}`}>
      <div
        onClick={() => setExpanded(!expanded)}
        className="flex items-center gap-3 p-3 bg-surface rounded-lg border border-border/50 hover:border-orange/30 cursor-pointer mb-2"
      >
        {members.length > 0
          ? (expanded
              ? <ChevronDown size={16} className="text-text-muted" />
              : <ChevronRight size={16} className="text-text-muted" />)
          : <div className="w-4" />}
        <div className="p-1.5 bg-orange/10 rounded-lg">
          <Building2 size={16} className="text-orange" />
        </div>
        <div className="flex-1">
          <p className="text-sm font-medium text-white">{dept.name}</p>
          <p className="text-xs text-text-muted">
            {dept.employeeCount} members • Head: {head?.name || 'TBD'}
          </p>
        </div>
        <Badge>{dept.employeeCount}</Badge>
      </div>

      {expanded && (
        <div className="ml-4 space-y-1 mb-2">
          {members.map(m => (
            <div
              key={m.id}
              onClick={() => navigate(`/profile/${m.id}`)}
              className="flex items-center gap-3 p-2 rounded-lg hover:bg-surface-hover cursor-pointer transition-colors"
            >
              <div className="w-7 h-7 rounded-full bg-orange/20 flex items-center justify-center text-orange text-xs font-medium">
                {m.name.charAt(0)}
              </div>
              <div>
                <p className="text-sm text-white">{m.name}</p>
                <p className="text-xs text-text-muted">{m.position}</p>
              </div>
              <Badge
                variant={m.role === 'Manager' ? 'orange' : m.role === 'Supervisor' ? 'info' : 'default'}
                className="ml-auto"
              >
                {m.role}
              </Badge>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── OrgChart ──────────────────────────────────────────────────────────────────
export default function OrgChart() {
  const [orgData, setOrgData] = useState({ departments: [], users: [], ceo: null });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data } = await orgChartService.getOrgChart();

        /*
         * Backend response shape (OrgChartController.cs):
         * {
         *   company: { companyId, companyName },
         *   ceo:     { userId, fullName, email, position, role },
         *   departments: [
         *     {
         *       departmentId, departmentName, description, parentDepartmentId,
         *       members: [{ userId, fullName, email, position, role, roleName }]
         *     }
         *   ]
         * }
         */
        const payload = data?.data ?? data;
        const rawDepts = Array.isArray(payload?.departments) ? payload.departments : [];
        const apiCeo   = payload?.ceo ?? null;

        const allUsers = [];
        const depts = rawDepts.map(d => {
            // Support both direct members and members nested inside positions array
            let rawMembers = Array.isArray(d.members) ? d.members : [];
            if (rawMembers.length === 0 && Array.isArray(d.positions)) {
              d.positions.forEach(pos => {
                if (Array.isArray(pos.users)) {
                  rawMembers = rawMembers.concat(pos.users);
                }
              });
            }

            const members = rawMembers.map(u => {
              const role = normalizeRole(u.roleName ?? u.role);
              const mapped = {
                id:         u.userId ?? u.id,
                name:       (u.userName ?? u.fullName ?? `${u.firstName || ''} ${u.lastName || ''}`.trim()) || 'Unknown',
                role:       role,
                position:   u.position ?? u.positionTitle ?? 'N/A',
                department: d.departmentName ?? d.name ?? 'N/A',
              };
            allUsers.push(mapped);
            return mapped;
          });

          return {
            id:            d.departmentId ?? d.id,
            name:          d.departmentName ?? d.name ?? 'Unknown',
            employeeCount: members.length,
            headId:        members.find(m => ['Manager','Supervisor','HR','Admin'].includes(m.role))?.id ?? null,
            parentId:      d.parentDepartmentId ?? null,
            members,
          };
        });

        // Normalize CEO — may come from payload.ceo or already be in allUsers
        let ceoUser = null;
        if (apiCeo) {
          ceoUser = {
            id:       apiCeo.userId ?? apiCeo.id,
            name:     (apiCeo.fullName ?? `${apiCeo.firstName || ''} ${apiCeo.lastName || ''}`.trim()) || 'CEO',
            role:     'CEO',
            position: apiCeo.position ?? 'Chief Executive Officer',
          };
          // Add CEO to allUsers if not already present from departments
          if (!allUsers.find(u => u.id === ceoUser.id)) {
            allUsers.unshift(ceoUser);
          }
        }

        setOrgData({ departments: depts, users: allUsers, ceo: ceoUser });
      } catch (err) {
        console.error('Failed to load org chart:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const { departments, users, ceo } = orgData;

  // Build per-dept member map (keyed by dept id for accuracy)
  const deptUsers = {};
  departments.forEach(d => { deptUsers[d.id] = d.members || []; });

  // Resolve CEO display: prefer payload.ceo, fallback to first CEO-role user
  const ceoDisplay = ceo ?? users.find(u => u.role === 'CEO') ?? null;

  if (loading)
    return (
      <AppLayout title="Organization Chart">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );

  return (
    <AppLayout title="Organization Chart">

      {/* CEO at top */}
      <div className="mb-6">
        <Card className="max-w-sm mx-auto text-center">
          <div className="w-12 h-12 rounded-full bg-orange/20 flex items-center justify-center text-orange text-lg font-bold mx-auto mb-2">
            {ceoDisplay?.name?.charAt(0) || 'C'}
          </div>
          <p className="text-white font-semibold">{ceoDisplay?.name || 'CEO'}</p>
          <p className="text-xs text-text-muted">{ceoDisplay?.position || 'Chief Executive Officer'}</p>
          <Badge variant="orange" className="mt-2">CEO</Badge>
        </Card>
        <div className="w-px h-8 bg-border/50 mx-auto" />
      </div>

      {/* Departments */}
      <div className="grid md:grid-cols-2 gap-4">
        {departments.map(d => (
          <DeptNode
            key={d.id}
            dept={d}
            members={deptUsers[d.id] || []}
            level={d.parentId ? 1 : 0}
          />
        ))}
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mt-8">
        <Card className="text-center">
          <p className="text-2xl font-bold text-orange">{users.length}</p>
          <p className="text-xs text-text-muted">Total Employees</p>
        </Card>
        <Card className="text-center">
          <p className="text-2xl font-bold text-orange">{departments.length}</p>
          <p className="text-xs text-text-muted">Departments</p>
        </Card>
        <Card className="text-center">
          <p className="text-2xl font-bold text-orange">{users.filter(u => u.role === 'Manager').length}</p>
          <p className="text-xs text-text-muted">Managers</p>
        </Card>
        <Card className="text-center">
          <p className="text-2xl font-bold text-orange">{users.filter(u => u.role === 'Supervisor').length}</p>
          <p className="text-xs text-text-muted">Supervisors</p>
        </Card>
      </div>

    </AppLayout>
  );
}
