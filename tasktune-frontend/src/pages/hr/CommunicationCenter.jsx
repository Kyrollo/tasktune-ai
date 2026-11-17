import { useState, useEffect, useRef } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import AppLayout from '../../layouts/AppLayout';
import { Card, Button, Badge } from '../../components/ui';
import { communicationService } from '../../services/communicationService';
import { userService } from '../../services/userService';
import { Send, Clock, Sparkles, CheckCircle2, Mail, Users, ShieldCheck, XCircle } from 'lucide-react';

const fallbackTemplates = [
  { key: '', label: 'No template', subject: '', body: '' },
  { key: 'performance_review', label: 'Performance Review Notice', subject: 'Performance review follow-up', body: 'Hello,\n\nThis is a reminder regarding your upcoming performance review.\n\nBest regards,\nHR Team' },
  { key: 'attendance_warning', label: 'Attendance Warning', subject: 'Attendance warning', body: 'Hello,\n\nWe need to discuss your recent attendance record.\n\nRegards,\nHR Team' },
  { key: 'team_meeting', label: 'Team Meeting Invitation', subject: 'Team meeting invitation', body: 'Hello team,\n\nYou are invited to attend the upcoming team meeting.\n\nBest regards,' },
  { key: 'company_announcement', label: 'Company Announcement', subject: 'Company announcement', body: 'Hello everyone,\n\nPlease review the following company update.\n\nThanks,' },
];

export default function CommunicationCenter() {
  const idempotencyKeyRef = useRef(crypto.randomUUID());
  const { user } = useAuth();
  const canApprove = ['CEO', 'Admin'].includes(user?.role);
  const [communications, setCommunications] = useState([]);
  const [users, setUsers] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [templatesList, setTemplatesList] = useState(fallbackTemplates);
  const [agentInfo, setAgentInfo] = useState(null);
  const [stats, setStats] = useState({ totalSent: 0, scheduled: 0 });
  const [loading, setLoading] = useState(true);

  const [recipientType, setRecipientType] = useState('Individual');
  const [commType, setCommType] = useState('Email');
  const [recipient, setRecipient] = useState('');
  const [selectedRecipients, setSelectedRecipients] = useState([]);
  const [selectedTemplate, setSelectedTemplate] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [prompt, setPrompt] = useState('');
  const [cc, setCc] = useState('');

  const [sent, setSent] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [recommending, setRecommending] = useState(false);
  const [recommendation, setRecommendation] = useState(null);
  const [error, setError] = useState('');
  const [approvals, setApprovals] = useState([]);
  const [approvalError, setApprovalError] = useState('');
  const [reviewingApproval, setReviewingApproval] = useState(null);

  const refreshCommunications = async () => {
    const { data } = await communicationService.getCommunications();
    setCommunications(Array.isArray(data) ? data : data?.items ?? []);
  };

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [commsRes, usersRes, overviewRes] = await Promise.all([
          communicationService.getCommunications(),
          userService.getUsers(),
          communicationService.getOverview().catch(() => ({ data: null })),
        ]);
        const commsData = commsRes.data;
        const usersData = usersRes.data;
        setCommunications(Array.isArray(commsData) ? commsData : commsData?.items ?? []);
        setUsers(overviewRes.data?.users || (Array.isArray(usersData) ? usersData : usersData?.items ?? []));
        setDepartments(overviewRes.data?.departments || []);
        setTemplatesList(overviewRes.data?.templates?.length ? overviewRes.data.templates : fallbackTemplates);
        setAgentInfo(overviewRes.data?.agent || null);
        setStats(overviewRes.data?.stats || { totalSent: 0, scheduled: 0 });
      } catch (err) {
        console.error('Failed to load communications:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  useEffect(() => {
    if (!canApprove) return;
    communicationService.getPendingApprovals()
      .then(({ data }) => setApprovals(Array.isArray(data) ? data : data?.items ?? []))
      .catch(() => setApprovalError('Unable to load pending communication approvals.'));
  }, [canApprove]);

  const refreshApprovals = async () => {
    if (!canApprove) return;
    const { data } = await communicationService.getPendingApprovals();
    setApprovals(Array.isArray(data) ? data : data?.items ?? []);
  };

  const handleReviewApproval = async (approvalId, decision) => {
    try {
      setApprovalError('');
      setReviewingApproval(approvalId);
      if (decision === 'approve') {
        await communicationService.approve(approvalId);
      } else {
        await communicationService.reject(approvalId);
      }
      await Promise.all([refreshApprovals(), refreshCommunications()]);
    } catch (reviewError) {
      setApprovalError(reviewError.response?.data?.message || 'Unable to update the approval request.');
    } finally {
      setReviewingApproval(null);
    }
  };

  const getSelectedUser = () => users.find((u) => String(u.userId ?? u.id) === recipient);
  const getSelectedDepartment = () => departments.find((d) => String(d.departmentId ?? d.id) === recipient);
  const getSelectedUsers = () => users.filter((u) => selectedRecipients.includes(String(u.userId ?? u.id)));
  const ccEmails = cc.split(',').map((item) => item.trim()).filter(Boolean);

  const handleTemplateChange = (templateKey) => {
    const template = templatesList.find((t) => (t.key || t.value) === templateKey);
    setSelectedTemplate(templateKey);
    if (template) {
      setSubject((prev) => prev || template.subject || '');
      setMessage((prev) => prev || template.body || '');
    }
  };

  const handleGetRecommendation = async () => {
    try {
      setRecommending(true);
      setError('');
      const selectedUser = getSelectedUser();
      const selectedDepartment = getSelectedDepartment();
      const multiUsers = getSelectedUsers();
      const { data } = await communicationService.getRecommendation({
        recipientType,
        type: commType,
        communicationType: commType,
        recipientUserId: recipientType === 'Individual' ? (selectedUser?.userId ?? selectedUser?.id ?? null) : null,
        recipientUserIds: recipientType === 'Multiple Employees' ? multiUsers.map((u) => u.userId ?? u.id) : [],
        recipientDepartmentId: recipientType === 'Department' ? (selectedDepartment?.departmentId ?? selectedDepartment?.id ?? null) : null,
        recipient: selectedUser?.fullName || selectedUser?.name || selectedDepartment?.departmentName || selectedDepartment?.name || 'All Employees',
        templateKey: selectedTemplate || null,
        subject,
        body: message,
        prompt,
      });
      setRecommendation(data);
    } catch (err) {
      console.error('Failed to get recommendation:', err);
      setError(err.response?.data?.message || 'Failed to generate AI recommendation.');
    } finally {
      setRecommending(false);
    }
  };

  const applyRecommendation = () => {
    if (!recommendation) return;
    setSubject(recommendation.subject || '');
    setMessage(recommendation.body || '');
    setCc((recommendation.cc || []).join(', '));
  };

  const handleSend = async () => {
    try {
      setSubmitting(true);
      setError('');
      const selectedUser = getSelectedUser();
      const selectedDepartment = getSelectedDepartment();
      const multiUsers = getSelectedUsers();

      await communicationService.createCommunication({
        recipientType,
        type: commType,
        communicationType: commType,
        recipientUserId: recipientType === 'Individual' ? (selectedUser?.userId ?? selectedUser?.id ?? null) : null,
        recipientUserIds: recipientType === 'Multiple Employees' ? multiUsers.map((u) => u.userId ?? u.id) : [],
        recipientDepartmentId: recipientType === 'Department' ? (selectedDepartment?.departmentId ?? selectedDepartment?.id ?? null) : null,
        recipient: selectedUser?.fullName || selectedUser?.name || selectedDepartment?.departmentName || selectedDepartment?.name || 'All Employees',
        subject,
        body: message,
        ccEmails,
        recommendationUsed: Boolean(recommendation),
        idempotencyKey: idempotencyKeyRef.current,
      });

      idempotencyKeyRef.current = crypto.randomUUID();
      await refreshCommunications();
      setStats((prev) => ({
        ...prev,
        pendingApproval: (prev.pendingApproval || 0) + 1,
      }));
      setSent(true);
      setSubject('');
      setMessage('');
      setPrompt('');
      setCc('');
      setRecipient('');
      setSelectedRecipients([]);
      setSelectedTemplate('');
      setRecommendation(null);
    } catch (err) {
      console.error('Failed to send communication:', err);
      setError(err.response?.data?.message || 'Failed to send communication.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return (
    <AppLayout title="Communication Center">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  const canSend = subject.trim() && message.trim() && (
    recipientType === 'Company' ||
    (recipientType === 'Individual' && recipient) ||
    (recipientType === 'Department' && recipient) ||
    (recipientType === 'Multiple Employees' && selectedRecipients.length > 0)
  );

  return (
    <AppLayout title="Communication Center">
      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          <Card>
            <div className="flex items-start justify-between gap-4 mb-4">
              <div>
                <h3 className="text-lg font-semibold text-text-primary">Compose Message</h3>
                <p className="text-sm text-text-secondary mt-1">
                  {agentInfo?.enabled
                    ? 'AI email recommendations are available. Messages are sent only after an authorized approval.'
                    : 'Compose a message for approval. Outbound delivery remains disabled until an authorized reviewer approves it.'}
                </p>
              </div>
              <Badge variant={agentInfo?.enabled ? 'success' : 'warning'}>
                {agentInfo?.enabled ? 'Agent 5 Online' : 'Agent 5 Draft Mode'}
              </Badge>
            </div>

            {sent ? (
              <div className="text-center py-8">
                <Send size={40} className="text-success mx-auto mb-3" />
                <h4 className="text-lg font-medium text-text-primary mb-2">Submitted for Approval</h4>
                <p className="text-sm text-text-secondary mb-4">Your message is waiting for review. It will not be emailed until a different CEO or Admin approves it.</p>
                <Button onClick={() => setSent(false)}>Compose Another</Button>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Recipient Type</label>
                    <div className="flex flex-wrap gap-2">
                      {['Individual', 'Multiple Employees', 'Department', 'Company'].map((type) => (
                        <button
                          key={type}
                          onClick={() => { setRecipientType(type); setRecipient(''); setSelectedRecipients([]); }}
                          className={`px-3 py-2 rounded-lg text-sm ${recipientType === type ? 'bg-orange text-text-primary' : 'bg-bg border border-border text-text-secondary'}`}
                        >
                          {type}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Communication Type</label>
                    <div className="flex gap-2">
                      {['Email', 'Warning', 'Announcement'].map((type) => (
                        <button
                          key={type}
                          onClick={() => setCommType(type)}
                          className={`px-3 py-2 rounded-lg text-sm ${commType === type ? 'bg-orange text-text-primary' : 'bg-bg border border-border text-text-secondary'}`}
                        >
                          {type}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>

                {recipientType === 'Individual' && (
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Select Employee</label>
                    <select value={recipient} onChange={(e) => setRecipient(e.target.value)} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none">
                      <option value="">Select...</option>
                      {users.map((user) => <option key={user.userId ?? user.id} value={user.userId ?? user.id}>{user.fullName ?? `${user.firstName || ''} ${user.lastName || ''}`.trim()}</option>)}
                    </select>
                  </div>
                )}

                {recipientType === 'Multiple Employees' && (
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Select Employees</label>
                    <select
                      multiple
                      value={selectedRecipients}
                      onChange={(e) => setSelectedRecipients(Array.from(e.target.selectedOptions).map((option) => option.value))}
                      className="w-full min-h-[150px] bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                    >
                      {users.map((user) => <option key={user.userId ?? user.id} value={String(user.userId ?? user.id)}>{user.fullName ?? `${user.firstName || ''} ${user.lastName || ''}`.trim()}</option>)}
                    </select>
                    <p className="text-xs text-text-muted mt-2 flex items-center gap-2"><Users size={12} /> Hold Ctrl/Cmd to choose multiple employees.</p>
                  </div>
                )}

                {recipientType === 'Department' && (
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Select Department</label>
                    <select value={recipient} onChange={(e) => setRecipient(e.target.value)} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none">
                      <option value="">Select...</option>
                      {departments.map((department) => <option key={department.departmentId ?? department.id} value={department.departmentId ?? department.id}>{department.departmentName ?? department.name}</option>)}
                    </select>
                  </div>
                )}

                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">Template</label>
                  <select
                    value={selectedTemplate}
                    onChange={(e) => handleTemplateChange(e.target.value)}
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none"
                  >
                    {templatesList.map((template) => <option key={template.key || template.value} value={template.key || template.value}>{template.label}</option>)}
                  </select>
                </div>

                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">What should the email say?</label>
                  <textarea
                    value={prompt}
                    onChange={(e) => setPrompt(e.target.value)}
                    rows={3}
                    placeholder="Describe what you want the email to communicate. Agent 5 will use this plus the template, communication type, and recipient type."
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
                  />
                </div>

                <div className="grid md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">Subject</label>
                    <input
                      value={subject}
                      onChange={(e) => setSubject(e.target.value)}
                      className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                      placeholder="Enter subject..."
                    />
                  </div>
                  <div>
                    <label className="block text-sm text-text-secondary mb-1.5">CC</label>
                    <input
                      value={cc}
                      onChange={(e) => setCc(e.target.value)}
                      className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                      placeholder="name@company.com, teamlead@company.com"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">Message</label>
                  <textarea
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                    rows={8}
                    placeholder="Write your message..."
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
                  />
                </div>

                {recommendation && (
                  <div className="p-4 rounded-lg border border-purple-500/20 bg-purple-500/5">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <p className="text-sm font-semibold text-text-primary flex items-center gap-2"><Sparkles size={15} className="text-purple-400" /> AI Recommendation</p>
                        <p className="text-xs text-text-muted mt-1">{recommendation.rationale || 'Review the generated subject, recipients, CC list, and email body below.'}</p>
                      </div>
                      <Button variant="secondary" onClick={applyRecommendation}>Apply</Button>
                    </div>
                    <div className="mt-4 space-y-3">
                      <div className="p-3 rounded-lg bg-bg border border-border/50">
                        <p className="text-xs text-text-muted mb-1">Suggested To</p>
                        <p className="text-sm text-text-primary">{(recommendation.send_to || []).join(', ') || 'No explicit recipient emails returned.'}</p>
                      </div>
                      <div className="p-3 rounded-lg bg-bg border border-border/50">
                        <p className="text-xs text-text-muted mb-1">Suggested CC</p>
                        <p className="text-sm text-text-primary">{(recommendation.cc || []).join(', ') || 'No CC recommended.'}</p>
                      </div>
                      <div className="p-3 rounded-lg bg-bg border border-border/50">
                        <p className="text-xs text-text-muted mb-1">Suggested Subject</p>
                        <p className="text-sm text-text-primary">{recommendation.subject}</p>
                      </div>
                      <div className="p-3 rounded-lg bg-bg border border-border/50">
                        <p className="text-xs text-text-muted mb-1">Suggested Body</p>
                        <pre className="whitespace-pre-wrap text-sm text-text-secondary font-sans">{recommendation.body}</pre>
                      </div>
                    </div>
                  </div>
                )}

                {error && <p className="text-sm text-danger">{error}</p>}

                <div className="flex items-center gap-3 flex-wrap">
                  <Button onClick={handleSend} disabled={submitting || !canSend}>
                    <span className="flex items-center gap-2"><Send size={16} /> {submitting ? 'Submitting...' : 'Submit for Approval'}</span>
                  </Button>
                  <Button variant="secondary" onClick={handleGetRecommendation} disabled={recommending}>
                    <span className="flex items-center gap-2"><Sparkles size={16} /> {recommending ? 'Generating...' : 'AI Recommendation'}</span>
                  </Button>
                  <Button variant="secondary" disabled>
                    <span className="flex items-center gap-2"><Clock size={16} /> Schedule</span>
                  </Button>
                </div>
              </div>
            )}
          </Card>
        </div>

        <div className="space-y-4">
          {canApprove && (
            <Card>
              <div className="flex items-center gap-2 mb-3">
                <ShieldCheck size={16} className="text-orange" />
                <h3 className="text-base font-semibold text-text-primary">Pending Approvals</h3>
                <Badge variant="warning">{approvals.length}</Badge>
              </div>
              {approvalError && <p className="text-sm text-danger mb-3">{approvalError}</p>}
              {approvals.length === 0 ? (
                <p className="text-sm text-text-muted">No communications are waiting for review.</p>
              ) : (
                <div className="space-y-3">
                  {approvals.map(approval => (
                    <div key={approval.approvalId} className="p-3 rounded-lg border border-border bg-bg">
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="text-sm font-medium text-text-primary">{approval.subject || 'Communication request'}</p>
                          <p className="text-xs text-text-muted mt-1">
                            Requested by {approval.requestedByName || 'Company user'} · {approval.recipientCount} recipient(s)
                          </p>
                          <p className="text-xs text-text-muted mt-1">
                            {approval.requestedAt ? new Date(approval.requestedAt).toLocaleString() : ''}
                          </p>
                          {approval.preview && (
                            <p className="text-xs text-text-secondary mt-2 whitespace-pre-wrap line-clamp-3">{approval.preview}</p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center justify-end gap-2 mt-3">
                        <Button
                          variant="secondary"
                          disabled={reviewingApproval === approval.approvalId}
                          onClick={() => handleReviewApproval(approval.approvalId, 'reject')}
                        >
                          <span className="inline-flex items-center gap-1"><XCircle size={14} /> Reject</span>
                        </Button>
                        <Button
                          disabled={reviewingApproval === approval.approvalId}
                          onClick={() => handleReviewApproval(approval.approvalId, 'approve')}
                        >
                          {reviewingApproval === approval.approvalId ? 'Reviewing...' : 'Approve & Queue'}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </Card>
          )}

          <Card>
            <h3 className="text-base font-semibold text-text-primary mb-3">Communication History</h3>
            <div className="space-y-3">
              {communications.map((communication) => (
                <div key={communication.communicationId ?? communication.id} className="p-3 bg-bg rounded-lg border border-border/50">
                  <div className="flex items-center justify-between mb-1">
                    <Badge variant={communication.type === 'Warning' ? 'danger' : communication.type === 'Announcement' ? 'info' : 'default'}>{communication.type}</Badge>
                    <Badge variant={communication.status === 'Sent' ? 'success' : 'warning'}>{communication.status}</Badge>
                  </div>
                  <p className="text-sm font-medium text-text-primary mt-1">{communication.subject}</p>
                  <p className="text-xs text-text-muted mt-1">To: {communication.recipient || communication.recipientType} • {communication.date ? new Date(communication.date).toLocaleDateString() : communication.sentAt ? new Date(communication.sentAt).toLocaleDateString() : '—'}</p>
                </div>
              ))}
            </div>
          </Card>

          <Card>
            <h3 className="text-base font-semibold text-text-primary mb-3">Quick Stats</h3>
            <div className="grid grid-cols-2 gap-3">
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-orange">{stats.totalSent || communications.length}</p>
                <p className="text-xs text-text-muted">Total Sent</p>
              </div>
              <div className="p-3 bg-bg rounded-lg text-center">
                <p className="text-lg font-bold text-warning">{stats.pendingApproval || 0}</p>
                <p className="text-xs text-text-muted">Pending Approval</p>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-2 mb-3">
              <Mail size={16} className="text-orange" />
              <h3 className="text-base font-semibold text-text-primary">Agent 5</h3>
            </div>
            <p className="text-sm text-text-muted">
              Agent 5 uses the selected communication type, recipient type, template, and your prompt to recommend the email subject, body, send-to list, and CC list.
            </p>
            {recommendation && (
              <div className="mt-3 flex items-center gap-2 text-sm text-success">
                <CheckCircle2 size={14} />
                Recommendation ready to apply
              </div>
            )}
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
