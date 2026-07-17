import { describe, it, expect } from 'vitest';
import { toLocalDateTime } from './timezone';

describe('timezone utility tests', () => {
  it('should return empty string for null or undefined input', () => {
    expect(toLocalDateTime(null)).toBe('');
    expect(toLocalDateTime(undefined)).toBe('');
    expect(toLocalDateTime('')).toBe('');
  });

  it('should return empty string for invalid date formats', () => {
    expect(toLocalDateTime('invalid-date')).toBe('');
  });

  it('should correctly format valid UTC ISO date strings', () => {
    const utcDateStr = '2026-07-17T04:00:00.000Z';
    const formatted = toLocalDateTime(utcDateStr);
    
    // Output depends on local system timezone but it must contain parts of year/month/time
    expect(formatted).toContain('2026');
    expect(formatted).toContain('Jul');
  });
});
