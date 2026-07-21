import React from 'react';
import Sidebar from './Sidebar';

const DashboardLayout = ({ children, user }) => {
  return (
    <div className="dashboard-layout-wrapper">
      <Sidebar user={user} />
      <main className="dashboard-main-content">
        {children}
      </main>
    </div>
  );
};

export default DashboardLayout;
