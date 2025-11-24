DROP DATABASE IF EXISTS cadastral_management;

CREATE DATABASE IF NOT EXISTS cadastral_management 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

USE cadastral_management;

-- Таблица: Пользователи
CREATE TABLE Users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    login VARCHAR(50) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    phone_number VARCHAR(20),
    user_type ENUM('Citizen', 'Employee') NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Таблица: Граждане
CREATE TABLE Citizens (
    citizen_id INT PRIMARY KEY,
    passport_data VARCHAR(10) NOT NULL,
    FOREIGN KEY (citizen_id) REFERENCES Users(user_id) ON DELETE CASCADE
);

-- Таблица: Сотрудники
CREATE TABLE Employees (
    employee_id INT PRIMARY KEY,
    department VARCHAR(100) NOT NULL,
    FOREIGN KEY (employee_id) REFERENCES Users(user_id) ON DELETE CASCADE
);

-- Таблица: Кадастровые объекты
CREATE TABLE CadastralObjects (
    cadastral_object_id INT AUTO_INCREMENT PRIMARY KEY,
    cadastral_number VARCHAR(14) NOT NULL UNIQUE,
    address VARCHAR(500) NOT NULL,
    area DECIMAL(10, 2) NOT NULL,
    cadastralObject_type ENUM('Земельный участок', 'Здание', 'Помещение') NOT NULL,
    owner_id INT NOT NULL,
    registration_date DATE NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (owner_id) REFERENCES Citizens(citizen_id) ON DELETE RESTRICT
);

-- Таблица: Заявления
CREATE TABLE Applications (
    application_id INT AUTO_INCREMENT PRIMARY KEY,
    application_date DATETIME NOT NULL,
    application_status ENUM('Принят к проверке', 'На проверке', 'Одобрен', 'Отклонен', 'Учтено') NOT NULL DEFAULT 'Принят к проверке',
    application_type ENUM('Регистрация', 'Обновление') NOT NULL,
    citizen_comment TEXT,
    decision_comment TEXT,
    applicant_id INT NOT NULL,
    assigned_employee_id INT,
    cadastral_object_id INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (applicant_id) REFERENCES Citizens(citizen_id) ON DELETE CASCADE,
    FOREIGN KEY (assigned_employee_id) REFERENCES Employees(employee_id) ON DELETE SET NULL,
    FOREIGN KEY (cadastral_object_id) REFERENCES CadastralObjects(cadastral_object_id) ON DELETE SET NULL
);

-- Таблица: Выписки
CREATE TABLE Extracts (
    extract_id INT AUTO_INCREMENT PRIMARY KEY,
    generation_date DATETIME NOT NULL,
    cadastral_object_id INT NOT NULL,
    requested_by_id INT NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    download_link_hash VARCHAR(64) UNIQUE,
    is_sent_via_email BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (cadastral_object_id) REFERENCES CadastralObjects(cadastral_object_id) ON DELETE CASCADE,
    FOREIGN KEY (requested_by_id) REFERENCES Citizens(citizen_id) ON DELETE CASCADE
);

-- Таблица: История заявлений
CREATE TABLE ApplicationHistory (
    history_id INT AUTO_INCREMENT PRIMARY KEY,
    application_id INT NOT NULL,
    old_status VARCHAR(50),
    new_status VARCHAR(50) NOT NULL,
    change_date DATETIME NOT NULL,
    changed_by_employee_id INT NOT NULL,
    history_comment TEXT,
    FOREIGN KEY (application_id) REFERENCES Applications(application_id) ON DELETE CASCADE,
    FOREIGN KEY (changed_by_employee_id) REFERENCES Employees(employee_id) ON DELETE CASCADE
);

-- Таблица: История изменений объектов
CREATE TABLE CadastralObjectHistory (
    history_id INT AUTO_INCREMENT PRIMARY KEY,
    cadastral_object_id INT NOT NULL,
    changed_field VARCHAR(50) NOT NULL,
    old_value TEXT,
    new_value TEXT,
    change_date DATETIME NOT NULL,
    changed_by_employee_id INT NOT NULL,
    FOREIGN KEY (cadastral_object_id) REFERENCES CadastralObjects(cadastral_object_id) ON DELETE CASCADE,
    FOREIGN KEY (changed_by_employee_id) REFERENCES Employees(employee_id) ON DELETE CASCADE
);

-- Таблица: Прикрепленные документы
CREATE TABLE Attachments (
    attachment_id INT AUTO_INCREMENT PRIMARY KEY,
    application_id INT NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    upload_date DATETIME NOT NULL,
    FOREIGN KEY (application_id) REFERENCES Applications(application_id) ON DELETE CASCADE
);

-- Создание индексов для оптимизации производительности

-- Индексы для таблицы Users
CREATE INDEX idx_users_login ON Users(login);
CREATE INDEX idx_users_email ON Users(email);
CREATE INDEX idx_users_type ON Users(user_type);

-- Индексы для таблицы CadastralObjects
CREATE INDEX idx_cadastral_objects_number ON CadastralObjects(cadastral_number);
CREATE INDEX idx_cadastral_objects_address ON CadastralObjects(address);
CREATE INDEX idx_cadastral_objects_owner ON CadastralObjects(owner_id);
CREATE INDEX idx_cadastral_objects_type ON CadastralObjects(cadastralObject_type);

-- Индексы для таблицы Applications
CREATE INDEX idx_applications_status ON Applications(application_status);
CREATE INDEX idx_applications_date ON Applications(application_date);
CREATE INDEX idx_applications_applicant ON Applications(applicant_id);
CREATE INDEX idx_applications_employee ON Applications(assigned_employee_id);
CREATE INDEX idx_applications_type ON Applications(application_type);

-- Индексы для таблицы Extracts
CREATE INDEX idx_extracts_generation_date ON Extracts(generation_date);
CREATE INDEX idx_extracts_requested_by ON Extracts(requested_by_id);
CREATE INDEX idx_extracts_link_hash ON Extracts(download_link_hash);

-- Индексы для таблицы ApplicationHistory
CREATE INDEX idx_app_history_application ON ApplicationHistory(application_id);
CREATE INDEX idx_app_history_date ON ApplicationHistory(change_date);
CREATE INDEX idx_app_history_employee ON ApplicationHistory(changed_by_employee_id);

-- Индексы для таблицы CadastralObjectHistory
CREATE INDEX idx_obj_history_object ON CadastralObjectHistory(cadastral_object_id);
CREATE INDEX idx_obj_history_date ON CadastralObjectHistory(change_date);
CREATE INDEX idx_obj_history_field ON CadastralObjectHistory(changed_field);

-- Индексы для таблицы Attachments
CREATE INDEX idx_attachments_application ON Attachments(application_id);
