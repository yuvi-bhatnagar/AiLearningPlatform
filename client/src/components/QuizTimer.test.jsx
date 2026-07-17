import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import QuizTimer from './QuizTimer';

describe('QuizTimer Component tests', () => {
  it('should render placeholders when timeLeftSeconds is null', () => {
    render(<QuizTimer timeLeftSeconds={null} />);
    const timerElement = screen.getByTestId('quiz-timer');
    
    expect(timerElement).toBeInTheDocument();
    expect(timerElement.textContent).toBe('--:--');
  });

  it('should format seconds into MM:SS correctly', () => {
    render(<QuizTimer timeLeftSeconds={125} />); // 2 mins 5 secs
    const timerElement = screen.getByTestId('quiz-timer');
    
    expect(timerElement.textContent).toBe('02:05');
  });

  it('should apply timer-warning class when time left is below 60 seconds', () => {
    render(<QuizTimer timeLeftSeconds={45} />);
    const timerElement = screen.getByTestId('quiz-timer');
    
    expect(timerElement).toHaveClass('timer-warning');
  });

  it('should not apply timer-warning class when time left is 60 seconds or above', () => {
    render(<QuizTimer timeLeftSeconds={60} />);
    const timerElement = screen.getByTestId('quiz-timer');
    
    expect(timerElement).not.toHaveClass('timer-warning');
  });
});
