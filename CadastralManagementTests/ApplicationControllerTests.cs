using System.Text;
using Cadastral_Management.Controllers;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Cadastral_Management.Services;
using Cadastral_ManagementServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace CadastralManagementTests
{
    [TestFixture]
    public class ApplicationControllerTests
    {
        private ApplicationDbContext _context;
        private Mock<ISessionService> _mockSessionService;
        private Mock<IFileService> _mockFileService;
        private Mock<IApplicationService> _mockApplicationService;
        private ApplicationController _controller;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockSessionService = new Mock<ISessionService>();
            _mockFileService = new Mock<IFileService>();
            _mockApplicationService = new Mock<IApplicationService>();

            // Создаем контроллер с моками
            _controller = new ApplicationController(
                _context,
                _mockSessionService.Object,
                _mockFileService.Object,
                _mockApplicationService.Object
            );
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _controller?.Dispose();
        }

        // Тест 1: Создание заявления "Регистрация" (Citizen)
        [Test]
        public async Task Create_RegistrationApplication_ReturnsViewWithSuccess()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var applicationType = "Регистрация";
            var address = "Москва, ул. Ленина, 1";
            var area = 100.5m;
            var objectType = "Земельный участок";
            var citizenComment = "Тестовый комментарий";

            // Настраиваем ApplicationService для создания заявления
            var createdApplication = new Application
            {
                ApplicationId = 1,
                ApplicationType = applicationType,
                Address = address,
                Area = area,
                CadastralObjectType = objectType,
                CitizenComment = citizenComment,
                ApplicantId = 1,
                ApplicationStatus = "Принят к проверке"
            };

            _mockApplicationService
                .Setup(s => s.CreateApplicationAsync(
                    It.Is<Application>(a =>
                        a.ApplicationType == applicationType &&
                        a.Address == address &&
                        a.Area == area &&
                        a.CitizenComment == citizenComment),
                    null))
                .ReturnsAsync(createdApplication);

            // Act
            var result = await _controller.Create(
                applicationType, "", address, area, objectType, citizenComment, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Success"], Is.Not.Null);
            Assert.That(viewResult.ViewData["Success"].ToString(), Contains.Substring("Заявление успешно подано"));
        }

        // Тест 2: Создание заявления "Обновление" (Citizen, владелец объекта) - ИСПРАВЛЕННЫЙ
        [Test]
        public async Task Create_UpdateApplication_WhenUserIsOwner_ReturnsViewWithSuccess()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var userId = 1;
            var cadastralNumber = "77:01:001:1234";

            var user = new User
            {
                UserId = userId,
                FullName = "Test User",
                Email = "mail@m.ru",
                Login = "login",
                PasswordHash = "hash",
                UserType = "Citizen"
            };

            var citizen = new Citizen
            {
                CitizenId = userId,
                PassportData = "1234567890",
                User = user // Связываем!
            };

            var cadastralObject = new CadastralObject
            {
                CadastralObjectId = 1,
                CadastralNumber = cadastralNumber,
                Address = "Старый адрес",
                CadastralObjectType = "Квартира",
                Area = 50.5m,
                OwnerId = userId,
                Owner = citizen
            };

            await _context.Users.AddAsync(user);
            await _context.Citizens.AddAsync(citizen);
            await _context.CadastralObjects.AddAsync(cadastralObject);
            await _context.SaveChangesAsync();

            var applicationType = "Обновление";
            var newAddress = "Новый адрес";
            var newArea = 150.5m;
            var newObjectType = "Здание";
            var citizenComment = "Хочу обновить данные";

            // Act
            var result = await _controller.Create(
                applicationType, cadastralNumber, newAddress, newArea, newObjectType, citizenComment, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;

            // Debug вывод
            Console.WriteLine($"Success: {viewResult.ViewData["Success"]}");
            Console.WriteLine($"Error: {viewResult.ViewData["Error"]}");

            // Проверяем результат
            Assert.That(viewResult.ViewData["Success"], Is.Not.Null);
            Assert.That(viewResult.ViewData["Success"].ToString(), Contains.Substring("Заявление успешно подано"));

            // Дополнительно проверяем что заявление создалось в БД
            var createdApplication = await _context.Applications
                .FirstOrDefaultAsync(a => a.ApplicationType == "Обновление");

            Assert.That(createdApplication, Is.Not.Null);
            Assert.That(createdApplication.CadastralObjectId, Is.EqualTo(1)); // Связано с объектом
            Assert.That(createdApplication.Address, Is.EqualTo(newAddress));
            Assert.That(createdApplication.ApplicationStatus, Is.EqualTo("Принят к проверке"));
        }

        // Тест 3: Создание заявления с документом - ИСПРАВЛЕННЫЙ
        [Test]
        public async Task Create_ApplicationWithDocument_ReturnsViewWithSuccess()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var applicationType = "Регистрация";
            var address = "Москва, ул. Ленина, 1";
            var area = 100.5m;
            var objectType = "Земельный участок";
            var citizenComment = "С документом";

            // Создаем мок файла (УПРОЩЕННЫЙ)
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("test.pdf");
            fileMock.Setup(f => f.Length).Returns(1024); // 1KB
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");

            // Простой MemoryStream
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            fileMock.Setup(f => f.OpenReadStream()).Returns(ms);

            // Настраиваем FileService
            _mockFileService
                .Setup(f => f.SaveApplicationDocumentAsync(fileMock.Object, It.IsAny<int>()))
                .ReturnsAsync("/uploads/applications/test.pdf");

            // Act
            var result = await _controller.Create(
                applicationType, "", address, area, objectType, citizenComment, fileMock.Object);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;

            // Debug вывод
            Console.WriteLine($"Success: {viewResult.ViewData["Success"]}");
            Console.WriteLine($"Error: {viewResult.ViewData["Error"]}");

            // Проверяем что что-то вернулось
            Assert.That(viewResult.ViewData["Success"], Is.Not.Null);
            Assert.That(viewResult.ViewData["Success"].ToString(), Contains.Substring("Заявление успешно подано"));

            // Проверяем вызов FileService
            _mockFileService.Verify(
                f => f.SaveApplicationDocumentAsync(fileMock.Object, It.IsAny<int>()),
                Times.Once);
        }

        // Тест 4: Создание заявления "Обновление" без кадастрового номера
        [Test]
        public async Task Create_UpdateApplicationWithoutCadastralNumber_ReturnsViewWithError()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var applicationType = "Обновление";
            var address = "Адрес";
            var area = 100.5m;
            var objectType = "Участок";
            var citizenComment = "Комментарий";

            // Act
            var result = await _controller.Create(
                applicationType, "", address, area, objectType, citizenComment, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Error"], Is.Not.Null);
            Assert.That(viewResult.ViewData["Error"].ToString(), Contains.Substring("необходимо указать кадастровый номер"));
        }

        // Тест 5: Одобрение заявления (Employee) - БЫСТРОЕ ИСПРАВЛЕНИЕ
        [Test]
        public async Task Approve_ValidApplication_RedirectsToViewAll()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(true);
            _mockSessionService.Setup(s => s.IsAdmin()).Returns(false); // Добавляем!
            _mockSessionService.Setup(s => s.GetUserId()).Returns("2");

            var applicationId = 1;
            var decisionComment = "Одобрено";

            // СОЗДАЕМ ВСЕ НЕОБХОДИМЫЕ СУЩНОСТИ
            var user = new User { UserId = 1,
                FullName = "Citizen User",
                Email = "user@mail.ru",
                Login = "userLogin",
                PasswordHash = "hash",
                UserType = "Citizen"
            };
            var citizen = new Citizen { CitizenId = 1, PassportData = "1234567890", User = user };

            var application = new Application
            {
                ApplicationId = applicationId,
                ApplicationType = "Регистрация",
                ApplicationStatus = "На проверке",
                Address = "Адрес",
                Area = 100.5m,
                CadastralObjectType = "Участок",
                ApplicantId = 1,
                Applicant = citizen
            };

            await _context.Users.AddAsync(user);
            await _context.Citizens.AddAsync(citizen);
            await _context.Applications.AddAsync(application);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Approve(applicationId, decisionComment);

            // Assert - расширенные проверки
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("ViewAll"));

            // Проверяем изменения в БД
            var updatedApp = await _context.Applications
                .Include(a => a.ApplicationHistories)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            Assert.That(updatedApp, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(updatedApp.ApplicationStatus, Is.EqualTo("Одобрен"));
                Assert.That(updatedApp.DecisionComment, Is.EqualTo(decisionComment));
                Assert.That(updatedApp.AssignedEmployeeId, Is.EqualTo(2));

                // Проверяем историю
                Assert.That(updatedApp.ApplicationHistories, Has.Count.EqualTo(1));
                var history = updatedApp.ApplicationHistories.First();
                Assert.That(history.NewStatus, Is.EqualTo("Одобрен"));
                Assert.That(history.ChangedByEmployeeId, Is.EqualTo(2));
            });

            // Для регистрации должен создаться объект
            var createdObject = await _context.CadastralObjects.FirstOrDefaultAsync();
            if (application.ApplicationType == "Регистрация")
            {
                Assert.That(createdObject, Is.Not.Null);
                Assert.That(createdObject.OwnerId, Is.EqualTo(1));
            }
        }

        // Тест 6: Отклонение заявления (Employee) - ИСПРАВЛЕННЫЙ
        [Test]
        public async Task Reject_ValidApplication_RedirectsToViewAll()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(true);
            _mockSessionService.Setup(s => s.IsAdmin()).Returns(false);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("2");

            var applicationId = 1;
            var employeeId = 2;
            var decisionComment = "Отклонено по причине...";

            // Создаем тестовое заявление в БД
            var application = new Application
            {
                ApplicationId = applicationId,
                ApplicationType = "Регистрация",
                ApplicationStatus = "На проверке",
                Address = "Адрес",
                Area = 100.5m,
                CadastralObjectType = "Участок",
                ApplicantId = 1
            };

            // Создаем гражданина для связи
            var citizen = new Citizen { CitizenId = 1, PassportData = "1234567890"};
            var user = new User
            {
                UserId = 1,
                FullName = "Test",
                Email = "mail@mail.ru",
                Login = "login",
                PasswordHash = "hash",
                UserType = "Citizen"
            };

            await _context.Users.AddAsync(user);
            await _context.Citizens.AddAsync(citizen);
            await _context.Applications.AddAsync(application);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Reject(applicationId, decisionComment);

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("ViewAll"));

            // Проверяем что заявление обновилось в БД
            var updatedApp = await _context.Applications.FindAsync(applicationId);
            Assert.That(updatedApp.ApplicationStatus, Is.EqualTo("Отклонен"));
            Assert.That(updatedApp.DecisionComment, Is.EqualTo(decisionComment));
            Assert.That(updatedApp.AssignedEmployeeId, Is.EqualTo(employeeId));
        }

        // Тест 7: Попытка одобрения неавторизованным пользователем
        [Test]
        public async Task Approve_WhenUserNotEmployee_RedirectsToAccessDenied()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(false);
            _mockSessionService.Setup(s => s.IsAdmin()).Returns(false);

            // Act
            var result = await _controller.Approve(1, "Комментарий");

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("AccessDenied"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
        }

        // Тест 8: Попытка отклонения неавторизованным пользователем
        [Test]
        public async Task Reject_WhenUserNotEmployee_RedirectsToAccessDenied()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(false);
            _mockSessionService.Setup(s => s.IsAdmin()).Returns(false);

            // Act
            var result = await _controller.Reject(1, "Комментарий");

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("AccessDenied"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
        }

        // Тест 9: Просмотр своих заявлений (Citizen)
        [Test]
        public async Task MyApplications_WhenUserIsCitizen_ReturnsViewWithApplications()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var citizenId = 1;

            var applications = new List<Application>
            {
                new Application
                {
                    ApplicationId = 1,
                    ApplicantId = citizenId,
                    ApplicationDate = DateTime.Now.AddDays(-1),
                    ApplicationStatus = "Принят к проверке",
                    ApplicationType = "Регистрация",
                    Address = "Адрес 1",
                    Area = 100.5m,
                    CadastralObjectType = "Земельный участок",
                    CitizenComment = "Комментарий 1"
                },
                new Application
                {
                    ApplicationId = 2,
                    ApplicantId = citizenId,
                    ApplicationDate = DateTime.Now,
                    ApplicationStatus = "Одобрен",
                    ApplicationType = "Обновление",
                    Address = "Адрес 2",
                    Area = 150.7m,
                    CadastralObjectType = "Здание",
                    CitizenComment = "Комментарий 2",
                    DecisionComment = "Одобрено",
                    AssignedEmployeeId = 2
                }
            };

            await _context.Applications.AddRangeAsync(applications);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MyApplications();

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.Model, Is.InstanceOf<List<Application>>());

            var model = viewResult.Model as List<Application>;
            Assert.That(model, Has.Count.EqualTo(2));
            Assert.That(model[0].ApplicationDate, Is.GreaterThan(model[1].ApplicationDate));
        }

        // Тест 10: Попытка просмотра заявлений неавторизованным пользователем
        [Test]
        public async Task MyApplications_WhenUserNotAuthenticated_RedirectsToLogin()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(false);

            // Act
            var result = await _controller.MyApplications();

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("Login"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Account"));
        }
    }
}