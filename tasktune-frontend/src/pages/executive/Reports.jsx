import { useState, useEffect, useRef, useCallback } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, Button, Badge, Select, ProgressBar } from '../../components/ui';
import { reportService } from '../../services/reportService';
import { departmentService } from '../../services/departmentService';
import {
  Download, Brain, Sparkles, FileText, Users, AlertTriangle,
  Activity, TrendingUp, Clock, CheckCircle, XCircle, Loader2,
  ChevronRight, Shield, BarChart3, PieChart as PieChartIcon,
  LineChart as LineChartIcon,
} from 'lucide-react';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, RadarChart, Radar, PolarGrid,
  PolarAngleAxis, PolarRadiusAxis, LineChart, Line, Legend,
  AreaChart, Area,
} from 'recharts';

const reportTypes = [
  { value: 'performance', label: 'Performance Report' },
  { value: 'department', label: 'Department Report' },
  { value: 'project', label: 'Project Report' },
  { value: 'user', label: 'User Report' },
];

const HEALTH_CONFIG = {
  good:     { color: '#10B981', bg: 'rgba(16,185,129,0.1)', border: 'rgba(16,185,129,0.2)', icon: CheckCircle, label: 'HEALTHY' },
  warning:  { color: '#F59E0B', bg: 'rgba(245,158,11,0.1)', border: 'rgba(245,158,11,0.2)', icon: AlertTriangle, label: 'WARNING' },
  critical: { color: '#EF4444', bg: 'rgba(239,68,68,0.1)',   border: 'rgba(239,68,68,0.2)',  icon: XCircle, label: 'CRITICAL' },
};

const PIE_COLORS = ['#F97316', '#3B82F6', '#10B981', '#8B5CF6', '#EF4444', '#F59E0B', '#06B6D4', '#EC4899'];
const LINE_COLORS = ['#F97316', '#3B82F6', '#10B981', '#8B5CF6'];

function normalizeReportPayload(payload) {
  if (!payload) return null;
  const raw = payload.metadata && !payload.total_tasks
    ? {
        ...payload.metadata,
        reportId: payload.reportId,
        generatedAt: payload.generatedAt,
        generatedByName: payload.generatedByName,
        title: payload.title || payload.metadata?.title,
        report_type: payload.metadata?.report_type || payload.reportTypeName?.toLowerCase(),
      }
    : payload;

  return {
    ...raw,
    reportId: raw.reportId ?? payload.reportId,
    title: raw.title || payload.title || 'AI Report',
    report_type: raw.report_type || payload.reportTypeName?.toLowerCase() || payload.reportType || 'report',
    generated_at: raw.generated_at || payload.generatedAt || raw.generatedAt,
  };
}

/* ── Determine the best chart types based on report data ── */
function determineBestCharts(report) {
  const charts = [];
  const empCount = report?.employees_summary?.length || 0;
  const hasChartData = (report?.chart_data?.length || 0) > 0;
  const reportType = report?.report_type || 'performance';

  // Bar chart: good for comparing employees/departments
  if (hasChartData || empCount > 0) {
    charts.push('bar');
  }

  // Pie chart: good for distribution/composition (task status, department breakdown)
  if (empCount > 1 && empCount <= 12) {
    charts.push('pie');
  }

  // Radar chart: good for multi-dimensional comparison (performance report)
  if (reportType === 'performance' && empCount > 1 && empCount <= 8) {
    charts.push('radar');
  }

  // Line/Area chart: good for trends over time (if chart_data has time series)
  if (reportType === 'project' || reportType === 'department') {
    charts.push('area');
  }

  // Default to at least bar + pie
  if (charts.length === 0) charts.push('bar', 'pie');

  return charts;
}

/* ── Build chart-specific data ── */
function buildChartData(report) {
  const empSummary = report?.employees_summary || [];
  const chartData = report?.chart_data || [];

  // Bar chart data
  const barData = chartData.length > 0
    ? chartData
    : empSummary.map(e => ({
        dept: e.employee_name?.split(' ')[0] || 'Unknown',
        score: e.avg_completion || 0,
        tasks: e.total_tasks || 0,
        late: e.late_tasks || 0,
      }));

  // Pie chart - task distribution by employee
  const pieData = empSummary.map(e => ({
    name: e.employee_name?.split(' ')[0] || 'Unknown',
    value: e.total_tasks || 0,
  }));

  // Status pie chart
  const statusCounts = {};
  empSummary.forEach(e => {
    if (e.at_risk) {
      statusCounts['At Risk'] = (statusCounts['At Risk'] || 0) + 1;
    } else {
      statusCounts['On Track'] = (statusCounts['On Track'] || 0) + 1;
    }
  });
  const statusPieData = Object.entries(statusCounts).map(([name, value]) => ({ name, value }));

  // Radar data
  const radarData = empSummary.slice(0, 8).map(e => ({
    name: e.employee_name?.split(' ')[0] || 'Unknown',
    completion: e.avg_completion || 0,
    onTime: e.total_tasks > 0 ? Math.round((e.on_time_tasks / e.total_tasks) * 100) : 0,
    tasks: e.total_tasks || 0,
  }));

  // Area/Line data (completion over employees)
  const areaData = empSummary.map(e => ({
    name: e.employee_name?.split(' ')[0] || 'Unknown',
    completion: e.avg_completion || 0,
    onTimePct: e.total_tasks > 0 ? Math.round((e.on_time_tasks / e.total_tasks) * 100) : 0,
    latePct: e.total_tasks > 0 ? Math.round((e.late_tasks / e.total_tasks) * 100) : 0,
  }));

  return { barData, pieData, statusPieData, radarData, areaData };
}

/* ── Tooltip style shared across charts ── */
const tooltipStyle = {
  background: '#1F2937', border: '1px solid #374151',
  borderRadius: 10, fontSize: 12, boxShadow: '0 10px 25px rgba(0,0,0,0.5)',
};

export default function Reports() {
  const [reports, setReports] = useState([]);
  const [loading, setLoading] = useState(true);
  const [type, setType] = useState('performance');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [department, setDepartment] = useState('All Departments');
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState('');
  const [agentOnline, setAgentOnline] = useState(null);
  const [currentReport, setCurrentReport] = useState(null);
  const [generationStep, setGenerationStep] = useState('');
  const reportRef = useRef(null);
  const [departments, setDepartments] = useState([]);
  const [pdfExporting, setPdfExporting] = useState(false);

  // Check agent health + load departments on mount
  useEffect(() => {
    const init = async () => {
      const online = await reportService.checkAgentHealth();
      setAgentOnline(online);
      try {
        const { data } = await departmentService.getDepartments();
        const items = Array.isArray(data) ? data : data?.items || [];
        setDepartments(items.map(d => d.departmentName || d.name || '').filter(Boolean));
      } catch { /* ignore */ }
    };
    init();
  }, []);

  // Load existing reports
  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data } = await reportService.getReports();
        const items = Array.isArray(data) ? data : data?.items ?? [];
        setReports(items);
      } catch (err) {
        console.error('Failed to load reports:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  // Generate report via Agent 4
  const handleGenerate = useCallback(async () => {
    setGenerating(true);
    setError('');
    setCurrentReport(null);

    const steps = [
      '🔍 Fetching task data...',
      '✅ Validating data integrity...',
      '🤖 Running AI analysis...',
      '📝 Building report structure...',
      '📧 Processing email notifications...',
      '📄 Preparing PDF-ready report...',
    ];

    // Simulate progress steps
    let stepIdx = 0;
    const interval = setInterval(() => {
      if (stepIdx < steps.length) {
        setGenerationStep(steps[stepIdx]);
        stepIdx++;
      }
    }, 2500);

    try {
      const { data } = await reportService.generateAIReport({
        reportType: type,
        startDate,
        endDate,
        department,
      });

      clearInterval(interval);
      setGenerationStep('✅ Report generated successfully!');
      setCurrentReport(normalizeReportPayload(data));
      setTimeout(() => setGenerationStep(''), 2000);

      // Refresh report list
      try {
        const { data: listData } = await reportService.getReports();
        const items = Array.isArray(listData) ? listData : listData?.items ?? [];
        setReports(items);
      } catch { /* ignore */ }
    } catch (err) {
      clearInterval(interval);
      setGenerationStep('');
      setError(
        err.response?.data?.detail ||
        err.response?.data?.message ||
        'Failed to generate report. Please try again later.'
      );
    } finally {
      setGenerating(false);
    }
  }, [type, startDate, endDate, department]);

  // Export to PDF using jsPDF + html2canvas
  const handleExportPDF = useCallback(async () => {
    if (!reportRef.current) {
      setError('No report content to export. Please generate a report first.');
      return;
    }
    setPdfExporting(true);
    setError('');
    try {
      const html2canvas = (await import('html2canvas')).default;
      const { jsPDF } = await import('jspdf');

      const element = reportRef.current;

      // Temporarily expand the element for capture
      const originalOverflow = element.style.overflow;
      const originalMaxHeight = element.style.maxHeight;
      element.style.overflow = 'visible';
      element.style.maxHeight = 'none';

      const canvas = await html2canvas(element, {
        scale: 2,
        backgroundColor: document.documentElement.getAttribute('data-theme') === 'light' ? '#F5F5F5' : '#0a0a0f',
        useCORS: true,
        logging: false,
        allowTaint: true,
        windowWidth: element.scrollWidth,
        windowHeight: element.scrollHeight,
      });

      // Restore original styles
      element.style.overflow = originalOverflow;
      element.style.maxHeight = originalMaxHeight;

      const imgData = canvas.toDataURL('image/png');
      const pdf = new jsPDF('p', 'mm', 'a4');
      const pdfWidth = pdf.internal.pageSize.getWidth();
      const pdfHeight = pdf.internal.pageSize.getHeight();
      const imgWidth = canvas.width;
      const imgHeight = canvas.height;
      const ratio = Math.min(pdfWidth / imgWidth, pdfHeight / imgHeight);
      const imgX = (pdfWidth - imgWidth * ratio) / 2;

      let heightLeft = imgHeight * ratio;
      let position = 0;

      // First page
      pdf.addImage(imgData, 'PNG', imgX, position, imgWidth * ratio, imgHeight * ratio);
      heightLeft -= pdfHeight;

      // Additional pages
      while (heightLeft > 0) {
        position = heightLeft - imgHeight * ratio;
        pdf.addPage();
        pdf.addImage(imgData, 'PNG', imgX, position, imgWidth * ratio, imgHeight * ratio);
        heightLeft -= pdfHeight;
      }

      const fileName = currentReport?.title
        ? `${currentReport.title.replace(/[^a-zA-Z0-9]/g, '_')}.pdf`
        : `TaskTune_Report_${new Date().toISOString().split('T')[0]}.pdf`;

      pdf.save(fileName);
    } catch (err) {
      console.error('PDF export failed:', err);
      setError(`PDF export failed: ${err.message || 'Unknown error'}. Try using a smaller report.`);
    } finally {
      setPdfExporting(false);
    }
  }, [currentReport]);

  const analysis = currentReport?.ai_analysis;
  const healthCfg = analysis ? HEALTH_CONFIG[analysis.overall_health] || HEALTH_CONFIG.good : null;

  // Build adaptive chart data
  const bestCharts = currentReport ? determineBestCharts(currentReport) : [];
  const chartDataSets = currentReport ? buildChartData(currentReport) : {};

  // Report type title
  const reportTypeLabel = reportTypes.find(r => r.value === (currentReport?.report_type || type))?.label || 'Report';
  const reportDateRange = [
    currentReport?.start_date || startDate,
    currentReport?.end_date || endDate,
  ].filter(Boolean).join(' — ');

  if (loading) return (
    <AppLayout title="Reports">
      <div className="flex items-center justify-center h-64">
        <Loader2 size={32} className="text-orange animate-spin" />
      </div>
    </AppLayout>
  );

  return (
    <AppLayout title="AI Reports">
      <div className="grid lg:grid-cols-3 gap-6">
        {/* ═══ LEFT: MAIN CONTENT ═══ */}
        <div className="lg:col-span-2 space-y-5">

          {/* Generate Report Card */}
          <Card>
            <div className="flex items-center gap-2 mb-4">
              <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: 'linear-gradient(135deg, #F97316, #F59E0B)' }}>
                <Sparkles size={16} className="text-white" />
              </div>
              <div>
                <h3 className="text-lg font-semibold text-text-primary">Generate AI Report</h3>
                <p className="text-xs text-text-muted">Select report type, dates, and department to generate a tailored AI report.</p>
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
              <Select
                label="Report Type"
                options={reportTypes}
                value={type}
                onChange={e => setType(e.target.value)}
              />
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Start Date</label>
                <input
                  type="date"
                  value={startDate}
                  onChange={e => setStartDate(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                />
              </div>
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">End Date</label>
                <input
                  type="date"
                  value={endDate}
                  onChange={e => setEndDate(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                />
              </div>
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Department</label>
                <select
                  value={department}
                  onChange={e => setDepartment(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none"
                >
                  <option>All Departments</option>
                  {departments.map(d => (
                    <option key={d} value={d}>{d}</option>
                  ))}
                </select>
              </div>
            </div>

            {error && (
              <div className="flex items-center gap-2 p-3 mb-3 rounded-lg border"
                style={{ background: 'rgba(239,68,68,0.05)', borderColor: 'rgba(239,68,68,0.15)' }}>
                <XCircle size={14} className="text-danger shrink-0" />
                <p className="text-sm text-danger">{error}</p>
              </div>
            )}

            {generationStep && (
              <div className="flex items-center gap-3 p-3 mb-3 rounded-lg border"
                style={{ background: 'rgba(249,115,22,0.05)', borderColor: 'rgba(249,115,22,0.15)' }}>
                <Loader2 size={14} className="text-orange animate-spin shrink-0" />
                <p className="text-sm text-orange font-medium">{generationStep}</p>
              </div>
            )}

            <div className="flex items-center gap-3">
              <Button onClick={handleGenerate} disabled={generating}>
                <span className="flex items-center gap-2">
                  {generating ? <Loader2 size={16} className="animate-spin" /> : <Brain size={16} />}
                  {generating ? 'AI Agent Working...' : 'Generate Report with AI'}
                </span>
              </Button>

              {currentReport && (
                <Button variant="secondary" onClick={handleExportPDF} disabled={pdfExporting}>
                  <span className="flex items-center gap-2">
                    {pdfExporting ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />}
                    {pdfExporting ? 'Exporting...' : 'Save as PDF'}
                  </span>
                </Button>
              )}
            </div>
          </Card>

          {/* ═══ REPORT OUTPUT ═══ */}
          {currentReport && (
            <div ref={reportRef} className="space-y-5" style={{ padding: '4px' }}>
              {/* Report Header */}
              <div className="rounded-xl border border-border bg-surface p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h2 className="text-lg font-bold text-text-primary">{currentReport.title || reportTypeLabel}</h2>
                    <p className="text-xs text-text-muted mt-1">
                      {reportTypeLabel}
                      {reportDateRange && ` • ${reportDateRange}`}
                      {department !== 'All Departments' && ` • ${department}`}
                      {currentReport.generated_at && ` • Generated ${new Date(currentReport.generated_at).toLocaleDateString()}`}
                    </p>
                  </div>
                  {healthCfg && (
                    <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg" style={{ background: healthCfg.bg, border: `1px solid ${healthCfg.border}` }}>
                      <healthCfg.icon size={14} style={{ color: healthCfg.color }} />
                      <span className="text-xs font-bold" style={{ color: healthCfg.color }}>{healthCfg.label}</span>
                    </div>
                  )}
                </div>
              </div>

              {currentReport.savedToDatabase === false && (
                <div className="rounded-xl border border-orange/20 bg-orange/5 p-4">
                  <p className="text-sm text-orange">This AI report was not saved to the database. Use <strong>Save as PDF</strong> to keep a copy.</p>
                </div>
              )}

              {/* KPI Cards */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="bg-surface border border-border rounded-xl p-4 relative overflow-hidden">
                  <div className="absolute top-0 left-0 right-0 h-0.5" style={{ background: 'linear-gradient(90deg, #F97316, transparent)' }} />
                  <div className="flex items-center gap-2 mb-2">
                    <FileText size={14} className="text-orange" />
                    <span className="text-xs text-text-muted uppercase tracking-wider">Total Tasks</span>
                  </div>
                  <p className="text-2xl font-bold text-text-primary">{currentReport.total_tasks ?? 0}</p>
                </div>
                <div className="bg-surface border border-border rounded-xl p-4 relative overflow-hidden">
                  <div className="absolute top-0 left-0 right-0 h-0.5" style={{ background: 'linear-gradient(90deg, #3B82F6, transparent)' }} />
                  <div className="flex items-center gap-2 mb-2">
                    <Users size={14} className="text-info" />
                    <span className="text-xs text-text-muted uppercase tracking-wider">Employees</span>
                  </div>
                  <p className="text-2xl font-bold text-text-primary">{currentReport.total_employees ?? 0}</p>
                </div>
                <div className="bg-surface border border-border rounded-xl p-4 relative overflow-hidden">
                  <div className="absolute top-0 left-0 right-0 h-0.5" style={{ background: `linear-gradient(90deg, ${currentReport.avg_completion >= 60 ? '#10B981' : '#F59E0B'}, transparent)` }} />
                  <div className="flex items-center gap-2 mb-2">
                    <TrendingUp size={14} style={{ color: currentReport.avg_completion >= 60 ? '#10B981' : '#F59E0B' }} />
                    <span className="text-xs text-text-muted uppercase tracking-wider">Avg Completion</span>
                  </div>
                  <p className="text-2xl font-bold text-text-primary">{currentReport.avg_completion ?? 0}%</p>
                </div>
                <div className="bg-surface border border-border rounded-xl p-4 relative overflow-hidden">
                  <div className="absolute top-0 left-0 right-0 h-0.5" style={{ background: `linear-gradient(90deg, ${healthCfg?.color || '#6B7280'}, transparent)` }} />
                  <div className="flex items-center gap-2 mb-2">
                    {healthCfg && <healthCfg.icon size={14} style={{ color: healthCfg.color }} />}
                    <span className="text-xs text-text-muted uppercase tracking-wider">Health</span>
                  </div>
                  <p className="text-lg font-bold" style={{ color: healthCfg?.color }}>
                    {healthCfg?.label || 'N/A'}
                  </p>
                </div>
              </div>

              {/* ═══ ADAPTIVE CHARTS ═══ */}
              <div className="grid md:grid-cols-2 gap-5">
                {/* Bar Chart — always shown if data exists */}
                {bestCharts.includes('bar') && chartDataSets.barData?.length > 0 && (
                  <Card>
                    <div className="flex items-center gap-2 mb-4">
                      <BarChart3 size={16} className="text-orange" />
                      <h4 className="text-sm font-semibold text-text-primary">
                        {type === 'department' ? 'Department Performance' : type === 'project' ? 'Project Completion' : 'Employee Performance'}
                      </h4>
                    </div>
                    <ResponsiveContainer width="100%" height={220}>
                      <BarChart data={chartDataSets.barData}>
                        <XAxis dataKey="dept" stroke="#4B5563" fontSize={11} tickLine={false} axisLine={false} />
                        <YAxis stroke="#4B5563" fontSize={11} tickLine={false} axisLine={false} />
                        <Tooltip contentStyle={tooltipStyle} cursor={{ fill: 'rgba(249,115,22,0.05)' }} />
                        <Bar dataKey="score" fill="url(#barGradient)" radius={[6, 6, 0, 0]} />
                        {chartDataSets.barData[0]?.late !== undefined && (
                          <Bar dataKey="late" fill="#EF4444" radius={[6, 6, 0, 0]} opacity={0.7} />
                        )}
                        <defs>
                          <linearGradient id="barGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#F97316" />
                            <stop offset="100%" stopColor="#F59E0B" />
                          </linearGradient>
                        </defs>
                      </BarChart>
                    </ResponsiveContainer>
                  </Card>
                )}

                {/* Pie Chart — Task Distribution */}
                {bestCharts.includes('pie') && chartDataSets.pieData?.length > 0 && (
                  <Card>
                    <div className="flex items-center gap-2 mb-4">
                      <Activity size={16} className="text-info" />
                      <h4 className="text-sm font-semibold text-text-primary">Task Distribution</h4>
                    </div>
                    <ResponsiveContainer width="100%" height={220}>
                      <PieChart>
                        <Pie
                          data={chartDataSets.pieData}
                          cx="50%" cy="50%"
                          innerRadius={50} outerRadius={80}
                          paddingAngle={4}
                          dataKey="value"
                        >
                          {chartDataSets.pieData.map((_, i) => (
                            <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                          ))}
                        </Pie>
                        <Tooltip contentStyle={tooltipStyle} />
                      </PieChart>
                    </ResponsiveContainer>
                    <div className="flex flex-wrap gap-3 justify-center mt-2">
                      {chartDataSets.pieData.map((d, i) => (
                        <div key={i} className="flex items-center gap-1.5">
                          <div className="w-2.5 h-2.5 rounded-full" style={{ background: PIE_COLORS[i % PIE_COLORS.length] }} />
                          <span className="text-xs text-text-muted">{d.name} ({d.value})</span>
                        </div>
                      ))}
                    </div>
                  </Card>
                )}

                {/* Radar Chart — Performance multi-dimensional */}
                {bestCharts.includes('radar') && chartDataSets.radarData?.length > 1 && (
                  <Card>
                    <div className="flex items-center gap-2 mb-4">
                      <Activity size={16} className="text-purple-400" />
                      <h4 className="text-sm font-semibold text-text-primary">Performance Radar</h4>
                    </div>
                    <ResponsiveContainer width="100%" height={220}>
                      <RadarChart data={chartDataSets.radarData}>
                        <PolarGrid stroke="#374151" />
                        <PolarAngleAxis dataKey="name" tick={{ fill: '#9CA3AF', fontSize: 11 }} />
                        <PolarRadiusAxis tick={{ fill: '#6B7280', fontSize: 10 }} />
                        <Radar name="Completion" dataKey="completion" stroke="#F97316" fill="#F97316" fillOpacity={0.2} />
                        <Radar name="On-Time %" dataKey="onTime" stroke="#3B82F6" fill="#3B82F6" fillOpacity={0.15} />
                        <Legend wrapperStyle={{ fontSize: 11 }} />
                      </RadarChart>
                    </ResponsiveContainer>
                  </Card>
                )}

                {/* Area Chart — Trend view */}
                {bestCharts.includes('area') && chartDataSets.areaData?.length > 1 && (
                  <Card>
                    <div className="flex items-center gap-2 mb-4">
                      <TrendingUp size={16} className="text-success" />
                      <h4 className="text-sm font-semibold text-text-primary">Completion Trends</h4>
                    </div>
                    <ResponsiveContainer width="100%" height={220}>
                      <AreaChart data={chartDataSets.areaData}>
                        <XAxis dataKey="name" stroke="#4B5563" fontSize={11} tickLine={false} axisLine={false} />
                        <YAxis stroke="#4B5563" fontSize={11} tickLine={false} axisLine={false} />
                        <Tooltip contentStyle={tooltipStyle} />
                        <defs>
                          <linearGradient id="areaGreen" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#10B981" stopOpacity={0.3} />
                            <stop offset="100%" stopColor="#10B981" stopOpacity={0} />
                          </linearGradient>
                          <linearGradient id="areaRed" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#EF4444" stopOpacity={0.3} />
                            <stop offset="100%" stopColor="#EF4444" stopOpacity={0} />
                          </linearGradient>
                        </defs>
                        <Area type="monotone" dataKey="completion" stroke="#10B981" fill="url(#areaGreen)" />
                        <Area type="monotone" dataKey="latePct" stroke="#EF4444" fill="url(#areaRed)" />
                        <Legend wrapperStyle={{ fontSize: 11 }} />
                      </AreaChart>
                    </ResponsiveContainer>
                  </Card>
                )}

                {/* Status Pie — Risk distribution */}
                {chartDataSets.statusPieData?.length > 0 && (
                  <Card>
                    <div className="flex items-center gap-2 mb-4">
                      <Shield size={16} className="text-warning" />
                      <h4 className="text-sm font-semibold text-text-primary">Risk Distribution</h4>
                    </div>
                    <ResponsiveContainer width="100%" height={220}>
                      <PieChart>
                        <Pie data={chartDataSets.statusPieData} cx="50%" cy="50%" innerRadius={55} outerRadius={80} paddingAngle={6} dataKey="value">
                          {chartDataSets.statusPieData.map((entry, i) => (
                            <Cell key={i} fill={entry.name === 'At Risk' ? '#EF4444' : '#10B981'} />
                          ))}
                        </Pie>
                        <Tooltip contentStyle={tooltipStyle} />
                      </PieChart>
                    </ResponsiveContainer>
                    <div className="flex flex-wrap gap-3 justify-center mt-2">
                      {chartDataSets.statusPieData.map((d, i) => (
                        <div key={i} className="flex items-center gap-1.5">
                          <div className="w-2.5 h-2.5 rounded-full" style={{ background: d.name === 'At Risk' ? '#EF4444' : '#10B981' }} />
                          <span className="text-xs text-text-muted">{d.name} ({d.value})</span>
                        </div>
                      ))}
                    </div>
                  </Card>
                )}
              </div>

              {/* Employee Summary Table */}
              {currentReport.employees_summary?.length > 0 && (
                <Card>
                  <div className="flex items-center gap-2 mb-4">
                    <Users size={16} className="text-orange" />
                    <h4 className="text-sm font-semibold text-text-primary">Employee Summary</h4>
                  </div>
                  <div className="space-y-3">
                    {currentReport.employees_summary.map((emp, i) => (
                      <div
                        key={emp.employee_id || i}
                        className="flex items-center gap-4 p-3 rounded-lg border transition-all duration-200 hover:border-orange/30"
                        style={{
                          background: emp.at_risk ? 'rgba(239,68,68,0.03)' : 'rgba(255,255,255,0.01)',
                          borderColor: emp.at_risk ? 'rgba(239,68,68,0.15)' : 'rgba(55,65,81,0.3)',
                        }}
                      >
                        <div className="w-9 h-9 rounded-lg flex items-center justify-center text-sm font-bold"
                          style={{
                            background: emp.at_risk ? 'rgba(239,68,68,0.1)' : 'rgba(249,115,22,0.1)',
                            color: emp.at_risk ? '#EF4444' : '#F97316',
                          }}>
                          {emp.employee_name.charAt(0)}
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2">
                            <p className="text-sm font-medium text-text-primary truncate">{emp.employee_name}</p>
                            <Badge variant={emp.at_risk ? 'danger' : 'success'}>
                              {emp.at_risk ? 'AT RISK' : 'ON TRACK'}
                            </Badge>
                          </div>
                          <div className="flex items-center gap-4 mt-1">
                            <span className="text-xs text-text-muted">{emp.total_tasks} tasks</span>
                            <span className="text-xs text-text-muted">{emp.on_time_tasks} on time</span>
                            {emp.late_tasks > 0 && (
                              <span className="text-xs text-danger">{emp.late_tasks} late</span>
                            )}
                          </div>
                        </div>
                        <div className="w-32">
                          <ProgressBar value={emp.avg_completion} />
                        </div>
                        <span className="text-sm font-bold w-12 text-right"
                          style={{ color: emp.avg_completion >= 70 ? '#10B981' : emp.avg_completion >= 40 ? '#F59E0B' : '#EF4444' }}>
                          {emp.avg_completion}%
                        </span>
                      </div>
                    ))}
                  </div>
                </Card>
              )}

              {/* AI Analysis Section */}
              {analysis && (
                <Card className="relative overflow-hidden">
                  <div className="absolute top-0 left-0 right-0 h-0.5"
                    style={{ background: 'linear-gradient(90deg, #F97316, #8B5CF6, #3B82F6)' }} />

                  <div className="flex items-center gap-2 mb-4 mt-1">
                    <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                      style={{ background: 'linear-gradient(135deg, #8B5CF6, #3B82F6)' }}>
                      <Brain size={16} className="text-white" />
                    </div>
                    <div>
                      <h4 className="text-sm font-semibold text-text-primary">AI Analysis & Insights</h4>
                      <p className="text-xs text-text-muted">Generated by Agent 4 using Llama 3.3 70B</p>
                    </div>
                  </div>

                  {/* Summary */}
                  <div className="p-4 rounded-lg border mb-4"
                    style={{ background: 'rgba(139,92,246,0.03)', borderColor: 'rgba(139,92,246,0.1)' }}>
                      <p className="text-sm text-text-secondary leading-relaxed">{analysis.summary}</p>
                    </div>

                  <div className="grid md:grid-cols-2 gap-4">
                    {/* At Risk Employees */}
                    {analysis.at_risk_employees?.length > 0 && (
                      <div>
                        <h5 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2 flex items-center gap-1.5">
                          <AlertTriangle size={12} className="text-danger" /> At-Risk Employees
                        </h5>
                        <div className="space-y-2">
                          {analysis.at_risk_employees.map((name, i) => (
                            <div key={i} className="flex items-center gap-2 p-2.5 rounded-lg"
                              style={{ background: 'rgba(239,68,68,0.05)', border: '1px solid rgba(239,68,68,0.1)' }}>
                              <XCircle size={12} className="text-danger shrink-0" />
                              <span className="text-sm text-text-primary">{name}</span>
                              {analysis.predictions?.[name] && (
                                <span className="text-xs text-text-muted ml-auto truncate max-w-[140px]">
                                  {analysis.predictions[name]}
                                </span>
                              )}
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Recommendations */}
                    {analysis.recommendations?.length > 0 && (
                      <div>
                        <h5 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2 flex items-center gap-1.5">
                          <Shield size={12} className="text-orange" /> Recommendations
                        </h5>
                        <div className="space-y-2">
                          {analysis.recommendations.map((rec, i) => (
                            <div key={i} className="flex items-start gap-2.5 p-2.5 rounded-lg"
                              style={{ background: 'rgba(249,115,22,0.03)', border: '1px solid rgba(249,115,22,0.1)' }}>
                              <span className="text-xs font-bold text-orange mt-0.5 shrink-0">{i + 1}</span>
                              <p className="text-sm text-text-secondary leading-relaxed">{rec}</p>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Predictions */}
                  {analysis.predictions && Object.keys(analysis.predictions).length > 0 && (
                    <div className="mt-4">
                      <h5 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2 flex items-center gap-1.5">
                        <Clock size={12} className="text-warning" /> Completion Predictions
                      </h5>
                      <div className="grid grid-cols-2 gap-2">
                        {Object.entries(analysis.predictions).map(([name, prediction], i) => (
                          <div key={i} className="flex items-center gap-2 p-2.5 rounded-lg"
                            style={{ background: 'rgba(245,158,11,0.03)', border: '1px solid rgba(245,158,11,0.1)' }}>
                            <ChevronRight size={12} className="text-warning shrink-0" />
                            <div className="min-w-0">
                              <p className="text-sm text-text-primary truncate">{name}</p>
                              <p className="text-xs text-text-muted truncate">{prediction}</p>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </Card>
              )}
            </div>
          )}

          {/* Empty state when no report */}
          {!currentReport && !generating && (
            <Card>
              <div className="flex flex-col items-center justify-center py-16">
                <div className="w-16 h-16 rounded-2xl flex items-center justify-center mb-4"
                  style={{ background: 'linear-gradient(135deg, rgba(249,115,22,0.1), rgba(139,92,246,0.1))' }}>
                  <Brain size={28} className="text-orange" />
                </div>
                <h3 className="text-lg font-semibold text-text-primary mb-2">Ready to Generate</h3>
                <p className="text-sm text-text-muted text-center max-w-sm">
                  Configure your report parameters above and click <strong className="text-orange">Generate Report with AI</strong> to
                  run the AI analysis and export the result as a PDF.
                </p>
              </div>
            </Card>
          )}
        </div>

        {/* ═══ RIGHT SIDEBAR ═══ */}
        <div className="space-y-5">

          {/* AI Insights Quick View */}
          {analysis && (
            <Card>
              <div className="flex items-center gap-2 mb-3">
                <Brain size={16} style={{ color: healthCfg?.color || '#F97316' }} />
                <h3 className="text-sm font-semibold text-text-primary">Quick Insights</h3>
              </div>
              <div
                className="p-3 rounded-lg border mb-3"
                style={{ background: healthCfg?.bg, borderColor: healthCfg?.border }}
              >
                <div className="flex items-center gap-2 mb-1">
                  {healthCfg && <healthCfg.icon size={14} style={{ color: healthCfg.color }} />}
                  <span className="text-xs font-bold" style={{ color: healthCfg?.color }}>
                    {healthCfg?.label}
                  </span>
                </div>
                <p className="text-xs text-text-secondary leading-relaxed line-clamp-3">
                  {analysis.summary?.substring(0, 150)}...
                </p>
              </div>

              {analysis.at_risk_employees?.length > 0 && (
                <div className="mb-3">
                  <p className="text-xs text-text-muted mb-1.5">⚠️ At Risk ({analysis.at_risk_employees.length})</p>
                  {analysis.at_risk_employees.map((name, i) => (
                    <p key={i} className="text-xs text-danger py-0.5">• {name}</p>
                  ))}
                </div>
              )}

              {currentReport.email_sent !== undefined && (
                <div className="flex items-center gap-2 text-xs">
                  {currentReport.email_sent ? (
                    <>
                      <CheckCircle size={12} className="text-success" />
                      <span className="text-success">Email notification sent</span>
                    </>
                  ) : (
                    <>
                      <XCircle size={12} className="text-text-muted" />
                      <span className="text-text-muted">Email not sent (SMTP not configured)</span>
                    </>
                  )}
                </div>
              )}
            </Card>
          )}

          {/* Report History */}
          <Card>
            <h3 className="text-sm font-semibold text-text-primary mb-3">Report History</h3>
            <div className="space-y-2">
              {reports.length === 0 ? (
                <p className="text-xs text-text-muted py-4 text-center">No reports yet. Generate your first AI report.</p>
              ) : (
                reports.slice(0, 8).map((r, i) => (
                  <button
                    key={r.reportId || r.id || i}
                    onClick={async () => {
                      try {
                        const { data } = await reportService.getReport(r.reportId || r.id);
                        setCurrentReport(normalizeReportPayload(data));
                      } catch (err) {
                        console.error('Failed to load report history item:', err);
                      }
                    }}
                    className={`w-full text-left flex items-center justify-between p-2.5 bg-bg rounded-lg border transition-colors ${
                      (currentReport?.reportId && (r.reportId || r.id) === currentReport.reportId)
                        ? 'border-orange/40'
                        : 'border-border/50 hover:border-border'
                    }`}
                  >
                    <div className="min-w-0">
                      <p className="text-xs text-text-primary truncate">{r.name || r.title || `Report ${i + 1}`}</p>
                      <p className="text-[10px] text-text-muted">
                        {r.generatedAt ? new Date(r.generatedAt).toLocaleDateString() : r.date ? new Date(r.date).toLocaleDateString() : r.createdAt ? new Date(r.createdAt).toLocaleDateString() : '—'}
                      </p>
                    </div>
                    <Badge>{r.reportTypeName || r.type || r.reportType || 'Report'}</Badge>
                  </button>
                ))
              )}
            </div>
          </Card>

          {/* Export Actions */}
          {currentReport && (
            <Card>
              <h3 className="text-sm font-semibold text-text-primary mb-3">Export Options</h3>
              <div className="space-y-2">
                <Button variant="secondary" className="w-full" onClick={handleExportPDF} disabled={pdfExporting}>
                  <span className="flex items-center gap-2 justify-center">
                    {pdfExporting ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />}
                    {pdfExporting ? 'Exporting PDF...' : 'Download PDF Report'}
                  </span>
                </Button>
              </div>
            </Card>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
