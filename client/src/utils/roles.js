export const UserRole = Object.freeze({
  STUDENT: 'Student',
  TEACHER: 'Teacher',
  ADMIN: 'Admin',
});

export const RoleMap = Object.freeze({
  0: UserRole.STUDENT,
  1: UserRole.TEACHER,
  2: UserRole.ADMIN,
});

export const RoleDashboardMap = Object.freeze({
  [UserRole.STUDENT]: '/student',
  [UserRole.TEACHER]: '/teacher',
  [UserRole.ADMIN]: '/admin',
});

/**
 * Normalizes any role input (number, numeric string, or string name)
 * into standard UserRole enum string ('Student' | 'Teacher' | 'Admin').
 */
export const normalizeRole = (role) => {
  if (role === null || role === undefined) return null;
  
  if (Object.prototype.hasOwnProperty.call(RoleMap, role)) {
    return RoleMap[role];
  }
  
  const str = String(role).trim().toLowerCase();
  if (str === '0' || str === 'student') return UserRole.STUDENT;
  if (str === '1' || str === 'teacher') return UserRole.TEACHER;
  if (str === '2' || str === 'admin') return UserRole.ADMIN;
  
  return role;
};

/**
 * Returns the dashboard route path for a given role.
 */
export const getDashboardForRole = (role) => {
  const normalized = normalizeRole(role);
  return RoleDashboardMap[normalized] || '/login';
};
