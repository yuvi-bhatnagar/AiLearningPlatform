import React, { useEffect, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { 
  LayoutDashboard, 
  BookOpen, 
  Trophy, 
  Award, 
  Sliders, 
  Sparkles, 
  CheckCircle, 
  Shield, 
  Activity, 
  List, 
  ExternalLink 
} from 'lucide-react';
import './Sidebar.css';

const Sidebar = ({ user }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const [activeHash, setActiveHash] = useState(location.hash);

  useEffect(() => {
    setActiveHash(location.hash);
  }, [location]);

  if (!user) return null;

  const getMenuForRole = (role) => {
    switch (role) {
      case 'Student':
        return [
          { icon: LayoutDashboard, label: 'Overview', target: '/student' },
          { icon: BookOpen, label: 'My Courses', target: '/student#courses' },
          { icon: Trophy, label: 'Leaderboard', target: '/student#leaderboard' },
          { icon: Award, label: 'Stats & History', target: '/student#stats' },
        ];
      case 'Teacher':
        return [
          { icon: LayoutDashboard, label: 'Overview', target: '/teacher' },
          { icon: BookOpen, label: 'Course Manager', target: '/teacher#courses' },
          { icon: Sliders, label: 'Quiz Manager', target: '/teacher#quizzes' },
          { icon: Sparkles, label: 'AI Generator', target: '/teacher#ai-gen' },
          { icon: CheckCircle, label: 'Manual Grading', target: '/teacher#grading' },
        ];
      case 'Admin':
        return [
          { icon: Shield, label: 'Overview', target: '/admin' },
          { icon: Activity, label: 'System Health', target: '/admin#health' },
          { icon: List, label: 'Audit Logs', target: '/admin#audit' },
          { icon: ExternalLink, label: 'Hangfire Jobs', target: 'http://localhost:5209/hangfire' },
        ];
      default:
        return [];
    }
  };

  const menuItems = getMenuForRole(user.role);

  const handleItemClick = (target) => {
    if (target.startsWith('http')) {
      window.open(target, '_blank');
      return;
    }

    if (target.includes('#')) {
      const [path, hash] = target.split('#');
      if (location.pathname !== path) {
        navigate(target);
      } else {
        navigate(target);
        setTimeout(() => {
          const element = document.getElementById(hash);
          if (element) {
            element.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }
        }, 100);
      }
    } else {
      navigate(target);
    }
  };

  const getBadgeClass = (role) => {
    switch (role) {
      case 'Admin': return 'badge-danger';
      case 'Teacher': return 'badge-success';
      default: return 'badge-student';
    }
  };

  return (
    <aside className="sidebar-container">
      <div className="sidebar-header">
        <h4 style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-title)' }}>
          Navigation
        </h4>
        <span className={`sidebar-role-badge badge ${getBadgeClass(user.role)}`}>
          {user.role}
        </span>
      </div>

      <nav className="sidebar-menu">
        {menuItems.map((item, idx) => {
          const Icon = item.icon;
          const isActive = item.target.includes('#')
            ? location.pathname + activeHash === item.target
            : location.pathname === item.target && !activeHash;

          return (
            <div
              key={idx}
              className={`sidebar-item ${isActive ? 'active' : ''}`}
              onClick={() => handleItemClick(item.target)}
            >
              <Icon size={18} />
              <span>{item.label}</span>
            </div>
          );
        })}
      </nav>

      <div className="sidebar-footer">
        <div className="sidebar-avatar">
          {user.username?.charAt(0).toUpperCase() || 'U'}
        </div>
        <div className="sidebar-user-details">
          <span className="sidebar-username">{user.username}</span>
          <span className="sidebar-user-role">{user.role} Account</span>
        </div>
      </div>
    </aside>
  );
};

export default Sidebar;
