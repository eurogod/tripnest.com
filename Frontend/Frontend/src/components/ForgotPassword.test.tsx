import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import ForgotPassword from './ForgotPassword';

describe('ForgotPassword', () => {
  it('renders the request-code step', () => {
    render(<ForgotPassword onDone={() => {}} />);
    expect(screen.getByPlaceholderText('Email address')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send reset code/i })).toBeInTheDocument();
  });

  it('calls onDone from "Back to sign in"', () => {
    const onDone = vi.fn();
    render(<ForgotPassword onDone={onDone} />);
    fireEvent.click(screen.getByRole('button', { name: /back to sign in/i }));
    expect(onDone).toHaveBeenCalledOnce();
  });
});
