import React, { useState, useEffect } from 'react';
import { Shield, Activity, FileText, Database, Layers, CheckCircle2, XCircle, AlertCircle, RefreshCw } from 'lucide-react';
import api from '../services/api';
import { toLocalDateTime } from '../utils/timezone';

const AdminDashboard = () => {
  const [health, setHealth] = useState(null);
  const [auditLogs, setAuditLogs] = useState([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchAdminData = async () => {
    try {
      setError('');
      // Fetch health checks directly using backend url to prevent proxy mismatch
      // and fetch audit logs via api service
      const [healthRes, logsRes] = await Promise.all([
        fetch('http://localhost:5209/health').then(r => r.json()),
        api.get('/api/v1/auditlogs')
      ]);

      setHealth(healthRes);
      setAuditLogs(logsRes.data);
    } catch (err) {
      setError('Failed to retrieve administrative diagnostics or audit logs.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchAdminData();
  }, []);

  const handleRefresh = () => {
    setRefreshing(true);
    fetchAdminData();
  };

  if (loading) {
    return <div style={{ textAlign: 'center', marginTop: '40px' }}>Loading Administration Dashboard...</div>;
  }

  // Calculate statistics from audit logs
  const actionCounts = auditLogs.reduce((acc, log) => {
    acc[log.action] = (acc[log.action] || 0) + 1;
    return acc;
  }, {});

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <h2 style={{ fontSize: '24px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Shield className="logo-icon" />
          Admin Panel
        </h2>
        <button className="btn btn-secondary btn-sm" onClick={handleRefresh} disabled={refreshing}>
          <RefreshCw size={16} className={refreshing ? 'animate-spin' : ''} />
          Refresh Panel
        </button>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Top Section: Health Check and Usage Stats */}
      <div className="dashboard-grid">
        {/* Component Health Check Status */}
        <div className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">
              <Activity size={18} />
              System Diagnostics
            </h3>
            {health && (
              <span className={`badge ${health.status === 'Healthy' ? 'badge-success' : 'badge-danger'}`}>
                System: {health.status}
              </span>
            )}
          </div>

          {health ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {health.checks?.map(check => {
                const isCheckHealthy = check.status === 'Healthy';
                return (
                  <div 
                    key={check.component} 
                    style={{ 
                      display: 'flex', 
                      justifyContent: 'space-between', 
                      alignItems: 'center',
                      padding: '14px 18px',
                      borderRadius: 'var(--radius-sm)',
                      border: '1px solid var(--border)',
                      backgroundColor: 'var(--bg-main)'
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                      {check.component === 'sqlserver' && <Database size={20} style={{ color: 'var(--primary)' }} />}
                      {check.component === 'redis' && <Layers size={20} style={{ color: 'var(--secondary)' }} />}
                      {check.component === 'hangfire' && <Activity size={20} style={{ color: 'var(--info)' }} />}
                      <div>
                        <strong style={{ textTransform: 'capitalize', fontSize: '15px' }}>
                          {check.component === 'sqlserver' ? 'SQL Server Database' : check.component}
                        </strong>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                          {check.description || 'Active connection verified.'}
                        </p>
                      </div>
                    </div>
                    {isCheckHealthy ? (
                      <CheckCircle2 size={24} style={{ color: 'var(--secondary)' }} />
                    ) : (
                      <XCircle size={24} style={{ color: 'var(--danger)' }} />
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Diagnostics unavailable.</p>
          )}
        </div>

        {/* Action Counters / Statistics */}
        <div className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">Activity Statistics</h3>
          </div>
          
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 12px', borderBottom: '1px solid var(--border)' }}>
              <span>Total Logged Actions</span>
              <strong>{auditLogs.length}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 12px', borderBottom: '1px solid var(--border)' }}>
              <span>User Registrations</span>
              <strong>{actionCounts['Registration'] || 0}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 12px', borderBottom: '1px solid var(--border)' }}>
              <span>Successful Logins</span>
              <strong>{actionCounts['Login'] || 0}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 12px', borderBottom: '1px solid var(--border)' }}>
              <span>Quizzes Started</span>
              <strong>{actionCounts['QuizStart'] || 0}</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 12px' }}>
              <span>Quizzes Submitted</span>
              <strong>{actionCounts['QuizSubmit'] || 0}</strong>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Section: Audit Log Table */}
      <div className="dashboard-card">
        <div className="dashboard-card-header">
          <h3 className="dashboard-card-title">
            <FileText size={18} />
            System Audit Trail
          </h3>
        </div>

        {auditLogs.length === 0 ? (
          <p style={{ color: 'var(--text-muted)', fontSize: '14px', textAlign: 'center', padding: '16px' }}>
            No audit logs recorded in database.
          </p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Action</th>
                  <th>Details</th>
                  <th>User</th>
                  <th>IP Address</th>
                </tr>
              </thead>
              <tbody>
                {auditLogs.map(log => (
                  <tr key={log.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>{toLocalDateTime(log.timestampUtc)}</td>
                    <td>
                      <span className={`badge ${
                        log.action === 'Login' ? 'badge-success' :
                        log.action === 'Registration' ? 'badge-student' :
                        log.action === 'QuizStart' ? 'badge-warning' : 'badge-teacher'
                      }`}>
                        {log.action}
                      </span>
                    </td>
                    <td>{log.details}</td>
                    <td><strong>{log.username}</strong></td>
                    <td style={{ fontFamily: 'var(--font-mono)', fontSize: '12px' }}>{log.ipAddress}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      
      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
        .animate-spin {
          animation: spin 1.5s linear infinite;
        }
      `}</style>
    </div>
  );
};

export default AdminDashboard;
