import React from 'react';
import { Clock } from 'lucide-react';

const QuizTimer = ({ timeLeftSeconds }) => {
  const formatTime = (seconds) => {
    if (seconds === null || seconds === undefined) return '--:--';
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  const isTimerLow = timeLeftSeconds !== null && timeLeftSeconds < 60;

  return (
    <div className={`timer-box ${isTimerLow ? 'timer-warning' : ''}`} data-testid="quiz-timer">
      <Clock size={20} />
      <span>{formatTime(timeLeftSeconds)}</span>
    </div>
  );
};

export default QuizTimer;
