import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Award, CheckCircle, XCircle, AlertCircle, Clock, Calendar, ArrowLeft, RefreshCw } from 'lucide-react';
import api from '../services/api';
import { toLocalDateTime } from '../utils/timezone';

const QuizResult = () => {
  const { attemptId } = useParams();
  const navigate = useNavigate();
  const [attempt, setAttempt] = useState(null);
  const [quiz, setQuiz] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const loadResultDetails = async () => {
    try {
      setLoading(true);
      // 1. Fetch attempt result
      const attemptRes = await api.get(`/api/v1/attempts/${attemptId}`);
      const attemptData = attemptRes.data;
      setAttempt(attemptData);

      // 2. Fetch corresponding quiz details
      const quizRes = await api.get(`/api/v1/quizzes/${attemptData.quizId}`);
      setQuiz(quizRes.data);
      
      setError('');
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load attempt result details.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadResultDetails();
  }, [attemptId]);

  if (loading) {
    return <div style={{ textAlign: 'center', marginTop: '40px' }}>Loading Quiz Results...</div>;
  }

  if (error) {
    return (
      <div className="auth-page">
        <div className="card" style={{ maxWidth: '600px' }}>
          <div className="alert alert-danger">
            <AlertCircle size={24} />
            <span>{error}</span>
          </div>
          <button className="btn btn-secondary" onClick={() => navigate('/student')}>
            Back to Dashboard
          </button>
        </div>
      </div>
    );
  }

  // Calculate total quiz points
  const totalPoints = quiz.questions?.reduce((sum, q) => sum + q.points, 0) || 0;
  
  // Check if AI subjective grading is still running
  const isPendingGrading = attempt.status === 'Submitted' || attempt.submissions?.some(s => s.isCorrect === null && s.score === null);

  return (
    <div style={{ maxWidth: '800px', margin: '0 auto', width: '100%' }}>
      {/* Top Banner / Score Summary Card */}
      <div className="dashboard-card" style={{ textAlign: 'center', padding: '40px 24px', position: 'relative' }}>
        <Award size={64} style={{ color: 'var(--primary)', marginBottom: '16px' }} />
        <h2 style={{ fontSize: '28px', marginBottom: '8px' }}>Quiz Results</h2>
        <p style={{ color: 'var(--text-muted)', fontSize: '15px' }}>{quiz.title}</p>
        
        <div style={{ margin: '24px 0', display: 'flex', justifyContent: 'center', alignItems: 'baseline', gap: '4px' }}>
          <span style={{ fontSize: '64px', fontWeight: 800, color: isPendingGrading ? 'var(--warning)' : 'var(--text-title)' }}>
            {isPendingGrading ? '--' : (attempt.score ?? 0).toFixed(1)}
          </span>
          <span style={{ fontSize: '24px', color: 'var(--text-muted)' }}>/ {totalPoints} points</span>
        </div>

        {isPendingGrading ? (
          <div 
            className="alert alert-warning" 
            style={{ 
              maxWidth: '400px', 
              margin: '0 auto 16px', 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'center',
              gap: '8px' 
            }}
          >
            <RefreshCw size={18} className="animate-spin" style={{ animation: 'spin 2s linear infinite' }} />
            <span>Subjective grading in progress. Please refresh in a moment.</span>
          </div>
        ) : (
          <span className="badge badge-success" style={{ padding: '6px 16px', fontSize: '12px' }}>
            Graded Successfully
          </span>
        )}

        <div 
          style={{ 
            display: 'flex', 
            justifyContent: 'center', 
            gap: '24px', 
            marginTop: '32px', 
            borderTop: '1px solid var(--border)', 
            paddingTop: '20px',
            fontSize: '13px',
            color: 'var(--text-muted)'
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Calendar size={16} />
            Started: {toLocalDateTime(attempt.startedAtUtc)}
          </div>
          {attempt.submittedAtUtc && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Clock size={16} />
              Submitted: {toLocalDateTime(attempt.submittedAtUtc)}
            </div>
          )}
        </div>
      </div>

      {/* Question Details List */}
      <h3 style={{ fontSize: '18px', margin: '32px 0 16px' }}>Question Review</h3>
      
      <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
        {quiz.questions?.map((q, idx) => {
          const sub = attempt.submissions?.find(s => s.questionId === q.id) || {};
          const isCorrect = sub.isCorrect;
          
          return (
            <div 
              key={q.id} 
              className="dashboard-card" 
              style={{ 
                borderLeft: `4px solid ${
                  isCorrect === true ? 'var(--secondary)' : 
                  isCorrect === false ? 'var(--danger)' : 
                  'var(--warning)'
                }`
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '16px' }}>
                <h4 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text-title)' }}>
                  Question {idx + 1}: {q.text}
                </h4>
                <span className="badge badge-student" style={{ flexShrink: 0 }}>
                  {sub.score !== null && sub.score !== undefined ? `${sub.score.toFixed(1)} / ${q.points}` : `? / ${q.points}`} pts
                </span>
              </div>

              <div style={{ marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '10px', fontSize: '14px' }}>
                {/* User Answer */}
                <div style={{ padding: '12px', borderRadius: 'var(--radius-sm)', backgroundColor: 'var(--bg-main)', border: '1px solid var(--border)' }}>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 600 }}>Your Answer:</div>
                  <div style={{ marginTop: '4px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                    {isCorrect === true && <CheckCircle size={16} style={{ color: 'var(--secondary)' }} />}
                    {isCorrect === false && <XCircle size={16} style={{ color: 'var(--danger)' }} />}
                    {isCorrect === null && <AlertCircle size={16} style={{ color: 'var(--warning)' }} />}
                    <span>{sub.studentAnswer || '(No answer submitted)'}</span>
                  </div>
                </div>

                {/* Correct answer check for MCQ */}
                {q.type === 0 && (
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginLeft: '4px' }}>
                    Correct Option: <strong>{q.correctAnswer}</strong>
                  </div>
                )}

                {/* AI / Teacher Feedback */}
                {sub.feedback && (
                  <div 
                    style={{ 
                      padding: '12px', 
                      borderRadius: 'var(--radius-sm)', 
                      backgroundColor: 'var(--primary-light)', 
                      border: '1px solid var(--primary-border)',
                      fontSize: '13px',
                      color: 'var(--text-title)',
                      marginTop: '8px'
                    }}
                  >
                    <div style={{ fontWeight: 600, color: 'var(--primary)', marginBottom: '4px' }}>Grading Feedback:</div>
                    <div>{sub.feedback}</div>
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Actions */}
      <div style={{ display: 'flex', gap: '16px', marginTop: '32px', marginBottom: '48px' }}>
        <button className="btn btn-secondary" onClick={() => navigate('/student')}>
          <ArrowLeft size={16} />
          Back to Dashboard
        </button>
        {isPendingGrading && (
          <button className="btn btn-primary" onClick={loadResultDetails}>
            <RefreshCw size={16} />
            Refresh Results
          </button>
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

export default QuizResult;
