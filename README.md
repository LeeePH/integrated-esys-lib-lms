# Integrated Campus Management System

A comprehensive web-based platform that seamlessly integrates three essential educational systems: Enrollment, Library Management, and Learning Management System (LMS).

## Overview

This integrated solution provides a unified experience for managing student enrollment, library operations, and online learning activities. The systems are interconnected to ensure data consistency and streamlined workflows across all campus operations.

## System Architecture

### Technology Stack
- **Database**: MongoDB
- **Architecture**: Model-View-Controller (MVC)
- **Frontend**: HTML, CSS, JavaScript
- **Backend**: C#

### Integrated Systems

#### 1. Enrollment System
Core system that manages student registration, course enrollment, and serves as the central authentication hub.

#### 2. Library System
Handles book cataloging, borrowing/returning operations, and penalty tracking.

#### 3. Learning Management System (LMS)
Facilitates online learning with course management, assignments, and professor-student interactions.

## System Integration

The three systems are tightly integrated to provide a seamless experience:

- **Unified Authentication**: All login credentials originate from the Enrollment System, providing single sign-on (SSO) capability across all platforms
- **Library-Enrollment Integration**: Library penalties are automatically reflected in the Enrollment System, ensuring students settle obligations before enrollment
- **LMS-Enrollment Integration**: Professor assignments and subject offerings from the LMS are synchronized with the Enrollment System for accurate course registration

## User Roles

### Enrollment System
- **Student**: Register for courses, view enrollment status, manage personal information
- **Admin**: Oversee all enrollment operations, manage system settings
- **Staff**: Process enrollments, handle student records

### Library System
- **Student**: Search catalog, borrow/return books, view borrowing history
- **Admin**: Manage library operations, generate reports
- **Librarian**: Process transactions, manage book inventory, handle penalties

### Learning Management System
- **Student**: Access course materials, submit assignments, view grades
- **Professor**: Create courses, upload materials, grade assignments
- **Admin**: Manage system settings, oversee all courses and users

## Default Credentials

### Library System
```
Email: admin@gmail.com
Password: admin123
```

### Learning Management System
```
Username: admin@mysuqc.local
Password: Admin@1234
```

### Enrollment System
```
Username: admin
Password: YourStrongPassword
```

> **Security Notice**: Change all default credentials immediately after initial setup. Use strong, unique passwords for each system.

## Getting Started

### Prerequisites
- MongoDB installed and running
- Modern web browser

### Installation

1. Clone the repository
```bash
git clone
cd integrated esys-lib-lms
```

2. Configure MongoDB connection
```bash
# Update database configuration in config files
```

3. Install dependencies
```bash
npm install
```

4. Start the application
```bash
npm start
```

5. Access the systems through your browser
- Enrollment System: `http://localhost:<port>/enrollment`
- Library System: `http://localhost:<port>/library`
- LMS: `http://localhost:<port>/lms`

## Key Features

### Enrollment System
- Student registration and course enrollment
- Academic records management
- Centralized user authentication
- Integration with library penalties
- Synchronized subject and professor data from LMS

### Library System
- Book catalog management
- Borrowing and return processing
- Fine and penalty calculation
- Real-time inventory tracking
- Automated penalty reporting to Enrollment System

### Learning Management System
- Course creation and management
- Assignment submission and grading
- Professor-student communication
- Grade tracking and reporting
- Subject data sync with Enrollment System

## Data Flow

1. **User Authentication**: Credentials verified through Enrollment System
2. **Library Penalties**: Automatically posted to student enrollment records
3. **Course Data**: Professor assignments and subjects flow from LMS to Enrollment
4. **Unified Profile**: Single student record across all systems

## API Integration

The systems communicate through RESTful APIs:

- **Authentication API**: Validates user credentials
- **Penalty Sync API**: Transfers library fines to enrollment records
- **Course Sync API**: Synchronizes LMS course data with enrollment

** SIA SET 1
