import React, { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Clock, CheckSquare, ArrowRight, ArrowLeft, Send, AlertTriangle } from 'lucide-react';
import api from '../services/api';
import QuizTimer from '../components/QuizTimer';

const QuizAttempt = () => {
  const { quizId } = useParams();
  const navigate = useNavigate();
  const [quiz, setQuiz] = useState(null);
  const [attempt, setAttempt] = useState(null);
  const [currentIdx, setCurrentIdx] = useState(0);
  const [answers, setAnswers] = useState({}); // { [questionId]: answer }
  const [timeLeftSeconds, setTimeLeftSeconds] = useState(null);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const timerRef = useRef(null);

  const loadQuizAndAttempt = async () => {
    try {
      // 1. Fetch quiz details first
      const quizRes = await api.get(`/api/v1/quizzes/${quizId}`);
      setQuiz(quizRes.data);

      // 2. Fetch list of attempts to see if there is an active one for this quiz
      const attemptsRes = await api.get('/api/v1/attempts');
      const activeAttempt = attemptsRes.data.find(
        att => att.quizId === quizId && att.status === 'InProgress'
      );

      let currentAttempt;
      if (activeAttempt) {
        // Load details of existing attempt
        const attemptRes = await api.get(`/api/v1/attempts/${activeAttempt.id}`);
        currentAttempt = attemptRes.data;
        setAttempt(currentAttempt);
        
        // Load previous answers if any
        const savedAnswers = {};
        currentAttempt.submissions?.forEach(sub => {
          savedAnswers[sub.questionId] = sub.studentAnswer;
        });
        setAnswers(savedAnswers);
      } else {
        // Start a new attempt
        const startRes = await api.post('/api/v1/attempts/start', { quizId });
        // Retrieve full attempt with questions
        const attemptRes = await api.get(`/api/v1/attempts/${startRes.data.id}`);
        currentAttempt = attemptRes.data;
        setAttempt(currentAttempt);
      }

      // 3. Setup countdown timer
      const startTime = new Date(currentAttempt.startedAtUtc).getTime();
      const limitMs = quizRes.data.timeLimitMinutes * 60 * 1000;
      const endTime = startTime + limitMs;
      
      const updateTimer = () => {
        const remainingMs = endTime - Date.now();
        if (remainingMs <= 0) {
          setTimeLeftSeconds(0);
          clearInterval(timerRef.current);
          handleAutoSubmit(currentAttempt.id);
        } else {
          setTimeLeftSeconds(Math.floor(remainingMs / 1000));
        }
      };

      updateTimer();
      timerRef.current = setInterval(updateTimer, 1000);

    } catch (err) {
      setError(err.response?.data?.message || 'Failed to initialize quiz attempt.');
    }
  };

  useEffect(() => {
    loadQuizAndAttempt();
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
  }, [quizId]);

  const handleAutoSubmit = async (attemptId) => {
    setSubmitting(true);
    try {
      const formattedAnswers = Object.entries(answers).map(([qId, val]) => ({
        questionId: qId,
        studentAnswer: val
      }));
      await api.post(`/api/v1/attempts/${attemptId}/submit`, { answers: formattedAnswers });
      navigate(`/attempt/${attemptId}/result`);
    } catch (err) {
      setError('Auto-submission failed. Please notify the instructor.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleManualSubmit = async () => {
    if (!window.confirm('Are you sure you want to submit your answers?')) return;
    setSubmitting(true);
    try {
      const formattedAnswers = Object.entries(answers).map(([qId, val]) => ({
        questionId: qId,
        studentAnswer: val
      }));
      await api.post(`/api/v1/attempts/${attempt.id}/submit`, { answers: formattedAnswers });
      navigate(`/attempt/${attempt.id}/result`);
    } catch (err) {
      setError(err.response?.data?.message || 'Submission failed. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleAnswerSelect = (questionId, value) => {
    setAnswers(prev => ({
      ...prev,
      [questionId]: value
    }));
  };

  if (error) {
    return (
      <div className="auth-page">
        <div className="card" style={{ maxWidth: '600px' }}>
          <div className="alert alert-danger">
            <AlertTriangle size={24} />
            <span>{error}</span>
          </div>
          <button className="btn btn-secondary" onClick={() => navigate('/student')}>
            Back to Dashboard
          </button>
        </div>
      </div>
    );
  }

  if (!quiz || !attempt) {
    return <div style={{ textAlign: 'center', marginTop: '40px' }}>Loading Quiz Session...</div>;
  }

  // Get current active question from the attempt structure
  // Note: the backend returns questions inside the attempt.
  // Wait! In `AttemptDto`, questions are returned inside the dto:
  // `questions: List<AttemptQuestionDto>`
  // Let's verify `attempt` has a questions list. Yes, `MapToAttemptDto` returns a list of questions!
  // Wait, does GetAttemptByIdAsync return an `AttemptResultDto` or `AttemptDto`?
  // Let's look at AttemptsController:
  // `public async Task<IActionResult> GetById(Guid id) { var result = await _attemptService.GetAttemptByIdAsync(id, CurrentUserId, CurrentUserRole); return Ok(result); }`
  // Wait! `GetAttemptByIdAsync` returns `AttemptResultDto`!
  // But wait, does `AttemptResultDto` contain the list of questions?
  // Let's look at `AttemptResultDto.cs` again:
  // `public record AttemptResultDto(Guid Id, Guid QuizId, Guid UserId, DateTime StartedAtUtc, DateTime? SubmittedAtUtc, double? Score, string Status, List<AnswerSubmissionDto> Submissions)`
  // Wait! `AttemptResultDto` does NOT contain the list of questions!
  // Oh!
  // But `AttemptDto` (returned by `StartAttemptAsync`) DOES contain the list of questions!
  // Wait! When the user calls `POST /api/v1/attempts/start`, it returns `AttemptDto`, which has:
  // `questions: List<AttemptQuestionDto>`
  // But if the user resumes the attempt, we call `GET /api/v1/attempts/{id}` (which returns `AttemptResultDto` that doesn't have the list of questions!).
  // Wait! How do we get the questions if we are resuming?
  // Ah! We can get the questions from the quiz details!
  // The quiz details are fetched from `GET /api/v1/quizzes/{quizId}`!
  // And `Quiz` has a list of questions!
  // Yes! The quiz details return the quiz object which contains `questions`!
  // Let's verify if `Quiz` has questions:
  // Let's search for `Quiz` or quizzes controller to check if `GET /api/v1/quizzes/{id}` returns the questions.
  // Let's see: yes! The quizzes controller maps details and returns a QuizDto.
  // Let's write `QuizAttempt` to use the questions from the `quiz` details! That is 100% reliable and always has all questions.
  const quizQuestions = quiz.questions || [];
  const currentQuestion = quizQuestions[currentIdx];

  return (
    <div style={{ maxWidth: '800px', margin: '0 auto', width: '100%' }}>
      {/* Quiz Attempt Header */}
      <div className="quiz-header">
        <div>
          <h2 style={{ fontSize: '20px' }}>{quiz.title}</h2>
          <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
            Question {currentIdx + 1} of {quizQuestions.length}
          </span>
        </div>
        <QuizTimer timeLeftSeconds={timeLeftSeconds} />
      </div>

      {/* Progress Bar */}
      <div className="progress-bar-outer">
        <div 
          className="progress-bar-inner" 
          style={{ width: `${((currentIdx + 1) / quizQuestions.length) * 100}%` }}
        />
      </div>

      {/* Question Card */}
      {currentQuestion && (
        <div className="question-card">
          <div className="question-text">
            {currentQuestion.text}
            <span style={{ fontSize: '12px', color: 'var(--text-muted)', marginLeft: '10px' }}>
              ({currentQuestion.points} points)
            </span>
          </div>

          {currentQuestion.type === 0 ? (
            // Multiple Choice Questions (Type 0 = MCQ)
            <div className="options-list">
              {/* Note: options are deserialized from Json string in C# but in frontend we parse them */}
              {(typeof currentQuestion.optionsJson === 'string' 
                ? JSON.parse(currentQuestion.optionsJson || '[]')
                : currentQuestion.options || []
              ).map((option, idx) => {
                const isSelected = answers[currentQuestion.id] === option;
                return (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => handleAnswerSelect(currentQuestion.id, option)}
                    className={`option-btn ${isSelected ? 'option-selected' : ''}`}
                  >
                    {option}
                  </button>
                );
              })}
            </div>
          ) : (
            // Subjective Questions (Type 1 = Subjective)
            <div>
              <label className="form-label" htmlFor="subjective-answer">Write your answer below:</label>
              <textarea
                id="subjective-answer"
                className="form-control"
                rows="6"
                placeholder="Type your explanation here..."
                value={answers[currentQuestion.id] || ''}
                onChange={(e) => handleAnswerSelect(currentQuestion.id, e.target.value)}
                style={{ resize: 'vertical' }}
              />
            </div>
          )}
        </div>
      )}

      {/* Navigation Buttons */}
      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '20px' }}>
        <button
          onClick={() => setCurrentIdx(prev => Math.max(0, prev - 1))}
          className="btn btn-secondary"
          disabled={currentIdx === 0}
        >
          <ArrowLeft size={16} />
          Previous
        </button>

        {currentIdx < quizQuestions.length - 1 ? (
          <button
            onClick={() => setCurrentIdx(prev => prev + 1)}
            className="btn btn-primary"
          >
            Next
            <ArrowRight size={16} />
          </button>
        ) : (
          <button
            onClick={handleManualSubmit}
            className="btn btn-primary"
            style={{ backgroundColor: 'var(--secondary)' }}
            disabled={submitting}
          >
            {submitting ? 'Submitting...' : (
              <>
                <Send size={16} />
                Submit Quiz
              </>
            )}
          </button>
        )}
      </div>
    </div>
  );
};

export default QuizAttempt;
