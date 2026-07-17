import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpen, Trophy, Award, BarChart2, Calendar, FileText, ChevronDown, ChevronUp, Play } from 'lucide-react';
import api from '../services/api';
import { toLocalDateTime } from '../utils/timezone';

const StudentDashboard = () => {
  const [courses, setCourses] = useState([]);
  const [expandedCourse, setExpandedCourse] = useState(null);
  const [quizzes, setQuizzes] = useState({});
  const [leaderboard, setLeaderboard] = useState([]);
  const [summary, setSummary] = useState(null);
  const [attempts, setAttempts] = useState([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  const fetchDashboardData = async () => {
    try {
      setLoading(true);
      const [coursesRes, leaderboardRes, attemptsRes] = await Promise.all([
        api.get('/api/v1/courses'),
        api.get('/api/v1/leaderboards'),
        api.get('/api/v1/attempts')
      ]);

      setCourses(coursesRes.data);
      setLeaderboard(leaderboardRes.data);
      setAttempts(attemptsRes.data);

      try {
        const summaryRes = await api.get('/api/v1/leaderboards/summary');
        setSummary(summaryRes.data);
      } catch (sumErr) {
        // Safe check if no performance statistics exist yet for new user
        setSummary(null);
      }
    } catch (err) {
      setError('Failed to load dashboard data. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const toggleCourse = async (courseId) => {
    if (expandedCourse === courseId) {
      setExpandedCourse(null);
      return;
    }

    setExpandedCourse(courseId);
    if (!quizzes[courseId]) {
      try {
        const res = await api.get(`/api/v1/quizzes/by-course/${courseId}`);
        setQuizzes(prev => ({ ...prev, [courseId]: res.data }));
      } catch (err) {
        setError('Failed to load course quizzes.');
      }
    }
  };

  const handleStartAttempt = async (quizId) => {
    try {
      // Direct navigation to quiz attempt which handles starting or resuming
      navigate(`/quiz/${quizId}/attempt`);
    } catch (err) {
      setError('Could not start attempt.');
    }
  };

  if (loading) {
    return <div style={{ textAlign: 'center', marginTop: '40px' }}>Loading Student Dashboard...</div>;
  }

  return (
    <div className="dashboard-grid">
      {/* Left Column: Courses and Quizzes */}
      <div>
        <h2 style={{ fontSize: '24px', marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <BookOpen className="logo-icon" />
          My Courses
        </h2>

        {error && <div className="alert alert-danger">{error}</div>}

        <div className="courses-grid" style={{ gridTemplateColumns: '1fr' }}>
          {courses.map(course => (
            <div key={course.id} className="dashboard-card" style={{ padding: '20px', marginBottom: '16px' }}>
              <div 
                onClick={() => toggleCourse(course.id)}
                style={{ 
                  display: 'flex', 
                  justifyContent: 'space-between', 
                  alignItems: 'center', 
                  cursor: 'pointer' 
                }}
              >
                <div>
                  <h3 style={{ fontSize: '18px', fontWeight: 600 }}>{course.title}</h3>
                  <p style={{ color: 'var(--text-muted)', fontSize: '14px', marginTop: '4px' }}>
                    {course.description}
                  </p>
                </div>
                {expandedCourse === course.id ? <ChevronUp size={20} /> : <ChevronDown size={20} />}
              </div>

              {expandedCourse === course.id && (
                <div style={{ marginTop: '20px', borderTop: '1px solid var(--border)', paddingTop: '16px' }}>
                  <h4 style={{ fontSize: '14px', color: 'var(--text-muted)', marginBottom: '12px' }}>Quizzes Available</h4>
                  
                  {quizzes[course.id]?.length === 0 ? (
                    <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>No quizzes created for this course yet.</p>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                      {quizzes[course.id]?.map(quiz => {
                        const hasAttempt = attempts.find(a => a.quizId === quiz.id);
                        return (
                          <div 
                            key={quiz.id} 
                            style={{ 
                              display: 'flex', 
                              justifyContent: 'space-between', 
                              alignItems: 'center',
                              padding: '12px',
                              borderRadius: 'var(--radius-sm)',
                              border: '1px solid var(--border)',
                              backgroundColor: 'var(--bg-main)'
                            }}
                          >
                            <div>
                              <div style={{ fontWeight: 600, fontSize: '14px' }}>{quiz.title}</div>
                              <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                                Time Limit: {quiz.timeLimitMinutes} mins | Questions: {quiz.questions?.length || 0}
                              </div>
                            </div>
                            
                            <div>
                              {hasAttempt ? (
                                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                  <span className={`badge ${hasAttempt.status === 'Completed' ? 'badge-success' : 'badge-warning'}`}>
                                    {hasAttempt.status === 'Completed' ? `Score: ${hasAttempt.score ?? 0}` : 'In Progress'}
                                  </span>
                                  {hasAttempt.status === 'Completed' ? (
                                    <button 
                                      onClick={() => navigate(`/attempt/${hasAttempt.id}/result`)}
                                      className="btn btn-secondary btn-sm"
                                    >
                                      Review
                                    </button>
                                  ) : (
                                    <button 
                                      onClick={() => handleStartAttempt(quiz.id)}
                                      className="btn btn-primary btn-sm"
                                    >
                                      Resume
                                    </button>
                                  )}
                                </div>
                              ) : (
                                <button 
                                  onClick={() => handleStartAttempt(quiz.id)}
                                  className="btn btn-primary btn-sm"
                                  style={{ gap: '4px' }}
                                >
                                  <Play size={12} fill="white" />
                                  Start
                                </button>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Quiz Attempts History */}
        <div className="dashboard-card" style={{ marginTop: '24px' }}>
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">
              <FileText size={18} />
              Attempt History
            </h3>
          </div>
          
          {attempts.length === 0 ? (
            <p style={{ color: 'var(--text-muted)', fontSize: '14px', textAlign: 'center', padding: '16px' }}>
              You haven't attempted any quizzes yet.
            </p>
          ) : (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>Date Attempted</th>
                    <th>Status</th>
                    <th>Score</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {attempts.map(att => (
                    <tr key={att.id}>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <Calendar size={14} style={{ color: 'var(--text-muted)' }} />
                          {toLocalDateTime(att.startedAtUtc)}
                        </div>
                      </td>
                      <td>
                        <span className={`badge ${att.status === 'Completed' ? 'badge-success' : 'badge-warning'}`}>
                          {att.status}
                        </span>
                      </td>
                      <td>
                        <strong>{att.score !== null ? `${att.score}` : '--'}</strong>
                      </td>
                      <td>
                        <button 
                          onClick={() => {
                            if (att.status === 'Completed') {
                              navigate(`/attempt/${att.id}/result`);
                            } else {
                              navigate(`/quiz/${att.quizId}/attempt`);
                            }
                          }}
                          className="btn btn-secondary btn-sm"
                        >
                          {att.status === 'Completed' ? 'View Details' : 'Resume'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Right Column: Performance Summary and Leaderboard */}
      <div>
        {/* Performance Statistics */}
        <div className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">
              <BarChart2 size={18} />
              My Stats
            </h3>
          </div>
          
          {summary ? (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
              <div style={{ padding: '12px', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Average Score</div>
                <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--primary)' }}>{summary.averageScore.toFixed(1)}</div>
              </div>
              <div style={{ padding: '12px', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Highest Score</div>
                <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--secondary)' }}>{summary.highestScore}</div>
              </div>
              <div style={{ padding: '12px', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Lowest Score</div>
                <div style={{ fontSize: '20px', fontWeight: 700 }}>{summary.lowestScore}</div>
              </div>
              <div style={{ padding: '12px', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Total Attempts</div>
                <div style={{ fontSize: '20px', fontWeight: 700 }}>{summary.totalAttempts}</div>
              </div>
            </div>
          ) : (
            <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
              Start attempting quizzes to build performance statistics!
            </p>
          )}
        </div>

        {/* Global Leaderboard */}
        <div className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">
              <Trophy size={18} style={{ color: '#f59e0b' }} />
              Leaderboard
            </h3>
          </div>

          <div className="leaderboard-list">
            {leaderboard.map((row, idx) => (
              <div key={row.userId} className="leaderboard-row">
                <div style={{ display: 'flex', alignItems: 'center' }}>
                  <span className={`leaderboard-rank ${idx === 0 ? 'rank-1' : idx === 1 ? 'rank-2' : idx === 2 ? 'rank-3' : 'rank-other'}`}>
                    {idx + 1}
                  </span>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '14px', color: 'var(--text-title)' }}>
                      {row.username}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      {row.totalAttempts} attempts
                    </div>
                  </div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--primary)' }}>
                    {row.totalScore.toFixed(1)}
                  </div>
                  <div style={{ fontSize: '10px', color: 'var(--text-muted)' }}>avg: {row.averageScore.toFixed(1)}</div>
                </div>
              </div>
            ))}

            {leaderboard.length === 0 && (
              <p style={{ color: 'var(--text-muted)', fontSize: '13px', textAlign: 'center' }}>
                No records listed on the leaderboard yet.
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default StudentDashboard;
