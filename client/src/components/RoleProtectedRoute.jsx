import React from 'react';
import { Navigate } from 'react-router-dom';

const RoleProtectedRoute = ({ children, allowedRoles }) => {
  const token = localStorage.getItem('accessToken');
  const userRole = localStorage.getItem('userRole'); // 'Student', 'Teacher', 'Admin'

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(userRole)) {
    // Redirect to appropriate dashboard based on their role
    if (userRole === 'Student') return <Navigate to="/student" replace />;
    if (userRole === 'Teacher') return <Navigate to="/teacher" replace />;
    if (userRole === 'Admin') return <Navigate to="/admin" replace />;
    return <Navigate to="/login" replace />;
  }

  return children;
};

export default RoleProtectedRoute;
