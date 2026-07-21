import React from 'react';
import { Navigate } from 'react-router-dom';
import { normalizeRole, getDashboardForRole } from '../utils/roles';

const RoleProtectedRoute = ({ children, allowedRoles }) => {
  const token = localStorage.getItem('accessToken');
  const userRole = normalizeRole(localStorage.getItem('userRole'));

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(userRole)) {
    return <Navigate to={getDashboardForRole(userRole)} replace />;
  }

  return children;
};

export default RoleProtectedRoute;
