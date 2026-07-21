import React, { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Link } from 'react-router-dom';
import { BookOpen, LogOut, Shield, GraduationCap, LayoutDashboard } from 'lucide-react';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import DashboardLayout from './components/DashboardLayout';
import Login from './pages/Login';
import Register from './pages/Register';
import StudentDashboard from './pages/StudentDashboard';
import TeacherDashboard from './pages/TeacherDashboard';
import AdminDashboard from './pages/AdminDashboard';
import QuizAttempt from './pages/QuizAttempt';
import QuizResult from './pages/QuizResult';
import { UserRole, normalizeRole, getDashboardForRole } from './utils/roles';

function App() {
  const [user, setUser] = useState(null);

  // Sync user state with local storage
  const syncUser = () => {
    const token = localStorage.getItem('accessToken');
    const role = normalizeRole(localStorage.getItem('userRole'));
    const username = localStorage.getItem('username');
    if (token && role) {
      setUser({ token, role, username });
    } else {
      setUser(null);
    }
  };

  useEffect(() => {
    syncUser();
    // Watch local storage for authentication changes across components
    window.addEventListener('storage', syncUser);
    return () => window.removeEventListener('storage', syncUser);
  }, []);

  const handleLogout = () => {
    localStorage.clear();
    setUser(null);
    window.location.href = '/login';
  };

  return (
    <BrowserRouter>
      {/* Premium Header */}
      <header>
        <div className="container header-content">
          <Link to="/" className="logo-link" onClick={syncUser}>
            <GraduationCap size={28} className="logo-icon" />
            <span>AI Learning Platform</span>
          </Link>
          
          <div className="nav-links">
            {user ? (
              <>
                <span className="user-info">
                  <span className="badge badge-student">
                    {user.role}
                  </span>
                  <strong>{user.username}</strong>
                </span>
                
                {user.role === 'Student' && (
                  <Link to="/student" className="btn btn-secondary btn-sm">
                    <LayoutDashboard size={16} />
                    Dashboard
                  </Link>
                )}
                {user.role === 'Teacher' && (
                  <Link to="/teacher" className="btn btn-secondary btn-sm">
                    <LayoutDashboard size={16} />
                    Dashboard
                  </Link>
                )}
                {user.role === 'Admin' && (
                  <Link to="/admin" className="btn btn-secondary btn-sm">
                    <Shield size={16} />
                    Admin Panel
                  </Link>
                )}

                <button onClick={handleLogout} className="btn btn-danger btn-sm">
                  <LogOut size={16} />
                  Logout
                </button>
              </>
            ) : (
              <>
                <Link to="/login" className="btn btn-secondary btn-sm">Sign In</Link>
                <Link to="/register" className="btn btn-primary btn-sm">Sign Up</Link>
              </>
            )}
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className={user ? "" : "container"} style={{ flexGrow: 1, padding: user ? '0' : '32px 24px', display: 'flex', flexDirection: 'column' }}>
        <Routes>
          {/* Guest routes */}
          <Route 
            path="/login" 
            element={
              user ? <Navigate to={getDashboardForRole(user.role)} replace /> : <Login onAuthSuccess={syncUser} />
            } 
          />
          <Route 
            path="/register" 
            element={
              user ? <Navigate to={getDashboardForRole(user.role)} replace /> : <Register onAuthSuccess={syncUser} />
            } 
          />

          {/* Student Protected routes */}
          <Route 
            path="/student" 
            element={
              <RoleProtectedRoute allowedRoles={[UserRole.STUDENT]}>
                <DashboardLayout user={user}>
                  <StudentDashboard />
                </DashboardLayout>
              </RoleProtectedRoute>
            } 
          />
          <Route 
            path="/quiz/:quizId/attempt" 
            element={
              <RoleProtectedRoute allowedRoles={[UserRole.STUDENT]}>
                <QuizAttempt />
              </RoleProtectedRoute>
            } 
          />
          <Route 
            path="/attempt/:attemptId/result" 
            element={
              <RoleProtectedRoute allowedRoles={[UserRole.STUDENT]}>
                <QuizResult />
              </RoleProtectedRoute>
            } 
          />

          {/* Teacher Protected routes */}
          <Route 
            path="/teacher" 
            element={
              <RoleProtectedRoute allowedRoles={[UserRole.TEACHER]}>
                <DashboardLayout user={user}>
                  <TeacherDashboard />
                </DashboardLayout>
              </RoleProtectedRoute>
            } 
          />

          {/* Admin Protected routes */}
          <Route 
            path="/admin" 
            element={
              <RoleProtectedRoute allowedRoles={[UserRole.ADMIN]}>
                <DashboardLayout user={user}>
                  <AdminDashboard />
                </DashboardLayout>
              </RoleProtectedRoute>
            } 
          />

          {/* Root redirect */}
          <Route 
            path="/" 
            element={
              user ? <Navigate to={getDashboardForRole(user.role)} replace /> : <Navigate to="/login" replace />
            } 
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>

      {/* Premium Footer */}
      <footer>
        <div className="container">
          <p>© {new Date().getFullYear()} AI-Powered Learning Platform. All rights reserved.</p>
        </div>
      </footer>
    </BrowserRouter>
  );
}

export default App;
