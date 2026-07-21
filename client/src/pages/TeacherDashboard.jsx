import React, { useState, useEffect } from 'react';
import { BookOpen, Plus, Trash2, HelpCircle, RefreshCw, Send, Sliders, AlertCircle, FileText, Check } from 'lucide-react';
import api from '../services/api';

const TeacherDashboard = () => {
  const [courses, setCourses] = useState([]);
  const [activeCourse, setActiveCourse] = useState(null);
  const [quizzes, setQuizzes] = useState([]);
  const [activeQuiz, setActiveQuiz] = useState(null);
  const [questions, setQuestions] = useState([]);
  const [reviews, setReviews] = useState([]);
  
  // Form states
  const [newCourseTitle, setNewCourseTitle] = useState('');
  const [newCourseDesc, setNewCourseDesc] = useState('');
  const [newQuizTitle, setNewQuizTitle] = useState('');
  const [newQuizDescription, setNewQuizDescription] = useState('');
  const [newQuizTimeLimit, setNewQuizTimeLimit] = useState(15);
  
  // Manual question form states
  const [qText, setQText] = useState('');
  const [qType, setQType] = useState(0); // 0 = MCQ, 1 = Subjective
  const [qOptions, setQOptions] = useState(['', '', '', '']);
  const [qCorrect, setQCorrect] = useState('');
  const [qPoints, setQPoints] = useState(5);

  // AI Generation states
  const [aiTopic, setAiTopic] = useState('');
  const [aiCount, setAiCount] = useState(5);
  const [aiGenerating, setAiGenerating] = useState(false);

  // Grade Override states
  const [overrideModal, setOverrideModal] = useState(null); // { attemptId, questionId, studentAnswer, maxPoints, studentName }
  const [overrideScore, setOverrideScore] = useState(0);
  const [overrideFeedback, setOverrideFeedback] = useState('');

  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchCoursesAndReviews = async () => {
    try {
      setLoading(true);
      setError('');

      const [coursesRes, reviewsRes] = await Promise.allSettled([
        api.get('/api/v1/courses'),
        api.get('/api/v1/attempts/low-confidence')
      ]);

      if (coursesRes.status === 'fulfilled') setCourses(coursesRes.value.data || []);
      if (reviewsRes.status === 'fulfilled') setReviews(reviewsRes.value.data || []);
    } catch (err) {
      // Graceful fallback
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCoursesAndReviews();
  }, []);

  const handleCreateCourse = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    try {
      const res = await api.post('/api/v1/courses', {
        title: newCourseTitle,
        description: newCourseDesc
      });
      setCourses(prev => [...prev, res.data]);
      setNewCourseTitle('');
      setNewCourseDesc('');
      setSuccess('Course created successfully!');
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create course.');
    }
  };

  const handleDeleteCourse = async (courseId, e) => {
    e.stopPropagation();
    if (!window.confirm('Are you sure you want to delete this course and all its quizzes?')) return;
    setError('');
    setSuccess('');
    try {
      await api.delete(`/api/v1/courses/${courseId}`);
      setCourses(prev => prev.filter(c => c.id !== courseId));
      if (activeCourse?.id === courseId) {
        setActiveCourse(null);
        setQuizzes([]);
        setActiveQuiz(null);
      }
      setSuccess('Course deleted successfully.');
    } catch (err) {
      setError('Failed to delete course.');
    }
  };

  const handleSelectCourse = async (course) => {
    setActiveCourse(course);
    setActiveQuiz(null);
    setQuestions([]);
    try {
      const res = await api.get(`/api/v1/quizzes/by-course/${course.id}`);
      setQuizzes(res.data);
    } catch (err) {
      setError('Failed to load course quizzes.');
    }
  };

  const handleCreateQuiz = async (e) => {
    e.preventDefault();
    if (!activeCourse) return;
    setError('');
    setSuccess('');
    try {
      const res = await api.post('/api/v1/quizzes', {
        courseId: activeCourse.id,
        title: newQuizTitle,
        description: newQuizDescription,
        timeLimitMinutes: parseInt(newQuizTimeLimit)
      });
      setQuizzes(prev => [...prev, res.data]);
      setNewQuizTitle('');
      setNewQuizDescription('');
      setNewQuizTimeLimit(15);
      setSuccess('Quiz created! Select it to manage questions.');
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create quiz.');
    }
  };

  const handleDeleteQuiz = async (quizId, e) => {
    e.stopPropagation();
    if (!window.confirm('Are you sure you want to delete this quiz?')) return;
    setError('');
    setSuccess('');
    try {
      await api.delete(`/api/v1/quizzes/${quizId}`);
      setQuizzes(prev => prev.filter(q => q.id !== quizId));
      if (activeQuiz?.id === quizId) {
        setActiveQuiz(null);
        setQuestions([]);
      }
      setSuccess('Quiz deleted.');
    } catch (err) {
      setError('Failed to delete quiz.');
    }
  };

  const handleSelectQuiz = async (quiz) => {
    setActiveQuiz(quiz);
    try {
      const res = await api.get(`/api/v1/questions/by-quiz/${quiz.id}`);
      setQuestions(res.data);
    } catch (err) {
      setError('Failed to load questions.');
    }
  };

  const handleCreateQuestion = async (e) => {
    e.preventDefault();
    if (!activeQuiz) return;
    setError('');
    setSuccess('');
    try {
      const res = await api.post('/api/v1/questions', {
        quizId: activeQuiz.id,
        text: qText,
        type: parseInt(qType),
        options: parseInt(qType) === 0 ? qOptions.filter(o => o !== '') : [],
        correctAnswer: qCorrect,
        points: parseInt(qPoints)
      });
      setQuestions(prev => [...prev, res.data]);
      setQText('');
      setQOptions(['', '', '', '']);
      setQCorrect('');
      setSuccess('Question added successfully.');
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to add question.');
    }
  };

  const handleGenerateAiQuestions = async (e) => {
    e.preventDefault();
    if (!activeQuiz) return;
    setError('');
    setSuccess('');
    setAiGenerating(true);
    try {
      const res = await api.post(`/api/v1/quizzes/${activeQuiz.id}/generate-questions`, {
        topic: aiTopic,
        questionCount: parseInt(aiCount)
      });
      
      // Reload questions
      const qRes = await api.get(`/api/v1/questions/by-quiz/${activeQuiz.id}`);
      setQuestions(qRes.data);
      setAiTopic('');
      setSuccess(`AI successfully generated ${res.data.length} questions!`);
    } catch (err) {
      setError(err.response?.data?.message || 'AI Question Generation failed. Please try again.');
    } finally {
      setAiGenerating(false);
    }
  };

  const handleOpenOverrideModal = (review) => {
    setOverrideModal(review);
    setOverrideScore(review.score || 0);
    setOverrideFeedback(review.feedback || '');
  };

  const handleOverrideSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    try {
      await api.post(`/api/v1/attempts/${overrideModal.attemptId}/override`, {
        questionId: overrideModal.questionId,
        score: parseFloat(overrideScore),
        feedback: overrideFeedback
      });
      
      // Refresh reviews list
      const reviewsRes = await api.get('/api/v1/attempts/low-confidence');
      setReviews(reviewsRes.data);
      setOverrideModal(null);
      setSuccess('Manual grading override saved successfully.');
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to submit override.');
    }
  };

  if (loading) {
    return <div style={{ textAlign: 'center', marginTop: '40px' }}>Loading Teacher Dashboard...</div>;
  }

  return (
    <div className="dashboard-grid">
      {/* Left Column: Courses and Quizzes Management */}
      <div>
        <h2 style={{ fontSize: '24px', marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <BookOpen className="logo-icon" />
          Teacher Workspace
        </h2>

        {error && <div className="alert alert-danger">{error}</div>}
        {success && <div className="alert alert-success">{success}</div>}

        {/* Courses Section */}
        <div id="courses" className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">Manage Courses</h3>
          </div>
          
          <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '16px' }}>
            {courses.map(course => (
              <div 
                key={course.id} 
                onClick={() => handleSelectCourse(course)}
                className={`leaderboard-row ${activeCourse?.id === course.id ? 'option-selected' : ''}`}
                style={{ cursor: 'pointer', padding: '16px' }}
              >
                <div>
                  <strong style={{ fontSize: '16px' }}>{course.title}</strong>
                  <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginTop: '4px' }}>{course.description}</p>
                </div>
                <button 
                  onClick={(e) => handleDeleteCourse(course.id, e)}
                  className="btn btn-danger btn-sm btn-icon-only"
                  title="Delete Course"
                >
                  <Trash2 size={16} />
                </button>
              </div>
            ))}
          </div>

          <form onSubmit={handleCreateCourse} style={{ marginTop: '24px', borderTop: '1px solid var(--border)', paddingTop: '20px' }}>
            <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Create New Course</h4>
            <div className="form-group">
              <input
                type="text"
                className="form-control"
                placeholder="Course Title (e.g. Advanced C#)"
                value={newCourseTitle}
                onChange={(e) => setNewCourseTitle(e.target.value)}
                required
              />
            </div>
            <div className="form-group">
              <input
                type="text"
                className="form-control"
                placeholder="Course Description"
                value={newCourseDesc}
                onChange={(e) => setNewCourseDesc(e.target.value)}
                required
              />
            </div>
            <button type="submit" className="btn btn-primary btn-sm">
              <Plus size={16} />
              Add Course
            </button>
          </form>
        </div>

        {/* Quizzes Section */}
        {activeCourse && (
          <div id="quizzes" className="dashboard-card">
            <div className="dashboard-card-header">
              <h3 className="dashboard-card-title">Quizzes for: {activeCourse.title}</h3>
            </div>
            
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {quizzes.map(quiz => (
                <div 
                  key={quiz.id}
                  onClick={() => handleSelectQuiz(quiz)}
                  className={`leaderboard-row ${activeQuiz?.id === quiz.id ? 'option-selected' : ''}`}
                  style={{ cursor: 'pointer', padding: '12px 16px' }}
                >
                  <div>
                    <strong>{quiz.title}</strong>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      Limit: {quiz.timeLimitMinutes} mins | Questions: {quiz.questions?.length || 0}
                    </div>
                  </div>
                  <button 
                    onClick={(e) => handleDeleteQuiz(quiz.id, e)}
                    className="btn btn-danger btn-sm btn-icon-only"
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              ))}
              {quizzes.length === 0 && <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>No quizzes in this course.</p>}
            </div>

            <form onSubmit={handleCreateQuiz} style={{ marginTop: '24px', borderTop: '1px solid var(--border)', paddingTop: '20px' }}>
              <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Add Quiz to Course</h4>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '12px' }}>
                <div style={{ display: 'flex', gap: '12px' }}>
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Quiz Title"
                    value={newQuizTitle}
                    onChange={(e) => setNewQuizTitle(e.target.value)}
                    required
                  />
                  <input
                    type="number"
                    className="form-control"
                    placeholder="Minutes"
                    value={newQuizTimeLimit}
                    onChange={(e) => setNewQuizTimeLimit(e.target.value)}
                    style={{ width: '120px' }}
                    required
                  />
                </div>
                <input
                  type="text"
                  className="form-control"
                  placeholder="Quiz Description (e.g. Test your knowledge on chapter 1)"
                  value={newQuizDescription}
                  onChange={(e) => setNewQuizDescription(e.target.value)}
                  required
                />
              </div>
              <button type="submit" className="btn btn-primary btn-sm">
                <Plus size={16} />
                Add Quiz
              </button>
            </form>
          </div>
        )}

        {/* Question Panel (Manual + AI) */}
        {activeQuiz && (
          <div className="dashboard-card">
            <div className="dashboard-card-header">
              <h3 className="dashboard-card-title">Questions in: {activeQuiz.title}</h3>
            </div>

            {/* Questions List */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '24px' }}>
              {questions.map((q, idx) => (
                <div key={q.id} style={{ padding: '12px', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', backgroundColor: 'var(--bg-main)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <strong>{idx + 1}. {q.text}</strong>
                    <span className="badge badge-student">{q.points} pts</span>
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                    Type: {q.type === 0 ? 'Multiple Choice' : 'Subjective'} | Correct: {q.correctAnswer}
                  </div>
                </div>
              ))}
              {questions.length === 0 && <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>No questions added to this quiz yet.</p>}
            </div>

            {/* AI Generation Form */}
            <div id="ai-gen" style={{ borderTop: '1px solid var(--border)', paddingTop: '20px', marginBottom: '24px' }}>
              <h4 style={{ fontSize: '14px', color: 'var(--primary)', marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Sliders size={16} />
                AI Question Generator
              </h4>
              <form onSubmit={handleGenerateAiQuestions} style={{ display: 'flex', gap: '12px', alignItems: 'flex-end' }}>
                <div style={{ flexGrow: 1 }}>
                  <label className="form-label">Topic</label>
                  <input
                    type="text"
                    className="form-control"
                    placeholder="e.g. Garbage Collection in .NET"
                    value={aiTopic}
                    onChange={(e) => setAiTopic(e.target.value)}
                    required
                  />
                </div>
                <div style={{ width: '100px' }}>
                  <label className="form-label">Count</label>
                  <input
                    type="number"
                    className="form-control"
                    value={aiCount}
                    onChange={(e) => setAiCount(e.target.value)}
                    min="1"
                    max="10"
                    required
                  />
                </div>
                <button type="submit" className="btn btn-primary" style={{ height: '42px' }} disabled={aiGenerating}>
                  {aiGenerating ? 'Generating...' : 'Generate'}
                </button>
              </form>
            </div>

            {/* Manual Question Form */}
            <div style={{ borderTop: '1px solid var(--border)', paddingTop: '20px' }}>
              <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Add Manual Question</h4>
              <form onSubmit={handleCreateQuestion}>
                <div className="form-group">
                  <label className="form-label">Question Text</label>
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Question Text"
                    value={qText}
                    onChange={(e) => setQText(e.target.value)}
                    required
                  />
                </div>
                
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                  <div className="form-group">
                    <label className="form-label">Type</label>
                    <select className="form-control" value={qType} onChange={(e) => setQType(parseInt(e.target.value))}>
                      <option value={0}>Multiple Choice</option>
                      <option value={1}>Subjective</option>
                    </select>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Points</label>
                    <input
                      type="number"
                      className="form-control"
                      value={qPoints}
                      onChange={(e) => setQPoints(e.target.value)}
                      required
                    />
                  </div>
                </div>

                {qType === 0 && (
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                    {qOptions.map((opt, idx) => (
                      <div key={idx}>
                        <label className="form-label">Option {idx + 1}</label>
                        <input
                          type="text"
                          className="form-control"
                          value={opt}
                          onChange={(e) => {
                            const copy = [...qOptions];
                            copy[idx] = e.target.value;
                            setQOptions(copy);
                          }}
                          required
                        />
                      </div>
                    ))}
                  </div>
                )}

                <div className="form-group">
                  <label className="form-label">Correct Answer</label>
                  <input
                    type="text"
                    className="form-control"
                    placeholder={qType === 0 ? "Copy exact text of correct option" : "Correct answer model criteria"}
                    value={qCorrect}
                    onChange={(e) => setQCorrect(e.target.value)}
                    required
                  />
                </div>

                <button type="submit" className="btn btn-secondary btn-sm">
                  <Plus size={16} />
                  Add Question
                </button>
              </form>
            </div>
          </div>
        )}
      </div>

      {/* Right Column: AI Submissions review list */}
      <div>
        <div id="grading" className="dashboard-card">
          <div className="dashboard-card-header">
            <h3 className="dashboard-card-title">
              <AlertCircle size={18} style={{ color: 'var(--warning)' }} />
              Flagged AI Submissions
            </h3>
          </div>
          
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '16px' }}>
            Flagged subjective questions graded with low confidence by Gemini AI requiring manual review.
          </p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {reviews.map((rev, idx) => (
              <div 
                key={idx} 
                style={{ 
                  padding: '16px', 
                  border: '1px solid var(--border)', 
                  borderRadius: 'var(--radius-sm)',
                  backgroundColor: 'var(--bg-main)'
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                  <span className="badge badge-student">{rev.studentName}</span>
                  <span className="badge badge-warning">Low Confidence</span>
                </div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Quiz: {rev.quizTitle}</div>
                <div style={{ fontSize: '13px', fontWeight: 600, marginTop: '8px' }}>Q: {rev.questionText}</div>
                <div style={{ fontSize: '13px', color: 'var(--text-title)', marginTop: '6px', borderLeft: '3px solid var(--border)', paddingLeft: '8px' }}>
                  Answer: {rev.studentAnswer}
                </div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px' }}>
                  AI Grade: {rev.score != null ? `${rev.score?.toFixed?.(1) ?? rev.score}` : '?'}/{rev.maxPoints ?? '?'} pts
                </div>

                <button 
                  onClick={() => handleOpenOverrideModal(rev)}
                  className="btn btn-secondary btn-sm" 
                  style={{ width: '100%', marginTop: '12px' }}
                >
                  Override Grade
                </button>
              </div>
            ))}

            {reviews.length === 0 && (
              <p style={{ color: 'var(--text-muted)', fontSize: '13px', textAlign: 'center', padding: '16px' }}>
                No flagged low-confidence evaluations found. All clear!
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Manual Grade Override Modal */}
      {overrideModal && (
        <div 
          style={{ 
            position: 'fixed', 
            top: 0, 
            left: 0, 
            right: 0, 
            bottom: 0, 
            backgroundColor: 'rgba(0,0,0,0.6)', 
            display: 'flex', 
            alignItems: 'center', 
            justifyContent: 'center', 
            zIndex: 1000 
          }}
        >
          <div className="card" style={{ maxWidth: '600px', width: '100%' }}>
            <h3 style={{ fontSize: '20px', marginBottom: '12px' }}>Review Student Answer</h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '16px' }}>
              Student: {overrideModal.studentName} | Quiz: {overrideModal.quizTitle}
            </p>

            <div style={{ marginBottom: '16px' }}>
              <strong>Question:</strong>
              <p style={{ fontSize: '14px', backgroundColor: 'var(--bg-main)', padding: '10px', borderRadius: '4px', border: '1px solid var(--border)', marginTop: '4px' }}>
                {overrideModal.questionText}
              </p>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <strong>Student's Answer:</strong>
              <p style={{ fontSize: '14px', backgroundColor: 'var(--bg-main)', padding: '10px', borderRadius: '4px', border: '1px solid var(--border)', marginTop: '4px' }}>
                {overrideModal.studentAnswer}
              </p>
            </div>

            <form onSubmit={handleOverrideSubmit}>
              <div className="form-group">
                <label className="form-label">Award Score (Max: {overrideModal.maxPoints} pts)</label>
                <input
                  type="number"
                  step="0.1"
                  className="form-control"
                  value={overrideScore}
                  onChange={(e) => setOverrideScore(e.target.value)}
                  min="0"
                  max={overrideModal.maxPoints}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">Feedback Explanation</label>
                <textarea
                  className="form-control"
                  rows="4"
                  value={overrideFeedback}
                  onChange={(e) => setOverrideFeedback(e.target.value)}
                  placeholder="Explain why you are adjusting the score..."
                  required
                />
              </div>

              <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '20px' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setOverrideModal(null)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Save Grade
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default TeacherDashboard;
